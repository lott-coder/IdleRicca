using System;
using System.Collections.Generic;
using IdleRicca.Application.Gameplay;
using UnityEngine;

namespace IdleRicca.Presentation.Gameplay
{
    /// <summary>把敌人 Collider 映射到一份生命状态，并响应归零。</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyTarget : MonoBehaviour
    {
        private static readonly Dictionary<Collider, EnemyTarget> Targets = new Dictionary<Collider, EnemyTarget>(8);
        private static Collider[] _overlapBuffer = new Collider[8];
        private static RaycastHit[] _sweepBuffer = new RaycastHit[8];

        [SerializeField] private EnemyHealthSettings _settings;
        [SerializeField] private Collider[] _hurtboxes;

        private EnemyActor _actor;

        internal EnemyHealth Health { get; private set; }
        internal static Collider[] OverlapBuffer => _overlapBuffer;
        internal static RaycastHit[] SweepBuffer => _sweepBuffer;

        public int CurrentHp => Health?.CurrentHp ?? 0;

        private void Awake()
        {
            _actor = GetComponent<EnemyActor>();
            if (_settings && _settings.StartingHp > 0)
                Health = new EnemyHealth(_settings.StartingHp);
            else
                Debug.LogError("EnemyTarget requires a positive starting HP configured in its asset.", this);
        }

        private void OnEnable()
        {
            if (Health == null || !Health.IsAlive || _hurtboxes == null)
                return;

            foreach (Collider hurtbox in _hurtboxes)
            {
                if (hurtbox)
                    Targets[hurtbox] = this;
            }

            if (_overlapBuffer.Length < Targets.Count)
            {
                int capacity = Mathf.NextPowerOfTwo(Targets.Count);
                Array.Resize(ref _overlapBuffer, capacity);
                Array.Resize(ref _sweepBuffer, capacity);
            }
        }

        private void OnDisable() => Unregister();

        private void Unregister()
        {
            if (_hurtboxes == null)
                return;

            foreach (Collider hurtbox in _hurtboxes)
            {
                if (!ReferenceEquals(hurtbox, null))
                    Targets.Remove(hurtbox);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearSceneTargets() => Targets.Clear();

        internal static bool TryResolve(Collider hit, out EnemyTarget target) => Targets.TryGetValue(hit, out target);

        internal void ShowHitResult(AttackHit result, NormalAttackSettings attack)
        {
            if (_actor)
                _actor.OnDamage(attack);
            else if (result == AttackHit.Defeated)
                gameObject.SetActive(false);
        }

        internal void DisableHurtboxes()
        {
            Unregister();
            if (_hurtboxes == null)
                return;

            foreach (Collider hurtbox in _hurtboxes)
            {
                if (hurtbox)
                    hurtbox.enabled = false;
            }
        }
    }
}

