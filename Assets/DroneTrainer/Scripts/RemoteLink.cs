using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using DroneTrainer.Core;

namespace DroneTrainer
{
    public enum RunRole { Local, Host, Remote }

    [Serializable]
    public sealed class WirePacket
    {
        public int version = 1;
        public string kind, code, nonce, session, command;
        public int sequence, action, ackAction;
        public FlightInput input;
        public float height, speed, heading;
        public bool flying, crashed;
        public string task;
    }

    [DefaultExecutionOrder(-50)]
    public sealed class RemoteLink : MonoBehaviour
    {
        public const int Port = 47777;
        public RunRole Role { get; private set; }
        public string Status { get; private set; } = "單機模式";
        public string PairCode { get; private set; }
        public bool Connected { get { return gate.Session != null; } }
        public FlightInput ReceivedInput { get; private set; }
        public FlightInput OutgoingInput;
        public WirePacket Telemetry { get; private set; }
        public Action<string> CommandReceived;
        public Action LinkLost;
        public Func<WirePacket> ReadTelemetry;
        public bool CommandPending { get { return pendingAction > acknowledgedAction; } }
        readonly PeerGate gate = new PeerGate();
        UdpClient socket;
        IPEndPoint peer;
        string nonce, requestedCode, pendingCommand;
        int sendSequence, pendingAction, acknowledgedAction;
        double nextSend, connectStarted;
        bool connecting;
        static double Now { get { return Time.realtimeSinceStartupAsDouble; } }

