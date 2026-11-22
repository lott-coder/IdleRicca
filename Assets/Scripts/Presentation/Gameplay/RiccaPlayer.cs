using System.Collections.Generic;
using IdleRicca.Application.Gameplay;
using IdleRicca.Domain.Gameplay;
using IdleRicca.Presentation.UI;
using UnityEngine;
using UnityApplication = UnityEngine.Application;

namespace IdleRicca.Presentation.Gameplay
{
    /// <summary>一帧键鼠意图，供真实设备与自动化验证共用。</summary>
    public readonly struct RiccaInputFrame
    {
        /// <summary>记录平面输入、鼠标增量、持续奔跑和单次攻击。</summary>
        public RiccaInputFrame(float strafe, float forward, Vector2 look, bool run, bool attackPressed)
            : this(strafe, forward, look, run, attackPressed, false, false)
        {
        }

        /// <summary>构造包含技能选择与取消意图的输入帧。</summary>
        public RiccaInputFrame(float strafe, float forward, Vector2 look, bool run, bool attackPressed,
            bool skillPressed, bool cancelPressed)
        {
            Strafe = strafe;
            Forward = forward;
            Look = look;
            Run = run;
            AttackPressed = attackPressed;
            SkillPressed = skillPressed;
            CancelPressed = cancelPressed;
        }

        public float Strafe { get; }
        public float Forward { get; }
        public Vector2 Look { get; }
        public bool Run { get; }
        public bool AttackPressed { get; }
        /// <summary>本帧是否按下技能选择键。</summary>
        public bool SkillPressed { get; }
        /// <summary>本帧是否请求关闭技能选择。</summary>
        public bool CancelPressed { get; }
    }

    /// <summary>Ricca 的输入生产端。</summary>
    public interface IRiccaInput
    {
        /// <summary>读取一帧输入。</summary>
        RiccaInputFrame Read();
    }

    /// <summary>将本地控制命令接入 Unity 胶囊、Animator 与剑身查询。</summary>
    [DisallowMultipleComponent]
    public sealed class RiccaPlayer : MonoBehaviour
    {
        private static readonly int Moving = Animator.StringToHash("Moving");
        private static readonly int RunBlend = Animator.StringToHash("RunBlend");
        private static readonly int HoldAttack = Animator.StringToHash("HoldAttack");
        private static readonly int Attack = Animator.StringToHash("Base Layer.Attack");
        private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
        private static readonly int Hit = Animator.StringToHash("Base Layer.Hit");
        private static readonly int Death = Animator.StringToHash("Base Layer.Death");

        // R4 的可调步行与奔跑速度，单位为 Unity 米/秒。
        [SerializeField, Min(0.01f)] private float _walkSpeed = 2.1f;
        [SerializeField, Min(0.02f)] private float _runSpeed = 4.5f;
        [SerializeField, Min(0.01f)] private float _turnSpeed = 900f;
        [SerializeField, Min(1)] private int _startingHp = 100;
        [SerializeField, Min(1)] private int _startingMp = PlayerActions.DefaultStartingMp;
        [SerializeField] private CharacterController _controller;
        [SerializeField] private Animator _animator;
        [SerializeField] private SwordHitbox _sword;
        [SerializeField] private NormalAttackSettings _attackSettings;
        [SerializeField] private RiccaSkillSettings _skillSettings;
        [SerializeField] private SkillHitbox _skillHitbox;
        [SerializeField] private ParticleSystem _attackImpact;
        [SerializeField] private ParticleSystem _damageImpact;

