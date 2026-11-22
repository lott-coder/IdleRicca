using IdleRicca.Application.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleRicca.Presentation.Gameplay
{
    /// <summary>查询有效窗口内的定向盒路径，由应用层结算伤害。</summary>
    [DisallowMultipleComponent]
    public sealed class SkillHitbox : MonoBehaviour
    {
        [SerializeField] private LayerMask _hurtboxMask;
        [SerializeField] private LayerMask _worldMask = 1;

        private readonly RaycastHit[] _worldHits = new RaycastHit[32];

        internal bool IsConfigured => _hurtboxMask.value != 0 && _worldMask.value != 0;

        internal Vector3 LimitTravel(Vector3 from, Vector3 displacement, Vector3 halfSize,
            Quaternion rotation, Transform caster)
        {
            float distance = displacement.magnitude;
            if (distance <= Mathf.Epsilon)
                return from;

            Vector3 direction = displacement / distance;
            int count = gameObject.scene.GetPhysicsScene().BoxCast(from, halfSize, direction, _worldHits,
                rotation, distance, _worldMask, QueryTriggerInteraction.Ignore);
            if (count == _worldHits.Length)
                return from;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _worldHits[i];
                if (hit.collider && !hit.collider.transform.IsChildOf(caster))
                    distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - .01f));
            }

            return from + direction * distance;
        }

        internal void Resolve(Vector3 from, Vector3 to, Vector3 halfSize, Quaternion rotation,
            AttackSampleBatch batch, IPlayerSkillCommands commands)
        {
            if (!batch.CanHit)
                return;

            var scene = gameObject.scene.GetPhysicsScene();
            Overlap(scene, from, halfSize, rotation, batch, commands);
            Vector3 displacement = to - from;
            float distance = displacement.magnitude;
            if (distance <= Mathf.Epsilon)
                return;

            RaycastHit[] hits = EnemyTarget.SweepBuffer;
            int count = scene.BoxCast(from, halfSize, displacement / distance, hits,
                rotation, distance, _hurtboxMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
                Apply(hits[i].collider, batch, commands);

            Overlap(scene, to, halfSize, rotation, batch, commands);
        }

        private void Overlap(PhysicsScene scene, Vector3 center, Vector3 halfSize, Quaternion rotation,
            AttackSampleBatch batch, IPlayerSkillCommands commands)
        {
            Collider[] hits = EnemyTarget.OverlapBuffer;
            int count = scene.OverlapBox(center, halfSize, hits, rotation, _hurtboxMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider hit = hits[i];
                hits[i] = null;
                Apply(hit, batch, commands);
            }
        }

        private static void Apply(Collider hit, AttackSampleBatch batch, IPlayerSkillCommands commands)
        {
            if (!hit || !EnemyTarget.TryResolve(hit, out EnemyTarget target))
                return;

            AttackHit result = commands.TryHitSkill(batch, target.Health);
            if (result != AttackHit.Ignored)
                target.ShowHitResult(result, null);
        }
    }
}
