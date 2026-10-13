using System;
using DroneTrainer.Core;

public static class CoreTests
{
    static int count;
    static void Check(bool condition, string name)
    { if (!condition) throw new Exception("FAIL: " + name); count++; }
    static bool Near(float a, float b) { return Math.Abs(a - b) < .0001f; }
    public static string Run()
    {
        count = 0;
        Check(ControlLogic.Axis(.04f, .08f, 1) == 0, "deadzone rejects small noise");
        Check(ControlLogic.Axis(-.08f, .08f, 1) == 0, "deadzone negative boundary");
        Check(Near(ControlLogic.Axis(.54f, .08f, 1), .5f), "deadzone rescales remaining range");
        Check(Near(ControlLogic.Axis(-2, .08f, .7f), -.7f), "negative input clamps and scales");
        Check(ControlLogic.Axis(float.NaN, .08f, 1) == 0, "NaN input rejected");
        Check(ControlLogic.Axis(float.PositiveInfinity, .08f, 1) == 0, "infinite input rejected");
        Check(Near(ControlLogic.Axis(1, 1, 1), 1), "deadzone cannot divide by zero");
        Check(ControlLogic.Axis(1, .08f, -1) == 0, "invalid negative sensitivity clamps");
        var m2 = ControlLogic.Map(.5f, 1, -.5f, -1, StickMode.Mode2, 1, .7f);
        Check(Near(m2.throttle, .7f) && Near(m2.pitch, -1) && m2.yaw > 0 && m2.roll < 0, "mode2 channels");
        var m1 = ControlLogic.Map(.5f, 1, -.5f, -1, StickMode.Mode1, 1, .7f);
        Check(Near(m1.throttle, -.7f) && Near(m1.pitch, 1) && m1.yaw > 0 && m1.roll < 0, "mode1 channels");
        var m3 = ControlLogic.Map(.5f, 1, -.5f, -1, StickMode.Mode3, 1, .7f);
        Check(Near(m3.throttle, -.7f) && Near(m3.pitch, 1) && m3.yaw < 0 && m3.roll > 0, "mode3 channels");
        Check(default(FlightInput).IsValid, "neutral packet valid");
        Check(!new FlightInput { yaw = float.NaN }.IsValid, "NaN network channel invalid");
        Check(!new FlightInput { pitch = float.NegativeInfinity }.IsValid, "infinite network channel invalid");
        Check(!new FlightInput { roll = 1.1f }.IsValid, "out of range packet invalid");
        var gate = new PeerGate(); var neutral = default(FlightInput);
        Check(!gate.Accept("a", 1, neutral, 0), "closed peer rejects input");
        gate.Open("a", 10);
        Check(!gate.Accept("b", 1, neutral, 10.1), "wrong session rejected");
        Check(gate.LastSeen == 10, "wrong session cannot refresh watchdog");
        Check(gate.Accept("a", 1, neutral, 10.1), "first frame accepted");
        Check(!gate.Accept("a", 1, neutral, 10.2), "duplicate rejected");
        Check(!gate.Accept("a", 0, neutral, 10.2), "out of order rejected");
        Check(!gate.Accept("a", 2, new FlightInput { throttle = 2 }, 10.2), "malformed frame rejected");
        Check(gate.LastSequence == 1 && gate.LastSeen == 10.1, "invalid frames cannot advance state");
        Check(!gate.Expired(10.69) && gate.Expired(10.71), "watchdog interval");
        Check(!gate.Accept("a", 2, neutral, 10.71), "late frame cannot revive expired session");
        Check(gate.Action(1) && !gate.Action(1) && !gate.Action(0), "action processed exactly once");
        Check(gate.Action(3) && !gate.Action(2), "old action rejected");
        gate.Close(); Check(gate.Session == null && !gate.Accept("a", 99, neutral, 11), "close revokes session");
        gate.Open("b", 12);
        Check(!gate.Accept("a", 100, neutral, 12.1) && gate.Accept("b", 1, neutral, 12.1), "new connection rejects replay from old session");
        Check(gate.Action(1), "new session resets action sequence");
        var hold = new HoldTimer();
        Check(!hold.Step(true, .4f, 1) && !hold.Step(true, .4f, 1), "hold accumulates below threshold");
        Check(!hold.Step(false, .1f, 1) && hold.Elapsed == 0, "leaving target resets hold");
        Check(!hold.Step(true, .6f, 1) && hold.Step(true, .4f, 1), "continuous hold succeeds");
        hold.Reset(); Check(hold.Elapsed == 0, "explicit hold reset");
        Check(!hold.Step(true, -1, 1) && hold.Elapsed == 0, "negative time ignored");
        Check(ExamRules.Stable(0, 1.5f, 0, 0), "exam centered hover accepts");
        Check(!ExamRules.Stable(0, 3, 0, 0), "old 3m hover is rejected");
        Check(!ExamRules.Stable(0, 1.5f, 0, 90), "wrong-facing hover rejected");
        Check(!ExamRules.Stable(0, 1.5f, 1, 0), "moving hover rejected");
        Check(!ExamRules.Stable(.6f, 1.5f, 0, 0), "outside hover target rejected");
        Check(Near(ExamRules.AngleDelta(355, 5), 10), "clockwise wrap direction");
        Check(Near(ExamRules.AngleDelta(5, 355), -10), "counterclockwise wrap direction");
        Check(ExamRules.GearInside(0, 0, 45, 0, 0, 1), "rotated gear fits H circle");
        Check(!ExamRules.GearInside(.7f, 0, 0, 0, 0, 1), "center inside is insufficient if gear crosses line");
        Check(ExamRules.GearInside(-6, 6, 175, -6, 6, 4), "emergency inner circle accepts any heading");
        Check(ExamRules.Scale(1) == 1 && ExamRules.Scale(2) == 1.5f && ExamRules.Scale(3) == 2, "CAA field grade scaling");
        hold.Reset();
        Check(!hold.Step(true, 4.9f, ExamRules.HoverSeconds), "five second dwell cannot finish early");
        hold.Step(false, .1f, ExamRules.HoverSeconds);
        Check(!hold.Step(true, .2f, ExamRules.HoverSeconds), "broken exam dwell restarts");
        return "PASS: " + count + " core assertions";
    }
}
