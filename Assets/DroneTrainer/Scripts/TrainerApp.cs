using UnityEngine;
using UnityEngine.UI;
using DroneTrainer.Core;

namespace DroneTrainer
{
    public sealed class TrainerApp : MonoBehaviour
    {
        public DroneMotor Drone { get; private set; }
        public RemoteLink Link { get; private set; }
        public PracticeCourse Course { get; private set; }
        public FlightCamera CameraRig { get; private set; }
        public StickMode Mode = StickMode.Mode2;
        public float Sensitivity = .75f, ThrottleSensitivity = .7f;
        public bool Paused { get; private set; }
        public TrainerHud Hud { get; private set; }
        bool focused = true;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Mode = (StickMode)Mathf.Clamp(PlayerPrefs.GetInt("stickMode", 0), 0, 2);
            Sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("sensitivity", .75f), .2f, 1);
            ThrottleSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("throttleSensitivity", .7f), .2f, 1);
            Drone = SceneFactory.BuildField();
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.64f, .78f, .86f); camera.farClipPlane = 200; camera.nearClipPlane = .05f;
            camera.transform.position = new Vector3(-5, 4, -8); camera.gameObject.AddComponent<AudioListener>();
            CameraRig = camera.gameObject.AddComponent<FlightCamera>(); CameraRig.target = Drone.transform;
            Course = gameObject.AddComponent<PracticeCourse>(); Course.drone = Drone; Course.Configure(PracticeKind.Free);
            Link = gameObject.AddComponent<RemoteLink>();
            Link.CommandReceived = Command;
            Link.LinkLost = () => { if (Drone != null && Drone.Body != null) Drone.Neutralize(); if (Hud != null) Hud.ClearSticks(); };
            Link.ReadTelemetry = () => new WirePacket { height = Drone.Height, speed = Drone.Speed,
                heading = Drone.transform.eulerAngles.y, flying = Drone.Flying, crashed = Drone.Crashed,
                task = Paused ? "電腦已暫停" : Course.Instruction };
            Hud = gameObject.AddComponent<TrainerHud>(); Hud.Build(this);
        }
        void Update()
        {
            Vector2 left = Hud.Left.Value, right = Hud.Right.Value;
            bool typing = UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null &&
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null;
            if (!typing && !Application.isMobilePlatform && focused)
            {
                left += new Vector2(Key(KeyCode.D) - Key(KeyCode.A), Key(KeyCode.W) - Key(KeyCode.S));
                right += new Vector2(Key(KeyCode.RightArrow) - Key(KeyCode.LeftArrow), Key(KeyCode.UpArrow) - Key(KeyCode.DownArrow));
                if (Input.GetKeyDown(KeyCode.Space)) Command((Link.Role == RunRole.Remote ? Link.Telemetry != null && Link.Telemetry.flying : Drone.Flying) ? "land" : "takeoff");
                if (Input.GetKeyDown(KeyCode.R)) Command("reset");
                if (Input.GetKeyDown(KeyCode.C)) CameraRig.Cycle();
            }
            var input = focused && !Paused ? ControlLogic.Map(left.x, left.y, right.x, right.y, Mode, Sensitivity, ThrottleSensitivity) : default(FlightInput);
            Link.OutgoingInput = input;
            Drone.Input = Paused ? default(FlightInput) : Link.Role == RunRole.Host ? Link.ReceivedInput : Link.Role == RunRole.Local ? input : default(FlightInput);
            Course.Paused = Paused || Link.Role == RunRole.Remote;
        }
        static float Key(KeyCode key) { return Input.GetKey(key) ? 1 : 0; }
        public void SetRole(RunRole role)
        {
            SetPaused(false); Link.SetRole(role); Drone.ResetFlight(); Course.Configure(Course.Kind);
            CameraRig.GetComponent<Camera>().enabled = role != RunRole.Remote;
            CameraRig.enabled = role != RunRole.Remote;
            Hud.ClearSticks(); Hud.RefreshRole();
        }
        public void Command(string command)
        {
            if (Link.Role == RunRole.Remote) { Link.Request(command); return; }
            if (Paused && command != "reset") return;
            if (command == "takeoff") Drone.Takeoff();
            else if (command == "land") Drone.Land();
            else if (command == "reset") { SetPaused(false); Drone.ResetFlight(); Course.Configure(Course.Kind); Hud.ClearSticks(); }
        }
        public void SelectPractice(PracticeKind kind)
        { SetPaused(false); Drone.ResetFlight(); Course.Configure(kind); Hud.ClearSticks(); }
        public void SetPaused(bool pause)
        {
            Paused = pause; Drone.Body.isKinematic = false; Drone.Neutralize(); Hud?.ClearSticks();
            // Keep UDP heartbeats alive while freezing only the simulated rigidbody.
            Drone.enabled = !pause; Drone.Body.isKinematic = pause;
        }
        public void SavePreferences()
        {
            PlayerPrefs.SetInt("stickMode", (int)Mode); PlayerPrefs.SetFloat("sensitivity", Sensitivity);
            PlayerPrefs.SetFloat("throttleSensitivity", ThrottleSensitivity); PlayerPrefs.Save();
        }
        void OnApplicationFocus(bool focus)
        {
            focused = focus; if (focus || Drone == null) return;
            if (Link.Role != RunRole.Host) { Drone.Neutralize(); Hud.ClearSticks(); }
            if (Link.Role == RunRole.Local) SetPaused(true);
        }
        void OnApplicationPause(bool pause)
        { if (pause && Drone != null) { Link.Close(); if (Link.Role != RunRole.Remote) SetPaused(true); } }
        void OnDestroy() { Time.timeScale = 1; Screen.sleepTimeout = SleepTimeout.SystemSetting; }
    }
}
