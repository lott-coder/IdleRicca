using System;
using IdleRicca.Domain.Gameplay;

namespace IdleRicca.Application.Gameplay
{
    /// <summary>供波次界面读取的本局快照。</summary>
    public readonly struct WaveStatus
    {
        public enum RunPhase
        {
            Fighting,
            Reward,
            Defeated,
            Won,
            Restarting,
        }

        private readonly WaveReward _left;
        private readonly WaveReward _middle;
        private readonly WaveReward _right;

        internal WaveStatus(int number, RunPhase phase, ulong offerId, WaveReward left, WaveReward middle, WaveReward right)
        {
            Number = number;
            Phase = phase;
            OfferId = offerId;
            _left = left;
            _middle = middle;
            _right = right;
        }

        public const int TotalWaves = 10;
        public const int OfferSize = 3;
        /// <summary>当前显示波号，从 1 开始。</summary>
        public int Number { get; }
        /// <summary>快照所属的战斗、领奖或结局阶段。</summary>
        public RunPhase Phase { get; }
        /// <summary>当前奖励组选项标识，零表示暂无可结算的奖励。</summary>
        public ulong OfferId { get; }

        /// <summary>读取索引为 0–2 的奖励。</summary>
        public WaveReward GetReward(int index)
        {
            switch (index)
            {
                case 0: return _left;
                case 1: return _middle;
                case 2: return _right;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
