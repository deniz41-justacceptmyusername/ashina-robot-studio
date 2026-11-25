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
  public void EnterWorkspace(){if(Busy||Project==null)return;homeScreen=false;HideHomeStage();WorldVisible(true);Select(Selected);if(Project.network.autoConnect&&Nt==null)ConnectNetwork();GUI.FocusControl(null);}
  public void ShowHome(){if(Busy)return;SetRunning(false);PublishDriverStation();homeScreen=true;networkPanel=help=false;gizmo?.Select(null,false);WorldVisible(false);InvalidateHomeStage();}
  void RememberProject(string path){
   if(Environment.GetCommandLineArgs().Any(a=>a=="--self-test"||a.Contains("check")||a=="--capture"))return;
   try{var paths=JsonConvert.DeserializeObject<string[]>(PlayerPrefs.GetString("ashina.recentProjects","[]"))??new string[0];paths=new[]{Path.GetFullPath(path)}.Concat(paths).Where(p=>!IsTestProject(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();PlayerPrefs.SetString("ashina.recentProjects",JsonConvert.SerializeObject(paths));PlayerPrefs.Save();}catch{ }
  }
  static bool IsTestProject(string path){try{return File.Exists(Path.Combine(Path.GetDirectoryName(path),".ashina-test-data"));}catch{return false;}}
  async void StartNewRobot(){if(await LoadProject(RobotProject.Empty())){savedPath=null;EnterWorkspace();StartQuickSetup();}}
  async void LoadHomeProject(string path){
   if(Busy)return;try{var candidate=await Task.Run(()=>{var info=new FileInfo(path);if(info.Length>200000000)throw new Exception("Proje dosyası çok büyük.");return ProjectIO.Parse(File.ReadAllText(path));});if(await LoadProject(candidate)){savedPath=path;RememberProject(path);EnterWorkspace();}}catch(Exception e){Notice="Proje açılamadı: "+e.Message;}
  }
  void DrawHome(){DrawArenaHome();}

 }
}

