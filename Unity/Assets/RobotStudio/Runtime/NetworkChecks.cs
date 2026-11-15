using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace AshinaStudio {
 public partial class PlayerChecks {
  async Task NetworkRegression() {
   await Wait(()=>App.Nt.Get("/Fixture/State")!=null,"NT özel struct ve şemalar keşfediliyor");
   Check(App.Nt.Get("/Fixture/Enabled")?.Value is bool flag&&flag,"NT boolean alımı");
   Check(Convert.ToInt64(App.Nt.Get("/Fixture/Count")?.Value)==42,"NT int64 alımı");
   Check(Math.Abs(Convert.ToDouble(App.Nt.Get("/Fixture/Float")?.Value)-1.25)<.0001,"NT float32 alımı");
   Check((string)App.Nt.Get("/Fixture/Text")?.Value=="REEFSCAPE · Türkçe","NT UTF-8 metin alımı");
   Check(App.Nt.Get("/Fixture/Booleans")?.Value is object[] bools&&bools.Length==2&&(bool)bools[0]&&!(bool)bools[1],"NT boolean dizisi alımı");
   Check(App.Nt.Get("/Fixture/Strings")?.Value is object[] strings&&strings.Length==2&&(string)strings[1]=="coral","NT metin dizisi alımı");
   Check(NtData.Numbers(App.Nt.Get("/Fixture/Integers")?.Value)?.SequenceEqual(new[]{-7d,42d})==true,"NT tamsayı dizisi alımı");
   Check(NtData.Numbers(App.Nt.Get("/Fixture/Floats")?.Value)?.SequenceEqual(new[]{1.25d,-2.5d})==true,"NT float dizisi alımı");
   Check((App.Nt.Get("/Fixture/Raw")?.Value as byte[])?.SequenceEqual(new byte[]{0,1,255})==true,"NT ham veri kayıpsız alınıyor");
   Check((string)App.Nt.Get("/Fixture/Json")?.Value=="{\"season\":2025}","NT JSON veri türü alımı");
   Check((string)App.Nt.Get("NoLeadingSlash")?.Value=="visible","Başında eğik çizgi olmayan NT konuları da alınıyor");
   Check(NtData.Scalar(App.Nt,App.Nt.Get("/Fixture/State"),"pose.translation.x",out var x)&&Math.Abs(x-2)<.001,"İç içe WPILib struct alanı çözümleniyor");
   Check(NtData.Scalar(App.Nt,App.Nt.Get("/Fixture/State"),"height",out var height)&&Math.Abs(height-.75)<.001,"Özel struct sayısal alanı mekanizmaya seçilebiliyor");
   Check(!NtData.Scalar(App.Nt,App.Nt.Get("/Fixture/Strings"),"",out _),"Sayısal olmayan NT verisi güvenli reddediliyor");
   int cursor=0;var map=MsgPack.Read(MsgPack.Encode(new System.Collections.Generic.Dictionary<string,object>{{"name","test"},{"count",2L}}),ref cursor);
   Check(map is System.Collections.Generic.Dictionary<string,object>,"NT meta veri MessagePack nesneleri çözümleniyor");
   var settings=App.Project.network;string oldTopic=settings.poseTopic;settings.poseTopic="/missing";settings.autoPose=true;settings.mode="observe";App.SetRunning(true);
   await Task.Delay(700);
   Check(App.EffectivePoseTopic=="/SmartDashboard/Field/Robot"&&Math.Abs(App.Pose[0]-6)<.03&&Math.Abs(App.Pose[2]-Math.PI/2)<.001,"YAGSL Field2d konumu otomatik bulunuyor ve derece radyana çevriliyor");
   await Task.Delay(1500);
   Check(Math.Abs(App.Pose[0]-6)<.03&&Math.Abs(App.Pose[2]-Math.PI/2)<.001,"Değişmeyen Field2d konumu zaman aşımıyla kaybolmuyor");
   var action=App.Project.actions[0];action.topic="/Fixture/State";action.ntField="height";action.ntMode="position";action.ntMin=0;action.ntMax=1;
   await Task.Delay(250);
   Check(Math.Abs(App.Nodes[action.partId].transform.localPosition.y-.775)<.03,"NT sürekli değer kaydedilen mekanizma hareketine orantılı uygulanıyor");
   var saved=ProjectIO.Parse(ProjectIO.Write(App.Project));
   Check(saved.actions[0].ntField=="height"&&saved.actions[0].ntMode=="position","NT konu ve alan eşlemesi robot dosyasında korunuyor");
   action.topic="/Fixture/Strings";action.ntMode="trigger";action.ntField="";await Task.Delay(250);
   Check(Math.Abs(App.Nodes[action.partId].transform.localPosition.y-.4)<.03,"Yanlış NT veri türü sürüş döngüsünü çökertmiyor");
   action.topic="/Fixture/Enabled";await Task.Delay(250);
   Check(App.Nodes[action.partId].transform.localPosition.y>.85,"Boolean NT verisi kayıtlı hareketi tetikliyor");
   action.topic="";action.ntMode="trigger";App.SetRunning(false);
   await Wait(()=>App.Nt.Topics.ContainsKey("/Fixture/Ephemeral"),"Yeni NT konusu listede keşfediliyor");
   await App.Nt.Publish("/Fixture/drop",true,"boolean");
   await Wait(()=>!App.Nt.Topics.ContainsKey("/Fixture/Ephemeral")&&App.Nt.Get("/Fixture/Ephemeral")==null,"Silinen NT konusu ve eski değeri listeden kaldırılıyor");
   int generation=App.Nt.Generation;await App.Nt.Publish("/Fixture/restart",true,"boolean");
   await Wait(()=>App.Nt.Generation>generation&&App.Nt.Connected,"WPILib sunucusu yeniden başlayınca otomatik bağlanıyor",16000);
   await Wait(()=>App.Nt.Get("/SmartDashboard/Field/Robot")!=null,"Yeniden bağlantıda konular ve değerler tekrar keşfediliyor");
   settings.poseTopic=oldTopic;settings.autoPose=false;App.SetRunning(true);await Task.Delay(250);
   Check(Math.Abs(App.Pose[0]-2)<.03&&Math.Abs(App.Pose[1]-4)<.03,"Yeniden bağlantıdan sonra robot konumu doğru uygulanıyor");
   App.SetRunning(false);settings.autoPose=true;
   File.WriteAllText(Path.Combine(root,"nt-topics.json"),JsonConvert.SerializeObject(App.Nt.Topics.Values.OrderBy(t=>t.Name).Select(t=>new{name=t.Name,type=t.Type,value=NtData.Display(App.Nt,App.Nt.Get(t.Name))}),Formatting.Indented));
  }
 }
}
