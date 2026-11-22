using System;

namespace IdleRicca.Domain.Gameplay
{
    /// <summary>有零下界的角色生命值规则。</summary>
    public static class HitPoints
    {
        // R16：施放期间减伤 80%，即承受五分之一的伤害。
        private const int SkillDamageDivisor = 5;

        /// <summary>计算施放期间实际承受的整数伤害，正数伤害向上取整。</summary>
        public static int ReduceDamageDuringSkillCast(int damage) =>
            damage > 0 ? 1 + (damage - 1) / SkillDamageDivisor : 0;

        /// <summary>对仍存活的角色扣除正数伤害，结果最低为零。</summary>
        public static int AfterDamage(int current, int damage)
        {
            if (current <= 0)
                return 0;

            return damage > 0 ? Math.Max(0, current - damage) : current;
        }
    }
}
