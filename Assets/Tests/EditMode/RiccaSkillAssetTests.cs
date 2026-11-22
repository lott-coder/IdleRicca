using System.Linq;
using IdleRicca.Domain.Gameplay;
using IdleRicca.Presentation.Gameplay;
using IdleRicca.Presentation.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IdleRicca.Tests
{
    public sealed class RiccaSkillAssetTests
    {
        [Test]
        public void SkillAssetsBindAllTenSourceClipsAndFiveSourceEffects()
        {
            var config = AssetDatabase.LoadAssetAtPath<RiccaSkillSettings>("Assets/Config/Gameplay/RiccaSkills.asset");
            Assert.That(config, Is.Not.Null);
            Assert.That(config.SlowMultiplier, Is.EqualTo(.2f));
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Demo/Ricca.prefab").GetComponent<RiccaPlayer>();
            var playerData = new SerializedObject(player);
            Assert.That(playerData.FindProperty("_skillSettings").objectReferenceValue, Is.SameAs(config));
            Assert.That(playerData.FindProperty("_skillHitbox").objectReferenceValue, Is.InstanceOf<SkillHitbox>());
            var controller = (AnimatorController)((Animator)playerData.FindProperty("_animator").objectReferenceValue).runtimeAnimatorController;
            var rows = new SerializedObject(config).FindProperty("_skills");
            Assert.That(rows.arraySize, Is.EqualTo(5));
            string[] expectedMain = { "rc_stand_magicShot", "rc_stand_specialAttack_rush", "rc_stand_specialAttack_charge", "rck_stand_specialAttack_frozen", "rc_stand_specialAttack_geyser" };
            string[] expectedVariants = { "rc_stand_magicUpperShot", "rc_sit_magicShot", "rc_air_magicShot", "rc_air_magicUpperShot", "rc_air_magicLowerShot" };
            for (int i = 0; i < rows.arraySize; i++)
            {
                var row = rows.GetArrayElementAtIndex(i);
                Assert.That((SkillId)row.FindPropertyRelative("_id").intValue, Is.EqualTo((SkillId)((int)SkillId.SwordThrow + i)));
                var clip = (AnimationClip)row.FindPropertyRelative("_animation").objectReferenceValue;
                AssertSourceClip(controller, clip, expectedMain[i]);
                var variants = row.FindPropertyRelative("_variants");
                Assert.That(variants.arraySize, Is.EqualTo(i == 0 ? expectedVariants.Length : 0));
                for (int variant = 0; variant < variants.arraySize; variant++)
                    AssertSourceClip(controller, (AnimationClip)variants.GetArrayElementAtIndex(variant).objectReferenceValue,
                        expectedVariants[variant]);
                Assert.That(row.FindPropertyRelative("_damage2").intValue, Is.GreaterThan(row.FindPropertyRelative("_damage1").intValue));
                Assert.That(row.FindPropertyRelative("_damage3").intValue, Is.GreaterThan(row.FindPropertyRelative("_damage2").intValue));
                var effect = (GameObject)row.FindPropertyRelative("_effectRoot").objectReferenceValue;
                Assert.That(effect, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(effect), Does.StartWith("Assets/Art/Demo/Ricca/SourceSkills/"));
                Assert.That(effect.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
                Assert.That(effect.GetComponentsInChildren<Collider>(true), Is.Empty);
                foreach (Transform child in effect.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero);
            }

            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/SkillSelectionHud.prefab").GetComponent<SkillSelectionHud>();
            Assert.That(new SerializedObject(hud).FindProperty("_buttons").arraySize, Is.EqualTo(4));
        }

        private static void AssertSourceClip(AnimatorController controller, AnimationClip clip, string expectedName)
        {
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.name, Is.EqualTo(expectedName));
            Assert.That(AssetDatabase.GetAssetPath(clip),
                Is.EqualTo("Assets/Art/Demo/Ricca/SourceSkills/AnimationClip/" + expectedName + ".anim"));
            Assert.That(clip.isLooping, Is.False);
            Assert.That(AnimationUtility.GetCurveBindings(clip).Length, Is.GreaterThan(200));
            Assert.That(controller.layers[0].stateMachine.states.Any(x => x.state.name == clip.name && x.state.motion == clip), Is.True);
        }
    }
}
