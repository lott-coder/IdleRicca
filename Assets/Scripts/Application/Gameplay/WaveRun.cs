using System;
using IdleRicca.Domain.Gameplay;
using Phase = IdleRicca.Application.Gameplay.WaveStatus.RunPhase;

namespace IdleRicca.Application.Gameplay
{
    /// <summary>在当前场景内裁定波次、奖励与重开。</summary>
    public sealed class WaveRun
    {
        private readonly PlayerActions _player;
        private readonly WaveDefinition[] _waves;
        private readonly WaveRewardPool _rewards;
        private readonly EnemyPopulation _population;
        private readonly Action<ulong> _defeatCallback;
        private readonly WaveReward[] _offer = new WaveReward[WaveStatus.OfferSize];
        private readonly SkillRewardPreview[] _skillPreviews = new SkillRewardPreview[WaveStatus.OfferSize];
        private readonly Random _skillRandom;
        private Phase _phase;
        private Phase _beforeRestart;
        private int _wave;
        private int _outstanding;
        private int _defeated;
        private ulong _offerId;
        private double _lastAcceptedTime;
        private double _lastSpawnTime;
        private bool _hasSpawnedInWave;

        /// <summary>验证十波配置与奖励累加上限，并建立本局唯一的占位集合。</summary>
        public WaveRun(PlayerActions player, WaveDefinition[] waves, WaveRewardPool rewards,
            float minimumDistance, float maximumDistance, int? spawnSeed = null)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            _skillRandom = spawnSeed.HasValue ? new Random(spawnSeed.Value) : new Random();
            if (waves == null || waves.Length != WaveStatus.TotalWaves)
                throw new ArgumentException("A run requires exactly ten waves.", nameof(waves));

            _waves = (WaveDefinition[])waves.Clone();
            int highestLimit = 0;
            foreach (var wave in _waves)
            {
                if (!wave.IsValid)
                    throw new ArgumentException("Waves require positive sizes and finite spawn intervals.", nameof(waves));

                highestLimit = Math.Max(highestLimit, wave.MaximumEnemies);
            }

            if ((long)player.MaxHp + (long)rewards.MaximumHealthBonus * WaveStatus.TotalWaves > int.MaxValue ||
                (long)player.UnbuffedAttackDamage + (long)rewards.MaximumAttackBonus * WaveStatus.TotalWaves > int.MaxValue)
                throw new ArgumentException("The maximum reward stack must fit the combat stat range.", nameof(rewards));

            MaximumConfiguredEnemies = highestLimit;
            _player.PrepareHitCapacity(Math.Min(highestLimit, 16));
            _population = new EnemyPopulation(highestLimit, minimumDistance, maximumDistance, spawnSeed);
            _defeatCallback = OnEnemyDefeated;
        }

        /// <summary>技能系统可以用此事件接收本局已确认的技能类型与等级。</summary>
        public event Action<WaveReward> SkillRewardSelected;

        /// <summary>提供当前波号、阶段及可结算的选项。</summary>
        public WaveStatus Status => _phase == Phase.Reward
            ? new WaveStatus(_wave + 1, _phase, _offerId, _offer[0], _offer[1], _offer[2])
            : new WaveStatus(_wave + 1, _phase, 0, default, default, default);

        /// <summary>返回当前波的出怪参数。</summary>
        public WaveDefinition CurrentWave => _waves[_wave];
        /// <summary>包含待确认预约和未移除死亡模型的名额总数。</summary>
        public int OccupiedSlots => _population.Count;
        /// <summary>十波上限中的最大值，用于一次准备容量。</summary>
        public int MaximumConfiguredEnemies { get; }

        /// <summary>采样交给场景检查的敌人类型与候选位置。</summary>
        public EnemySpawnCandidate SampleCandidate() => _population.Commands.SampleCandidate();

        /// <summary>过滤无需进行 Unity 检查的生成请求。</summary>
        public bool CanSpawn(double now) => _phase == Phase.Fighting && _player.IsAlive && IsCurrentTime(now) &&
            (!_hasSpawnedInWave || now - _lastSpawnTime >= CurrentWave.SpawnInterval) &&
            OccupiedSlots < CurrentWave.MaximumEnemies && _outstanding < CurrentWave.TotalEnemies - _defeated;