        private const float GroundStickSpeed = -2f;
        private const float TerminalSpeed = -35f;
        private const float AttackExitBlendSeconds = 0.08f;
        private const int AttackImpactCapacity = 16;
        private const int DamageImpactCapacity = 8;
        private ThirdPersonCamera _view;
        private PlayerActions _actions;
        private IPlayerCommands _commands;
        private IRiccaInput _inputOverride;
        private Vector3 _previousAttackRoot;
        private float _verticalSpeed;
        private bool _ownsCursor;
        private int _blockedInputFrame = -1;
        private bool _pendingCursorCapture;
        private bool _driverHoldingAttack;
        private AnimationHitStop _hitStop;
        private ImpactEffectPool _attackImpacts;
        private ImpactEffectPool _damageImpacts;
        private ulong _presentedDamageReaction;
        private IPlayerSkillCommands _skillCommands;
        private SkillSelectionHud _skillHud;
        private ulong _shownSkillSelection;
        private bool _ownsBulletTime;
        private float _previousTimeScale;
        private float _previousFixedDeltaTime;
        private float _appliedTimeScale;
        private float _appliedFixedDeltaTime;
        private SkillCast _playingSkill;
        private RiccaSkillSettings.SkillView _playingSkillView;
        private int _playingSkillState;
        private bool _playedSkillEffect;
        private Vector3 _skillOrigin;
        private Quaternion _skillRotation;
        private Vector3 _skillTravel;
        private Quaternion _skillFacing;
        private bool _skillLaunchPending;
        private bool _skillLaunchBlocked;
        private RuntimeAnimatorController _skillController;
        private RiccaSkillSettings.SkillView[] _configuredSkillViews;
        private Dictionary<AnimationClip, int> _skillStateHashes;
        private GameObject[] _skillEffects;
        private ParticleSystem[][] _skillParticles;
        private string[,] _skillDetails;

        public int CurrentHp => _actions?.CurrentHp ?? 0;
        public bool IsAlive => _actions != null && _actions.IsAlive;
        internal PlayerActions Actions => _actions;
        internal CharacterController ContactCollider => _controller;

        public bool IsAttacking => _actions != null && _actions.IsAttacking;

        /// <summary>Returns the current local combat status after Ricca is initialized and active.</summary>
        public bool TryGetCombatStatus(out PlayerCombatStatus status)
        {
            if (isActiveAndEnabled && _actions != null)
            {
                status = _actions.CombatStatus;
                return true;
            }

            status = default;
            return false;
        }

        /// <summary>在角色有效时返回技能快照。</summary>
        public bool TryGetSkillStatus(out SkillStatus status)
        {
            status = isActiveAndEnabled && _actions != null ? _actions.SkillStatus : default;
            return status.IsEnabled;
        }

        /// <summary>查询预先缓存的技能等级及范围说明。</summary>
        public string GetSkillDetails(SkillId id, int level)
        {
            int index = (int)id - 1;
            return _skillDetails != null && (uint)index < (uint)_skillDetails.GetLength(0) &&
                level >= 1 && level <= WaveRewardPool.Levels ? _skillDetails[index, level - 1] : string.Empty;
        }

        /// <summary>消费当前选择并播放已获准的技能。</summary>
        public bool TryChooseSkill(ulong selectionId, int slot)
        {
            if (!isActiveAndEnabled || _skillCommands == null || selectionId == 0 ||
                selectionId != _shownSkillSelection)
                return false;

            float restoredScale = _ownsBulletTime && Time.timeScale == _appliedTimeScale
                ? _previousTimeScale : Time.timeScale;
            float restoredStep = _ownsBulletTime && Time.fixedDeltaTime == _appliedFixedDeltaTime
                ? _previousFixedDeltaTime : Time.fixedDeltaTime;
            if (!PositiveFinite(restoredScale) || !PositiveFinite(restoredStep))
            {
                CancelSkillSelection(selectionId);
                return false;
            }

            RefreshSkillAvailability();
            SkillStatus status = _actions.SkillStatus;
            if (!status.IsSelecting || !status.CanCastSlot(slot))
                return false;

            SkillId skill = status.GetSkillId(slot);
            RiccaSkillSettings.SkillView view = _configuredSkillViews[(int)skill - 1];

            var clip = view.ResolveAnimation(_controller.isGrounded, _view ? _view.Pitch : 0f, out float pitch);
            if (!clip || !_skillStateHashes.TryGetValue(clip, out int state) || !_animator.HasState(0, state) ||
                !_skillCommands.TryCastSkill(selectionId, slot, Time.timeAsDouble, out SkillCast cast))
                return false;

            CloseSkillSelectionPresentation();
            _playingSkill = cast;
            _playingSkillView = view;
            _playingSkillState = state;
            _skillOrigin = transform.position;
            _skillFacing = transform.rotation;
            _skillRotation = _skillFacing * Quaternion.Euler(pitch, 0f, 0f);
            _skillTravel = Vector3.zero;
            _skillLaunchPending = true;
            _skillLaunchBlocked = false;
            _playedSkillEffect = false;
            _animator.SetBool(Moving, false);
            _animator.Play(_playingSkillState, 0, 0f);
            _animator.Update(0f);
            return true;
        }

