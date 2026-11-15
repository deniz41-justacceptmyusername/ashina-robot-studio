using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AshinaStudio {
 // Local-only paired transport. The existing Blockly interpreter stays the single source of block behavior.
 public sealed class StudioBridge : IDisposable {
  readonly TcpListener listener; readonly CancellationTokenSource cancel=new CancellationTokenSource(); readonly object gate=new object(); NetworkStream client;
  public readonly string Token; public readonly int Port; public volatile bool Connected; public string Error=""; JObject incoming, pendingProject; long lastTicks;
  public StudioBridge(int port=5818){Port=port;var bytes=new byte[12];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);Token=BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();listener=new TcpListener(IPAddress.Loopback,port);listener.Start();Port=((IPEndPoint)listener.LocalEndpoint).Port;_ = Task.Run(Listen);}
  async Task Listen(){while(!cancel.IsCancellationRequested){try{var tcp=await listener.AcceptTcpClientAsync();_ = Session(tcp);}catch(Exception e){if(!cancel.IsCancellationRequested)Error=e.Message;break;}}}
  async Task Session(TcpClient tcp){NetworkStream stream=null;bool paired=false;try{tcp.NoDelay=true;tcp.ReceiveTimeout=5000;stream=tcp.GetStream();stream.WriteTimeout=1000;var header=new StringBuilder();while(header.Length<8192&&!header.ToString().EndsWith("\r\n\r\n")){int b=stream.ReadByte();if(b<0)throw new IOException();header.Append((char)b);}string key=null;foreach(var line in header.ToString().Split('\n'))if(line.StartsWith("Sec-WebSocket-Key:",StringComparison.OrdinalIgnoreCase))key=line.Substring(18).Trim();if(key==null)throw new IOException("WebSocket başlığı eksik.");string accept;using(var sha=SHA1.Create())accept=Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key+"258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));var response=Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "+accept+"\r\n\r\n");await stream.WriteAsync(response,0,response.Length,cancel.Token);
   while(!cancel.IsCancellationRequested){var h=await Read(stream,2);int opcode=h[0]&15;if((h[0]&128)==0||(h[1]&128)==0)throw new IOException("Tek çerçeve ve maske gerekli.");long length=h[1]&127;if(length==126){var n=await Read(stream,2);length=(n[0]<<8)|n[1];}if(length==127){var n=await Read(stream,8);length=0;foreach(byte b in n)length=(length<<8)|b;}if(length<0||length>25000000)throw new IOException("Mesaj boyutu sınırı.");var mask=await Read(stream,4);var data=await Read(stream,(int)length);for(int i=0;i<data.Length;i++)data[i]^=mask[i%4];if(opcode==8)break;if(opcode==9){lock(gate)SendFrame(stream,data,10);continue;}if(opcode!=1)continue;var raw=Encoding.UTF8.GetString(data);JObject message;using(var reader=new JsonTextReader(new StringReader(raw)){MaxDepth=96})message=JObject.Load(reader);if(!paired){if((string)message["type"]!="pair"||(string)message["token"]!=Token)break;lock(gate){if(client!=null)break;client=stream;Connected=true;paired=true;}Send(new{type="paired"});continue;}lock(gate){if(message["project"] is JObject project){message["project"]=null;message.Remove("project");pendingProject=project;}incoming=message;lastTicks=DateTime.UtcNow.Ticks;}}
  }catch(Exception e){if(!cancel.IsCancellationRequested)Error=e.Message;}finally{lock(gate)if(client==stream){client=null;Connected=false;}tcp.Close();}}
  async Task<byte[]> Read(Stream s,int count){var b=new byte[count];int offset=0;while(offset<count){int read=await s.ReadAsync(b,offset,count-offset,cancel.Token);if(read==0)throw new IOException("Bağlantı kapandı.");offset+=read;}return b;}
  void SendFrame(NetworkStream s,byte[] data,int opcode=1){using(var frame=new MemoryStream()){frame.WriteByte((byte)(128|opcode));if(data.Length<126)frame.WriteByte((byte)data.Length);else if(data.Length<65536){frame.WriteByte(126);frame.WriteByte((byte)(data.Length>>8));frame.WriteByte((byte)data.Length);}else{frame.WriteByte(127);for(int i=7;i>=0;i--)frame.WriteByte((byte)((long)data.Length>>(8*i)));}frame.Write(data,0,data.Length);var bytes=frame.ToArray();s.Write(bytes,0,bytes.Length);}}
  public void Send(object message){lock(gate){if(client==null)return;try{SendFrame(client,Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)));}catch{client.Close();client=null;Connected=false;}}}
  public JObject Take(){lock(gate){var message=incoming;incoming=null;if(message!=null&&pendingProject!=null){message["project"]=pendingProject;pendingProject=null;}return message;}}
  public bool Fresh=>Connected&&(DateTime.UtcNow.Ticks-lastTicks)<TimeSpan.TicksPerMillisecond*300;
  public void Dispose(){cancel.Cancel();listener.Stop();lock(gate){client?.Close();client=null;}cancel.Dispose();}
 }
}




