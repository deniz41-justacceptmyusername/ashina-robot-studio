'use strict';
let P,activeSS=-1,activeMeth=-1,workspace=null,selMod='frontleft',loadingBlocks=false,checkTimer,saveTimer,lastCheck;
const $=id=>document.getElementById(id),H=value=>FRC.html(value);
const STORAGE='frc-blockly-project-v2',BACKUP=STORAGE+'-backup';
function toast(message){$('toast').textContent=message;$('toast').classList.add('show');clearTimeout(toast.timer);toast.timer=setTimeout(()=>$('toast').classList.remove('show'),3500);}
function options(list,value){return list.map(([label,key])=>'<option value="'+H(key)+'"'+(key===value?' selected':'')+'>'+H(label)+'</option>').join('');}
function field(id,label,value,type='text',extra=''){return '<label for="'+id+'">'+H(label)+'</label><input id="'+id+'" type="'+type+'" value="'+H(value)+'" '+extra+'>';}
function select(id,label,list,value){return '<label for="'+id+'">'+H(label)+'</label><select id="'+id+'">'+options(list,value)+'</select>';}
function checkbox(id,label,value){return '<label class="check-label"><input id="'+id+'" type="checkbox"'+(value?' checked':'')+'>'+H(label)+'</label>';}
const number=id=>$(id).value.trim()===''?NaN:Number($(id).value);
function showModal(title,body,onSave,{wide=false,saveLabel='Kaydet'}={}){
  const c=$('modal-container');c.innerHTML='<div class="modal-overlay"><div class="modal'+(wide?' wide':'')+'" role="dialog" aria-modal="true" aria-labelledby="modal-title"><h3 id="modal-title">'+H(title)+'</h3><div id="modal-body">'+body+'</div><div id="modal-error" role="alert"></div><div class="modal-actions"><button id="modal-cancel">Kapat</button>'+(onSave?'<button class="primary" id="modal-save">'+H(saveLabel)+'</button>':'')+'</div></div></div>';
  $('modal-cancel').onclick=closeModal;
  c.firstChild.onclick=e=>{if(e.target===c.firstChild)closeModal();};
  if(onSave)$('modal-save').onclick=()=>{try{saveBlocks();if(onSave()!==false){closeModal();commit(false);}}catch(e){$('modal-error').textContent=e.message;}};
  c.querySelector('input,select,button')?.focus();
}
function closeModal(){$('modal-container').innerHTML='';}
function validName(name){if(!FRC.identifier(name)||FRC.reserved.has(name)||/^frc_/.test(name))throw Error('Harfle başlayan, boşluksuz ve ayrılmış olmayan bir ad girin (ör. IntakeSubsystem, intakeMotor).');}
function renameReferences(oldPath,newPath){
  for(const ss of P.subsystems)for(const m of ss.methods)if(m.blocks){const xml=Blockly.utils.xml.textToDom(m.blocks);for(const f of xml.getElementsByTagName('field'))if(['MOTOR','ENCODER','SWITCH','METHOD','DEVICE','GLOBALMETHOD'].includes(f.getAttribute('name'))&&(f.textContent===oldPath||f.textContent.startsWith(oldPath+'.')))f.textContent=newPath+f.textContent.slice(oldPath.length);m.blocks=Blockly.Xml.domToText(xml);}
  for(const b of P.controllerBindings)for(const k of ['methodPath','stopMethod'])if(b[k]&&(b[k]===oldPath||b[k].startsWith(oldPath+'.')))b[k]=newPath+b[k].slice(oldPath.length);
  for(const a of P.autonomous)if(a.methodPath&&(a.methodPath===oldPath||a.methodPath.startsWith(oldPath+'.')))a.methodPath=newPath+a.methodPath.slice(oldPath.length);
  for(const [k,list] of Object.entries(P.drive.motors))P.drive.motors[k]=list.map(v=>(v===oldPath||v.startsWith(oldPath+'.'))?newPath+v.slice(oldPath.length):v);
  if(P.drive.subsystem===oldPath)P.drive.subsystem=newPath;
}
function commit(flush=true){
  if(flush)saveBlocks();
  clearTimeout(checkTimer);checkTimer=setTimeout(()=>checkProject(false),350);
  clearTimeout(saveTimer);$('save-state').textContent='Kaydediliyor…';
  saveTimer=setTimeout(()=>{try{localStorage.setItem(STORAGE,JSON.stringify(P));$('save-state').textContent='Bu tarayıcıya otomatik kaydedildi';}catch(e){$('save-state').textContent='Otomatik kayıt yok — JSON kaydedin';}},400);
}
function saveBlocks(){if(!workspace||loadingBlocks||activeSS<0||activeMeth<0)return;const method=P.subsystems[activeSS]?.methods[activeMeth];if(method)method.blocks=Blockly.Xml.domToText(Blockly.Xml.workspaceToDom(workspace));}
function loadBlocks(xmlText){
  loadingBlocks=true;Blockly.Events.disable();
  try{
    workspace.clear();workspace.frcProject=P;workspace.frcOwner=P.subsystems[activeSS]?.name;
    const xml=xmlText?Blockly.utils.xml.textToDom(xmlText):null;workspace.frcExtras=xml?FRC.xmlExtras(xml):{};
    if(xml)Blockly.Xml.domToWorkspace(xml,workspace);
    else if(activeSS>=0&&activeMeth>=0){const b=workspace.newBlock('frc_method_start');b.initSvg();b.render();b.moveBy(45,45);}
    for(const b of workspace.getAllBlocks(false).filter(b=>b.type==='frc_method_start')){b.setFieldValue(P.subsystems[activeSS].name,'SSN');b.setFieldValue(P.subsystems[activeSS].methods[activeMeth].name,'MN');b.setDeletable(false);b.setMovable(false);}
    workspace.updateToolbox(FRC.toolbox());
  }catch(e){toast('Bloklar açılamadı: '+e.message);$('workspace-state').textContent='Yükleme hatası; kaydı korumak için metodu düzenleyin.';throw e;}
  finally{workspace.clearUndo();Blockly.Events.enable();loadingBlocks=false;}
}
function clearWorkspace(){loadingBlocks=true;try{workspace.clear();}finally{loadingBlocks=false;}renderWorkspaceState();}
function renderWorkspaceState(){const ss=P.subsystems[activeSS],m=ss?.methods[activeMeth];$('method-label').textContent=m?ss.name+'.'+m.name+'()':'Metot seç';$('workspace-empty').hidden=!!m;$('method-options').hidden=!m;$('workspace-state').textContent=m?'Her değişiklik otomatik kaydedilir':'';}
function selectSS(i){saveBlocks();activeSS=i;activeMeth=-1;clearWorkspace();renderSS();renderComponents();}
function selectMethod(i){
  saveBlocks();const candidate=P.subsystems[activeSS].methods[i];
  if(candidate.blocks){try{const result=FRC.compileMethod(P,P.subsystems[activeSS],candidate);if(result.errors.some(e=>e.includes('Bilinmeyen blok')))throw Error(result.errors.join('\n'));}catch(e){toast(e.message);return;}}
  activeMeth=i;try{loadBlocks(candidate.blocks);}catch(e){activeMeth=-1;clearWorkspace();return;}
  saveBlocks();renderMethods();renderWorkspaceState();commit(false);
}
function editSubsystem(index=-1){
  const existing=P.subsystems[index];showModal(existing?'Alt sistemi düzenle':'Alt sistem ekle',field('ss-name','Java sınıf adı',existing?.name||'')+'<p class="hint">Örnek: IntakeSubsystem, ShooterSubsystem. Ad değişikliği blok ve düğme bağlantılarına uygulanır.</p>',()=>{
    const name=$('ss-name').value.trim();validName(name);if(P.subsystems.some((s,i)=>i!==index&&s.name===name))throw Error('Bu alt sistem adı kullanılıyor.');
    if(existing){const old=existing.name;renameReferences(old,name);existing.name=name;if(activeSS===index&&activeMeth>=0)loadBlocks(existing.methods[activeMeth].blocks);}
    else{P.subsystems.push({name,components:[],methods:[]});selectSS(P.subsystems.length-1);}
    renderSS();renderComponents();renderSettings();renderBindings();renderAuto();
  });
}
function deleteSS(i){if(!confirm(P.subsystems[i].name+' silinsin mi? Ona bağlı bloklar ve eylemler proje kontrolünde hata olarak gösterilir.'))return;saveBlocks();P.subsystems.splice(i,1);activeSS=-1;activeMeth=-1;clearWorkspace();renderAll();commit(false);}
function renderSS(){
  $('subsystem-list').innerHTML=P.subsystems.map((s,i)=>'<div class="item'+(i===activeSS?' active':'')+'"><button class="name" data-ss="'+i+'">'+H(s.name)+'<small>'+s.components.length+' bileşen · '+s.methods.length+' metot</small></button><button class="small" data-edit-ss="'+i+'" aria-label="Alt sistemi düzenle">✎</button><button class="danger" data-delete-ss="'+i+'" aria-label="Alt sistemi sil">×</button></div>').join('')||'<p class="empty">Henüz alt sistem yok.</p>';
  $('subsystem-list').querySelectorAll('[data-ss]').forEach(b=>b.onclick=()=>selectSS(Number(b.dataset.ss)));
  $('subsystem-list').querySelectorAll('[data-edit-ss]').forEach(b=>b.onclick=()=>editSubsystem(Number(b.dataset.editSs)));
  $('subsystem-list').querySelectorAll('[data-delete-ss]').forEach(b=>b.onclick=()=>deleteSS(Number(b.dataset.deleteSs)));
}
function editComponent(type,index=-1){
  if(activeSS<0){toast('Önce alt sistem seçin.');return;}
  const ss=P.subsystems[activeSS],old=ss.components[index],c=old?FRC.clone(old):{type,name:'',...(type==='motor'?FRC.motorDefaults():{encType:'builtin',parentMotor:'',canId:20,canbus:'rio',dioChannel:0,normallyOpen:true,inverted:false})};
  let body=field('c-name','Bileşen adı',c.name);
  if(type==='motor')body+=select('c-motor','Motor / sürücü',FRC.motorTypes,c.motorType)+'<div class="row">'+field('c-id','CAN ID',c.canId,'number','min="0" max="62"')+field('c-bus','CAN bus',c.canbus)+'</div>'+field('c-pwm','PWM kanalı',c.pwmChannel,'number','min="0" max="19"')+checkbox('c-invert','Motor yönünü ters çevir',c.inverted)+checkbox('c-brake','Fren modu (CAN)',c.brake)+field('c-limit','Akım sınırı (A, CAN)',c.currentLimit,'number')+'<h2>Kapalı çevrim</h2><p class="hint">Hedef hız RPS. TalonFX kV: V/RPS; SPARK kV: V/RPM. Katsayıları mekanizmaya göre ayarlayın.</p><div class="row">'+['p','i','d','ff'].map(k=>field('c-'+k,k==='ff'?'kV':k.toUpperCase(),c[k],'number','step="any"')).join('')+'</div>'+checkbox('c-tuned','PID / kV değerlerini bu mekanizma için yapılandırdım',c.closedLoopConfigured)+checkbox('c-pro','TalonFX akım kontrolü için Phoenix Pro lisansım var',c.focLicensed);
  if(type==='encoder')body+=select('c-encoder','Enkoder türü',FRC.encoderTypes,c.encType)+select('c-parent','Dahili / takılı enkoderin motoru',[['Seçin',''],...ss.components.filter(x=>x.type==='motor').map(x=>[x.name,x.name])],c.parentMotor)+'<div class="row">'+field('c-id','CAN ID',c.canId,'number')+field('c-bus','CAN bus',c.canbus)+'</div>'+field('c-dio','DIO kanalı (duty cycle)',c.dioChannel,'number')+checkbox('c-invert','Enkoder işaretini ters çevir',c.inverted);
  if(type==='limitswitch')body+=field('c-dio','DIO kanalı',c.dioChannel,'number')+checkbox('c-no','Normally open (basıldığında DIO düşük)',c.normallyOpen)+'<p class="hint">NC seçilirse basılı bilgisi DIO yüksek olduğunda true olur. Devre bağlantısını kontrol edin.</p>';
  if(type==='motor')body+=field('c-max-rps','Kapalı çevrim azami motor hızı (RPS)',c.maxRps,'number','min="0.1" max="1000" step="any"');
  if(type==='motor')body+=FRC.physicsForm?.(c)||'';
  if(type==='motor')body+=FRC.motionForm?.(c)||'';
  if(type==='encoder')body+='<h2>Quadrature enkoder</h2><div class="row">'+field('c-channel-a','DIO A',c.channelA??0,'number')+field('c-channel-b','DIO B',c.channelB??1,'number')+'</div>'+field('c-cpr','Tur başına darbe (WPILib count)',c.countsPerRev??2048,'number');
  showModal(old?'Bileşeni düzenle':'Bileşen ekle',body,()=>{
    const name=$('c-name').value.trim();validName(name);if(ss.components.some((v,i)=>i!==index&&v.name===name))throw Error('Bu ad kullanılıyor.');c.name=name;
    if(type==='motor'){c.motorType=$('c-motor').value;c.connection=FRC.motorFamily(c.motorType)==='pwm'?'PWM':'CAN';c.canId=number('c-id');c.canbus=$('c-bus').value.trim();c.pwmChannel=number('c-pwm');c.inverted=$('c-invert').checked;c.brake=$('c-brake').checked;c.currentLimit=number('c-limit');for(const k of ['p','i','d','ff'])c[k]=number('c-'+k);c.closedLoopConfigured=$('c-tuned').checked;c.focLicensed=$('c-pro').checked;}
    if(type==='encoder'){c.encType=$('c-encoder').value;c.parentMotor=$('c-parent').value;c.canId=number('c-id');c.canbus=$('c-bus').value.trim();c.dioChannel=number('c-dio');c.inverted=$('c-invert').checked;}
    if(type==='limitswitch'){c.dioChannel=number('c-dio');c.normallyOpen=$('c-no').checked;}
    if(type==='motor')c.maxRps=number('c-max-rps');
    if(type==='motor'&&FRC.readPhysicsForm)c.simulation=FRC.readPhysicsForm();
    if(type==='motor'&&FRC.readMotionForm)c.motion=FRC.readMotionForm();
    if(type==='encoder'&&c.encType==='quadrature'){c.channelA=number('c-channel-a');c.channelB=number('c-channel-b');c.countsPerRev=number('c-cpr');}
    if(old){renameReferences(ss.name+'.'+old.name,ss.name+'.'+name);for(const v of ss.components)if(v.parentMotor===old.name)v.parentMotor=name;ss.components[index]=c;}
    else ss.components.push(c);
    if(activeMeth>=0)loadBlocks(ss.methods[activeMeth].blocks);
    renderComponents();renderSettings();
  });
  if(type==='motor'){
    const update=()=>{const pwm=FRC.motorFamily($('c-motor').value)==='pwm';for(const key of ['c-id','c-bus'])$(key).disabled=pwm;$('c-pwm').disabled=!pwm;};$('c-motor').onchange=update;update();
  }
}
function renderComponents(){
  const ss=P.subsystems[activeSS];$('components-area').hidden=!ss;if(!ss){$('component-list').innerHTML='';renderMethods();return;}
  $('comp-label').textContent=ss.name;
  $('component-list').innerHTML=ss.components.map((c,i)=>'<div class="item"><button class="name" data-comp="'+i+'">'+H(c.name)+'<small>'+H(c.type==='motor'?c.motorType+' · '+(c.connection==='PWM'?'PWM '+c.pwmChannel:c.canbus+' CAN '+c.canId):c.type==='encoder'?c.encType+' '+(c.parentMotor||''):('DIO '+c.dioChannel+' · '+(c.normallyOpen?'NO':'NC')))+'</small></button><button class="danger" data-del-comp="'+i+'" aria-label="Bileşeni sil">×</button></div>').join('')||'<p class="empty">Motor veya sensör ekleyin.</p>';
  $('component-list').querySelectorAll('[data-comp]').forEach(b=>b.onclick=()=>{const i=Number(b.dataset.comp);editComponent(ss.components[i].type,i);});
  $('component-list').querySelectorAll('[data-del-comp]').forEach(b=>b.onclick=()=>{const i=Number(b.dataset.delComp);if(!confirm(ss.components[i].name+' silinsin mi?'))return;saveBlocks();ss.components.splice(i,1);if(activeMeth>=0)loadBlocks(ss.methods[activeMeth].blocks);renderComponents();renderSettings();commit(false);});
  renderMethods();
}
function editMethod(index=-1){
  if(activeSS<0){toast('Önce alt sistem ekleyin / seçin.');return;}
  const ss=P.subsystems[activeSS],old=ss.methods[index];
  showModal(old?'Metot ayarları':'Metot ekle',field('m-name','Metot adı',old?.name||'')+select('m-role','Çalışma biçimi',[['Düğme / otonom / başka metot çağırır','manual'],['Varsayılan teleop (her 20 ms)','default'],['Enabled periodic (izleme için)','periodic']],old?.role||'manual')+'<p class="hint">Varsayılan metot komut zamanlayıcısıyla çalışır. Periodic metotları izleme için kullanın; motor komutlarıyla yarışmamalıdır.</p>',()=>{
    const name=$('m-name').value.trim();validName(name);if(ss.methods.some((m,i)=>i!==index&&m.name===name))throw Error('Metot adı kullanılıyor.');
    if(old){renameReferences(ss.name+'.'+old.name,ss.name+'.'+name);old.name=name;old.role=$('m-role').value;if(activeMeth===index)loadBlocks(old.blocks);}
    else{ss.methods.push({name,role:$('m-role').value,blocks:null});selectMethod(ss.methods.length-1);}
    renderMethods();renderWorkspaceState();renderBindings();renderAuto();
  });
}
function renderMethods(){
  const ss=P.subsystems[activeSS];$('method-list').innerHTML=ss?ss.methods.map((m,i)=>'<div class="item'+(i===activeMeth?' active':'')+'"><button class="name" data-method="'+i+'">'+H(m.name)+'()<small>'+H({manual:'Çağrıldığında',default:'Varsayılan teleop',periodic:'Enabled periodic'}[m.role])+'</small></button><button class="small" data-edit-method="'+i+'">✎</button><button class="danger" data-del-method="'+i+'" aria-label="Metodu sil">×</button></div>').join('')||'<p class="empty">Metot ekleyerek blok yazmaya başlayın.</p>':'';
  $('method-list').querySelectorAll('[data-method]').forEach(b=>b.onclick=()=>selectMethod(Number(b.dataset.method)));
  $('method-list').querySelectorAll('[data-edit-method]').forEach(b=>b.onclick=()=>editMethod(Number(b.dataset.editMethod)));
  $('method-list').querySelectorAll('[data-del-method]').forEach(b=>b.onclick=()=>{if(!confirm('Metot silinsin mi?'))return;saveBlocks();const i=Number(b.dataset.delMethod);ss.methods.splice(i,1);activeMeth=-1;clearWorkspace();renderMethods();renderBindings();renderAuto();commit(false);});
}
function methodOptions(){return P.subsystems.flatMap(s=>s.methods.map(m=>[s.name+'.'+m.name+'()',s.name+'.'+m.name]));}
function bindButton(button){
  const old=P.controllerBindings.find(b=>b.button===button)||{type:'method',trigger:'whileTrue',methodPath:'',stopMethod:''},mo=[['Seçin',''],...methodOptions()];
  showModal(button+' düğmesini bağla',select('b-action','Eylem',[['Metot çalıştır','method'],['Gyro sıfırla','zeroGyro']],old.type)+select('b-method','Metot',mo,old.methodPath)+select('b-trigger','Tetikleme',[['Basılıyken sürekli; bırakınca motorları durdur','whileTrue'],['Basılınca bir kez','onTrue'],['Bırakılınca bir kez','onFalse']],old.trigger)+select('b-stop','Durdururken ek metot (isteğe bağlı)',[['Yok',''],...methodOptions()],old.stopMethod)+'<p class="hint">Basılı tutma komutu iptal edilince de motorlar durur. Bir kez çalışan metot çıkışı, başka bir komut durdurana kadar kalabilir.</p>',()=>{
    const b={button,type:$('b-action').value};if(b.type==='method'){b.methodPath=$('b-method').value;b.trigger=$('b-trigger').value;b.stopMethod=$('b-stop').value;if(!b.methodPath)throw Error('Bir metot seçin.');}
    P.controllerBindings=P.controllerBindings.filter(v=>v.button!==button);P.controllerBindings.push(b);renderBindings();
  });
  const update=()=>{for(const id of ['b-method','b-trigger','b-stop'])$(id).disabled=$('b-action').value==='zeroGyro';};$('b-action').onchange=update;update();
}
function renderBindings(){
  $('ctrl-map').innerHTML=FRC.buttons.map(([id,label])=>'<button data-button="'+id+'" class="'+(P.controllerBindings.some(b=>b.button===id)?'bound':'')+'">'+label+'</button>').join('');
  $('ctrl-map').querySelectorAll('[data-button]').forEach(b=>b.onclick=()=>bindButton(b.dataset.button));
  $('bindings-list').innerHTML=P.controllerBindings.map((b,i)=>'<div class="item"><button class="name" data-bind="'+i+'">'+H(b.button)+'<small>'+H(b.type==='zeroGyro'?'Gyro sıfırla':b.methodPath+' · '+b.trigger)+'</small></button><button class="danger" data-del-bind="'+i+'" aria-label="Düğme bağlantısını sil">×</button></div>').join('')||'<p class="empty">Henüz düğme atanmamış.</p>';
  $('bindings-list').querySelectorAll('[data-bind]').forEach(b=>b.onclick=()=>bindButton(P.controllerBindings[Number(b.dataset.bind)].button));
  $('bindings-list').querySelectorAll('[data-del-bind]').forEach(b=>b.onclick=()=>{P.controllerBindings.splice(Number(b.dataset.delBind),1);renderBindings();commit();});
}
function editAuto(index=-1){
  const old=P.autonomous[index]||{type:'method',duration:1,methodPath:''};
  showModal('Otonom adımı',select('a-type','Adım',[['Metot çalıştır','method'],['Bekle','wait']],old.type)+select('a-method','Metot',[['Seçin',''],...methodOptions()],old.methodPath)+field('a-duration','Süre (saniye)',old.duration,'number','min="0.02" max="15" step="0.02"'),()=>{
    const step={type:$('a-type').value,duration:number('a-duration')};if(step.type==='method'){step.methodPath=$('a-method').value;if(!step.methodPath)throw Error('Metot seçin.');}if(!Number.isFinite(step.duration)||step.duration<0.02||step.duration>15)throw Error('Süre 0.02–15 saniye olmalı.');if(index<0)P.autonomous.push(step);else P.autonomous[index]=step;renderAuto();
  });
  const update=()=>{$('a-method').disabled=$('a-type').value==='wait';};$('a-type').onchange=update;update();
}
function renderAuto(){
  $('auto-list').innerHTML=P.autonomous.map((a,i)=>'<div class="item"><button class="name" data-auto="'+i+'">'+(i+1)+'. '+H(a.type==='wait'?'Bekle':a.methodPath)+'<small>'+H(a.duration)+' saniye</small></button><button class="small" data-up="'+i+'"'+(i===0?' disabled':'')+' aria-label="Adımı yukarı taşı">↑</button><button class="danger" data-del-auto="'+i+'" aria-label="Adımı sil">×</button></div>').join('')||'<p class="empty">Otonom boş; robot otonomda sürüş komutu almaz.</p>';
  $('auto-list').querySelectorAll('[data-auto]').forEach(b=>b.onclick=()=>editAuto(Number(b.dataset.auto)));
  $('auto-list').querySelectorAll('[data-up]').forEach(b=>b.onclick=()=>{const i=Number(b.dataset.up);[P.autonomous[i-1],P.autonomous[i]]=[P.autonomous[i],P.autonomous[i-1]];renderAuto();commit();});
  $('auto-list').querySelectorAll('[data-del-auto]').forEach(b=>b.onclick=()=>{P.autonomous.splice(Number(b.dataset.delAuto),1);renderAuto();commit();});
}
function setPath(path,value){const keys=path.split('.');let obj=P;for(const key of keys.slice(0,-1))obj=obj[key];obj[keys.at(-1)]=value;commit();}
function configInput(path,label,value,type='number',step='any'){
  return '<label>'+H(label)+'</label><input data-path="'+H(path)+'" data-kind="'+type+'" type="'+(type==='number'?'number':'text')+'" step="'+step+'" value="'+H(value)+'">';
}
function configSelect(path,label,list,value){return '<label>'+H(label)+'</label><select data-path="'+H(path)+'">'+options(list,value)+'</select>';}
function configCheck(path,label,value){return '<label class="check-label"><input type="checkbox" data-path="'+H(path)+'"'+(value?' checked':'')+'>'+H(label)+'</label>';}
function wireConfig(root){root.querySelectorAll('[data-path]').forEach(el=>el.onchange=()=>{const value=el.type==='checkbox'?el.checked:el.dataset.kind==='number'?(el.value.trim()===''?NaN:Number(el.value)):el.value;setPath(el.dataset.path,value);if(root.id==='swerve-module-editor')drawSwerve();});}
function renderSettings(){
  let body=configInput('name','Proje adı',P.name,'text')+configInput('team','Takım numarası',P.team,'number','1')+configInput('controllerPort','Kumanda USB portu',P.controllerPort,'number','1')+configSelect('drive.type','Sürüş düzeni',[['Swerve','swerve'],['Differential / tank (arcade kumanda)','differential'],['Mecanum','mecanum'],['Otomatik sürüş yok; blok metotları','none']],P.drive.type)+configInput('drive.scale','Teleop sürüş güç katsayısı (0.01–1)',P.drive.scale)+configInput('swerveConfig.deadband','Kumanda deadband',P.swerveConfig.deadband);
  if(P.drive.type==='swerve')body+=configSelect('drive.subsystem','Swerve alt sistemi',[['Seçin',''],...P.subsystems.map(s=>[s.name,s.name])],P.drive.subsystem)+'<button id="create-swerve" class="small" style="margin-top:8px">SwerveSubsystem oluştur</button>'+configCheck('drive.fieldRelative','Teleop field relative sürüş',P.drive.fieldRelative);
  if(['differential','mecanum'].includes(P.drive.type))for(const slot of P.drive.type==='differential'?['left','right']:FRC.moduleNames){const current=P.drive.motors[slot]||[];body+='<label>'+H(slot)+' motorları (Ctrl ile çoklu seçim)</label><select multiple size="4" data-drive-slot="'+slot+'">'+P.subsystems.flatMap(s=>s.components.filter(c=>c.type==='motor').map(c=>{const path=s.name+'.'+c.name;return '<option value="'+H(path)+'"'+(current.includes(path)?' selected':'')+'>'+H(path)+'</option>';})).join('')+'</select>';}
  body+='<h2>Robot projesi</h2><p class="hint">WPILib 2026 Java + YAGSL. Vision, PathPlanner, Choreo, SysId, odometri ve telemetri için Araçlar sekmesini açın. Üretilen projeyi WPILib ile derleyip simülasyonda çalıştırın.</p><button id="download-backup" class="small">Önceki otomatik kaydı indir</button>';
  $('project-settings').innerHTML=body;wireConfig($('project-settings'));
  $('project-settings').querySelector('[data-path="drive.type"]').onchange=e=>{setPath('drive.type',e.target.value);renderSettings();};
  $('create-swerve')?.addEventListener('click',()=>{saveBlocks();if(!P.subsystems.some(s=>s.name==='SwerveSubsystem'))P.subsystems.push({name:'SwerveSubsystem',components:[],methods:[]});P.drive.subsystem='SwerveSubsystem';renderSS();renderSettings();commit(false);});
  $('project-settings').querySelectorAll('[data-drive-slot]').forEach(el=>el.onchange=()=>{P.drive.motors[el.dataset.driveSlot]=[...el.selectedOptions].map(o=>o.value);commit();});
  $('download-backup').onclick=()=>{const raw=localStorage.getItem(BACKUP);if(raw)download('frc_previous_project.json',new Blob([raw],{type:'application/json'}));else toast('Önceki kayıt henüz yok.');};
}
function renderModule(){
  const sc=P.swerveConfig;
  $('module-tabs').innerHTML=[...FRC.moduleNames,'global'].map((k,i)=>'<button data-module="'+k+'" class="'+(selMod===k?'active':'')+'">'+['FL','FR','BL','BR','Genel'][i]+'</button>').join('');
  $('module-tabs').querySelectorAll('[data-module]').forEach(b=>b.onclick=()=>{selMod=b.dataset.module;renderModule();drawSwerve();});
  let body='';
  if(selMod==='global'){
    body='<h2>Gyro / IMU</h2>'+configSelect('swerveConfig.gyro.type','Gerçek sensör modeli',FRC.gyros,sc.gyro.type)+'<div class="row">'+configInput('swerveConfig.gyro.id','CAN ID (CAN gyro)',sc.gyro.id,'number','1')+configInput('swerveConfig.gyro.canbus','CAN bus',sc.gyro.canbus,'text')+'</div>'+configCheck('swerveConfig.gyro.inverted','Gyro yönünü ters çevir',sc.gyro.inverted)+'<p class="hint">ADIS16470: SPI CS0, Z yaw. ADIS16448: MXP SPI. Sensör modelini ve montaj eksenini doğrulayın.</p><h2>Fiziksel değerler</h2>'+configInput('swerveConfig.maxSpeed','Azami hız (m/s)',sc.maxSpeed)+'<div class="row">';
    for(const [k,label] of [['wheelDiameter','Teker çapı (inç)'],['driveGearRatio','Drive dişli oranı'],['angleGearRatio','Angle dişli oranı'],['driveCurrentLimit','Drive akım sınırı (A)'],['angleCurrentLimit','Angle akım sınırı (A)']])body+=configInput('swerveConfig.physical.'+k,label,sc.physical[k]);
    body+='</div><h2>Swerve PID</h2><p class="hint">YAGSL dönüşüm birimleri: drive m/s; açı derece. Bunlar mekanizma motor PID değerlerinden ayrıdır.</p><div class="row">';
    for(const k of Object.keys(sc.pid))body+=configInput('swerveConfig.pid.'+k,k,sc.pid[k]);
    body+='</div><h2>Heading controller</h2><div class="row">';for(const k of ['angleDeadband','headingP','headingI','headingD'])body+=configInput('swerveConfig.controller.'+k,k,sc.controller[k]);body+='</div>';
  }else{
    const m=sc.modules[selMod],prefix='swerveConfig.modules.'+selMod;
    body='<h2>'+H(selMod)+'</h2><div class="row">'+configInput(prefix+'.location.front','Öne uzaklık (inç)',m.location.front)+configInput(prefix+'.location.left','Sola uzaklık (inç)',m.location.left)+'</div>';
    for(const [kind,label] of [['drive','Drive motoru'],['angle','Açı motoru'],['encoder','Mutlak enkoder']]){
      const d=m[kind];body+='<h2>'+label+'</h2>'+configSelect(prefix+'.'+kind+'.type','Tür',kind==='encoder'?FRC.swerveEncoders:FRC.swerveMotors,d.type)+'<div class="row">'+configInput(prefix+'.'+kind+'.id',kind==='encoder'?'CAN ID / DIO / analog kanal':'CAN ID',d.id,'number','1')+configInput(prefix+'.'+kind+'.canbus','CAN bus',d.canbus,'text')+'</div>'+configCheck(prefix+'.'+kind+'.inverted','Yönü ters çevir',d.inverted);
      if(kind==='encoder')body+=configInput(prefix+'.encoder.offset','Sıfır ofseti (derece)',d.offset);
    }
    body+='<p class="hint">Enkoder attached ise angle sürücüsüyle uyumlu MAX / Flex portunu seçin. CAN ve kanal 0 geçerlidir.</p>';
  }
  $('swerve-module-editor').innerHTML=body;wireConfig($('swerve-module-editor'));
}
function drawSwerve(){
  const cv=$('swerve-canvas'),ctx=cv.getContext('2d'),cx=140,cy=150;
  ctx.clearRect(0,0,280,280);ctx.fillStyle='#1d3049';ctx.fillRect(77,75,126,150);ctx.strokeStyle='#3b526e';ctx.strokeRect(77,75,126,150);
  ctx.fillStyle='#fb7185';ctx.font='bold 12px Segoe UI';ctx.textAlign='center';ctx.fillText('ROBOT ÖNÜ ↑',cx,38);
  const coords=[[75,75],[205,75],[75,225],[205,225]];
  coords.forEach(([x,y],i)=>{const name=FRC.moduleNames[i],m=P.swerveConfig.modules[name];ctx.beginPath();ctx.arc(x,y,21,0,Math.PI*2);ctx.fillStyle=name===selMod?'#d94565':'#284361';ctx.fill();ctx.fillStyle='white';ctx.font='bold 12px Segoe UI';ctx.fillText(['FL','FR','BL','BR'][i],x,y+4);ctx.font='9px Segoe UI';ctx.fillStyle='#b7c6d7';ctx.fillText(m.drive.id+' / '+m.angle.id+' / '+m.encoder.id,x,y+(i<2?-30:35));});
  ctx.fillStyle='#b7c6d7';ctx.font='10px Segoe UI';ctx.fillText('drive / angle / encoder',cx,155);
}
function checkProject(flush=true){
  if(flush)saveBlocks();
  try{lastCheck=FRC.compileAll(P);}catch(e){lastCheck={errors:['Proje yapısı geçersiz: '+e.message],warnings:[]};}
  $('diagnostics').innerHTML=(lastCheck.errors.length?'<div class="diagnostic error"><strong>'+lastCheck.errors.length+' hata · dışa aktarım engelli</strong></div>':'<div class="diagnostic success">Blok ve ayar kontrolü geçti. Donanım kalibrasyonu ayrıca gerekir.</div>')+lastCheck.errors.map(e=>'<div class="diagnostic error">'+H(e)+'</div>').join('')+lastCheck.warnings.map(w=>'<div class="diagnostic warning">'+H(w)+'</div>').join('');
  return lastCheck;
}
function previewCode(){
  saveBlocks();const check=checkProject(false);if(check.errors.length){toast('Önce proje kontrolündeki hataları düzeltin.');return;}
  try{const {files}=FRC.buildFiles(P),names=Object.keys(files).filter(n=>typeof files[n]==='string'&&(/\.java$|\.json$|\.gradle$/.test(n)));
    showModal('Üretilen robot kodu',select('preview-file','Dosya',names.map(n=>[n,n]),names.find(n=>n.endsWith('RobotContainer.java')))+'<pre id="code-preview"></pre>',null,{wide:true});const update=()=>{$('code-preview').textContent=files[$('preview-file').value];};$('preview-file').onchange=update;update();
  }catch(e){toast(e.message);}
}
function download(name,blob){const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=name;document.body.appendChild(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),10000);}
function saveProject(){saveBlocks();download(P.name+'_blocks.json',new Blob([JSON.stringify(P,null,2)],{type:'application/json'}));toast('Blok projesi kaydedildi.');}
async function exportProject(){
  saveBlocks();const check=checkProject(false);if(check.errors.length){toast('Dışa aktarım için '+check.errors.length+' hatayı düzeltin.');return;}
  const btn=$('export');btn.disabled=true;btn.textContent='Paketleniyor…';
  try{const {files}=FRC.buildFiles(P),zip=new JSZip();for(const [name,content] of Object.entries(files)){const opts=typeof content==='object'?{base64:true}:{};opts.unixPermissions=name==='gradlew'?0o100755:0o100644;zip.file(P.name+'/'+name,typeof content==='object'?content.base64:content,opts);}download(P.name+'_2026.zip',await zip.generateAsync({type:'blob',compression:'DEFLATE',platform:'UNIX'}));toast('WPILib 2026 projesi hazır.');}
  catch(e){toast('Dışa aktarım başarısız: '+e.message);}
  finally{btn.disabled=false;btn.textContent='Robot projesi indir';}
}
async function loadProject(event){
  const file=event.target.files?.[0];if(!file)return;
  try{
    if(file.size>25*1024*1024)throw Error('Proje dosyası 25 MB sınırını aşıyor.');
    const {project,notes}=FRC.normalize(JSON.parse(await file.text()));FRC.compileAll(project);
    saveBlocks();try{localStorage.setItem(BACKUP,JSON.stringify(P));}catch{}
    P=project;activeSS=-1;activeMeth=-1;clearWorkspace();renderAll();commit(false);toast(notes.length?'Proje yüklendi; taşınan ayarları kontrol edin.':'Proje yüklendi.');
    if(notes.length)showModal('Eski proje taşındı',notes.map(n=>'<p>'+H(n)+'</p>').join(''),null);
  }catch(e){showModal('Proje yüklenemedi','<p>'+H(e.message)+'</p><p>Mevcut proje korundu.</p>',null);}
  finally{event.target.value='';}
}
function renderAll(){renderSS();renderComponents();renderBindings();renderAuto();renderSettings();renderModule();drawSwerve();renderWorkspaceState();checkProject(false);}
function loadExample(){
  if(!confirm('Mevcut proje önceki otomatik kayıt olarak saklanıp örnek Ashina projesi açılsın mı?'))return;
  saveBlocks();try{localStorage.setItem(BACKUP,JSON.stringify(P));}catch{}
  P=FRC.example();activeSS=-1;activeMeth=-1;clearWorkspace();renderAll();selectSS(0);if(P.subsystems[0].methods.length)selectMethod(0);commit(false);
}
function init(){
  if(!window.Blockly||!window.JSZip||!window.FRC?.assets){document.body.innerHTML='<div class="fatal"><h1>Uygulama dosyaları yüklenemedi.</h1><p>ZIP dosyasını tamamen bir klasöre çıkarın; index.html, vendor ve diğer dosyalar birlikte bulunmalı.</p></div>';return;}
  P=FRC.defaultProject();P.subsystems.push({name:'SwerveSubsystem',components:[],methods:[]});FRC.installBlocks();
  try{const raw=localStorage.getItem(STORAGE);if(raw){const recovered=FRC.normalize(JSON.parse(raw)).project;FRC.compileAll(recovered);P=recovered;}}catch(e){try{const raw=localStorage.getItem(STORAGE);if(raw)localStorage.setItem(BACKUP,raw);}catch{}toast('Otomatik kayıt okunamadı; önceki kayıt yedeklendi. JSON yedeğinizi açabilirsiniz.');}
  workspace=Blockly.inject('blocklyDiv',{toolbox:FRC.toolbox(),media:'vendor/media/',sounds:false,grid:{spacing:20,length:3,colour:'#314057',snap:true},zoom:{controls:true,wheel:true},trashcan:true,theme:Blockly.Theme.defineTheme('frc2026',{base:Blockly.Themes.Classic,componentStyles:{workspaceBackgroundColour:'#101827',toolboxBackgroundColour:'#172438',toolboxForegroundColour:'#edf3fa',flyoutBackgroundColour:'#20324a',flyoutForegroundColour:'#edf3fa',scrollbarColour:'#60718a',scrollbarOpacity:0.6}})});
  workspace.addChangeListener(event=>{if(!loadingBlocks&&activeSS>=0&&activeMeth>=0&&!event.isUiEvent&&event.type!==Blockly.Events.FINISHED_LOADING)commit();});
  new ResizeObserver(()=>Blockly.svgResize(workspace)).observe($('workspace-wrap'));
  $('add-subsystem').onclick=()=>editSubsystem();$('add-method').onclick=()=>editMethod();$('method-options').onclick=()=>editMethod(activeMeth);
  $('add-first-method').onclick=()=>{if(activeSS<0&&P.subsystems.length)selectSS(0);if(activeSS<0)editSubsystem();else editMethod();};
  document.querySelectorAll('[data-add]').forEach(b=>b.onclick=()=>editComponent(b.dataset.add));
  document.querySelectorAll('[data-tab]').forEach(b=>b.onclick=()=>{document.querySelectorAll('[data-tab]').forEach(x=>x.classList.toggle('active',x===b));for(const name of ['subsystems','swerve','settings'])$('tab-'+name).hidden=b.dataset.tab!==name;if(b.dataset.tab==='swerve')drawSwerve();});
  $('save-json').onclick=saveProject;$('load-json').onclick=()=>$('load-file').click();$('load-file').onchange=loadProject;$('export').onclick=exportProject;$('preview').onclick=previewCode;$('check').onclick=()=>checkProject();$('add-auto').onclick=()=>editAuto();$('load-example').onclick=loadExample;
  $('swerve-canvas').onclick=e=>{const r=e.target.getBoundingClientRect(),x=(e.clientX-r.left)*280/r.width,y=(e.clientY-r.top)*280/r.height;for(const [i,[mx,my]] of [[75,75],[205,75],[75,225],[205,225]].entries())if(Math.hypot(x-mx,y-my)<27){selMod=FRC.moduleNames[i];renderModule();drawSwerve();}};
  document.addEventListener('keydown',e=>{if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='s'){e.preventDefault();saveProject();}if(e.key==='Escape')closeModal();});
  window.addEventListener('beforeunload',()=>{saveBlocks();try{localStorage.setItem(STORAGE,JSON.stringify(P));}catch{}});
  renderAll();$('save-state').textContent='Yerel çalışma · JSON ile taşınabilir';
}
document.addEventListener('DOMContentLoaded',()=>{try{init();}catch(e){console.error(e);$('diagnostics').innerHTML='<div class="diagnostic error">Başlatma hatası: '+H(e.message)+'</div>';}});
