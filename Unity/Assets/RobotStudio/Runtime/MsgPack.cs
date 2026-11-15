using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace AshinaStudio {
 public static class MsgPack {
  public static byte[] Encode(object value){using(var stream=new MemoryStream()){Write(stream,value);return stream.ToArray();}}
  static void Bytes(Stream s,ulong n,int count){for(int i=count-1;i>=0;i--)s.WriteByte((byte)(n>>(i*8)));}
  static void Write(Stream s,object value) {
   if(value==null){s.WriteByte(0xc0);return;}
   if(value is bool b){s.WriteByte(b?(byte)0xc3:(byte)0xc2);return;}
   if(value is float f){s.WriteByte(0xca);Bytes(s,BitConverter.ToUInt32(BitConverter.GetBytes(f),0),4);return;}
   if(value is double d){s.WriteByte(0xcb);Bytes(s,unchecked((ulong)BitConverter.DoubleToInt64Bits(d)),8);return;}
   if(value is int||value is long||value is short||value is byte){long n=Convert.ToInt64(value);if(n>=0&&n<128)s.WriteByte((byte)n);else if(n<0&&n>=-32)s.WriteByte(unchecked((byte)n));else{s.WriteByte(0xd3);Bytes(s,unchecked((ulong)n),8);}return;}
   if(value is ulong u){s.WriteByte(0xcf);Bytes(s,u,8);return;}
   if(value is string str){var bytes=Encoding.UTF8.GetBytes(str);s.WriteByte(0xdb);Bytes(s,(ulong)bytes.Length,4);s.Write(bytes,0,bytes.Length);return;}
   if(value is byte[] bin){s.WriteByte(0xc6);Bytes(s,(ulong)bin.Length,4);s.Write(bin,0,bin.Length);return;}
   if(value is IDictionary map){s.WriteByte(0xdf);Bytes(s,(ulong)map.Count,4);foreach(DictionaryEntry item in map){Write(s,item.Key);Write(s,item.Value);}return;}
   if(value is ICollection list){s.WriteByte(0xdd);Bytes(s,(ulong)list.Count,4);foreach(var item in list)Write(s,item);return;}
   throw new IOException("MessagePack türü desteklenmiyor: "+value.GetType().Name);
  }
  static ulong U(byte[] bytes,ref int at,int count){if(count<0||at<0||at>bytes.Length-count)throw new IOException("Eksik MessagePack.");ulong n=0;for(int i=0;i<count;i++)n=(n<<8)|bytes[at++];return n;}
  public static object Read(byte[] bytes,ref int at,int depth=0) {
   if(depth>40||at>=bytes.Length)throw new IOException("MessagePack sınırı.");byte code=bytes[at++];
   if(code<128)return (long)code;if(code>=224)return (long)(sbyte)code;
   if((code&240)==144)return Array(bytes,ref at,code&15,depth);
   if((code&240)==128)return Map(bytes,ref at,code&15,depth);
   if((code&224)==160)return String(bytes,ref at,code&31);
   switch(code) {
    case 0xc0:return null;case 0xc2:return false;case 0xc3:return true;
    case 0xcc:return (long)U(bytes,ref at,1);case 0xcd:return (long)U(bytes,ref at,2);case 0xce:return (long)U(bytes,ref at,4);case 0xcf:return U(bytes,ref at,8);
    case 0xd0:return (long)unchecked((sbyte)U(bytes,ref at,1));case 0xd1:return (long)unchecked((short)U(bytes,ref at,2));case 0xd2:return (long)unchecked((int)U(bytes,ref at,4));case 0xd3:return unchecked((long)U(bytes,ref at,8));
    case 0xca:return BitConverter.ToSingle(BitConverter.GetBytes((uint)U(bytes,ref at,4)),0);case 0xcb:return BitConverter.Int64BitsToDouble(unchecked((long)U(bytes,ref at,8)));
    case 0xd9:return String(bytes,ref at,(int)U(bytes,ref at,1));case 0xda:return String(bytes,ref at,(int)U(bytes,ref at,2));case 0xdb:return String(bytes,ref at,Count(U(bytes,ref at,4)));
    case 0xc4:return Bin(bytes,ref at,(int)U(bytes,ref at,1));case 0xc5:return Bin(bytes,ref at,(int)U(bytes,ref at,2));case 0xc6:return Bin(bytes,ref at,Count(U(bytes,ref at,4)));
    case 0xdc:return Array(bytes,ref at,(int)U(bytes,ref at,2),depth);case 0xdd:return Array(bytes,ref at,Count(U(bytes,ref at,4)),depth);
    case 0xde:return Map(bytes,ref at,(int)U(bytes,ref at,2),depth);case 0xdf:return Map(bytes,ref at,Count(U(bytes,ref at,4)),depth);
    default:throw new IOException("Desteklenmeyen MessagePack etiketi: "+code);
   }
  }
  static int Count(ulong n){if(n>16000000)throw new IOException("MessagePack uzunluk sınırı.");return (int)n;}
  static object[] Array(byte[] b,ref int at,int count,int depth){if(count>1000000||count>b.Length-at)throw new IOException("MessagePack dizi sınırı.");var values=new object[count];for(int i=0;i<count;i++)values[i]=Read(b,ref at,depth+1);return values;}
  static Dictionary<string,object> Map(byte[] b,ref int at,int count,int depth){if(count>100000||count>(b.Length-at)/2)throw new IOException("MessagePack nesne sınırı.");var map=new Dictionary<string,object>();for(int i=0;i<count;i++){var key=Read(b,ref at,depth+1);map[Convert.ToString(key)??""]=Read(b,ref at,depth+1);}return map;}
  static byte[] Bin(byte[] b,ref int at,int n){if(n<0||at>b.Length-n)throw new IOException("Eksik MessagePack veri alanı.");var value=new byte[n];System.Array.Copy(b,at,value,0,n);at+=n;return value;}
  static string String(byte[] b,ref int at,int n)=>Encoding.UTF8.GetString(Bin(b,ref at,n));
 }
}
