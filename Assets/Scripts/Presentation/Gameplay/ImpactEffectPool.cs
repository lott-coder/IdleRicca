using UnityEngine;

namespace IdleRicca.Presentation.Gameplay
{
    /// <summary>预建世界空间的一次性粒子实例，供已结算的命中复用。</summary>
    internal sealed class ImpactEffectPool
    {
        private readonly GameObject _root;
        private readonly ParticleSystem[] _effects;
        private int _next;

        public ImpactEffectPool(ParticleSystem prefab, Transform owner, int capacity)
        {
            _root = new GameObject(prefab.name + " Effects");
            // 跟随角色所在场景卸载，播放中的粒子留在命中位置。
            _root.transform.SetParent(owner, false);
            _root.transform.SetParent(null, false);
            _root.SetActive(false);
            _effects = new ParticleSystem[capacity];
            for (int i = 0; i < capacity; i++)
            {
                ParticleSystem effect = Object.Instantiate(prefab, _root.transform);
                effect.gameObject.SetActive(false);
                _effects[i] = effect;
            }
            _root.SetActive(true);
        }

        public void Play(Vector3 position)
        {
            ParticleSystem effect = _effects[_next];
            _next = (_next + 1) % _effects.Length;
            if (effect.gameObject.activeSelf)
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            effect.transform.position = position;
            effect.gameObject.SetActive(true);
            effect.Play(true);
        }

        public void RetireCompleted()
        {
            foreach (ParticleSystem effect in _effects)
            {
                if (effect && effect.gameObject.activeSelf && !effect.IsAlive(true))
                    effect.gameObject.SetActive(false);
            }
        }

        public void Clear()
        {
            foreach (ParticleSystem effect in _effects)
            {
                if (effect && effect.gameObject.activeSelf)
                {
                    effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    effect.gameObject.SetActive(false);
                }
            }
        }

        public void Dispose()
        {
            if (_root)
                Object.Destroy(_root);
        }
    }
}
