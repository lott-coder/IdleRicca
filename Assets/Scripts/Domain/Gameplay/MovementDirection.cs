using System;

namespace IdleRicca.Domain.Gameplay
{
    /// <summary>镜头水平朝向下的移动输入。</summary>
    public readonly struct MovementDirection
    {
        private MovementDirection(float x, float z, bool running)
        {
            X = x;
            Z = z;
            IsRunning = running;
        }

        public float X { get; }
        public float Z { get; }
        public bool IsRunning { get; }
        public bool IsMoving => X != 0f || Z != 0f;

        /// <summary>净化并旋转移动输入，使斜向移动不超过单轴速度。</summary>
        public static MovementDirection FromInput(float x, float y, float yawDegrees, bool running)
        {
            x = ClampAxis(x);
            y = ClampAxis(y);
            if (x == 0f && y == 0f)
                return default;

            float lengthSquared = x * x + y * y;
            if (lengthSquared > 1f)
            {
                float scale = 1f / (float)Math.Sqrt(lengthSquared);
                x *= scale;
                y *= scale;
            }

            double yaw = IsFinite(yawDegrees) ? yawDegrees % 360f * Math.PI / 180d : 0d;
            float sin = (float)Math.Sin(yaw);
            float cos = (float)Math.Cos(yaw);
            return new MovementDirection(x * cos + y * sin, y * cos - x * sin, running);
        }

        private static float ClampAxis(float value) => IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0f;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
