namespace IdleRicca.Domain.Gameplay
{
    /// <summary>一波的生成参数。间隔单位为游戏秒。</summary>
    public readonly struct WaveDefinition
    {
        /// <summary>组合同屏上限、游戏秒间隔及击败目标数。</summary>
        public WaveDefinition(int maximumEnemies, double spawnInterval, int totalEnemies)
        {
            MaximumEnemies = maximumEnemies;
            SpawnInterval = spawnInterval;
            TotalEnemies = totalEnemies;
        }

        /// <summary>一次生成后的最大占用名额数。</summary>
        public int MaximumEnemies { get; }
        /// <summary>确认生成后至下次生成的最短游戏秒数。</summary>
        public double SpawnInterval { get; }
        /// <summary>通过该波所需的击败数。</summary>
        public int TotalEnemies { get; }
        /// <summary>检查可用于运行时的正数配置。</summary>
        public bool IsValid => MaximumEnemies > 0 && TotalEnemies > 0 &&
            SpawnInterval > 0 && !double.IsInfinity(SpawnInterval);
    }
}
