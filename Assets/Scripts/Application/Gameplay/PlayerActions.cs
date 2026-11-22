using System;
using System.Collections.Generic;
using IdleRicca.Domain.Gameplay;

namespace IdleRicca.Application.Gameplay
{
    /// <summary>表现层提交普攻意图、动画采样和命中报告的入口。</summary>
    public interface IPlayerCommands
    {
        /// <summary>空闲时开始一次普攻，直到结束前锁定移动。</summary>
        bool TryStartAttack();

        /// <summary>打开本帧待处理区间。未进入动画时不生成批次。</summary>
        AttackSampleBatch BeginAttackFrame(bool inAttack, float normalizedTime, float deltaTime);

        /// <summary>提交当前批次的命中，一次攻击对同一目标最多扣血一次。</summary>
        AttackHit TryHit(AttackSampleBatch batch, EnemyHealth target);

        /// <summary>消费已打开的批次。返回本次判定是否到达攻击末尾。</summary>
        bool EndAttackFrame(AttackSampleBatch batch);

        /// <summary>主动取消挥击，同时作废已打开的批次。</summary>
        void CancelAttack();

        /// <summary>以非倒退的游戏时间推进结算，必要时结束续连窗口。</summary>
        void AdvanceCombatTime(double now);
    }

    /// <summary>Ricca 的本地控制及挥击状态。</summary>
    public sealed class PlayerActions : IPlayerCommands, IPlayerSkillCommands
    {
        public const double ComboWindowSeconds = 5.0;
        /// <summary>当前版本统一的技能蓝耗。</summary>
        public const int SkillManaCost = 10;
        /// <summary>未配置时，角色每局初始的蓝量上限。</summary>
        public const int DefaultStartingMp = 100;
        private const float AttackEntryTimeoutSeconds = 0.5f;

        private readonly HashSet<EnemyHealth> _hitTargets;
        private readonly int _baseDamage;
        private readonly int _baseMaxHp;
        private bool _waveControlsEnabled = true;
        private readonly float _activeStart;
        private readonly float _activeEnd;
        private bool _entered;
        private float _entryWait;
        private float _progress;
        private ulong _batchSequence;
        private AttackSampleBatch _openBatch;
        private double _combatTime;
        private double _lastComboHit;
        private int _comboCount;
        private ulong _comboHitSequence;
        private readonly PlayerSkillSet _skills;
        private ulong _selectionSequence;
        private ulong _selectionId;
        private ulong _castSequence;
        private SkillCast _skillCast;
        private double _castStartedAt;
        private double _lastCastSample;
        private double _blazeChargeUntil;

        /// <summary>以场景配置的只读数值初始化控制逻辑。</summary>
        public PlayerActions(int damage, float activeStart, float activeEnd, int expectedTargets = 8, int startingHp = 100)
        {
            if (damage <= 0)
                throw new ArgumentOutOfRangeException(nameof(damage));
            if (!IsFinite(activeStart) || !IsFinite(activeEnd) ||
                activeStart < 0f || activeEnd > 1f || activeStart >= activeEnd)
                throw new ArgumentOutOfRangeException(nameof(activeStart));

            if (startingHp <= 0)
                throw new ArgumentOutOfRangeException(nameof(startingHp));

            MaxMp = DefaultStartingMp;
            CurrentMp = MaxMp;
            MaxHp = startingHp;
            CurrentHp = startingHp;
            _baseDamage = damage;
            _baseMaxHp = startingHp;
            UnbuffedAttackDamage = damage;
            _activeStart = activeStart;
            _activeEnd = activeEnd;
            _hitTargets = new HashSet<EnemyHealth>(Math.Max(1, expectedTargets));
        }

        /// <summary>构造具有独立局内技能状态的玩家。</summary>
        public PlayerActions(int damage, float activeStart, float activeEnd, SkillDefinition[] skills,
            int expectedTargets = 8, int startingHp = 100)
            : this(damage, activeStart, activeEnd, skills, expectedTargets, startingHp, DefaultStartingMp)
        {
        }

