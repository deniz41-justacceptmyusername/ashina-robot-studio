'use strict';
// Browser preview deliberately uses a bounded interpreter, never eval / user Java.
class StudioPreview {
  constructor(project){this.project=FRC.clone(project);this.reset();}
  reset(){this.time=0;this.pose={x:1,y:1,angle:0};this.velocity={x:0,y:0,omega:0};this.axes={LeftX:0,LeftY:0,RightX:0,RightY:0,LeftTriggerAxis:0,RightTriggerAxis:0};this.operator={...this.axes};this.buttons=new Set();this.previousButtons=new Set();this.commands=new Map();this.states=new Map();this.vars=new Map();this.pids=new Map();this.sensors=new Map();this.cameras=new Map();this.nt=new Map();this.methodCache=new Map();this.errors=new Set();this.autoIndex=0;this.autoStarted=false;this.mode='disabled';
    for(const ss of this.project.subsystems)for(const c of ss.components){this.states.set(ss.name+'.'+c.name,{duty:0,rps:0,turns:0,value:c.initialAngle||0,active:false});if(['limitswitch','digital','analog','ultrasonic','canrange'].includes(c.type))this.sensors.set(ss.name+'.'+c.name,['limitswitch','digital'].includes(c.type)?false:0);}
    for(const c of this.project.integrations.cameras)this.cameras.set(c.id,{target:false,yaw:0,pitch:0,area:0,distance:2,id:1,latency:25,poseX:0,poseY:0,poseYaw:0});
  }
  stop(owner){for(const [key,s]of this.states)if(key.startsWith(owner+'.')){s.duty=0;s.targetRps=undefined;s.targetPosition=undefined;const type=this.component(key)?.type;if(['solenoid','doublesolenoid','relay'].includes(type)){s.active=false;s.value='OFF';}}for(const key of this.pids.keys())if(key.startsWith(owner+'.'))this.pids.delete(key);if(FRC.driveOwner(this.project)?.name===owner)this.velocity={x:0,y:0,omega:0};}
  stopAll(){for(const ss of this.project.subsystems)this.stop(ss.name);this.commands.clear();}
  changeMode(mode){this.stopAll();this.mode=mode;this.previousButtons=new Set(this.buttons);this.autoIndex=0;this.autoStarted=false;}
  command(path,duration=Infinity,kind='manual',cleanup=''){const owner=path.split('.')[0],old=this.commands.get(owner);if(old?.path===path&&old.kind===kind)return;if(old)this.stop(owner);this.commands.set(owner,{path,end:this.time+duration,kind,cleanup});}
  component(path){const [owner,name]=path.split('.');return this.project.subsystems.find(s=>s.name===owner)?.components.find(c=>c.name===name);}
  state(path){const s=this.states.get(path);if(!s)throw Error('Bileşen bulunamadı: '+path);return s;}
  compileMethod(path){if(this.methodCache.has(path))return this.methodCache.get(path);const [owner,name]=path.split('.'),ss=this.project.subsystems.find(s=>s.name===owner),m=ss?.methods.find(m=>m.name===name);if(!m)throw Error('Metot bulunamadı: '+path);const ws=new Blockly.Workspace();ws.frcProject=this.project;ws.frcOwner=owner;if(m.blocks){const xml=Blockly.utils.xml.textToDom(m.blocks);ws.frcExtras=FRC.xmlExtras(xml);Blockly.Xml.domToWorkspace(xml,ws);}const first=ws.getTopBlocks(false).find(b=>b.type==='frc_method_start')?.getNextBlock()||null;this.methodCache.set(path,{ws,first});return this.methodCache.get(path);}
  value(b,owner,depth=0){if(!b||depth>100)throw Error('Önizlemede değer bloğu eksik / çok derin.');const f=k=>b.getFieldValue(k),v=k=>this.value(b.getInputTargetBlock(k),owner,depth+1),s=k=>this.state(f(k));switch(b.type){
    case 'math_number':return Number(f('NUM'));case 'logic_boolean':return f('BOOL')==='TRUE';case 'frc_getvar':return this.vars.get(owner+'.'+f('VAR'))||0;
    case 'frc_time':return this.time;case 'frc_trig':return Math[{SIN:'sin',COS:'cos',TAN:'tan'}[f('OP')]](v('ANGLE'));
    case 'frc_controller_axis':return this.axes[f('AXIS')]||0;case 'frc_operator_axis':return this.operator[f('AXIS')]||0;case 'frc_gyro_angle':return this.pose.angle*180/Math.PI;
    case 'frc_enc_pos':case 'frc_enc_vel':{const path=f('ENCODER'),c=this.component(path),state=c.parentMotor?this.state(path.split('.')[0]+'.'+c.parentMotor):this.state(path),sign=c.inverted?-1:1;return (b.type==='frc_enc_pos'?state.turns:state.rps*60)*sign;}
    case 'frc_limitswitch':return !!this.sensors.get(f('SWITCH'));case 'frc_device_active':return this.sensors.has(f('DEVICE'))?!!this.sensors.get(f('DEVICE')):s('DEVICE').active;case 'frc_device_value':return this.sensors.has(f('DEVICE'))?Number(this.sensors.get(f('DEVICE'))):s('DEVICE').value;
    case 'frc_vision_target':return !!this.cameras.get(f('CAMERA'))?.target;case 'frc_vision_value':{const c=this.cameras.get(f('CAMERA'));return c?.target?c[f('METRIC')]||0:0;}
    case 'frc_nt_number':return this.nt.has(f('KEY'))?Number(this.nt.get(f('KEY'))):v('DEFAULT');case 'frc_nt_boolean':return !!this.nt.get(f('KEY'));
    case 'frc_pose_value':return f('METRIC')==='ANGLE'?this.pose.angle*180/Math.PI:this.pose[f('METRIC').toLowerCase()];
    case 'frc_compare':case 'logic_compare':{const a=v('A'),c=v('B');return {GT:()=>a>c,LT:()=>a<c,GTE:()=>a>=c,LTE:()=>a<=c,EQ:()=>a===c,NEQ:()=>a!==c}[f('OP')]();}
    case 'frc_bool':case 'logic_operation':return f('OP')==='AND'?v('A')&&v('B'):v('A')||v('B');case 'frc_not':return !v('V');case 'logic_negate':return !v('BOOL');
    case 'math_arithmetic':{const a=v('A'),c=v('B');return {ADD:()=>a+c,MINUS:()=>a-c,MULTIPLY:()=>a*c,DIVIDE:()=>a/c,POWER:()=>Math.pow(a,c)}[f('OP')]();}
    case 'math_single':{const n=v('NUM');return {ROOT:()=>Math.sqrt(n),ABS:()=>Math.abs(n),NEG:()=>-n,LN:()=>Math.log(n),LOG10:()=>Math.log10(n),EXP:()=>Math.exp(n),POW10:()=>10**n}[f('OP')]();}case 'math_modulo':return v('DIVIDEND')%v('DIVISOR');case 'math_constrain':return Math.max(v('LOW'),Math.min(v('HIGH'),v('VALUE')));
    case 'frc_pid':{const key=owner+'.'+f('NAME'),last=this.pids.get(key)||{error:0,integral:0,first:true},error=v('SETPOINT')-v('MEASUREMENT'),integral=last.integral+error*.02,output=Number(f('KP'))*error+Number(f('KI'))*integral+Number(f('KD'))*(last.first?0:(error-last.error)/.02);this.pids.set(key,{error,integral,first:false});return output;}
    case 'frc_feedforward':{const speed=v('VELOCITY');return Number(f('KS'))*Math.sign(speed)+Number(f('KV'))*speed+(f('KIND')==='ARM'?Number(f('KG'))*Math.cos(v('POSITION')):f('KIND')==='ELEVATOR'?Number(f('KG')):0);}
    default:throw Error('Bu değer için WPILib simülasyonu kullanın: '+b.type);
  }}
  run(path,depth=0){if(depth>30)throw Error('Metot çağrı sınırı.');const owner=path.split('.')[0];this.chain(this.compileMethod(path).first,owner,depth);}
  chain(first,owner,depth=0){let count=0;for(let b=first;b;b=b.getNextBlock()){if(++count>2000)throw Error('Blok sınırı.');if(!b.isEnabled())continue;const f=k=>b.getFieldValue(k),v=k=>this.value(b.getInputTargetBlock(k),owner),s=k=>this.state(f(k));switch(b.type){
    case 'frc_drive_motor':s('MOTOR').targetRps=undefined;s('MOTOR').targetPosition=undefined;s('MOTOR').duty=this.bounded(v('SPEED'));break;
    case 'frc_motor_voltage':s('MOTOR').targetRps=undefined;s('MOTOR').targetPosition=undefined;s('MOTOR').duty=this.bounded(v('VOLTAGE')/12);break;
    case 'frc_stop_motor':s('MOTOR').duty=0;s('MOTOR').targetRps=undefined;s('MOTOR').targetPosition=undefined;break;
    case 'frc_drive_velocity':s('MOTOR').targetRps=v('VEL');s('MOTOR').targetPosition=undefined;break;
    case 'frc_motor_position':s('MOTOR').targetPosition=v('POSITION');break;case 'frc_encoder_zero':s('MOTOR').turns=v('POSITION');break;
    case 'frc_setvar':this.vars.set(owner+'.'+f('VAR'),v('VAL'));break;
    case 'frc_if':case 'frc_if_else':this.chain(b.getInputTargetBlock(v('COND')?'DO':'ELSE'),owner,depth);break;
    case 'frc_call':this.run(f('METHOD'),depth+1);break;case 'frc_schedule':this.command(f('GLOBALMETHOD'),Math.min(15,Math.max(.02,v('DURATION'))),'scheduled');break;
    case 'frc_drive':case 'frc_drive_field':this.drive(v('X'),v('Y'),v('ROT'),b.type==='frc_drive_field');break;
    case 'frc_zero_gyro':this.pose.angle=0;break;case 'frc_pose_reset':this.pose={x:v('X'),y:v('Y'),angle:v('ANGLE')*Math.PI/180};break;
    case 'frc_servo':s('DEVICE').value=Math.max(0,Math.min(180,v('ANGLE')));break;case 'frc_solenoid':s('DEVICE').active=f('STATE')==='FORWARD';s('DEVICE').value=f('STATE');break;case 'frc_compressor':s('DEVICE').active=f('STATE')==='ON';break;case 'frc_relay':s('DEVICE').value=f('STATE');break;case 'frc_led':s('DEVICE').value=['R','G','B'].map(v).map(x=>Math.max(0,Math.min(255,x))).join(',');break;
    case 'frc_nt_write':this.nt.set(f('KEY'),v('VALUE'));break;case 'frc_telemetry':this.nt.set(owner+'/'+f('KEY'),v('VALUE'));break;
    case 'frc_rumble':case 'frc_dashboard_notice':case 'frc_dashboard_tab':case 'frc_vision_pipeline':case 'frc_vision_led':case 'frc_vision_driver':break;
    default:throw Error('Önizleme bu bloğu çalıştırmıyor; WPILib simülasyonunda deneyin: '+b.type);
  }}}
  bounded(x){return Number.isFinite(x)?Math.max(-1,Math.min(1,x)):0;}
  drive(x,y,r,field=false){const p=this.project,swerve=p.drive.type==='swerve',speed=swerve?p.swerveConfig.maxSpeed:p.integrations.motion.maxSpeed,omega=p.integrations.motion.maxAngular;let vx=this.bounded(x)*speed,vy=p.drive.type==='differential'?0:this.bounded(y)*speed;if(!field){const c=Math.cos(this.pose.angle),s=Math.sin(this.pose.angle);[vx,vy]=[vx*c-vy*s,vx*s+vy*c];}this.velocity={x:vx,y:vy,omega:this.bounded(r)*omega};}
  step(){const dt=.02,p=this.project,owner=FRC.driveOwner(p);this.time+=dt;if(this.mode==='disabled'){this.stopAll();this.updateMotors(dt);return;}
    if(this.mode==='teleop')for(const b of p.controllerBindings){const key=(b.controller||'driver')+':'+b.button,down=this.buttons.has(key),was=this.previousButtons.has(key);if(b.type==='zeroGyro'){if(down&&!was)this.pose.angle=0;continue;}const target=b.methodPath?.split('.')[0];if(b.trigger==='whileTrue'){if(down&&!was)this.command(b.methodPath,Infinity,'button',b.stopMethod);if(!down&&was&&this.commands.get(target)?.kind==='button'){if(b.stopMethod)this.tryRun(b.stopMethod);this.stop(target);this.commands.delete(target);}}else{if(b.trigger==='onTrue'&&down&&!was||b.trigger==='onFalse'&&!down&&was){const old=this.commands.get(target);if(old){this.stop(target);this.commands.delete(target);}this.tryRun(b.methodPath);if(b.trigger==='onFalse'&&b.stopMethod)this.tryRun(b.stopMethod);}else if(b.trigger==='onTrue'&&!down&&was&&b.stopMethod)this.tryRun(b.stopMethod);}}
    if(this.mode==='autonomous'&&this.autoIndex<p.autonomous.length){const a=p.autonomous[this.autoIndex];if(!this.autoStarted){this.autoEnd=this.time+a.duration;this.autoStarted=true;for(const path of a.type==='parallel'?a.methods:a.type==='method'?[a.methodPath]:[])this.command(path,a.duration,'auto');}if(this.time>=this.autoEnd){for(const [ss,c]of this.commands)if(c.kind==='auto'){this.stop(ss);this.commands.delete(ss);}this.autoIndex++;this.autoStarted=false;}}
    for(const [ss,c]of this.commands){if(this.time>=c.end){if(c.cleanup)this.tryRun(c.cleanup);this.stop(ss);this.commands.delete(ss);}else this.tryRun(c.path);}
    if(this.mode==='teleop')for(const ss of p.subsystems)if(!this.commands.has(ss.name)){const m=ss.methods.find(m=>m.role==='default');if(m)this.tryRun(ss.name+'.'+m.name);else if(ss.name===owner?.name)this.drive(-this.axes.LeftY*p.drive.scale,-this.axes.LeftX*p.drive.scale,-this.axes.RightX*p.drive.scale,p.drive.fieldRelative&&p.drive.type!=='differential');}
    for(const ss of p.subsystems)for(const m of ss.methods)if(m.role==='periodic')this.tryRun(ss.name+'.'+m.name);
    this.previousButtons=new Set(this.buttons);this.updateMotors(dt);this.pose.x+=this.velocity.x*dt;this.pose.y+=this.velocity.y*dt;this.pose.angle+=this.velocity.omega*dt;
  }
  updateMotors(dt){for(const [path,s]of this.states)if(this.component(path)?.type==='motor'){if(this.mode!=='disabled'&&s.targetPosition!==undefined)s.targetRps=Math.max(-20,Math.min(20,(s.targetPosition-s.turns)*5));if(this.mode!=='disabled'&&s.targetRps!==undefined)s.duty=this.bounded(s.targetRps/100);const target=s.duty*100;s.rps+=(target-s.rps)*Math.min(1,dt/.15);s.turns+=s.rps*dt;}}
  tryRun(path){try{this.run(path);}catch(e){this.errors.add(e.message);this.stop(path.split('.')[0]);}}
  dispose(){for(const {ws}of this.methodCache.values())ws.dispose();this.methodCache.clear();}
}
FRC.StudioPreview=StudioPreview;

