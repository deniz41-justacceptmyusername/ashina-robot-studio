'use strict';
FRC.extraFieldEnums={frc_solenoid:{STATE:['FORWARD','REVERSE','OFF']},frc_compressor:{STATE:['ON','OFF']},frc_relay:{STATE:['ON','OFF','FORWARD','REVERSE']},frc_vision_value:{METRIC:['yaw','pitch','area','id','distance','latency','poseX','poseY','poseYaw']},frc_vision_led:{MODE:['DEFAULT','ON','OFF','BLINK']},frc_vision_driver:{MODE:['ON','OFF']},frc_feedforward:{KIND:['ARM','ELEVATOR','MOTOR']},frc_pose_value:{METRIC:['X','Y','ANGLE']},frc_rumble:{CONTROLLER:['driver','operator']},frc_trig:{OP:['SIN','COS','TAN']}};
FRC.extraMenu=(block,kind)=>{
  const ws=block.workspace,p=ws.frcProject||P,ss=p.subsystems.find(s=>s.name===(ws.frcOwner??P.subsystems[activeSS]?.name));
  let list=kind==='CAMERA'?p.integrations.cameras.filter(c=>c.enabled&&c.kind!=='usb').map(c=>[c.id+' · '+c.kind,c.id]):kind==='GLOBALMETHOD'?p.subsystems.flatMap(s=>s.methods.map(m=>[s.name+'.'+m.name,s.name+'.'+m.name])):(ss?.components||[]).filter(c=>FRC.extendedComponentTypes.includes(c.type)).map(c=>[c.name+' · '+FRC.deviceDefinitions[c.type].label,ss.name+'.'+c.name]);
  for(const value of ws.frcExtras?.[kind]||[])if(!list.some(x=>x[1]===value))list.push(['⚠ '+value,value]);
  return list.length?list:[['Önce uygun bileşen / kamera ekleyin','']];
};
FRC.installExtraBlocks=()=>{
  for(const kind of ['DEVICE','CAMERA','GLOBALMETHOD'])Blockly.Extensions.register('studio_'+kind.toLowerCase(),function(){this.getField(kind).menuGenerator_=()=>FRC.extraMenu(this,kind);});
  const menu=name=>({type:'field_dropdown',name,options:[['Seçin','']]}),num=name=>({type:'input_value',name,check:'Number'}),txt=(name,text)=>({type:'field_input',name,text}),choice=(name,options)=>({type:'field_dropdown',name,options});
  const stm=(type,message0,args0,colour=200,extensions=[])=>({type,message0,args0,previousStatement:null,nextStatement:null,colour,extensions});
  const expr=(type,message0,args0,output='Number',colour=180,extensions=[])=>({type,message0,args0,output,colour,extensions});
  Blockly.defineBlocksWithJsonArray([
    stm('frc_motor_position','%1 hedef konum %2 tur',[menu('MOTOR'),num('POSITION')],230,['frc_motor_menu']),
    stm('frc_motor_voltage','%1 motor gerilimi %2 V',[menu('MOTOR'),num('VOLTAGE')],230,['frc_motor_menu']),
    stm('frc_encoder_zero','%1 motor enkoderini %2 tura sıfırla',[menu('MOTOR'),num('POSITION')],180,['frc_motor_menu']),
    stm('frc_profile_position','%1 ivme sınırlı konum %2 tur',[menu('MOTOR'),num('POSITION')],230,['frc_motor_menu']),
    stm('frc_profile_velocity','%1 ivme sınırlı hız %2 RPS',[menu('MOTOR'),num('VELOCITY')],230,['frc_motor_menu']),
    expr('frc_time','Robot zamanı (s)',[],'Number',330),
    expr('frc_trig','%1 (%2 radyan)',[choice('OP',[['sin','SIN'],['cos','COS'],['tan','TAN']]),num('ANGLE')],'Number',330),
    stm('frc_rumble','%1 kumanda titreşimi %2',[choice('CONTROLLER',[['sürücü','driver'],['operatör','operator']]),num('POWER')],65),
    expr('frc_device_value','%1 sensör / servo değeri',[menu('DEVICE')],'Number',180,['studio_device']),
    expr('frc_device_active','%1 aktif mi?',[menu('DEVICE')],'Boolean',120,['studio_device']),
    stm('frc_servo','%1 servo açısı %2 derece',[menu('DEVICE'),num('ANGLE')],40,['studio_device']),
    stm('frc_solenoid','%1 pnömatik %2',[menu('DEVICE'),choice('STATE',[['ileri / açık','FORWARD'],['geri / kapalı','REVERSE'],['enerjiyi kes','OFF']])],40,['studio_device']),
    stm('frc_compressor','%1 kompresör %2',[menu('DEVICE'),choice('STATE',[['otomatik basınç','ON'],['kapat','OFF']])],40,['studio_device']),
    stm('frc_relay','%1 relay %2',[menu('DEVICE'),choice('STATE',[['kapalı','OFF'],['ileri','FORWARD'],['geri','REVERSE'],['iki yön','ON']])],40,['studio_device']),
    stm('frc_led','%1 LED kırmızı %2 yeşil %3 mavi %4',[menu('DEVICE'),num('R'),num('G'),num('B')],40,['studio_device']),
    expr('frc_vision_target','%1 hedef görüyor mu?',[menu('CAMERA')],'Boolean',290,['studio_camera']),
    expr('frc_vision_value','%1 %2',[menu('CAMERA'),choice('METRIC',[['yaw / tx (derece)','yaw'],['pitch / ty (derece)','pitch'],['alan (%)','area'],['AprilTag ID','id'],['uzaklık (m)','distance'],['gecikme (ms)','latency'],['poz X (m)','poseX'],['poz Y (m)','poseY'],['poz yaw (derece)','poseYaw']])],'Number',290,['studio_camera']),
    stm('frc_vision_pipeline','%1 pipeline %2',[menu('CAMERA'),num('PIPELINE')],290,['studio_camera']),
    stm('frc_vision_led','%1 LED modu %2',[menu('CAMERA'),choice('MODE',[['pipeline','DEFAULT'],['kapat','OFF'],['aç','ON'],['yanıp sön','BLINK']])],290,['studio_camera']),
    stm('frc_vision_driver','%1 sürücü kamera modu %2',[menu('CAMERA'),choice('MODE',[['açık','ON'],['kapalı','OFF']])],290,['studio_camera']),
    expr('frc_nt_number','NT %1 sayı (yoksa %2)',[txt('KEY','/SmartDashboard/target'),num('DEFAULT')],'Number',45),
    expr('frc_nt_boolean','NT %1 doğru mu?',[txt('KEY','/SmartDashboard/ready')],'Boolean',45),
    stm('frc_nt_write','NT %1 = %2',[txt('KEY','/FRCStudio/user/value'),num('VALUE')],45),
    stm('frc_dashboard_notice','Elastic %1 : %2',[txt('TITLE','Robot'),txt('MESSAGE','Hazır')],45),
    stm('frc_dashboard_tab','Elastic sekme %1',[txt('TAB','Teleop')],45),
    expr('frc_operator_axis','Operatör %1',[choice('AXIS',[['sol X','LeftX'],['sol Y','LeftY'],['sağ X','RightX'],['sağ Y','RightY'],['sol tetik','LeftTriggerAxis'],['sağ tetik','RightTriggerAxis']])],'Number',65),
    expr('frc_pid','PID %1 P %2 I %3 D %4 ölçüm %5 hedef %6',[txt('NAME','mekanizma'),{type:'field_number',name:'KP',value:1,min:0,max:10000},{type:'field_number',name:'KI',value:0,min:0,max:10000},{type:'field_number',name:'KD',value:0,min:0,max:10000},num('MEASUREMENT'),num('SETPOINT')],'Number',330),
    expr('frc_feedforward','%1 feedforward kS %2 kG %3 kV %4 hız %5 konum %6',[choice('KIND',[['kol','ARM'],['asansör','ELEVATOR'],['motor','MOTOR']]),{type:'field_number',name:'KS',value:0,min:0},{type:'field_number',name:'KG',value:0,min:0},{type:'field_number',name:'KV',value:0,min:0},num('VELOCITY'),num('POSITION')],'Number',330),
    stm('frc_drive_field','Saha göreli sürüş ileri %1 sol %2 dönüş %3',[num('X'),num('Y'),num('ROT')],65),
    stm('frc_pose_reset','Poz sıfırla X %1 m Y %2 m açı %3 derece',[num('X'),num('Y'),num('ANGLE')],65),
    expr('frc_pose_value','Robot pozu %1',[choice('METRIC',[['X (m)','X'],['Y (m)','Y'],['açı (derece)','ANGLE']])],'Number',65),
    stm('frc_schedule','%1 metodunu %2 saniye başlat',[menu('GLOBALMETHOD'),num('DURATION')],120,['studio_globalmethod']),
    stm('frc_java','Özel Java %1',[txt('CODE','// Java komutu')],0)
  ]);
};
FRC.extraToolbox=()=>[
  ['Konum / PID','#c084fc',['frc_motor_position','frc_motor_voltage','frc_profile_position','frc_profile_velocity','frc_encoder_zero','frc_pid','frc_feedforward','frc_time','frc_trig']],
  ['Donanım','#fbbf24',['frc_device_value','frc_device_active','frc_servo','frc_solenoid','frc_compressor','frc_relay','frc_led']],
  ['Vision','#a78bfa',['frc_vision_target','frc_vision_value','frc_vision_pipeline','frc_vision_led','frc_vision_driver']],
  ['NT / Elastic','#5eead4',['frc_nt_number','frc_nt_boolean','frc_nt_write','frc_dashboard_notice','frc_dashboard_tab']],
  ['İleri kontrol','#84cc16',['frc_operator_axis','frc_rumble','frc_drive_field','frc_pose_reset','frc_pose_value','frc_schedule','frc_java']]
];
FRC.extensionContext=(b,ctx)=>{
  const {p,ss,fail}=ctx;
  const device=allowed=>{const path=b.getFieldValue('DEVICE'),c=ss.components.find(c=>ss.name+'.'+c.name===path);if(!c||!allowed.includes(c.type))fail(b,'Bu blok için '+allowed.join(' / ')+' bileşeni seçin.');return c;};
  const camera=()=>{const id=b.getFieldValue('CAMERA'),c=p.integrations.cameras.find(c=>c.id===id&&c.enabled&&c.kind!=='usb');if(!c)fail(b,'Etkin, geçerli bir vision kamerası seçin.');return c;};
  const drive=()=>{if(FRC.driveOwner(p)?.name!==ss.name)fail(b,'Bu blok sürüş alt sisteminin metodunda kullanılabilir.');};
  return {device,camera,drive};
};
FRC.extensionExpression=(b,ctx)=>{
  const {p,ss,m,component,input,fail,result}=ctx,{device,camera,drive}=FRC.extensionContext(b,ctx),q=FRC.s,j=FRC.javaNumber;let code,type='Number';
  switch(b.type){
    case 'frc_time':code='Timer.getFPGATimestamp()';break;
    case 'frc_trig':{const op={SIN:'sin',COS:'cos',TAN:'tan'}[b.getFieldValue('OP')];if(!op)fail(b,'Geçersiz trigonometrik işlem.');code='Math.'+op+'('+input(b,'ANGLE','Number')+')';break;}
    case 'frc_device_value':{const c=device(['analog','ultrasonic','canrange','servo']);code='frc_read_'+c.name+'()';break;}
    case 'frc_device_active':{const c=device(['digital','compressor']);code='frc_active_'+c.name+'()';type='Boolean';break;}
    case 'frc_vision_target':code='RobotServices.vision().hasTarget('+q(camera().id)+')';type='Boolean';break;
    case 'frc_vision_value':{const metric=b.getFieldValue('METRIC');if(!['yaw','pitch','area','id','distance','latency','poseX','poseY','poseYaw'].includes(metric))fail(b,'Geçersiz vision değeri.');code='RobotServices.vision().value('+q(camera().id)+', '+q(metric)+')';break;}
    case 'frc_nt_number':code='RobotServices.number('+q(b.getFieldValue('KEY'))+', '+input(b,'DEFAULT','Number')+')';break;
    case 'frc_nt_boolean':code='RobotServices.flag('+q(b.getFieldValue('KEY'))+')';type='Boolean';break;
    case 'frc_operator_axis':if(!p.integrations.operator.enabled)fail(b,'Proje ayarlarında operatör kumandasını açın.');code='RobotIO.operator.get'+b.getFieldValue('AXIS')+'()';break;
    case 'frc_pid':{const name=b.getFieldValue('NAME');if(!FRC.identifier(name))fail(b,'PID adı Java kimliği olmalı.');const gains=['KP','KI','KD'].map(k=>Number(b.getFieldValue(k)));if(gains.some(g=>!Number.isFinite(g)||g<0))fail(b,'PID katsayıları geçersiz.');result.controllers??=new Map();if(result.controllers.has(name)&&JSON.stringify(result.controllers.get(name))!==JSON.stringify(gains))fail(b,'Aynı PID adı farklı katsayılarla kullanılamaz.');result.controllers.set(name,gains);code='frc_pid_'+name+'.calculate('+input(b,'MEASUREMENT','Number')+', '+input(b,'SETPOINT','Number')+')';break;}
    case 'frc_feedforward':{const ks=Number(b.getFieldValue('KS')),kg=Number(b.getFieldValue('KG')),kv=Number(b.getFieldValue('KV')),v=input(b,'VELOCITY','Number'),position=input(b,'POSITION','Number');if([ks,kg,kv].some(n=>!Number.isFinite(n)||n<0))fail(b,'Feedforward katsayıları geçersiz.');code='('+j(ks)+' * Math.signum('+v+') + '+j(kv)+' * '+v+(b.getFieldValue('KIND')==='ARM'?' + '+j(kg)+' * Math.cos('+position+')':b.getFieldValue('KIND')==='ELEVATOR'?' + '+j(kg):'')+')';break;}
    case 'frc_pose_value':if(!FRC.hasOdometry(p))fail(b,'Robot pozu için odometri yapılandırın.');code='RobotServices.drive().getPose().'+({X:'getX()',Y:'getY()',ANGLE:'getRotation().getDegrees()'}[b.getFieldValue('METRIC')]);break;
    default:return null;
  }
  return {code,type};
};
FRC.extensionStatement=(b,ctx)=>{
  const {p,ss,component,input,fail,result}=ctx,{device,camera,drive}=FRC.extensionContext(b,ctx),q=FRC.s;
  const acts=()=>{result.actuates=true;};
  switch(b.type){
    case 'frc_profile_position':case 'frc_profile_velocity':{acts();const c=component(b,'MOTOR','motor');if(!['fx','max','flex'].includes(FRC.motorFamily(c.motorType))||!FRC.isBrushless(c))fail(b,'Motion Magic / MAXMotion için dahili sensörlü brushless CAN motoru seçin.');if(!c.closedLoopConfigured||!c.motion?.configured)fail(b,'Motor PID ve hareket profili ayarlarını yapılandırıp onaylayın.');return 'frc_profile'+(b.type==='frc_profile_position'?'Position_':'Velocity_')+c.name+'('+input(b,b.type==='frc_profile_position'?'POSITION':'VELOCITY','Number')+');';}
    case 'frc_rumble':{const controller=b.getFieldValue('CONTROLLER');if(!['driver','operator'].includes(controller)||controller==='operator'&&!p.integrations.operator.enabled)fail(b,'Etkin kumanda seçin.');(result.rumble??=new Set()).add(controller);return 'RobotIO.'+controller+'.setRumble(edu.wpi.first.wpilibj.GenericHID.RumbleType.kBothRumble,Math.max(0.0,RobotIO.percent('+input(b,'POWER','Number')+')));';}
    case 'frc_motor_voltage':{acts();const c=component(b,'MOTOR','motor');return 'frc_voltage_'+c.name+'('+input(b,'VOLTAGE','Number')+');';}
    case 'frc_motor_position':case 'frc_encoder_zero':{acts();const c=component(b,'MOTOR','motor');if(!['fx','max','flex'].includes(FRC.motorFamily(c.motorType))||!FRC.isBrushless(c))fail(b,'Dahili sensörlü CAN motoru seçin.');if(b.type==='frc_motor_position'&&!c.closedLoopConfigured)fail(b,'Motor PID ayarlarını onaylayın.');return 'frc_'+(b.type==='frc_motor_position'?'position_':'zero_')+c.name+'('+input(b,'POSITION','Number')+');';}
    case 'frc_servo':{acts();const c=device(['servo']);return c.name+'.setAngle(Math.max(0.0, RobotIO.bounded('+input(b,'ANGLE','Number')+',180.0)));';}
    case 'frc_solenoid':{acts();const c=device(['solenoid','doublesolenoid']),state=b.getFieldValue('STATE');if(!['FORWARD','REVERSE','OFF'].includes(state))fail(b,'Solenoid durumu geçersiz.');return c.name+'.set('+(c.type==='solenoid'?(state==='FORWARD'?'true':'false'):'edu.wpi.first.wpilibj.DoubleSolenoid.Value.'+({FORWARD:'kForward',REVERSE:'kReverse',OFF:'kOff'}[state]))+');';}
    case 'frc_compressor':{const c=device(['compressor']);return c.name+'.'+(b.getFieldValue('STATE')==='ON'?'enableDigital()':'disable()')+';';}
    case 'frc_relay':{acts();const c=device(['relay']);return c.name+'.set(edu.wpi.first.wpilibj.Relay.Value.'+({ON:'kOn',OFF:'kOff',FORWARD:'kForward',REVERSE:'kReverse'}[b.getFieldValue('STATE')])+');';}
    case 'frc_led':{const c=device(['led']);return 'frc_led_'+c.name+'('+['R','G','B'].map(k=>input(b,k,'Number')).join(', ')+');';}
    case 'frc_vision_pipeline':return 'RobotServices.vision().pipeline('+q(camera().id)+', (int)('+input(b,'PIPELINE','Number')+'));';
    case 'frc_vision_led':return 'RobotServices.vision().led('+q(camera().id)+', '+q(b.getFieldValue('MODE'))+');';
    case 'frc_vision_driver':return 'RobotServices.vision().driverMode('+q(camera().id)+', '+(b.getFieldValue('MODE')==='ON')+');';
    case 'frc_nt_write':return 'RobotServices.publish('+q(b.getFieldValue('KEY'))+', '+input(b,'VALUE','Number')+');';
    case 'frc_dashboard_notice':return 'RobotServices.notice('+q(b.getFieldValue('TITLE'))+', '+q(b.getFieldValue('MESSAGE'))+');';
    case 'frc_dashboard_tab':return 'RobotServices.tab('+q(b.getFieldValue('TAB'))+');';
    case 'frc_drive_field':acts();drive();if(p.drive.type==='differential')fail(b,'Saha göreli doğrudan sürüş swerve / mecanum içindir. Tank için normal sürüş veya otonom yol kullanın.');if(p.swerveConfig.gyro.type==='none')fail(b,'Saha göreli sürüş için gyro gerekir.');return 'drive('+['X','Y','ROT'].map(k=>input(b,k,'Number')).join(', ')+', true);';
    case 'frc_pose_reset':acts();drive();if(!FRC.hasOdometry(p))fail(b,'Poz sıfırlama için odometri yapılandırın.');return 'resetPose(new edu.wpi.first.math.geometry.Pose2d('+input(b,'X','Number')+', '+input(b,'Y','Number')+', edu.wpi.first.math.geometry.Rotation2d.fromDegrees('+input(b,'ANGLE','Number')+')));';
    case 'frc_schedule':{result.actuates=true;const path=b.getFieldValue('GLOBALMETHOD'),target=p.subsystems.find(s=>s.methods.some(m=>s.name+'.'+m.name===path));if(!target)fail(b,'Geçerli bir metot seçin.');if(target.name===ss.name)fail(b,'Aynı alt sistem için normal metot çağırma bloğunu kullanın.');return 'RobotServices.startMethod('+q(path)+', '+input(b,'DURATION','Number')+');';}
    case 'frc_java':{acts();const code=b.getFieldValue('CODE');if(code.length>10000)fail(b,'Java bloğu 10.000 karakter sınırını aşıyor.');result.warnings.push(ss.name+': özel Java, WPILib Build sırasında ayrıca derlenir.');return code;}
    default:return null;
  }
};
