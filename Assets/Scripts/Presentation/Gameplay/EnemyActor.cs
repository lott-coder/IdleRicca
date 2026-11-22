using IdleRicca.Application.Gameplay;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace IdleRicca.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class EnemyActor : MonoBehaviour
    {
        private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
        private static readonly int Attack = Animator.StringToHash("Base Layer.Attack");
        private static readonly int Hit = Animator.StringToHash("Base Layer.Hit");
        private static readonly int Death = Animator.StringToHash("Base Layer.Death");
        private static readonly int Moving = Animator.StringToHash("Moving");
        private static readonly int HoldAttack = Animator.StringToHash("HoldAttack");
        private const float PathInterval = .25f;

        [SerializeField] private EnemyTarget _target;
        [SerializeField] private Animator _animator;
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private BoxCollider _body;
        [SerializeField] private BoxCollider _attackBox;
        [SerializeField] private EnemyCombatSettings _settings;

        private EnemyActions _actions;
        private IEnemyCommands _commands;
        private EnemyPhase _shownPhase;
        private EnemySpawner _owner;
        private ulong _slot;
        private RiccaPlayer _ricca;
        private LayerMask _worldLayers;
        private NavMeshPath _path;
        private readonly RaycastHit[] _sightHits = new RaycastHit[8];
        private PhysicsScene _physicsScene;
        private Vector3 _previousRicca;
        private float _nextPath;
        private bool _hasPath;
        private bool _retiring;
        private AnimationHitStop _hitStop;

        public EnemyPhase Phase => _actions?.Phase ?? EnemyPhase.Idle;
        public BoxCollider Body => _body;
        internal EnemyHealth Health => _target ? _target.Health : null;
        public BoxCollider AttackBox => _attackBox;
        public EnemyCombatSettings Settings => _settings;
        public float BodyRadius { get; private set; }

        public bool IsConfigured => _target && _animator && _animator.runtimeAnimatorController &&
            _agent && _body && _body.enabled && _attackBox && _attackBox.isTrigger && _attackBox.enabled &&
            _settings && _settings.IsValid;

        private void Start()
        {
            if (!EnsureInitialized())
            {
                Debug.LogError("EnemyActor requires its Animator, body, attack box and valid settings.", this);
                enabled = false;
            }
        }

        internal bool TakeSlot(EnemySpawner owner, ulong slot)
        {
            if (_owner || !isActiveAndEnabled || slot == 0 || !owner)
                return false;

            _owner = owner;
            _slot = slot;
            return true;
        }

        internal bool BindRicca(RiccaPlayer ricca, LayerMask worldLayers)
        {
            if (!ricca || !EnsureInitialized() || !_commands.BindTarget(ricca.Actions))
                return false;

            _ricca = ricca;
            _worldLayers = worldLayers;
            _path = new NavMeshPath();
            _agent.speed = _settings.Speed;
            _agent.enabled = true;
            return _agent.isOnNavMesh;
        }

        private bool EnsureInitialized()
        {
            if (_actions != null)
                return true;
            if (!IsConfigured || _target.Health == null || !_animator.HasState(0, Attack) ||
                !_animator.HasState(0, Hit) || !_animator.HasState(0, Death))
                return false;

            _actions = new EnemyActions(_target.Health, _settings.Range, _settings.MaximumHeightDifference,
                _settings.Damage, _settings.ActiveStart, _settings.ActiveEnd, _settings.Cooldown,
                _settings.AttackClip.length, _settings.HitClip.length, _settings.DeathClip.length);
            _commands = _actions.Commands;
            _physicsScene = gameObject.scene.GetPhysicsScene();
            Vector3 half = Vector3.Scale(_body.size * .5f, _body.transform.lossyScale);
            Vector3 offset = _body.transform.TransformPoint(_body.center) - transform.position;
            BodyRadius = Mathf.Sqrt(half.x * half.x + half.z * half.z) +
                Mathf.Sqrt(offset.x * offset.x + offset.z * offset.z);
            return true;
        }

        internal void OnDamage(NormalAttackSettings attack)
        {
            if (EnsureInitialized())
            {
                Show(_commands.ObserveHit(), true);
                if (attack && isActiveAndEnabled)
                    _hitStop.Apply(_animator, attack);
            }
            else if (_target && _target.CurrentHp == 0)
                Retire();
        }

        private void Update()
        {
            _hitStop.RestoreIfElapsed(_animator);
            if (_actions == null)
                return;

            if (!_ricca || !_ricca.isActiveAndEnabled || !_ricca.IsAlive)
            {
                _commands.CancelAttack();
                Show(_commands.Step(false, float.PositiveInfinity, 0f, false, Time.deltaTime));
                return;
            }

            Vector3 target = _ricca.transform.position;
            Vector3 offset = target - transform.position;
            float distanceSquared = offset.x * offset.x + offset.z * offset.z;
            bool inRange = distanceSquared <= _settings.Range * _settings.Range &&
                Mathf.Abs(offset.y) <= _settings.MaximumHeightDifference;
            bool sight = inRange && HasClearSight(target);
            if (!sight && (Phase == EnemyPhase.Idle || Phase == EnemyPhase.Chasing))
                UpdatePath(target);

            Show(_commands.Step(_hasPath, distanceSquared, offset.y, sight, Time.deltaTime));
            if (Phase == EnemyPhase.Chasing && _agent.enabled && _agent.isOnNavMesh)
                _animator.SetBool(Moving, _agent.velocity.sqrMagnitude > .015f);
        }

        private void LateUpdate()
        {
            if (_actions == null)
                return;

            bool animationReady = _animator && _animator.isActiveAndEnabled;
            AnimatorStateInfo state = animationReady ? _animator.GetCurrentAnimatorStateInfo(0) : default;
            if (Phase == EnemyPhase.Attacking)
            {
                Vector3 position = _ricca ? _ricca.transform.position : _previousRicca;
                var frame = _commands.BeginAttackFrame(animationReady && state.fullPathHash == Attack,
                    state.normalizedTime, Time.deltaTime);
                if (frame.IsValid)
                {
                    if (frame.CanHit && _ricca && _ricca.isActiveAndEnabled && _ricca.IsAlive)
                        FindContact(frame, position);

                    _commands.EndAttackFrame(frame);
                }

                _previousRicca = position;
                Show(Phase);
            }
            else if (Phase == EnemyPhase.Hit || Phase == EnemyPhase.Death)
            {
                EnemyPhase played = EnemyPhase.Idle;
                if (animationReady && state.fullPathHash == Hit)
                    played = EnemyPhase.Hit;
                else if (animationReady && state.fullPathHash == Death)
                    played = EnemyPhase.Death;

                // 受击与死亡的超时按局部动画时间推进。
                Show(_commands.ObserveAnimation(played, state.normalizedTime, Time.deltaTime * _hitStop.Multiplier));
            }
        }

        private void UpdatePath(Vector3 target)
        {
            if (!_agent.enabled || !_agent.isOnNavMesh)
            {
                _hasPath = false;
                return;
            }

            if (Time.time < _nextPath)
                return;

            _nextPath = Time.time + PathInterval;
            _hasPath = NavMesh.SamplePosition(target, out NavMeshHit navHit, .75f, _agent.areaMask) &&
                Mathf.Abs(navHit.position.y - target.y) <= .45f &&
                _agent.CalculatePath(navHit.position, _path) &&
                _path.status == NavMeshPathStatus.PathComplete && _agent.SetPath(_path);
            if (!_hasPath)
                _agent.ResetPath();
        }

        private void FindContact(EnemyAttackFrame frame, Vector3 position)
        {
            var collider = _ricca.ContactCollider;
            if (!_attackBox.enabled || !collider || !collider.enabled)
                return;

            float t1 = Mathf.InverseLerp(frame.PreviousProgress, frame.Progress, frame.Start);
            float t2 = Mathf.InverseLerp(frame.PreviousProgress, frame.Progress, frame.End);
            float travel = Vector3.Distance(_previousRicca, position) * (t2 - t1);
            int steps = Mathf.Clamp(Mathf.CeilToInt(travel / .12f), 1, 8);
            Transform box = _attackBox.transform;
            for (int i = 0; i <= steps; i++)
            {
                float t = Mathf.Lerp(t1, t2, (float)i / steps);
                Vector3 riccaAtHit = Vector3.Lerp(_previousRicca, position, t);
                Vector3 capsuleAtHit = collider.transform.position + (riccaAtHit - position);
                if (!Physics.ComputePenetration(_attackBox, box.position, box.rotation, collider,
                        capsuleAtHit, collider.transform.rotation, out _, out _) || !HasClearSight(riccaAtHit))
                    continue;

                if (_commands.TryHitRicca(frame, _ricca.Actions) != AttackHit.Ignored)
                {
                    Vector3 center = capsuleAtHit + collider.transform.TransformVector(collider.center);
                    Vector3 towardsAttacker = _body.transform.TransformPoint(_body.center) - center;
                    towardsAttacker.y = 0f;
                    _ricca.ShowReceivedHit(center + towardsAttacker.normalized * collider.radius);
                }
                return;
            }
        }

        private bool HasClearSight(Vector3 target)
        {
            Vector3 from = _body.transform.TransformPoint(_body.center);
            Vector3 to = target + Vector3.up * .82f;
            Vector3 offset = to - from;
            float length = offset.magnitude;
            if (length <= .001f)
                return true;

            int count = _physicsScene.Raycast(from, offset / length, _sightHits, length,
                _worldLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_sightHits[i].collider != _ricca.ContactCollider)
                    return false;
            }

            return count < _sightHits.Length;
        }

        private void Show(EnemyPhase phase, bool replayReaction = false)
        {
            if (phase == _shownPhase && !(replayReaction && phase == EnemyPhase.Hit))
                return;

            EnemyPhase previous = _shownPhase;
            if (_actions.AnimationTimedOut &&
                (previous == EnemyPhase.Attacking || previous == EnemyPhase.Hit || previous == EnemyPhase.Death))
                Debug.LogWarning("Enemy animation did not reach the expected completion state.", this);
            _shownPhase = phase;
            if (phase == EnemyPhase.Retired)
            {
                Retire();
                return;
            }

            if (phase == EnemyPhase.Chasing)
            {
                if (_agent.enabled && _agent.isOnNavMesh)
                    _agent.isStopped = false;

                return;
            }

            StopAgent();
            if (phase == EnemyPhase.Death)
            {
                _agent.enabled = false;
                _attackBox.enabled = false;
                _target.DisableHurtboxes();
            }

            if (!_animator || !_animator.isActiveAndEnabled)
                return;

            _animator.SetBool(Moving, false);
            _animator.SetBool(HoldAttack, phase == EnemyPhase.Attacking);
            switch (phase)
            {
                case EnemyPhase.Attacking:
                    Vector3 toward = _ricca.transform.position - transform.position;
                    toward.y = 0f;
                    if (toward.sqrMagnitude > .0001f)
                        transform.rotation = Quaternion.LookRotation(toward);
                    _previousRicca = _ricca.transform.position;
                    _animator.Play(Attack, 0, 0f);
                    _animator.Update(0f);
                    break;
                case EnemyPhase.Hit:
                    _animator.Play(Hit, 0, 0f);
                    _animator.Update(0f);
                    break;
                case EnemyPhase.Death:
                    _animator.Play(Death, 0, 0f);
                    _animator.Update(0f);
                    break;
                case EnemyPhase.Idle:
                    if (previous != EnemyPhase.Chasing)
                        _animator.Play(Idle, 0, 0f);
                    break;
            }
        }

        private void StopAgent()
        {
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
                _agent.velocity = Vector3.zero;
            }

            _hasPath = false;
            _nextPath = 0f;
        }

        internal void Retire()
        {
            if (_retiring)
                return;

            _retiring = true;
            enabled = false;
            if (_agent)
                _agent.enabled = false;
            if (_attackBox)
                _attackBox.enabled = false;
            if (_target)
                _target.DisableHurtboxes();
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            _hitStop.Release(_animator);
            _commands?.CancelAttack();
            if (_animator && _animator.isActiveAndEnabled)
                _animator.SetBool(HoldAttack, false);

            if (_owner && !gameObject.scene.isLoaded)
                return;
            if (_owner)
                Retire();
        }

        private void OnDestroy()
        {
            if (_owner)
                _owner.Release(this, _slot);
        }

        private void OnDrawGizmosSelected()
        {
            if (!_attackBox)
                return;

            Gizmos.color = new Color(1f, .28f, .1f);
            Gizmos.matrix = _attackBox.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(_attackBox.center, _attackBox.size);
        }
    }
}
