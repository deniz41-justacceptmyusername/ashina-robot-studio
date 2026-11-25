using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AshinaStudio {
 // Only the loopback tunnel process can open this listener. The invitation key is checked before upgrading.
 internal sealed class MatchInternetListener : IDisposable {
  readonly TcpListener listener;
  readonly CancellationTokenSource stop=new CancellationTokenSource();
  readonly ConcurrentQueue<MatchLink> accepted=new ConcurrentQueue<MatchLink>();
  readonly ConcurrentDictionary<TcpClient,byte> handshakes=new ConcurrentDictionary<TcpClient,byte>();
  readonly string key;
  public int Port {get;}

  public MatchInternetListener(string key){
   this.key=key;listener=new TcpListener(IPAddress.Loopback,0);listener.Start(12);
   Port=((IPEndPoint)listener.LocalEndpoint).Port;_=Task.Run(Accept);
  }

  async Task Accept(){
   try{
    while(!stop.IsCancellationRequested){
     var socket=await listener.AcceptTcpClientAsync().ConfigureAwait(false);
     if(stop.IsCancellationRequested||handshakes.Count>=12||accepted.Count>=8){socket.Close();continue;}
     handshakes.TryAdd(socket,0);_=Handshake(socket);
    }
   }catch(SocketException){}catch(ObjectDisposedException){}
  }

  async Task Handshake(TcpClient socket){
   bool handedOff=false;
   try{
    socket.NoDelay=true;
    using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token)){
     timeout.CancelAfter(5000);var stream=socket.GetStream();
     string[] request=await ReadHeader(stream,timeout.Token).ConfigureAwait(false);
     string challenge=Header(request,"Sec-WebSocket-Key");
     bool valid=request[0]=="GET /room/"+key+" HTTP/1.1"&&Header(request,"Upgrade").Equals("websocket",StringComparison.OrdinalIgnoreCase)&&Header(request,"Sec-WebSocket-Version")=="13";
     if(!valid||challenge.Length!=24||Convert.FromBase64String(challenge).Length!=16){
      await Reply(stream,"404 Not Found","",timeout.Token).ConfigureAwait(false);return;
     }
     string responseKey;
     using(var sha=SHA1.Create())responseKey=Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(challenge+"258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
     await Write(stream,"HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: websocket\r\nSec-WebSocket-Accept: "+responseKey+"\r\n\r\n",timeout.Token).ConfigureAwait(false);
     var match=new MatchLink(WebSocket.CreateFromStream(stream,true,null,TimeSpan.FromSeconds(15)),socket);
     accepted.Enqueue(match);handedOff=true;
     if(stop.IsCancellationRequested)match.Dispose();
    }
   }catch(Exception){}finally{handshakes.TryRemove(socket,out _);if(!handedOff)socket.Close();}
  }

  static async Task<string[]> ReadHeader(NetworkStream stream,CancellationToken token){
   var header=new byte[8192];var one=new byte[1];int length=0;
   while(length<header.Length){
    if(await stream.ReadAsync(one,0,1,token).ConfigureAwait(false)!=1)break;
    header[length++]=one[0];
    if(length>=4&&header[length-4]==13&&header[length-3]==10&&header[length-2]==13&&header[length-1]==10)return Encoding.ASCII.GetString(header,0,length).Split(new[]{"\r\n"},StringSplitOptions.None);
   }
   throw new IOException("Eksik internet oda isteği.");
  }

  static string Header(string[] request,string name){
   foreach(string row in request)if(row.StartsWith(name+":",StringComparison.OrdinalIgnoreCase))return row.Substring(name.Length+1).Trim();
   return "";
  }

  static Task Reply(NetworkStream stream,string status,string body,CancellationToken token)=>Write(stream,"HTTP/1.1 "+status+"\r\nContent-Type: text/plain\r\nCache-Control: no-store\r\nConnection: close\r\nContent-Length: "+body.Length+"\r\n\r\n"+body,token);
  static Task Write(NetworkStream stream,string message,CancellationToken token){var bytes=Encoding.ASCII.GetBytes(message);return stream.WriteAsync(bytes,0,bytes.Length,token);}
  public bool Take(out MatchLink link)=>accepted.TryDequeue(out link);
  public void Dispose(){if(stop.IsCancellationRequested)return;stop.Cancel();listener.Stop();foreach(var socket in handshakes.Keys)socket.Close();while(accepted.TryDequeue(out var link))link.Dispose();}
 }
}
