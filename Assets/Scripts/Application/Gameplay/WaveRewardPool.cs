using System;
using IdleRicca.Domain.Gameplay;

namespace IdleRicca.Application.Gameplay
{
    /// <summary>将类型权重等分给三个等级，在一组内无放回抽取。</summary>
    public sealed class WaveRewardPool
    {
        public const int Levels = 3;
        private const int KindCount = 3;
        private readonly Random _random;
        private readonly WaveReward[] _catalog;
        private readonly int[] _weights;
        private readonly int _totalWeight;

        /// <summary>验证并复制配置。种子可用于复现奖励序列。</summary>
        public WaveRewardPool(int healthWeight, int attackWeight, int skillWeight,
            int[] healthAmounts, int[] attackAmounts, int? seed = null)
        {
            long total = Levels * ((long)healthWeight + attackWeight + skillWeight);
            if (skillWeight <= 0 || healthWeight <= 0 || attackWeight <= 0 || total > int.MaxValue)
                throw new ArgumentException("Reward weights must be positive and fit the total draw weight.");
            if (!Increasing(healthAmounts) || !Increasing(attackAmounts))
                throw new ArgumentException("Health and attack rewards require three increasing positive amounts.");

            _totalWeight = (int)total;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _catalog = new WaveReward[KindCount * Levels];
            _weights = new[] { healthWeight, attackWeight, skillWeight };
            for (int level = 0; level < Levels; level++)
            {
                _catalog[level] = new WaveReward(WaveReward.RewardKind.Health, level + 1, healthAmounts[level]);
                _catalog[Levels + level] = new WaveReward(WaveReward.RewardKind.Attack, level + 1, attackAmounts[level]);
                _catalog[2 * Levels + level] = new WaveReward(WaveReward.RewardKind.Skill, level + 1, 0);
            }
        }

        internal int MaximumHealthBonus => _catalog[Levels - 1].Amount;
        internal int MaximumAttackBonus => _catalog[2 * Levels - 1].Amount;

        /// <summary>填充预先分配的三个选项。</summary>
        public void Draw(WaveReward[] destination) => Draw(destination, true);

        /// <summary>按技能资格向原缓冲区写入三个不同奖励。</summary>
        public void Draw(WaveReward[] destination, bool includeSkill)
        {
            if (destination == null || destination.Length != WaveStatus.OfferSize)
                throw new ArgumentException("A wave offer requires three slots.", nameof(destination));

            int drawn = 0;
            int remaining = includeSkill ? _totalWeight : _totalWeight - Levels * _weights[KindCount - 1];
            int candidateCount = includeSkill ? _catalog.Length : (KindCount - 1) * Levels;
            for (int offer = 0; offer < destination.Length; offer++)
            {
                int roll = _random.Next(remaining);
                for (int index = 0; index < candidateCount; index++)
                {
                    int bit = 1 << index;
                    if ((drawn & bit) != 0)
                        continue;

                    int weight = _weights[index / Levels];
                    if (roll < weight)
                    {
                        destination[offer] = _catalog[index];
                        drawn |= bit;
                        remaining -= weight;
                        break;
                    }

                    roll -= weight;
                }
            }
        }

        private static bool Increasing(int[] amounts) => amounts != null && amounts.Length == Levels &&
            amounts[0] > 0 && amounts[1] > amounts[0] && amounts[2] > amounts[1];
    }
}
