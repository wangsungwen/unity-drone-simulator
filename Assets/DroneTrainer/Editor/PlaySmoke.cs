using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using DroneTrainer.Core;

namespace DroneTrainer.Editor
{
    // Run with graphics enabled: -batchmode -projectPath ... -executeMethod DroneTrainer.Editor.PlaySmoke.Run
    // Do not pass -quit; this harness exits after testing. Supports manual menu invocation too.
    [InitializeOnLoad]
    public static class PlaySmoke
    {
        const string Active = "DroneTrainer.SmokeActive";
        static int phase;
        static double since;
        static TrainerApp app;
        static RemoteLink client;
        static Vector3 initial;
        static string runtimeError;
        static readonly List<string> results = new List<string>();
        static PlaySmoke()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Active, false)) return;
                phase = 0; since = EditorApplication.timeSinceStartup; results.Clear();
                runtimeError = null; Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
                EditorApplication.update -= Tick; EditorApplication.update += Tick;
            };
        }
        [MenuItem("Drone Trainer/Tests/Play smoke (about 25 seconds)")]
        public static void Run()
        {
            TrainerBuild.Configure(); TrainerBuild.OpenScene();
            SessionState.SetBool(Active, true); EditorApplication.isPlaying = true;
        }
        static void Check(bool condition, string name)
        { if (!condition) throw new Exception(name); results.Add("PASS: " + name); }
        static void Next() { phase++; since = EditorApplication.timeSinceStartup; }
        static void Tick()
        {
            try
            {
                double elapsed = EditorApplication.timeSinceStartup - since;
                if (runtimeError != null) throw new Exception("Runtime log error: " + runtimeError);
                if (elapsed > 12) throw new Exception("Timeout in smoke phase " + phase);
                if (phase == 0 && elapsed > 1)
                {
                    app = UnityEngine.Object.FindObjectOfType<TrainerApp>();
                    Check(app != null && app.Hud != null && app.Drone != null, "scene boots with drone and HUD");
                    Check(app.Link.Role == RunRole.Local && app.CameraRig.GetComponent<Camera>().enabled && app.Hud.Left.gameObject.activeSelf, "local mode renders local flight with sticks without pairing");
                    TestTouchSticks();
                    app.SetRole(RunRole.Host); app.Link.Listen();
                    client = new GameObject("Smoke remote").AddComponent<RemoteLink>();
                    client.Connect("127.0.0.1", app.Link.PairCode); Next();
                }
                else if (phase == 1 && client.Connected && app.Link.Connected)
                { Check(true, "UDP loopback pairs"); client.Request("takeoff"); Next(); }
                else if (phase == 2 && elapsed > 4)
                {
                    Check(app.Drone.Flying && app.Drone.Height > 1.2f, "remote takeoff climbs");
                    Check(client.Telemetry != null && client.Telemetry.height > 1.1f, "telemetry roundtrip");
                    initial = app.Drone.transform.position;
                    client.OutgoingInput = new FlightInput { pitch = .6f }; Next();
                }
                else if (phase == 3 && elapsed > 2)
                {
                    Check(app.Drone.transform.position.z > initial.z + 2, "remote pitch moves aircraft forward");
                    client.OutgoingInput = default(FlightInput); Next();
                }
                else if (phase == 4 && elapsed > 3)
                {
                    Check(app.Drone.Speed < .8f, "neutral input brakes and holds");
                    client.OutgoingInput = new FlightInput { yaw = .7f }; Next();
                }
                else if (phase == 5 && elapsed > 1)
                {
                    Check(app.Drone.transform.eulerAngles.y > 30, "remote yaw rotates aircraft");
                    client.enabled = false; Next();
                }
                else if (phase == 6 && elapsed > 1)
                {
                    Check(!app.Link.Connected && app.Link.ReceivedInput.yaw == 0, "lost heartbeat revokes control");
                    Check(app.Drone.Speed < .1f, "link loss neutralizes simulated vehicle");
                    client.Close(); app.SetRole(RunRole.Local);
                    app.Command("takeoff"); Next();
                }
                else if (phase == 7 && elapsed > 4)
                {
                    Directory.CreateDirectory("TestResults");
                    Capture("TestResults/desktop-scene.png");
                    app.Command("land"); Next();
                }
                else if (phase == 8 && elapsed > 6)
                {
                    Check(!app.Drone.Flying && app.Drone.Height < .2f, "automatic landing finishes");
                    app.Command("reset"); Check(!app.Drone.Crashed && app.Drone.Height < .05f, "reset restores home");
                    app.SetRole(RunRole.Remote); Next();
                }
                else if (phase == 9 && elapsed > .5)
                {
                    Check(app.Link.Role == RunRole.Remote && !app.CameraRig.GetComponent<Camera>().enabled && app.Hud.Left.gameObject.activeSelf, "remote mode presents sticks and disables local scene rendering");
                    Capture("TestResults/phone-remote.png");
                    app.SetRole(RunRole.Host); app.Link.Listen(); Next();
                }
                else if (phase == 10 && elapsed > .5)
                {
                    Check(app.CameraRig.GetComponent<Camera>().enabled && !app.Hud.Left.gameObject.activeSelf, "host mode renders flight without local sticks");
                    Capture("TestResults/desktop-host.png");
                    ExamCourseChecks.Run(app, Check);
                    app.Hud.OpenPracticeMenu(); Next();
                }
                else if (phase == 11 && elapsed > .5)
                {
                    Capture("TestResults/exam-menu.png");
                    app.SetRole(RunRole.Local); app.SelectProfile(ExamProfile.BasicI); app.SelectPractice(PracticeKind.FigureEight); Next();
                }
                else if (phase == 12 && elapsed > .5)
                {
                    Capture("TestResults/exam-eight.png");
                    CheckOverview();
                    Capture("TestResults/phone-local-wide.png",1950,900);
                    Capture("TestResults/tablet-local.png",1200,900);
                    app.CameraRig.SetMode(1); app.Command("takeoff"); Next();
                }
                else if (phase == 13 && elapsed > 4)
                {
                    Check(Mathf.Abs(app.CameraRig.transform.position.y-1.7f)<.01f && Mathf.Abs(app.CameraRig.transform.position.y-app.Drone.transform.position.y)<.3f,"eye-level camera is approximately level with normal hover");
                    Capture("TestResults/operator-eye.png");
                    Finish(true, null);
                }
            }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        static void OnLog(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = condition + "\n" + trace; }
        static void Capture(string path, int width=1600, int height=900)
        {
            // Explicit render works in batch-mode, where Game View never presents a frame.
            var camera = app.CameraRig.GetComponent<Camera>();
            var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            var target = new RenderTexture(width, height, 24);
            var previous = RenderTexture.active;
            var priorTarget = camera.targetTexture;
            bool priorEnabled = camera.enabled;
            var priorMode = canvas.renderMode;
            var priorScale = canvas.scaleFactor;
            camera.enabled = true; camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
            canvas.planeDistance = 1; canvas.scaleFactor = Mathf.Min(width/1600f,height/900f);
            camera.SendMessage("LateUpdate");
            // Dynamic CJK glyphs can rebuild the font atlas during the first canvas pass.
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var label in canvas.GetComponentsInChildren<UnityEngine.UI.Text>())
                { label.font.RequestCharactersInTexture(label.text, label.fontSize, label.fontStyle); label.SetAllDirty(); }
                Canvas.ForceUpdateCanvases(); camera.Render();
            }
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            var layout = new List<string>();
            foreach (var label in canvas.GetComponentsInChildren<UnityEngine.UI.Text>())
                layout.Add(label.text.Replace("\n", " / ") + " | rect=" + label.rectTransform.rect + " | verts=" + label.cachedTextGenerator.vertexCount + " | cull=" + label.canvasRenderer.cull + " | alpha=" + label.canvasRenderer.GetAlpha());
            File.WriteAllLines(path + ".layout.txt", layout);
            RenderTexture.active = previous; camera.targetTexture = priorTarget; camera.enabled = priorEnabled;
            canvas.renderMode = priorMode; canvas.scaleFactor = priorScale;
            UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(target);
        }
        static void CheckOverview()
        {
            foreach(var profile in new[] { ExamProfile.Under2kg, ExamProfile.BasicI, ExamProfile.BasicIII })
            {
                app.SelectProfile(profile); app.SelectPractice(profile == ExamProfile.Under2kg ? PracticeKind.Square : PracticeKind.Circuit);
                app.CameraRig.SetMode(0); app.CameraRig.SendMessage("LateUpdate");
                Bounds bounds=app.CameraRig.CourseBounds;
                var camera=app.CameraRig.GetComponent<Camera>(); bool inside=true;
                for(int i=0;i<8;i++)
                {
                    Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    Vector3 view=camera.WorldToViewportPoint(corner);
                    inside &= view.z>0 && view.x>.05f && view.x<.95f && view.y>.26f && view.y<.74f;
                }
                Check(inside,"overview fits entire course in unobstructed center band: "+profile);
            }
            app.SelectProfile(ExamProfile.BasicI); app.SelectPractice(PracticeKind.FigureEight);
            app.CameraRig.SendMessage("LateUpdate");
        }
        static PointerEventData Pointer(TouchStick stick, int id, Vector2 direction)
        {
            var rect = (RectTransform)stick.transform;
            Vector3 world = rect.TransformPoint(rect.rect.center + direction * rect.rect.width * .36f);
            return new PointerEventData(EventSystem.current) { pointerId = id, position = RectTransformUtility.WorldToScreenPoint(null, world) };
        }
        static void TestTouchSticks()
        {
            var left = app.Hud.Left; var right = app.Hud.Right;
            left.OnPointerDown(Pointer(left, 11, Vector2.zero));
            Check(left.Value.magnitude < .01f, "touch center produces zero");
            left.OnDrag(Pointer(left, 11, Vector2.right));
            right.OnPointerDown(Pointer(right, 12, Vector2.up));
            Check(left.Value.x > .99f && right.Value.y > .99f, "two pointers operate independently");
            left.OnPointerUp(Pointer(left, 12, Vector2.zero));
            Check(left.Value.x > .99f, "other pointer cannot release held stick");
            left.OnPointerUp(Pointer(left, 11, Vector2.zero));
            Check(left.Value == Vector2.zero && right.Value.y > .99f, "releasing one stick preserves the other");
            app.Hud.ClearSticks();
        }
        static void Finish(bool success, string error)
        {
            EditorApplication.update -= Tick; SessionState.SetBool(Active, false);
            Application.logMessageReceived -= OnLog;
            Directory.CreateDirectory("TestResults");
            if (!success) results.Add("FAIL: " + error);
            File.WriteAllLines("TestResults/play-smoke.txt", results);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else { EditorApplication.isPlaying = false; Debug.Log(string.Join("\n", results)); }
        }
    }
}