        /// <summary>使用场景配置的初始满额 MP 创建技能角色。</summary>
        public PlayerActions(int damage, float activeStart, float activeEnd, SkillDefinition[] skills,
            int expectedTargets, int startingHp, int startingMp)
            : this(damage, activeStart, activeEnd, expectedTargets, startingHp)
        {
            if (startingMp <= 0)
                throw new ArgumentOutOfRangeException(nameof(startingMp));

            MaxMp = startingMp;
            CurrentMp = MaxMp;
            _skills = new PlayerSkillSet(skills);
        }

        public IPlayerCommands Commands => this;
        /// <summary>返回技能选择、施放和碰撞报告入口。</summary>
        public IPlayerSkillCommands SkillCommands => this;
        /// <summary>返回技能及选择快照。</summary>
        public SkillStatus SkillStatus => _skills?.GetStatus(_selectionId, CurrentMp) ?? default;
        /// <summary>指示是否已有在途技能动作。</summary>
        public bool IsCastingSkill => _skillCast.IsValid;
        private bool CanStartAction => CanControl && !IsAttacking && !IsCastingSkill;
        public bool IsAttacking { get; private set; }
        public int CurrentHp { get; private set; }
        public int MaxHp { get; private set; }
        /// <summary>本局当前可消耗的 MP。</summary>
        public int CurrentMp { get; private set; }
        /// <summary>本局角色配置的最大 MP。</summary>
        public int MaxMp { get; }
        internal int UnbuffedAttackDamage { get; private set; }
        /// <summary>普攻结算使用的伤害，包含本局奖励和当前火焰充能。</summary>
        public int AttackDamage => IsBlazeCharged
            ? SkillDefinition.GetChargedAttackDamage(UnbuffedAttackDamage)
            : UnbuffedAttackDamage;
        /// <summary>指示是否还在 Blaze Charge 的强化期间。</summary>
        public bool IsBlazeCharged => IsAlive && _combatTime < _blazeChargeUntil;
        /// <summary>当前生命和局内阶段是否允许战斗输入。</summary>
        public bool CanControl => _waveControlsEnabled && IsAlive;
        public bool IsAlive => CurrentHp > 0;

        /// <summary>Increments once for each accepted loss of HP.</summary>
        public ulong DamageSequence { get; private set; }

        /// <summary>仅在应播放受击或死亡动作时递增。</summary>
        public ulong DamageReactionSequence { get; private set; }

        public PlayerCombatStatus CombatStatus => new PlayerCombatStatus(
            CurrentHp, MaxHp, CurrentMp, MaxMp, _comboCount,
            _comboCount == 0 ? 0d : ComboWindowSeconds - (_combatTime - _lastComboHit),
            _comboHitSequence);

        internal AttackHit ReceiveEnemyDamage(int damage)
        {
            if (!CanControl || damage <= 0)
                return AttackHit.Ignored;

            bool protectedBySkill = IsCastingSkill;
            int appliedDamage = protectedBySkill ? HitPoints.ReduceDamageDuringSkillCast(damage) : damage;
            CurrentHp = HitPoints.AfterDamage(CurrentHp, appliedDamage);
            _comboCount = 0;
            DamageSequence++;

            if (!protectedBySkill || !IsAlive)
            {
                DamageReactionSequence++;
                CancelCombatIntent();
            }

            return IsAlive ? AttackHit.Damaged : AttackHit.Defeated;
        }

        /// <summary>查询当前可执行的世界平面移动。</summary>
        public MovementDirection GetMovement(float x, float y, float yaw, bool running)
        {
            return CanStartAction && _selectionId == 0 ? MovementDirection.FromInput(x, y, yaw, running) : default;
        }

        bool IPlayerCommands.TryStartAttack()
        {
            if (!CanStartAction || _selectionId != 0)
                return false;

            IsAttacking = true;
            return true;
        }

        AttackSampleBatch IPlayerCommands.BeginAttackFrame(bool inAttack, float normalizedTime, float deltaTime)
        {
            if (!IsAttacking || _openBatch.IsValid)
                return default;

            if (!inAttack || !IsFinite(normalizedTime))
            {
                if (IsFinite(deltaTime) && deltaTime > 0f)
                    _entryWait += deltaTime;
                if (_entered || _entryWait >= AttackEntryTimeoutSeconds)
                    ResetAttack();

                return default;
            }

            float progress = Math.Clamp(normalizedTime, 0f, 1f);
            if (progress < _progress)
            {
                ResetAttack();
                return default;
            }

            return OpenBatch(progress, _activeStart, _activeEnd);
        }

