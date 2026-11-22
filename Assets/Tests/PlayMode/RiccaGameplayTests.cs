using System.Collections;
using System.IO;
using System.Linq;
using IdleRicca.Application.Gameplay;
using IdleRicca.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IdleRicca.Tests
{
    public sealed class RiccaGameplayTests
    {
        private static readonly int Attack = Animator.StringToHash("Base Layer.Attack");
        private const float WindowStart = 14f / 70f;
        private const float WindowEnd = 22f / 70f;

        [UnityTest]
        public IEnumerator CameraRelativeWalkRunAndSwingUseTheDemoBindings()
        {
            yield return SceneManager.LoadSceneAsync(0);
            Object.FindObjectOfType<EnemySpawner>().enabled = false;
            var view = Camera.main.GetComponent<ThirdPersonCamera>();
            var player = view.Player;
            var animator = player.GetComponent<Animator>();
            var input = new ScriptedInput();
            player.UseInputSource(input);

            int savedRate = Time.captureFramerate;
            Time.captureFramerate = 60;
            try
            {
                yield return Frames(5);
                SaveCameraFrame("Ricca-Start.png");
                float yaw = view.Yaw;
                float pitch = view.Pitch;
                Vector3 initialCamera = view.transform.position;
                input.NextLook = new Vector2(10f, 3f);
                yield return Frames(2);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(view.Yaw, yaw)), Is.GreaterThan(15f));
                Assert.That(view.Pitch, Is.LessThan(pitch));
                Assert.That(Vector3.Distance(initialCamera, view.transform.position), Is.GreaterThan(0.25f));

                Vector3 forward = Quaternion.Euler(0f, view.Yaw, 0f) * Vector3.forward;
                Vector3 origin = player.transform.position;
                Vector3 cameraOrigin = view.transform.position;
                input.Forward = 1f;
                yield return Frames(16);
                Vector3 walk = Planar(player.transform.position - origin);
                Assert.That(Vector3.Dot(walk.normalized, forward), Is.GreaterThan(0.95f));
                Assert.That(walk.magnitude, Is.GreaterThan(0.25f));
                Assert.That(Vector3.Dot(view.transform.position - cameraOrigin, forward), Is.GreaterThan(0.2f));
                Assert.That(MainClip(animator), Is.EqualTo("rc_walk_loop"));

                origin = player.transform.position;
                input.Run = true;
                yield return Frames(16);
                Vector3 run = Planar(player.transform.position - origin);
                Assert.That(run.magnitude, Is.GreaterThan(walk.magnitude * 1.55f));
                Assert.That(MainClip(animator), Is.EqualTo("rc_run_loop"));
                SaveCameraFrame("Ricca-Run.png");

                input.Run = false;
                yield return Frames(8);
                Assert.That(MainClip(animator), Is.EqualTo("rc_walk_loop"));
                Quaternion facing = player.transform.rotation;
                input.Run = true;
                input.NextAttack = true;
                yield return Frames(2);
                Assert.That(player.IsAttacking, Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(Attack));
                origin = player.transform.position;
                yaw = view.Yaw;
                input.NextLook = new Vector2(12f, 2f);
                for (int i = 0; player.IsAttacking && i < 100; i++)
                {
                    Assert.That(Planar(player.transform.position - origin).magnitude, Is.LessThan(0.02f));
                    Assert.That(Quaternion.Angle(facing, player.transform.rotation), Is.LessThan(0.1f));
                    if (i == 17)
                        SaveCameraFrame("Ricca-Attack.png");
                    yield return null;
                }

                Assert.That(player.IsAttacking, Is.False, "The held Attack state must exit.");
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(view.Yaw, yaw)), Is.GreaterThan(10f));
                yield return Frames(12);
                Assert.That(Planar(player.transform.position - origin).magnitude, Is.GreaterThan(0.3f));
                input.Forward = 0f;
                yield return Frames(14);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"), Is.True);
                SaveCameraFrame("Ricca-ThirdPerson.png");
            }
            finally
            {
                Time.captureFramerate = savedRate;
                input.Forward = 0f;
                player.UseInputSource(null);
            }
        }

        [UnityTest]
        public IEnumerator FastSwingHitsAnimatedArcOncePerEnemyAndZeroHpDisablesCollisions()
        {
            yield return SceneManager.LoadSceneAsync(0);
            var player = Camera.main.GetComponent<ThirdPersonCamera>().Player;
            var animator = player.GetComponent<Animator>();
            var input = new ScriptedInput();
            player.UseInputSource(input);
            yield return Frames(5);

            var spawner = Object.FindObjectOfType<EnemySpawner>();
            spawner.enabled = false;
            var enemies = new[] { EnemyKind.Goblin, EnemyKind.Skeleton, EnemyKind.Hellhound }.Select(kind =>
            {
                var template = spawner.GetTemplate(kind);
                var enemy = Object.Instantiate(template, player.transform.position + Vector3.up * 4f, Quaternion.identity);
                enemy.name = template.name;
                return enemy.GetComponent<EnemyTarget>();
            }).ToArray();
            Assert.That(enemies.Select(enemy => enemy.name), Is.EquivalentTo(new[] { "Goblin", "Skeleton", "Hellhound" }));
            Assert.That(enemies.Select(enemy => enemy.CurrentHp), Is.All.EqualTo(100));
            var goblin = enemies.Single(enemy => enemy.name == "Goblin");
            var skeleton = enemies.Single(enemy => enemy.name == "Skeleton");
            var hound = enemies.Single(enemy => enemy.name == "Hellhound");

            var reference = Object.Instantiate(player.gameObject,
                player.transform.position + Vector3.up * 80f, player.transform.rotation);
            var referenceAnimator = reference.GetComponent<Animator>();
            referenceAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var referenceBlade = reference.GetComponent<SwordHitbox>().Geometry;
            Vector3 offset = player.transform.position - reference.transform.position;
            Vector3 arc = SampleBlade(referenceAnimator, referenceBlade, 18f / 70f) + offset;
            Vector3 start = SampleBlade(referenceAnimator, referenceBlade, WindowStart);
            Vector3 end = SampleBlade(referenceAnimator, referenceBlade, WindowEnd);
            Vector3 chord = Vector3.Lerp(start, end, 0.5f);
            Vector3 recovery = SampleBlade(referenceAnimator, referenceBlade, 47f / 70f) + offset;

            for (int step = 0; step <= 40; step++)
            {
                float progress = Mathf.Lerp(WindowStart, WindowEnd, step / 40f);
                SampleBlade(referenceAnimator, referenceBlade, progress);
                Vector3 location = referenceBlade.transform.InverseTransformPoint(chord) - referenceBlade.center;
                Vector3 half = referenceBlade.size * 0.5f + Vector3.one * 0.065f;
                Assert.That(Mathf.Abs(location.x) > half.x || Mathf.Abs(location.y) > half.y ||
                    Mathf.Abs(location.z) > half.z, Is.True, "Chord case must stay outside the real blade.");
            }

            Object.Destroy(reference);
            var goblinBox = Place(goblin, arc);
            var houndBox = Place(hound, arc + player.transform.right * 0.02f);
            var skeletonBox = Place(skeleton, chord + offset);
            int mask = 1 << LayerMask.NameToLayer("EnemyHurtbox");
            Assert.That(Physics.OverlapSphere(arc, 0.1f, mask, QueryTriggerInteraction.Collide),
                Does.Contain(goblinBox));

            AnimatorCullingMode originalCulling = animator.cullingMode;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = 0f;
            try
            {
                for (int attackIndex = 1; attackIndex <= 4; attackIndex++)
                {
                    Place(skeleton, chord + offset);
                    input.NextAttack = true;
                    yield return Frames(2);
                    Assert.That(player.IsAttacking, Is.True,
                        $"Attack {attackIndex}, normalized state {animator.GetCurrentAnimatorStateInfo(0).normalizedTime}, HP {goblin.CurrentHp}.");
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.LessThan(WindowStart));
                    SetAttackProgress(animator, 0.45f);
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(WindowEnd));
                    yield return Frames(2);

                    int hp = 100 - attackIndex * 25;
                    Assert.That(goblin.CurrentHp, Is.EqualTo(hp));
                    Assert.That(hound.CurrentHp, Is.EqualTo(hp));
                    Assert.That(skeleton.CurrentHp, Is.EqualTo(100));
                    if (hp == 0)
                    {
                        Assert.That(goblin.gameObject.activeInHierarchy && hound.gameObject.activeInHierarchy, Is.True);
                        Assert.That(goblin.GetComponent<EnemyActor>().Phase, Is.EqualTo(EnemyPhase.Death));
                        Assert.That(hound.GetComponent<EnemyActor>().Phase, Is.EqualTo(EnemyPhase.Death));
                        Assert.That(goblin.GetComponent<Animator>().speed, Is.EqualTo(0.05f).Within(0.001f));
                        Assert.That(hound.GetComponent<Animator>().speed, Is.EqualTo(0.05f).Within(0.001f));
                        var active = Physics.OverlapSphere(arc, 0.1f, mask, QueryTriggerInteraction.Collide);
                        Assert.That(active.Contains(goblinBox), Is.False);
                        Assert.That(active.Contains(houndBox), Is.False);
                        SaveCameraFrame("Ricca-After-Hit.png");
                    }

                    Place(skeleton, recovery);
                    SetAttackProgress(animator, 1.05f);
                    yield return Frames(3);
                    Assert.That(player.IsAttacking, Is.False);
                    Assert.That(skeleton.CurrentHp, Is.EqualTo(100));
                }
            }
            finally
            {
                animator.speed = 1f;
                animator.cullingMode = originalCulling;
                player.UseInputSource(null);
            }

            float deathDeadline = Time.realtimeSinceStartup + 5f;
            while ((goblin || hound) && Time.realtimeSinceStartup < deathDeadline)
                yield return null;
            Assert.That(!goblin && !hound, Is.True, "Death animation must finish before removal.");

            yield return SceneManager.LoadSceneAsync(0);
            yield return EnemyGameplayTests.WaitForRoster();
            var reloaded = Object.FindObjectsOfType<EnemyTarget>();
            Assert.That(reloaded.Length, Is.EqualTo(3));
            foreach (EnemyTarget actor in reloaded)
            {
                Assert.That(actor.CurrentHp, Is.EqualTo(100));
                Assert.That(actor.GetComponentInChildren<BoxCollider>().enabled, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator HitStopSlowsOnlyDamageParticipantsAndRefreshesWithoutStacking()
        {
            yield return SceneManager.LoadSceneAsync(0);
            var view = Camera.main.GetComponent<ThirdPersonCamera>();
            var player = view.Player;
            var animator = player.GetComponent<Animator>();
            var input = new ScriptedInput();
            player.UseInputSource(input);
            var spawner = Object.FindObjectOfType<EnemySpawner>();
            spawner.enabled = false;

            var goblin = Object.Instantiate(spawner.GetTemplate(EnemyKind.Goblin),
                player.transform.position + Vector3.up * 4f, Quaternion.identity);
            var skeleton = Object.Instantiate(spawner.GetTemplate(EnemyKind.Skeleton),
                player.transform.position + Vector3.up * 4f, Quaternion.identity);
            var goblinTarget = goblin.GetComponent<EnemyTarget>();
            var skeletonTarget = skeleton.GetComponent<EnemyTarget>();
            var goblinAnimator = goblin.GetComponent<Animator>();
            var skeletonAnimator = skeleton.GetComponent<Animator>();
            var reference = Object.Instantiate(player.gameObject,
                player.transform.position + Vector3.up * 80f, player.transform.rotation);
            var clipAnimator = reference.GetComponent<Animator>();
            clipAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var referenceBlade = reference.GetComponent<SwordHitbox>().Geometry;

            int savedRate = Time.captureFramerate;
            float originalTimeScale = Time.timeScale;
            Time.captureFramerate = 100;
            animator.speed = 0.6f;
            goblinAnimator.speed = 0.8f;
            try
            {
                yield return Frames(5);
                input.NextAttack = true;
                yield return Frames(2);
                Assert.That(player.IsAttacking, Is.True);

                SetAttackProgress(animator, 0.22f);
                yield return null;
                Assert.That(animator.speed, Is.EqualTo(0.6f).Within(0.001f), "An empty sweep uses the original speed.");

                Vector3 offset = player.transform.position - reference.transform.position;
                Place(goblinTarget, SampleBlade(clipAnimator, referenceBlade, 0.255f) + offset);
                SetAttackProgress(animator, 0.26f);
                yield return Frames(2);

                Assert.That(goblinTarget.CurrentHp, Is.EqualTo(75));
                Assert.That(animator.speed, Is.EqualTo(0.03f).Within(0.001f));
                Assert.That(goblinAnimator.speed, Is.EqualTo(0.04f).Within(0.001f));
                Assert.That(skeletonAnimator.speed, Is.EqualTo(1f).Within(0.001f));
                Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale));

                double firstObserved = Time.unscaledTimeAsDouble;
                float originalYaw = view.Yaw;
                input.NextLook = new Vector2(8f, 0f);
                yield return Frames(3);
                Assert.That(view.Yaw, Is.Not.EqualTo(originalYaw));
                Assert.That(animator.speed, Is.EqualTo(0.03f).Within(0.001f));

                Place(skeletonTarget, SampleBlade(clipAnimator, referenceBlade, 0.30f) + offset);
                double refreshRequest = Time.unscaledTimeAsDouble;
                SetAttackProgress(animator, 0.305f);
                yield return Frames(2);
                Assert.That(skeletonTarget.CurrentHp, Is.EqualTo(75));
                Assert.That(goblinTarget.CurrentHp, Is.EqualTo(75));
                Assert.That(skeletonAnimator.speed, Is.EqualTo(0.05f).Within(0.001f));
                Assert.That(animator.speed, Is.EqualTo(0.03f).Within(0.001f));

                while (Time.unscaledTimeAsDouble < firstObserved + 0.10)
                    yield return null;
                Assert.That(goblinAnimator.speed, Is.EqualTo(0.8f).Within(0.001f));
                Assert.That(animator.speed, Is.EqualTo(0.03f).Within(0.001f));

                double timeout = refreshRequest + 0.2;
                while (animator.speed < 0.6f && Time.unscaledTimeAsDouble < timeout)
                    yield return null;
                double elapsed = Time.unscaledTimeAsDouble - refreshRequest;
                Assert.That(elapsed, Is.InRange(0.099, 0.13));
                Assert.That(animator.speed, Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(skeletonAnimator.speed, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                animator.speed = 1f;
                player.UseInputSource(null);
                Time.captureFramerate = savedRate;
                Object.Destroy(reference);
            }
        }

        private static BoxCollider Place(EnemyTarget target, Vector3 position)
        {
            var box = target.GetComponent<EnemyActor>().Body;
            box.size = Vector3.one * 0.035f;
            Transform root = target.transform;
            root.position += position - box.transform.TransformPoint(box.center);
            Physics.SyncTransforms();
            return box;
        }

        private static Vector3 SampleBlade(Animator animator, BoxCollider box, float progress)
        {
            animator.Play(Attack, 0, progress);
            animator.Update(0f);
            return box.transform.TransformPoint(box.center);
        }

        private static void SetAttackProgress(Animator animator, float progress)
        {
            animator.Play(Attack, 0, progress);
            animator.Update(0f);
        }

        private static string MainClip(Animator animator)
        {
            return animator.GetCurrentAnimatorClipInfo(0).OrderByDescending(info => info.weight).First().clip.name;
        }

        private static Vector3 Planar(Vector3 value) => new Vector3(value.x, 0f, value.z);

        private static IEnumerator Frames(int count)
        {
            while (count-- > 0)
                yield return null;
        }

        private static void SaveCameraFrame(string name)
        {
            string directory = System.Environment.GetEnvironmentVariable("IDLERICCA_GAMEPLAY_LOGS");
            if (string.IsNullOrEmpty(directory))
                return;

            Camera camera = Camera.main;
            RenderTexture oldActive = RenderTexture.active;
            RenderTexture oldTarget = camera.targetTexture;
            var target = RenderTexture.GetTemporary(960, 540, 24);
            var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory, name), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(pixels);
            }
        }

        private sealed class ScriptedInput : IRiccaInput
        {
            internal float Forward;
            internal bool Run;
            internal bool NextAttack;
            internal Vector2 NextLook;

            public RiccaInputFrame Read()
            {
                var input = new RiccaInputFrame(0f, Forward, NextLook, Run, NextAttack);
                NextAttack = false;
                NextLook = Vector2.zero;
                return input;
            }
        }
    }
}
