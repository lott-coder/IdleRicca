using IdleRicca.Application.Gameplay;
using IdleRicca.Domain.Gameplay;
using UnityEngine;

namespace IdleRicca.Presentation.Gameplay
{
    /// <summary>在 Inspector 配置波次及属性奖励。运行状态保存在场景内。</summary>
    [CreateAssetMenu(menuName = "IdleRicca/Gameplay/Combat waves")]
    public sealed class WaveSettings : ScriptableObject
    {
        [System.Serializable]
        private struct WaveRow
        {
            [SerializeField, Min(1)] private int _maximumEnemies;
            [SerializeField, Min(.01f)] private float _spawnInterval;
            [SerializeField, Min(1)] private int _totalEnemies;

            public WaveDefinition ToDefinition() => new WaveDefinition(_maximumEnemies, _spawnInterval, _totalEnemies);
        }

        [SerializeField] private WaveRow[] _waves;
        [SerializeField, Min(1)] private int _healthWeight = 45;
        [SerializeField, Min(1)] private int _attackWeight = 45;
        [SerializeField, Min(1)] private int _skillWeight = 45;
        [SerializeField] private int[] _healthBonuses = { 10, 20, 30 };
        [SerializeField] private int[] _attackBonuses = { 4, 8, 12 };

        internal bool HasWaves => _waves != null && _waves.Length > 0;

        internal WaveRun CreateRun(PlayerActions player, EnemySpawnSettings spawning)
        {
            var waves = new WaveDefinition[_waves?.Length ?? 0];
            for (int i = 0; i < waves.Length; i++)
                waves[i] = _waves[i].ToDefinition();

            var pool = new WaveRewardPool(_healthWeight, _attackWeight, _skillWeight, _healthBonuses, _attackBonuses);
            return new WaveRun(player, waves, pool, spawning.MinimumDistance, spawning.MaximumDistance);
        }

        private void OnValidate()
        {
            if (_waves == null || _waves.Length == 0)
                return;

            bool isValid = _waves.Length == WaveStatus.TotalWaves;
            foreach (var wave in _waves)
                isValid &= wave.ToDefinition().IsValid;
            if (!isValid)
                Debug.LogWarning("Combat waves need ten entries with positive limits, intervals and totals.", this);
        }
    }
}
