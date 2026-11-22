using IdleRicca.Domain.Gameplay;

namespace IdleRicca.Application.Gameplay
{
    /// <summary>施放开始时锁定并保持到动作结束的快照。</summary>
    public readonly struct SkillCast
    {
        internal SkillCast(ulong id, SkillDefinition definition, int level)
        {
            Id = id;
            Definition = definition;
            Level = level;
        }

        /// <summary>标识本次施放，后续报告应携带此编号。</summary>
        public ulong Id { get; }
        /// <summary>施放时锁定的技能定义。</summary>
        public SkillDefinition Definition { get; }
        /// <summary>施放时锁定的等级。</summary>
        public int Level { get; }
        /// <summary>用已锁定等级计算实际伤害。</summary>
        public int Damage => Definition.GetDamage(Level);
        /// <summary>指示快照来自已接受的施放。</summary>
        public bool IsValid => Id != 0;
    }
}
