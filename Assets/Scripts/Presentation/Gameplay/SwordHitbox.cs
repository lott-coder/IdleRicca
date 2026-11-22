using IdleRicca.Application.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleRicca.Presentation.Gameplay
{
    /// <summary>用禁用盒的几何与烘焙的剑身动画轨迹查询受击体。</summary>
    [DisallowMultipleComponent]
    public sealed class SwordHitbox : MonoBehaviour
    {
        [SerializeField] private BoxCollider _geometry;
        [SerializeField] private LayerMask _hurtboxMask;

        public BoxCollider Geometry => _geometry;

        internal bool IsConfigured => _geometry && !_geometry.enabled && _hurtboxMask.value != 0;

        internal bool Resolve(AttackSampleBatch batch, NormalAttackSettings settings, IPlayerCommands commands,
            Vector3 previousRoot, Vector3 currentRoot)
        {
            var scene = gameObject.scene.GetPhysicsScene();
            Quaternion rootRotation = transform.rotation;
            Vector3 rootScale = transform.lossyScale;
            var magnitudes = new Vector3(Mathf.Abs(rootScale.x), Mathf.Abs(rootScale.y), Mathf.Abs(rootScale.z));
            var buffer = EnemyTarget.OverlapBuffer;

            bool hit = ResolvePose(settings.Sample(batch.Start), batch, settings, commands, scene, buffer,
                previousRoot, currentRoot, rootRotation, rootScale, magnitudes);

            for (int i = 0; i < settings.PoseCount; i++)
            {
                BladePose pose = settings.GetPose(i);
                if (pose.Progress > batch.Start && pose.Progress < batch.End)
                    hit |= ResolvePose(pose, batch, settings, commands, scene, buffer,
                        previousRoot, currentRoot, rootRotation, rootScale, magnitudes);
            }

            if (batch.End > batch.Start)
                hit |= ResolvePose(settings.Sample(batch.End), batch, settings, commands, scene, buffer,
                    previousRoot, currentRoot, rootRotation, rootScale, magnitudes);

            return hit;
        }

        private bool ResolvePose(BladePose pose, AttackSampleBatch batch, NormalAttackSettings settings, IPlayerCommands commands,
            PhysicsScene scene, Collider[] buffer, Vector3 previousRoot, Vector3 currentRoot,
            Quaternion rootRotation, Vector3 rootScale, Vector3 magnitudes)
        {
            float fraction = Mathf.InverseLerp(batch.PreviousProgress, batch.Progress, pose.Progress);
            Vector3 root = Vector3.Lerp(previousRoot, currentRoot, fraction);
            Vector3 center = root + rootRotation * Vector3.Scale(pose.Center, rootScale);
            Vector3 halfExtents = Vector3.Scale(pose.HalfExtents, magnitudes);
            Quaternion rotation = rootRotation * pose.Rotation;
            int count = scene.OverlapBox(center, halfExtents, buffer, rotation, _hurtboxMask, QueryTriggerInteraction.Collide);
            bool hit = false;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = buffer[i];
                buffer[i] = null;
                if (!EnemyTarget.TryResolve(candidate, out EnemyTarget target))
                    continue;

                AttackHit result = commands.TryHit(batch, target.Health);
                if (result == AttackHit.Ignored)
                    continue;

                target.ShowHitResult(result, settings);
                hit = true;
            }

            return hit;
        }
    }
}
