using System;
using System.Collections.Generic;

namespace IdleRicca.Application.Gameplay
{
    public enum EnemyKind
    {
        Goblin,
        Skeleton,
        Hellhound,
    }

    public readonly struct EnemySpawnCandidate
    {
        internal EnemySpawnCandidate(EnemyKind kind, float x, float z)
        {
            Kind = kind;
            X = x;
            Z = z;
        }

        public EnemyKind Kind { get; }
        public float X { get; }
        public float Z { get; }
    }

    public interface IEnemySpawnCommands
    {
        /// <summary>在最大导航距离的水平搜索半径内取候选点。</summary>
        EnemySpawnCandidate SampleCandidate();
        /// <summary>按完整导航路线的长度平方及场景检查结果分配名额。</summary>
        bool TryReserve(EnemyKind kind, float distanceSquared, bool onGround, bool clear, bool reachable, out ulong slot);
        bool ConfirmSpawn(ulong slot);
        bool Release(ulong slot);
    }

    /// <summary>Scene-local slots remain reserved until the view reports actual removal.</summary>
    public sealed class EnemyPopulation : IEnemySpawnCommands
    {
        private const int KindCount = 3;

        private readonly Dictionary<ulong, SpawnSlot> _slots;
        private readonly int _limit;
        private readonly float _minimumSquared;
        private readonly float _maximumSquared;
        private readonly Random _random;
        private int _openingKinds = (1 << KindCount) - 1;
        private int _nextKind;
        private ulong _nextSlot;

        public EnemyPopulation(int limit, float minimumDistance, float maximumDistance, int? seed = null)
        {
            if (limit < 1)
                throw new ArgumentOutOfRangeException(nameof(limit));
            if (!(minimumDistance > 0f && maximumDistance > minimumDistance) ||
                float.IsInfinity(maximumDistance * maximumDistance))
                throw new ArgumentOutOfRangeException(nameof(maximumDistance));

            _limit = limit;
            _minimumSquared = minimumDistance * minimumDistance;
            _maximumSquared = maximumDistance * maximumDistance;
            _slots = new Dictionary<ulong, SpawnSlot>(Math.Min(limit, 16));
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public IEnemySpawnCommands Commands => this;
        public int Count => _slots.Count;
        public bool IsFull => Count >= _limit;

        EnemySpawnCandidate IEnemySpawnCommands.SampleCandidate()
        {
            var kind = NextKind();
            double angle = _random.NextDouble() * (2d * Math.PI);
            double radius = Math.Sqrt(_maximumSquared * _random.NextDouble());
            return new EnemySpawnCandidate(kind, (float)(radius * Math.Cos(angle)), (float)(radius * Math.Sin(angle)));
        }

        bool IEnemySpawnCommands.TryReserve(EnemyKind kind, float distanceSquared, bool onGround,
            bool clear, bool reachable, out ulong slot)
        {
            slot = 0;
            if (IsFull || (uint)kind >= KindCount || !onGround || !clear || !reachable ||
                !(distanceSquared >= _minimumSquared && distanceSquared <= _maximumSquared))
                return false;

            slot = ++_nextSlot;
            _slots.Add(slot, new SpawnSlot(kind, null));
            return true;
        }

        bool IEnemySpawnCommands.ConfirmSpawn(ulong slot)
        {
            if (!_slots.TryGetValue(slot, out SpawnSlot entry))
                return false;

            _openingKinds &= ~(1 << (int)entry.Kind);
            return true;
        }

        bool IEnemySpawnCommands.Release(ulong slot) => Release(slot, out _);

        internal bool TryBindWaveHealth(ulong slot, EnemyHealth health, Action<ulong> defeated)
        {
            if (health == null || !_slots.TryGetValue(slot, out SpawnSlot entry) ||
                entry.Health != null || !health.TrackWaveDefeat(slot, defeated))
                return false;

            _slots[slot] = new SpawnSlot(entry.Kind, health);
            _openingKinds &= ~(1 << (int)entry.Kind);
            return true;
        }

        internal bool IsWaveDefeat(ulong slot) =>
            _slots.TryGetValue(slot, out SpawnSlot entry) && entry.Health != null && !entry.Health.IsAlive;

        internal bool Release(ulong slot, out bool wasDefeated)
        {
            wasDefeated = false;
            if (!_slots.TryGetValue(slot, out SpawnSlot entry))
                return false;

            if (entry.Health != null)
            {
                wasDefeated = !entry.Health.IsAlive;
                entry.Health.StopTrackingWaveDefeat();
            }

            return _slots.Remove(slot);
        }

        internal void FinishOpeningRoster() => _openingKinds = 0;

        private readonly struct SpawnSlot
        {
            public SpawnSlot(EnemyKind kind, EnemyHealth health)
            {
                Kind = kind;
                Health = health;
            }

            public EnemyKind Kind { get; }
            public EnemyHealth Health { get; }
        }

        private EnemyKind NextKind()
        {
            if (_openingKinds == 0)
                return (EnemyKind)_random.Next(KindCount);

            while ((_openingKinds & (1 << _nextKind)) == 0)
                _nextKind = (_nextKind + 1) % KindCount;

            int kind = _nextKind;
            _nextKind = (_nextKind + 1) % KindCount;
            return (EnemyKind)kind;
        }
    }
}