        /// <summary>检查当前波配额和场景候选的安全条件后预约名额。</summary>
        public bool TryReserveSpawn(EnemyKind kind, float distanceSquared, bool onGround, bool clear, bool reachable,
            double now, out ulong slot)
        {
            slot = 0;
            if (!CanSpawn(now) || !_population.Commands.TryReserve(kind, distanceSquared, onGround, clear, reachable, out slot))
                return false;

            _outstanding++;
            _lastAcceptedTime = now;
            return true;
        }

        /// <summary>将已绑定场景的敌人生命接到名额，成功后开始刷怪间隔。</summary>
        public bool ConfirmSpawn(ulong slot, EnemyHealth health, double now)
        {
            if (_phase != Phase.Fighting || !_player.IsAlive || !IsCurrentTime(now) ||
                (_hasSpawnedInWave && now - _lastSpawnTime < CurrentWave.SpawnInterval) ||
                !_population.TryBindWaveHealth(slot, health, _defeatCallback))
                return false;

            _player.PrepareHitCapacity(OccupiedSlots);
            _hasSpawnedInWave = true;
            _lastSpawnTime = now;
            _lastAcceptedTime = now;
            return true;
        }

        /// <summary>回收实例名额。只有未击败的预约或活敌会返还击败配额。</summary>
        public bool Release(ulong slot)
        {
            if (!_population.Release(slot, out bool wasDefeated))
                return false;

            if (!wasDefeated)
                _outstanding--;
            return true;
        }

        /// <summary>在本帧双方伤害都结算后先处理死亡，再裁定通过。</summary>
        public void Advance()
        {
            if (_phase != Phase.Fighting && _phase != Phase.Reward)
                return;

            if (!_player.IsAlive)
            {
                _phase = Phase.Defeated;
                _wave = 0;
                _player.SetWaveControls(false);
                _player.ClearWaveRewards();
                return;
            }

            if (_phase != Phase.Fighting || _defeated != CurrentWave.TotalEnemies)
                return;

            _rewards.Draw(_offer, _player.CanOfferSkillReward);
            for (int i = 0; i < _offer.Length; i++)
                _player.TryPrepareSkillReward(_offer[i], _skillRandom, out _skillPreviews[i]);
            _offerId++;
            _phase = Phase.Reward;
            _player.SetWaveControls(false);
        }

        /// <summary>消费当前组的一个选项。技能通知在提交状态后发布。</summary>
        public bool TryChooseReward(ulong offerId, int index, double now)
        {
            if (_phase != Phase.Reward || !_player.IsAlive || offerId != _offerId ||
                (uint)index >= WaveStatus.OfferSize || !IsCurrentTime(now))
                return false;

            WaveReward chosen = _offer[index];
            if (!_player.TryApplyWaveReward(chosen, _skillPreviews[index]))
                return false;

            _lastAcceptedTime = now;
            if (_wave + 1 == _waves.Length)
                _phase = Phase.Won;
            else
            {
                _wave++;
                _defeated = 0;
                _hasSpawnedInWave = false;
                _population.FinishOpeningRoster();
                _phase = Phase.Fighting;
                _player.SetWaveControls(true);
            }

            if (chosen.Kind == WaveReward.RewardKind.Skill)
                SkillRewardSelected?.Invoke(chosen);

            return true;
        }

        /// <summary>读取本组技能奖励预计的授予或升级结果。</summary>
        public bool TryGetSkillRewardPreview(ulong offerId, int index, out SkillRewardPreview preview)
        {
            preview = default;
            if (_phase != Phase.Reward || !_player.IsAlive || offerId != _offerId ||
                (uint)index >= WaveStatus.OfferSize)
                return false;

            preview = _skillPreviews[index];
            return preview.Skill != SkillId.None;
        }

        /// <summary>仅从死亡或通关状态接受一个重载请求。</summary>
        public bool TryBeginRestart()
        {
            if (_phase != Phase.Defeated && _phase != Phase.Won)
                return false;

            _beforeRestart = _phase;
            _phase = Phase.Restarting;
            return true;
        }

        /// <summary>Unity 未能发出重载请求时恢复原结局。</summary>
        public void CancelRestart()
        {
            if (_phase == Phase.Restarting)
                _phase = _beforeRestart;
        }

        private void OnEnemyDefeated(ulong slot)
        {
            // 回调仅由绑定在此名额的生命对象在首次归零时发出。
            if (!_population.IsWaveDefeat(slot))
                return;

            _outstanding--;
            _defeated++;
        }

        private bool IsCurrentTime(double now) => now >= _lastAcceptedTime && !double.IsInfinity(now);
    }
}
