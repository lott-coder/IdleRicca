using System;
using UnityEngine;

namespace IdleRicca.Presentation.Gameplay
{
    /// <summary>普攻数值及从源动作采出的剑身盒。</summary>
    public sealed class NormalAttackSettings : ScriptableObject
    {
        // R5 的采样预算须容纳整段有效窗口和两个插值边界。
        public const int MaxBakedPoses = 126;
        private const float DefaultHitStopSeconds = 0.10f;
        private const float DefaultHitStopMultiplier = 0.05f;

        [SerializeField, Min(1)] private int _damage = 25;
        [SerializeField] private AnimationClip _clip;
        [SerializeField, Range(0f, 1f)] private float _activeStart = 14f / 70f;
        [SerializeField, Range(0f, 1f)] private float _activeEnd = 22f / 70f;
        [SerializeField, Min(0f), Tooltip("有效命中后双方减速的真实秒数。0 关闭卡肉。")]
        private float _hitStopSeconds = DefaultHitStopSeconds;
        [SerializeField, Range(0f, 1f), Tooltip("命中双方共用的播放倍率。0 完全停顿，1 保持原速。")]
        private float _hitStopMultiplier = DefaultHitStopMultiplier;
        [SerializeField, HideInInspector] private AnimationClip _bakedClip;
        [SerializeField, HideInInspector] private RuntimeAnimatorController _bakedController;
        [SerializeField, HideInInspector] private BladePose[] _poses;

        public int Damage => _damage;
        public AnimationClip Clip => _clip;
        public float ActiveStart => _activeStart;
        public float ActiveEnd => _activeEnd;
        /// <summary>卡肉从命中起持续的真实秒数。</summary>
        public float HitStopSeconds => Mathf.Max(0f, FiniteOrDefault(_hitStopSeconds, DefaultHitStopSeconds));

        /// <summary>Ricca 与被命中敌人共用的播放倍率。</summary>
        public float HitStopMultiplier => Mathf.Clamp01(FiniteOrDefault(_hitStopMultiplier, DefaultHitStopMultiplier));

        internal int PoseCount => _poses.Length;
        internal BladePose GetPose(int index) => _poses[index];

        private void OnValidate()
        {
            if (_damage < 1)
                _damage = 1;
            _hitStopSeconds = HitStopSeconds;
            _hitStopMultiplier = HitStopMultiplier;
        }

        private static float FiniteOrDefault(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        /// <summary>检查可用配置以及烘焙姿态是否覆盖当前有效窗口。</summary>
        public bool HasValidTrajectory()
        {
            if (!_clip || _clip != _bakedClip || !_bakedController || _clip.length <= 0f || _damage <= 0 || !(_activeStart >= 0f) ||
                !(_activeEnd <= 1f) || !(_activeEnd > _activeStart) ||
                _poses == null || _poses.Length < 2 || _poses.Length > MaxBakedPoses ||
                _poses[0].Progress != _activeStart || _poses[_poses.Length - 1].Progress != _activeEnd)
                return false;

            for (int i = 0; i < _poses.Length; i++)
            {
                BladePose pose = _poses[i];
                if (!pose.IsValid || (i > 0 && pose.Progress <= _poses[i - 1].Progress))
                    return false;
            }

            return true;
        }

        internal bool MatchesController(RuntimeAnimatorController controller) => controller == _bakedController;

        internal BladePose Sample(float progress)
        {
            for (int i = 1; i < _poses.Length; i++)
            {
                BladePose next = _poses[i];
                if (progress > next.Progress)
                    continue;

                BladePose previous = _poses[i - 1];
                return BladePose.Interpolate(previous, next, progress);
            }

            return _poses[_poses.Length - 1];
        }
    }

    [Serializable]
    internal struct BladePose
    {
        [SerializeField] private float _progress;
        [SerializeField] private Vector3 _center;
        [SerializeField] private Quaternion _rotation;
        [SerializeField] private Vector3 _halfExtents;

        internal float Progress => _progress;
        internal Vector3 Center => _center;
        internal Quaternion Rotation => _rotation;
        internal Vector3 HalfExtents => _halfExtents;
        internal bool IsValid => Finite(_progress) && Finite(_center.x) && Finite(_center.y) &&
            Finite(_center.z) && Finite(_rotation.x) && Finite(_rotation.y) && Finite(_rotation.z) &&
            Finite(_rotation.w) && Mathf.Abs(Quaternion.Dot(_rotation, _rotation) - 1f) < 0.01f &&
            _halfExtents.x > 0f && _halfExtents.y > 0f && _halfExtents.z > 0f &&
            Finite(_halfExtents.x) && Finite(_halfExtents.y) && Finite(_halfExtents.z);

        internal static BladePose Interpolate(BladePose from, BladePose to, float progress)
        {
            float fraction = Mathf.InverseLerp(from.Progress, to.Progress, progress);
            return new BladePose
            {
                _progress = progress,
                _center = Vector3.LerpUnclamped(from.Center, to.Center, fraction),
                _rotation = Quaternion.SlerpUnclamped(from.Rotation, to.Rotation, fraction),
                _halfExtents = Vector3.LerpUnclamped(from.HalfExtents, to.HalfExtents, fraction)
            };
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
