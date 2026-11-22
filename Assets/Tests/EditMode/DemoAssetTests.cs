using System.IO;
using System.Linq;
using IdleRicca.Application.Gameplay;
using IdleRicca.Presentation.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

namespace IdleRicca.Tests
{
    public sealed class DemoAssetTests
    {
        private const string ScenePath = "Assets/Scenes/Demo.unity";

        [Test]
        public void WaveSettingsAssetUsesFiftyPercentSkillWeight()
        {
            var asset = AssetDatabase.LoadAssetAtPath<WaveSettings>("Assets/Config/Gameplay/WaveSettings.asset");
            Assert.That(asset, Is.Not.Null);
            using var serialized = new SerializedObject(asset);
            Assert.That(serialized.FindProperty("_skillWeight").intValue, Is.EqualTo(50));
            Assert.That(serialized.FindProperty("_healthWeight").intValue, Is.EqualTo(25));
            Assert.That(serialized.FindProperty("_attackWeight").intValue, Is.EqualTo(25));
        }

        [Test]
        public void DemoIsTheBuildEntryWithIntactCityAssets()
        {
            Assert.That(EditorBuildSettings.scenes.First(scene => scene.enabled).path, Is.EqualTo(ScenePath));
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var objects = scene.GetRootGameObjects();
                var city = objects.Single(root => root.name == "City Environment");
                foreach (string name in new[] { "Street", "House", "Props", "Environment", "Decals" })
                    Assert.That(city.transform.Find(name), Is.Not.Null, name);
                Assert.That(objects.Any(root => root.name == "Room Environment"), Is.False);

                var terrain = city.GetComponentInChildren<Terrain>();
                Assert.That(terrain, Is.Not.Null);
                Assert.That(terrain.terrainData, Is.Not.Null);
                Assert.That(terrain.terrainData.terrainLayers, Is.Not.Empty);
                Assert.That(terrain.terrainData.terrainLayers, Is.All.Not.Null);
                foreach (var layer in terrain.terrainData.terrainLayers)
                {
                    Assert.That(layer.diffuseTexture, Is.Not.Null, layer.name);
                    Assert.That(layer.normalMapTexture, Is.Not.Null, layer.name);
                }
                Assert.That(terrain.materialTemplate.shader.name, Is.EqualTo("Nature/Terrain/Standard"));
                Assert.That(terrain.GetComponent<TerrainCollider>().terrainData, Is.SameAs(terrain.terrainData));

                var lodGroups = city.GetComponentsInChildren<LODGroup>(true);
                Assert.That(lodGroups.Length, Is.GreaterThan(1000));
                foreach (var lod in lodGroups.SelectMany(group => group.GetLODs()))
                    Assert.That(lod.renderers, Is.All.Not.Null);

                var renderers = city.GetComponentsInChildren<MeshRenderer>(true).Where(renderer => renderer.enabled).ToArray();
                Assert.That(renderers.Length, Is.GreaterThan(3000), "Retain the authored street and building submesh ranges.");
                var cityMaterials = renderers.SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null).Distinct().ToArray();
                AssertTexture(cityMaterials, "Road_Patches_01", "_MainTex");
                AssertTexture(cityMaterials, "Road_Patches_01", "_BumpMap");
                AssertTexture(cityMaterials, "V01_House_Wall_01", "_MainTex");
                AssertTexture(cityMaterials, "Vending_Machine", "_MainTex");
                AssertTexture(cityMaterials, "Flowers", "_MainTex");
                AssertTexture(cityMaterials, "Flowers", "_BumpMap");
                AssertSurface(cityMaterials, "Glass_House", false);
                AssertSurface(cityMaterials, "Glass_House_Fade", true);
                foreach (var renderer in renderers)
                    Assert.That(renderer.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null, renderer.name);

                foreach (var root in objects)
                {
                    AssertComponents(root);
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                    {
                        foreach (var material in renderer.sharedMaterials)
                        {
                            Assert.That(material, Is.Not.Null, renderer.name);
                            Assert.That(material.shader.name, Is.EqualTo("Standard"), material.name);
                        }
                    }
                }

