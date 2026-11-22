using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IdleRicca.Application.Gameplay;
using IdleRicca.Domain.Gameplay;
using IdleRicca.Presentation.Gameplay;
using IdleRicca.Presentation.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Phase = IdleRicca.Application.Gameplay.WaveStatus.RunPhase;

namespace IdleRicca.Tests
{
    public sealed class RiccaSkillGameplayTests
    {
        private const BindingFlags InternalInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly PropertyInfo Actions = typeof(RiccaPlayer).GetProperty("Actions", InternalInstance);
        private static readonly PropertyInfo Health = typeof(EnemyTarget).GetProperty("Health", InternalInstance);
        private static readonly MethodInfo ShowHit = typeof(EnemyTarget).GetMethod("ShowHitResult", InternalInstance);
        private static readonly MethodInfo Focus = typeof(RiccaPlayer).GetMethod("OnApplicationFocus", InternalInstance);
        private static readonly string[] Animations = {
            "rc_stand_magicShot", "rc_stand_specialAttack_rush", "rc_stand_specialAttack_charge",
            "rck_stand_specialAttack_frozen", "rc_stand_specialAttack_geyser"
        };
        private static readonly int[,] Damages = { { 34, 46, 60 }, { 29, 40, 54 }, { 38, 52, 68 }, { 32, 45, 60 }, { 40, 55, 72 } };
        private static readonly string[] Effects = { "RicassoMagicSwordShotEdge", "LightningRush", "EnchantFire", "FrozenOrbBullet", "HolyGeyser" };

