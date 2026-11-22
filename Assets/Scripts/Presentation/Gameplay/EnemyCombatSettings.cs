using UnityEngine;

namespace IdleRicca.Presentation.Gameplay
{
    [CreateAssetMenu(menuName = "IdleRicca/Gameplay/Enemy combat")]
    public sealed class EnemyCombatSettings : ScriptableObject
    {
        [SerializeField, Min(.01f)] private float _speed = 1.9f;
        [SerializeField, Min(.01f)] private float _range = 1.25f;
        [SerializeField, Min(.01f)] private float _maximumHeightDifference = .73f;
        [SerializeField, Min(1)] private int _damage = 8;
        [SerializeField, Range(0f, 1f)] private float _activeStart = .36f;
        [SerializeField, Range(0f, 1f)] private float _activeEnd = .48f;
        [SerializeField, Min(.01f)] private float _cooldown = .85f;
        [SerializeField] private AnimationClip _attackClip;
        [SerializeField] private AnimationClip _hitClip;
        [SerializeField] private AnimationClip _deathClip;

        public float Speed => _speed;
        public float Range => _range;
        public float MaximumHeightDifference => _maximumHeightDifference;
        public int Damage => _damage;
        public float ActiveStart => _activeStart;
        public float ActiveEnd => _activeEnd;
        public float Cooldown => _cooldown;
        public AnimationClip AttackClip => _attackClip;
        public AnimationClip HitClip => _hitClip;
        public AnimationClip DeathClip => _deathClip;

        public bool IsValid => Positive(_speed) && Positive(_range * _range) && _range > 0f &&
            Positive(_maximumHeightDifference) && _damage > 0 && Positive(_cooldown) &&
            _activeStart >= 0f && _activeStart < _activeEnd && _activeEnd <= 1f &&
            ValidClip(_attackClip) && ValidClip(_hitClip) && ValidClip(_deathClip);

        private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
        private static bool ValidClip(AnimationClip clip) => clip && !clip.isLooping && Positive(clip.length);

        private void OnValidate() => _damage = Mathf.Max(1, _damage);
    }
}
