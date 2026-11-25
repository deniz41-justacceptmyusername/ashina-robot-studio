using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  readonly List<Texture2D> homeTextures=new List<Texture2D>();
  GUIStyle homePanelStyle,homeShadowStyle,homePrimaryStyle,homeSecondaryStyle,homeEyebrowStyle,homeHeadingStyle,homeSubtitleStyle,homeProjectStyle,homeLinkStyle,homeArrowStyle,homeBrandStyle,homeBadgeStyle,homeSmallStyle;
  Texture2D homeFade;bool homeProjectsOpen;string[] homeRecentPaths;
  Texture2D HomeSurface(Color top,Color bottom,Color edge,float radius=13){
   const int size=64;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Home UI surface",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
   for(int y=0;y<size;y++)for(int x=0;x<size;x++){float qx=Mathf.Abs(x+.5f-size/2f)-(size/2f-radius),qy=Mathf.Abs(y+.5f-size/2f)-(size/2f-radius);float distance=new Vector2(Mathf.Max(qx,0),Mathf.Max(qy,0)).magnitude+Mathf.Min(Mathf.Max(qx,qy),0)-radius;var c=Color.Lerp(bottom,top,y/(size-1f));if(distance> -1.1f)c=Color.Lerp(c,edge,Mathf.Clamp01(distance+1.1f));c.a*=Mathf.Clamp01(.5f-distance);tex.SetPixel(x,y,c);}tex.Apply(false,true);homeTextures.Add(tex);return tex;
  }
  GUIStyle HomeBox(Texture2D texture)=>new GUIStyle{normal={background=texture},border=new RectOffset(14,14,14,14)};
  void HomeMenuStyles(){
   if(homePanelStyle!=null)return;
   homePanelStyle=HomeBox(HomeSurface(new Color(.055f,.085f,.12f,.89f),new Color(.035f,.055f,.085f,.80f),new Color(.39f,.59f,.68f,.30f),16));
   homeShadowStyle=HomeBox(HomeSurface(new Color(0,0,0,.20f),new Color(0,0,0,.35f),Color.clear,17));
   var primary=HomeSurface(new Color(.06f,.49f,.57f,.98f),new Color(.035f,.34f,.43f,.98f),new Color(.36f,.85f,.86f,.63f));var primaryHover=HomeSurface(new Color(.08f,.59f,.66f),new Color(.04f,.42f,.51f),new Color(.5f,.96f,.94f,.9f));
   var secondary=HomeSurface(new Color(.15f,.20f,.26f,.87f),new Color(.09f,.135f,.19f,.86f),new Color(.50f,.61f,.68f,.24f));var secondaryHover=HomeSurface(new Color(.20f,.28f,.34f,.95f),new Color(.13f,.21f,.27f,.95f),new Color(.47f,.77f,.80f,.55f));
   homePrimaryStyle=new GUIStyle(GUI.skin.button){border=new RectOffset(14,14,14,14),alignment=TextAnchor.MiddleLeft,fontStyle=FontStyle.Bold,padding=new RectOffset(21,40,9,9)};homePrimaryStyle.normal.background=primary;homePrimaryStyle.normal.textColor=Color.white;homePrimaryStyle.hover.background=primaryHover;homePrimaryStyle.hover.textColor=Color.white;homePrimaryStyle.active.background=primary;homePrimaryStyle.active.textColor=Color.white;homePrimaryStyle.focused.background=primaryHover;homePrimaryStyle.focused.textColor=Color.white;
   homeSecondaryStyle=new GUIStyle(homePrimaryStyle){fontStyle=FontStyle.Normal};homeSecondaryStyle.normal.background=secondary;homeSecondaryStyle.hover.background=secondaryHover;homeSecondaryStyle.active.background=secondary;homeSecondaryStyle.focused.background=secondaryHover;
   homeEyebrowStyle=new GUIStyle(GUI.skin.label){normal={textColor=new Color(.45f,.77f,.80f)},fontStyle=FontStyle.Bold};
   homeHeadingStyle=new GUIStyle(GUI.skin.label){normal={textColor=new Color(.96f,.98f,1)},fontStyle=FontStyle.Bold};
   homeSubtitleStyle=new GUIStyle(GUI.skin.label){normal={textColor=new Color(.65f,.73f,.80f)},wordWrap=true};
   homeProjectStyle=new GUIStyle(GUI.skin.label){normal={textColor=new Color(.67f,.83f,.84f)},clipping=TextClipping.Clip};
   homeLinkStyle=new GUIStyle(GUI.skin.button){normal={textColor=new Color(.69f,.78f,.83f)},hover={textColor=Color.white},active={textColor=new Color(.43f,.85f,.85f)},alignment=TextAnchor.MiddleLeft,padding=new RectOffset(0,0,3,3)};
   homeLinkStyle.normal.background=homeLinkStyle.hover.background=homeLinkStyle.active.background=homeLinkStyle.focused.background=null;homeLinkStyle.border=new RectOffset();
   homeArrowStyle=new GUIStyle(GUI.skin.label){normal={textColor=new Color(.77f,.94f,.95f)},alignment=TextAnchor.MiddleRight};
   homeBrandStyle=new GUIStyle(homeHeadingStyle);homeBadgeStyle=new GUIStyle(homeProjectStyle){alignment=TextAnchor.MiddleRight};homeSmallStyle=new GUIStyle(homeSubtitleStyle){alignment=TextAnchor.MiddleRight};
   homeFade=new Texture2D(256,1,TextureFormat.RGBA32,false){name="Home stage vignette",wrapMode=TextureWrapMode.Clamp};for(int x=0;x<256;x++){float t=x/255f;homeFade.SetPixel(x,0,new Color(.015f,.025f,.042f,Mathf.Pow(1-t,2.8f)*.77f));}homeFade.Apply(false,true);homeTextures.Add(homeFade);
  }
  bool HomeButton(Rect rect,string caption,bool primary){bool clicked=GUI.Button(rect,caption,primary?homePrimaryStyle:homeSecondaryStyle);GUI.Label(new Rect(rect.xMax-39,rect.y,22,rect.height),"›",homeArrowStyle);return clicked;}
  void DrawArenaHome(){
   HomeMenuStyles();float s=Mathf.Clamp(Screen.height/900f,.75f,1.14f),margin=32*s;float width=416*s,height=548*s;var card=new Rect(margin,Screen.height-margin-height,width,height);
   homePrimaryStyle.fontSize=Mathf.RoundToInt(19*s);homeSecondaryStyle.fontSize=Mathf.RoundToInt(18*s);homeEyebrowStyle.fontSize=Mathf.RoundToInt(11*s);homeHeadingStyle.fontSize=Mathf.RoundToInt(32*s);homeSubtitleStyle.fontSize=Mathf.RoundToInt(14*s);homeProjectStyle.fontSize=Mathf.RoundToInt(12*s);homeLinkStyle.fontSize=Mathf.RoundToInt(13*s);homeArrowStyle.fontSize=Mathf.RoundToInt(27*s);homeBrandStyle.fontSize=Mathf.RoundToInt(25*s);homeBadgeStyle.fontSize=Mathf.RoundToInt(14*s);homeSmallStyle.fontSize=Mathf.RoundToInt(10*s);
   GUI.DrawTexture(new Rect(0,0,Screen.width*.83f,Screen.height),homeFade,ScaleMode.StretchToFill,true);
   if(brandIcon!=null)GUI.DrawTexture(new Rect(margin,margin,58*s,58*s),brandIcon,ScaleMode.ScaleToFit,true);
   GUI.Label(new Rect(margin+73*s,margin+2*s,360*s,36*s),"ASHINA",homeBrandStyle);GUI.Label(new Rect(margin+75*s,margin+37*s,330*s,25*s),"ROBOT STUDIO",homeEyebrowStyle);
   GUI.Label(new Rect(Screen.width-margin-190*s,margin,190*s,22*s),"v"+Application.version,homeSmallStyle);
   GUI.Box(new Rect(card.x-8*s,card.y+8*s,card.width+16*s,card.height+8*s),GUIContent.none,homeShadowStyle);GUI.Box(card,GUIContent.none,homePanelStyle);
   Rect R(float x,float y,float w,float h)=>new Rect(card.x+x*s,card.y+y*s,w*s,h*s);
   GUI.Label(R(26,25,364,20),"PİT ALANI  /  ANA MENÜ",homeEyebrowStyle);GUI.Label(R(24,49,368,47),"Sahaya hazır.",homeHeadingStyle);GUI.Label(R(26,100,364,25),"Robotunu seç, antrenmana geç.",homeSubtitleStyle);
   string name=Project?.name??"Robot hazırlanıyor…";if(name.Length>43)name=name.Substring(0,40)+"…";GUI.Label(R(26,136,364,23),new GUIContent("●  "+name,Project?.name??""),homeProjectStyle);
   GUI.enabled=!Busy&&Project!=null&&!roomPanel;
   if(HomeButton(R(25,169,366,64),"Sahaya devam et",true)){homeProjectsOpen=false;EnterWorkspace();}
   if(HomeButton(R(25,243,366,56),"Robot projesi aç",false)){homeProjectsOpen=false;Open();}
   if(HomeButton(R(25,309,366,56),"Yeni robot hazırla",false)){homeProjectsOpen=false;StartNewRobot();}
   if(HomeButton(R(25,375,366,56),"Çevrimiçi maç",false)){homeProjectsOpen=false;roomPanel=true;fields.Clear();GUI.FocusControl(null);}
   if(GUI.Button(R(27,447,364,29),homeProjectsOpen?"Örnekler ve son projeler   ‹":"Örnekler ve son projeler   ›",homeLinkStyle)){homeProjectsOpen=!homeProjectsOpen;homeRecentPaths=null;}
   GUI.enabled=true;
   string status=Busy?"Robot ve saha hazırlanıyor…":"Klavye, kumanda veya WPILib ile antrenman.";
   if(!Busy&&!string.IsNullOrEmpty(Notice)&&new[]{"açılamadı","hata","bulunamadı","geçersiz"}.Any(k=>Notice.IndexOf(k,StringComparison.OrdinalIgnoreCase)>=0))status=Notice;
   GUI.Label(R(26,491,364,40),status,homeSubtitleStyle);
   string season=Project?.season=="2026"?"2026  /  REBUILT":Project?.season=="2025"?"2025  /  REEFSCAPE":Project?.season=="2024"?"2024  /  CRESCENDO":"PRACTICE FIELD";
   GUI.Box(new Rect(Screen.width-margin-246*s,Screen.height-margin-57*s,246*s,57*s),GUIContent.none,homePanelStyle);GUI.Label(new Rect(Screen.width-margin-232*s,Screen.height-margin-47*s,216*s,24*s),season,homeBadgeStyle);GUI.Label(new Rect(Screen.width-margin-232*s,Screen.height-margin-22*s,216*s,21*s),"REGIONAL PRACTICE ARENA",homeSmallStyle);
   if(homeProjectsOpen&&!roomPanel)DrawHomeProjects(card,s,margin);
  }
  void DrawHomeProjects(Rect card,float s,float margin){
   if(homeRecentPaths==null){homeRecentPaths=new string[0];if(!Environment.GetCommandLineArgs().Contains("--capture"))try{homeRecentPaths=(JsonConvert.DeserializeObject<string[]>(PlayerPrefs.GetString("ashina.recentProjects","[]"))??new string[0]).Where(p=>File.Exists(p)&&!IsTestProject(p)).Take(3).ToArray();}catch{ }}
   float width=330*s,height=(homeRecentPaths.Length==0?190:158+homeRecentPaths.Length*40)*s;var rect=new Rect(Mathf.Min(card.xMax+16*s,Screen.width-margin-width),card.yMax-height,width,height);GUI.Box(rect,GUIContent.none,homePanelStyle);
   GUILayout.BeginArea(new Rect(rect.x+20*s,rect.y+16*s,rect.width-40*s,rect.height-30*s));GUILayout.Label("PROJELER",homeEyebrowStyle);GUILayout.Space(10*s);
   if(GUILayout.Button("Hedefli atış örneği",homeSecondaryStyle,GUILayout.Height(42*s))){homeProjectsOpen=false;LoadHomeProject(Path.Combine(Application.streamingAssetsPath,"examples/Target-Practice.ashina.json"));}
   GUILayout.Space(12*s);GUILayout.Label("SON PROJELER",homeEyebrowStyle);if(homeRecentPaths.Length==0)GUILayout.Label("Kaydettiğin projeler burada görünür.",homeSubtitleStyle);
   foreach(var path in homeRecentPaths)if(GUILayout.Button(Path.GetFileNameWithoutExtension(path),homeLinkStyle,GUILayout.Height(36*s))){homeProjectsOpen=false;LoadHomeProject(path);}GUILayout.EndArea();
  }
 }
}
