using System;
using System.Collections.Generic;
using IdleRicca.Application.Gameplay;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using WavePhase = IdleRicca.Application.Gameplay.WaveStatus.RunPhase;

namespace IdleRicca.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(150)]
    public sealed class EnemySpawner : MonoBehaviour
    {
        private const int ChecksPerFrame = 2;
        private const float FloorProbeHeight = 1.3f;
        private const float FloorProbeLength = 2.9f;
        private const float ProjectionDistance = .45f;
        private const float FloorTolerance = .27f;
        private const float ClearanceSkin = .025f;
        private const double DeathAnimationTimeout = 12d;
        private const double RestartFeedbackTimeout = 15d;
        private static readonly HashSet<int> ActiveScenes = new HashSet<int>();

        [SerializeField] private RiccaPlayer _ricca;
        [SerializeField] private EnemyActor _goblin;
        [SerializeField] private EnemyActor _skeleton;
        [SerializeField] private EnemyActor _hellhound;
        [SerializeField] private EnemySpawnSettings _settings;
        [SerializeField] private WaveSettings _waveSettings;
        [SerializeField] private NavMeshData _navigation;
        [SerializeField] private LayerMask _worldLayers = 1;

        private EnemyPopulation _population;
        private WaveRun _waveRun;
        private List<EnemyActor> _actors;
        private List<PendingRemoval> _pending;
        private NavMeshPath _spawnPath;
        private readonly Collider[] _obstacle = new Collider[1];
        private PhysicsScene _physicsScene;
        private NavMeshDataInstance _navigationInstance;
        private int _sceneHandle;
        private int _attemptsRemaining;
        private double _nextAttempt;
        private double _deathDeadline;
        private double _restartDeadline;
        private bool _ownsScene;
        private bool _ready;
        private bool _started;
        private bool _waitingForRemoval;

        public int OccupiedSlots => _waveRun?.OccupiedSlots ?? _population?.Count ?? 0;
        public int MaximumEnemies => _waveRun?.CurrentWave.MaximumEnemies ??
            (_settings ? _settings.MaximumEnemies : 0);

        /// <summary>读取场景已经就绪的局内波次和奖励。</summary>
        public bool TryGetWaveStatus(out WaveStatus status)
        {
            status = _ready && _waveRun != null ? _waveRun.Status : default;
            return status.Number > 0;
        }

        internal bool TryGetSkillRewardPreview(ulong offerId, int index, out SkillRewardPreview preview)
        {
            preview = default;
            return _ready && _waveRun != null && _waveRun.TryGetSkillRewardPreview(offerId, index, out preview);
        }

        /// <summary>记录当前面板选项，在同一帧拦截传入角色的点击。</summary>
        public bool TryChooseWaveReward(ulong offerId, int index)
        {
            if (!_ready || _waveRun == null || !_waveRun.TryChooseReward(offerId, index, Time.timeAsDouble))
                return false;

            _attemptsRemaining = 0;
            _nextAttempt = Time.timeAsDouble;
            _ricca.ApplyWaveInputState();
            return true;
        }

        /// <summary>死亡演出结束或通关时允许重开。</summary>
        public bool IsWaveRestartReady => TryGetWaveStatus(out WaveStatus status) &&
            (status.Phase == WavePhase.Won || (status.Phase == WavePhase.Defeated &&
                (_ricca.HasFinishedDeathAnimation || Time.unscaledTimeAsDouble >= _deathDeadline)));

        /// <summary>场景载入延迟时，界面改为显示状态提示。</summary>
        public bool IsWaveRestartDelayed => _ready && _waveRun != null &&
            _waveRun.Status.Phase == WavePhase.Restarting && Time.unscaledTimeAsDouble >= _restartDeadline;

        /// <summary>重新载入当前构建场景，每次结局只接受一次。</summary>
        public bool TryRestartWaveRun()
        {
            if (!IsWaveRestartReady || !_waveRun.TryBeginRestart())
                return false;

            int buildIndex = gameObject.scene.buildIndex;
            try
            {
                if (buildIndex >= 0 && SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single) != null)
                {
                    // 场景载入发起后 Unity 无法取消，超时只切换反馈，避免重复请求。
                    _restartDeadline = Time.unscaledTimeAsDouble + RestartFeedbackTimeout;
                    return true;
                }
                Debug.LogError("The combat scene is not available to restart.", this);
            }
            catch (ArgumentException exception)
            {
                Debug.LogException(exception, this);
            }

            _waveRun.CancelRestart();
            return false;
        }

        public EnemyActor GetTemplate(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Goblin: return _goblin;
                case EnemyKind.Skeleton: return _skeleton;
                case EnemyKind.Hellhound: return _hellhound;
                default: return null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOwners() => ActiveScenes.Clear();

        private void Start()
        {
            _started = true;
            Initialize();
        }

        private void OnEnable()
        {
            if (!_started)
                return;

            if (OccupiedSlots > 0)
                _waitingForRemoval = true;
            else
                Initialize();
        }

        private void Initialize()
        {
            _sceneHandle = gameObject.scene.handle;
            if (!_ricca || _ricca.gameObject.scene != gameObject.scene || _ricca.Actions == null ||
                !_settings || !_settings.IsValid || !_navigation || _worldLayers.value == 0 ||
                !ValidTemplate(_goblin) || !ValidTemplate(_skeleton) || !ValidTemplate(_hellhound))
            {
                Debug.LogError("Enemy spawning requires Ricca, three valid prefab templates, settings and City navigation.", this);
                enabled = false;
                return;
            }

            if (_waveSettings && _waveSettings.HasWaves && _waveRun == null)
            {
                try
                {
                    _waveRun = _waveSettings.CreateRun(_ricca.Actions, _settings);
                }
                catch (ArgumentException exception)
                {
                    Debug.LogError($"Invalid wave settings: {exception.Message}", this);
                    enabled = false;
                    return;
                }
            }

            if (!ActiveScenes.Add(_sceneHandle))
            {
                Debug.LogError("Only one active enemy spawner can own a scene.", this);
                enabled = false;
                return;
            }

            _ownsScene = true;
            _navigationInstance = NavMesh.AddNavMeshData(_navigation);
            if (!_navigationInstance.valid)
            {
                Debug.LogError("The City enemy navigation data could not be loaded.", this);
                enabled = false;
                return;
            }

            if (_waveRun == null)
                _population = new EnemyPopulation(_settings.MaximumEnemies, _settings.MinimumDistance, _settings.MaximumDistance);
            int capacity = Math.Min(_waveRun?.MaximumConfiguredEnemies ?? _settings.MaximumEnemies, 16);
            _actors = new List<EnemyActor>(capacity);
            _pending = new List<PendingRemoval>(capacity);
            _spawnPath = new NavMeshPath();
            _physicsScene = gameObject.scene.GetPhysicsScene();
            _attemptsRemaining = 0;
            _nextAttempt = Time.timeAsDouble;
            _ready = true;
        }

        private void Update()
        {
            if (_waitingForRemoval)
            {
                ReclaimUnboundInstances();
                if (OccupiedSlots > 0)
                    return;

                _waitingForRemoval = false;
                Initialize();
            }

            if (!_ready)
                return;

            ReclaimUnboundInstances();
            double now = Time.timeAsDouble;
            if (!_ricca || !_ricca.isActiveAndEnabled || !_ricca.IsAlive || !CanSpawn(now))
            {
                _attemptsRemaining = 0;
                if (_waveRun == null)
                    _nextAttempt = now + _settings.WaveInterval;
                return;
            }

            if (_attemptsRemaining == 0)
            {
                if (now < _nextAttempt)
                    return;

                _attemptsRemaining = _settings.AttemptsPerWave;
            }

            Vector3 target = _ricca.transform.position;
            if (NavMesh.SamplePosition(target, out NavMeshHit targetHit, .75f, NavMesh.AllAreas) &&
                Mathf.Abs(targetHit.position.y - target.y) <= .45f)
            {
                for (int i = 0; i < ChecksPerFrame && _attemptsRemaining > 0 && CanSpawn(now); i++)
                {
                    _attemptsRemaining--;
                    if (TrySpawn(target, targetHit.position, now) && _waveRun != null)
                    {
                        _attemptsRemaining = 0;
                        return;
                    }
                }
            }
            else
            {
                _attemptsRemaining = 0;
            }

            if (_attemptsRemaining == 0)
                _nextAttempt = now + (_waveRun?.CurrentWave.SpawnInterval ?? _settings.WaveInterval);
        }

        private void LateUpdate()
        {
            if (!_ready || _waveRun == null || !_ricca)
                return;

            WavePhase previous = _waveRun.Status.Phase;
            _waveRun.Advance();
            WavePhase current = _waveRun.Status.Phase;
            if (previous == current)
                return;

            if (current == WavePhase.Defeated)
                _deathDeadline = Time.unscaledTimeAsDouble + DeathAnimationTimeout;
            _ricca.ApplyWaveInputState();
        }

        private bool CanSpawn(double now) => _waveRun != null ? _waveRun.CanSpawn(now) : !_population.IsFull;

        private bool TrySpawn(Vector3 target, Vector3 targetOnNavMesh, double now)
        {
            var candidate = _waveRun != null ? _waveRun.SampleCandidate() : _population.Commands.SampleCandidate();
            var template = GetTemplate(candidate.Kind);
            Vector3 point = target + new Vector3(candidate.X, 0f, candidate.Z);
            if (!TryFindSpawnPoint(template, point, target, targetOnNavMesh, out Vector3 position, out Quaternion rotation))
                return false;

            float distanceSquared = PlanarSquared(position - target);
            bool reserved = _waveRun != null
                ? _waveRun.TryReserveSpawn(candidate.Kind, distanceSquared, true, true, true, now, out ulong slot)
                : _population.Commands.TryReserve(candidate.Kind, distanceSquared, true, true, true, out slot);
            if (!reserved)
                return false;

            EnemyActor actor;
            try
            {
                actor = Instantiate(template, position, rotation);
            }
            catch (Exception exception)
            {
                ReleaseSlot(slot);
                Debug.LogException(exception, this);
                return false;
            }

            actor.gameObject.name = template.gameObject.name;
            if (!actor.TakeSlot(this, slot))
            {
                _pending.Add(new PendingRemoval(actor, slot));
                Destroy(actor.gameObject);
                return false;
            }

            _actors.Add(actor);
            if (actor.BindRicca(_ricca, _worldLayers))
            {
                bool confirmed = _waveRun != null
                    ? _waveRun.ConfirmSpawn(slot, actor.Health, now)
                    : _population.Commands.ConfirmSpawn(slot);
                if (confirmed)
                    return true;
            }

            actor.Retire();
            return false;
        }

        private bool TryFindSpawnPoint(EnemyActor template, Vector3 candidate, Vector3 target, Vector3 targetOnNavMesh,
            out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;
            if (!_physicsScene.Raycast(candidate + Vector3.up * FloorProbeHeight, Vector3.down,
                    out RaycastHit floor, FloorProbeLength, _worldLayers, QueryTriggerInteraction.Ignore) ||
                floor.normal.y < .71f ||
                !NavMesh.SamplePosition(floor.point, out NavMeshHit nav, ProjectionDistance, NavMesh.AllAreas) ||
                Mathf.Abs(nav.position.y - floor.point.y) > FloorTolerance)
                return false;

            position = nav.position;
            Vector3 toward = target - position;
            toward.y = 0f;
            float distance = toward.sqrMagnitude;
            if (distance < _settings.MinimumDistance * _settings.MinimumDistance ||
                distance > _settings.MaximumDistance * _settings.MaximumDistance)
                return false;

            rotation = Quaternion.LookRotation(toward);
            var body = template.Body;
            Vector3 bodyOffset = rotation * template.transform.InverseTransformPoint(body.transform.TransformPoint(body.center));
            Vector3 center = position + bodyOffset;
            Vector3 half = Vector3.Scale(body.size * .5f, body.transform.lossyScale);
            float radius = Mathf.Sqrt(PlanarSquared(half)) + Mathf.Sqrt(PlanarSquared(bodyOffset));
            half.y = Mathf.Max(.01f, half.y - ClearanceSkin);

            if (_physicsScene.OverlapBox(center + Vector3.up * ClearanceSkin, half, _obstacle,
                    rotation, _worldLayers, QueryTriggerInteraction.Ignore) != 0)
            {
                _obstacle[0] = null;
                return false;
            }

            foreach (EnemyActor active in _actors)
            {
                if (!active)
                    continue;

                float separation = radius + active.BodyRadius + .05f;
                Vector3 offset = active.transform.position - position;
                if (Mathf.Abs(offset.y) < 2f && PlanarSquared(offset) < separation * separation)
                    return false;
            }

            return NavMesh.CalculatePath(position, targetOnNavMesh, NavMesh.AllAreas, _spawnPath) &&
                _spawnPath.status == NavMeshPathStatus.PathComplete;
        }

        internal void Release(EnemyActor actor, ulong slot)
        {
            _actors?.Remove(actor);
            ReleaseSlot(slot);
        }

        private void ReleaseSlot(ulong slot)
        {
            if (_waveRun != null)
                _waveRun.Release(slot);
            else
                _population?.Commands.Release(slot);
        }

        private void ReclaimUnboundInstances()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Actor)
                    continue;

                ReleaseSlot(_pending[i].Slot);
                _pending.RemoveAt(i);
            }
        }

        private void OnDisable()
        {
            _waitingForRemoval = false;
            if (!_ownsScene)
                return;

            _ready = false;
            if (_actors != null)
            {
                foreach (EnemyActor actor in _actors)
                {
                    if (actor)
                        actor.Retire();
                }
            }

            if (_navigationInstance.valid)
                _navigationInstance.Remove();

            ActiveScenes.Remove(_sceneHandle);
            _ownsScene = false;
        }

        private static bool ValidTemplate(EnemyActor actor) =>
            actor && actor.enabled && actor.gameObject.activeSelf && actor.IsConfigured;

        private static float PlanarSquared(Vector3 vector) => vector.x * vector.x + vector.z * vector.z;

        private readonly struct PendingRemoval
        {
            public PendingRemoval(EnemyActor actor, ulong slot)
            {
                Actor = actor;
                Slot = slot;
            }

            public EnemyActor Actor { get; }
            public ulong Slot { get; }
        }
    }
}
