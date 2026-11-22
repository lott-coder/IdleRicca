using UnityEngine;

namespace IdleRicca.Presentation.Gameplay
{
    internal struct AnimationHitStop
    {
        private double _endsAt;
        private float _originalSpeed;
        private float _appliedSpeed;
        private float _multiplier;
        private bool _active;

        internal float Multiplier => _active ? _multiplier : 1f;

        internal void Apply(Animator animator, NormalAttackSettings settings)
        {
            if (!animator || !animator.isActiveAndEnabled)
                return;

            float seconds = settings.HitStopSeconds;
            float multiplier = settings.HitStopMultiplier;
            if (seconds <= 0f || multiplier >= 1f)
            {
                Release(animator);
                return;
            }

            if (!_active || animator.speed != _appliedSpeed)
                _originalSpeed = animator.speed;

            _active = true;
            _endsAt = Time.unscaledTimeAsDouble + seconds;
            _multiplier = multiplier;
            _appliedSpeed = _originalSpeed * multiplier;
            animator.speed = _appliedSpeed;
        }

        internal void RestoreIfElapsed(Animator animator)
        {
            if (_active && Time.unscaledTimeAsDouble >= _endsAt)
                Release(animator);
        }

        internal void Release(Animator animator)
        {
            if (!_active)
                return;

            _active = false;
            // 外部若已接管播放速度，保留它设置的值。
            if (animator && animator.speed == _appliedSpeed)
                animator.speed = _originalSpeed;
        }
    }
}
