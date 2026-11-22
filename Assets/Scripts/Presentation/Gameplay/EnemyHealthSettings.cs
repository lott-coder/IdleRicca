using UnityEngine;

namespace IdleRicca.Presentation.Gameplay
{
    /// <summary>可在敌人 Inspector 中选择的初始生命配置。</summary>
    public sealed class EnemyHealthSettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int _startingHp = 100;

        public int StartingHp => _startingHp;

        private void OnValidate()
        {
            if (_startingHp < 1)
                _startingHp = 1;
        }
    }
}
