package frc.robot.sim;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import edu.wpi.first.math.geometry.*;
import edu.wpi.first.networktables.*;
import edu.wpi.first.wpilibj.Timer;
import java.util.*;
import org.photonvision.PhotonCamera;
import org.photonvision.simulation.PhotonCameraSim;
import org.photonvision.targeting.*;

/** Converts Unity geometric AprilTag frames into the team's normal PhotonCamera NT packets. */
public final class AshinaPhotonBridge implements AutoCloseable {
  private final StringSubscriber input;
  private final PhotonCamera camera;
  private final PhotonCameraSim simulation;
  private final ObjectMapper json = new ObjectMapper();
  private long lastFrame = -1;
  private String lastPacket = "";
  private final StringPublisher status;

  public AshinaPhotonBridge(String name) {
    var nt=NetworkTableInstance.getDefault();
    input=nt.getStringTopic("/AshinaSim/Vision/"+name+"/frame").subscribe("");
    status=nt.getStringTopic("/AshinaRobot/Bridge/visionStatus").publish();
    camera=new PhotonCamera(name);
    simulation=new PhotonCameraSim(camera);
    simulation.enableRawStream(false);simulation.enableProcessedStream(false);
  }

  /** Called from simulationPeriodic; the real robot's PhotonCamera code remains in use. */
  public void update() {
    String packet=input.get();if(packet.isEmpty()||packet.equals(lastPacket))return;
    lastPacket=packet;
    try {
      JsonNode frame=json.readTree(packet);long sequence=frame.path("sequence").asLong();
      if(sequence==lastFrame)return;lastFrame=sequence;
      double latency=Math.max(0,Math.min(1000,frame.path("latency").asDouble()));
      long now=(long)(Timer.getFPGATimestamp()*1_000_000);
      var metadata=new PhotonPipelineMetadata();metadata.captureTimestampMicros=now-(long)(latency*1000);metadata.publishTimestampMicros=now;metadata.sequenceID=sequence;metadata.timeSinceLastPong=0;
      List<PhotonTrackedTarget> targets=new ArrayList<>();List<Short> ids=new ArrayList<>();
      for(JsonNode source:frame.path("targets")) {
        int id=source.path("id").asInt(-1);if(id<0||targets.size()>=64)continue;
        var target=new PhotonTrackedTarget();target.fiducialId=id;target.yaw=source.path("yaw").asDouble();target.pitch=source.path("pitch").asDouble();target.area=source.path("area").asDouble();
        target.objDetectId=-1;target.objDetectConf=0;target.poseAmbiguity=.001;
        target.bestCameraToTarget=transform(source.path("cameraToTarget"));target.altCameraToTarget=target.bestCameraToTarget;
        target.minAreaRectCorners=List.of(new TargetCorner(0,0),new TargetCorner(1,0),new TargetCorner(1,1),new TargetCorner(0,1));
        target.detectedCorners=target.minAreaRectCorners;targets.add(target);ids.add((short)id);
      }
      Optional<MultiTargetPNPResult> multitag=Optional.empty();
      if(targets.size()>=2)multitag=Optional.of(new MultiTargetPNPResult(new PnpResult(transform(frame.path("cameraPose")),.01),ids));
      simulation.submitProcessedFrame(new PhotonPipelineResult(metadata,targets,multitag));
      status.set("Unity frame "+sequence+" · "+targets.size()+" targets");
    } catch(Exception error) {status.set("Unity vision error: "+error.getMessage());}
  }
  private static Transform3d transform(JsonNode v) {
    if(!v.isArray()||v.size()!=7)throw new IllegalArgumentException("Expected [x,y,z,w,x,y,z]");
    double[] a=new double[7];for(int i=0;i<7;i++){a[i]=v.get(i).asDouble(Double.NaN);if(!Double.isFinite(a[i]))throw new IllegalArgumentException("Non-finite camera transform");}
    return new Transform3d(new Translation3d(a[0],a[1],a[2]),new Rotation3d(new Quaternion(a[3],a[4],a[5],a[6])));
  }
  @Override public void close(){input.close();status.close();simulation.close();camera.close();}
}