        [UnityTest]
        public IEnumerator DemoAwardsAndCastsFourSourceSkillsThroughTheActualHud()
        {
            yield return SceneManager.LoadSceneAsync(0);
            yield return null;
            var player = Camera.main.GetComponent<ThirdPersonCamera>().Player;
            var input = new SkillInput();
            player.UseInputSource(input);
            var spawner = Object.FindObjectOfType<EnemySpawner>();
            var hud = Object.FindObjectOfType<SkillSelectionHud>();
            var waveHud = Object.FindObjectOfType<WaveHud>();
            var panel = hud.transform.Find("Skill choice overlay").gameObject;
            var slots = panel.GetComponentsInChildren<Button>(true);
            var playerActions = (PlayerActions)Actions.GetValue(player);
            var animator = player.GetComponentInChildren<Animator>();
            var run = (WaveRun)typeof(EnemySpawner).GetField("_waveRun", InternalInstance).GetValue(spawner);
            typeof(WaveRun).GetField("_rewards", InternalInstance).SetValue(run, Pool(FirstFourSkillSeed()));
            var camera = Camera.main.GetComponent<ThirdPersonCamera>();
            var originalScale = Time.timeScale;
            var originalStep = Time.fixedDeltaTime;
            int originalCaptureRate = Time.captureFramerate;

            try
            {
                Time.timeScale = .8f;
                Time.fixedDeltaTime = .017f;
                input.Next = Toggle();
                yield return Until(() => panel.activeInHierarchy, "empty selection");
                Assert.That(slots.Length, Is.EqualTo(4));
                Assert.That(slots.All(slot => !slot.interactable), Is.True);
                Assert.That(panel.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("wave rewards")), Is.True);
                AssertTime(.8f, .017f);
                CapturePair("01-Empty-Slots");

                input.Next = new RiccaInputFrame(1, 1, Vector2.one, true, true, false, true);
                yield return Until(() => !panel.activeInHierarchy, "Esc cancels the empty selection");
                Assert.That(player.IsAttacking, Is.False);
                AssertTime(.8f, .017f);

                Time.timeScale = 8f;
                Time.fixedDeltaTime = originalStep;
                for (int wave = 1; wave <= 4; wave++)
                {
                    yield return ClearWave(run, wave, playerActions);
                    WaveStatus offer = run.Status;
                    int card = SkillCard(offer);
                    var button = waveHud.transform.Find("Reward Panel/Reward Card/Reward " + (card + 1)).GetComponent<Button>();
                    Assert.That(button.GetComponentInChildren<TMP_Text>().text, Does.StartWith("Learn "));
                    Assert.That(run.TryGetSkillRewardPreview(offer.OfferId, card, out SkillRewardPreview preview), Is.True);
                    Click(button);
                    Assert.That(playerActions.SkillStatus.GetSkillId(wave - 1), Is.EqualTo(preview.Skill));
                    Assert.That(playerActions.SkillStatus.GetLevel(wave - 1), Is.EqualTo(offer.GetReward(card).Level));
                    yield return null;
                }

                spawner.enabled = false;
                yield return null;
                Time.timeScale = .8f;
                Time.fixedDeltaTime = .017f;
                Time.captureFramerate = 60;
                var template = spawner.GetTemplate(EnemyKind.Goblin);
                var combat = Object.FindObjectOfType<CombatHud>();
                var manaText = combat.transform.Find("Status/Mana value").GetComponent<TMP_Text>();
                var manaFill = combat.transform.Find("Status/Mana fill");

                for (int slot = 0; slot < slots.Length; slot++)
                {
                    var skill = playerActions.SkillStatus.GetSkillId(slot);
                    int configuredIndex = (int)skill - (int)SkillId.SwordThrow;
                    int damage = Damages[configuredIndex, playerActions.SkillStatus.GetLevel(slot) - 1];
                    Vector3 origin = player.transform.position;
                    Vector3 forward = player.transform.forward;
                    Vector3 right = player.transform.right;
                    var first = SpawnTarget(template, origin + forward * .95f + right * .15f, true);
                    var second = SpawnTarget(template, origin + forward * 1.1f - right * .18f, false);
                    var outside = SpawnTarget(template, origin - forward * 2.8f, false);
                    yield return null;
                    Physics.SyncTransforms();
                    input.Next = Toggle();
                    yield return Until(() => panel.activeInHierarchy, "selection opens for " + skill);
                    Assert.That(slots.All(button => button.interactable), Is.True);
                    AssertTime(.16f, .0034f);
                    if (slot == 0)
                    {
                        CapturePair("02-Ready-Skills");
                        float yaw = camera.Yaw;
                        input.Next = new RiccaInputFrame(1, 1, Vector2.one, true, true);
                        yield return null;
                        Assert.That(camera.Yaw, Is.EqualTo(yaw));
                        Assert.That(player.IsAttacking, Is.False);
                        Time.timeScale = 0f;
                        Click(slots[slot]);
                        Assert.That(playerActions.CurrentMp, Is.EqualTo(100));
                        Assert.That(playerActions.IsCastingSkill, Is.False);
                        yield return Until(() => !panel.activeInHierarchy, "external pause closes selection");
                        Time.timeScale = .8f;
                        input.Next = Toggle();
                        yield return Until(() => panel.activeInHierarchy, "selection opens after pause");
                    }

                    Click(slots[slot]);
                    AssertTime(.8f, .017f);
                    Assert.That(player.IsAttacking, Is.False);
                    Assert.That(playerActions.IsCastingSkill, Is.True);
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + Animations[configuredIndex]), Is.True);
                    int expectedMp = 100 - (slot + 1) * PlayerActions.SkillManaCost;
                    Assert.That(playerActions.CurrentMp, Is.EqualTo(expectedMp));
                    Assert.That(first.CurrentHp, Is.EqualTo(100));
                    var effect = GetEffect(player, skill);
                    var particles = effect.GetComponentsInChildren<ParticleSystem>(true);
                    yield return Until(() => effect.activeInHierarchy && particles.Any(value => value.particleCount > 0),
                        "source particles play for " + skill);
                    CapturePair("FX-" + skill);
                    Assert.That(manaText.text, Is.EqualTo(expectedMp + " / 100"));
                    Assert.That(manaFill.localScale.x, Is.EqualTo(expectedMp / 100f).Within(.0001f));
                    yield return Until(() => !playerActions.IsCastingSkill, "cast completes for " + skill);
                    Assert.That(first.CurrentHp, Is.EqualTo(100 - damage), "Duplicate hurtboxes for " + skill);
                    Assert.That(second.CurrentHp, Is.EqualTo(100 - damage), "Second target inside " + skill);
                    Assert.That(outside.CurrentHp, Is.EqualTo(100), "Target behind " + skill);
                    DestroyTarget(first);
                    DestroyTarget(second);
                    DestroyTarget(outside);
                    yield return null;
                }

                RecordSteadyUiAllocations(player, hud);
                input.Next = Toggle();
                typeof(RiccaPlayer).GetMethod("Update", InternalInstance).Invoke(player, null);
                Assert.That(playerActions.SkillStatus.IsSelecting, Is.True);
                hud.enabled = false;
                Assert.That(playerActions.SkillStatus.IsSelecting, Is.False, "HUD teardown cancels a selection before its first repaint.");
                AssertTime(.8f, .017f);
                input.Next = Toggle();
                yield return null;
                yield return null;
                Assert.That(playerActions.SkillStatus.IsSelecting, Is.False, "A disabled HUD cannot capture input.");
                hud.enabled = true;

                input.Next = Toggle();
                yield return Until(() => panel.activeInHierarchy, "selection before an external pause");
                Time.timeScale = 0f;
                yield return Until(() => !panel.activeInHierarchy, "external pause cancels the HUD");
                AssertTime(0f, .017f);
                Time.timeScale = .8f;

                input.Next = Toggle();
                yield return Until(() => panel.activeInHierarchy, "selection before focus loss");
                Focus.Invoke(player, new object[] { false });
                AssertTime(.8f, .017f);
                yield return Until(() => !panel.activeInHierarchy, "focus loss closes the HUD");

