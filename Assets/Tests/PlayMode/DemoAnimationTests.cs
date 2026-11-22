using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using IdleRicca.Application.Gameplay;
using Object = UnityEngine.Object;
using IdleRicca.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IdleRicca.Tests
{
    public sealed class DemoAnimationTests
    {
        private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
        private static readonly int Hit = Animator.StringToHash("Base Layer.Hit");
        private static readonly int Death = Animator.StringToHash("Base Layer.Death");
        private static readonly int Move = Animator.StringToHash("Base Layer.Move");
        private static readonly int Attack = Animator.StringToHash("Base Layer.Attack");
        private static readonly int MovingParameter = Animator.StringToHash("Moving");
        private static readonly int AttackParameter = Animator.StringToHash("Attack");

        [UnityTest]
        public IEnumerator DemoActorsMoveTheirBonesAndReturnToIdleAfterAttacking()
        {
            yield return SceneManager.LoadSceneAsync(0);
            Assert.That(Camera.main, Is.Not.Null);
            yield return EnemyGameplayTests.WaitForRoster();
            var spawner = Object.FindObjectOfType<EnemySpawner>();
            var enemies = Object.FindObjectsOfType<EnemyActor>();
            spawner.enabled = false;
            var clones = enemies.Select(enemy =>
            {
                var kind = (EnemyKind)Enum.Parse(typeof(EnemyKind), enemy.name);
                var template = spawner.GetTemplate(kind).GetComponent<Animator>();
                var clone = Object.Instantiate(template, enemy.transform.position, enemy.transform.rotation);
                clone.name = enemy.name;
                return clone;
            }).ToArray();
            yield return null;
            var player = Camera.main.GetComponent<ThirdPersonCamera>().Player;
            player.UseInputSource(new EnemyGameplayTests.NoInput());
            var animators = clones.Prepend(player.GetComponent<Animator>()).ToArray();
            Assert.That(animators.Select(animator => animator.name),
                Is.EquivalentTo(new[] { "Ricca", "Goblin", "Skeleton", "Hellhound" }));
            foreach (var animator in clones)
                animator.Play(Idle, 0, 0f);

            foreach (var animator in animators)
            {
                var bones = animator.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .SelectMany(renderer => renderer.bones).Distinct().ToArray();

                AssertMotion(animator, bones, Idle);

                animator.SetBool(MovingParameter, true);
                AdvanceToState(animator, Move);
                AssertMotion(animator, bones, Move);

                animator.SetBool(MovingParameter, false);
                animator.SetTrigger(AttackParameter);
                AdvanceToState(animator, Attack);
                AssertMotion(animator, bones, Attack);
                AdvanceToState(animator, Idle);
                animator.Play(Hit, 0, 0f);
                AssertMotion(animator, bones, Hit);
                if (animator.name == "Ricca")
                    AdvanceToState(animator, Idle);
                else
                {
                    EnemyGameplayTests.Capture(Camera.main, Camera.main.GetComponent<ThirdPersonCamera>(),
                        animator.GetComponent<EnemyActor>(), animator.name + "-Hit");
                }

                animator.Play(Death, 0, 0f);
                AssertMotion(animator, bones, Death);
                if (animator.name != "Ricca")
                {
                    animator.Update(animator.GetCurrentAnimatorStateInfo(0).length * .6f);
                    EnemyGameplayTests.Capture(Camera.main, Camera.main.GetComponent<ThirdPersonCamera>(),
                        animator.GetComponent<EnemyActor>(), animator.name + "-Death");
                }
            }

            foreach (var animator in clones)
                Object.Destroy(animator.gameObject);
        }

        [UnityTest]
        public IEnumerator EnemyMoveLoopsKeepTheVisualRootAlignedWithTheNavigationRoot()
        {
            yield return SceneManager.LoadSceneAsync(0);
            var spawner = Object.FindObjectOfType<EnemySpawner>();
            Assert.That(spawner, Is.Not.Null);
            spawner.enabled = false;
            yield return null;

            var displacedEnemies = new List<string>();
            foreach (EnemyKind kind in new[] { EnemyKind.Goblin, EnemyKind.Skeleton, EnemyKind.Hellhound })
            {
                var template = spawner.GetTemplate(kind).GetComponent<Animator>();
                var animator = Object.Instantiate(template, Vector3.up * 40f, Quaternion.identity);
                try
                {
                    var visualRoot = animator.transform.Find(kind.ToString().ToLowerInvariant() + "/root");
                    Assert.That(visualRoot, Is.Not.Null, kind.ToString());
                    var navigation = animator.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    Assert.That(navigation.enabled, Is.False, "Sample the animation without navigation motion.");
                    Vector3 objectStart = animator.transform.position;
                    AdvanceToState(animator, Idle);
                    Vector3 idleOffset = visualRoot.position - objectStart;
                    animator.SetBool(MovingParameter, true);
                    AdvanceToState(animator, Move);
                    float slice = animator.GetCurrentAnimatorStateInfo(0).length / 60f;
                    float maximumOffset = 0f;
                    for (int frame = 0; frame < 120; frame++)
                    {
                        animator.Update(slice);
                        Vector3 offset = visualRoot.position - animator.transform.position - idleOffset;
                        offset.y = 0f;
                        maximumOffset = Mathf.Max(maximumOffset, offset.magnitude);
                    }

                    Assert.That(Vector3.Distance(animator.transform.position, objectStart), Is.LessThan(.001f));
                    Debug.Log($"{kind} maximum horizontal visual-root offset: {maximumOffset:F4} m.");
                    if (maximumOffset >= .025f)
                        displacedEnemies.Add($"{kind}: {maximumOffset:F4} m");
                }
                finally
                {
                    Object.Destroy(animator.gameObject);
                }
            }

            Assert.That(displacedEnemies, Is.Empty, "Enemy Move loops must stay within 2.5 cm of the navigation origin.");
        }

        private static void AdvanceToState(Animator animator, int state)
        {
            for (int step = 0; step < 180; step++)
            {
                animator.Update(1f / 60f);
                if (!animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == state)
                    return;
            }

            Assert.Fail($"{animator.name} did not reach Animator state {state} within three seconds.");
        }

        private static void AssertMotion(Animator animator, Transform[] bones, int state)
        {
            animator.Update(0);
            var info = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(info.fullPathHash, Is.EqualTo(state), animator.name);
            Assert.That(bones.Length, Is.Positive, animator.name);
            var rotations = bones.Select(bone => bone.localRotation).ToArray();
            animator.Update(info.length * 0.20f);
            Assert.That(bones.Where((bone, index) => Quaternion.Angle(bone.localRotation, rotations[index]) > 0.1f),
                Is.Not.Empty, animator.name + " in " + info.fullPathHash);
        }
    }
}
