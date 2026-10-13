using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DroneTrainer.Core;

namespace DroneTrainer
{
    public sealed class TrainerHud : MonoBehaviour
    {
        public TouchStick Left { get; private set; }
        public TouchStick Right { get; private set; }
        TrainerApp app;
        Canvas canvas;
        RectTransform safe;
        Font font;
        Sprite circle;
        Text telemetry, task, linkStatus, leftLabel, rightLabel, flightState, cameraLabel, pauseLabel, modeLabel, networkButtonLabel;
        Text windLabel, sensitivityLabel, throttleLabel;
        Text hostAddress;
        InputField ip, code;
        GameObject network, settings, remoteBackdrop;
        Button networkButton;
        Button practiceButton, cameraButton, pauseButton;
        readonly Color panel = new Color(.035f, .075f, .10f, .94f);
        readonly Color muted = new Color(.63f, .74f, .78f);
        readonly Color accent = new Color(.18f, .85f, .8f);
        string localIps;

        public void Build(TrainerApp trainer)
        {
            app = trainer; localIps = RemoteLink.LocalAddresses();
            font = Resources.Load<Font>("TrainerFont");
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "PingFang TC", "Noto Sans CJK TC", "Arial Unicode MS" }, 24);
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            circle = MakeCircle();
            var go = new GameObject("Trainer UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            safe = Rect("Safe area", go.transform, 0, 0, 0, 0);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one; safe.offsetMin = safe.offsetMax = Vector2.zero;
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EventSystem.current.sendNavigationEvents = false;

            remoteBackdrop = Panel("Remote backdrop", safe, 0, 0, 0, 0, new Color(.035f, .07f, .095f)).gameObject;
            var back = (RectTransform)remoteBackdrop.transform; back.anchorMin = Vector2.zero; back.anchorMax = Vector2.one;
            back.offsetMin = back.offsetMax = Vector2.zero;
            var remoteTitle = Rect("Remote title", back, 0, 0, 1000, 140);
            remoteTitle.anchorMin = remoteTitle.anchorMax = new Vector2(.5f, .5f); remoteTitle.pivot = new Vector2(.5f, .5f); remoteTitle.anchoredPosition = new Vector2(0, 20);
            Label(remoteTitle, "REMOTE CONTROLLER", 0, 0, 1000, 52, 32, accent, TextAnchor.MiddleCenter);
            Label(remoteTitle, "使用下方雙搖桿控制電腦場景\n橘色機頭為前方；手機顯示即時遙測", 0, 58, 1000, 72, 22, muted, TextAnchor.MiddleCenter);

            var top = Group("Top", safe, true, 1540, 240);
            Panel("Header", top, 0, 0, 1540, 74, panel);
            Label(top, "DRONE / 無人機飛行訓練", 24, 0, 640, 46, 29, Color.white);
            Label(top, "原創模擬器  /  自訂術科練習  /  離線可玩", 25, 43, 650, 27, 16, muted);
            flightState = Label(top, "待命", 1210, 12, 300, 50, 25, accent, TextAnchor.MiddleRight);
            Button(top, "手機／單機飛行", 0, 86, 240, 54, () => app.SetRole(RunRole.Local));
            Button(top, "電腦接收手機", 250, 86, 240, 54, () => app.SetRole(RunRole.Host));
            Button(top, "手機遙控器", 500, 86, 240, 54, () => app.SetRole(RunRole.Remote));
            practiceButton = Button(top, "練習項目", 1000, 86, 180, 54, CyclePractice);
            Button(top, "控制設定", 1190, 86, 170, 54, () => settings.SetActive(!settings.activeSelf));
            Button(top, "說明", 1370, 86, 170, 54, () => help.SetActive(!help.activeSelf));
            var networkRect = Panel("Network", top, 0, 150, 1540, 72, panel); network = networkRect.gameObject;
            ip = InputBox(networkRect, "電腦 IPv4", 14, 10, 260, "192.168.1.10");
            code = InputBox(networkRect, "六位配對碼", 286, 10, 190, ""); code.contentType = InputField.ContentType.IntegerNumber; code.characterLimit = 6;
            networkButton = Button(networkRect, "連線", 490, 10, 150, 50, () =>
            {
                ClearSticks();
                if (app.Link.Role == RunRole.Host) app.Link.Listen();
                else app.Link.Connect(ip.text, code.text);
            });
            networkButtonLabel = networkButton.GetComponentInChildren<Text>();
            Button(networkRect, "中斷", 650, 10, 130, 50, () => app.Link.Close());
            linkStatus = Label(networkRect, "", 802, 8, 720, 56, 18, accent);
            hostAddress = Label(networkRect, "", 14, 8, 460, 56, 18, Color.white);

            var bottom = Group("Bottom", safe, false, 1540, 310);
            Left = Stick(bottom, 12, 18); Right = Stick(bottom, 1278, 18);
            leftLabel = Label(bottom, "", 0, 275, 280, 32, 20, Color.white, TextAnchor.MiddleCenter);
            rightLabel = Label(bottom, "", 1260, 275, 280, 32, 20, Color.white, TextAnchor.MiddleCenter);
            Panel("Telemetry", bottom, 325, 18, 890, 90, panel);
            telemetry = Label(bottom, "", 346, 24, 850, 74, 25, Color.white, TextAnchor.MiddleCenter);
            task = Label(bottom, "", 300, -73, 940, 80, 21, Color.white, TextAnchor.MiddleCenter);
            Button(bottom, "起飛", 370, 124, 180, 64, () => app.Command("takeoff"), accent);
            Button(bottom, "降落", 562, 124, 180, 64, () => app.Command("land"));
            Button(bottom, "重置", 754, 124, 180, 64, () => app.Command("reset"));
            var pause = Button(bottom, "暫停", 946, 124, 180, 64, () =>
            { if (app.Link.Role != RunRole.Remote) app.SetPaused(!app.Paused); });
            pauseLabel = pause.GetComponentInChildren<Text>();
            pauseButton = pause;
            var cam = Button(bottom, "視角：追蹤", 390, 205, 330, 54, () => app.CameraRig.Cycle()); cameraLabel = cam.GetComponentInChildren<Text>();
            cameraButton = cam;
            var mode = Button(bottom, "", 736, 205, 390, 54, () =>
            { app.Mode = (StickMode)(((int)app.Mode + 1) % 3); ClearSticks(); app.SavePreferences(); }); modeLabel = mode.GetComponentInChildren<Text>();
            Label(bottom, "WASD = 左桿  ·  方向鍵 = 右桿  ·  Space 起降  ·  R 重置  ·  C 視角", 300, 278, 940, 26, 16, muted, TextAnchor.MiddleCenter);
            BuildSettings(); BuildHelp(); RefreshRole(); UpdateSafeArea();
        }
        GameObject help;
        void BuildHelp()
        {
            var box = Group("Help", safe, true, 1050, 490); box.anchoredPosition = new Vector2(0, -160);
            Panel("Background", box, 0, 0, 1050, 490, panel);
            Label(box, "開始練習", 30, 15, 850, 55, 30, accent);
            Label(box,
                "1  選單機 → 起飛 → 使用雙搖桿；放手回中即定高／定位。\n" +
                "2  預設 Mode 2：左桿升降／轉向，右桿前後／左右。\n" +
                "3 「練習項目」依序切換自由、懸停、方形、8 字、降落。\n" +
                "4  電腦選接收手機 → 啟動接收；手機選遙控器，\n" +
                "     填電腦 Wi-Fi IPv4 與畫面六位碼 → 連線。\n" +
                "5  兩端需同一區域網路，電腦允許 UDP 47777。\n" +
                "6  斷線／手機切到背景後須重新啟動接收與配對。\n\n" +
                "練習場為自訂規則，非正式測驗評分；本程式只控制虛擬飛機。",
                30, 80, 990, 330, 22, Color.white);
            Button(box, "了解，開始飛行", 710, 422, 310, 52, () => help.SetActive(false), accent);
            help = box.gameObject; help.SetActive(false);
        }
        void BuildSettings()
        {
            var box = Group("Settings", safe, true, 1050, 430); box.anchoredPosition = new Vector2(0, -160);
            Panel("Background", box, 0, 0, 1050, 430, panel);
            Label(box, "控制設定", 30, 12, 600, 50, 30, accent);
            Button(box, "關閉", 880, 14, 140, 48, () => { settings.SetActive(false); app.SavePreferences(); });
            sensitivityLabel = Label(box, "", 30, 85, 330, 42, 22, Color.white);
            Slider(box, 390, 85, app.Sensitivity, .2f, 1, value => { app.Sensitivity = value; });
            throttleLabel = Label(box, "", 30, 153, 330, 42, 22, Color.white);
            Slider(box, 390, 153, app.ThrottleSensitivity, .2f, 1, value => { app.ThrottleSensitivity = value; });
            windLabel = Label(box, "", 30, 221, 350, 42, 22, Color.white);
            Slider(box, 390, 221, 0, 0, 6, value => { app.Drone.windSpeed = value; });
            Button(box, "定位／姿態模式切換", 30, 295, 450, 55, () =>
            { app.Drone.positionHold = !app.Drone.positionHold; app.Drone.Neutralize(); });
            Button(box, "風向旋轉 45°", 510, 295, 510, 55, () => app.Drone.windHeading = (app.Drone.windHeading + 45) % 360);
            Label(box, "風與定位設定影響本機場景；遙控器模式請在電腦調整。", 30, 366, 990, 40, 19, muted);
            settings = box.gameObject; settings.SetActive(false);
        }
        public void RefreshRole()
        {
            bool remote = app.Link.Role == RunRole.Remote;
            network.SetActive(app.Link.Role != RunRole.Local); remoteBackdrop.SetActive(remote);
            ip.gameObject.SetActive(remote); code.gameObject.SetActive(remote);
            hostAddress.gameObject.SetActive(!remote);
            networkButtonLabel.text = remote ? "連線" : "啟動接收";
            practiceButton.interactable = !remote; cameraButton.interactable = !remote; pauseButton.interactable = !remote;
            Left.gameObject.SetActive(app.Link.Role != RunRole.Host); Right.gameObject.SetActive(app.Link.Role != RunRole.Host);
        }
        public void ClearSticks() { Left?.Clear(); Right?.Clear(); }
        void CyclePractice()
        {
            if (app.Link.Role == RunRole.Remote) return;
            app.SelectPractice((PracticeKind)(((int)app.Course.Kind + 1) % 5));
        }
        void Update()
        {
            if (app == null) return;
            UpdateSafeArea();
            bool remote = app.Link.Role == RunRole.Remote;
            var t = app.Link.Telemetry;
            bool flying = remote ? t != null && t.flying : app.Drone.Flying;
            bool crashed = remote ? t != null && t.crashed : app.Drone.Crashed;
            flightState.text = app.Paused ? "已暫停" : crashed ? "碰撞 • 請重置" : flying ? "飛行中" : "待命 • 點起飛";
            string[] courses = { "自由飛行", "定點懸停", "方形航線", "8 字航線", "定點降落" };
            task.text = remote ? (t == null ? "配對電腦後即可控制；手機不傳送視訊" : t.task) : courses[(int)app.Course.Kind] + "\n" + app.Course.Instruction;
            telemetry.text = remote && t == null ? "高度 —    速度 —    航向 —" : string.Format("高度 {0:F1} m     速度 {1:F1} m/s     航向 {2:000}°\n{3}",
                remote ? t.height : app.Drone.Height, remote ? t.speed : app.Drone.Speed, remote ? t.heading : app.Drone.transform.eulerAngles.y,
                remote ? (app.Link.CommandPending ? "指令傳送中…" : "Wi-Fi 遙控") : (app.Drone.positionHold ? "定位模式" : "姿態練習 • 風漂移") + "  /  " + app.Course.Elapsed.ToString("F0") + " 秒");
            hostAddress.text = "電腦 IPv4：" + localIps;
            linkStatus.text = app.Link.Role == RunRole.Host ? "配對碼 " + (app.Link.PairCode ?? "—") + "\n" + app.Link.Status : app.Link.Status;
            // Reuse the otherwise empty address area on the host for a short label.
            networkButtonLabel.text = app.Link.Role == RunRole.Host ? "啟動接收" : "連線";
            cameraLabel.text = "視角：" + app.CameraRig.ModeName;
            pauseLabel.text = app.Paused ? "繼續" : "暫停";
            modeLabel.text = "搖桿：" + (app.Mode == StickMode.Mode2 ? "Mode 2 美國手" : app.Mode == StickMode.Mode1 ? "Mode 1 日本手" : "Mode 3 中國手");
            leftLabel.text = app.Mode == StickMode.Mode2 ? "左桿｜升降・轉向" : app.Mode == StickMode.Mode1 ? "左桿｜前後・轉向" : "左桿｜前後・左右";
            rightLabel.text = app.Mode == StickMode.Mode2 ? "右桿｜前後・左右" : app.Mode == StickMode.Mode1 ? "右桿｜升降・左右" : "右桿｜升降・轉向";
            sensitivityLabel.text = "操控靈敏度  " + app.Sensitivity.ToString("F2");
            throttleLabel.text = "升降靈敏度  " + app.ThrottleSensitivity.ToString("F2");
            windLabel.text = "風速 " + app.Drone.windSpeed.ToString("F1") + " m/s  ·  " + app.Drone.windHeading + "°";
        }
        void UpdateSafeArea()
        {
            Rect area = Screen.safeArea;
            if (area.width <= 0 || area.height <= 0) return;
            canvas.scaleFactor = Mathf.Min(area.width / 1600, area.height / 900);
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }
        RectTransform Group(string name, Transform parent, bool top, float width, float height)
        {
            var rect = Rect(name, parent, 0, 0, width, height);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, top ? 1 : 0);
            rect.pivot = new Vector2(.5f, top ? 1 : 0); rect.anchoredPosition = new Vector2(0, top ? -20 : 12); return rect;
        }
        RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        RectTransform Panel(string name, Transform parent, float x, float y, float width, float height, Color color)
        { var rect = Rect(name, parent, x, y, width, height); rect.gameObject.AddComponent<Image>().color = color; return rect; }
        Text Label(Transform parent, string text, float x, float y, float width, float height, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var label = Rect("Text", parent, x, y, width, height).gameObject.AddComponent<Text>();
            label.text = text; label.font = font; label.fontSize = size; label.color = color; label.alignment = align;
            label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        Button Button(Transform parent, string text, float x, float y, float width, float height, UnityEngine.Events.UnityAction action, Color? color = null)
        {
            var rect = Panel(text, parent, x, y, width, height, color ?? new Color(.10f, .18f, .22f));
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(action);
            Label(rect, text, 4, 0, width - 8, height, 23, color.HasValue ? panel : Color.white, TextAnchor.MiddleCenter); return button;
        }
        InputField InputBox(Transform parent, string placeholder, float x, float y, float width, string value)
        {
            var rect = Panel(placeholder, parent, x, y, width, 50, new Color(.12f, .20f, .25f));
            var input = rect.gameObject.AddComponent<InputField>(); input.targetGraphic = rect.GetComponent<Image>();
            input.textComponent = Label(rect, "", 12, 0, width - 24, 50, 22, Color.white);
            input.placeholder = Label(rect, placeholder, 12, 0, width - 24, 50, 19, muted);
            input.text = value; input.characterLimit = 64; input.lineType = InputField.LineType.SingleLine; return input;
        }
        void Slider(Transform parent, float x, float y, float value, float min, float max, UnityEngine.Events.UnityAction<float> changed)
        {
            var rect = Panel("Slider", parent, x, y, 620, 42, new Color(.12f, .2f, .25f));
            var slider = rect.gameObject.AddComponent<Slider>();
            var area = Rect("Handle area", rect, 20, 0, 580, 42);
            var handle = Panel("Handle", area, 0, 0, 36, 42, accent); handle.pivot = new Vector2(.5f, .5f);
            slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight; slider.minValue = min; slider.maxValue = max;
            slider.value = value; slider.onValueChanged.AddListener(changed);
        }
        TouchStick Stick(Transform parent, float x, float y)
        {
            var rect = Panel("Touch stick", parent, x, y, 250, 250, new Color(.05f, .12f, .16f, .82f));
            rect.GetComponent<Image>().sprite = circle;
            Label(rect, "+", 0, 0, 250, 250, 90, new Color(.4f, .6f, .65f, .35f), TextAnchor.MiddleCenter);
            var knob = Panel("Thumb", rect, 0, 0, 86, 86, accent);
            knob.anchorMin = knob.anchorMax = new Vector2(.5f, .5f); knob.pivot = new Vector2(.5f, .5f); knob.anchoredPosition = Vector2.zero;
            var img = knob.GetComponent<Image>(); img.sprite = circle; img.raycastTarget = false;
            var stick = rect.gameObject.AddComponent<TouchStick>(); stick.knob = knob; return stick;
        }
        static Sprite MakeCircle()
        {
            const int size = 128; var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            { float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(64, 64)); pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(64 - d)); }
            texture.SetPixels(pixels); texture.Apply(); return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }
    }
}
