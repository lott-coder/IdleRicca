using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IdleRicca.Application.Gameplay;
using IdleRicca.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdleRicca.Tests
{
    public sealed class EnemyGameplayTests
    {
        private static readonly int Attack = Animator.StringToHash("Base Layer.Attack");
        private static readonly int HoldAttack = Animator.StringToHash("HoldAttack");

        [UnityTest]
        public IEnumerator CitySpawnsThreeReachableKindsAndReleasesOnlyAfterRemoval()
        {
            yield return SceneManager.LoadSceneAsync(0);
            var spawner = Object.FindObjectOfType<EnemySpawner>();
            var ricca = Camera.main.GetComponent<ThirdPersonCamera>().Player;
            ricca.UseInputSource(new NoInput());
            var observed = new HashSet<EnemyActor>();
            var path = new NavMeshPath();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (observed.Count < 3 && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(spawner.OccupiedSlots, Is.LessThanOrEqualTo(3));
                foreach (var enemy in Object.FindObjectsOfType<EnemyActor>())
                {
                    if (!observed.Add(enemy))
                        continue;

                    Vector3 offset = enemy.transform.position - ricca.transform.position;
                    offset.y = 0f;
                    Assert.That(offset.magnitude, Is.InRange(6.8f, 14.2f), enemy.name);
                    Assert.That(enemy.GetComponent<EnemyTarget>().CurrentHp, Is.EqualTo(100));
                    var agent = enemy.GetComponent<NavMeshAgent>();
                    Assert.That(NavMesh.SamplePosition(ricca.transform.position, out var target, .75f, NavMesh.AllAreas));
                    Assert.That(agent.CalculatePath(target.position, path) && path.status == NavMeshPathStatus.PathComplete,
                        Is.True, enemy.name);
                }

                yield return null;
            }

            Assert.That(observed.Select(actor => actor.name), Is.EquivalentTo(new[] { "Goblin", "Skeleton", "Hellhound" }));
            Vector3 feet = ricca.transform.position;
            Assert.That(NavMesh.SamplePosition(feet, out var startNav, .75f, NavMesh.AllAreas), Is.True);
            AssertCityObstacle(spawner, new Vector3(-37.782f, feet.y, 10.062f),
                "Fence_Concrete_4m_04_LOD0", feet, startNav.position);
            AssertCityObstacle(spawner, new Vector3(-30.961f, feet.y, -1.104f),
                "Large_Flowerbed_01_LOD0", feet, startNav.position);

            var removed = observed.First();
            Assert.That(spawner.OccupiedSlots, Is.EqualTo(3));
            removed.enabled = false;
            Assert.That(removed, Is.Not.Null);
            Assert.That(spawner.OccupiedSlots, Is.EqualTo(3));
            yield return WaitFor(() => !removed && spawner.OccupiedSlots == 2, "retirement must release the reserved slot");
            yield return WaitFor(() => spawner.OccupiedSlots == 3, "recently freed slot should refill");
            Assert.That(Object.FindObjectsOfType<EnemyActor>().Length, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator RealAnimatorChasesAttacksAndHitsRiccaOnceAcrossAWholeWindow()
        {
            yield return SceneManager.LoadSceneAsync(0);
            var camera = Camera.main;
            var view = camera.GetComponent<ThirdPersonCamera>();
            var ricca = view.Player;
            ricca.UseInputSource(new NoInput());
            var spawner = Object.FindObjectOfType<EnemySpawner>();
            var template = spawner.GetTemplate(EnemyKind.Goblin);
            spawner.enabled = false;
            yield return null;

            Vector3 origin = new Vector3(124f, 40f, 120f);
            var ground = Box("Test ground", origin + Vector3.down * .25f, new Vector3(18f, .5f, 18f));
            var wall = Box("Test wall", origin + new Vector3(0f, 1f, 0f), new Vector3(4f, 2f, .4f));
            NavMeshData data = BuildTestMesh(ground.transform, wall.transform, origin);
            var nav = NavMesh.AddNavMeshData(data);
            Physics.SyncTransforms();

            EnemyActor attacker = Object.Instantiate(template, origin + Vector3.back * 3f, Quaternion.identity);
            EnemyActor bystander = Object.Instantiate(template, origin + Vector3.back * 6f, Quaternion.identity);
            int originalRate = Time.captureFramerate;
            Time.captureFramerate = 60;
            try
            {
                SetPlayerPosition(ricca, origin + Vector3.forward * 3f);
                Assert.That(Bind(attacker, ricca), Is.True);
                var start = attacker.transform.position;
                yield return WaitFor(() => Vector3.Distance(attacker.transform.position, start) > .5f, "enemy should move");
                Assert.That(attacker.Phase, Is.EqualTo(EnemyPhase.Chasing));
                Assert.That(ricca.CurrentHp, Is.EqualTo(100));
                Capture(camera, view, attacker, "Enemy-Chase");

                yield return WaitFor(() => attacker.Phase == EnemyPhase.Attacking, "enemy should circle wall and attack", 12f);
                Assert.That(attacker.transform.position.z, Is.GreaterThan(origin.z + .5f));
                var animator = attacker.GetComponent<Animator>();
                animator.speed = 0f;
                Assert.That(animator.GetBool(HoldAttack), Is.True);
                SetProgress(animator, .12f);
                yield return null;
                Assert.That(ricca.CurrentHp, Is.EqualTo(100));
                SetProgress(animator, .42f);
                Capture(camera, view, attacker, "Enemy-Attack");
                SetProgress(animator, .12f);

                bystander.transform.position = ricca.transform.position;
                SetProgress(animator, 1.10f);
                yield return WaitFor(() => attacker.Phase == EnemyPhase.Idle, "finished attack must return to idle");
                Assert.That(ricca.CurrentHp, Is.EqualTo(100 - attacker.Settings.Damage));
                Assert.That(bystander.GetComponent<EnemyTarget>().CurrentHp, Is.EqualTo(100));
                Assert.That(animator.GetBool(HoldAttack), Is.False);
            }
            finally
            {
                Time.captureFramerate = originalRate;
                Object.Destroy(attacker.gameObject);
                Object.Destroy(bystander.gameObject);
                Object.Destroy(ground);
                Object.Destroy(wall);
                nav.Remove();
                Object.Destroy(data);
            }
        }

        [UnityTest]
        public IEnumerator RiccaCanDodgeTheBoxAndSceneReloadRestoresLife()
        {
            yield return SceneManager.LoadSceneAsync(0);
            var view = Camera.main.GetComponent<ThirdPersonCamera>();
            var player = view.Player;
            player.UseInputSource(new NoInput());
            var spawner = Object.FindObjectOfType<EnemySpawner>();
            var templates = new[] { EnemyKind.Goblin, EnemyKind.Skeleton, EnemyKind.Hellhound }
                .Select(spawner.GetTemplate).ToArray();
            var playerController = player.GetComponent<CharacterController>();
            var input = new NoInput();
            player.UseInputSource(input);
            spawner.enabled = false;
            yield return null;

            Vector3 origin = new Vector3(137f, 43f, 137f);
            var ground = Box("Test ground", origin + Vector3.down * .25f, new Vector3(18f, .5f, 18f));
            var data = BuildTestMesh(ground.transform, null, origin);
            var nav = NavMesh.AddNavMeshData(data);
            int captureFramerate = Time.captureFramerate;
            Time.captureFramerate = 60;
            try
            {
                foreach (var template in templates)
                {
                    SetPlayerPosition(player, origin + Vector3.forward);
                    var enemy = Object.Instantiate(template, origin, Quaternion.identity);
                    Assert.That(Bind(enemy, player), Is.True);
                    yield return WaitFor(() => enemy.Phase == EnemyPhase.Attacking, "in-range enemy should attack");
                    var animator = enemy.GetComponent<Animator>();
                    animator.speed = 0f;
                    SetProgress(animator, .12f);
                    yield return null;
                    SetPlayerPosition(player, origin + Vector3.right * 3f);
                    yield return null;
                    SetProgress(animator, 1.10f);
                    yield return WaitFor(() => enemy.Phase != EnemyPhase.Attacking, "whiff must finish");
                    Assert.That(player.CurrentHp, Is.EqualTo(100), enemy.name);
                    if (enemy.name.StartsWith("Goblin", StringComparison.Ordinal))
                    {
                        Capture(Camera.main, view, enemy, "Enemy-Dodge");
                        SetPlayerPosition(player, origin + Vector3.forward);
                        yield return WaitFor(() => enemy.Phase == EnemyPhase.Attacking, "goblin can attack again");
                        SetProgress(animator, .12f);
                        SetPlayerPosition(player, origin + Vector3.forward + Vector3.right * 1.23f);
                        yield return null;
                        SetPlayerPosition(player, origin + Vector3.forward);
                        SetProgress(animator, 1.4f);
                        yield return WaitFor(() => enemy.Phase != EnemyPhase.Attacking, "late entry attack must finish");
                        Assert.That(player.CurrentHp, Is.EqualTo(100), "Ricca entered only after the active window closed.");
                    }
                    Object.Destroy(enemy.gameObject);
                    yield return null;
                }

                SetPlayerPosition(player, origin + Vector3.forward);
                var skeleton = Object.Instantiate(templates[1], origin, Quaternion.identity);
                Assert.That(Bind(skeleton, player), Is.True);
                var driver = skeleton.GetComponent<Animator>();
                var riccaAnimator = player.GetComponent<Animator>();
                int riccaHit = Animator.StringToHash("Base Layer.Hit");
                int riccaDeath = Animator.StringToHash("Base Layer.Death");
                for (int swing = 0; swing < 10; swing++)
                {
                    yield return WaitFor(() => skeleton.Phase == EnemyPhase.Attacking, "skeleton should attack on cooldown");
                    driver.speed = 0f;
                    SetProgress(driver, 1.10f);
                    yield return WaitFor(() => skeleton.Phase != EnemyPhase.Attacking, "each hit must finish");
                    Assert.That(player.CurrentHp, Is.EqualTo(100 - 10 * (swing + 1)));
                    int reaction = swing == 9 ? riccaDeath : riccaHit;
                    yield return WaitFor(() => riccaAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash == reaction,
                        "an actual enemy strike should start the corresponding Ricca reaction");
                    if (swing == 0)
                    {
                        yield return WaitFor(() => riccaAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= .28f,
                            "Ricca's injury pose should advance");
                        CaptureActor(Camera.main, view, player.transform, "Ricca-Hit");
                    }
                }

                Assert.That(player.IsAlive, Is.False);
                Assert.That(player.IsAttacking, Is.False);
                Assert.That(playerController.enabled, Is.True);
                yield return WaitFor(() => riccaAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f,
                    "the death animation should advance to its final pose");
                Assert.That(riccaAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(riccaDeath));
                CaptureActor(Camera.main, view, player.transform, "Ricca-Death");

                player.gameObject.SetActive(false);
                yield return null;
                player.gameObject.SetActive(true);
                yield return WaitFor(() => riccaAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash == riccaDeath,
                    "reactivation should keep the death pose");
                Assert.That(riccaAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(.99f));
                Object.Destroy(skeleton.gameObject);
            }
            finally
            {
                Time.captureFramerate = captureFramerate;
                nav.Remove();
                Object.Destroy(ground);
                Object.Destroy(data);
            }

            yield return SceneManager.LoadSceneAsync(0);
            Assert.That(Camera.main.GetComponent<ThirdPersonCamera>().Player.CurrentHp, Is.EqualTo(100));
            yield return WaitForRoster();
        }

        [UnityTest]
        public IEnumerator ObstructedGroundSkipsSpawnsAndLimitOneRecoversWhenItClears()
        {
            yield return SceneManager.LoadSceneAsync(0);
            var player = Camera.main.GetComponent<ThirdPersonCamera>().Player;
            player.UseInputSource(new NoInput());
            var original = Object.FindObjectOfType<EnemySpawner>();
            original.enabled = false;
            yield return null;

            Vector3 origin = new Vector3(141f, 48f, 140f);
            var ground = Box("Test spawn ground", origin + Vector3.down * .25f, new Vector3(6f, .5f, 8f));
            Vector3 islandCenter = origin + Vector3.right * 6f;
            var island = Box("Disconnected spawn island", islandCenter + Vector3.down * .25f, new Vector3(3.2f, .5f, 4f));
            var data = BuildTestMesh(ground.transform, island.transform, origin);
            var blockers = new[]
            {
                Box("West spawn obstacle", origin + new Vector3(-4.5f, .6f, 0f), new Vector3(6.3f, 1.2f, 16f)),
                Box("East spawn obstacle", origin + new Vector3(4.5f, .6f, 0f), new Vector3(6.3f, 1.2f, 16f)),
                Box("North spawn obstacle", origin + new Vector3(0f, .6f, 4.5f), new Vector3(2.7f, 1.2f, 6.3f)),
                Box("South spawn obstacle", origin + new Vector3(0f, .6f, -4.5f), new Vector3(2.7f, 1.2f, 6.3f)),
            };
            SetPlayerPosition(player, origin);

            var settings = ScriptableObject.CreateInstance<EnemySpawnSettings>();
            SetField(settings, "_maximumEnemies", 1);
            SetField(settings, "_minimumDistance", 2f);
            SetField(settings, "_maximumDistance", 7f);
            SetField(settings, "_waveInterval", .2f);

            var root = new GameObject("Single slot fixture");
            root.SetActive(false);
            var spawner = root.AddComponent<EnemySpawner>();
            SetField(spawner, "_ricca", player);
            SetField(spawner, "_goblin", original.GetTemplate(EnemyKind.Goblin));
            SetField(spawner, "_skeleton", original.GetTemplate(EnemyKind.Skeleton));
            SetField(spawner, "_hellhound", original.GetTemplate(EnemyKind.Hellhound));
            SetField(spawner, "_navigation", data);
            SetField(spawner, "_settings", settings);

            int captureFramerate = Time.captureFramerate;
            Time.captureFramerate = 60;
            try
            {
                root.SetActive(true);
                for (int frame = 0; frame < 40; frame++)
                {
                    yield return null;
                    Assert.That(spawner.OccupiedSlots, Is.Zero, "blocked spawn region");
                    Assert.That(Object.FindObjectsOfType<EnemyActor>(), Is.Empty);
                }

                foreach (var blocker in blockers)
                    blocker.SetActive(false);
                Physics.SyncTransforms();
                Assert.That(NavMesh.SamplePosition(islandCenter, out var islandHit, .45f, NavMesh.AllAreas), Is.True);
                Assert.That(NavMesh.SamplePosition(origin, out var targetHit, .45f, NavMesh.AllAreas), Is.True);
                var partial = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(islandHit.position, targetHit.position, NavMesh.AllAreas, partial), Is.True);
                Assert.That(partial.status, Is.EqualTo(NavMeshPathStatus.PathPartial));
                Assert.That(SpawnPointAccepted(spawner, islandCenter, origin, targetHit.position), Is.False);
                yield return WaitFor(() => spawner.OccupiedSlots == 1, "single legal slot when blockers leave");
                var first = Object.FindObjectsOfType<EnemyActor>().Single();
                Assert.That(NavMesh.SamplePosition(first.transform.position, out _, .25f, NavMesh.AllAreas), Is.True);
                for (int frame = 0; frame < 12; frame++)
                {
                    yield return null;
                    Assert.That(spawner.OccupiedSlots, Is.EqualTo(1));
                }

                first.enabled = false;
                Assert.That(spawner.OccupiedSlots, Is.EqualTo(1));
                yield return WaitFor(() => !first && spawner.OccupiedSlots == 0, "single slot released after destroy");
                yield return WaitFor(() => spawner.OccupiedSlots == 1, "single slot refilled");
                Assert.That(Object.FindObjectsOfType<EnemyActor>().Length, Is.EqualTo(1));

                var previous = Object.FindObjectsOfType<EnemyActor>().Single();
                root.SetActive(false);
                root.SetActive(true);
                Assert.That(spawner.OccupiedSlots, Is.EqualTo(1));
                yield return WaitFor(() => !previous && spawner.OccupiedSlots == 1,
                    "reenabling waits for actual cleanup, then resumes spawning");
                Assert.That(Object.FindObjectsOfType<EnemyActor>().Length, Is.EqualTo(1));
            }
            finally
            {
                Time.captureFramerate = captureFramerate;
                root.SetActive(false);
                Object.Destroy(root);
                Object.Destroy(settings);
                Object.Destroy(data);
                Object.Destroy(ground);
                Object.Destroy(island);
                foreach (var blocker in blockers)
                    Object.Destroy(blocker);
            }
        }

        private static void AssertCityObstacle(EnemySpawner spawner, Vector3 point, string name, Vector3 ricca,
            Vector3 riccaOnNavMesh)
        {
            var solids = Physics.OverlapSphere(point + Vector3.up * .5f, .04f, 1, QueryTriggerInteraction.Ignore);
            Assert.That(solids.Any(collider => collider.name == name), Is.True, name);
            Assert.That(SpawnPointAccepted(spawner, point, ricca, riccaOnNavMesh), Is.False, name);
        }

        private static bool SpawnPointAccepted(EnemySpawner spawner, Vector3 candidate, Vector3 target, Vector3 targetNav)
        {
            var method = typeof(EnemySpawner).GetMethod("TryFindSpawnPoint", BindingFlags.Instance | BindingFlags.NonPublic);
            return (bool)method.Invoke(spawner, new object[]
            {
                spawner.GetTemplate(EnemyKind.Goblin), candidate, target, targetNav, Vector3.zero, Quaternion.identity
            });
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);


        internal static IEnumerator WaitForRoster()
        {
            return WaitFor(() => Object.FindObjectsOfType<EnemyActor>().Length == 3, "opening roster must contain three enemies");
        }

        private static IEnumerator WaitFor(Func<bool> predicate, string message, float seconds = 8f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(predicate(), Is.True, message);
        }

        private static bool Bind(EnemyActor enemy, RiccaPlayer player)
        {
            var method = typeof(EnemyActor).GetMethod("BindRicca", BindingFlags.Instance | BindingFlags.NonPublic);
            return (bool)method.Invoke(enemy, new object[] { player, (LayerMask)1 });
        }

        private static void SetProgress(Animator animator, float progress)
        {
            animator.Play(Attack, 0, progress);
            animator.Update(0f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(progress).Within(.01f));
        }

        private static void SetPlayerPosition(RiccaPlayer player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static GameObject Box(string name, Vector3 position, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = size;
            return box;
        }

        private static NavMeshData BuildTestMesh(Transform floor, Transform wall, Vector3 origin)
        {
            var sources = new List<NavMeshBuildSource> { MeshSource(floor) };
            if (wall)
                sources.Add(MeshSource(wall));

            return NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(origin, new Vector3(19f, 6f, 19f)), Vector3.zero, Quaternion.identity);
        }

        private static NavMeshBuildSource MeshSource(Transform collider)
        {
            return new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(collider.position, collider.rotation, Vector3.one),
                size = collider.localScale,
            };
        }

        internal static void Capture(Camera camera, ThirdPersonCamera view, EnemyActor enemy, string name) =>
            CaptureActor(camera, view, enemy.transform, name);

        private static void CaptureActor(Camera camera, ThirdPersonCamera view, Transform actor, string name)
        {
            string directory = Environment.GetEnvironmentVariable("IDLERICCA_ENEMY_VERIFY_OUTPUT");
            if (string.IsNullOrEmpty(directory))
                return;

            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            var texture = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            bool wasEnabled = view.enabled;
            try
            {
                view.enabled = false;
                Vector3 aim = actor.position + Vector3.up * .65f;
                for (int step = 0; step < 8; step++)
                {
                    Vector3 heading = Quaternion.Euler(0f, 35f + step * 45f, 0f) * actor.forward;
                    Vector3 offset = heading * 2.8f + Vector3.up * 1.05f;
                    if (Physics.Raycast(aim, offset.normalized, offset.magnitude, 1, QueryTriggerInteraction.Ignore))
                        continue;

                    camera.transform.position = aim + offset;
                    break;
                }

                camera.transform.LookAt(aim);
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(texture);
                Object.Destroy(pixels);
                view.enabled = wasEnabled;
            }
        }

        internal sealed class NoInput : IRiccaInput
        {
            public RiccaInputFrame Read() => default;
        }
    }
}
