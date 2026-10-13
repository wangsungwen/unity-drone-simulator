using System;

namespace DroneTrainer.Core
{
    // Numeric tolerances below are simulator feedback settings, not CAA pass/fail thresholds.
    public static class ExamRules
    {
        public const float HoverSeconds = 5;
        public const float LowHeight = 1.5f;
        public const float CircuitHeight = 20;
        public const float HeadingTolerance = 15;
        public static float AngleDelta(float from, float to)
        { return (to - from + 540) % 360 - 180; }
        public static bool Stable(float horizontalError, float height, float speed, float headingError)
        { return horizontalError <= .55f && height >= 1 && height <= 2 && speed <= .5f && Math.Abs(headingError) <= HeadingTolerance; }
        public static bool GearInside(float x, float z, float heading, float centerX, float centerZ, float radius)
        {
            double a = heading * Math.PI / 180;
            for (int i = 0; i < 4; i++)
            {
                float gx = (i % 2 == 0 ? -.325f : .325f), gz = (i < 2 ? -.325f : .325f);
                double dx = x + gx * Math.Cos(a) + gz * Math.Sin(a) - centerX;
                double dz = z - gx * Math.Sin(a) + gz * Math.Cos(a) - centerZ;
                if (dx * dx + dz * dz >= radius * radius) return false;
            }
            return true;
        }
        public static float Scale(int grade) { return grade == 2 ? 1.5f : grade == 3 ? 2 : 1; }
    }
}
