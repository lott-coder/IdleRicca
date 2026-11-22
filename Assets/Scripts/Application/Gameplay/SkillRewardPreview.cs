using IdleRicca.Domain.Gameplay;

namespace IdleRicca.Application.Gameplay
{
    /// <summary>描述领取前可查看的技能和等级变化。</summary>
    public readonly struct SkillRewardPreview
    {
        internal SkillRewardPreview(int slot, SkillId skill, int previousLevel, int level)
        {
            Slot = slot;
            Skill = skill;
            PreviousLevel = previousLevel;
            Level = level;
        }

        /// <summary>奖励影响的零基槽位。</summary>
        public int Slot { get; }
        /// <summary>授予或升级的技能标识。</summary>
        public SkillId Skill { get; }
        /// <summary>原等级，未学会时为零。</summary>
        public int PreviousLevel { get; }
        /// <summary>领取后的等级。</summary>
        public int Level { get; }
    }
}
