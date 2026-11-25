using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshinaStudio {
 public partial class StudioApp {
  const int HomeLayer=30;
  Transform homeStage,homeBackdrop;Camera homeCamera;FieldWorld homeField;bool homeStageDirty=true;float homeOpenedAt;
  readonly List<Mesh> homeMeshes=new List<Mesh>();
  void HideHomeStage(){if(homeStage!=null)homeStage.gameObject.SetActive(false);}
  void InvalidateHomeStage(){homeStageDirty=true;homeOpenedAt=Time.unscaledTime;}
  static bool VisibleBelow(Transform node,Transform owner){for(var t=node;t!=null&&t!=owner;t=t.parent)if(!t.gameObject.activeSelf)return false;return true;}
  void ClearHomeBackdrop(){if(homeBackdrop!=null){homeBackdrop.gameObject.SetActive(false);Lifetime.Destroy(homeBackdrop.gameObject);}foreach(var mesh in homeMeshes)Lifetime.Destroy(mesh);homeMeshes.Clear();}
  void RebuildHomeBackdrop(){
   ClearHomeBackdrop();homeBackdrop=new GameObject("Arena presentation / visual snapshot").transform;homeBackdrop.SetParent(homeStage,false);homeBackdrop.gameObject.layer=HomeLayer;
   foreach(var source in Field.Root.GetComponentsInChildren<MeshFilter>(true)){
    var renderer=source.GetComponent<MeshRenderer>();if(source.GetComponent<TextMesh>()!=null||source.sharedMesh==null||renderer==null||!renderer.enabled||!VisibleBelow(source.transform,Field.Root))continue;
    var go=new GameObject(source.name);go.layer=HomeLayer;go.transform.SetParent(homeBackdrop,false);go.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);go.transform.localScale=source.transform.lossyScale;go.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;var copy=go.AddComponent<MeshRenderer>();copy.sharedMaterials=renderer.sharedMaterials;copy.shadowCastingMode=renderer.shadowCastingMode;copy.receiveShadows=renderer.receiveShadows;
   }
   foreach(var source in Field.Root.GetComponentsInChildren<TextMesh>(true)){
    var go=new GameObject("Event lettering");go.layer=HomeLayer;go.transform.SetParent(homeBackdrop,false);go.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);go.transform.localScale=source.transform.lossyScale;var copy=go.AddComponent<TextMesh>();copy.font=source.font;copy.fontSize=source.fontSize;copy.characterSize=source.characterSize;copy.anchor=source.anchor;copy.alignment=source.alignment;copy.color=source.color;copy.text=source.text;go.GetComponent<MeshRenderer>().sharedMaterial=copy.font.material;
   }
   // A single combined mesh per piece material keeps the 456-FUEL opening view inexpensive.
   var pieces=new Dictionary<Material,List<CombineInstance>>();foreach(var piece in Pieces.Active){if(piece==null||piece.Removed)continue;foreach(var source in piece.GetComponentsInChildren<MeshFilter>(true)){var renderer=source.GetComponent<MeshRenderer>();if(renderer==null||source.sharedMesh==null||renderer.sharedMaterial==null)continue;if(!pieces.TryGetValue(renderer.sharedMaterial,out var list))pieces.Add(renderer.sharedMaterial,list=new List<CombineInstance>());list.Add(new CombineInstance{mesh=source.sharedMesh,transform=source.transform.localToWorldMatrix});}}
   foreach(var pair in pieces){var mesh=new Mesh{name="Presentation game pieces",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);homeMeshes.Add(mesh);var go=new GameObject("Game-piece presentation");go.layer=HomeLayer;go.transform.SetParent(homeBackdrop,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;}
   foreach(var source in Field.Root.GetComponentsInChildren<Light>(true)){
    var go=new GameObject("Presentation arena light");go.layer=HomeLayer;go.transform.SetParent(homeBackdrop,false);go.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);var copy=go.AddComponent<Light>();copy.type=source.type;copy.color=source.color;copy.intensity=source.intensity;copy.range=source.range;copy.spotAngle=source.spotAngle;copy.innerSpotAngle=source.innerSpotAngle;copy.shadows=LightShadows.None;copy.cullingMask=1<<HomeLayer;
   }
   homeField=Field;homeStageDirty=false;
  }
  void UpdateHomeStage(){
   if(Field?.Root==null)return;
   if(homeStage==null){homeStage=new GameObject("Home arena presentation").transform;homeStage.gameObject.layer=HomeLayer;var go=new GameObject("Home cinematic camera");go.transform.SetParent(homeStage,false);homeCamera=go.AddComponent<Camera>();homeCamera.clearFlags=CameraClearFlags.SolidColor;homeCamera.backgroundColor=new Color(.035f,.045f,.065f);homeCamera.nearClipPlane=.12f;homeCamera.farClipPlane=120;homeCamera.cullingMask=1<<HomeLayer;homeCamera.allowHDR=false;homeCamera.fieldOfView=47;homeOpenedAt=Time.unscaledTime;}
   if(homeStageDirty||homeField!=Field)RebuildHomeBackdrop();homeStage.gameObject.SetActive(true);homeCamera.enabled=true;
   float width=(float)Field.Width,length=(float)Field.Length;var center=new Vector3(-width*.5f,1.8f,length*.5f);float slow=Environment.GetCommandLineArgs().Length>0&&Array.IndexOf(Environment.GetCommandLineArgs(),"--capture")>=0?0:Mathf.Sin((Time.unscaledTime-homeOpenedAt)*.045f)*.7f;
   homeCamera.transform.position=new Vector3(6.2f+slow*.2f,8.4f,-7.1f+slow*.15f);homeCamera.transform.LookAt(center);float fov=Screen.width/(float)Mathf.Max(1,Screen.height)<1.4f?57:52;var projection=Matrix4x4.Perspective(fov,Screen.width/(float)Mathf.Max(1,Screen.height),.12f,120);projection.m02=-.30f;homeCamera.projectionMatrix=projection;
  }
  void DisposeHomeStage(){ClearHomeBackdrop();if(homeStage!=null)Lifetime.Destroy(homeStage.gameObject);foreach(var texture in homeTextures)Lifetime.Destroy(texture);homeTextures.Clear();}
 }
}
