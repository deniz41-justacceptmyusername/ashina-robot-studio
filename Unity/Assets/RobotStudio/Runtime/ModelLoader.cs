using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using UnityEngine;
using GLTFast;

namespace AshinaStudio {
 public static class Lifetime { public static void Destroy(UnityEngine.Object obj){if(obj==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj);} }
 public class PartNode : MonoBehaviour {public string partId;public Transform content;}
 public class ModelLoader : IDisposable {
  readonly Dictionary<string,GameObject> cache=new Dictionary<string,GameObject>();readonly List<GltfImport> imports=new List<GltfImport>();readonly Transform hidden;
  public ModelLoader(){hidden=new GameObject("Model kaynakları").transform;hidden.gameObject.SetActive(false);}
  public async Task<GameObject> Source(string id,ModelAsset asset)
  {
   if(cache.TryGetValue(id,out var existing))return existing;
   // Disk decoding, text parsing and validation do not occupy Unity's window thread.
   byte[] bytes=await Task.Run(()=>{var data=Convert.FromBase64String(asset.data);if(data.Length>125000000)throw new Exception("Model 125 MB sınırını aşıyor.");return data;});
   Geometry geometry=null;
   if(asset.type=="glb")await Task.Run(()=>ValidateGlb(bytes));
   else geometry=await Task.Run(()=>asset.type=="stl"?ParseStl(bytes):ParseObj(Encoding.UTF8.GetString(bytes)));
   var root=new GameObject(asset.name??"Model");root.transform.SetParent(hidden,false);GltfImport import=null;
   try
   {
    if(asset.type=="glb")
    {
     import=new GltfImport();
     if(!await import.LoadGltfBinary(bytes))throw new Exception("GLB yüklenemedi. Dokuları gömülü, sıkıştırmasız GLB dışa aktarımını deneyin.");
     var basis=new GameObject("FRC koordinatı").transform;basis.SetParent(root.transform,false);basis.localRotation=Quaternion.LookRotation(Vector3.up,Vector3.left);
     if(!await import.InstantiateMainSceneAsync(basis))throw new Exception("GLB sahnesi oluşturulamadı.");
     if(root.GetComponentsInChildren<MeshFilter>(true).Length==0&&import.SceneCount>0)await import.InstantiateSceneAsync(basis,0);
     if(root.GetComponentsInChildren<MeshFilter>(true).Length==0)throw new Exception("Bu modelde görüntülenecek geometri yok.");
     imports.Add(import);
    }
    else
    {
     var go=new GameObject("Geometri");go.transform.SetParent(root.transform,false);
     go.AddComponent<MeshFilter>().sharedMesh=Mesh(geometry.Vertices,geometry.Indices);
     go.AddComponent<MeshRenderer>().sharedMaterial=Material(new Color(.63f,.72f,.8f));
    }
    foreach(var r in root.GetComponentsInChildren<Renderer>(true)){foreach(var m in r.sharedMaterials){if(m==null)continue;if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",Mathf.Min(m.GetFloat("_Metallic"),.3f));}r.receiveShadows=true;}
    cache[id]=root;return root;
   }
   catch {if(import!=null){imports.Remove(import);import.Dispose();}Lifetime.Destroy(root);throw;}
  }
  static void ValidateGlb(byte[] bytes)
  {
   if(bytes.Length<20||BitConverter.ToUInt32(bytes,0)!=0x46546c67||BitConverter.ToUInt32(bytes,4)!=2||BitConverter.ToUInt32(bytes,8)!=bytes.Length||BitConverter.ToUInt32(bytes,16)!=0x4e4f534a)throw new Exception("GLB dosyası eksik veya başlığı geçersiz.");
   uint length=BitConverter.ToUInt32(bytes,12);if(length>bytes.Length-20)throw new Exception("GLB veri bölümü eksik.");
   var json=Newtonsoft.Json.Linq.JObject.Parse(Encoding.UTF8.GetString(bytes,20,(int)length));
   foreach(var token in new[]{json["buffers"],json["images"]}.Where(x=>x!=null))foreach(var entry in token){var uri=(string)entry["uri"];if(uri!=null&&!uri.StartsWith("data:"))throw new Exception("GLB dış dosya istiyor. Dokuları ve geometrileri GLB içine gömün.");}
  }
  public async Task<GameObject> Visual(RobotPart part,Dictionary<string,ModelAsset> assets){var result=new GameObject(part.name+" görünümü");try{if(part.kind=="asset"){var source=await Source(part.asset,assets[part.asset]);if(part.meshIndex.HasValue){var filters=source.GetComponentsInChildren<MeshFilter>(true);if(part.meshIndex<0||part.meshIndex>=filters.Length)throw new Exception("Montaj alt parçası bulunamadı.");var selected=filters[part.meshIndex.Value];var matrix=source.transform.worldToLocalMatrix*selected.transform.localToWorldMatrix;var mesh=new GameObject(selected.name);mesh.transform.SetParent(result.transform,false);mesh.transform.localPosition=matrix.GetColumn(3);mesh.transform.localRotation=matrix.rotation;mesh.transform.localScale=matrix.lossyScale;mesh.AddComponent<MeshFilter>().sharedMesh=selected.sharedMesh;var renderer=mesh.AddComponent<MeshRenderer>();renderer.sharedMaterials=selected.GetComponent<Renderer>().sharedMaterials;}else{var instance=UnityEngine.Object.Instantiate(source,result.transform,false);instance.SetActive(true);instance.transform.localPosition=Vector3.zero;instance.transform.localRotation=Quaternion.identity;}}
   else if(part.kind!="group"){var shape=GameObject.CreatePrimitive(part.kind=="cylinder"?PrimitiveType.Cylinder:PrimitiveType.Cube);shape.transform.SetParent(result.transform,false);shape.transform.localScale=part.kind=="cylinder"?new Vector3((float)part.size[0],(float)part.size[2]/2,(float)part.size[0]):new Vector3((float)part.size[1],(float)part.size[2],(float)part.size[0]);Lifetime.Destroy(shape.GetComponent<Collider>());ColorUtility.TryParseHtmlString(part.color,out var color);shape.GetComponent<Renderer>().sharedMaterial=Material(color);}
   result.transform.localScale=Vector3.one*(float)part.scale;if(part.kind=="asset"&&part.assetCenter!=null)result.transform.localPosition=-Frc.Position(part.assetCenter)*(float)part.scale;
   // Picking bounds avoid cooking a CAD collision mesh whenever the user adds a part.
   foreach(var filter in result.GetComponentsInChildren<MeshFilter>(true)){if(filter.sharedMesh==null)continue;var bounds=filter.sharedMesh.bounds;var c=filter.gameObject.AddComponent<BoxCollider>();c.center=bounds.center;c.size=Vector3.Max(bounds.size,Vector3.one*.002f);c.enabled=false;filter.gameObject.layer=9;}return result;}catch{Lifetime.Destroy(result);throw;}
  }
  public static Material Material(Color color){var shader=Shader.Find("Standard")??Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Diffuse");var m=new Material(shader){color=color};if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",.28f);return m;}
  sealed class Geometry {public Vector3[] Vertices;public int[] Indices;}
  public static Mesh ReadStl(byte[] bytes){var geometry=ParseStl(bytes);return Mesh(geometry.Vertices,geometry.Indices);}
  public static Mesh ReadObj(string text){var geometry=ParseObj(text);return Mesh(geometry.Vertices,geometry.Indices);}
  static Geometry ParseStl(byte[] bytes)
  {
   var vertices=new List<Vector3>();uint count=bytes.Length>=84?BitConverter.ToUInt32(bytes,80):0;
   if(count>0&&84L+count*50L==bytes.Length)
   {
    if(count>500000)throw new Exception("STL fazla ayrıntılı (500 bin üçgen sınırı).");
    vertices.Capacity=(int)count*3;
    for(int i=0;i<count;i++)for(int v=0;v<3;v++){int o=84+i*50+12+v*12;vertices.Add(Frc.Position(new[]{(double)BitConverter.ToSingle(bytes,o),(double)BitConverter.ToSingle(bytes,o+4),(double)BitConverter.ToSingle(bytes,o+8)}));}
   }
   else
   {
    foreach(Match match in Regex.Matches(Encoding.UTF8.GetString(bytes),@"vertex\s+([-+\deE.]+)\s+([-+\deE.]+)\s+([-+\deE.]+)",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(10)))
    {if(vertices.Count>=4000000)throw new Exception("STL fazla ayrıntılı (500 bin üçgen sınırı).");vertices.Add(Frc.Position(new[]{double.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture),double.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture),double.Parse(match.Groups[3].Value,CultureInfo.InvariantCulture)}));}
   }
   ValidateVertices(vertices,"STL");if(vertices.Count%3!=0)throw new Exception("STL üçgenleri eksik.");
   var indices=new int[vertices.Count];for(int i=0;i<indices.Length;i+=3){indices[i]=i;indices[i+1]=i+2;indices[i+2]=i+1;}
   return new Geometry{Vertices=vertices.ToArray(),Indices=indices};
  }
  static Geometry ParseObj(string text)
  {
   var vertices=new List<Vector3>();var triangles=new List<int>();
   using(var reader=new StringReader(text))for(string line;(line=reader.ReadLine())!=null;)
   {
    int comment=line.IndexOf('#');if(comment>=0)line=line.Substring(0,comment);var t=line.Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);if(t.Length==0)continue;
    if(t[0]=="v"&&t.Length>=4){if(vertices.Count>=1500000)throw new Exception("OBJ fazla ayrıntılı.");vertices.Add(Frc.Position(new[]{double.Parse(t[1],CultureInfo.InvariantCulture),double.Parse(t[2],CultureInfo.InvariantCulture),double.Parse(t[3],CultureInfo.InvariantCulture)}));}
    if(t[0]=="f"&&t.Length>=4)
    {
     int Parse(string token){int value=int.Parse(token.Split('/')[0],CultureInfo.InvariantCulture);int index=value<0?vertices.Count+value:value-1;if(index<0||index>=vertices.Count)throw new Exception("OBJ yüz indisi geçersiz.");return index;}
     int first=Parse(t[1]);for(int i=2;i<t.Length-1;i++){if(t[i].StartsWith("#"))break;if(triangles.Count>=6000000)throw new Exception("OBJ fazla ayrıntılı (2 milyon üçgen sınırı).");triangles.Add(first);triangles.Add(Parse(t[i+1]));triangles.Add(Parse(t[i]));}
    }
   }
   ValidateVertices(vertices,"OBJ");if(triangles.Count==0)throw new Exception("OBJ içinde üçgen bulunamadı.");
   return new Geometry{Vertices=vertices.ToArray(),Indices=triangles.ToArray()};
  }
  static void ValidateVertices(List<Vector3> vertices,string type)
  {if(vertices.Count==0||vertices.Any(v=>!Valid(v.x)||!Valid(v.y)||!Valid(v.z)))throw new Exception(type+" geometrisi boş veya geçersiz.");}
  static bool Valid(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value)&&Math.Abs(value)<100000000;
  public static Mesh Mesh(Vector3[] vertices,int[] triangles){var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
  public void Dispose(){foreach(var import in imports)import.Dispose();if(hidden!=null)Lifetime.Destroy(hidden.gameObject);}
 }
}



