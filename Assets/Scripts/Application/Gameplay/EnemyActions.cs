using System;

namespace IdleRicca.Application.Gameplay
{
    public enum EnemyPhase
    {
        Idle,
        Chasing,
        Attacking,
        Hit,
        Death,
        Retired,
    }

    public readonly struct EnemyAttackFrame
    {
        internal EnemyAttackFrame(EnemyActions owner, ulong id, float previousProgress, float progress,
            bool canHit, float start, float end)
        {
            Owner = owner;
            Id = id;
            PreviousProgress = previousProgress;
            Progress = progress;
            CanHit = canHit;
            Start = start;
            End = end;
        }

        internal EnemyActions Owner { get; }
        internal ulong Id { get; }

        public bool IsValid => Owner != null;
        public bool CanHit { get; }
        public float PreviousProgress { get; }
        public float Progress { get; }
        public float Start { get; }
        public float End { get; }
    }

    public interface IEnemyCommands
    {
        bool BindTarget(PlayerActions target);
        EnemyPhase Step(bool reachable, float distanceSquared, float heightDifference, bool clearSight, float deltaTime);
        EnemyPhase ObserveHit();
        EnemyPhase ObserveAnimation(EnemyPhase animation, float normalizedTime, float deltaTime);
        EnemyAttackFrame BeginAttackFrame(bool inAttack, float normalizedTime, float deltaTime);
        AttackHit TryHitRicca(EnemyAttackFrame frame, PlayerActions target);
        bool EndAttackFrame(EnemyAttackFrame frame);
        void CancelAttack();
    }

    /// <summary>Authority for one enemy's behaviour and its single-target combat.</summary>
    public sealed class EnemyActions : IEnemyCommands
    {
        private const float EntryTimeout = .5f;
        private const float AnimationGrace = .5f;

        private readonly float _rangeSquared;
        private readonly float _heightTolerance;
        private readonly int _damage;
        private readonly float _activeStart;
        private readonly float _activeEnd;
        private readonly float _cooldownSeconds;
        private readonly float _attackTimeout;
        private readonly float _hitTimeout;
        private readonly float _deathTimeout;
        private PlayerActions _target;
        private ulong _observedHit;
        private ulong _frameId;
        private bool _hasHit;
        private bool _enteredAttack;
        private float _phaseElapsed;
        private float _cooldownRemaining;
        private float _progress;
        private EnemyAttackFrame _openFrame;

        public EnemyActions(EnemyHealth health, float range, float heightTolerance, int damage,
            float activeStart, float activeEnd, float cooldownSeconds, float attackDuration, float hitDuration, float deathDuration)
        {
            Health = health ?? throw new ArgumentNullException(nameof(health));
            if (!PositiveFinite(range * range) || range <= 0f || !PositiveFinite(heightTolerance))
                throw new ArgumentOutOfRangeException(nameof(range));
            if (damage <= 0 || !(activeStart >= 0f && activeStart < activeEnd && activeEnd <= 1f))
                throw new ArgumentOutOfRangeException(nameof(damage));
            if (!PositiveFinite(cooldownSeconds) || !PositiveFinite(attackDuration) ||
                !PositiveFinite(hitDuration) || !PositiveFinite(deathDuration))
                throw new ArgumentOutOfRangeException(nameof(cooldownSeconds));

            _rangeSquared = range * range;
            _heightTolerance = heightTolerance;
            _damage = damage;
            _activeStart = activeStart;
            _activeEnd = activeEnd;
            _cooldownSeconds = cooldownSeconds;
            _attackTimeout = attackDuration + AnimationGrace;
            _hitTimeout = hitDuration + AnimationGrace;
            _deathTimeout = deathDuration + AnimationGrace;
        }

        public EnemyHealth Health { get; }
        public IEnemyCommands Commands => this;
        public EnemyPhase Phase { get; private set; }
        public bool AnimationTimedOut { get; private set; }

        bool IEnemyCommands.BindTarget(PlayerActions target)
        {
            if (target == null || (_target != null && !ReferenceEquals(_target, target)))
                return false;

            _target = target;
            return true;
        }

        EnemyPhase IEnemyCommands.Step(bool reachable, float distanceSquared, float heightDifference,
            bool clearSight, float deltaTime)
        {
            ObserveHit();
            if (Phase == EnemyPhase.Death || Phase == EnemyPhase.Retired)
                return Phase;

            _cooldownRemaining = Math.Max(0f, _cooldownRemaining - ValidTime(deltaTime));
            if (Phase == EnemyPhase.Hit)
                return Phase;

            if (_target == null || !_target.IsAlive)
            {
                CancelAttack();
                Phase = EnemyPhase.Idle;
                return Phase;
            }

            if (Phase == EnemyPhase.Attacking)
                return Phase;

            if (!IsFinite(distanceSquared) || distanceSquared < 0f || !IsFinite(heightDifference))
            {
                Phase = EnemyPhase.Idle;
                return Phase;
            }

            bool inRange = distanceSquared <= _rangeSquared && Math.Abs(heightDifference) <= _heightTolerance;
            if (inRange && clearSight)
            {
                if (_cooldownRemaining <= 0f)
                {
                    _phaseElapsed = 0f;
                    AnimationTimedOut = false;
                    Phase = EnemyPhase.Attacking;
                }
                else
                {
                    Phase = EnemyPhase.Idle;
                }
            }
            else
            {
                Phase = reachable ? EnemyPhase.Chasing : EnemyPhase.Idle;
            }

            return Phase;
        }

