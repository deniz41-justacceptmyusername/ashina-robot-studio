'use strict';
FRC.dropdown = (block, kind) => {
  const ws=block.workspace,p=ws.frcProject||P,owner=ws.frcOwner??P.subsystems[activeSS]?.name;
  let options=[];
  for(const ss of p.subsystems.filter(s=>s.name===owner)) {
    const source=kind==='METHOD'?ss.methods:ss.components.filter(c=>c.type===({MOTOR:'motor',ENCODER:'encoder',SWITCH:'limitswitch'}[kind]));
    for(const item of source)options.push([item.name,ss.name+'.'+item.name]);
  }
  for(const value of ws.frcExtras?.[kind]||[])if(!options.some(o=>o[1]===value))options.push(['⚠ '+value,value]);
  return options.length?options:[['Önce bileşen ekleyin','']];
};
FRC.xmlExtras = xml => {
  const extras={};
  for(const f of xml.getElementsByTagName('field'))if(['MOTOR','ENCODER','SWITCH','METHOD','DEVICE','CAMERA','GLOBALMETHOD'].includes(f.getAttribute('name'))){const k=f.getAttribute('name');(extras[k]??=new Set()).add(f.textContent);}
  return extras;
};
FRC.installBlocks = () => {
  for(const kind of ['MOTOR','ENCODER','SWITCH','METHOD'])Blockly.Extensions.register('frc_'+kind.toLowerCase()+'_menu',function(){this.getField(kind).menuGenerator_=()=>FRC.dropdown(this,kind);});
  const menu=(name)=>({type:'field_dropdown',name,options:[['Bileşen seçin','']]}),num=(name)=>({type:'input_value',name,check:'Number'}),bool=(name)=>({type:'input_value',name,check:'Boolean'});
  const statement=(type,message0,args0,colour,extensions=[])=>({type,message0,args0,previousStatement:null,nextStatement:null,colour,extensions});
  FRC.installExtraBlocks?.();
  Blockly.defineBlocksWithJsonArray([
    {type:'frc_method_start',message0:'%1 . %2 çağrıldığında',args0:[{type:'field_label',name:'SSN',text:'Alt sistem'},{type:'field_label',name:'MN',text:'metot'}],nextStatement:null,colour:120,hat:'cap'},
    statement('frc_drive_motor','%1 motor gücü %2',[menu('MOTOR'),num('SPEED')],230,['frc_motor_menu']),
    statement('frc_drive_velocity','%1 hedef hız %2 RPS',[menu('MOTOR'),num('VEL')],230,['frc_motor_menu']),
    statement('frc_drive_current','%1 hedef akım %2 A',[menu('MOTOR'),num('CUR')],230,['frc_motor_menu']),
    statement('frc_stop_motor','%1 motoru durdur',[menu('MOTOR')],230,['frc_motor_menu']),
    ...[['frc_enc_pos','%1 konum (tur)'],['frc_enc_vel','%1 hız (RPM)']].map(([type,message0])=>({type,message0,args0:[menu('ENCODER')],output:'Number',colour:180,extensions:['frc_encoder_menu']})),
    {type:'frc_limitswitch',message0:'%1 basılı mı?',args0:[menu('SWITCH')],output:'Boolean',colour:120,extensions:['frc_switch_menu']},
    {type:'frc_gyro_angle',message0:'Gyro açısı (derece)',output:'Number',colour:290},
    statement('frc_zero_gyro','Gyro sıfırla',[],290),
    {type:'frc_if',message0:'Eğer %1',args0:[bool('COND')],message1:'yap %1',args1:[{type:'input_statement',name:'DO'}],previousStatement:null,nextStatement:null,colour:210},
    {type:'frc_if_else',message0:'Eğer %1',args0:[bool('COND')],message1:'yap %1',args1:[{type:'input_statement',name:'DO'}],message2:'değilse %1',args2:[{type:'input_statement',name:'ELSE'}],previousStatement:null,nextStatement:null,colour:210},
    {type:'frc_compare',message0:'%1 %2 %3',args0:[num('A'),{type:'field_dropdown',name:'OP',options:[['>','GT'],['<','LT'],['≥','GTE'],['≤','LTE'],['=','EQ'],['≠','NEQ']]},num('B')],output:'Boolean',colour:210},
    {type:'frc_bool',message0:'%1 %2 %3',args0:[bool('A'),{type:'field_dropdown',name:'OP',options:[['VE','AND'],['VEYA','OR']]},bool('B')],output:'Boolean',colour:210},
    {type:'frc_not',message0:'DEĞİL %1',args0:[bool('V')],output:'Boolean',colour:210},
    statement('frc_setvar','%1 değişkenini %2 yap',[{type:'field_input',name:'VAR',text:'hedef'},num('VAL')],330),
    {type:'frc_getvar',message0:'%1',args0:[{type:'field_input',name:'VAR',text:'hedef'}],output:'Number',colour:330},
    {type:'frc_controller_axis',message0:'Kumanda %1',args0:[{type:'field_dropdown',name:'AXIS',options:[['sol X','LeftX'],['sol Y','LeftY'],['sağ X','RightX'],['sağ Y','RightY'],['sol tetik','LeftTriggerAxis'],['sağ tetik','RightTriggerAxis']]}],output:'Number',colour:65},
    statement('frc_call','%1 metodunu çağır',[menu('METHOD')],120,['frc_method_menu']),
    statement('frc_telemetry','Dashboard %1 = %2',[{type:'field_input',name:'KEY',text:'deger'},num('VALUE')],45),
    statement('frc_drive','Sürüş ileri %1 sol %2 dönüş %3',[num('X'),num('Y'),num('ROT')],65)
  ]);
};
FRC.toolbox = () => ({kind:'categoryToolbox',contents:[
  ['Motor','#e94560',['frc_drive_motor','frc_drive_velocity','frc_drive_current','frc_stop_motor']],
  ['Sensör','#0d9488',['frc_enc_pos','frc_enc_vel','frc_limitswitch','frc_gyro_angle','frc_zero_gyro']],
  ['Mantık','#3b82f6',['frc_if','frc_if_else','frc_compare','frc_bool','frc_not','logic_boolean']],
  ['Sayı / işlem','#ec4899',['math_number','math_arithmetic','math_single','math_modulo','math_constrain']],
  ['Değişken','#f59e0b',['frc_setvar','frc_getvar']],
  ['Kumanda / sürüş','#84cc16',['frc_controller_axis','frc_drive']],
  ['Metot / izleme','#a855f7',['frc_call','frc_telemetry']]
,...(FRC.extraToolbox?.()||[])].map(([name,colour,types])=>({kind:'category',name,colour,contents:types.map(type=>{
  const item={kind:'block',type},input={frc_drive_motor:['SPEED',0.2],frc_drive_velocity:['VEL',1],frc_drive_current:['CUR',5],frc_setvar:['VAL',0],frc_telemetry:['VALUE',0]}[type];
  if(input)item.inputs={[input[0]]:{shadow:{type:'math_number',fields:{NUM:input[1]}}}};
  return item;
})}))});
FRC.compileMethod = (p,ss,m) => {
  const result={code:'',variables:new Set(),reads:new Set(),calls:new Set(),actuates:false,errors:[],warnings:[]};
  if(!m.blocks){result.warnings.push(ss.name+'.'+m.name+': metot boş.');return result;}
  const ws=new Blockly.Workspace();ws.frcProject=p;ws.frcOwner=ss.name;
  const fail=(block,msg)=>{throw Error(msg+' [blok '+block.type+' / '+block.id+']');};
  let depth=0;
  const nameOf=b=>{const n=b.getFieldValue('VAR');if(!FRC.identifier(n))fail(b,'Geçersiz değişken adı: '+n);return n;};
  function component(b,field,type){
    const path=b.getFieldValue(field),prefix=ss.name+'.';
    if(!path?.startsWith(prefix))fail(b,'Bu metotta yalnızca '+ss.name+' bileşenleri kullanılabilir: '+path);
    const c=ss.components.find(c=>c.name===path.slice(prefix.length)&&c.type===type);
    if(!c)fail(b,'Bileşen silinmiş veya bulunamadı: '+path);return c;
  }
  function input(b,key,type){const target=b.getInputTargetBlock(key);if(!target)fail(b,key+' girişine bir blok bağlayın.');const e=expression(target);if(e.type!==type)fail(b,key+' girişi '+type+' olmalı.');return e.code;}
  function expression(b){
    if(!b.isEnabled())fail(b,'Devre dışı değer bloğunu etkinleştirin veya değiştirin.');
    if(++depth>128)fail(b,'Blok iç içe geçme sınırı aşıldı.');
    try{
      let code,type='Number';
      switch(b.type){
        case 'math_number': {const n=Number(b.getFieldValue('NUM'));if(!Number.isFinite(n))fail(b,'Sayı sonlu olmalı.');code=FRC.javaNumber(n);break;}
        case 'logic_boolean':code=b.getFieldValue('BOOL')==='TRUE'?'true':'false';type='Boolean';break;
        case 'frc_enc_pos':case 'frc_enc_vel':{const c=component(b,'ENCODER','encoder');if(b.type==='frc_enc_vel'&&['throughbore_dio','hexbore_dio'].includes(c.encType))result.warnings.push(ss.name+'.'+m.name+': DIO mutlak enkoder RPM hesabı örnekler arası farktır; 20 ms döngüde 1500 RPM üstü hızlar aliasing nedeniyle ölçülemez.');code='frc_'+c.name+(b.type==='frc_enc_pos'?'Position()':'RPM()');break;}
        case 'frc_limitswitch':{const c=component(b,'SWITCH','limitswitch');code=(c.normallyOpen?'!':'')+c.name+'.get()';type='Boolean';break;}
        case 'frc_gyro_angle':if(p.swerveConfig.gyro.type==='none')fail(b,'Gyro bloğu için bir gyro seçin.');code='RobotIO.headingDegrees()';break;
        case 'frc_getvar':{const n=nameOf(b);result.reads.add(n);code='frc_var_'+n;break;}
        case 'frc_controller_axis':{const axis=b.getFieldValue('AXIS');if(!['LeftX','LeftY','RightX','RightY','LeftTriggerAxis','RightTriggerAxis'].includes(axis))fail(b,'Geçersiz kumanda ekseni.');code='RobotIO.driver.get'+axis+'()';break;}
        case 'frc_compare':case 'logic_compare':{const ops={GT:'>',LT:'<',GTE:'>=',LTE:'<=',EQ:'==',NEQ:'!='},op=ops[b.getFieldValue('OP')];if(!op)fail(b,'Geçersiz karşılaştırma.');code='('+input(b,'A','Number')+' '+op+' '+input(b,'B','Number')+')';type='Boolean';break;}
        case 'frc_bool':case 'logic_operation':code='('+input(b,'A','Boolean')+(b.getFieldValue('OP')==='AND'?' && ':' || ')+input(b,'B','Boolean')+')';type='Boolean';break;
        case 'frc_not':code='(!'+input(b,'V','Boolean')+')';type='Boolean';break;
        case 'logic_negate':code='(!'+input(b,'BOOL','Boolean')+')';type='Boolean';break;
        case 'math_arithmetic':{const a=input(b,'A','Number'),c=input(b,'B','Number'),op=b.getFieldValue('OP'),ops={ADD:'+',MINUS:'-',MULTIPLY:'*',DIVIDE:'/'};code=op==='POWER'?'Math.pow('+a+', '+c+')':ops[op]?'('+a+' '+ops[op]+' '+c+')':fail(b,'Geçersiz işlem.');break;}
        case 'math_single':{const v=input(b,'NUM','Number'),op=b.getFieldValue('OP'),fn={ROOT:'sqrt',ABS:'abs',LN:'log',LOG10:'log10',EXP:'exp'}[op];code=fn?'Math.'+fn+'('+v+')':op==='NEG'?'(-'+v+')':op==='POW10'?'Math.pow(10.0, '+v+')':fail(b,'Desteklenmeyen matematik işlemi.');break;}
        case 'math_modulo':code='('+input(b,'DIVIDEND','Number')+' % '+input(b,'DIVISOR','Number')+')';break;
        case 'math_constrain':code='Math.max('+input(b,'LOW','Number')+', Math.min('+input(b,'HIGH','Number')+', '+input(b,'VALUE','Number')+'))';break;
        default:{const emitted=FRC.extensionExpression?.(b,{p,ss,m,component,input,fail,result});if(!emitted)fail(b,'Desteklenmeyen değer bloğu.');code=emitted.code;type=emitted.type;break;}
      }
      return {code,type};
    }finally{depth--;}
  }
  const indent=n=>'    '.repeat(n);
  function chain(first,level=2){let code='';for(let b=first;b;b=b.getNextBlock()){
    if(!b.isEnabled())continue;
    if(['frc_drive_motor','frc_stop_motor','frc_drive_velocity','frc_drive_current','frc_drive','frc_zero_gyro'].includes(b.type))result.actuates=true;
    const pad=indent(level);let line;
    switch(b.type){
      case 'frc_drive_motor':{const c=component(b,'MOTOR','motor');line='frc_set_'+c.name+'('+input(b,'SPEED','Number')+');';break;}
      case 'frc_stop_motor':{const c=component(b,'MOTOR','motor');line='frc_stop_'+c.name+'();';break;}
      case 'frc_drive_velocity':case 'frc_drive_current':{
        const c=component(b,'MOTOR','motor'),fam=FRC.motorFamily(c.motorType),velocity=b.type==='frc_drive_velocity';
        if(!['fx','max','flex'].includes(fam))fail(b,'Hız / akım kontrolü için TalonFX veya SPARK CAN sürücüsü gerekir.');
        if(!c.closedLoopConfigured)fail(b,c.name+': kapalı çevrim PID ayarlarını düzenleyip onaylayın.');
        if(velocity&&!FRC.isBrushless(c))fail(b,'Fırçalı motor hız kontrolü için geri bildirim adaptörü yapılandırılmalı.');
        if(!velocity&&fam==='fx'&&!c.focLicensed)fail(b,'TalonFX TorqueCurrentFOC için Phoenix Pro lisansı onayı gerekir.');
        line='frc_'+(velocity?'velocity_':'current_')+c.name+'('+input(b,velocity?'VEL':'CUR','Number')+');';break;
      }
      case 'frc_zero_gyro':if(p.swerveConfig.gyro.type==='none')fail(b,'Gyro bloğu için bir gyro seçin.');line='RobotServices.zeroGyro();';break;
      case 'frc_setvar':{const n=nameOf(b);result.variables.add(n);line='frc_var_'+n+' = '+input(b,'VAL','Number')+';';break;}
      case 'frc_if':case 'frc_if_else':{
        code+=pad+'if ('+input(b,'COND','Boolean')+') {\n'+chain(b.getInputTargetBlock('DO'),level+1)+pad+'}';
        if(b.type==='frc_if_else')code+=' else {\n'+chain(b.getInputTargetBlock('ELSE'),level+1)+pad+'}';code+='\n';continue;
      }
      case 'frc_call':{const path=b.getFieldValue('METHOD'),method=ss.methods.find(x=>ss.name+'.'+x.name===path);if(!method)fail(b,'Aynı alt sistem içinde geçerli bir metot seçin.');result.calls.add(method.name);line='this.'+method.name+'();';break;}
      case 'frc_telemetry':line='SmartDashboard.putNumber('+FRC.s(ss.name+'/'+b.getFieldValue('KEY'))+', '+input(b,'VALUE','Number')+');';break;
      case 'frc_drive':if(FRC.driveOwner(p)?.name!==ss.name)fail(b,'Bu blok sürüş alt sisteminin metotlarında kullanılabilir.');line='drive('+input(b,'X','Number')+', '+input(b,'Y','Number')+', '+input(b,'ROT','Number')+', false);';break;
      default:{const emitted=FRC.extensionStatement?.(b,{p,ss,m,component,input,fail,result});if(emitted==null)fail(b,'Desteklenmeyen komut bloğu.');line=emitted;break;}
    }
    code+=pad+line+'\n';
  }return code;}
  try{
    const xml=Blockly.utils.xml.textToDom(m.blocks);
    const rawBlocks=[...xml.getElementsByTagName('*')].filter(n=>['block','shadow'].includes(n.tagName.toLowerCase()));
    if(rawBlocks.length>2000)throw Error('Metotta en fazla 2000 blok kullanılabilir.');
    for(const n of rawBlocks){
      const type=n.getAttribute('type');if(!Blockly.Blocks[type])throw Error('Bilinmeyen blok türü: '+type);
      const allowed={frc_compare:['GT','LT','GTE','LTE','EQ','NEQ'],logic_compare:['GT','LT','GTE','LTE','EQ','NEQ'],frc_bool:['AND','OR'],logic_operation:['AND','OR'],math_arithmetic:['ADD','MINUS','MULTIPLY','DIVIDE','POWER'],math_single:['ROOT','ABS','NEG','LN','LOG10','EXP','POW10']}[type];
      for(const field of [...n.children].filter(x=>x.tagName.toLowerCase()==='field')){
        const name=field.getAttribute('name'),value=field.textContent;
        const choices=FRC.extraFieldEnums?.[type]?.[name];if(choices&&!choices.includes(value))throw Error(type+': geçersiz '+name+' alanı '+value);
        if(['frc_pid','frc_feedforward'].includes(type)&&['KP','KI','KD','KS','KG','KV'].includes(name)&&(value.trim()===''||!Number.isFinite(Number(value))||Number(value)<0||type==='frc_pid'&&Number(value)>10000))throw Error(type+': geçersiz katsayı '+name);
        if(name==='OP'&&allowed&&!allowed.includes(value))throw Error(type+': geçersiz işlem alanı '+value);
        if(name==='AXIS'&&!['LeftX','LeftY','RightX','RightY','LeftTriggerAxis','RightTriggerAxis'].includes(value))throw Error('Geçersiz kumanda ekseni: '+value);
        if(name==='BOOL'&&!['TRUE','FALSE'].includes(value))throw Error('Geçersiz boolean alanı: '+value);
        if(type==='math_number'&&name==='NUM'&&(value.trim()===''||!Number.isFinite(Number(value))))throw Error('Geçersiz sayı alanı: '+value);
      }
    }
    ws.frcExtras=FRC.xmlExtras(xml);Blockly.Xml.domToWorkspace(xml,ws);
    if(ws.getAllBlocks(false).length>2000)throw Error('Metotta en fazla 2000 blok kullanılabilir.');
    const tops=ws.getTopBlocks(true).filter(b=>b.isEnabled()),hats=tops.filter(b=>b.type==='frc_method_start');
    if(hats.length!==1||tops.length!==1)throw Error('Komutları tek metot başlangıcına bağlayın; boşta blok bırakmayın.');
    result.code=chain(hats[0].getNextBlock());
  }catch(e){result.errors.push(ss.name+'.'+m.name+': '+e.message);}
  finally{ws.dispose();}
  return result;
};
FRC.compileAll = p => {
  const result=FRC.validate(p),compiled=new Map();
  for(const ss of p.subsystems){
    const vars=new Set(),reads=new Set(),methods=new Map();
    for(const m of ss.methods){const r=FRC.compileMethod(p,ss,m);methods.set(m.name,r);r.variables.forEach(v=>vars.add(v));r.reads.forEach(v=>reads.add(v));result.errors.push(...r.errors);result.warnings.push(...r.warnings);}
    for(const v of reads)if(!vars.has(v))result.errors.push(ss.name+': '+v+' değişkeni okunuyor fakat hiçbir metotta tanımlanmamış.');
    const controllers=new Map();for(const method of methods.values())for(const [name,gains]of method.controllers||[]){if(controllers.has(name)&&JSON.stringify(controllers.get(name))!==JSON.stringify(gains))result.errors.push(ss.name+': PID '+name+' farklı katsayılarla kullanılmış.');controllers.set(name,gains);}
    const visiting=new Set(),done=new Set();
    function visit(name){if(visiting.has(name)){result.errors.push(ss.name+'.'+name+': döngüsel metot çağrısı engellendi.');return;}if(done.has(name))return;visiting.add(name);for(const target of methods.get(name)?.calls||[])visit(target);visiting.delete(name);done.add(name);}
    for(const name of methods.keys())visit(name);
    const actuates=(name,seen=new Set())=>{if(seen.has(name))return false;seen.add(name);const m=methods.get(name);return !!m&&(m.actuates||[...m.calls].some(n=>actuates(n,seen)));};
    for(const m of ss.methods)if(m.role==='periodic'&&actuates(m.name))result.errors.push(ss.name+'.'+m.name+': periodic metodu motor / gyro komutu içeriyor; varsayılan teleop veya çağrılan metot kullanın.');
    compiled.set(ss.name,{vars,methods});
  }
  result.compiled=compiled;return result;
};
