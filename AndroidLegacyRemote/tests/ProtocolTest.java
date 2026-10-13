package tw.traininglab.legacyremote;

import org.json.JSONObject;
import java.net.*;
import java.nio.charset.StandardCharsets;

/** Desktop UDP fixture for the actual Android client's networking code. */
public final class ProtocolTest {
    static int checks;
    static DatagramSocket host;
    static SocketAddress phone;
    static RemoteClient client;
    static void check(boolean value,String name) { if(!value)throw new AssertionError(name); checks++; }
    static JSONObject read(String kind) throws Exception {
        long end=System.nanoTime()+2000000000L;
        while(System.nanoTime()<end){
            byte[] b=new byte[4096]; DatagramPacket d=new DatagramPacket(b,b.length); host.receive(d); phone=d.getSocketAddress();
            JSONObject p=new JSONObject(new String(b,0,d.getLength(),StandardCharsets.UTF_8));
            if(p.optString("kind").equals(kind))return p;
        }
        throw new AssertionError("missing "+kind);
    }
    static void send(JSONObject p) throws Exception {
        byte[] b=p.toString().getBytes(StandardCharsets.UTF_8); host.send(new DatagramPacket(b,b.length,phone));
    }
    static JSONObject state(int seq,String session) throws Exception {
        return new JSONObject().put("version",1).put("kind","state").put("session",session).put("sequence",seq)
            .put("height",12.5).put("speed",2).put("heading",90).put("task","fixture").put("ackAction",0);
    }
    public static void main(String[] args) throws Exception {
        host=new DatagramSocket(47777,InetAddress.getByName("127.0.0.1")); host.setSoTimeout(2000);
        client=new RemoteClient();
        try {
            check(RemoteClient.axis(.04f,1)==0,"deadzone");
            check(RemoteClient.axis(Float.NaN,1)==0,"reject NaN");
            check(RemoteClient.axis(2,1)==1,"axis clamp");
            client.connect("bad-address","123456"); Thread.sleep(80); check(!client.connected && client.status.contains("IPv4"),"invalid address");
            client.connect("127.0.0.1","abcdef"); Thread.sleep(80); check(!client.connected && client.status.contains("IPv4"),"numeric code validation");
            client.connect("127.0.0.1","123456");
            JSONObject hello=read("hello"); check(hello.getString("code").equals("123456"),"pairing code");
            check(hello.getInt("version")==1,"protocol version");
            JSONObject welcome=new JSONObject().put("version",1).put("kind","welcome").put("nonce","wrong").put("session","test-session");
            send(welcome); Thread.sleep(70); check(!client.connected,"reject wrong nonce");
            JSONObject retry=read("hello"); check(retry.getString("nonce").equals(hello.getString("nonce")),"hello retransmission retains nonce");
            welcome.put("nonce",hello.getString("nonce")); send(welcome); JSONObject control=read("control");
            check(client.connected && control.getString("session").equals("test-session"),"welcome establishes session");
            send(state(1,"test-session")); Thread.sleep(60); check(client.telemetry.contains("12.5"),"telemetry displayed");
            send(state(2,"wrong").put("height",99)); Thread.sleep(60); check(!client.telemetry.contains("99"),"wrong session rejected");
            send(state(1,"test-session").put("height",99)); Thread.sleep(60); check(!client.telemetry.contains("99"),"replayed state rejected");
            client.input(1,-1,-1,1,0,1);
            // Drain prior neutral frames until the changed input reaches the wire.
            do { control=read("control"); } while(control.getJSONObject("input").getDouble("yaw")==0);
            JSONObject a=control.getJSONObject("input");
            check(a.getDouble("throttle")==-1 && a.getDouble("yaw")==1 && a.getDouble("pitch")==1 && a.getDouble("roll")==-1,"Mode 2 axes");
            send(state(2,"test-session")); client.request("takeoff");
            do { control=read("control"); } while(control.getInt("action")==0);
            check(control.getInt("action")==1 && control.getString("command").equals("takeoff"),"takeoff command");
            client.request("land"); JSONObject repeated=read("control");
            check(repeated.getInt("action")==1 && repeated.getString("command").equals("takeoff"),"pending action not overwritten");
            check(repeated.getInt("sequence")>control.getInt("sequence"),"control sequence increments");
            send(state(3,"test-session").put("ackAction",1)); Thread.sleep(60); check(!client.pending,"ack clears pending");
            client.request("land");
            do { control=read("control"); } while(control.getInt("action")!=2);
            check(control.getString("command").equals("land"),"next action has new id");
            send(state(4,"test-session").put("ackAction",2));
            client.input(1,-1,-1,1,1,1);
            do { control=read("control"); } while(control.getJSONObject("input").getDouble("throttle")!=1);
            a=control.getJSONObject("input"); check(a.getDouble("pitch")==-1 && a.getDouble("yaw")==1 && a.getDouble("roll")==-1,"Mode 1 axes");
            client.input(1,-1,-1,1,2,1);
            do { control=read("control"); } while(control.getJSONObject("input").getDouble("yaw")!=-1);
            check(control.getJSONObject("input").getDouble("roll")==1,"Mode 3 axes");
            Thread.sleep(720); check(!client.connected && client.status.contains("逾時"),"600ms telemetry timeout");
            check(read("bye").getString("session").equals("test-session"),"timeout sends bye");
            client.connect("127.0.0.1","123456"); hello=read("hello");
            send(welcome.put("nonce",hello.getString("nonce")).put("session","new-session")); control=read("control");
            check(control.getInt("action")==0 && control.getInt("sequence")==1,"reconnect resets counters");
            check(control.getJSONObject("input").getDouble("yaw")==0,"reconnect neutral input");
            client.disconnect(); check(read("bye").getString("session").equals("new-session"),"manual disconnect sends bye");
            Thread.sleep(50); check(!client.connected,"manual disconnect closes client");
            System.out.println("PASS: "+checks+" native remote protocol assertions");
        } finally { client.destroy(); host.close(); }
    }
}