        EnemyPhase IEnemyCommands.ObserveHit() => ObserveHit();

        EnemyPhase IEnemyCommands.ObserveAnimation(EnemyPhase animation, float normalizedTime, float deltaTime)
        {
            ObserveHit();
            if (Phase != EnemyPhase.Hit && Phase != EnemyPhase.Death)
                return Phase;

            bool death = Phase == EnemyPhase.Death;
            _phaseElapsed += ValidTime(deltaTime);
            if (animation == Phase && IsFinite(normalizedTime) && normalizedTime >= 1f)
                Phase = death ? EnemyPhase.Retired : EnemyPhase.Idle;
            else if (_phaseElapsed >= (death ? _deathTimeout : _hitTimeout))
            {
                AnimationTimedOut = true;
                Phase = death ? EnemyPhase.Retired : EnemyPhase.Idle;
            }

            return Phase;
        }

        EnemyAttackFrame IEnemyCommands.BeginAttackFrame(bool inAttack, float normalizedTime, float deltaTime)
        {
            ObserveHit();
            if (Phase != EnemyPhase.Attacking || _openFrame.IsValid)
                return default;

            if (_target == null || !_target.IsAlive)
            {
                CancelAttack();
                return default;
            }

            _phaseElapsed += ValidTime(deltaTime);
            if (!inAttack || !IsFinite(normalizedTime))
            {
                if (_enteredAttack || _phaseElapsed >= EntryTimeout)
                {
                    AnimationTimedOut = true;
                    CancelAttack();
                }

                return default;
            }

            // Keep the sampled endpoint so spatial interpolation maps to the real frame duration.
            float progress = Math.Max(normalizedTime, 0f);
            if (progress < _progress || (progress < 1f && _phaseElapsed >= _attackTimeout))
            {
                AnimationTimedOut = true;
                CancelAttack();
                return default;
            }

            float start = Math.Max(_progress, _activeStart);
            float end = Math.Min(progress, _activeEnd);
            bool canHit = !_hasHit && start <= end && _progress < _activeEnd && progress > _progress;
            _enteredAttack = true;
            _openFrame = new EnemyAttackFrame(this, ++_frameId, _progress, progress, canHit, start, end);
            return _openFrame;
        }

        AttackHit IEnemyCommands.TryHitRicca(EnemyAttackFrame frame, PlayerActions target)
        {
            ObserveHit();
            if (Phase != EnemyPhase.Attacking || !IsCurrent(frame) || !_openFrame.CanHit || _hasHit ||
                target == null || !ReferenceEquals(target, _target) || !target.IsAlive)
                return AttackHit.Ignored;

            _hasHit = true;
            return target.ReceiveEnemyDamage(_damage);
        }

        bool IEnemyCommands.EndAttackFrame(EnemyAttackFrame frame)
        {
            ObserveHit();
            if (!IsCurrent(frame))
                return false;

            _progress = _openFrame.Progress;
            _openFrame = default;
            if (_progress < 1f)
                return false;

            CancelAttack();
            return true;
        }

        void IEnemyCommands.CancelAttack() => CancelAttack();

        private bool IsCurrent(EnemyAttackFrame frame)
        {
            return _openFrame.IsValid && ReferenceEquals(this, frame.Owner) && _openFrame.Id == frame.Id;
        }

        private EnemyPhase ObserveHit()
        {
            if (_observedHit == Health.HitVersion || Phase == EnemyPhase.Death || Phase == EnemyPhase.Retired)
                return Phase;

            _observedHit = Health.HitVersion;
            CancelAttack();
            _phaseElapsed = 0f;
            AnimationTimedOut = false;
            Phase = Health.IsAlive ? EnemyPhase.Hit : EnemyPhase.Death;
            return Phase;
        }

        private void CancelAttack()
        {
            if (Phase != EnemyPhase.Attacking)
                return;

            _openFrame = default;
            _progress = 0f;
            _enteredAttack = false;
            _hasHit = false;
            _cooldownRemaining = _cooldownSeconds;
            Phase = EnemyPhase.Idle;
        }

        private static float ValidTime(float value) => PositiveFinite(value) ? value : 0f;
        private static bool PositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
