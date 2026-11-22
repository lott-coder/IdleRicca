namespace IdleRicca.Application.Gameplay
{
    /// <summary>本地玩家的技能选择、施放和碰撞报告入口。</summary>
    public interface IPlayerSkillCommands
    {
        /// <summary>更新由 Unity 资源生命周期确定的不可用技能位图。</summary>
        void SetUnavailableSkills(int mask);
        /// <summary>尝试开启一组新的技能选择。</summary>
        bool TryOpenSkillSelection();
        /// <summary>使指定选择编号失效。</summary>
        bool CancelSkillSelection(ulong selectionId);
        /// <summary>提交选项并返回施放快照。</summary>
        bool TryCastSkill(ulong selectionId, int slot, double now, out SkillCast cast);
        /// <summary>取消施放并作废命中报告。</summary>
        bool CancelSkillCast(ulong castId);
        /// <summary>开启当前施放的动画采样区间。</summary>
        AttackSampleBatch BeginSkillFrame(ulong castId, bool inSkill, float normalizedTime, double now);
        /// <summary>尝试对敌人结算当前批次的伤害。</summary>
        AttackHit TryHitSkill(AttackSampleBatch batch, EnemyHealth target);
        /// <summary>消费当前批次并返回动作是否完成。</summary>
        bool EndSkillFrame(AttackSampleBatch batch);
    }
}

