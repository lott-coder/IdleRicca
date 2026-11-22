using UnityEngine;

namespace IdleRicca.Presentation.Gameplay
{
    [CreateAssetMenu(menuName = "IdleRicca/Gameplay/Enemy spawning")]
    public sealed class EnemySpawnSettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int _maximumEnemies = 3;
        [SerializeField, Min(.1f), Tooltip("Minimum complete navigation path length to Ricca, in meters.")]
        private float _minimumDistance = 7f;
        [SerializeField, Min(.2f), Tooltip("Maximum complete navigation path length to Ricca, in meters.")]
        private float _maximumDistance = 14f;
        [SerializeField, Min(.1f)] private float _waveInterval = 1.5f;
        [SerializeField, Min(1)] private int _attemptsPerWave = 12;

        public int MaximumEnemies => _maximumEnemies;
        public float MinimumDistance => _minimumDistance;
        public float MaximumDistance => _maximumDistance;
        public float WaveInterval => _waveInterval;
        public int AttemptsPerWave => _attemptsPerWave;

        public bool IsValid => _maximumEnemies > 0 && _minimumDistance > 0f &&
            _maximumDistance > _minimumDistance && !float.IsInfinity(_maximumDistance * _maximumDistance) &&
            _waveInterval > 0f && !float.IsInfinity(_waveInterval) && _attemptsPerWave > 0;

        private void OnValidate()
        {
            _maximumEnemies = Mathf.Max(1, _maximumEnemies);
            _attemptsPerWave = Mathf.Max(1, _attemptsPerWave);
        }
    }
}