        /// <summary>关闭对应选择并释放本次减速。</summary>
        public bool CancelSkillSelection(ulong selectionId)
        {
            if (_skillCommands == null || selectionId == 0 || selectionId != _shownSkillSelection)
                return false;

            bool cancelled = _skillCommands.CancelSkillSelection(selectionId);
            CloseSkillSelectionPresentation();
            return cancelled;
        }

        internal bool BindSkillHud(SkillSelectionHud hud)
        {
            if (!hud || hud.gameObject.scene != gameObject.scene || (_skillHud && _skillHud != hud))
                return false;

            _skillHud = hud;
            return true;
        }

        internal void UnbindSkillHud(SkillSelectionHud hud)
        {
            if (_skillHud != hud)
                return;

            CancelSkillSelection(_shownSkillSelection);
            _skillHud = null;
        }

        internal void ApplySkillInterruptions()
        {
            if (_shownSkillSelection != 0 && !_actions.SkillStatus.IsSelecting)
                CloseSkillSelectionPresentation();

            if (_playingSkill.IsValid && !_actions.IsCastingSkill)
                EndSkillPresentation(false);
        }

        internal void ShowReceivedHit(Vector3 position)
        {
            _damageImpacts?.Play(position);
            ApplySkillInterruptions();
        }

        /// <summary>可替换设备采集，仍使用角色的同一控制和结算路径。</summary>
        public void UseInputSource(IRiccaInput source)
        {
            _inputOverride = source;
            if (source != null)
                ReleaseCursor();
            else if (_view && isActiveAndEnabled && UnityApplication.isFocused)
                CaptureCursor();
        }

        internal bool HasFinishedDeathAnimation
        {
            get
            {
                if (IsAlive || !_animator || !_animator.isActiveAndEnabled)
                    return false;

                AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
                return state.fullPathHash == Death && state.normalizedTime >= 1f;
            }
        }

        internal void ApplyWaveInputState()
        {
            ApplySkillInterruptions();
            _blockedInputFrame = Time.frameCount;
            _pendingCursorCapture = _actions != null && _actions.CanControl;
            if (!_pendingCursorCapture)
                ReleaseCursor();
        }

