using System;
using IdleRicca.Domain.Gameplay;

namespace IdleRicca.Application.Gameplay
{
    /// <summary>敌人的单场景生命值。外部只能查询。</summary>
    public sealed class EnemyHealth
    {
        /// <summary>为新敌人实例建立满生命状态。</summary>
        public EnemyHealth(int startingHp)
        {
            if (startingHp <= 0)
                throw new ArgumentOutOfRangeException(nameof(startingHp));

            CurrentHp = startingHp;
        }

        private ulong _waveSlot;
        private Action<ulong> _waveDefeated;

        public int CurrentHp { get; private set; }
        public bool IsAlive => CurrentHp > 0;
        internal ulong HitVersion { get; private set; }

        internal bool TrackWaveDefeat(ulong slot, Action<ulong> defeated)
        {
            if (_waveDefeated != null || !IsAlive)
                return false;

            _waveSlot = slot;
            _waveDefeated = defeated;
            return true;
        }

        internal void StopTrackingWaveDefeat()
        {
            _waveSlot = 0;
            _waveDefeated = null;
        }

        internal AttackHit ReceiveDamage(int damage)
        {
            if (!IsAlive || damage <= 0)
                return AttackHit.Ignored;

            CurrentHp = HitPoints.AfterDamage(CurrentHp, damage);
            HitVersion++;
            if (IsAlive)
                return AttackHit.Damaged;

            _waveDefeated?.Invoke(_waveSlot);
            return AttackHit.Defeated;
        }
    }
}
