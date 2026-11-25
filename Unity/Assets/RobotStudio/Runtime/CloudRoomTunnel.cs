using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AshinaStudio {
 internal sealed class TunnelAddressUnavailable : IOException {
  public TunnelAddressUnavailable():base("Geçici internet adresi DNS üzerinden bulunamadı."){}
 }

 internal sealed class CloudRoomTunnel : IDisposable {
  static readonly Regex PublicHost=new Regex(@"https://[a-z0-9-]+\.trycloudflare\.com\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  readonly Process process;
  readonly TaskCompletionSource<string> discovered=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
  readonly TaskCompletionSource<bool> registered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
  int disposed;
  public string Address {get;private set;}
  public bool Running {get{try{return disposed==0&&!process.HasExited;}catch(InvalidOperationException){return false;}}}

  public CloudRoomTunnel(string executable,int originPort){
   if(!File.Exists(executable))throw new IOException("İnternet oda aracı eksik. Son Windows ZIP paketinin tamamını çıkar.");
   var info=new ProcessStartInfo(executable,"tunnel --no-autoupdate --protocol http2 --url http://127.0.0.1:"+originPort){
    UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
    WorkingDirectory=Path.GetDirectoryName(executable)
   };
   process=new Process{StartInfo=info,EnableRaisingEvents=true};
   process.OutputDataReceived+=ReadAddress;process.ErrorDataReceived+=ReadAddress;
   process.Exited+=(sender,args)=>discovered.TrySetException(new IOException("İnternet oda aracı kapandı. Biraz sonra yeniden dene."));
   try{if(!process.Start())throw new IOException("İnternet oda aracı başlatılamadı.");process.BeginOutputReadLine();process.BeginErrorReadLine();}
   catch{Dispose();throw;}
  }

  void ReadAddress(object sender,DataReceivedEventArgs args){
   if(args.Data==null)return;
   var match=PublicHost.Match(args.Data);if(match.Success)discovered.TrySetResult(match.Value);
   if(args.Data.Contains("Registered tunnel connection"))registered.TrySetResult(true);
  }

  public async Task<string> Verify(string key,CancellationToken token){
   string status="Adres henüz alınmadı";
   using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){
    timeout.CancelAfter(TimeSpan.FromSeconds(55));
    try{
     if(await Task.WhenAny(discovered.Task,Task.Delay(Timeout.Infinite,timeout.Token)).ConfigureAwait(false)!=discovered.Task)timeout.Token.ThrowIfCancellationRequested();
     string address=await discovered.Task.ConfigureAwait(false);
     status="Adres alındı, WebSocket bağlantısı doğrulanıyor";
     await Task.WhenAny(registered.Task,Task.Delay(8000,timeout.Token)).ConfigureAwait(false);
     await Task.Delay(3000,timeout.Token).ConfigureAwait(false);
     var probeAddress=new UriBuilder(address){Scheme="wss",Port=-1,Path="/room/"+key}.Uri;
     int unresolved=0;
     while(Running){
      try{
       using(var attempt=CancellationTokenSource.CreateLinkedTokenSource(timeout.Token))using(var client=new ClientWebSocket()){
        attempt.CancelAfter(5500);
        await client.ConnectAsync(probeAddress,attempt.Token).ConfigureAwait(false);
        await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure,"",attempt.Token).ConfigureAwait(false);
        Address=address;return address;
       }
      }catch(WebSocketException error){
       var cause=error.GetBaseException();
       if(cause is SocketException socket){
        status=socket.SocketErrorCode.ToString();
        if((socket.SocketErrorCode==SocketError.HostNotFound||socket.SocketErrorCode==SocketError.NoData||socket.SocketErrorCode==SocketError.TryAgain)&&++unresolved>=4)throw new TunnelAddressUnavailable();
       }else status=cause is System.Net.WebException netError?netError.Status.ToString():cause.GetType().Name;
      }
      catch(OperationCanceledException) when(!timeout.IsCancellationRequested){status="WebSocket bağlantısı zaman aşımı";}
      await Task.Delay(650,timeout.Token).ConfigureAwait(false);
     }
    }catch(OperationCanceledException) when(!token.IsCancellationRequested){throw new IOException("İnternet daveti zamanında hazırlanamadı. Tekrar dene veya aynı ağ odasını kullan. ("+status+")");}
   }
   throw new IOException("İnternet bağlantısı kapanmış. Yeniden davet oluştur.");
  }

  public void Dispose(){
   if(Interlocked.Exchange(ref disposed,1)!=0)return;
   try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}finally{process.Dispose();}
  }
 }
}