        private void Awake()
        {
            if (!_controller || !_animator || !_sword || !_sword.IsConfigured ||
                !_attackSettings || !_attackSettings.HasValidTrajectory() ||
                !_attackSettings.MatchesController(_animator.runtimeAnimatorController) ||
                !PositiveFinite(_walkSpeed) || !PositiveFinite(_runSpeed) || _runSpeed <= _walkSpeed ||
                !PositiveFinite(_turnSpeed) || _startingHp <= 0 || _startingMp <= 0)
            {
                Debug.LogError("Ricca requires valid controller, Animator, blade and movement settings.", this);
                enabled = false;
                return;
            }

            if (_skillSettings)
            {
                SkillDefinition[] definitions = System.Array.Empty<SkillDefinition>();
                if (!_skillHitbox || !_skillHitbox.IsConfigured)
                    Debug.LogError("Ricca skill catalog is unavailable: missing skill hitbox or collision layers.", this);
                else if (!_skillSettings.TryBuild(_animator, out definitions, out _skillStateHashes))
                {
                    Debug.LogError("Ricca skill catalog is unavailable: invalid Animator, timing or catalog.", this);
                    definitions = System.Array.Empty<SkillDefinition>();
                }

                _skillController = _animator.runtimeAnimatorController;
                _actions = new PlayerActions(_attackSettings.Damage, _attackSettings.ActiveStart,
                    _attackSettings.ActiveEnd, definitions, expectedTargets: 8, startingHp: _startingHp, startingMp: _startingMp);
                _skillCommands = _actions.SkillCommands;
                PrepareSkillPresentation(definitions);
                RefreshSkillAvailability();
            }
            else
            {
                _actions = new PlayerActions(_attackSettings.Damage, _attackSettings.ActiveStart,
                    _attackSettings.ActiveEnd, startingHp: _startingHp);
            }

            _commands = _actions.Commands;
            if (_attackImpact)
            {
                _attackImpacts = new ImpactEffectPool(_attackImpact, transform, AttackImpactCapacity);
                _sword.BindImpactEffects(_attackImpacts);
                if (_skillHitbox)
                    _skillHitbox.BindImpactEffects(_attackImpacts);
            }
            if (_damageImpact)
                _damageImpacts = new ImpactEffectPool(_damageImpact, transform, DamageImpactCapacity);
        }

        internal void BindCamera(ThirdPersonCamera view)
        {
            _view = view;
            if (isActiveAndEnabled && UnityApplication.isFocused && _inputOverride == null)
                CaptureCursor();
        }

        internal void UnbindCamera(ThirdPersonCamera view)
        {
            if (_view != view)
                return;

            _view = null;
            CancelSkillSelection(_shownSkillSelection);
            ReleaseCursor();
        }

        private void OnEnable()
        {
            if (_view && UnityApplication.isFocused && _inputOverride == null)
                CaptureCursor();
        }

        private void OnDisable()
        {
            _attackImpacts?.Clear();
            _damageImpacts?.Clear();
            CancelSkillSelection(_shownSkillSelection);
            if (_playingSkill.IsValid)
            {
                _skillCommands.CancelSkillCast(_playingSkill.Id);
                EndSkillPresentation(false);
            }
            SetSkillEffectActive(SkillId.BlazeCharge, false);
            _hitStop.Release(_animator);
            ReleaseCursor();
            _commands?.CancelAttack();
            ReleaseAnimator(false);
        }

        private void OnDestroy()
        {
            _attackImpacts?.Dispose();
            _damageImpacts?.Dispose();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                CancelSkillSelection(_shownSkillSelection);
                ReleaseCursor();
            }
            else if (_view && isActiveAndEnabled && _inputOverride == null)
                CaptureCursor();
        }