                input.Next = Toggle();
                yield return Until(() => panel.activeInHierarchy, "selection before hit");
                DamagePlayer(playerActions);
                yield return Until(() => !panel.activeInHierarchy, "hit closes the HUD");
                AssertTime(.8f, .017f);

                input.Next = Toggle();
                yield return Until(() => panel.activeInHierarchy, "selection before unload");
                yield return SceneManager.LoadSceneAsync(0);
                yield return null;
                AssertTime(.8f, .017f);
                var fresh = Camera.main.GetComponent<ThirdPersonCamera>().Player;
                Assert.That(fresh.TryGetSkillStatus(out SkillStatus slotsAfterReload), Is.True);
                Assert.That(slotsAfterReload.GetSkillId(0), Is.EqualTo(SkillId.None));
                Assert.That(slotsAfterReload.CurrentMp, Is.EqualTo(100));
            }
            finally
            {
                Time.captureFramerate = originalCaptureRate;
                Time.timeScale = originalScale;
                Time.fixedDeltaTime = originalStep;
            }
        }

        [UnityTest]
        public IEnumerator EverySourceSkillDamagesThroughItsOwnClipAndShowsImportedEffects()
        {
            float rate = Time.timeScale;
            float fixedStep = Time.fixedDeltaTime;
            try
            {
                for (int i = 0; i < Animations.Length; i++)
                {
                    var id = (SkillId)((int)SkillId.SwordThrow + i);
                    Time.timeScale = 5f;
                    yield return SceneManager.LoadSceneAsync(0);
                    yield return null;
                    var player = Camera.main.GetComponent<ThirdPersonCamera>().Player;
                    var input = new SkillInput();
                    player.UseInputSource(input);
                    var actions = (PlayerActions)Actions.GetValue(player);
                    var spawner = Object.FindObjectOfType<EnemySpawner>();
                    var run = (WaveRun)typeof(EnemySpawner).GetField("_waveRun", InternalInstance).GetValue(spawner);
                    typeof(WaveRun).GetField("_rewards", InternalInstance).SetValue(run, Pool(FirstFourSkillSeed()));
                    int seed = Enumerable.Range(0, 100).First(value => new System.Random(value).Next(Animations.Length) == i);
                    typeof(WaveRun).GetField("_skillRandom", InternalInstance).SetValue(run, new System.Random(seed));

                    yield return ClearWave(run, 1, actions);
                    var offer = run.Status;
                    int card = SkillCard(offer);
                    var waveHud = Object.FindObjectOfType<WaveHud>();
                    Click(waveHud.transform.Find("Reward Panel/Reward Card/Reward " + (card + 1)).GetComponent<Button>());
                    Assert.That(actions.SkillStatus.GetSkillId(0), Is.EqualTo(id));
                    spawner.enabled = false;

                    Time.timeScale = 1f;
                    Time.fixedDeltaTime = fixedStep;
                    yield return null;
                    var skillHud = Object.FindObjectOfType<SkillSelectionHud>();
                    var panel = skillHud.transform.Find("Skill choice overlay").gameObject;
                    Button slot = panel.GetComponentsInChildren<Button>(true)[0];
                    var animator = player.GetComponent<Animator>();
                    Vector3 origin = player.transform.position;
                    Vector3 forward = player.transform.forward;
                    var template = spawner.GetTemplate(EnemyKind.Goblin);
                    var inside = SpawnTarget(template, origin + forward * 1.3f, true);
                    var outside = SpawnTarget(template, origin + forward * 1.3f + player.transform.right * 3f, false);
                    yield return null;
                    Physics.SyncTransforms();

                    input.Next = Toggle();
                    yield return Until(() => panel.activeInHierarchy, "open source skill " + id);
                    Click(slot);
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + Animations[i]), Is.True, id.ToString());
                    Assert.That(inside.CurrentHp, Is.EqualTo(100));
                    Assert.That(actions.CurrentMp, Is.EqualTo(90));
                    GameObject effect = GetEffect(player, id);
                    var particles = effect.GetComponentsInChildren<ParticleSystem>(true);
                    yield return Until(() => effect.activeInHierarchy && particles.Any(system => system.particleCount > 0),
                        "active source effect for " + id);
                    yield return new WaitForSeconds(id == SkillId.HolyGeyser ? .28f : .09f);
                    CapturePair("Source-" + id);
                    yield return Until(() => inside.CurrentHp < 100, "damage from " + id);
                    Assert.That(RiccaGameplayTests.ActiveImpactCount("SwordHitBlue"), Is.EqualTo(1),
                        "A skill must play one impact even when the enemy has two hurtboxes: " + id);
                    yield return null;
                    CapturePair("Impact-" + id);
                    yield return Until(() => !actions.IsCastingSkill, "finish source skill " + id);
                    Assert.That(inside.CurrentHp, Is.EqualTo(100 - Damages[i, actions.SkillStatus.GetLevel(0) - 1]), id.ToString());
                    Assert.That(outside.CurrentHp, Is.EqualTo(100), id.ToString());
                    DestroyTarget(inside);
                    DestroyTarget(outside);
                    yield return null;

                    if (id == SkillId.BlazeCharge)
                        yield return CheckBlazeChargeAfterCast(player, actions, effect, particles);

                    if (id == SkillId.SwordThrow || id == SkillId.BlazeCharge || id == SkillId.FrozenOrb)
                    {
                        yield return CheckSweptWindowAndWall(player, input, slot, panel, actions, template, animator, i);
                        yield return CheckObstructedLaunch(player, input, slot, panel, actions, template, animator);
                    }
                    if (id == SkillId.SwordThrow)
                        yield return CheckSwordAimVariants(player, input, slot, panel, actions, animator);
                }
            }
            finally
            {
                Time.timeScale = rate;
                Time.fixedDeltaTime = fixedStep;
            }
        }

        private static IEnumerator CheckBlazeChargeAfterCast(RiccaPlayer player, PlayerActions actions,
            GameObject effect, ParticleSystem[] particles)
        {
            BoxCollider blade = player.GetComponent<SwordHitbox>().Geometry;
            Assert.That(effect.transform.parent, Is.SameAs(blade.transform));
            Assert.That(effect.transform.localPosition, Is.EqualTo(blade.center));
            Assert.That(actions.AttackDamage, Is.EqualTo(30));

            float rate = Time.timeScale;
            double started = Time.timeAsDouble;
            try
            {
                Time.timeScale = 8f;
                yield return new WaitForSeconds(5f);
                Assert.That(effect.activeSelf && particles.Any(system => system.particleCount > 0), Is.True);
                Assert.That(actions.AttackDamage, Is.EqualTo(30));
                CapturePair("Blaze-Charge-Sword-Buff");
                yield return Until(() => !effect.activeSelf, "the sword flame expires");
                Assert.That(Time.timeAsDouble - started, Is.InRange(19d, 21d));
                Assert.That(actions.AttackDamage, Is.EqualTo(25));
                CapturePair("Blaze-Charge-Expired");
            }
            finally
            {
                Time.timeScale = rate;
            }
        }

        private static IEnumerator CheckSweptWindowAndWall(RiccaPlayer player, SkillInput input, Button slot, GameObject panel,
            PlayerActions actions, EnemyActor template, Animator animator, int skillIndex)
        {
            Vector3 origin = player.transform.position;
            Vector3 forward = player.transform.forward;
            var crossed = SpawnTarget(template, origin + forward * 2.52f, true);
            var hidden = SpawnTarget(template, origin + forward * 4.45f, false);
            var beyond = SpawnTarget(template, origin + forward * 7.6f, false);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Skill path wall";
            wall.transform.SetPositionAndRotation(origin + forward * 3.5f + Vector3.up, player.transform.rotation);
            wall.transform.localScale = new Vector3(3f, 2f, .15f);
            yield return null;
            Physics.SyncTransforms();
            try
            {
                input.Next = Toggle();
                yield return Until(() => panel.activeInHierarchy, "open coarse path skill");
                Click(slot);
                int state = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
                animator.speed = 0f;
                SampleCast(player, animator, state, .15f);
                Assert.That(crossed.CurrentHp, Is.EqualTo(100), "No hit before the active window.");
                SampleCast(player, animator, state, 1f);
                Assert.That(crossed.CurrentHp, Is.EqualTo(100 - Damages[skillIndex, actions.SkillStatus.GetLevel(0) - 1]),
                    "The active segment crosses the target within a single coarse step.");
                Assert.That(hidden.CurrentHp, Is.EqualTo(100), "A wall bounds the sweep.");
                Assert.That(beyond.CurrentHp, Is.EqualTo(100), "A sample after the window cannot extend the sweep.");
                Assert.That(actions.IsCastingSkill, Is.False);
                if (skillIndex == 0)
                    RecordSkillQueryAllocations(player, actions, animator, state);
            }
            finally
            {
                animator.speed = 1f;
                DestroyTarget(crossed);
                DestroyTarget(hidden);
                DestroyTarget(beyond);
                Object.Destroy(wall);
            }
        }

        private static IEnumerator CheckObstructedLaunch(RiccaPlayer player, SkillInput input, Button slot, GameObject panel,
            PlayerActions actions, EnemyActor template, Animator animator)
        {
            yield return null;
            Vector3 origin = player.transform.position;
            Vector3 forward = player.transform.forward;
            var behind = SpawnTarget(template, origin + forward * 1.25f, false);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Thin wall at skill launch";
            wall.transform.SetPositionAndRotation(origin + forward * .46f + Vector3.up, player.transform.rotation);
            wall.transform.localScale = new Vector3(3f, 2f, .04f);
            yield return null;
            Physics.SyncTransforms();
            try
            {
                int before = actions.CurrentMp;
                input.Next = Toggle();
                yield return Until(() => panel.activeInHierarchy, "open skill next to a thin wall");
                Click(slot);
                SampleCast(player, animator, animator.GetCurrentAnimatorStateInfo(0).fullPathHash, 1f);
                Assert.That(behind.CurrentHp, Is.EqualTo(100), "The launch box does not hit through an adjacent wall.");
                Assert.That(actions.CurrentMp, Is.EqualTo(before - 10));
            }
            finally
            {
                DestroyTarget(behind);
                Object.Destroy(wall);
            }
            yield return null;
        }

        private static IEnumerator CheckSwordAimVariants(RiccaPlayer player, SkillInput input, Button slot, GameObject panel,
            PlayerActions actions, Animator animator)
        {
            var controller = player.GetComponent<CharacterController>();
            var camera = Camera.main.GetComponent<ThirdPersonCamera>();
            string[] states = { "rc_stand_magicUpperShot", "rc_air_magicUpperShot", "rc_air_magicShot", "rc_air_magicLowerShot" };
            float[] lookDeltas = { 20f, 0f, -11f, -15f };
            Vector3 ground = player.transform.position;
            for (int i = 0; i < states.Length; i++)
            {
                if (i > 0)
                {
                    controller.enabled = false;
                    player.transform.position = ground + Vector3.up * 2.5f;
                    controller.enabled = true;
                }
                input.Next = new RiccaInputFrame(0, 0, new Vector2(0f, lookDeltas[i]), false, false);
                yield return null;
                yield return null;
                Assert.That(controller.isGrounded, Is.EqualTo(i == 0), states[i]);

                input.Next = Toggle();
                yield return Until(() => panel.activeInHierarchy, "open Sword Throw aim variant " + states[i]);
                Click(slot);
                var state = animator.GetCurrentAnimatorStateInfo(0);
                Assert.That(state.IsName("Base Layer." + states[i]), Is.True, "Camera pitch " + camera.Pitch);
                SampleCast(player, animator, state.fullPathHash, 1f);
                Assert.That(actions.IsCastingSkill, Is.False);
                yield return null;
            }
        }

        private static void RecordSkillQueryAllocations(RiccaPlayer player, PlayerActions actions, Animator animator, int state)
        {
            string root = Environment.GetEnvironmentVariable("IDLERICCA_SOURCE_SKILL_LOGS");
            if (string.IsNullOrEmpty(root))
                return;

            var commands = actions.SkillCommands;
            Assert.That(commands.TryOpenSkillSelection(), Is.True);
            Assert.That(commands.TryCastSkill(actions.SkillStatus.SelectionId, 0, Time.timeAsDouble, out SkillCast cast), Is.True);
            var batch = commands.BeginSkillFrame(cast.Id, true, .7f, Time.timeAsDouble);
            Assert.That(batch.CanHit, Is.True);
            var hitbox = player.GetComponent<SkillHitbox>();
            var query = (Action<Vector3, Vector3, Vector3, Quaternion, AttackSampleBatch, IPlayerSkillCommands>)
                Delegate.CreateDelegate(typeof(Action<Vector3, Vector3, Vector3, Quaternion, AttackSampleBatch, IPlayerSkillCommands>),
                    hitbox, typeof(SkillHitbox).GetMethod("Resolve", InternalInstance));
            Vector3 start = player.transform.position + Vector3.up * 70f;
            Vector3 end = start + Vector3.forward;
            query(start, end, Vector3.one, Quaternion.identity, batch, commands);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 120; i++)
                query(start, end, Vector3.one, Quaternion.identity, batch, commands);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            commands.CancelSkillCast(cast.Id);
            animator.Play(state, 0, 1f);
            File.WriteAllText(Path.Combine(root, "SkillQuerySteadyAllocations.txt"),
                "120 warmed moving-box queries with no contacts allocated " + bytes + " B of managed memory.");
            Assert.That(bytes, Is.Zero);
        }

        private static void SampleCast(RiccaPlayer player, Animator animator, int state, float progress)
        {
            animator.Play(state, 0, progress);
            animator.Update(0f);
            typeof(RiccaPlayer).GetMethod("LateUpdate", InternalInstance).Invoke(player, null);
        }

        [UnityTest]
        public IEnumerator SourceVariantsAnimateAndEmptyManaDisablesTheRealSkillButton()
        {
            yield return SceneManager.LoadSceneAsync(0);
            yield return null;
            var player = Camera.main.GetComponent<ThirdPersonCamera>().Player;
            var input = new SkillInput();
            player.UseInputSource(input);
            var actions = (PlayerActions)Actions.GetValue(player);
            var spawner = Object.FindObjectOfType<EnemySpawner>();
            var run = (WaveRun)typeof(EnemySpawner).GetField("_waveRun", InternalInstance).GetValue(spawner);
            typeof(WaveRun).GetField("_rewards", InternalInstance).SetValue(run, Pool(FirstFourSkillSeed()));
            yield return ClearWave(run, 1, actions);
            int card = SkillCard(run.Status);
            var waveHud = Object.FindObjectOfType<WaveHud>();
            Click(waveHud.transform.Find("Reward Panel/Reward Card/Reward " + (card + 1)).GetComponent<Button>());
            spawner.enabled = false;
            yield return null;

            var animator = player.GetComponent<Animator>();
            string[] variants = { "rc_stand_magicUpperShot", "rc_sit_magicShot", "rc_air_magicShot",
                "rc_air_magicUpperShot", "rc_air_magicLowerShot" };
            foreach (string variant in variants)
            {
                animator.Play("Base Layer." + variant, 0, .45f);
                animator.Update(0f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + variant), Is.True);
            }
            animator.Play("Base Layer.Idle", 0, 0f);
            animator.Update(0f);
            typeof(PlayerActions).GetProperty("CurrentMp").SetValue(actions, 19);

            var skillHud = Object.FindObjectOfType<SkillSelectionHud>();
            var panel = skillHud.transform.Find("Skill choice overlay").gameObject;
            Button slot = panel.GetComponentsInChildren<Button>(true)[0];
            var hitbox = player.GetComponent<SkillHitbox>();
            input.Next = Toggle();
            yield return Until(() => panel.activeInHierarchy, "19 MP selection");
            hitbox.enabled = false;
            Click(slot);
            Assert.That(actions.IsCastingSkill, Is.False);
            Assert.That(actions.CurrentMp, Is.EqualTo(19));
            hitbox.enabled = true;
            Click(slot);
            Assert.That(actions.CurrentMp, Is.EqualTo(9));
            Assert.That(actions.IsCastingSkill, Is.True);
            int castAnimation = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            int healthBeforeHit = actions.CurrentHp;
            DamagePlayer(actions);
            Assert.That(actions.CurrentHp, Is.EqualTo(healthBeforeHit - 1));
            yield return null;
            Assert.That(actions.IsCastingSkill, Is.True);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(castAnimation));
            CapturePair("05-Protected-Cast-After-Hit");
            yield return Until(() => !actions.IsCastingSkill && !panel.activeInHierarchy, "protected cast completes");
            yield return null;
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Hit"), Is.False);
            DamagePlayer(actions);
            Assert.That(actions.CurrentHp, Is.EqualTo(healthBeforeHit - 6));
            yield return Until(() => animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Hit"),
                "a hit after the cast plays the normal damage reaction");
            CapturePair("06-Normal-Reaction-After-Cast");
            Assert.That(actions.CurrentMp, Is.EqualTo(9));
            float rate = Time.timeScale;
            float fixedStep = Time.fixedDeltaTime;
            input.Next = Toggle();
            yield return Until(() => panel.activeInHierarchy, "9 MP selection");
            Assert.That(slot.interactable, Is.False);
            Assert.That(panel.GetComponentsInChildren<TMP_Text>().Any(label => label.text.Contains("Not enough MP")), Is.True);
            AssertTime(rate, fixedStep);
            CapturePair("03-Insufficient-MP");
            input.Next = Toggle();
            yield return Until(() => !panel.activeInHierarchy, "close low-mana selection");
            typeof(PlayerActions).GetProperty("CurrentMp").SetValue(actions, 10);
            animator.enabled = false;
            input.Next = Toggle();
            yield return Until(() => panel.activeInHierarchy, "selection with an unavailable Animator");
            yield return null;
            yield return null;
            Assert.That(panel.activeInHierarchy, Is.True);
            Assert.That(slot.interactable, Is.False);
            AssertTime(rate, fixedStep);
            input.Next = Toggle();
            yield return Until(() => !panel.activeInHierarchy, "close unavailable Animator selection");
            animator.enabled = true;

            Object.Destroy(GetEffect(player, actions.SkillStatus.GetSkillId(0)));
            yield return null;
            input.Next = Toggle();
            yield return Until(() => panel.activeInHierarchy, "selection with a missing effect");
            AssertTime(rate, fixedStep);
            Assert.That(slot.interactable, Is.False);
            Assert.That(panel.GetComponentsInChildren<TMP_Text>().Any(label => label.text.Contains("resources unavailable")), Is.True);
            slot.onClick.Invoke();
            Assert.That(actions.CurrentMp, Is.EqualTo(10));
            CapturePair("04-Unavailable-Skill");
            Assert.That(actions.IsCastingSkill, Is.False);
            input.Next = Toggle();
            yield return Until(() => !panel.activeInHierarchy, "close missing-effect selection");
            AssertTime(rate, fixedStep);
        }

        [UnityTest]
        public IEnumerator ASourceSkillWithAMissingPrefabIsDiagnosedAndExcludedAtStartup()
        {
            yield return SceneManager.LoadSceneAsync(0);
            yield return null;
            var player = Camera.main.GetComponent<ThirdPersonCamera>().Player;
            var holder = new GameObject("Inactive Ricca startup verification");
            holder.SetActive(false);
            var clone = Object.Instantiate(player, holder.transform);
            var settingsField = typeof(RiccaPlayer).GetField("_skillSettings", InternalInstance);
            var config = Object.Instantiate((RiccaSkillSettings)settingsField.GetValue(clone));
            settingsField.SetValue(clone, config);
            var rows = (Array)typeof(RiccaSkillSettings).GetField("_skills", InternalInstance).GetValue(config);
            object firstRow = rows.GetValue(0);
            firstRow.GetType().GetField("_effectRoot", InternalInstance).SetValue(firstRow, null);

            try
            {
                LogAssert.Expect(LogType.Error, "Ricca skill SwordThrow is unavailable: required effect is missing.");
                holder.SetActive(true);
                yield return null;
                var actions = (PlayerActions)Actions.GetValue(clone);
                Assert.That(clone.isActiveAndEnabled, Is.True);
                Assert.That(actions.CurrentMp, Is.EqualTo(100));

                var waves = Enumerable.Repeat(new WaveDefinition(1, .25, 1), WaveStatus.TotalWaves).ToArray();
                int skillSeed = Enumerable.Range(0, 100).First(seed => new System.Random(seed).Next(5) == 0);
                var run = new WaveRun(actions, waves, Pool(FirstFourSkillSeed()), 7, 14, skillSeed);
                Assert.That(run.TryReserveSpawn(EnemyKind.Goblin, 64, true, true, true, 0d, out ulong enemySlot), Is.True);
                var enemy = new EnemyHealth(1);
                Assert.That(run.ConfirmSpawn(enemySlot, enemy, 0d), Is.True);
                var commands = actions.Commands;
                Assert.That(commands.TryStartAttack(), Is.True);
                var batch = commands.BeginAttackFrame(true, 1f, .1f);
                Assert.That(commands.TryHit(batch, enemy), Is.EqualTo(AttackHit.Defeated));
                commands.EndAttackFrame(batch);
                Assert.That(run.Release(enemySlot), Is.True);
                run.Advance();
                var offer = run.Status;
                Assert.That(offer.Phase, Is.EqualTo(Phase.Reward));
                Assert.That(run.TryGetSkillRewardPreview(offer.OfferId, SkillCard(offer), out SkillRewardPreview skill), Is.True);
                Assert.That(skill.Skill, Is.InRange(SkillId.LightningRush, SkillId.HolyGeyser));
            }
            finally
            {
                Object.Destroy(holder);
                Object.Destroy(config);
            }
            yield return null;
        }

        private static GameObject GetEffect(RiccaPlayer player, SkillId id) =>
            player.GetComponentsInChildren<Transform>(true)
                .Single(child => child.name == Effects[(int)id - (int)SkillId.SwordThrow] + "(Clone)").gameObject;

        private static void DestroyTarget(EnemyTarget target)
        {
            if (target)
                Object.Destroy(target.gameObject);
        }

        private static RiccaInputFrame Toggle() => new RiccaInputFrame(0, 0, default, false, false, true, false);

        private static WaveRewardPool Pool(int seed) =>
            new WaveRewardPool(45, 45, 45, new[] { 10, 20, 30 }, new[] { 4, 8, 12 }, seed);

        private static int FirstFourSkillSeed()
        {
            var rewards = new WaveReward[WaveStatus.OfferSize];
            for (int seed = 0; seed < 10000; seed++)
            {
                var rewardsForSeed = Pool(seed);
                int waves;
                for (waves = 0; waves < 4; waves++)
                {
                    rewardsForSeed.Draw(rewards);
                    if (!rewards.Any(reward => reward.Kind == WaveReward.RewardKind.Skill))
                        break;
                }
                if (waves == 4)
                    return seed;
            }
            Assert.Fail("No four-wave reward seed found.");
            return 0;
        }

        private static int SkillCard(WaveStatus offer)
        {
            for (int i = 0; i < WaveStatus.OfferSize; i++)
                if (offer.GetReward(i).Kind == WaveReward.RewardKind.Skill)
                    return i;
            Assert.Fail("Expected the seeded wave to offer a skill.");
            return -1;
        }

        private static IEnumerator ClearWave(WaveRun run, int wave, PlayerActions player)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (run.Status.Phase == Phase.Reward && run.Status.Number == wave)
                {
                    yield return null;
                    yield break;
                }

                foreach (var target in Object.FindObjectsOfType<EnemyTarget>())
                {
                    var enemy = (EnemyHealth)Health.GetValue(target);
                    while (enemy.IsAlive)
                    {
                        var commands = player.Commands;
                        Assert.That(commands.TryStartAttack(), Is.True);
                        var batch = commands.BeginAttackFrame(true, 1f, .1f);
                        var result = commands.TryHit(batch, enemy);
                        Assert.That(result, Is.Not.EqualTo(AttackHit.Ignored));
                        ShowHit.Invoke(target, new object[] { result, null });
                        commands.EndAttackFrame(batch);
                    }
                }
                yield return null;
            }
            Assert.Fail("Timed out before reward for wave " + wave);
        }

        private static EnemyTarget SpawnTarget(EnemyActor template, Vector3 position, bool doubleHurtbox)
        {
            var actor = Object.Instantiate(template, position, Quaternion.identity);
            var target = actor.GetComponent<EnemyTarget>();
            if (doubleHurtbox)
            {
                var hurtboxes = typeof(EnemyTarget).GetField("_hurtboxes", InternalInstance);
                var original = (Collider[])hurtboxes.GetValue(target);
                actor.gameObject.SetActive(false);
                var duplicate = new GameObject("Duplicate hurtbox for skill verification");
                duplicate.layer = original[0].gameObject.layer;
                duplicate.transform.SetParent(original[0].transform, false);
                var duplicateCollider = duplicate.AddComponent<BoxCollider>();
                duplicateCollider.center = new Vector3(0f, .9f, 0f);
                duplicateCollider.size = Vector3.one;
                duplicateCollider.isTrigger = true;
                hurtboxes.SetValue(target, original.Concat(new[] { duplicateCollider }).ToArray());
                actor.gameObject.SetActive(true);
            }
            return target;
        }

        private static void DamagePlayer(PlayerActions player)
        {
            var commands = new EnemyActions(new EnemyHealth(100), 1.25f, .75f, 5, .3f, .5f, 1f, 1f, 1f, 1f).Commands;
            commands.BindTarget(player);
            commands.Step(true, 1f, 0f, true, .1f);
            Assert.That(commands.TryHitRicca(commands.BeginAttackFrame(true, .4f, .1f), player), Is.EqualTo(AttackHit.Damaged));
        }

        private static void AssertTime(float rate, float step)
        {
            Assert.That(Time.timeScale, Is.EqualTo(rate).Within(.0001f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(step).Within(.0001f));
        }

        private static IEnumerator Until(Func<bool> ready, string description)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!ready() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(ready(), Is.True, description);
        }

        private static void Click(Button button)
        {
            Assert.That(button.isActiveAndEnabled && button.interactable, Is.True);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.Positive);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(button.gameObject));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void RecordSteadyUiAllocations(RiccaPlayer player, SkillSelectionHud hud)
        {
            string root = Environment.GetEnvironmentVariable("IDLERICCA_SOURCE_SKILL_LOGS");
            if (string.IsNullOrEmpty(root))
                return;

            var tick = (Action)Delegate.CreateDelegate(typeof(Action), hud,
                typeof(SkillSelectionHud).GetMethod("LateUpdate", InternalInstance));
            tick();
            player.TryGetSkillStatus(out _);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 120; i++)
            {
                player.TryGetSkillStatus(out _);
                tick();
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            File.WriteAllText(Path.Combine(root, "SkillUiSteadyAllocations.txt"),
                "120 warmed steady-state status/HUD ticks on the managed main thread: " + bytes +
                " B. This measurement excludes Editor, rendering, enemy spawning and the test harness.");
            Assert.That(bytes, Is.Zero);
        }

        private static void CapturePair(string capture)
        {
            string root = Environment.GetEnvironmentVariable("IDLERICCA_SOURCE_SKILL_LOGS");
            if (string.IsNullOrEmpty(root))
                return;
            var canvases = new[] { Object.FindObjectOfType<WaveHud>().GetComponent<Canvas>(),
                Object.FindObjectOfType<CombatHud>().GetComponent<Canvas>() };
            Capture(root, capture, 1280, 720, canvases);
            Capture(root, capture, 1920, 1080, canvases);
        }

        private static void Capture(string root, string capture, int width, int height, Canvas[] canvases)
        {
            var camera = Camera.main;
            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var oldScales = canvases.Select(canvas => canvas.scaleFactor).ToArray();
            var scalers = canvases.Select(canvas => canvas.GetComponent<CanvasScaler>()).ToArray();
            try
            {
                camera.targetTexture = target;
                for (int i = 0; i < canvases.Length; i++)
                {
                    scalers[i].enabled = false;
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = camera.nearClipPlane + .1f;
                    canvases[i].scaleFactor = height / (i == 0 ? 720f : 1080f);
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                string folder = Path.Combine(root, "Screens");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, capture + "-" + height + "p.png"), pixels.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                    canvases[i].worldCamera = null;
                    canvases[i].scaleFactor = oldScales[i];
                    scalers[i].enabled = true;
                }
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(pixels);
            }
        }

        private sealed class SkillInput : IRiccaInput
        {
            public RiccaInputFrame Next { get; set; }
            public RiccaInputFrame Read()
            {
                var result = Next;
                Next = default;
                return result;
            }
        }
    }
}
