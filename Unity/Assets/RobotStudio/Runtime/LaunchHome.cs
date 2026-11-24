using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  bool homeScreen=true;public bool HomeVisible=>homeScreen;
  void WorldVisible(bool visible){if(ViewCamera!=null)ViewCamera.enabled=visible;if(Robot!=null)Robot.gameObject.SetActive(visible);if(Field?.Root!=null)Field.Root.gameObject.SetActive(visible);foreach(var p in Pieces.Active)if(p!=null)p.gameObject.SetActive(visible);}
  public void EnterWorkspace(){if(Busy||Project==null)return;homeScreen=false;WorldVisible(true);Select(Selected);if(Project.network.autoConnect&&Nt==null)ConnectNetwork();GUI.FocusControl(null);}
  public void ShowHome(){if(Busy)return;SetRunning(false);PublishDriverStation();homeScreen=true;networkPanel=help=false;gizmo?.Select(null,false);WorldVisible(false);}
  void RememberProject(string path){
   if(Environment.GetCommandLineArgs().Any(a=>a=="--self-test"||a.Contains("check")||a=="--capture"))return;
   try{var paths=JsonConvert.DeserializeObject<string[]>(PlayerPrefs.GetString("ashina.recentProjects","[]"))??new string[0];paths=new[]{Path.GetFullPath(path)}.Concat(paths).Where(p=>!IsTestProject(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();PlayerPrefs.SetString("ashina.recentProjects",JsonConvert.SerializeObject(paths));PlayerPrefs.Save();}catch{ }
  }
  static bool IsTestProject(string path){try{return File.Exists(Path.Combine(Path.GetDirectoryName(path),".ashina-test-data"));}catch{return false;}}
  async void StartNewRobot(){if(await LoadProject(RobotProject.Empty())){savedPath=null;EnterWorkspace();StartQuickSetup();}}
  async void LoadHomeProject(string path){
   if(Busy)return;try{var candidate=await Task.Run(()=>{var info=new FileInfo(path);if(info.Length>200000000)throw new Exception("Proje dosyası çok büyük.");return ProjectIO.Parse(File.ReadAllText(path));});if(await LoadProject(candidate)){savedPath=path;RememberProject(path);EnterWorkspace();}}catch(Exception e){Notice="Proje açılamadı: "+e.Message;}
  }
  void DrawHome(){
   GUI.Box(new Rect(0,0,Screen.width,Screen.height),"",panel);
   float width=Mathf.Min(870,Screen.width-56),x=(Screen.width-width)/2,y=Mathf.Max(32,(Screen.height-620)/2);
   GUILayout.BeginArea(new Rect(x,y,width,Screen.height-y-25));
   GUILayout.BeginHorizontal();if(brandIcon!=null)GUILayout.Label(brandIcon,GUILayout.Width(72),GUILayout.Height(72));GUILayout.BeginVertical();GUILayout.Space(8);GUILayout.Label("ASHINA ROBOT STUDIO",title);Text("Robotunu hazırla. Kodunu bağla. Sahaya çık.",true);Text("Masaüstü · "+Application.version,true);GUILayout.EndVertical();GUILayout.EndHorizontal();GUILayout.Space(25);
   GUI.enabled=!Busy&&Project!=null;
   GUILayout.BeginHorizontal();GUILayout.BeginVertical(GUILayout.Width(width*.53f));
   if(Button("Yeni robot hazırla"))StartNewRobot();Text("Tek ana modelle başla; yalnız ihtiyaç duyduğun mekanizmaları ekle.",true);GUILayout.Space(12);
   if(Button("Robot projesi aç"))Open();Text("Kaydettiğin robot, eylemler ve bağlantılar birlikte açılır.",true);GUILayout.Space(12);
   if(Button("Hedefli atış örneğini incele"))LoadHomeProject(Path.Combine(Application.streamingAssetsPath,"examples/Target-Practice.ashina.json"));Text("Sabit açı, maksimum güç, saçılma ve bölgeye göre feedleme.",true);
   GUILayout.EndVertical();GUILayout.Space(30);GUILayout.BeginVertical();
   Text("HAZIR PROJE",true);Text(Project?.name??"Hazırlanıyor…");GUILayout.Space(8);if(Button("Düzenlemeye / antrenmana devam et",true))EnterWorkspace();
   GUILayout.Space(22);Text("SON PROJELER",true);
   if(!Environment.GetCommandLineArgs().Contains("--capture"))try{foreach(var path in (JsonConvert.DeserializeObject<string[]>(PlayerPrefs.GetString("ashina.recentProjects","[]"))??new string[0]).Where(p=>File.Exists(p)&&!IsTestProject(p)).Take(3))if(Button(Path.GetFileNameWithoutExtension(path)))LoadHomeProject(path);}catch{ }
   GUILayout.EndVertical();GUILayout.EndHorizontal();GUI.enabled=true;GUILayout.Space(26);
   Text(Busy?"Proje hazırlanıyor…":string.IsNullOrEmpty(Notice)?"Önce robotunu seç; sürüşü çalışma alanından başlat.":Notice,true);
   GUILayout.Space(12);Text("Hızlı kurulum: model → hareketli parçalar → açı / hız / tetikleyici → deneme.",true);
   GUILayout.EndArea();
  }
 }
}

