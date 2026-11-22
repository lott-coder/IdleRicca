namespace IdleRicca.Domain.Gameplay
{
    /// <summary>技能的纯规则时序和伤害配置。</summary>
    public readonly struct SkillDefinition
    {
        private readonly int _damage1;
        private readonly int _damage2;
        private readonly int _damage3;

        /// <summary>构造不可变的技能配置快照。</summary>
        public SkillDefinition(SkillId id, int damage1, int damage2, int damage3,
            float activeStart, float activeEnd, float duration)
        {
            Id = id;
            _damage1 = damage1;
            _damage2 = damage2;
            _damage3 = damage3;
            ActiveStart = activeStart;
            ActiveEnd = activeEnd;
            Duration = duration;
        }

        /// <summary>返回技能标识。</summary>
        public SkillId Id { get; }
        /// <summary>归一化命中窗口起点。</summary>
        public float ActiveStart { get; }
        /// <summary>归一化命中窗口终点。</summary>
        public float ActiveEnd { get; }
        /// <summary>配置的动作时长，单位为秒。</summary>
        public float Duration { get; }

        /// <summary>校验 ID、伤害及时间范围。</summary>
        public bool IsValid =>
            Id >= SkillId.QuickCut && Id <= SkillId.HolyGeyser &&
            _damage1 > 0 && _damage1 < _damage2 && _damage2 < _damage3 &&
            ActiveStart >= 0f && ActiveStart < ActiveEnd && ActiveEnd <= 1f &&
            Duration > 0f && !float.IsInfinity(Duration);

        /// <summary>返回指定等级伤害，非法等级为零。</summary>
        public int GetDamage(int level) => level switch
        {
            1 => _damage1,
            2 => _damage2,
            3 => _damage3,
            _ => 0
        };
    }
}
