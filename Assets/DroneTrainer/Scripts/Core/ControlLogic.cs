using System;

namespace DroneTrainer.Core
{
    public enum StickMode { Mode2, Mode1, Mode3 }

    [Serializable]
    public struct FlightInput
    {
        public float throttle, yaw, pitch, roll;
        public bool IsValid
        {
            get { return Valid(throttle) && Valid(yaw) && Valid(pitch) && Valid(roll); }
        }
        static bool Valid(float v) { return !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) <= 1.001f; }
    }

    public static class ControlLogic
    {
        public static float Axis(float value, float deadzone, float sensitivity)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0;
            deadzone = Math.Max(0, Math.Min(.95f, deadzone));
            float magnitude = Math.Min(1, Math.Abs(value));
            if (magnitude <= deadzone) return 0;
            return Math.Sign(value) * (magnitude - deadzone) / (1 - deadzone) * Math.Max(0, Math.Min(1, sensitivity));
        }

        public static FlightInput Map(float lx, float ly, float rx, float ry, StickMode mode, float sensitivity, float throttleSensitivity)
        {
            FlightInput result;
            if (mode == StickMode.Mode1)
                result = new FlightInput { yaw = lx, pitch = ly, roll = rx, throttle = ry };
            else if (mode == StickMode.Mode3)
                result = new FlightInput { pitch = ly, roll = lx, yaw = rx, throttle = ry };
            else
                result = new FlightInput { yaw = lx, throttle = ly, roll = rx, pitch = ry };
            result.yaw = Axis(result.yaw, .08f, sensitivity);
            result.pitch = Axis(result.pitch, .08f, sensitivity);
            result.roll = Axis(result.roll, .08f, sensitivity);
            result.throttle = Axis(result.throttle, .08f, throttleSensitivity);
            return result;
        }
    }

    // Both sequence checks and timeout use the receiver's monotonic clock.
    public sealed class PeerGate
    {
        public const double Timeout = .6;
        public string Session { get; private set; }
        public int LastSequence { get; private set; }
        public int LastAction { get; private set; }
        public double LastSeen { get; private set; }
        public void Open(string session, double now)
        {
            Session = session; LastSequence = 0; LastAction = 0; LastSeen = now;
        }
        public bool Accept(string session, int sequence, FlightInput input, double now)
        {
            if (Session == null || session != Session || sequence <= LastSequence || !input.IsValid || Expired(now)) return false;
            LastSequence = sequence; LastSeen = now; return true;
        }
        public bool Action(int id)
        {
            if (id <= LastAction || id <= 0) return false;
            LastAction = id; return true;
        }
        public bool Expired(double now) { return Session != null && now - LastSeen > Timeout; }
        public void Close() { Session = null; LastSequence = 0; LastAction = 0; }
    }

    public sealed class HoldTimer
    {
        public float Elapsed { get; private set; }
        public bool Step(bool inside, float dt, float duration)
        {
            Elapsed = inside ? Elapsed + Math.Max(0, dt) : 0;
            return Elapsed >= duration;
        }
        public void Reset() { Elapsed = 0; }
    }
}