        public void SetRole(RunRole role)
        {
            Close(); Role = role;
            Status = role == RunRole.Local ? "單機模式" : role == RunRole.Host ? "請啟動接收" : "輸入電腦 IPv4 與配對碼";
        }
        public void Listen()
        {
            Close(); Role = RunRole.Host;
            try
            {
                socket = new UdpClient(new IPEndPoint(IPAddress.Any, Port));
                socket.Client.Blocking = false;
                PairCode = UnityEngine.Random.Range(100000, 1000000).ToString();
                Status = "等待手機配對";
            }
            catch (Exception e) when (e is SocketException || e is ArgumentException)
            { Close(); Status = "無法監聽 UDP " + Port + "：" + e.Message; }
        }
        public void Connect(string address, string code)
        {
            Close(); Role = RunRole.Remote;
            IPAddress ip;
            if (!IPAddress.TryParse(address.Trim(), out ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            { Status = "請輸入電腦 IPv4，例如 192.168.1.10"; return; }
            if (code.Trim().Length != 6) { Status = "配對碼須為六位數"; return; }
            try
            {
                socket = new UdpClient(0); socket.Client.Blocking = false;
                peer = new IPEndPoint(ip, Port); requestedCode = code.Trim();
                nonce = Guid.NewGuid().ToString("N");
                connectStarted = Now; nextSend = 0; connecting = true;
                Status = "配對中…";
            }
            catch (SocketException e) { Close(); Status = "連線失敗：" + e.Message; }
        }
        public void Request(string command)
        {
            if (!Connected || Role != RunRole.Remote || CommandPending) return;
            pendingAction++; pendingCommand = command;
        }
        void Update()
        {
            if (socket == null) return;
            try
            {
                for (int count = 0; count < 64 && socket.Available > 0; count++)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    byte[] bytes = socket.Receive(ref from);
                    if (bytes.Length > 2048) continue;
                    WirePacket packet;
                    try { packet = JsonUtility.FromJson<WirePacket>(Encoding.UTF8.GetString(bytes)); }
                    catch (ArgumentException) { continue; }
                    if (packet == null || packet.version != 1) continue;
                    Receive(packet, from);
                    if (socket == null) return;
                }
                if (gate.Expired(Now))
                {
                    Close(); Status = "連線逾時，控制歸零；請重新啟動接收與配對";
                    return;
                }
                if (connecting && Now - connectStarted > 5)
                { Close(); Status = "配對逾時：檢查 IP、配對碼、Wi-Fi 與防火牆"; return; }
                if (Now < nextSend) return;
                nextSend = Now + (connecting ? .25 : Role == RunRole.Host ? .1 : 1.0 / 30);
                if (connecting) Send(new WirePacket { kind = "hello", code = requestedCode, nonce = nonce });
                else if (Connected && Role == RunRole.Remote)
                    Send(new WirePacket { kind = "control", session = gate.Session, sequence = ++sendSequence,
                        input = OutgoingInput, action = pendingAction, command = pendingCommand });
                else if (Connected && Role == RunRole.Host)
                {
                    var packet = ReadTelemetry != null ? ReadTelemetry() : new WirePacket();
                    packet.kind = "state"; packet.session = gate.Session; packet.sequence = ++sendSequence;
                    packet.ackAction = gate.LastAction; Send(packet);
                }
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.WouldBlock) return;
                Close(); Status = "網路錯誤：" + e.SocketErrorCode;
            }
            catch (ObjectDisposedException) { Close(); Status = "連線已關閉"; }
        }
        bool SamePeer(IPEndPoint from) { return peer != null && from.Equals(peer); }
        void Receive(WirePacket p, IPEndPoint from)
        {
            if (Role == RunRole.Host && p.kind == "hello")
            {
                if (p.code != PairCode || string.IsNullOrEmpty(p.nonce) || p.nonce.Length > 64) return;
                if (Connected && (!SamePeer(from) || nonce != p.nonce)) return;
                if (!Connected)
                {
                    peer = from; nonce = p.nonce;
                    gate.Open(Guid.NewGuid().ToString("N"), Now);
                    Status = "手機已連線";
                }
                Send(new WirePacket { kind = "welcome", nonce = nonce, session = gate.Session }); return;
            }
            if (!SamePeer(from)) return;
            if (Role == RunRole.Remote && connecting && p.kind == "welcome" && p.nonce == nonce && !string.IsNullOrEmpty(p.session))
            {
                gate.Open(p.session, Now); connecting = false; nextSend = 0;
                Status = "已連線 • 30 Hz 控制"; return;
            }
            if (p.kind == "bye" && Connected && p.session == gate.Session)
            { Close(); Status = "對端離線，請重新配對"; return; }
            if (Role == RunRole.Host && p.kind == "control" && gate.Accept(p.session, p.sequence, p.input, Now))
            {
                ReceivedInput = p.input;
                if (gate.Action(p.action)) CommandReceived?.Invoke(p.command);
            }
            else if (Role == RunRole.Remote && p.kind == "state" && gate.Accept(p.session, p.sequence, default(FlightInput), Now))
            { Telemetry = p; acknowledgedAction = Math.Max(acknowledgedAction, p.ackAction); }
        }
        void Send(WirePacket p)
        {
            if (socket == null || peer == null) return;
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(p));
            socket.Send(bytes, bytes.Length, peer);
        }
        public void Close()
        {
            if (socket != null && Connected)
                try { Send(new WirePacket { kind = "bye", session = gate.Session }); } catch (SocketException) { }
            bool wasOpen = socket != null;
            socket?.Close(); socket = null;
            gate.Close(); peer = null; connecting = false; PairCode = null;
            sendSequence = 0; pendingAction = 0; acknowledgedAction = 0; pendingCommand = null;
            ReceivedInput = default(FlightInput); OutgoingInput = default(FlightInput); Telemetry = null;
            Status = "已中斷";
            if (wasOpen) LinkLost?.Invoke();
        }
        void OnApplicationPause(bool paused) { if (paused) Close(); }
        void OnApplicationFocus(bool focus) { if (!focus && Application.isMobilePlatform) Close(); }
        void OnDestroy() { Close(); }
        public static string LocalAddresses()
        {
            try
            {
                var list = new System.Collections.Generic.List<string>();
                foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)) list.Add(ip.ToString());
                return list.Count == 0 ? "用 ipconfig 查詢電腦 IPv4" : string.Join(" / ", list.ToArray());
            }
            catch (SocketException) { return "用 ipconfig 查詢電腦 IPv4"; }
        }
    }
}
