'use strict';
FRC.integrationDefaults=()=>({
  cameras:[],field:'k2026RebuiltWelded',vision:{maxAge:0.5,maxAmbiguity:0.2,maxDistance:6,maxZ:0.4,maxJump:3,stdXY:0.7,stdHeading:999999,maxYawRate:540},
  pathplanner:{enabled:false,translationP:5,rotationP:4,massKg:50,moi:6,friction:1.1,motor:'krakenx60',motorsPerModule:1,events:[]},
  choreo:{enabled:false,translationP:5,rotationP:4},
  telemetry:{enabled:true,dataLog:true,elastic:true,robotModel:'',publishPeriod:0.05},
  sysid:{enabled:false,subsystem:'',motor:'',ramp:1,step:4,timeout:5},
  operator:{enabled:false,port:1},
  motion:{wheelDiameterM:0.1016,gearRatio:6.75,trackWidthM:0.6,wheelBaseM:0.6,maxSpeed:4.5,maxAngular:8,encoders:{},kP:1,kI:0,kD:0,kS:0.2,kV:2.3},
  assets:[],autonomousMode:'blocks',selectedAuto:'',customSources:[],customVendors:[]
});
FRC.cameraDefaults=()=>({id:'frontCamera',kind:'limelight',name:'limelight',enabled:true,pipeline:0,driverMode:false,poseFusion:false,megaTag2:true,x:0.25,y:0,z:0.5,roll:0,pitch:0,yaw:0});
FRC.deviceDefinitions={
  servo:{label:'Servo (PWM)',fields:[['pwmChannel','PWM kanalı','integer',0],['initialAngle','Başlangıç açısı (derece)','number',90]]},
  solenoid:{label:'Tek solenoid',fields:[['moduleType','Modül','pneumatics','REVPH'],['moduleId','Modül CAN ID','integer',1],['channel','Kanal','integer',0]]},
  doublesolenoid:{label:'Çift solenoid',fields:[['moduleType','Modül','pneumatics','REVPH'],['moduleId','Modül CAN ID','integer',1],['forwardChannel','İleri kanal','integer',0],['reverseChannel','Geri kanal','integer',1]]},
  compressor:{label:'Kompresör',fields:[['moduleType','Modül','pneumatics','REVPH'],['moduleId','Modül CAN ID','integer',1],['enabled','Basınç anahtarıyla otomatik çalıştır','boolean',true]]},
  digital:{label:'Dijital sensör / beam break',fields:[['dioChannel','DIO kanalı','integer',0],['activeLow','Düşük sinyalde aktif','boolean',true]]},
  analog:{label:'Analog sensör',fields:[['analogChannel','Analog kanal','integer',0],['scale','Volt başına ölçüm katsayısı','number',1],['offset','Ölçüm ofseti','number',0]]},
  ultrasonic:{label:'Ultrasonik (ping / echo)',fields:[['pingChannel','Ping DIO','integer',0],['echoChannel','Echo DIO','integer',1]]},
  canrange:{label:'CTRE CANrange',fields:[['canId','CAN ID','integer',30],['canbus','CAN bus','text','rio']]},
  relay:{label:'Relay',fields:[['relayChannel','Relay kanalı','integer',0]]},
  led:{label:'Addressable LED',fields:[['pwmChannel','PWM kanalı','integer',0],['length','LED sayısı','integer',60]]}
};
FRC.extendedComponentTypes=Object.keys(FRC.deviceDefinitions);
FRC.physicsDefaults=()=>({enabled:true,model:'flywheel',motorModel:'auto',gearing:1,inertia:0.002,mass:5,radius:0.025,length:0.5,min:0,max:1.5,initial:0});
FRC.motionDefaults=()=>({configured:false,cruise:20,acceleration:40,jerk:0,tolerance:0.05});
for(const n of ['DriveIO','RobotServices','RobotVision','VisionCamera','MotorPhysics','Elastic','resetPose','getRobotSpeeds','driveRobotRelative','addVisionMeasurement','Encoder'])FRC.reserved.add(n);
FRC.extraComponentDefaults=type=>({type,name:'',...Object.fromEntries((FRC.deviceDefinitions[type]?.fields||[]).map(([k,,type,v])=>[k,v]))});
FRC.driveOwner=p=>{if(p.drive.type==='none')return null;if(p.drive.type==='swerve')return p.subsystems.find(s=>s.name===p.drive.subsystem);const slots=p.drive.type==='differential'?['left','right']:FRC.moduleNames,paths=slots.flatMap(slot=>p.drive.motors[slot]||[]);return p.subsystems.find(s=>paths.some(path=>path.startsWith(s.name+'.')));};
FRC.hasOdometry=p=>p.drive.type==='swerve'||['differential','mecanum'].includes(p.drive.type)&&(p.drive.type==='differential'?['left','right']:FRC.moduleNames).every(slot=>!!p.integrations.motion.encoders[slot]);
FRC.assetPath=path=>typeof path==='string'&&/^(pathplanner\/(autos\/[A-Za-z0-9 _.-]+\.auto|paths\/[A-Za-z0-9 _.-]+\.path|settings\.json)|choreo\/[A-Za-z0-9 _.-]+\.traj)$/.test(path)&&!path.includes('..')&&!path.includes('\\');
FRC.validCustomVendor=v=>v&&typeof v==='object'&&v.frcYear==='2026'&&/^[A-Za-z0-9_.-]+\.json$/.test(v.fileName)&&!v.fileName.includes('..')&&/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i.test(v.uuid)&&typeof v.name==='string'&&Array.isArray(v.mavenUrls)&&v.mavenUrls.every(u=>typeof u==='string'&&u.startsWith('https://'))&&Array.isArray(v.javaDependencies)&&v.javaDependencies.every(d=>d&&['groupId','artifactId','version'].every(k=>typeof d[k]==='string'&&d[k].length));
FRC.assetNames=(p,kind)=>p.integrations.assets.filter(a=>a.path.startsWith(kind==='pathplanner'?'pathplanner/autos/':'choreo/')&&a.path.endsWith(kind==='pathplanner'?'.auto':'.traj')).map(a=>a.path.split('/').at(-1).replace(/\.(auto|traj)$/,''));
FRC.extraNormalize=(p,raw,notes)=>{
  if(!p.integrations||typeof p.integrations!=='object'||Array.isArray(p.integrations))throw Error('Entegrasyon ayarları geçersiz.');
  const i=p.integrations;
  for(const key of ['cameras','assets','customSources','customVendors'])if(!Array.isArray(i[key]))throw Error(key+' liste olmalı.');
  for(const key of ['vision','pathplanner','choreo','telemetry','operator','motion','sysid'])if(!i[key]||typeof i[key]!=='object'||Array.isArray(i[key]))throw Error(key+' ayarları geçersiz.');
  if(!Array.isArray(i.pathplanner.events)||!i.motion.encoders||typeof i.motion.encoders!=='object')throw Error('Event / enkoder ayarları geçersiz.');
  if(i.cameras.length>16||i.assets.length>300||i.customSources.length>100||i.customVendors.length>50)throw Error('Entegrasyon dosya / kamera sınırı aşıldı.');
  for(const c of i.cameras)if(!c||typeof c!=='object')throw Error('Kamera kaydı geçersiz.');
  for(const ss of p.subsystems)for(const c of ss.components)if(FRC.extendedComponentTypes.includes(c.type))Object.assign(c,{...FRC.extraComponentDefaults(c.type),...c});
  for(const ss of p.subsystems)for(const c of ss.components){if(c.type==='motor'){c.simulation={...FRC.physicsDefaults(),...c.simulation};c.motion={...FRC.motionDefaults(),...c.motion};}if(c.type==='encoder'&&c.encType==='quadrature')Object.assign(c,{channelA:0,channelB:1,countsPerRev:2048,...c});}
  if((raw.schemaVersion||1)<3)notes.push('Proje Studio 3 biçimine taşındı; eski bloklar ve donanım ayarları korundu.');
};
FRC.validateIntegrations=(p,ctx)=>{
  const {err,warn,resource,port,can,finite,integer,boolean,paths}=ctx,i=p.integrations;
  if(!i){err('Entegrasyon','Ayar yapısı eksik.');return;}
  boolean(i.operator,['enabled'],'Operatör');
  if(!integer(i.operator.port,0,5))err('Operatör','USB portu 0–5 olmalı.');
  if(i.operator.enabled&&i.operator.port===p.controllerPort)err('Operatör','Sürücü ve operatör aynı portu kullanamaz.');
  const cameraIds=new Set(),cameraNames=new Set();
  for(const c of i.cameras){
    const label='Kamera '+c.id;
    if(!FRC.identifier(c.id)||cameraIds.has(c.id))err(label,'Benzersiz, geçerli kamera kimliği girin.');cameraIds.add(c.id);
    if(!['limelight','photonvision','usb'].includes(c.kind))err(label,'Kamera türü desteklenmiyor.');
    if(typeof c.name!=='string'||!c.name.trim()||c.name.length>120||cameraNames.has(c.kind+':'+c.name))err(label,'Benzersiz kamera / NetworkTables adı girin.');cameraNames.add(c.kind+':'+c.name);
    boolean(c,['enabled','poseFusion','driverMode','megaTag2'],label);
    if(!integer(c.pipeline,0,255))err(label,'Pipeline 0–255 olmalı.');
    for(const k of ['x','y','z'])if(!finite(c[k],-10,10))err(label,k+' metre olarak geçersiz.');
    for(const k of ['roll','pitch','yaw'])if(!finite(c[k],-360,360))err(label,k+' derece olarak geçersiz.');
    if(c.enabled&&c.poseFusion&&!FRC.hasOdometry(p))err(label,'Poz birleştirmesi için gyro ve sürüş odometrisi gerekir.');
    if(c.kind==='usb'&&(!integer(Number(c.name),0,9)||c.poseFusion))err(label,'USB kamera adı 0–9 cihaz numarası olmalı; poz birleştirme kullanılamaz.');
  }
  for(const k of ['maxAge','maxDistance','maxZ','maxJump','stdXY','stdHeading','maxYawRate'])if(!finite(i.vision[k],0.001,k==='stdHeading'?1e9:10000))err('Vision',k+' pozitif olmalı.');
  if(!finite(i.vision.maxAmbiguity,0,1))err('Vision','Ambiguity sınırı 0–1 olmalı.');
  if(!['k2026RebuiltWelded','k2026RebuiltAndyMark','k2025ReefscapeWelded','k2025ReefscapeAndyMark'].includes(i.field))err('Vision','Saha AprilTag düzeni geçersiz.');
  for(const [key,c] of Object.entries({PathPlanner:i.pathplanner,Choreo:i.choreo})){
    boolean(c,['enabled'],key);
    if(c.enabled&&!FRC.hasOdometry(p))err(key,'Yol takibi için tam sürüş odometrisi gerekir.');
    if(c.enabled&&p.drive.type==='none')err(key,'Sürüş sistemi yok.');
    if(c.enabled&&key==='Choreo'&&p.drive.type==='mecanum')err(key,'Choreo swerve/differential örneklerini destekler; mecanum için PathPlanner kullanın.');
    for(const g of ['translationP','rotationP'])if(!finite(c[g],0,1e4))err(key,g+' geçersiz.');
  }
  for(const key of ['massKg','moi','friction'])if(!finite(i.pathplanner[key],0.01,2000))err('PathPlanner',key+' pozitif olmalı.');
  if(!['krakenx60','falcon500','neo','vortex'].includes(i.pathplanner.motor)||!integer(i.pathplanner.motorsPerModule,1,4))err('PathPlanner','Motor modeli / sayısı geçersiz.');
  if(!finite(i.telemetry.publishPeriod,0.02,1))err('Telemetri','Yayın periyodu 0.02–1 saniye olmalı.');
  boolean(i.telemetry,['enabled','dataLog','elastic'],'Telemetri');
  if(!['blocks','pathplanner','choreo'].includes(i.autonomousMode))err('Otonom','Otonom kaynak türü geçersiz.');
  if(i.autonomousMode!=='blocks'&&(!i[i.autonomousMode].enabled||!FRC.assetNames(p,i.autonomousMode).includes(i.selectedAuto)))err('Otonom','Etkin entegrasyondan içe aktarılmış bir otonom seçin.');
  const assetPaths=new Set();
  for(const a of i.assets){
    if(!a||!FRC.assetPath(a.path)||typeof a.text!=='string'||a.text.length>5e6){err('Dosya','Geçersiz deploy dosyası.');continue;}
    if(assetPaths.has(a.path))err(a.path,'Aynı deploy dosyası iki kez eklenmiş.');assetPaths.add(a.path);
    let json;try{json=JSON.parse(a.text);}catch{err(a.path,'Geçersiz JSON.');continue;}
    if(!json||typeof json!=='object'||Array.isArray(json)){err(a.path,'JSON nesnesi gerekli.');continue;}
    if(a.path.endsWith('.path')&&(!Array.isArray(json.waypoints)||json.waypoints.length<2))err(a.path,'PathPlanner yolu en az iki waypoint içermeli.');
    if(a.path.endsWith('.auto')&&(!json.command||typeof json.command!=='object'))err(a.path,'PathPlanner auto command ağacı eksik.');
    if(a.path.endsWith('.traj')&&(!json.trajectory||!Array.isArray(json.trajectory.samples)||!json.trajectory.samples.length))err(a.path,'Choreo trajectory.samples eksik.');
    if(a.path.endsWith('.traj')&&json.trajectory?.sampleType!==(p.drive.type==='differential'?'Differential':'Swerve'))err(a.path,'Choreo sampleType seçilen sürüş sistemiyle eşleşmiyor.');
    if(a.path.endsWith('.traj')){
      if(json.version!==3)err(a.path,'Choreo 2026 için sürüm 3 .traj çıktısı gerekli.');
      let previous=-1;const keys=p.drive.type==='differential'?['t','x','y','heading','vl','vr','omega','al','ar','alpha','fl','fr']:['t','x','y','heading','vx','vy','omega','ax','ay','alpha'];
      for(const s of json.trajectory?.samples||[]){if(!s||keys.some(k=>!finite(s[k],-1e7,1e7))||s.t<0||s.t<=previous){err(a.path,'Trajectory örnekleri sonlu SI değerleri ve artan zaman içermeli.');break;}if(p.drive.type!=='differential'&&['fx','fy'].some(k=>!Array.isArray(s[k])||s[k].length!==4||s[k].some(v=>!finite(v,-1e7,1e7)))){err(a.path,'Swerve örneğinde dört teker kuvveti (fx/fy) gerekli.');break;}previous=s.t;}
    }
    if(a.path.endsWith('.path')||a.path.endsWith('.auto'))if(typeof json.version!=='string'||json.version.split('.')[0]!=='2025')err(a.path,'PathPlanner 2026 kütüphanesi için 2025.x dosya şeması gerekli.');
    if(a.path.endsWith('.path')){
      for(const w of json.waypoints||[])for(const k of ['anchor','prevControl','nextControl'])if((k==='anchor'||w[k]!=null)&&(!w[k]||!finite(w[k].x,-1000,1000)||!finite(w[k].y,-1000,1000)))err(a.path,'Waypoint / kontrol noktası geçersiz.');
      for(const k of ['maxVelocity','maxAcceleration','maxAngularVelocity','maxAngularAcceleration','nominalVoltage'])if(!finite(json.globalConstraints?.[k],.001,10000))err(a.path,'globalConstraints.'+k+' pozitif olmalı.');
      for(const k of ['goalEndState','idealStartingState'])if(!(k==='idealStartingState'&&json[k]==null)&&(!json[k]||!finite(json[k].velocity,0,1000)||!finite(json[k].rotation,-1e7,1e7)))err(a.path,k+' hız / yön değerleri eksik.');
    }
    if(a.path==='pathplanner/settings.json'){
      for(const k of ['robotMass','robotMOI','driveWheelRadius','driveGearing','maxDriveSpeed','wheelCOF','driveCurrentLimit'])if(!finite(json[k],.00001,1e6))err(a.path,k+' pozitif olmalı.');
      if(json.holonomicMode!==(p.drive.type!=='differential'))err(a.path,'holonomicMode sürüş türüyle eşleşmiyor.');
      if(!['krakenX60','krakenX60FOC','falcon500','falcon500FOC','vortex','NEO','CIM','miniCIM'].includes(json.driveMotorType))err(a.path,'driveMotorType desteklenmiyor.');
      if(json.holonomicMode){for(const k of ['fl','fr','bl','br'])for(const axis of ['X','Y'])if(!finite(json[k+'Module'+axis],-10,10))err(a.path,k+' modül konumu geçersiz.');}else if(!finite(json.robotTrackwidth,.01,10))err(a.path,'robotTrackwidth geçersiz.');
    }
  }
  const eventNames=new Set();
  for(const e of i.pathplanner.events){
    if(!e||typeof e.name!=='string'||!e.name.trim()||eventNames.has(e.name))err('PathPlanner event','Benzersiz event adı gerekli.');else eventNames.add(e.name);
    if(paths.get(e?.methodPath)?.type!=='method')err('PathPlanner event','Metot bulunamadı.');
    if(!finite(e?.duration,0.02,15))err('PathPlanner event','Süre 0.02–15 saniye olmalı.');
    if(e?.methodPath?.startsWith(FRC.driveOwner(p)?.name+'.'))err('PathPlanner event','Event sürüş alt sistemini gerektirirse yol komutunu keser; mekanizma metodu seçin.');
  }
  function inspectCommand(node,label,seen=new Set()){
    if(!node||typeof node!=='object'||seen.has(node))return;seen.add(node);
    if(!['wait','named','path','sequential','parallel','race','deadline'].includes(node.type)||!node.data||typeof node.data!=='object'){err(label,'Geçersiz PathPlanner komut ağacı.');return;}
    if(node.type==='wait'&&!finite(node.data.waitTime,0,1e5))err(label,'Bekleme süresi geçersiz.');
    if(['sequential','parallel','race','deadline'].includes(node.type)&&!Array.isArray(node.data.commands))err(label,'Komut grubu commands listesi içermeli.');
    if(node.type==='named'&&!eventNames.has(node.data?.name))err(label,'Named command tanımlı değil: '+node.data?.name);
    if(node.type==='path'&&!assetPaths.has('pathplanner/paths/'+node.data?.pathName+'.path'))err(label,'Yol dosyası eksik: '+node.data?.pathName);
    if(node.data?.commands)for(const c of node.data.commands)inspectCommand(c,label,seen);
    if(node.command)inspectCommand(node.command,label,seen);
  }
  for(const a of i.assets)try{const j=JSON.parse(a.text);if(a.path.endsWith('.auto')){if(j.choreoAuto)err(a.path,'Choreo dosyalarını doğrudan Choreo kaynağından seçin; PathPlanner choreoAuto bu sürümde desteklenmiyor.');inspectCommand(j.command,a.path);}if(a.path.endsWith('.path'))for(const e of j.eventMarkers||[])if(e.command)inspectCommand(e.command,a.path);if(a.path.endsWith('.traj'))for(const e of j.events||[])if(e.name&&!eventNames.has(e.name))err(a.path,'Choreo olayını bir metoda bağlayın: '+e.name);}catch{}
  if(['differential','mecanum'].includes(p.drive.type)){
    const slots=p.drive.type==='differential'?['left','right']:FRC.moduleNames;
    for(const k of ['wheelDiameterM','gearRatio','trackWidthM','wheelBaseM','maxSpeed','maxAngular'])if(!finite(i.motion[k],0.001,1000))err('Sürüş fiziği',k+' pozitif olmalı.');
    for(const k of ['kP','kI','kD','kS','kV'])if(!finite(i.motion[k],0,10000))err('Sürüş kontrolü',k+' geçersiz.');
    const any=slots.some(s=>i.motion.encoders[s]);
    if(any){
      if(p.swerveConfig.gyro.type==='none')err('Odometri','Gyro seçin.');
      const sensors=new Set();for(const slot of slots){const path=i.motion.encoders[slot],c=paths.get(path);if(!c||c.type!=='encoder'||!['builtin','quadrature'].includes(c.encType)||!path.startsWith(FRC.driveOwner(p)?.name+'.'))err('Odometri '+slot,'Sürüş alt sisteminden birikimli motor / quadrature enkoder seçin.');else{const identity=c.encType==='builtin'?path.split('.')[0]+'.'+c.parentMotor:path;if(sensors.has(identity))err('Odometri '+slot,'Aynı fiziksel enkoder iki teker grubu için kullanılamaz.');sensors.add(identity);if(c.encType==='builtin'&&!p.drive.motors[slot]?.includes(path.split('.')[0]+'.'+c.parentMotor))err('Odometri '+slot,'Enkoderin motoru bu teker grubunda olmalı.');if(c.encType==='quadrature'&&!c.parentMotor)warn('Odometri '+slot,'Quadrature enkoderin simülasyonda hareket etmesi için ilgili motorunu seçin.');}}
    }
  }
  const modules=new Set();
  for(const ss of p.subsystems)for(const c of ss.components){const label=ss.name+'.'+c.name;
    if(c.type==='encoder'&&c.encType==='quadrature'){port(c.channelA,label+' A','DIO',25);port(c.channelB,label+' B','DIO',25);if(!finite(c.countsPerRev,1,1e7))err(label,'Tur başına darbe sayısı pozitif olmalı.');}
    if(!FRC.deviceDefinitions[c.type])continue;
    if(c.type==='servo'){port(c.pwmChannel,label,'PWM',19);if(!finite(c.initialAngle,0,180))err(label,'Başlangıç açısı 0–180 olmalı.');}
    if(c.type==='led'){port(c.pwmChannel,label,'PWM',19);if(!integer(c.length,1,1000))err(label,'LED sayısı 1–1000 olmalı.');resource('AddressableLED:engine',label);}
    if(['solenoid','doublesolenoid','compressor'].includes(c.type)){
      if(!['REVPH','CTREPCM'].includes(c.moduleType)||!integer(c.moduleId,0,62))err(label,'Pnömatik modül / CAN ID geçersiz.');
      const mod=c.moduleType+':'+c.moduleId;if(!modules.has(mod)){can({id:c.moduleId,canbus:'rio'},label,false,'pneumatic-'+c.moduleType);modules.add(mod);}
      const max=c.moduleType==='REVPH'?15:7;
      for(const key of c.type==='solenoid'?['channel']:c.type==='doublesolenoid'?['forwardChannel','reverseChannel']:[]){if(!integer(c[key],0,max))err(label,key+' kanal sınırını aşıyor.');else resource('PNEU:'+mod+':'+c[key],label);}
      if(c.type==='compressor'){resource('Compressor:'+mod,label);boolean(c,['enabled'],label);}
    }
    if(c.type==='digital'){port(c.dioChannel,label,'DIO',25);boolean(c,['activeLow'],label);}
    if(c.type==='analog'){port(c.analogChannel,label,'Analog',7);for(const k of ['scale','offset'])if(!finite(c[k],-1e9,1e9))err(label,k+' geçersiz.');}
    if(c.type==='ultrasonic'){port(c.pingChannel,label+' ping','DIO',25);port(c.echoChannel,label+' echo','DIO',25);}
    if(c.type==='canrange')can({id:c.canId,canbus:c.canbus},label,false,'ctre-canrange');
    if(c.type==='relay')port(c.relayChannel,label,'Relay',3);
  }
  if(i.sysid.enabled){
    const c=paths.get(i.sysid.subsystem+'.'+i.sysid.motor);
    if(!c||c.type!=='motor')err('SysId','Geçerli alt sistem / motor seçin.');
    else if(!['fx','max','flex'].includes(FRC.motorFamily(c.motorType))||!FRC.isBrushless(c))err('SysId','Dahili konum / hız sensörlü TalonFX veya brushless SPARK motor seçin.');
    for(const k of ['ramp','step','timeout'])if(!finite(i.sysid[k],0.01,k==='step'?8:15))err('SysId',k+' geçersiz.');
    warn('SysId','Dashboard test komutları yalnızca Test modunda çalışır; mekanizmayı hazırlayın.');
  }
  for(const ss of p.subsystems)for(const c of ss.components)if(c.type==='motor'){
    const s={...FRC.physicsDefaults(),...c.simulation},label=ss.name+'.'+c.name+' simülasyon';
    const motion={...FRC.motionDefaults(),...c.motion};boolean(motion,['configured'],ss.name+'.'+c.name+' profil');
    for(const k of ['cruise','acceleration','tolerance'])if(!finite(motion[k],.000001,1e6))err(ss.name+'.'+c.name,'Profil '+k+' pozitif olmalı.');
    if(!finite(motion.jerk,0,1e7))err(ss.name+'.'+c.name,'Profil jerk geçersiz.');
    if(motion.configured&&(!['fx','max','flex'].includes(FRC.motorFamily(c.motorType))||!FRC.isBrushless(c)))err(ss.name+'.'+c.name,'Motion Magic / MAXMotion için sensörlü brushless CAN motoru seçin.');
    boolean(s,['enabled'],label);
    if(!['flywheel','elevator','arm'].includes(s.model))err(label,'Fizik modeli geçersiz.');
    if(!['auto','cim','neo','neo550','vortex','falcon500','krakenx60','krakenx44','775pro'].includes(s.motorModel))err(label,'Fizik motor modeli geçersiz.');
    for(const k of ['gearing','inertia','mass','radius','length'])if(!finite(s[k],0.000001,1e6))err(label,k+' pozitif olmalı.');
    if(!finite(s.min,-10000,10000)||!finite(s.max,-10000,10000)||s.min>=s.max||!finite(s.initial,s.min,s.max))err(label,'Başlangıç konumu alt / üst sınırlar içinde olmalı.');
  }
  for(const [idx,s]of p.autonomous.entries())if(s.type==='parallel'){
    if(!Array.isArray(s.methods)||s.methods.length<2||s.methods.length>16){err('Paralel adım '+(idx+1),'En az iki metot seçin.');continue;}
    const owners=new Set();for(const path of s.methods){if(paths.get(path)?.type!=='method')err('Paralel adım','Metot bulunamadı: '+path);const owner=String(path).split('.')[0];if(owners.has(owner))err('Paralel adım','Aynı alt sistemi iki paralel komut kullanamaz.');owners.add(owner);}
  }
  for(const b of p.controllerBindings){if(b.controller&&!['driver','operator'].includes(b.controller))err('Kumanda','Kumanda türü geçersiz.');if(b.controller==='operator'&&!i.operator.enabled)err('Kumanda','Operatör kumandasını etkinleştirin.');}
  const sourcePaths=new Set();for(const c of i.customSources){if(!c||typeof c.path!=='string'||!/^frc\/robot\/custom\/(?:[A-Za-z_]\w*\/)*[A-Za-z_]\w*\.java$/.test(c.path)||typeof c.text!=='string'||c.text.length>1e6)err('Özel Java','Dosyalar frc/robot/custom altında .java olmalı (en fazla 1 MB).');if(sourcePaths.has(c?.path))err('Özel Java','Aynı kaynak yolu iki kez eklenmiş.');sourcePaths.add(c?.path);}
  const vendorNames=new Set(Object.values(FRC.assets?.vendors||{}).map(v=>v.fileName)),vendorIds=new Set(Object.values(FRC.assets?.vendors||{}).map(v=>v.uuid));
  for(const v of Object.values(FRC.integrationAssets?.vendors||{})){vendorNames.add(v.fileName);vendorIds.add(v.uuid);}
  for(const v of i.customVendors||[]){if(!FRC.validCustomVendor(v))err('Vendordep','Geçerli 2026 vendordep gerekli.');else if(vendorNames.has(v.fileName)||vendorIds.has(v.uuid))err('Vendordep','Yerleşik veya yinelenen vendor üstüne yazılamaz.');else{vendorNames.add(v.fileName);vendorIds.add(v.uuid);}}
};
