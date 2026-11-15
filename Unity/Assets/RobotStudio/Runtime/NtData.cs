using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace AshinaStudio {
 public static class NtData {
  public static bool Number(object value,out double number) {
   number=0;
   if(value is bool b){number=b?1:0;return true;}
   if(value is double||value is float||value is long||value is int||value is ulong||value is short||value is byte)number=Convert.ToDouble(value);
   else if(value is string text){if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out number))return false;}
   else return false;
   return !double.IsNaN(number)&&!double.IsInfinity(number);
  }
  public static bool Scalar(Nt4Client client,NtValue value,string field,out double number) {
   number=0;if(value==null)return false;object current=value.Value;
   if(current is byte[]) {try{current=Decode(client,value);}catch{return false;}}
   if(!string.IsNullOrWhiteSpace(field))foreach(string key in field.Replace("[",".").Replace("]","").Split(new[]{'.'},StringSplitOptions.RemoveEmptyEntries)) {
    if(current is IDictionary<string,object> map){if(!map.TryGetValue(key,out current))return false;}
    else if(current is IList list&&int.TryParse(key,out int index)&&index>=0&&index<list.Count)current=list[index];
    else return false;
   }
   return Number(current,out number);
  }
  public static double[] Numbers(object value) {
   if(!(value is IList list)||value is byte[]||list.Count>100000)return null;
   var result=new double[list.Count];for(int i=0;i<result.Length;i++)if(!Number(list[i],out result[i]))return null;return result;
  }
  static double LittleDouble(byte[] raw,int offset) {
   if(BitConverter.IsLittleEndian)return BitConverter.ToDouble(raw,offset);
   var bytes=new byte[8];Buffer.BlockCopy(raw,offset,bytes,0,8);System.Array.Reverse(bytes);return BitConverter.ToDouble(bytes,0);
  }
  public static bool Pose(NtValue value,string topic,string format,out double[] pose) {
   pose=null;if(value==null)return false;
   if(value.Value is byte[] raw) {
    if(value.Type=="struct:Pose2d"&&raw.Length==24)pose=new[]{LittleDouble(raw,0),LittleDouble(raw,8),LittleDouble(raw,16)};
    else if(value.Type=="struct:Pose3d"&&raw.Length==56){double w=LittleDouble(raw,24),x=LittleDouble(raw,32),y=LittleDouble(raw,40),z=LittleDouble(raw,48);pose=new[]{LittleDouble(raw,0),LittleDouble(raw,8),Math.Atan2(2*(w*z+x*y),1-2*(y*y+z*z))};}
    else return false;
   } else {
    var values=Numbers(value.Value);if(values==null||values.Length<3)return false;
    bool botpose=format=="botpose"||(format=="auto"&&topic.IndexOf("botpose",StringComparison.OrdinalIgnoreCase)>=0);
    if(botpose){if(values.Length<6)return false;pose=new[]{values[0],values[1],values[5]*Math.PI/180};}
    else {bool degrees=format=="degrees"||format=="field2d"||(format=="auto"&&topic.EndsWith("/Robot",StringComparison.Ordinal));pose=new[]{values[0],values[1],values[2]*(degrees?Math.PI/180:1)};}
   }
   return pose.All(x=>!double.IsNaN(x)&&!double.IsInfinity(x))&&Math.Abs(pose[0])<1000&&Math.Abs(pose[1])<1000;
  }
  public static bool Speeds(NtValue value,out double[] speeds) {
   speeds=null;if(value==null)return false;
   if(value.Value is byte[] raw&&value.Type=="struct:ChassisSpeeds"&&raw.Length==24)speeds=new[]{LittleDouble(raw,0),LittleDouble(raw,8),LittleDouble(raw,16)};
   else speeds=Numbers(value.Value);
   return speeds!=null&&speeds.Length>=3&&speeds.Take(3).All(x=>!double.IsNaN(x)&&!double.IsInfinity(x));
  }
  public static string FindPose(Nt4Client client,string requested) {
   if(client==null)return null;
   if(!string.IsNullOrEmpty(requested)&&Pose(client.Get(requested),requested,"auto",out _))return requested;
   foreach(var name in new[]{"/SmartDashboard/Field/Robot","/SmartDashboard/Field2d/Robot","/FRCStudio/Drive/Pose","/Swerve/RobotPose","/AdvantageKit/RealOutputs/Odometry/Robot"})if(Pose(client.Get(name),name,"auto",out _))return name;
   return client.Values.OrderBy(x=>x.Key,StringComparer.Ordinal).Where(x=>!x.Key.StartsWith("/AshinaSim/")&&!x.Key.Contains("limelight")&&!x.Key.Contains("Target")&&!x.Key.Contains("target")).FirstOrDefault(x=>x.Value.Type=="struct:Pose2d"||x.Value.Type=="struct:Pose3d"||x.Key.EndsWith("/Robot")&&Pose(x.Value,x.Key,"auto",out _)).Key;
  }
  public static object Decode(Nt4Client client,NtValue value) {
   if(value==null)return null;if(!(value.Value is byte[] bytes))return value.Value;
   if(value.Type=="structschema")return Encoding.UTF8.GetString(bytes);if(value.Type=="msgpack"){int cursor=0;return MsgPack.Read(bytes,ref cursor);}
   if(!value.Type.StartsWith("struct:"))return new Dictionary<string,object>{{"bytes",bytes.Length},{"base64",Convert.ToBase64String(bytes)}};
   string type=value.Type.Substring(7);bool array=type.EndsWith("[]");if(array)type=type.Substring(0,type.Length-2);
   int at=0;if(!array)return ReadStruct(client,type,bytes,ref at,0);
   var result=new List<object>();while(at<bytes.Length&&result.Count<4096){int before=at;result.Add(ReadStruct(client,type,bytes,ref at,0));if(at<=before)throw new IOException("Boş struct şeması.");}return result;
  }
  static object ReadStruct(Nt4Client client,string type,byte[] bytes,ref int at,int depth) {
   if(depth>16)throw new IOException("Struct iç içe geçme sınırı.");
   var schema=client?.Get("/.schema/struct:"+type);string text=schema?.Value is byte[] raw?Encoding.UTF8.GetString(raw):schema?.Value as string;
   if(text==null) {
    switch(type){case "Pose2d":text="Translation2d translation;Rotation2d rotation";break;case "Translation2d":text="double x;double y";break;case "Rotation2d":text="double value";break;case "ChassisSpeeds":text="double vx;double vy;double omega";break;default:throw new IOException("Struct şeması bekleniyor: "+type);}
   }
   if(text.Length>65536)throw new IOException("Struct şeması çok uzun.");text=Regex.Replace(text,@"enum\s*\{[^}]*\}","");
   var fields=new Dictionary<string,object>();
   foreach(var declaration in text.Split(';')) {
    if(string.IsNullOrWhiteSpace(declaration))continue;
    var match=Regex.Match(declaration,@"^\s*(\w+)\s+(\w+)(?:\s*\[\s*(\d+)\s*\])?\s*$");
    if(!match.Success)throw new IOException("Bu struct alanı ham veri olarak kullanılabilir: "+declaration.Trim());
    string primitive=match.Groups[1].Value,name=match.Groups[2].Value;bool array=match.Groups[3].Success;int count=array?int.Parse(match.Groups[3].Value):1;if(count>4096)throw new IOException("Struct dizi sınırı.");
    var values=new object[count];for(int i=0;i<count;i++)values[i]=ReadPrimitive(client,primitive,bytes,ref at,depth+1);
    fields[name]=array?values:values[0];
   }
   return fields;
  }
  static object ReadPrimitive(Nt4Client client,string type,byte[] bytes,ref int at,int depth) {
   int size;switch(type){case "bool":case "char":case "int8":case "uint8":size=1;break;case "int16":case "uint16":size=2;break;case "int32":case "uint32":case "float":size=4;break;case "int64":case "uint64":case "double":size=8;break;default:return ReadStruct(client,type,bytes,ref at,depth);}
   if(at>bytes.Length-size)throw new IOException("Eksik struct verisi.");ulong bits=0;for(int i=0;i<size;i++)bits|=(ulong)bytes[at++]<<(8*i);
   switch(type){case "bool":return bits!=0;case "double":return BitConverter.Int64BitsToDouble(unchecked((long)bits));case "float":return BitConverter.ToSingle(BitConverter.GetBytes((uint)bits),0);case "int8":return (long)unchecked((sbyte)bits);case "int16":return (long)unchecked((short)bits);case "int32":return (long)unchecked((int)bits);case "int64":return unchecked((long)bits);default:return bits;}
  }
  public static string Display(Nt4Client client,NtValue value,int max=1800) {
   if(value==null)return "Değer bekleniyor…";string text;
   try{object display;if(value.Value is byte[] large&&large.Length>65536)display=new{bytes=large.Length,prefixBase64=Convert.ToBase64String(large,0,384)};else if(value.Value is System.Collections.IList list&&!(value.Value is byte[])&&list.Count>128)display=new{count=list.Count,first=list.Cast<object>().Take(128).ToArray()};else display=Decode(client,value);text=JsonConvert.SerializeObject(display,Formatting.None);}catch(Exception error){var raw=value.Value as byte[];text=error.Message+(raw==null?"":" · "+raw.Length+" bayt · "+Convert.ToBase64String(raw,0,Math.Min(384,raw.Length)));}
   return text.Length>max?text.Substring(0,max)+"…":text;
  }
 }
}
