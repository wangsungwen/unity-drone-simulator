using System.Collections.Generic;
using UnityEngine;
using DroneTrainer.Core;

namespace DroneTrainer
{
    public enum PracticeKind { Free, Hover, Square, FigureEight, Landing }
    public sealed class PracticeCourse : MonoBehaviour
    {
        public DroneMotor drone;
        public PracticeKind Kind { get; private set; }
        public string Instruction { get; private set; }
        public float Elapsed { get; private set; }
        public int Score { get; private set; } = 100;
        public bool Finished { get; private set; }
        public bool Paused;
        readonly List<Vector3> points = new List<Vector3>();
        readonly HoldTimer hold = new HoldTimer();
        LineRenderer route;
        Transform target;
        int step;
        float outsideTime;
        bool started;
        public void Configure(PracticeKind kind)
        {
            Kind = kind; Finished = false; started = false; Elapsed = 0; Score = 100;
            step = 0; outsideTime = 0; hold.Reset(); points.Clear();
            if (kind == PracticeKind.Hover) points.Add(new Vector3(0, 3.32f, 0));
            if (kind == PracticeKind.Square)
            {
                points.Add(new Vector3(0, 3.32f, 0)); points.Add(new Vector3(0, 3.32f, 10));
                points.Add(new Vector3(10, 3.32f, 10)); points.Add(new Vector3(10, 3.32f, 0)); points.Add(new Vector3(0, 3.32f, 0));
            }
            if (kind == PracticeKind.FigureEight)
                for (int i = 0; i <= 24; i++)
                {
                    float t = i * Mathf.PI * 2 / 24;
                    points.Add(new Vector3(8 * Mathf.Sin(t), 3.32f, 5 * Mathf.Sin(t * 2)));
                }
            if (kind == PracticeKind.Landing)
            { points.Add(new Vector3(0, 3.32f, 0)); points.Add(new Vector3(8, 3.32f, 8)); points.Add(new Vector3(8, .32f, 8)); }
            if (route == null)
            {
                route = new GameObject("Practice route").AddComponent<LineRenderer>();
                route.material = SceneFactory.MakeMaterial(new Color(.15f, .8f, .88f), true);
                route.widthMultiplier = .06f; route.useWorldSpace = true;
                target = SceneFactory.Shape("Current target", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .35f,
                    new Color(1, .72f, .2f), false).transform;
            }
            route.positionCount = points.Count; route.SetPositions(points.ToArray());
            target.gameObject.SetActive(points.Count > 0);
            if (points.Count > 0) target.position = points[0];
            Instruction = kind == PracticeKind.Free ? "自由飛行 • 起飛後用雙搖桿練習，無時間限制" : "按「起飛」，依序抵達金色目標球";
        }
        void Update()
        {
            if (drone == null || Paused || Kind == PracticeKind.Free || Finished) return;
            if (drone.Crashed) { Finish(false, "碰撞或離開場域"); return; }
            if (!started) { if (!drone.Flying) return; started = true; }
            Elapsed += Time.deltaTime;
            if (Elapsed >= 240) { Finish(false, "練習超時"); return; }
            float distance = Vector3.Distance(drone.transform.position, points[step]);
            bool landing = Kind == PracticeKind.Landing && step == points.Count - 1;
            float duration = Kind == PracticeKind.Hover ? 10 : Kind == PracticeKind.FigureEight ? .15f : 2;
            bool inside = distance <= (landing ? .8f : 1) && (Kind == PracticeKind.FigureEight || drone.Speed < .7f);
            if (landing) inside &= !drone.Flying && drone.Height < .15f;
            if (hold.Step(inside, Time.deltaTime, duration))
            {
                step++; hold.Reset();
                if (step >= points.Count) { Finish(true, "練習完成"); return; }
                target.position = points[step];
            }
            // Penalize time outside the practice envelope, not normal travel between targets.
            bool outside = drone.Height > 10 || Mathf.Abs(drone.transform.position.x) > 20 || Mathf.Abs(drone.transform.position.z) > 20;
            outsideTime += outside ? Time.deltaTime : 0;
            Score = Mathf.Max(0, 100 - Mathf.FloorToInt(outsideTime) * 2);
            Instruction = string.Format("目標 {0}/{1} • 距離 {2:F1} m • 穩定 {3:F1}/{4:F1} s{5}",
                step + 1, points.Count, Vector3.Distance(drone.transform.position, points[step]), hold.Elapsed, duration,
                landing ? " • 移到降落點上方後按降落" : "");
        }
        void Finish(bool success, string reason)
        {
            Finished = true; if (!success) Score = 0;
            Instruction = reason + " • " + Score + " 分 • " + Elapsed.ToString("F1") + " 秒（自訂練習評分）";
            target.gameObject.SetActive(false);
            int best = PlayerPrefs.GetInt("best." + Kind, 0);
            if (success && Score > best) { PlayerPrefs.SetInt("best." + Kind, Score); PlayerPrefs.Save(); }
        }
    }
}
