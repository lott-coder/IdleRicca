namespace IdleRicca.Domain.Gameplay
{
    /// <summary>固定类型和等级的一张波次奖励。</summary>
    public readonly struct WaveReward
    {
        public enum RewardKind
        {
            Health,
            Attack,
            Skill,
        }

        /// <summary>构造带等级和增量的固定选项。</summary>
        public WaveReward(RewardKind kind, int level, int amount)
        {
            Kind = kind;
            Level = level;
            Amount = amount;
        }

        /// <summary>应用属性增量或触发技能入口的类别。</summary>
        public RewardKind Kind { get; }
        /// <summary>从 1 开始的奖励等级。</summary>
        public int Level { get; }
        /// <summary>属性增加值。技能占位的增量为零。</summary>
        public int Amount { get; }
    }
}
