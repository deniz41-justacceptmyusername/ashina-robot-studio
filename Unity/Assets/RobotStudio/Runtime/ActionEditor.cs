using System;
using System.Linq;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  int shotStep;
  string shotGuideId;
  bool shotAdvanced,shotLive;
  void ChangeEditorTab(int tab){
   if(uiTab==tab)return;
   ClearActionAnchor(true);CancelTargetPick();uiTab=tab;propertyScroll=Vector2.zero;
   RestoreEditorGizmo();
  }

  void CancelTargetPick(){pickingAction=null;pendingZone=null;}

  RobotAction SelectedAction()=>uiTab==1?Project?.actions.Find(action=>action.id==chosenAction):null;
  bool PartEditingActive(){var action=SelectedAction();return actionAnchor==null&&pickingAction==null&&(action==null||action.type=="move");}

  void RestoreEditorGizmo(){
   if(gizmo==null||Running)return;
   var action=SelectedAction();
   if(action?.type=="capture")EditActionAnchor(action,resizingIntake);
   else if(action?.type=="emit")gizmo.Select(null,false);
   else gizmo.Select(Nodes.TryGetValue(Selected,out var node)?node.transform:null,rotateGizmo);
  }

  void SelectRobotAction(RobotAction action){
   if(action==null)return;
   ClearActionAnchor(true);CancelTargetPick();uiTab=1;chosenAction=action.id;
   Select(action.partId);
   if(shotGuideId!=action.id){shotGuideId=action.id;shotStep=0;shotAdvanced=false;shotLive=false;}
   propertyScroll=Vector2.zero;
  }

  void ChangeActionMount(RobotAction action){
   string mount=Cycle("Bağlı parça",action.partId,Project.parts.Select(part=>part.id).ToArray(),Project.parts.Select(part=>part.name).ToArray());
   if(mount==action.partId)return;
   ClearActionAnchor(true);action.partId=mount;Select(mount);
  }

  void FocusActionArea(RobotAction action){
   follow=false;cameraTarget=Nodes[action.partId].content.TransformPoint(Frc.Position(action.spawn));orbitDistance=2.6f;
  }

  void DrawIntakeEditor(RobotAction action){
   action.name=Edit("actionname",action.name);ChangeActionMount(action);
   Section("TOPLAMA ALANI");Text("Yeşil alan, topun nerede tutulacağını gösterir.",true);
   GUILayout.BeginHorizontal();
   if(Button("Alanı taşı",actionAnchor!=null&&!resizingIntake))EditActionAnchor(action);
   if(Button("Boyutlandır",actionAnchor!=null&&resizingIntake))EditActionAnchor(action,true);
   GUILayout.EndHorizontal();
   if(Button("Alanı yakından göster")){if(actionAnchor==null)EditActionAnchor(action);FocusActionArea(action);}

   string shape=Cycle("Şekil",action.captureShape,new[]{"box","sphere"},new[]{"Kutu","Küre"});
   if(shape!=action.captureShape){action.captureShape=shape;EditActionAnchor(action,resizingIntake);}
   if(action.captureShape=="box"){
    Section("BOYUT · METRE");GUILayout.BeginHorizontal();
    string[] dimensions={"Derinlik","Genişlik","Yükseklik"};
    for(int i=0;i<3;i++){GUILayout.BeginVertical(GUILayout.Width((rightWidth-44)/3));action.captureSize[i]=Num("intakeSize"+action.id+i,dimensions[i],action.captureSize[i],.01,5);GUILayout.EndVertical();}
    GUILayout.EndHorizontal();
   }else action.captureRadius=Num("intakeRadius"+action.id,"Yarıçap · metre",action.captureRadius,.05,3);

   Section("TOPLAMA");action.piece=SelectPiece(action);DrawActionKey(action);
   action.captureEfficiency=Num("efficiency"+action.id,"Toplama başarısı · %",action.captureEfficiency*100,0,100)/100;
   actionDetails=Fold("Konum ve bağlantı ayrıntıları",actionDetails);
   if(actionDetails){Vector("spawn"+action.id,"Parçaya göre konum · metre",action.spawn,-10,10);DrawActionController(action);DrawActionSignal(action);DrawRemoveAction(action);}
   SyncActionAnchor(action);
  }

  void OpenShotGuide(RobotAction action){quickSetup=false;SelectRobotAction(action);SetShotStep(0);}

  void SetShotStep(int step){
   ClearActionAnchor(true);CancelTargetPick();shotStep=Mathf.Clamp(step,0,2);propertyScroll=Vector2.zero;GUI.FocusControl(null);
  }

  void DrawShotGuide(RobotAction action){
   if(shotGuideId!=action.id){shotGuideId=action.id;shotStep=0;}
   Text($"ATIŞ REHBERİ  ·  {shotStep+1} / 3",true);
   switch(shotStep){
    case 0:
     GUILayout.Label("Çıkış noktası",title);
     action.name=Edit("actionname",action.name);action.piece=SelectPiece(action);ChangeActionMount(action);
     Text("Topun veya coralın robottan çıktığı yeri seç.",true);
     if(Button(actionAnchor!=null?"Çıkış noktasını taşıyorsun":"Çıkış noktasını taşı",actionAnchor!=null)){
      EditActionAnchor(action);FocusActionArea(action);
     }
     if(actionAnchor!=null)Text("Sahnedeki renkli okları sürükle.",true);
     shotAdvanced=Fold("Konum ve coral duruşu",shotAdvanced);
     if(shotAdvanced){Vector("spawn"+action.id,"Parçaya göre çıkış · metre",action.spawn,-10,10);DrawReleaseOrientation(action);}
     SyncActionAnchor(action);
     break;
    case 1:
     GUILayout.Label("Atış biçimi",title);
     DrawShotSetup(action);
     break;
    default:
     GUILayout.Label("Kontrol ve deneme",title);
     DrawActionKey(action);DrawActionController(action);
     shotLive=Fold("Robot koduna bağla · isteğe bağlı",shotLive);
     if(shotLive){
      DrawShotLinks(action);DrawActionSignal(action);
      Text("Hız veri alanı",true);action.speedField=Edit("shotSpeedField"+action.id,action.speedField);
      action.speedScale=Num("shotScale"+action.id,"Hız çarpanı · m/s",action.speedScale,-1000,1000);
      action.speedOffset=Num("shotOffset"+action.id,"Hıza ekle · m/s",action.speedOffset,-1000,1000);
      action.interval=Num("interval"+action.id,"Atış aralığı · saniye",action.interval,.05,10);
      action.inheritVelocity=GUILayout.Toggle(action.inheritVelocity," Robotun hızını ekle");
     }
     ShotLimitControl();
     if(PlanShot(action,out _,out var reason))Text("Sarı çizgi atışın izleyeceği yolu gösterir.",true);
     else Text(reason,true);
     if(Button("Sürüşe geç ve dene",true))SetRunning(true);
     if(Button("Projeyi kaydet"))SaveDialog();
     actionDetails=Fold("Eylem işlemleri",actionDetails);if(actionDetails)DrawRemoveAction(action);
     break;
   }
   GUILayout.Space(12);GUILayout.BeginHorizontal();
   if(shotStep>0&&Button("Geri"))SetShotStep(shotStep-1);
   GUILayout.FlexibleSpace();if(shotStep<2&&Button("Devam",true))SetShotStep(shotStep+1);
   GUILayout.EndHorizontal();
  }

  void DrawReleaseOrientation(RobotAction action){
   action.orientationMode=Cycle("Eşyanın duruşu",action.orientationMode,new[]{"auto","custom"},new[]{"Otomatik","Bağımsız açı"});
   if(action.orientationMode=="custom"){
    action.piecePitch=Num("piecePitch"+action.id,"Dikey duruş · derece",action.piecePitch,-180,180);
    action.pieceYaw=Num("pieceYaw"+action.id,"Yatay duruş · derece",action.pieceYaw,-180,180);
    action.pieceRoll=Num("pieceRoll"+action.id,"Kendi ekseninde dönüş · derece",action.pieceRoll,-180,180);
   }
   if(action.piece=="coral")action.coralSpin=Num("coralSpin"+action.id,"Coral dönüşü · derece/sn",action.coralSpin,-1440,1440);
  }

  string SelectPiece(RobotAction action)=>Cycle("Oyun eşyası",action.piece,PieceCatalog.Names.Keys.ToArray(),PieceCatalog.Names.Values.ToArray());

  void DrawActionKey(RobotAction action){
   if(Button(captureKey==action.id?"Bir tuşa bas…":"Tuş: "+(string.IsNullOrEmpty(action.key)?"Atanmamış":action.key)))captureKey=action.id;
  }

  void DrawActionController(RobotAction action){
   action.mode=Cycle("Tetikleme",action.mode,new[]{"hold","press","toggle"},new[]{"Basılı tut","Her basışta","Aç / kapat"});
   action.button=Cycle("Kumanda",action.button,new[]{"","a","b","x","y","leftBumper","rightBumper","back","start","leftStick","rightStick"},new[]{"Atanmamış","A","B","X","Y","LB · sol omuz","RB · sağ omuz","Back","Start","Sol çubuk","Sağ çubuk"});
   action.controller=(int)Num("controller"+action.id,"Kumanda sırası · 0 ilk kumanda",action.controller,0,5);
  }

  void DrawActionSignal(RobotAction action){
   Section("TETİKLEYİCİ · NT / BLOCKLY");action.topic=Edit("topic"+action.id,action.topic);action.invert=GUILayout.Toggle(action.invert," Negatif / ters yönde kullan");
   if(!string.IsNullOrEmpty(action.topic)&&(action.ntSource||action.topic.StartsWith("/"))){
    Text("Dizi / struct alanı",true);action.ntField=Edit("actionNtField"+action.id,action.ntField);
    if(action.type=="move"){
     action.ntMode=Cycle("NT kullanım",action.ntMode,new[]{"trigger","position"},new[]{"Hareketi tetikle","Değere göre konum"});
     if(action.ntMode=="position"){action.ntMin=Num("ntMin","Başlangıç değeri",action.ntMin,-1000000,1000000);action.ntMax=Num("ntMax","Bitiş değeri",action.ntMax,-1000000,1000000);}
    }
   }
   if(Button("Canlı NT verisi seç"))BeginBinding("trigger",action.id);
  }

  void DrawRemoveAction(RobotAction action){
   if(!Button("Eylemi kaldır"))return;
   ClearActionAnchor(false);CancelTargetPick();Project.actions.Remove(action);chosenAction=null;captureKey=null;
  }

  internal void CheckIntakeEditor(Action<bool,string> check){
   var part=Nodes["chassis"].transform;
   Vector3 position=part.localPosition;Quaternion rotation=part.localRotation;
   var action=new RobotAction{type="capture",partId="chassis",piece="fuel",spawn=new[]{.35,0,.4},captureSize=new[]{.2,.15,.2}};
   Project.actions.Add(action);
   SelectRobotAction(action);
   check(gizmo.Target!=part&&gizmo.Target==actionAnchor&&actionAnchor.parent==Nodes["chassis"].content,"Intake selects its own scene handle instead of the chassis");

   actionAnchor.localPosition=Frc.Position(new[]{.9,0,.4});RecordNode();
   EditActionAnchor(action,true);actionAnchor.localScale=new Vector3(.9f,.2f,.3f);RecordNode();
   var restored=ProjectIO.Parse(ProjectIO.Write(Project)).actions.Find(a=>a.id==action.id);
   check(restored.spawn[0]==.9&&restored.captureSize.SequenceEqual(new[]{.3,.9,.2})&&part.localPosition==position&&part.localRotation==rotation,"Intake position and dimensions save independently of the chassis");

   var mount=Nodes["chassis"].content;
   var inside=Pieces.Spawn("fuel",mount.TransformPoint(Frc.Position(new[]{.9,.38,.4})),Vector3.zero,Quaternion.identity);
   var outside=Pieces.Spawn("fuel",mount.TransformPoint(Frc.Position(new[]{.35,0,.4})),Vector3.zero,Quaternion.identity);
   int count=Stored("fuel");
   check(CaptureZone(action,true)==1&&inside.Removed&&!outside.Removed,"Capture uses the moved and resized intake volume");
   Project.inventory["fuel"]=count;Pieces.Remove(outside);
   ClearActionAnchor(false);Project.actions.Remove(action);chosenAction=null;uiTab=0;RestoreEditorGizmo();
  }

  internal void CheckShotGuide(Action<bool,string> check){
   var action=new RobotAction{type="emit",partId="chassis",piece="fuel",aimMode="zones"};
   Project.actions.Add(action);OpenShotGuide(action);EditActionAnchor(action);
   Vector3 chassis=Nodes["chassis"].transform.localPosition;
   actionAnchor.localPosition=Frc.Position(new[]{.8,.05,.35});RecordNode();SetShotStep(1);
   var blue=NewHalfZone(action,true);var red=NewHalfZone(action,false);red.target[2]=.1;
   BeginShotPick(action,4,blue);ApplyShotScenePick(action,Frc.Position(new[]{8.0,3.0,0}));
   BeginShotPick(action,4,red);ApplyShotScenePick(action,Frc.Position(new[]{13.0,5.0,0}));
   BeginShotPick(action,5,red);ApplyShotScenePick(action,Frc.Position(new[]{12.0,4.0,0}));
   var saved=ProjectIO.Parse(ProjectIO.Write(Project)).actions.Find(a=>a.id==action.id);
   check(saved.spawn.SequenceEqual(new[]{.8,.05,.35})&&Nodes["chassis"].transform.localPosition==chassis,"Shot guide places and persists the muzzle independently of the chassis");
   check(saved.zones.Count==2&&saved.zones[0].target[0]==8&&saved.zones[1].target[0]==12&&saved.zones[1].target[2]==.1&&saved.zones[0].center[0]<Field.Length/2&&saved.zones[1].center[0]>Field.Length/2,"Each field half stores its own editable target and height");
   Project.actions.Remove(action);chosenAction=null;uiTab=0;RestoreEditorGizmo();
  }
 }
}