        AttackHit IPlayerCommands.TryHit(AttackSampleBatch batch, EnemyHealth target)
        {
            if (!IsAttacking)
                return AttackHit.Ignored;

            AttackHit result = ResolveHit(batch, target, AttackDamage);
            if (result != AttackHit.Ignored)
            {
                if (_comboCount < int.MaxValue)
                    _comboCount++;

                _lastComboHit = _combatTime;
                _comboHitSequence++;
            }

            return result;
        }

        bool IPlayerCommands.EndAttackFrame(AttackSampleBatch batch) => IsAttacking && FinishBatch(batch);

        void IPlayerCommands.CancelAttack()
        {
            if (IsAttacking)
                ResetAttack();
        }

        void IPlayerSkillCommands.SetUnavailableSkills(int mask)
        {
            _skills?.SetUnavailableSkills(mask);
            if (_skillCast.IsValid && (mask & (1 << (int)_skillCast.Definition.Id)) != 0)
                ResetAttack();
        }

        bool IPlayerSkillCommands.TryOpenSkillSelection()
        {
            if (_skills == null || !CanStartAction || _selectionId != 0)
                return false;

            _selectionId = ++_selectionSequence;
            return true;
        }

        bool IPlayerSkillCommands.CancelSkillSelection(ulong selectionId)
        {
            if (selectionId == 0 || selectionId != _selectionId)
                return false;

            _selectionId = 0;
            return true;
        }

        bool IPlayerSkillCommands.TryCastSkill(ulong selectionId, int slot, double now, out SkillCast cast)
        {
            cast = default;
            if (_skills == null || !CanStartAction || CurrentMp < SkillManaCost || selectionId == 0 || selectionId != _selectionId ||
                !IsValidTime(now, _combatTime) || !IsValidTime(now, _lastCastSample) ||
                !_skills.TryGetEquipped(slot, out SkillDefinition definition, out int level))
                return false;

            CurrentMp -= SkillManaCost;
            _selectionId = 0;
            _castStartedAt = now;
            _lastCastSample = now;
            _skillCast = new SkillCast(++_castSequence, definition, level);
            cast = _skillCast;
            return true;
        }

        bool IPlayerSkillCommands.CancelSkillCast(ulong castId)
        {
            if (!IsCastingSkill || _skillCast.Id != castId)
                return false;

            ResetAttack();
            return true;
        }

        AttackSampleBatch IPlayerSkillCommands.BeginSkillFrame(ulong castId, bool inSkill, float normalizedTime, double now)
        {
            if (!IsCastingSkill || _skillCast.Id != castId || !CanControl || _openBatch.IsValid)
                return default;

            SkillDefinition definition = _skillCast.Definition;
            double elapsed = now - _castStartedAt;
            if (!IsValidTime(now, _lastCastSample) || elapsed >= 2d * definition.Duration + AttackEntryTimeoutSeconds ||
                (inSkill && (!IsFinite(normalizedTime) || normalizedTime < _progress)) ||
                (!inSkill && (_entered || elapsed >= AttackEntryTimeoutSeconds)))
            {
                ResetAttack();
                return default;
            }

            _lastCastSample = now;
            return inSkill ? OpenBatch(Math.Min(normalizedTime, 1f), definition.ActiveStart, definition.ActiveEnd) : default;
        }

        AttackHit IPlayerSkillCommands.TryHitSkill(AttackSampleBatch batch, EnemyHealth target) =>
            IsCastingSkill && CanControl ? ResolveHit(batch, target, _skillCast.Damage) : AttackHit.Ignored;

        bool IPlayerSkillCommands.EndSkillFrame(AttackSampleBatch batch)
        {
            if (!IsCastingSkill)
                return false;

            SkillId skill = _skillCast.Definition.Id;
            if (!FinishBatch(batch))
                return false;

            if (skill == SkillId.BlazeCharge)
                StartBlazeCharge();
            return true;
        }

