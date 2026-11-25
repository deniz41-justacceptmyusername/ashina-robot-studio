using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshinaStudio {
 // The venue is a set of combined, non-colliding meshes. The official field remains the physics world.
 public sealed class ArenaVenue : IDisposable {
  public const int Layer=29;
  public const int RoofLayer=28;
  public Transform Root {get;private set;}
  readonly List<Material> materials=new List<Material>();readonly List<Mesh> meshes=new List<Mesh>();
  readonly Dictionary<string,Batch> batches=new Dictionary<string,Batch>();
  sealed class Batch {public Material Material;public readonly List<Vector3> V=new List<Vector3>();public readonly List<int> T=new List<int>();}
  Batch Group(string name,Color color,float glow=0){if(batches.TryGetValue(name,out var b))return b;var mat=ModelLoader.Material(color);mat.enableInstancing=true;if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",.12f);if(glow>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*glow);}materials.Add(mat);b=new Batch{Material=mat};batches.Add(name,b);return b;}
  static void Quad(Batch b,Vector3 a,Vector3 c,Vector3 d,Vector3 e){int n=b.V.Count;b.V.AddRange(new[]{a,c,d,e});b.T.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
  static void Box(Batch b,Vector3 center,Vector3 size,Quaternion rotation){var p=new Vector3[8];for(int i=0;i<8;i++)p[i]=center+rotation*Vector3.Scale(size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));Quad(b,p[0],p[4],p[6],p[2]);Quad(b,p[1],p[3],p[7],p[5]);Quad(b,p[0],p[1],p[5],p[4]);Quad(b,p[2],p[6],p[7],p[3]);Quad(b,p[0],p[2],p[3],p[1]);Quad(b,p[4],p[5],p[7],p[6]);}
  static void Box(Batch b,Vector3 center,Vector3 size)=>Box(b,center,size,Quaternion.identity);
  static void Beam(Batch b,Vector3 a,Vector3 c,float thickness){var d=c-a;Box(b,(a+c)*.5f,new Vector3(thickness,thickness,d.magnitude),Quaternion.LookRotation(d));}
  public static ArenaVenue Create(Transform field,double fieldLength,double fieldWidth,string season){var venue=new ArenaVenue();venue.Build(field,(float)fieldLength,(float)fieldWidth,season);return venue;}
  void Build(Transform field,float length,float width,string season){
   Root=new GameObject("Regional arena / visual environment").transform;Root.SetParent(field,false);Root.gameObject.layer=Layer;
   float cx=-width/2,cz=length/2,span=width+23,depth=length+23,roof=12.5f;
   var floor=Group("Arena concrete",new Color(.105f,.125f,.15f));var wall=Group("Hall acoustic walls",new Color(.12f,.145f,.19f));var trim=Group("Wall panels",new Color(.18f,.21f,.26f));var steel=Group("Roof steel",new Color(.13f,.16f,.20f));var rails=Group("Rails",new Color(.42f,.47f,.53f));var riser=Group("Bleacher risers",new Color(.23f,.255f,.29f));
   var blue=Group("Blue seating",new Color(.075f,.21f,.43f));var red=Group("Red seating",new Color(.46f,.10f,.14f));var white=Group("LED luminaires",new Color(.83f,.91f,1),1.8f);var amber=Group("Warm aisle lights",new Color(.94f,.57f,.20f),1.1f);var teal=Group("Event accent",new Color(.035f,.36f,.43f),.25f);
   Box(floor,new Vector3(cx,-.20f,cz),new Vector3(span,.16f,depth));Box(wall,new Vector3(cx,roof/2,cz+depth/2),new Vector3(span,roof,.25f));Box(wall,new Vector3(cx-span/2,roof/2,cz),new Vector3(.25f,roof,depth));Box(wall,new Vector3(cx+span/2,roof/2,cz),new Vector3(.25f,roof,depth));Box(wall,new Vector3(cx,roof/2,cz-depth/2),new Vector3(span,roof,.25f));Box(Group("Ceiling",new Color(.12f,.145f,.19f)),new Vector3(cx,roof+.1f,cz),new Vector3(span,.2f,depth));
   // Tall wall bays, roof trusses and suspended LED fixtures establish an indoor scale.
   for(float z=cz-depth/2+1;z<cz+depth/2;z+=4){foreach(float x in new[]{cx-span/2+.2f,cx+span/2-.2f}){Box(trim,new Vector3(x,5.5f,z),new Vector3(.13f,10.7f,.16f));Box(trim,new Vector3(x,8.4f,z+1.8f),new Vector3(.12f,3.8f,3.25f));}}
   for(float z=-6;z<=length+6;z+=6){
    float left=cx-span/2+.5f,right=cx+span/2-.5f;
    Beam(steel,new Vector3(left,11.5f,z),new Vector3(right,11.5f,z),.18f);Beam(steel,new Vector3(left,12.25f,z),new Vector3(right,12.25f,z),.14f);
    for(float x=left;x<right-1;x+=1.5f){Beam(steel,new Vector3(x,11.5f,z),new Vector3(x+1.5f,12.25f,z),.065f);Beam(steel,new Vector3(x,12.25f,z),new Vector3(x+1.5f,11.5f,z),.065f);}
    foreach(float x in new[]{cx-4.7f,cx+4.7f}){Beam(steel,new Vector3(x,11.6f,z),new Vector3(x,10.6f,z),.045f);Box(steel,new Vector3(x,10.55f,z),new Vector3(2.3f,.15f,.72f));Box(white,new Vector3(x,10.46f,z),new Vector3(2.12f,.035f,.59f));}
   }
   foreach(float x in new[]{cx-span/2+.5f,cx+span/2-.5f,cx})Beam(steel,new Vector3(x,11.6f,-7),new Vector3(x,11.6f,length+7),.16f);
   foreach(int side in new[]{-1,1}){
    float start=side>0?2.7f:-width-2.7f;
    for(int row=0;row<6;row++){
     float x=start+side*row*.86f,y=.43f*(row+1);Box(riser,new Vector3(x,y-.12f,cz),new Vector3(.88f,.22f,length+4));Box(trim,new Vector3(x,y-.25f,cz),new Vector3(.86f,.43f,length+4));
     var seatColor=side>0?blue:red;
     int count=(int)((length+3)/.52f);for(int seat=0;seat<count;seat++){
      float z=-1.4f+seat*.52f;if(Mathf.Abs(z-cz)<.85f)continue;var at=new Vector3(x,y+.12f,z);Box(seatColor,at,new Vector3(.43f,.07f,.43f));Box(seatColor,at+Vector3.up*.21f+Vector3.right*(side*.19f),new Vector3(.06f,.38f,.43f));
     }
     Box(amber,new Vector3(x-side*.42f,y-.05f,cz),new Vector3(.026f,.035f,1.1f));
    }
    foreach(float z in new[]{-1.9f,cz-.75f,cz+.75f,length+1.9f}){
     for(int row=0;row<6;row++){float x=start+side*row*.86f,y=.43f*(row+1);Beam(rails,new Vector3(x,y,z),new Vector3(x,y+1.02f,z),.035f);if(row<5)Beam(rails,new Vector3(x,y+1.02f,z),new Vector3(x+side*.86f,y+1.45f,z),.04f);}
    }
    // Low crowd-control rails leave a clear field-side aisle.
    float railX=side>0?1.25f:-width-1.25f;for(float z=-.5f;z<length+1;z+=2){Beam(rails,new Vector3(railX,0,z),new Vector3(railX,1.05f,z),.045f);if(z+2<length+1)Beam(rails,new Vector3(railX,1.02f,z),new Vector3(railX,1.02f,z+2),.04f);}
    Box(teal,new Vector3(side>0?cx+span/2-.3f:cx-span/2+.3f,5.2f,cz),new Vector3(.06f,.75f,length+3));
   }
   // A scoring desk and event signage belong outside the official field perimeter.
   var desk=Group("Field crew desk",new Color(.055f,.085f,.13f));float deskZ=length+3.4f;Box(desk,new Vector3(cx,.63f,deskZ),new Vector3(6.6f,1.15f,.9f));Box(teal,new Vector3(cx,.57f,deskZ-.46f),new Vector3(6.35f,.55f,.025f));
   for(int i=0;i<5;i++){float x=cx-2.5f+i*1.25f;Box(steel,new Vector3(x,1.39f,deskZ-.15f),new Vector3(.62f,.42f,.07f));Box(white,new Vector3(x,1.39f,deskZ-.195f),new Vector3(.56f,.35f,.012f));}
   Box(desk,new Vector3(cx,6.9f,length+6.1f),new Vector3(7.8f,2.0f,.22f));Box(teal,new Vector3(cx,5.94f,length+5.97f),new Vector3(7.8f,.06f,.04f));
   AddText("ROBOTICS REGIONAL",new Vector3(cx,7.19f,length+5.95f),.10f,46,Color.white);AddText("ASHINA  /  PRACTICE ARENA",new Vector3(cx,6.55f,length+5.95f),.055f,36,new Color(.43f,.75f,.79f));
   foreach(var pair in batches){var mesh=new Mesh{name=pair.Key,indexFormat=pair.Value.V.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(pair.Value.V);mesh.SetTriangles(pair.Value.T,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);var go=new GameObject(pair.Key);go.layer=pair.Key=="Ceiling"||pair.Key=="Roof steel"||pair.Key=="LED luminaires"?RoofLayer:Layer;go.transform.SetParent(Root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=pair.Value.Material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;}
   for(int i=0;i<2;i++){var go=new GameObject("Arena overhead wash "+(i+1));go.layer=Layer;go.transform.SetParent(Root,false);go.transform.position=new Vector3(cx,10.2f,length*(i==0?.24f:.76f));go.transform.rotation=Quaternion.Euler(90,0,0);var light=go.AddComponent<Light>();light.type=LightType.Spot;light.color=new Color(.88f,.94f,1);light.intensity=1.15f;light.range=23;light.spotAngle=112;light.innerSpotAngle=76;light.shadows=LightShadows.None;}
  }
  void AddText(string text,Vector3 position,float characterSize,int fontSize,Color color){var go=new GameObject("Event sign");go.layer=Layer;go.transform.SetParent(Root,false);go.transform.position=position;var label=go.AddComponent<TextMesh>();label.text=text;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=fontSize;label.characterSize=characterSize;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=color;go.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;go.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;}
  public void Dispose(){if(Root!=null){Root.gameObject.SetActive(false);Lifetime.Destroy(Root.gameObject);}foreach(var mesh in meshes)Lifetime.Destroy(mesh);foreach(var material in materials)Lifetime.Destroy(material);meshes.Clear();materials.Clear();batches.Clear();}
 }
}
