package tw.traininglab.legacyremote;

import org.json.JSONObject;
import java.net.*;
import java.nio.charset.Charset;
import java.util.UUID;
import java.util.concurrent.*;

/** Protocol v1 peer of Unity RemoteLink. All socket/session mutations use one worker. */
public final class RemoteClient {
    private static final Charset UTF8 = Charset.forName("UTF-8");
    private final ScheduledExecutorService worker = Executors.newSingleThreadScheduledExecutor();
    private DatagramSocket socket;
    private String nonce, code, session, command;
    private int sequence, receivedSequence, action, ack;
    private long started, lastSeen, nextHello;
    private volatile float[] axes = new float[4];
    public volatile boolean connected;
    public volatile boolean pending;
    public volatile String status = "輸入電腦 IPv4 與六位配對碼";
    public volatile String telemetry = "高度 —   速度 —   航向 —";
    public volatile String task = "請先在電腦啟動接收；兩端需同一區域網路。";

    public RemoteClient() {
        worker.scheduleAtFixedRate(new Runnable() { public void run() { tick(); } }, 0, 33, TimeUnit.MILLISECONDS);
    }
    public void connect(final String address, final String pairingCode) {
        worker.execute(new Runnable() { public void run() {
            close("配對中…");
            try {
                String[] parts = address.trim().split("\\.", -1);
                if (parts.length != 4 || !pairingCode.matches("[0-9]{6}")) throw new IllegalArgumentException();
                byte[] ip = new byte[4];
                for (int i=0;i<4;i++) {
                    if (!parts[i].matches("[0-9]{1,3}")) throw new IllegalArgumentException();
                    int n = Integer.parseInt(parts[i]);
                    if (n > 255) throw new IllegalArgumentException();
                    ip[i] = (byte)n;
                }
                socket = new DatagramSocket();
                socket.connect(InetAddress.getByAddress(ip), 47777);
                socket.setSoTimeout(1);
                code = pairingCode; nonce = UUID.randomUUID().toString().replace("-", "");
                started = now(); nextHello = 0;
            } catch (IllegalArgumentException e) { close("請輸入有效 IPv4 與六位數配對碼"); }
              catch (Exception e) { close("無法連線：" + e.getClass().getSimpleName()); }
        }});
    }
    public void input(float lx, float ly, float rx, float ry, int mode, float sensitivity) {
        float throttle = mode == 0 ? ly : ry;
        float yaw = mode == 2 ? rx : lx;
        float pitch = mode == 0 ? ry : ly;
        float roll = mode == 2 ? lx : rx;
        axes = new float[]{axis(throttle,sensitivity),axis(yaw,sensitivity),axis(pitch,sensitivity),axis(roll,sensitivity)};
    }
    static float axis(float value, float sensitivity) {
        if (Float.isNaN(value) || Float.isInfinite(value)) return 0;
        float magnitude = Math.min(1, Math.abs(value));
        return magnitude <= .08f ? 0 : Math.signum(value)*(magnitude-.08f)/.92f*Math.max(0,Math.min(1,sensitivity));
    }
    public void request(final String value) {
        worker.execute(new Runnable() { public void run() {
            if (!connected || pending) return;
            if (!value.equals("takeoff") && !value.equals("land") && !value.equals("reset") && !value.equals("confirm")) return;
            action++; command=value; pending=true;
        }});
    }
    public void disconnect() {
        axes = new float[4];
        worker.execute(new Runnable() { public void run() { close("已中斷；請重新啟動電腦接收並配對"); }});
    }
    public void destroy() { disconnect(); worker.shutdown(); }
    private static long now() { return System.nanoTime()/1000000L; }
    private JSONObject packet(String kind) throws Exception {
        return new JSONObject().put("version",1).put("kind",kind);
    }
    private void send(JSONObject p) throws Exception {
        byte[] b=p.toString().getBytes(UTF8); socket.send(new DatagramPacket(b,b.length));
    }
    private void tick() {
        if (socket == null) return;
        try {
            if (connected && now()-lastSeen > 600) { close("遙測逾時，已中斷；請重新配對"); return; }
            if (!connected && now()-started > 5000) { close("配對逾時：檢查 IP、六位碼、Wi-Fi 與防火牆"); return; }
            if (!connected && now() >= nextHello) {
                send(packet("hello").put("code",code).put("nonce",nonce)); nextHello=now()+250;
            } else if (connected) {
                float[] a=axes;
                JSONObject input=new JSONObject().put("throttle",a[0]).put("yaw",a[1]).put("pitch",a[2]).put("roll",a[3]);
                send(packet("control").put("session",session).put("sequence",++sequence).put("input",input)
                    .put("action",action).put("command",command == null ? "" : command));
            }
            for (int i=0;i<32;i++) {
                byte[] b=new byte[2049]; DatagramPacket d=new DatagramPacket(b,b.length);
                try { socket.receive(d); } catch (SocketTimeoutException e) { break; }
                if (d.getLength()>2048) continue;
                try { receive(new JSONObject(new String(b,0,d.getLength(),UTF8))); }
                catch (org.json.JSONException ignored) { }
                if (socket==null) return;
            }
        } catch (Exception e) { close("網路中斷：" + e.getClass().getSimpleName()); }
    }
    private void receive(JSONObject p) throws Exception {
        if (p.optInt("version") != 1) return;
        String kind=p.optString("kind");
        if (!connected && kind.equals("welcome") && nonce.equals(p.optString("nonce")) && !p.optString("session").isEmpty()) {
            session=p.getString("session"); connected=true; lastSeen=now(); status="已連線 • 30 Hz 控制"; return;
        }
        if (!connected || !session.equals(p.optString("session"))) return;
        if (kind.equals("bye")) { close("電腦已離線；請重新配對"); return; }
        int seq=p.optInt("sequence");
        if (!kind.equals("state") || seq <= receivedSequence) return;
        receivedSequence=seq; lastSeen=now();
        ack=Math.max(ack,Math.min(action,p.optInt("ackAction"))); pending=action>ack;
        telemetry=String.format(java.util.Locale.US,"高度 %.1f m   速度 %.1f m/s   航向 %.0f°",p.optDouble("height",0),p.optDouble("speed",0),p.optDouble("heading",0));
        task=(p.optBoolean("crashed") ? "碰撞 • " : p.optBoolean("flying") ? "飛行中 • " : "待命 • ")+p.optString("task");
    }
    private void close(String message) {
        if (socket!=null) {
            if (connected) try { send(packet("bye").put("session",session)); } catch (Exception ignored) { }
            socket.close(); socket=null;
        }
        connected=false; pending=false; session=null; sequence=receivedSequence=action=ack=0;
        axes=new float[4]; command=null; telemetry="高度 —   速度 —   航向 —"; task="請重新配對後再操作。"; status=message;
    }
}