        void IPlayerCommands.AdvanceCombatTime(double now)
        {
            if (now < _combatTime || double.IsNaN(now) || double.IsInfinity(now))
                return;

            _combatTime = now;
            if (_comboCount > 0 && _combatTime - _lastComboHit >= ComboWindowSeconds)
                _comboCount = 0;
        }

        internal void SetWaveControls(bool enabled)
        {
            _waveControlsEnabled = enabled;
            if (!enabled)
            {
                // 清波可提前结束已打出伤害的突击动作，同时保留它的充能。
                if (IsAlive && _skillCast.Definition.Id == SkillId.BlazeCharge && _hitTargets.Count > 0)
                    StartBlazeCharge();
                CancelCombatIntent();
            }
        }

        internal void PrepareHitCapacity(int capacity) => _hitTargets.EnsureCapacity(capacity);

        internal bool CanOfferSkillReward => _skills == null || (CurrentMp >= SkillManaCost && _skills.CanReceiveReward);

        internal bool TryPrepareSkillReward(WaveReward reward, Random random, out SkillRewardPreview preview)
        {
            preview = default;
            return reward.Kind == WaveReward.RewardKind.Skill &&
                _skills != null && _skills.TryPreview(reward.Level, random, out preview);
        }

        internal bool TryApplyWaveReward(WaveReward reward, SkillRewardPreview preview)
        {
            switch (reward.Kind)
            {
                case WaveReward.RewardKind.Health:
                    MaxHp += reward.Amount;
                    CurrentHp += reward.Amount;
                    return true;
                case WaveReward.RewardKind.Attack:
                    UnbuffedAttackDamage += reward.Amount;
                    return true;
                case WaveReward.RewardKind.Skill:
                    return _skills == null || (CurrentMp >= SkillManaCost && _skills.TryReceive(reward.Level, preview));
                default:
                    return false;
            }
        }

        internal void ClearWaveRewards()
        {
            MaxHp = _baseMaxHp;
            CurrentHp = Math.Min(CurrentHp, MaxHp);
            UnbuffedAttackDamage = _baseDamage;
            _blazeChargeUntil = 0d;
            _skills?.Clear();
        }

        private void StartBlazeCharge() =>
            _blazeChargeUntil = _lastCastSample + SkillDefinition.BlazeChargeDurationSeconds;

        private AttackSampleBatch OpenBatch(float progress, float activeStart, float activeEnd)
        {
            float start = Math.Max(_progress, activeStart);
            float end = Math.Min(progress, activeEnd);
            bool canHit = start <= end && (!_entered || _progress < activeEnd) &&
                (!_entered || progress > _progress);
            _entered = true;
            _openBatch = new AttackSampleBatch(this, ++_batchSequence, _progress, progress, canHit, start, end);
            return _openBatch;
        }

        private AttackHit ResolveHit(AttackSampleBatch batch, EnemyHealth target, int damage)
        {
            if (!IsCurrent(batch) || !_openBatch.CanHit || target == null || !target.IsAlive ||
                !_hitTargets.Add(target))
                return AttackHit.Ignored;

            return target.ReceiveDamage(damage);
        }

        private bool FinishBatch(AttackSampleBatch batch)
        {
            if (!IsCurrent(batch))
                return false;

            _progress = _openBatch.Progress;
            _openBatch = default;
            if (_progress < 1f)
                return false;

            ResetAttack();
            return true;
        }

        private void CancelCombatIntent()
        {
            _selectionId = 0;
            if (IsAttacking || IsCastingSkill)
                ResetAttack();
        }

        private bool IsCurrent(AttackSampleBatch batch)
        {
            return _openBatch.IsValid && ReferenceEquals(this, batch.Owner) && _openBatch.Id == batch.Id;
        }

        private void ResetAttack()
        {
            IsAttacking = false;
            _skillCast = default;
            _entered = false;
            _entryWait = 0f;
            _progress = 0f;
            _openBatch = default;
            _hitTargets.Clear();
        }

        private static bool IsValidTime(double now, double previous) => now >= previous && !double.IsInfinity(now);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
