'use strict';
// An explicit popup handshake works on file:// and on the optional localhost launcher.
document.addEventListener('DOMContentLoaded',()=>{
 const button=document.createElement('button');button.id='open-3d-simulator';button.textContent='↗ 3B simülatör';button.className='primary';document.querySelector('header nav').append(button);
 let popup=null,token='',ready=false,last='',timer=null;
 function send(force=false){if(!ready||!popup||popup.closed)return;saveBlocks();const raw=JSON.stringify(P);if(!force&&raw===last)return;last=raw;popup.postMessage({type:'ashina:project',version:1,token,project:JSON.parse(raw)},location.protocol==='file:'?'*':location.origin);}
 button.onclick=()=>{token=crypto.randomUUID();ready=false;last='';popup=window.open('../index.html?link='+encodeURIComponent(token),'ashina-simulator');if(!popup){toast('Simülatör penceresi açılamadı. Açılır pencerelere izin verin.');return;}clearInterval(timer);timer=setInterval(()=>{if(popup.closed){clearInterval(timer);ready=false;return;}send();},500);};
 window.addEventListener('message',event=>{if(event.source!==popup||event.data?.token!==token||event.data?.type!=='ashina:ready')return;if(location.protocol!=='file:'&&event.origin!==location.origin)return;ready=true;send(true);});
 window.addEventListener('beforeunload',()=>{clearInterval(timer);if(ready&&popup&&!popup.closed)popup.postMessage({type:'ashina:closed',token},location.protocol==='file:'?'*':location.origin);});
});

