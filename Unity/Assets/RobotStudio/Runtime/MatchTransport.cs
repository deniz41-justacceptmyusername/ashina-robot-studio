using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace AshinaStudio {
 // One reader and one writer per socket. World snapshots replace obsolete unsent snapshots.
 // Socket work never touches a Unity object or blocks the main thread.
 public sealed class MatchLink : IDisposable {
  readonly TcpClient socket;readonly CancellationTokenSource stop=new CancellationTokenSource();readonly SemaphoreSlim wake=new SemaphoreSlim(0);
  readonly ConcurrentQueue<byte[]> reliable=new ConcurrentQueue<byte[]>();readonly ConcurrentQueue<JObject> incoming=new ConcurrentQueue<JObject>();
  byte[] latest;int disposed;public bool Connected=>disposed==0;public string Error {get;private set;}="";
  public double LastReceived {get;private set;}=Clock;public static double Clock=>clock.Elapsed.TotalSeconds;
  static readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
  public MatchLink(TcpClient client){socket=client;socket.NoDelay=true;socket.SendBufferSize=128*1024;socket.ReceiveBufferSize=128*1024;_ = Task.Run(Read);_ = Task.Run(Write);}
  public static async Task<MatchLink> Connect(string host,int port){
   var client=new TcpClient();try{var task=client.ConnectAsync(host,port);if(await Task.WhenAny(task,Task.Delay(10000))!=task)throw new IOException("Odaya bağlantı zaman aşımına uğradı.");await task;return new MatchLink(client);}catch{client.Dispose();throw;}
  }
  public bool Take(out JObject packet)=>incoming.TryDequeue(out packet);
  public void Send(object message,bool replace=false){
   if(!Connected)return;var bytes=Encoding.UTF8.GetBytes(MatchProtocol.Encode(message));if(bytes.Length>MatchProtocol.MaxFrame)throw new IOException("Oda iletisi boyut sınırını aşıyor.");
   if(replace)Interlocked.Exchange(ref latest,bytes);else{if(reliable.Count>=24){Fail("Oda bağlantısı veri birikmesi nedeniyle kapatıldı.");return;}reliable.Enqueue(bytes);}try{wake.Release();}catch(ObjectDisposedException){}
  }
  async Task Exact(NetworkStream stream,byte[] buffer){int offset=0;while(offset<buffer.Length){int count=await stream.ReadAsync(buffer,offset,buffer.Length-offset,stop.Token).ConfigureAwait(false);if(count==0)throw new EndOfStreamException();offset+=count;LastReceived=Clock;}}
  async Task Read(){try{var stream=socket.GetStream();while(!stop.IsCancellationRequested){var header=new byte[4];await Exact(stream,header);int length=(header[0]<<24)|(header[1]<<16)|(header[2]<<8)|header[3];if(length<2||length>MatchProtocol.MaxFrame)throw new IOException("Geçersiz oda iletisi.");var bytes=new byte[length];await Exact(stream,bytes);if(incoming.Count>=64)throw new IOException("Oda veri hızı sınırı aşıldı.");var packet=MatchProtocol.Decode(new UTF8Encoding(false,true).GetString(bytes));LastReceived=Clock;incoming.Enqueue(packet);}}
   catch(Exception e){if(!stop.IsCancellationRequested)Fail(e is EndOfStreamException?"Oda bağlantısı kapandı.":e.Message);}}
  async Task Write(){try{var stream=socket.GetStream();while(!stop.IsCancellationRequested){await wake.WaitAsync(stop.Token).ConfigureAwait(false);while(reliable.TryDequeue(out var bytes))await Frame(stream,bytes);var state=Interlocked.Exchange(ref latest,null);if(state!=null)await Frame(stream,state);while(wake.Wait(0)){}if(!reliable.IsEmpty||latest!=null)wake.Release();}}
   catch(Exception e){if(!stop.IsCancellationRequested)Fail(e.Message);}}
  async Task Frame(NetworkStream stream,byte[] data){int n=data.Length;var h=new[]{(byte)(n>>24),(byte)(n>>16),(byte)(n>>8),(byte)n};await stream.WriteAsync(h,0,4,stop.Token).ConfigureAwait(false);await stream.WriteAsync(data,0,n,stop.Token).ConfigureAwait(false);}
  void Fail(string reason){Error=reason;Dispose();}
  public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;stop.Cancel();socket.Close();Interlocked.Exchange(ref latest,null);while(reliable.TryDequeue(out _)){} }
 }
 public sealed class MatchListener : IDisposable {
  readonly TcpListener server;readonly ConcurrentQueue<MatchLink> accepted=new ConcurrentQueue<MatchLink>();volatile bool closed;
  public int Port=>((IPEndPoint)server.LocalEndpoint).Port;
  public MatchListener(int port,bool localOnly=false){server=new TcpListener(localOnly?IPAddress.Loopback:IPAddress.Any,port);server.Start(8);_ = Task.Run(Accept);}
  async Task Accept(){try{while(!closed){var tcp=await server.AcceptTcpClientAsync().ConfigureAwait(false);if(closed||accepted.Count>=8){tcp.Close();continue;}accepted.Enqueue(new MatchLink(tcp));}}catch(ObjectDisposedException){}catch(SocketException){}}
  public bool Take(out MatchLink link)=>accepted.TryDequeue(out link);
  public void Dispose(){closed=true;server.Stop();while(accepted.TryDequeue(out var link))link.Dispose();}
 }
}
