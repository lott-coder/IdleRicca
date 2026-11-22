using System;

namespace IdleRicca.Domain.Gameplay
{
    /// <summary>有零下界的角色生命值规则。</summary>
    public static class HitPoints
    {
        /// <summary>对仍存活的角色扣除正数伤害，结果最低为零。</summary>
        public static int AfterDamage(int current, int damage)
        {
            if (current <= 0)
                return 0;

            return damage > 0 ? Math.Max(0, current - damage) : current;
        }
    }
}
