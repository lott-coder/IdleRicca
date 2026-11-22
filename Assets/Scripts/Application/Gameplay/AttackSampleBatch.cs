namespace IdleRicca.Application.Gameplay
{
    /// <summary>当前挥击中待处理的一段动画进度。</summary>
    public readonly struct AttackSampleBatch
    {
        internal AttackSampleBatch(PlayerActions owner, ulong id, float previousProgress, float progress,
            bool canHit, float start, float end)
        {
            Owner = owner;
            Id = id;
            PreviousProgress = previousProgress;
            Progress = progress;
            CanHit = canHit;
            Start = start;
            End = end;
        }

        internal PlayerActions Owner { get; }
        internal ulong Id { get; }

        public bool IsValid => Owner != null;
        public bool CanHit { get; }
        public float PreviousProgress { get; }
        public float Progress { get; }
        public float Start { get; }
        public float End { get; }
    }

    /// <summary>一次命中报告的结算结果。</summary>
    public enum AttackHit
    {
        Ignored,
        Damaged,
        Defeated
    }
}
