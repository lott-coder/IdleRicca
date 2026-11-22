using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleRicca.Presentation.Gameplay
{
    /// <summary>围绕 Ricca 的第三人称主相机。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private RiccaPlayer _player;
        [SerializeField, Min(0.1f)] private float _height = 1.32f;
        [SerializeField, Min(0.5f)] private float _distance = 3.3f;
        [SerializeField, Min(0f)] private float _lookSensitivity = 2.6f;
        [SerializeField] private LayerMask _obstructions = ~0;

        private const float CameraRadius = 0.18f;
        private const float CollisionPadding = 0.08f;
        private const float InitialPitch = 13f;
        private const float MinimumPitch = -15f;
        private const float MaximumPitch = 64f;
        private const float MaximumMouseDelta = 45f;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private Transform _target;
        private Camera _camera;
        private float _cameraHeight;
        private float _followDistance;
        private float _sensitivity;

        public RiccaPlayer Player => _player;
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _cameraHeight = ValidRange(_height, 0.1f, 6f, 1.32f);
            _followDistance = ValidRange(_distance, 0.5f, 15f, 3.3f);
            _sensitivity = ValidRange(_lookSensitivity, 0f, 20f, 2.6f);
        }

        private void OnEnable()
        {
            if (!_player)
                return;

            _target = _player.transform;
            Yaw = _target.eulerAngles.y;
            Pitch = InitialPitch;
            _player.BindCamera(this);
        }

        private void Start()
        {
            if (!_player)
                Debug.LogError("The third-person camera needs a serialized Ricca reference.", this);
        }

        private void OnDisable()
        {
            if (_player)
                _player.UnbindCamera(this);
        }

        internal void Look(Vector2 delta)
        {
            Yaw = Mathf.Repeat(Yaw + ValidRange(delta.x, -MaximumMouseDelta, MaximumMouseDelta, 0f) * _sensitivity, 360f);
            Pitch = Mathf.Clamp(Pitch - ValidRange(delta.y, -MaximumMouseDelta, MaximumMouseDelta, 0f) * _sensitivity,
                MinimumPitch, MaximumPitch);
        }

        private void LateUpdate()
        {
            if (!_target)
                return;

            Quaternion rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 away = rotation * Vector3.back;
            Vector3 pivot = _target.position + Vector3.up * _cameraHeight;
            float distance = _followDistance;
            float minimum = Mathf.Max(0.22f, _camera.nearClipPlane + CameraRadius);
            PhysicsScene scene = gameObject.scene.GetPhysicsScene();
            int count = scene.SphereCast(pivot, CameraRadius, away, _hits, _followDistance,
                _obstructions, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.transform && !hit.transform.IsChildOf(_target))
                    distance = Mathf.Min(distance, Mathf.Max(minimum, hit.distance - CollisionPadding));
            }

            transform.SetPositionAndRotation(pivot + away * distance, rotation);
        }

        private static float ValidRange(float value, float minimum, float maximum, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, minimum, maximum);
        }
    }
}
