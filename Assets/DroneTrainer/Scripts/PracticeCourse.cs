using System.Collections.Generic;
using UnityEngine;
using DroneTrainer.Core;

namespace DroneTrainer
{
    public enum PracticeKind { Free, Hover, Square, FigureEight, Landing, Sideways, Circuit, Emergency, Preflight, Postflight, MissionBrief, EmergencyOral }
    public enum ExamProfile { Under2kg, BasicI, BasicII, BasicIII }
    public sealed class PracticeCourse : MonoBehaviour
    {
        public DroneMotor drone;
        public PracticeKind Kind { get; private set; }
        public ExamProfile Profile { get; private set; } = ExamProfile.Under2kg;
        public string Instruction { get; private set; }
        public float Elapsed { get; private set; }
        public float DeviationSeconds { get; private set; }
        public int Score { get; private set; } = 100;
        public bool Finished { get; private set; }
        public bool Paused;
        public bool IsProcedure { get { return Kind >= PracticeKind.Preflight; } }
        public int StepIndex { get { return step; } }
        public int StepCount { get { return stages.Count; } }
        public float Scale { get { return ExamRules.Scale((int)Profile); } }
        public static readonly string[] Names = { "自由飛行", "定點起降及四面停懸", "矩形航線（雙向）", "8 字水平圓", "定點精準降落", "側面懸停及前進、後退", "高度保持五邊飛行", "緊急處置：手控返場", "飛行前檢查", "飛行後檢查", "設定飛行任務：程序複習", "緊急處置：口述複習" };
        public static readonly string[] ProfileNames = { "基本級未達 2 公斤", "基本級 I（未達 25 公斤）", "基本級 II（25–150 公斤）", "基本級 III（150 公斤以上）" };
        public PracticeKind[] Available { get { return Profile == ExamProfile.Under2kg
            ? new[] { PracticeKind.Free, PracticeKind.Preflight, PracticeKind.MissionBrief, PracticeKind.Hover, PracticeKind.Square, PracticeKind.Landing, PracticeKind.EmergencyOral, PracticeKind.Postflight }
            : new[] { PracticeKind.Free, PracticeKind.Preflight, PracticeKind.Hover, PracticeKind.FigureEight, PracticeKind.Sideways, PracticeKind.Circuit, PracticeKind.Emergency, PracticeKind.Postflight }; } }
        public sealed class Stage
        {
            public Vector3 point;
            public float heading, seconds;
            public bool landing, moving, clockwise;
            public string label;
        }
        readonly List<Stage> stages = new List<Stage>();
        readonly HoldTimer hold = new HoldTimer();
        LineRenderer route;
        Transform target, headingArrow;
        GameObject markings;
        int step, card;
        bool started, emergencyTriggered;
        float priorHeading, turnTotal, emergencyAt;
        string[] cards;
        Vector3 previousPoint;
        public Stage CurrentStage { get { return step < stages.Count ? stages[step] : null; } }
        public void SetProfile(ExamProfile profile) { Profile = profile; Configure(PracticeKind.Free); }
        void Add(string label, Vector3 p, float heading = 0, float seconds = 5, bool landing = false, bool moving = false, bool clockwise = false)
        { stages.Add(new Stage { label = label, point = p, heading = heading, seconds = seconds, landing = landing, moving = moving, clockwise = clockwise }); }
        Vector3 P(float x, float z, float height = ExamRules.LowHeight) { return new Vector3(x, height + DroneMotor.GroundHeight, z); }
        void Travel(string label, Vector3 end, float heading, bool moving = true)
        {
            Vector3 start = stages[stages.Count - 1].point;
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start, end) / 1.2f));
            for (int i = 1; i <= count; i++) Add(label, Vector3.Lerp(start, end, (float)i / count), heading, 0, false, moving);
        }
        public void Configure(PracticeKind kind)
        {
            Kind = kind; Finished = false; started = false; emergencyTriggered = false; Elapsed = 0; DeviationSeconds = 0; Score = 100;
            step = 0; card = 0; turnTotal = 0; priorHeading = 0; hold.Reset(); stages.Clear();
            emergencyAt = Random.Range(28f, 42f);
            previousPoint = P(0, 0, 0);
            float s = Scale;
            if (kind == PracticeKind.Hover)
            {
                Add("機頭朝外，穩定停懸", P(0, 0));
                for (int i = 1; i <= 4; i++) Add("順時針轉 90°，穩定停懸", P(0, 0), i * 90 % 360, 5, false, false, true);
                Add("機頭朝外，降落 H；起落架須在圈內", P(0, 0, 0), 0, .5f, true);
            }
            if (kind == PracticeKind.Square)
            {
                Add("H 朝外停懸", P(0, 0));
                Vector3[] corners = { P(-6,0), P(-6,5), P(6,5), P(6,0), P(0,0), P(6,0), P(6,5), P(-6,5), P(-6,0), P(0,0) };
                string[] labels = { "P1", "P2", "P3", "P4", "H：反向起點", "P4", "P3", "P2", "P1", "H" };
                for (int item = 0; item < corners.Length; item++)
                {
                    Vector3 delta = corners[item] - stages[stages.Count - 1].point;
                    float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                    Travel("機頭朝航向 → " + labels[item], corners[item], yaw);
                    if (item != 4 && item != 9) Add(labels[item] + " 穩定停懸", corners[item], yaw);
                }
                Add("H 機頭轉朝外；口誦結束", P(0,0));
            }
            if (kind == PracticeKind.FigureEight)
            {
                Add("H 朝外停懸", P(0,0)); Travel("前進 P4", P(0,6*s), 0);
                for (int side = 0; side < 2; side++)
                    for (int i = 1; i <= 72; i++)
                    {
                        float angle = i * Mathf.PI * 2 / 72;
                        float x = side == 0 ? -6*s + 6*s*Mathf.Cos(angle) : 6*s - 6*s*Mathf.Cos(angle);
                        Add(side == 0 ? "左圈逆時針 P4→P1→P2→P3→P4" : "右圈順時針 P4→P5→P6→P7→P4",
                            P(x,6*s+6*s*Mathf.Sin(angle)), side == 0 ? -angle*Mathf.Rad2Deg : angle*Mathf.Rad2Deg, 0, false, true);
                    }
                Travel("機頭朝外，後退返回 H", P(0,0), 0); Add("H 等高停懸；口誦結束", P(0,0));
            }
            if (kind == PracticeKind.Sideways)
            {
                Add("H 轉機頭朝左", P(0,0), 270);
                foreach (float yaw in new[] { 270f, 90f })
                {
                    float left = yaw == 270 ? -6*s : 6*s;
                    if (yaw == 90) Add("H 轉機頭朝右", P(0,0), yaw);
                    foreach (float x in new[] { left, -left, 0f })
                    { Travel("保持側向機頭，前進／後退", P(x,0), yaw); Add("側面定點停懸", P(x,0), yaw); }
                }
                Add("H 轉機頭朝外；口誦結束", P(0,0));
            }
            if (kind == PracticeKind.Circuit || kind == PracticeKind.Emergency)
            {
                // windHeading is the direction wind travels. Start toward the wind, along P3–P7.
                float sign = drone == null || Mathf.Sin(drone.windHeading * Mathf.Deg2Rad) >= 0 ? -1 : 1;
                Add("H 朝外停懸；五邊約 20 m 高", P(0,0));
                Travel("爬升至 20 m", P(0,0,20), 0, false);
                Vector3[] corners = { P(sign*40*s,0,20), P(sign*40*s,20*s,20), P(-sign*40*s,20*s,20), P(-sign*40*s,0,20), P(0,0,20) };
                for (int i = 0; i < corners.Length; i++)
                {
                    Vector3 delta = corners[i] - stages[stages.Count - 1].point;
                    Travel("第 " + (i+1) + " 邊：機頭朝航向", corners[i], Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg);
                }
                Travel("H 上方下降至 1–2 m", P(0,0), 0, false); Add("H 朝外停懸；口誦結束", P(0,0));
            }
            if (kind == PracticeKind.Landing)
            { Add("H 朝外停懸", P(0,0)); Add("降落 H；起落架須在圈內", P(0,0,0), 0, .5f, true); }
            cards = ProcedureCards(kind);
            if (route == null)
            {
                route = new GameObject("Practice route").AddComponent<LineRenderer>();
                route.material = SceneFactory.MakeMaterial(new Color(.15f,.8f,.88f),true); route.widthMultiplier = .055f;
                target = SceneFactory.Shape("Current target",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.25f,new Color(1,.72f,.2f),false).transform;
                headingArrow = new GameObject("Target heading").transform;
                var arrow = SceneFactory.Shape("Heading arrow",PrimitiveType.Cube,Vector3.zero,new Vector3(.10f,.08f,.7f),new Color(1,.4f,.1f),false);
                arrow.transform.SetParent(headingArrow,false); arrow.transform.localPosition = Vector3.forward*.4f;
            }
            if (markings != null) { markings.SetActive(false); Destroy(markings); }
            markings = ExamField.Build(Profile, kind);
            RefreshRoute();
            Instruction = IsProcedure ? cards[0] : kind == PracticeKind.Free ? "自由飛行 • 無時間限制" : "口誦 3、2、1、Go → 起飛；依金色目標及橘色航向引導";
        }
        void RefreshRoute()
        {
            route.positionCount = stages.Count;
            for (int i = 0; i < stages.Count; i++) route.SetPosition(i, stages[i].point);
            target.gameObject.SetActive(stages.Count > 0); headingArrow.gameObject.SetActive(stages.Count > 0);
            RefreshTarget();
        }
        void RefreshTarget()
        {
            if (CurrentStage == null) return;
            target.position = CurrentStage.point;
            headingArrow.position = CurrentStage.point; headingArrow.rotation = Quaternion.Euler(0,CurrentStage.heading,0);
        }
        public void ConfirmProcedure()
        {
            if (!IsProcedure || Finished || Paused) return;
            card++;
            if (card >= cards.Length) { Finish(true,"程序複習完成（自我確認，未作實機檢查／口試評定）"); return; }
            Instruction = cards[card];
        }
        static string[] ProcedureCards(PracticeKind kind)
        {
            if (kind == PracticeKind.Preflight) return new[] {
                "1/4 系統知識：依應考機型口述型式、總重、酬載、重心及槳葉規格。完成後按確認。",
                "2/4 口述動力、電池、遙控頻率及滯空時間；實機規格須查機型手冊。",
                "3/4 檢查搖桿方向、模式與開關功能；確認控制鏈路。",
                "4/4 依自備檢查表逆時針進行 360° 檢查：機體、槳葉、動力、電池、飛控與酬載；按確認結束複習。" };
            if (kind == PracticeKind.Postflight) return new[] {
                "1/2 安全落地後，依機型程序停止動力及通斷電。",
                "2/2 按檢查表逆時針完成 360° 飛行後檢查，記錄損傷與異常；按確認結束複習。" };
            if (kind == PracticeKind.MissionBrief) return new[] {
                "1/3 任務設定程序複習：依選定圖卡規劃航點位置、高度與速度。本頁不執行任務上傳。",
                "2/3 口述歸航點位置、高度及適用的地理圍欄；檢查航線與規劃區域。",
                "3/3 確認控制鏈路與任務上傳結果，口誦結束；定位模式註記之免測依正式規範。" };
            return new[] {
                "必考 1：口述動力／電力異常時，如何判斷高度、距離、迫降航線與安全落點。",
                "必考 2：分別口述姿態儀、電子羅盤、GNSS 異常的辨識、模式選擇與安全處置。",
                "擇一複習：影像鏈路、慣性導航異常或飛安事件通報。依機型程序與現行規範作答。",
                "自我核對：參閱 AC 107-005D 附錄八及機型手冊；此處不自動判定口述答案正誤。" };
        }
        void Update() { Advance(Time.deltaTime); }
        public void Advance(float dt)
        {
            if (drone == null || Paused || Kind == PracticeKind.Free || Finished || IsProcedure || dt <= 0) return;
            if (drone.Crashed) { Finish(false,"碰撞或離開模擬場域"); return; }
            if (!started) { if (!drone.Flying) return; started = true; priorHeading = drone.transform.eulerAngles.y; }
            Elapsed += dt;
            Vector3 pos = drone.transform.position;
            float operatorZ = Profile == ExamProfile.Under2kg ? -2.25f : -7*Scale;
            if (pos.z < operatorZ) { Finish(false,"越過操作員後方，停止本次練習"); return; }
            if (Kind == PracticeKind.Emergency && !emergencyTriggered && Elapsed >= emergencyAt && drone.Height > 19 && CurrentStage != null && CurrentStage.moving)
                TriggerEmergency();
            Stage stage = CurrentStage;
            if (stage == null) return;
            float yaw = drone.transform.eulerAngles.y;
            turnTotal += Mathf.DeltaAngle(priorHeading,yaw); priorHeading = yaw;
            float hError = Mathf.Abs(Mathf.DeltaAngle(yaw,stage.heading));
            float horizontal = Vector2.Distance(new Vector2(pos.x,pos.z),new Vector2(stage.point.x,stage.point.z));
            float altitude = Mathf.Abs(pos.y-stage.point.y);
            bool inside;
            if (stage.landing)
            {
                float radius = Kind == PracticeKind.Emergency ? 4*Scale : (Profile == ExamProfile.Under2kg ? 1 : Scale);
                inside = !drone.Flying && drone.Height < .12f && drone.Speed < .25f &&
                    ExamRules.GearInside(pos.x,pos.z,yaw,stage.point.x,stage.point.z,radius) && (Kind == PracticeKind.Emergency || hError <= 15);
                if (!drone.Flying && !inside) { Finish(false,"落地位置／航向不符：起落架須完整落在指定圈內"); return; }
            }
            else if (stage.seconds > 0)
                inside = drone.Flying && ExamRules.Stable(horizontal,drone.Height,drone.Speed,hError) && (!stage.clockwise || turnTotal >= 75 && turnTotal <= 105);
            else inside = drone.Flying && horizontal < .75f && altitude < .55f && (emergencyTriggered || hError <= 25) && (!stage.moving || drone.Speed > .15f) && (!emergencyTriggered || step != 0 || drone.Speed < .5f);
            if (!drone.Flying && !stage.landing) { Finish(false,"尚未到降落階段即落地，請重置重練"); return; }
            // Track deviation along the active segment; normal travel is not a hover error.
            Vector3 segment = stage.point-previousPoint;
            float t = segment.sqrMagnitude < .001f ? 1 : Mathf.Clamp01(Vector3.Dot(pos-previousPoint,segment)/segment.sqrMagnitude);
            float routeError = Vector3.Distance(pos,previousPoint+segment*t);
            bool deviation = routeError > (Kind == PracticeKind.Circuit ? 2 : 1) || (!emergencyTriggered && drone.Speed > .5f && hError > 25) ||
                (Kind == PracticeKind.FigureEight && stage.moving && drone.Speed < .15f);
            if (deviation) DeviationSeconds += dt;
            if (hold.Step(inside,dt,Mathf.Max(.001f,stage.seconds)))
            {
                previousPoint = stage.point; step++; hold.Reset(); turnTotal = 0;
                if (step >= stages.Count) { Finish(true,"動作流程完成；請口誦結束"); return; }
                RefreshTarget(); stage = CurrentStage;
            }
            Instruction = stage.label + string.Format("\n目標高 {0:F1} m / 航向 {1:000}° · 停懸 {2:F1}/{3:F0}s · 偏差累計 {4:F1}s",
                stage.point.y-DroneMotor.GroundHeight,Mathf.Repeat(stage.heading,360),hold.Elapsed,stage.seconds,DeviationSeconds);
        }
        public void TriggerEmergency()
        {
            if (Kind != PracticeKind.Emergency || emergencyTriggered || !drone.Flying || Finished) return;
            emergencyTriggered = true;
            Vector3 pos = drone.transform.position;
            float x = pos.x <= 0 ? -6*Scale : 6*Scale;
            stages.Clear(); step = 0; hold.Reset(); previousPoint = pos;
            // No mandated five-second dwell or heading constraint after the emergency command (005D).
            Add("緊急返航！口誦 3、2、1、Go；先原處穩定",pos,drone.transform.eulerAngles.y,.5f);
            // Emergency stabilization is handled as a short low-speed capture at any altitude.
            stages[0].seconds = 0; stages[0].moving = false;
            Add("手控下滑至較近側內圈 1–2 m；自由選擇安全進場航線",P(x,6*Scale),0,0);
            Add("手控降落較近側八字內圈；不得壓線",P(x,6*Scale,0),0,.5f,true);
            RefreshRoute();
        }
        void Finish(bool success,string reason)
        {
            Finished = true; Score = success ? Mathf.Max(0,100-Mathf.FloorToInt(DeviationSeconds)) : 0;
            Instruction = reason + "\n" + Elapsed.ToString("F1") + " 秒 · 偏差 " + DeviationSeconds.ToString("F1") + " 秒（訓練回饋，非正式合格判定）";
            target.gameObject.SetActive(false); headingArrow.gameObject.SetActive(false);
        }
    }
}