                var characters = objects.Single(root => root.name == "Characters");
                Assert.That(characters.transform.childCount, Is.EqualTo(1));
                Assert.That(characters.transform.GetChild(0).name, Is.EqualTo("Ricca"));
                var spawner = objects.Select(root => root.GetComponent<EnemySpawner>())
                    .Single(component => component);
                Assert.That(spawner.MaximumEnemies, Is.EqualTo(3));
                foreach (EnemyKind kind in new[] { EnemyKind.Goblin, EnemyKind.Skeleton, EnemyKind.Hellhound })
                    Assert.That(spawner.GetTemplate(kind).gameObject.name, Is.EqualTo(kind.ToString()));
                Assert.That(new SerializedObject(spawner).FindProperty("_navigation").objectReferenceValue,
                    Is.InstanceOf<NavMeshData>());
                Physics.SyncTransforms();
                foreach (Transform child in characters.transform)
                {
                    Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject),
                        Is.EqualTo($"Assets/Prefabs/Demo/{child.name}.prefab"));
                    var origin = child.position + Vector3.up * .3f;
                    Assert.That(Physics.Raycast(origin, Vector3.down, out var hit, .65f, ~0,
                        QueryTriggerInteraction.Ignore), Is.True, child.name);
                    Assert.That(hit.collider.transform.IsChildOf(city.transform), Is.True, child.name);
                    Assert.That(Mathf.Abs(hit.point.y - child.position.y), Is.LessThan(.05f), child.name);
                }
            }
            finally
            {
                if (openedForTest)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase("Ricca")]
        [TestCase("Goblin")]
        [TestCase("Skeleton")]
        [TestCase("Hellhound")]
        public void ActorHasARigWithTexturedMeshesAndAllExpectedClips(string name)
        {
            var actor = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Demo/{name}.prefab");
            Assert.That(actor, Is.Not.Null);
            AssertComponents(actor);
            var animator = actor.GetComponent<Animator>();
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            if (name == "Ricca")
                Assert.That(actor.CompareTag("Player"), Is.True);

            var renderers = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.enabled && renderer.gameObject.activeSelf).ToArray();
            Assert.That(renderers.Length, Is.Positive);
            foreach (var mesh in renderers)
            {
                Assert.That(mesh.sharedMesh, Is.Not.Null, mesh.name);
                Assert.That(mesh.rootBone, Is.Not.Null, mesh.name);
                Assert.That(mesh.bones.Length, Is.EqualTo(mesh.sharedMesh.bindposes.Length), mesh.name);
                Assert.That(mesh.bones, Is.All.Not.Null, mesh.name);
                Assert.That(mesh.sharedMaterials.Length, Is.EqualTo(mesh.sharedMesh.subMeshCount), mesh.name);
                Assert.That(mesh.sharedMaterials.Any(material => material != null && material.mainTexture != null),
                    Is.True, mesh.name);
            }

            var clips = animator.runtimeAnimatorController.animationClips;
            Assert.That(clips.Length, Is.EqualTo(name == "Ricca" ? 16 : 5));
            if (name == "Ricca")
            {
                Assert.That(clips.Select(clip => clip.name), Is.EquivalentTo(new[]
                {
                    "rck_stand_wait_shortSword_loop", "rc_walk_loop", "rc_run_loop", "rc_stand_normalAttack_01",
                    "rc_stand_damage", "rc_death",
                    "rc_stand_magicShot",
                    "rc_stand_specialAttack_rush",
                    "rc_stand_specialAttack_charge",
                    "rck_stand_specialAttack_frozen",
                    "rc_stand_specialAttack_geyser",
                    "rc_stand_magicUpperShot",
                    "rc_sit_magicShot",
                    "rc_air_magicShot",
                    "rc_air_magicUpperShot",
                    "rc_air_magicLowerShot"
                }));
                Assert.That(clips.Single(clip => clip.name == "rc_run_loop").isLooping, Is.True);
                Assert.That(actor.GetComponent<RiccaPlayer>(), Is.Not.Null);
                Assert.That(actor.GetComponent<CharacterController>(), Is.Not.Null);
                var blade = actor.GetComponent<SwordHitbox>().Geometry;
                Assert.That(blade.enabled, Is.False);
                var attack = AssetDatabase.LoadAssetAtPath<NormalAttackSettings>(
                    "Assets/Config/Gameplay/RiccaNormalAttack.asset");
                Assert.That(attack.HasValidTrajectory(), Is.True);
                Assert.That(attack.Damage, Is.EqualTo(25));
                Assert.That(attack.HitStopSeconds, Is.EqualTo(0.10f));
                Assert.That(attack.HitStopMultiplier, Is.EqualTo(0.05f));
                Assert.That(attack.Clip, Is.SameAs(clips.Single(clip => clip.name == "rc_stand_normalAttack_01")));
                var controller = (AnimatorController)animator.runtimeAnimatorController;
                var authoredAttack = controller.layers[0].stateMachine.states.Single(entry => entry.state.name == "Attack");
                Assert.That(authoredAttack.state.motion, Is.SameAs(attack.Clip));
                var riccaStates = controller.layers[0].stateMachine.states;
                var hit = riccaStates.Single(entry => entry.state.name == "Hit").state;
                var death = riccaStates.Single(entry => entry.state.name == "Death").state;
                Assert.That(hit.motion, Is.SameAs(clips.Single(clip => clip.name == "rc_stand_damage")));
                Assert.That(death.motion, Is.SameAs(clips.Single(clip => clip.name == "rc_death")));
                Assert.That(((AnimationClip)hit.motion).isLooping, Is.False);
                Assert.That(((AnimationClip)death.motion).isLooping, Is.False);
                Assert.That(hit.transitions.Single().destinationState.name, Is.EqualTo("Idle"));
                Assert.That(death.transitions, Is.Empty);
            }
            else
            {
                Assert.That(actor.GetComponent<EnemyTarget>(), Is.Not.Null);
                var enemy = actor.GetComponent<EnemyActor>();
                Assert.That(enemy, Is.Not.Null);
                Assert.That(enemy.IsConfigured, Is.True);
                Assert.That(enemy.AttackBox.isTrigger && enemy.AttackBox.enabled, Is.True);
                Assert.That(enemy.AttackBox.gameObject.layer, Is.EqualTo(2));
                var controller = (AnimatorController)animator.runtimeAnimatorController;
                var states = controller.layers[0].stateMachine.states;
                Assert.That(states.Single(entry => entry.state.name == "Hit").state.motion, Is.SameAs(enemy.Settings.HitClip));
                Assert.That(states.Single(entry => entry.state.name == "Death").state.motion, Is.SameAs(enemy.Settings.DeathClip));
                Assert.That(new[] { enemy.Settings.HitClip, enemy.Settings.DeathClip }
                        .Select(clip => Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(clip))),
                    Is.EqualTo(name == "Goblin" ? new[] { "damage_7", "death_7" } :
                        name == "Skeleton" ? new[] { "skeleton_damage", "skeleton_death" } :
                        new[] { "hellhound_damage", "hellhound_death" }));
                var hurtbox = enemy.Body;
                Assert.That(hurtbox.isTrigger, Is.True);
                Assert.That(hurtbox.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("EnemyHurtbox")));
                var health = AssetDatabase.LoadAssetAtPath<EnemyHealthSettings>(
                    $"Assets/Config/Gameplay/{name}Health.asset");
                Assert.That(health.StartingHp, Is.EqualTo(100));
            }
            foreach (var clip in clips)
            {
                Assert.That(clip.length, Is.Positive);
                Assert.That(clip.events, Is.Empty);
                var bindings = AnimationUtility.GetCurveBindings(clip);
                Assert.That(bindings.Any(binding => binding.type == typeof(Transform) &&
                    !string.IsNullOrEmpty(binding.path) && actor.transform.Find(binding.path)), Is.True, clip.name);
            }
        }

        private static void AssertSurface(Material[] materials, string name, bool transparent)
        {
            var material = materials.SingleOrDefault(candidate => candidate.name == name);
            Assert.That(material, Is.Not.Null, name);
            Assert.That(material.GetInt("_ZWrite"), Is.EqualTo(transparent ? 0 : 1), name);
            Assert.That(material.renderQueue >= 3000, Is.EqualTo(transparent), name);
        }

        private static void AssertTexture(Material[] materials, string name, string property)
        {
            var material = materials.SingleOrDefault(candidate => candidate.name == name);
            Assert.That(material, Is.Not.Null, name);
            Assert.That(material.GetTexture(property), Is.Not.Null, name + " " + property);
        }

        private static void AssertComponents(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject),
                    Is.Zero, transform.name);
        }
    }
}
