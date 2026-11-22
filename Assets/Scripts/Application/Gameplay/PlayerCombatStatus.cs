namespace IdleRicca.Application.Gameplay
{
    /// <summary>Read-only life and combo values from the local combat session.</summary>
    public readonly struct PlayerCombatStatus
    {
        internal PlayerCombatStatus(int currentHp, int maxHp, int currentMp, int maxMp, int comboCount,
            double comboSecondsRemaining, ulong hitSequence)
        {
            CurrentHp = currentHp;
            MaxHp = maxHp;
            CurrentMp = currentMp;
            MaxMp = maxMp;
            ComboCount = comboCount;
            ComboSecondsRemaining = comboSecondsRemaining;
            HitSequence = hitSequence;
        }

        public int CurrentHp { get; }
        public int MaxHp { get; }
        /// <summary>用于 HUD 的当前 MP。</summary>
        public int CurrentMp { get; }
        /// <summary>用于 HUD 比例的 MP 上限。</summary>
        public int MaxMp { get; }
        public int ComboCount { get; }
        public double ComboSecondsRemaining { get; }
        public ulong HitSequence { get; }
    }
}
