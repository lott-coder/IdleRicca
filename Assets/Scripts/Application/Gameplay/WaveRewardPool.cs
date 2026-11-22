using System;
using IdleRicca.Domain.Gameplay;

namespace IdleRicca.Application.Gameplay
{
    /// <summary>按固定权重抽取类型，在同组内为该类型抽取未出现的等级。</summary>
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
            long total = (long)healthWeight + attackWeight + skillWeight;
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
            int totalWeight = includeSkill ? _totalWeight : _totalWeight - _weights[(int)WaveReward.RewardKind.Skill];
            // 三张卡与三个等级保证每次抽卡前，每个合资格的类型都还有可选等级。
            Span<int> availableLevels = stackalloc int[KindCount] { Levels, Levels, Levels };
            for (int offer = 0; offer < destination.Length; offer++)
            {
                int kind = 0;
                int kindRoll = _random.Next(totalWeight);
                while (kindRoll >= _weights[kind])
                {
                    kindRoll -= _weights[kind];
                    kind++;
                }

                int levelRoll = _random.Next(availableLevels[kind]);
                int first = kind * Levels;
                for (int index = first; index < first + Levels; index++)
                {
                    int bit = 1 << index;
                    if ((drawn & bit) != 0)
                        continue;

                    if (levelRoll == 0)
                    {
                        destination[offer] = _catalog[index];
                        drawn |= bit;
                        availableLevels[kind]--;
                        break;
                    }

                    levelRoll--;
                }
            }
        }

        private static bool Increasing(int[] amounts) => amounts != null && amounts.Length == Levels &&
            amounts[0] > 0 && amounts[1] > amounts[0] && amounts[2] > amounts[1];
    }
}