        private void Update()
        {
            _attackImpacts?.RetireCompleted();
            _damageImpacts?.RetireCompleted();
            _hitStop.RestoreIfElapsed(_animator);
            _commands?.AdvanceCombatTime(Time.timeAsDouble);
            RefreshSkillAvailability();
            ApplySkillInterruptions();
            SetSkillEffectActive(SkillId.BlazeCharge, _actions?.IsBlazeCharged == true ||
                (_playedSkillEffect && _playingSkill.Definition.Id == SkillId.BlazeCharge));
            bool reactionStarted = PlayDamageReactionIfNeeded();
            if (!_view || _actions == null)
                return;

            RiccaInputFrame input = default;
            if (_actions.CanControl && Time.frameCount > _blockedInputFrame)
            {
                if (_pendingCursorCapture)
                {
                    _pendingCursorCapture = false;
                    if (_inputOverride == null && UnityApplication.isFocused)
                        CaptureCursor();
                }

                input = _inputOverride != null ? _inputOverride.Read() : ReadDevice();
                if (_shownSkillSelection != 0)
                {
                    if (input.SkillPressed || input.CancelPressed || !PositiveFinite(Time.timeScale))
                        CancelSkillSelection(_shownSkillSelection);
                    input = default;
                }
                else if (!reactionStarted && input.SkillPressed && TryOpenSkillSelection())
                {
                    input = default;
                }
                _view.Look(input.Look);
            }

            bool animatorEnabled = _animator && _animator.isActiveAndEnabled;
            if (animatorEnabled && _driverHoldingAttack && !_actions.IsAttacking)
                ReleaseAnimator(true);

            if (animatorEnabled && !reactionStarted && input.AttackPressed && _commands.TryStartAttack())
            {
                _previousAttackRoot = transform.position;
                _animator.SetBool(HoldAttack, true);
                _animator.Play(Attack, 0, 0f);
                // Animate Physics 可能还在返回上一挥的末帧。立即求值以开始本挥。
                _animator.Update(0f);
                _driverHoldingAttack = true;
            }

            var movement = _actions.GetMovement(input.Strafe, input.Forward, _view.Yaw, input.Run);
            var direction = new Vector3(movement.X, 0f, movement.Z);
            float speed = movement.IsRunning ? _runSpeed : _walkSpeed;
            float deltaTime = Time.deltaTime;
            if (movement.IsMoving)
            {
                Quaternion target = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeed * deltaTime);
            }

