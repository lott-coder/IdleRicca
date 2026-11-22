using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    public sealed class WaveRunGameplayTests
    {
        private static readonly BindingFlags InternalInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly PropertyInfo ActionsGetter = typeof(RiccaPlayer).GetProperty("Actions", InternalInstance);
        private static readonly PropertyInfo HealthGetter = typeof(EnemyTarget).GetProperty("Health", InternalInstance);
        private static readonly FieldInfo NormalAttackField = typeof(RiccaPlayer).GetField("_attackSettings", InternalInstance);
        private static readonly MethodInfo ShowHit = typeof(EnemyTarget).GetMethod("ShowHitResult", InternalInstance);
        private static readonly MethodInfo OnFocus = typeof(RiccaPlayer).GetMethod("OnApplicationFocus", InternalInstance);
        private static readonly MethodInfo UpdateInput = typeof(RiccaPlayer).GetMethod("Update", InternalInstance);
        private static readonly int DeathAnimation = Animator.StringToHash("Base Layer.Death");

        [UnityTest]
        public IEnumerator WaveSelectionConsumesTheClickAndDeathRestartCreatesAFreshRun()
        {
            yield return SceneManager.LoadSceneAsync(0);
            yield return null;
            var spawner = Object.FindObjectOfType<EnemySpawner>();
            var player = Camera.main.GetComponent<ThirdPersonCamera>().Player;
            var hud = Object.FindObjectOfType<WaveHud>();
            var input = new ScriptedInput();
            player.UseInputSource(input);

            yield return Until(() => Status(spawner).Phase == Phase.Fighting, "Wave 1 initialized");
            Assert.That(Status(spawner).Number, Is.EqualTo(1));
            Assert.That(Counter(hud), Is.EqualTo("1/10"));
            CapturePair(hud, "Wave01");

            IEnumerator wave = DefeatWave(spawner, player, 1);
            while (wave.MoveNext())
                yield return wave.Current;

            Assert.That(Status(spawner).Phase, Is.EqualTo(Phase.Reward));
            Assert.That(Status(spawner).Number, Is.EqualTo(1));
            Assert.That(Counter(hud), Is.EqualTo("1/10"));
            CapturePair(hud, "WaveReward");

            player.UseInputSource(null);
            OnFocus.Invoke(player, new object[] { false });
            OnFocus.Invoke(player, new object[] { true });
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            player.UseInputSource(input);

            var camera = Camera.main.GetComponent<ThirdPersonCamera>();
            float yaw = camera.Yaw;
            int reads = input.Reads;
            input.AttackAndMove = true;
            yield return null;
            Assert.That(input.Reads, Is.EqualTo(reads));
            Assert.That(camera.Yaw, Is.EqualTo(yaw));
            Assert.That(player.IsAttacking, Is.False);

            Button choice = RewardButton(hud, 1);
            string text = choice.GetComponentInChildren<TMP_Text>().text;
            PlayerActions actions = (PlayerActions)ActionsGetter.GetValue(player);
            int baseHp = actions.MaxHp;
            int previousAttack = actions.AttackDamage;
            int previousHealth = actions.CurrentHp;
            Click(choice);
            Assert.That(Status(spawner).Number, Is.EqualTo(2));
            Assert.That(Status(spawner).Phase, Is.EqualTo(Phase.Fighting));
            AssertReward(text, actions, baseHp, previousHealth, previousAttack);
            UpdateInput.Invoke(player, null);
            Assert.That(input.Reads, Is.EqualTo(reads), "The selection frame cannot feed gameplay input.");
            Assert.That(player.IsAttacking, Is.False);
            Assert.That(camera.Yaw, Is.EqualTo(yaw));

            input.AttackAndMove = false;
            yield return null;
            Assert.That(input.Reads, Is.GreaterThan(reads));
            int sceneHandle = player.gameObject.scene.handle;
            Assert.That(AttackPlayer(actions), Is.EqualTo(AttackHit.Defeated));
            yield return Until(() => Status(spawner).Phase == Phase.Defeated, "death transition");
            Assert.That(Status(spawner).Number, Is.EqualTo(1));
            Assert.That(actions.CurrentHp, Is.Zero);
            Assert.That(actions.MaxHp, Is.EqualTo(100));
            Assert.That(actions.AttackDamage, Is.EqualTo(25));
            Button restart = RestartButton(hud);
            var animator = player.GetComponentInChildren<Animator>();
            yield return Until(() => animator.GetCurrentAnimatorStateInfo(0).fullPathHash == DeathAnimation,
                "Ricca entered her existing death animation");
            Assert.That(restart.gameObject.activeInHierarchy, Is.False);
            yield return Until(() => restart.isActiveAndEnabled && restart.interactable, "restart after death animation");
            Assert.That(Counter(hud), Is.EqualTo("1/10"));
            Assert.That(hud.transform.Find("Result Panel/Result Card/Result Title").GetComponent<TMP_Text>().text,
                Is.EqualTo("Run Over"));
            CapturePair(hud, "RunOver");

            Click(restart);
            yield return Until(() => SceneManager.GetActiveScene().handle != sceneHandle, "fresh Demo after death");
            yield return null;
            AssertFreshRun();
        }

        [UnityTest]
        public IEnumerator DemoReachesTenAndDisplaysFinalRewardAndCompletion()
        {
            yield return SceneManager.LoadSceneAsync(0);
            yield return null;
            var spawner = Object.FindObjectOfType<EnemySpawner>();
            var player = Camera.main.GetComponent<ThirdPersonCamera>().Player;
            var hud = Object.FindObjectOfType<WaveHud>();
            player.UseInputSource(new ScriptedInput());
            float originalScale = Time.timeScale;
            Time.timeScale = 8f;
            try
            {
                for (int waveNumber = 1; waveNumber <= 10; waveNumber++)
                {
                    IEnumerator wave = DefeatWave(spawner, player, waveNumber);
                    while (wave.MoveNext())
                        yield return wave.Current;

                    Assert.That(Status(spawner).Number, Is.EqualTo(waveNumber));
                    Assert.That(Counter(hud), Is.EqualTo(waveNumber + "/10"));
                    Button choice = RewardButton(hud, 1);
                    if (waveNumber == 10)
                    {
                        Assert.That(hud.transform.Find("Reward Panel/Reward Card/Reward Title").GetComponent<TMP_Text>().text,
                            Is.EqualTo("Final Reward"));
                        CapturePair(hud, "FinalReward");
                    }
                    Click(choice);
                    yield return null;
                }

                Assert.That(Status(spawner).Phase, Is.EqualTo(Phase.Won));
                Assert.That(Counter(hud), Is.EqualTo("10/10"));
                Assert.That(hud.transform.Find("Result Panel/Result Card/Result Title").GetComponent<TMP_Text>().text,
                    Is.EqualTo("Run Complete"));
                CapturePair(hud, "RunComplete");
                var waveRun = (WaveRun)typeof(EnemySpawner).GetField("_waveRun", InternalInstance).GetValue(spawner);
                Assert.That(waveRun.TryBeginRestart(), Is.True);
                typeof(EnemySpawner).GetField("_restartDeadline", InternalInstance)
                    .SetValue(spawner, Time.unscaledTimeAsDouble - 1);
                yield return null;
                Assert.That(spawner.IsWaveRestartDelayed, Is.True);
                Assert.That(RestartButton(hud).interactable, Is.False);
                Assert.That(spawner.TryRestartWaveRun(), Is.False);
                Assert.That(hud.transform.Find("Result Panel/Result Card/Result Title").GetComponent<TMP_Text>().text,
                    Is.EqualTo("Reload delayed"));
                CapturePair(hud, "RestartDelayed");
                waveRun.CancelRestart();
                yield return null;
                int sceneHandle = player.gameObject.scene.handle;
                Click(RestartButton(hud));
                yield return Until(() => SceneManager.GetActiveScene().handle != sceneHandle, "fresh Demo after victory");
                yield return null;
                AssertFreshRun();
            }
            finally
            {
                Time.timeScale = originalScale;
            }
        }

        private static IEnumerator DefeatWave(EnemySpawner spawner, RiccaPlayer player, int expectedWave)
        {
            var actions = (PlayerActions)ActionsGetter.GetValue(player);
            var attackSettings = (NormalAttackSettings)NormalAttackField.GetValue(player);
            float deadline = Time.realtimeSinceStartup + 18;
            while (Time.realtimeSinceStartup < deadline)
            {
                WaveStatus status = Status(spawner);
                if (status.Number == expectedWave && status.Phase == Phase.Reward)
                {
                    yield return null;
                    yield break;
                }
                Assert.That(player.IsAlive, Is.True);
                foreach (var enemy in Object.FindObjectsOfType<EnemyTarget>())
                {
                    var health = (EnemyHealth)HealthGetter.GetValue(enemy);
                    if (health == null || !health.IsAlive)
                        continue;

                    while (health.IsAlive)
                    {
                        var commands = actions.Commands;
                        Assert.That(commands.TryStartAttack(), Is.True);
                        var batch = commands.BeginAttackFrame(true, 1f, .1f);
                        AttackHit result = commands.TryHit(batch, health);
                        Assert.That(result, Is.Not.EqualTo(AttackHit.Ignored));
                        ShowHit.Invoke(enemy, new object[] { result, attackSettings });
                        commands.EndAttackFrame(batch);
                    }
                }

                yield return null;
            }
            Assert.Fail("Timed out clearing wave " + expectedWave);
        }

        private static void AssertReward(string label, PlayerActions player, int maxHp, int currentHp, int damage)
        {
            if (label.StartsWith("Health", StringComparison.Ordinal))
            {
                int bonus = ParseBonus(label);
                Assert.That(player.MaxHp, Is.EqualTo(maxHp + bonus));
                Assert.That(player.CurrentHp, Is.EqualTo(currentHp + bonus));
                Assert.That(player.AttackDamage, Is.EqualTo(damage));
            }
            else if (label.StartsWith("Attack", StringComparison.Ordinal))
            {
                Assert.That(player.AttackDamage, Is.EqualTo(damage + ParseBonus(label)));
                Assert.That(player.MaxHp, Is.EqualTo(maxHp));
            }
            else
            {
                Assert.That(label, Does.StartWith("Learn "));
                Assert.That(player.SkillStatus.GetSkillId(0), Is.InRange(SkillId.SwordThrow, SkillId.HolyGeyser));
                int level = int.Parse(label.Substring(label.LastIndexOf("Lv.", StringComparison.Ordinal) + 3),
                    CultureInfo.InvariantCulture);
                Assert.That(player.SkillStatus.GetLevel(0), Is.EqualTo(level));
                Assert.That(player.MaxHp, Is.EqualTo(maxHp));
                Assert.That(player.AttackDamage, Is.EqualTo(damage));
            }
        }

        private static int ParseBonus(string label) => int.Parse(
            label.Split('\n')[1].Split(' ')[0], CultureInfo.InvariantCulture);

        private static AttackHit AttackPlayer(PlayerActions actions)
        {
            var attacker = new EnemyActions(new EnemyHealth(100), 2f, 1f, 10000, .2f, .5f, 1f, 1f, 1f, 1f).Commands;
            attacker.BindTarget(actions);
            attacker.Step(true, 1f, 0f, true, .1f);
            return attacker.TryHitRicca(attacker.BeginAttackFrame(true, .4f, .1f), actions);
        }

        private static WaveStatus Status(EnemySpawner spawner)
        {
            Assert.That(spawner && spawner.TryGetWaveStatus(out _), Is.True);
            spawner.TryGetWaveStatus(out WaveStatus state);
            return state;
        }

        private static void AssertFreshRun()
        {
            var player = Camera.main.GetComponent<ThirdPersonCamera>().Player;
            var spawner = Object.FindObjectOfType<EnemySpawner>();
            Assert.That(Status(spawner).Number, Is.EqualTo(1));
            Assert.That(Status(spawner).Phase, Is.EqualTo(Phase.Fighting));
            Assert.That(player.TryGetCombatStatus(out PlayerCombatStatus health), Is.True);
            Assert.That(health.CurrentHp, Is.EqualTo(100));
            Assert.That(health.MaxHp, Is.EqualTo(100));
            Assert.That(health.ComboCount, Is.Zero);
            Assert.That(((PlayerActions)ActionsGetter.GetValue(player)).AttackDamage, Is.EqualTo(25));
        }

        private static Button RewardButton(WaveHud hud, int number) =>
            hud.transform.Find("Reward Panel/Reward Card/Reward " + number).GetComponent<Button>();

        private static Button RestartButton(WaveHud hud) =>
            hud.transform.Find("Result Panel/Result Card/Restart").GetComponent<Button>();

        private static string Counter(WaveHud hud) =>
            hud.transform.Find("Wave Badge/Wave Counter").GetComponent<TMP_Text>().text;

        private static void Click(Button button)
        {
            Assert.That(button.isActiveAndEnabled && button.interactable, Is.True);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(button.gameObject));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static IEnumerator Until(Func<bool> ready, string reason)
        {
            float deadline = Time.realtimeSinceStartup + 14f;
            while (!ready() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(ready(), Is.True, reason);
        }

        private static void CapturePair(WaveHud hud, string name)
        {
            string evidence = Environment.GetEnvironmentVariable("IDLERICCA_WAVE_LOGS");
            if (string.IsNullOrEmpty(evidence))
                return;
            Canvas waveCanvas = hud.GetComponent<Canvas>();
            Canvas combatCanvas = Object.FindObjectOfType<CombatHud>().GetComponent<Canvas>();
            Capture(evidence, name + "-720p", 1280, 720, waveCanvas, combatCanvas);
            Capture(evidence, name + "-1080p", 1920, 1080, waveCanvas, combatCanvas);
        }

        private static void Capture(string evidence, string name, int width, int height, params Canvas[] canvases)
        {
            var camera = Camera.main;
            var beforeActive = RenderTexture.active;
            var beforeTarget = camera.targetTexture;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var savedScales = canvases.Select(canvas => canvas.scaleFactor).ToArray();
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
                string directory = Path.Combine(evidence, "Screens");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                    canvases[i].worldCamera = null;
                    canvases[i].scaleFactor = savedScales[i];
                    scalers[i].enabled = true;
                }
                camera.targetTexture = beforeTarget;
                RenderTexture.active = beforeActive;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(pixels);
            }
        }

        private sealed class ScriptedInput : IRiccaInput
        {
            public bool AttackAndMove { get; set; }
            public int Reads { get; private set; }

            public RiccaInputFrame Read()
            {
                Reads++;
                return AttackAndMove ? new RiccaInputFrame(1, 1, Vector2.one, true, true) : default;
            }
        }
    }
}
