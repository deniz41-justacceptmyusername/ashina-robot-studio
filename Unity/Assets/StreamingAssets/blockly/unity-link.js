'use strict';
// The block interpreter is shared with the Blockly application's existing preview.
document.addEventListener('DOMContentLoaded',()=>{
 const params=new URLSearchParams(location.hash.slice(1)),token=params.get('unity');
 const button=document.createElement('button');button.className='primary';button.textContent='Unity bağlantısı';document.querySelector('header nav').append(button);
 let socket=null,runtime=null,last='',mode='disabled',sentProject=false,lastCheck=0;
 function connect(){if(!token){toast('Unity uygulamasında Blockly’yi aç düğmesini kullan.');return;}socket?.close();socket=new WebSocket('ws://127.0.0.1:'+(params.get('port')||5818));button.textContent='Unity bağlanıyor…';
  socket.onopen=()=>socket.send(JSON.stringify({type:'pair',token}));
  socket.onclose=()=>{button.textContent='Unity: bağlantı kapalı';runtime?.dispose();runtime=null;};
  socket.onerror=()=>{button.textContent='Unity: bağlantı kurulamadı';};
  socket.onmessage=event=>{try{const input=JSON.parse(event.data);if(input.type==='paired'){button.textContent='● Unity bağlı';return;}if(input.type==='project'){const loaded=FRC.normalize(input.project).project;FRC.compileAll(loaded);P=loaded;activeSS=-1;activeMeth=-1;clearWorkspace();renderAll();commit(false);last='';return;}if(input.type!=='tick')return;
   let raw=last;if(!runtime||performance.now()-lastCheck>500){saveBlocks();raw=JSON.stringify(P);lastCheck=performance.now();}if(raw!==last||!runtime){runtime?.dispose();runtime=new FRC.StudioPreview(P);last=raw;sentProject=false;mode='disabled';}
   if(mode!==input.mode){runtime.changeMode(input.mode);mode=input.mode;}runtime.pose={...input.pose};runtime.axes={...runtime.axes,...input.axes};runtime.buttons=new Set(input.buttons||[]);
   for(const camera of runtime.project.integrations.cameras){const data=input.cameras?.[camera.name];if(data)runtime.cameras.set(camera.id,data);}
   runtime.step();const motors=Object.fromEntries([...runtime.states].map(([path,s])=>[path,s.duty||0]));const output={type:'state',velocity:runtime.velocity,motors,errors:[...runtime.errors]};if(!sentProject){output.project=P;sentProject=true;}socket.send(JSON.stringify(output));
   if(runtime.errors.size)button.title=[...runtime.errors].join('\n');
  }catch(e){button.title=e.message;button.textContent='Unity: '+e.message.slice(0,45);}};
 }
 button.onclick=connect;if(token)connect();window.addEventListener('beforeunload',()=>socket?.close());
});