            _verticalSpeed = _controller.isGrounded
                ? GroundStickSpeed
                : Mathf.Max(TerminalSpeed, _verticalSpeed + Physics.gravity.y * deltaTime);
            Vector3 velocity = direction * speed + Vector3.up * _verticalSpeed;
            _controller.Move(velocity * deltaTime);
            if (animatorEnabled)
            {
                _animator.SetFloat(RunBlend, movement.IsRunning ? 1f : 0f);
                _animator.SetBool(Moving, movement.IsMoving);
            }
        }

        private void LateUpdate()
        {
            if (_playingSkill.IsValid)
            {
                TickSkill();
                return;
            }

            if (!_driverHoldingAttack)
                return;

            if (!_animator || !_animator.isActiveAndEnabled)
            {
                _commands.CancelAttack();
                return;
            }

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            bool inAttack = state.fullPathHash == Attack;
            AttackSampleBatch batch = _commands.BeginAttackFrame(inAttack,
                state.normalizedTime, Time.deltaTime);
            Vector3 currentRoot = transform.position;
            if (batch.IsValid)
            {
                if (batch.CanHit && _sword.Resolve(batch, _attackSettings, _commands, _previousAttackRoot, currentRoot))
                    _hitStop.Apply(_animator, _attackSettings);

                if (_commands.EndAttackFrame(batch))
                    ReleaseAnimator(true);

                _previousAttackRoot = currentRoot;
            }
            else if (!_actions.IsAttacking)
            {
                ReleaseAnimator(false);
            }
        }

        private bool PlayDamageReactionIfNeeded()
        {
            if (_actions == null || !_animator || !_animator.isActiveAndEnabled)
                return false;

            bool newReaction = _presentedDamageReaction != _actions.DamageReactionSequence;
            if (!newReaction && (_actions.IsAlive || _animator.GetCurrentAnimatorStateInfo(0).fullPathHash == Death))
                return false;

            _presentedDamageReaction = _actions.DamageReactionSequence;
            ReleaseAnimator(false);
            _animator.SetBool(Moving, false);
            // Unity may reset an Animator when its GameObject is reactivated.
            _animator.Play(_actions.IsAlive ? Hit : Death, 0, newReaction ? 0f : 1f);
            _animator.Update(0f);
            return true;
        }

        private RiccaInputFrame ReadDevice()
        {
            if (!UnityApplication.isFocused)
                return default;

            bool cancelPressed = Input.GetKeyDown(KeyCode.Escape);
            bool skillPressed = _skillCommands != null && Input.GetKeyDown(KeyCode.F);
            if (skillPressed || _shownSkillSelection != 0)
                return new RiccaInputFrame(0f, 0f, default, false, false, skillPressed, cancelPressed);

            if (cancelPressed)
            {
                ReleaseCursor();
                return default;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Input.GetMouseButtonDown(0))
                    CaptureCursor();

                return default;
            }

            float horizontal = Axis(KeyCode.A, KeyCode.D);
            float forward = Axis(KeyCode.S, KeyCode.W);
            bool run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            return new RiccaInputFrame(horizontal, forward, look, run, Input.GetMouseButtonDown(0));
        }

        private void CaptureCursor()
        {
            if (_actions == null || !_actions.CanControl || _shownSkillSelection != 0 ||
                Time.frameCount <= _blockedInputFrame)
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _ownsCursor = true;
        }

        private void ReleaseCursor()
        {
            if (!_ownsCursor)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _ownsCursor = false;
        }

        private void ReleaseAnimator(bool completed)
        {
            if (!_driverHoldingAttack)
                return;

            _driverHoldingAttack = false;
            if (!_animator || !_animator.isActiveAndEnabled)
                return;

            _animator.SetBool(HoldAttack, false);
            if (completed)
                _animator.CrossFadeInFixedTime(Idle, AttackExitBlendSeconds, 0);
        }

        private void PrepareSkillPresentation(SkillDefinition[] definitions)
        {
            int skillIdCount = (int)SkillId.HolyGeyser;
            _configuredSkillViews = new RiccaSkillSettings.SkillView[skillIdCount];
            _skillEffects = new GameObject[skillIdCount];
            _skillParticles = new ParticleSystem[skillIdCount][];
            _skillDetails = new string[skillIdCount, WaveRewardPool.Levels];
            foreach (SkillDefinition definition in definitions)
            {
                RiccaSkillSettings.SkillView view = _skillSettings.GetView(definition.Id);
                int index = (int)definition.Id - 1;
                _configuredSkillViews[index] = view;
                if (view.EffectRoot)
                {
                    bool isBlazeCharge = definition.Id == SkillId.BlazeCharge;
                    BoxCollider blade = _sword.Geometry;
                    GameObject effect = Instantiate(view.EffectRoot, isBlazeCharge ? blade.transform : _skillHitbox.transform);
                    if (isBlazeCharge)
                        effect.transform.SetLocalPositionAndRotation(blade.center, Quaternion.identity);
                    effect.SetActive(false);
                    _skillEffects[index] = effect;
                    _skillParticles[index] = effect.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (ParticleSystem particle in _skillParticles[index])
                    {
                        var main = particle.main;
                        main.playOnAwake = false;
                        main.stopAction = ParticleSystemStopAction.None;
                        if (isBlazeCharge)
                            main.simulationSpace = ParticleSystemSimulationSpace.Local;
                        particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }

                for (int level = 1; level <= WaveRewardPool.Levels; level++)
                    _skillDetails[index, level - 1] = $"Lv.{level}  /  {view.RangeHint} ";
            }
        }

        private void RefreshSkillAvailability()
        {
            if (_skillCommands == null)
                return;

            if (!_animator || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController != _skillController ||
                !_skillSettings || !_skillHitbox || !_skillHitbox.isActiveAndEnabled || !_controller || !_controller.enabled)
            {
                _skillCommands.SetUnavailableSkills(-1);
            }
            else
            {
                int unavailable = 0;
                for (int i = 0; i < _configuredSkillViews.Length; i++)
                {
                    var view = _configuredSkillViews[i];
                    if (view != null && (!view.Animation || (view.RequiresEffect && (!view.EffectRoot || !_skillEffects[i]))))
                        unavailable |= 1 << (int)view.Id;
                }
                _skillCommands.SetUnavailableSkills(unavailable);
            }

            if (_ownsBulletTime && !_actions.SkillStatus.IsUsingBulletTime)
                RestoreSkillTime();
        }

        private bool TryOpenSkillSelection()
        {
            if (_skillCommands == null || !_skillHud || !_skillHud.isActiveAndEnabled ||
                !PositiveFinite(Time.timeScale) ||
                !PositiveFinite(Time.fixedDeltaTime) || !_skillCommands.TryOpenSkillSelection())
                return false;

            SkillStatus status = _actions.SkillStatus;
            _shownSkillSelection = status.SelectionId;
            ReleaseCursor();
            if (status.IsUsingBulletTime)
            {
                _previousTimeScale = Time.timeScale;
                _previousFixedDeltaTime = Time.fixedDeltaTime;
                float scale = _previousTimeScale * _skillSettings.SlowMultiplier;
                float step = _previousFixedDeltaTime * _skillSettings.SlowMultiplier;
                if (!PositiveFinite(scale) || !PositiveFinite(step))
                {
                    CancelSkillSelection(_shownSkillSelection);
                    return false;
                }
                _ownsBulletTime = true;
                _appliedTimeScale = scale;
                _appliedFixedDeltaTime = step;
                Time.timeScale = scale;
                Time.fixedDeltaTime = step;
            }

            return true;
        }

        private void CloseSkillSelectionPresentation()
        {
            _shownSkillSelection = 0;
            RestoreSkillTime();
            _blockedInputFrame = Time.frameCount;
            _pendingCursorCapture = _actions != null && _actions.CanControl;
        }

        private void RestoreSkillTime()
        {
            if (!_ownsBulletTime)
                return;

            _ownsBulletTime = false;
            if (Time.timeScale == _appliedTimeScale)
                Time.timeScale = _previousTimeScale;
            if (Time.fixedDeltaTime == _appliedFixedDeltaTime)
                Time.fixedDeltaTime = _previousFixedDeltaTime;
        }

        private void TickSkill()
        {
            if (!_animator || !_animator.isActiveAndEnabled || !_skillHitbox ||
                !_skillHitbox.isActiveAndEnabled || !_controller || !_controller.enabled || !PositiveFinite(Time.timeScale))
            {
                _skillCommands.CancelSkillCast(_playingSkill.Id);
                EndSkillPresentation(false);
                return;
            }

            if (!_actions.IsCastingSkill)
            {
                EndSkillPresentation(false);
                return;
            }

            var state = _animator.GetCurrentAnimatorStateInfo(0);
            var batch = _skillCommands.BeginSkillFrame(_playingSkill.Id, state.fullPathHash == _playingSkillState,
                state.normalizedTime, Time.timeAsDouble);
            if (!batch.IsValid)
            {
                if (!_actions.IsCastingSkill)
                    EndSkillPresentation(false);
                return;
            }

            var view = _playingSkillView;
            Vector3 centerOffset = _skillFacing * view.Center;
            Vector3 launchBase = _skillOrigin + Vector3.up * centerOffset.y;
            Vector3 from = _skillOrigin + centerOffset + _skillTravel;
            Vector3 to = from;
            Vector3 halfSize = view.Size * .5f;
            if (batch.CanHit && view.Mode != RiccaSkillSettings.MotionMode.Stationary)
            {
                bool dash = view.Mode == RiccaSkillSettings.MotionMode.Dash;
                Vector3 worldHalfSize = dash ? halfSize - Vector3.up * (.05f * halfSize.y) : halfSize;
                if (_skillLaunchPending)
                {
                    _skillLaunchPending = false;
                    Vector3 launch = _skillHitbox.LimitTravel(launchBase, from - launchBase, worldHalfSize, _skillRotation, transform);
                    _skillLaunchBlocked = (from - launch).sqrMagnitude > .000001f;
                }

                if (!_skillLaunchBlocked)
                {
                    SkillDefinition definition = _playingSkill.Definition;
                    float start = Mathf.InverseLerp(definition.ActiveStart, definition.ActiveEnd, batch.Start);
                    float end = Mathf.InverseLerp(definition.ActiveStart, definition.ActiveEnd, batch.End);
                    Vector3 step = (_skillRotation * Vector3.forward) * (view.TravelDistance * (end - start));
                    if (dash)
                    {
                        from = transform.position + centerOffset;
                        Vector3 allowed = _skillHitbox.LimitTravel(from, step, worldHalfSize, _skillRotation, transform);
                        _controller.Move(allowed - from);
                        to = transform.position + centerOffset;
                    }
                    else
                    {
                        to = _skillHitbox.LimitTravel(from, step, worldHalfSize, _skillRotation, transform);
                    }

                    _skillTravel += to - from;
                }
            }

            if (_skillLaunchBlocked)
                from = to = launchBase;

            UpdateSkillEffect(batch, from, to, view);
            if (!_skillLaunchBlocked)
                _skillHitbox.Resolve(from, to, halfSize, _skillRotation, batch, _skillCommands);
            if (_skillCommands.EndSkillFrame(batch))
                EndSkillPresentation(true);
        }

        private void UpdateSkillEffect(AttackSampleBatch batch, Vector3 from, Vector3 to,
            RiccaSkillSettings.SkillView view)
        {
            SkillId skill = _playingSkill.Definition.Id;
            GameObject effect = _skillEffects[(int)skill - 1];
            if (!effect || batch.Progress < view.EffectAt)
                return;

            if (skill == SkillId.BlazeCharge)
            {
                _playedSkillEffect = true;
                SetSkillEffectActive(skill, true);
                return;
            }

            Vector3 offset = Vector3.up * (view.EffectHeight - view.Center.y);
            if (view.Mode == RiccaSkillSettings.MotionMode.Dash)
            {
                BoxCollider blade = _sword.Geometry;
                if (blade)
                    effect.transform.SetPositionAndRotation(blade.transform.TransformPoint(blade.center), blade.transform.rotation);
            }
            else if (!_playedSkillEffect)
            {
                effect.transform.SetPositionAndRotation(from + offset, _skillRotation);
            }

            if (!_playedSkillEffect)
            {
                _playedSkillEffect = true;
                SetSkillEffectActive(skill, true);
            }

            if (view.Mode == RiccaSkillSettings.MotionMode.Projectile)
                effect.transform.position = to + offset;
        }

        private void SetSkillEffectActive(SkillId skill, bool active)
        {
            if (_skillEffects == null)
                return;

            int index = (int)skill - 1;
            GameObject effect = _skillEffects[index];
            if (!effect || effect.activeSelf == active)
                return;

            if (active)
            {
                effect.SetActive(true);
                foreach (ParticleSystem particle in _skillParticles[index])
                {
                    if (particle.gameObject.activeInHierarchy)
                        particle.Play(false);
                }
                return;
            }

            foreach (ParticleSystem particle in _skillParticles[index])
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.SetActive(false);
        }

        private void EndSkillPresentation(bool completed)
        {
            SkillId skill = _playingSkill.Definition.Id;
            if (_playedSkillEffect && (skill != SkillId.BlazeCharge || !_actions.IsBlazeCharged))
                SetSkillEffectActive(skill, false);

            int skillState = _playingSkillState;
            _playingSkill = default;
            _playingSkillView = null;
            _playingSkillState = 0;
            _playedSkillEffect = false;
            if (_animator && _animator.isActiveAndEnabled &&
                _animator.GetCurrentAnimatorStateInfo(0).fullPathHash == skillState)
            {
                if (completed)
                    _animator.CrossFadeInFixedTime(Idle, AttackExitBlendSeconds, 0);
                else
                    _animator.Play(Idle, 0, 0f);
            }
        }

        private static float Axis(KeyCode negative, KeyCode positive)
        {
            return (Input.GetKey(positive) ? 1f : 0f) - (Input.GetKey(negative) ? 1f : 0f);
        }

        private static bool PositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
    }
}
