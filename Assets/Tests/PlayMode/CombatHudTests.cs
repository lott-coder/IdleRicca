using System;
using System.Collections;
using System.IO;
using System.Reflection;
using IdleRicca.Application.Gameplay;
using IdleRicca.Presentation.Gameplay;
using IdleRicca.Presentation.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IdleRicca.Tests
{
    public sealed class CombatHudTests
    {
        private static readonly int Attack = Animator.StringToHash("Base Layer.Attack");
        private static readonly PropertyInfo PlayerActionsProperty =
            typeof(RiccaPlayer).GetProperty("Actions", BindingFlags.Instance | BindingFlags.NonPublic);

        [UnityTest]
        public IEnumerator DemoHudTracksRealSwordHitsAndTheSharedEnemyDamageCommand()
        {
            yield return SceneManager.LoadSceneAsync(0);
            var hud = Object.FindObjectOfType<CombatHud>();
            var camera = Camera.main;
            var player = camera.GetComponent<ThirdPersonCamera>().Player;
            var animator = player.GetComponent<Animator>();
            var spawner = Object.FindObjectOfType<EnemySpawner>();
            var template = spawner.GetTemplate(EnemyKind.Goblin);
            spawner.enabled = false;
            var input = new ScriptedInput();
            player.UseInputSource(input);
            yield return null;

            Assert.That(hud, Is.Not.Null);
            var canvas = hud.GetComponent<Canvas>();
            var status = hud.transform.Find("Status");
            var combo = hud.transform.Find("Combo");
            var hp = status.Find("Health value").GetComponent<TextMeshProUGUI>();
            var count = combo.Find("Combo number scale/Combo value").GetComponent<TextMeshProUGUI>();
            var timer = combo.Find("Timer fill");
            var comboNumber = combo.Find("Combo number scale");
            var comboFlash = combo.Find("Combo gold glint").GetComponent<CanvasGroup>();
            var mana = status.Find("Mana fill");
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(hud.GetComponent<GraphicRaycaster>(), Is.Null);
            foreach (Graphic graphic in hud.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, graphic.name);
            Assert.That(hp.text, Is.EqualTo("100 / 100"));
            Assert.That(mana.gameObject.activeInHierarchy, Is.True);
            Assert.That(status.Find("Mana value").GetComponent<TextMeshProUGUI>().text, Is.EqualTo("100 / 100"));
            Assert.That(combo.gameObject.activeSelf, Is.False);
            Capture(camera, canvas, "1080p-Start", 1920, 1080);
            Capture(camera, canvas, "720p-Start", 1280, 720);

            var target = Object.Instantiate(template, player.transform.position + Vector3.up * 5f, Quaternion.identity);
            PositionTargetOnBlade(player, target);
            var enemy = target.GetComponent<EnemyTarget>();
            var originalMode = animator.cullingMode;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = 0f;
            try
            {
                yield return Swing(animator, input);
                Assert.That(enemy.CurrentHp, Is.EqualTo(75));
                Assert.That(combo.gameObject.activeSelf, Is.False);
                yield return Swing(animator, input);
                Assert.That(enemy.CurrentHp, Is.EqualTo(50));
                Assert.That(count.text, Is.EqualTo("2"));
                Assert.That(combo.gameObject.activeSelf, Is.True);
                Assert.That(timer.localScale.x, Is.GreaterThan(0.8f));
                Capture(camera, canvas, "1080p-Combo", 1920, 1080);
                Capture(camera, canvas, "720p-Combo", 1280, 720);

                bool recording = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IDLERICCA_HUD_LOGS"));
                if (recording)
                {
                    for (int captureIndex = 0; captureIndex < 5; captureIndex++)
                    {
                        Capture(camera, canvas, $"Clip-Renewal-{captureIndex:D2}", 720, 405);
                        yield return new WaitForSeconds(.16f);
                    }
                }
                else
                    yield return new WaitForSeconds(.8f);

                Assert.That(comboFlash.alpha, Is.Zero);
                float beforeRenewal = timer.localScale.x;
                Assert.That(beforeRenewal, Is.LessThan(.9f));
                Capture(camera, canvas, "Combo-01-CountingDown", 960, 540);
                yield return Swing(animator, input, finish: false);
                Assert.That(enemy.CurrentHp, Is.EqualTo(25));
                Assert.That(count.text, Is.EqualTo("3"));
                Assert.That(comboNumber.localScale.x, Is.GreaterThan(1.01f));
                Assert.That(comboFlash.alpha, Is.GreaterThan(0f));
                Assert.That(timer.localScale.x, Is.GreaterThan(beforeRenewal));
                Capture(camera, canvas, "Clip-Renewal-05", 720, 405);
                Capture(camera, canvas, "Combo-02-Renewed", 960, 540);
                if (recording)
                {
                    yield return new WaitForSeconds(.16f);
                    Capture(camera, canvas, "Clip-Renewal-06", 720, 405);
                }

                var commands = (PlayerActions)PlayerActionsProperty.GetValue(player);
                Assert.That(ApplyEnemyAttack(commands, 25), Is.EqualTo(AttackHit.Damaged));
                yield return null;
                yield return null;
                Assert.That(hp.text, Is.EqualTo("75 / 100"));
                Assert.That(status.Find("Health fill").localScale.x, Is.EqualTo(.75f).Within(.001f));
                Assert.That(combo.gameObject.activeSelf, Is.False);
                Assert.That(mana.localScale, Is.EqualTo(Vector3.one));
                Capture(camera, canvas, "720p-ReceivedHit", 1280, 720);
                Capture(camera, canvas, "Clip-Renewal-07", 720, 405);

                player.enabled = false;
                yield return null;
                yield return null;
                Assert.That(status.gameObject.activeSelf, Is.False);
                player.enabled = true;
                yield return null;
                yield return null;
                Assert.That(status.gameObject.activeSelf, Is.True);
                Assert.That(hp.text, Is.EqualTo("75 / 100"));

                Assert.That(ApplyEnemyAttack(commands, 125), Is.EqualTo(AttackHit.Defeated));
                yield return null;
                yield return null;
                Assert.That(hp.text, Is.EqualTo("0 / 100"));
                Assert.That(status.Find("Health fill").localScale.x, Is.Zero);
                Assert.That(combo.gameObject.activeSelf, Is.False);
                Capture(camera, canvas, "720p-EmptyHealth", 1280, 720);
            }
            finally
            {
                animator.speed = 1f;
                animator.cullingMode = originalMode;
                player.UseInputSource(null);
                Object.Destroy(target.gameObject);
            }

            yield return SceneManager.LoadSceneAsync(0);
            yield return null;
            var restored = Object.FindObjectOfType<CombatHud>();
            Assert.That(restored.transform.Find("Status/Health value").GetComponent<TextMeshProUGUI>().text,
                Is.EqualTo("100 / 100"));
            Assert.That(restored.transform.Find("Combo").gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator DemoHudHidesTheComboWhenGameTimeReachesTheDeadline()
        {
            yield return SceneManager.LoadSceneAsync(0);
            var camera = Camera.main;
            var player = camera.GetComponent<ThirdPersonCamera>().Player;
            Object.FindObjectOfType<EnemySpawner>().enabled = false;
            var hud = Object.FindObjectOfType<CombatHud>();
            var canvas = hud.GetComponent<Canvas>();
            var combo = hud.transform.Find("Combo");
            var timer = combo.Find("Timer fill");
            // Let the regular player Update sample Unity's scene clock before issuing hits.
            yield return null;
            var actions = (PlayerActions)PlayerActionsProperty.GetValue(player);
            var enemy = new EnemyHealth(100);
            Assert.That(actions.Commands.TryStartAttack(), Is.True);
            var frame = actions.Commands.BeginAttackFrame(true, 1f, .1f);
            Assert.That(actions.Commands.TryHit(frame, enemy), Is.EqualTo(AttackHit.Damaged));
            Assert.That(actions.Commands.TryHit(frame, new EnemyHealth(100)), Is.EqualTo(AttackHit.Damaged));
            actions.Commands.EndAttackFrame(frame);
            yield return null;
            yield return null;
            Assert.That(combo.gameObject.activeSelf, Is.True);

            float originalScale = Time.timeScale;
            try
            {
                bool recording = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IDLERICCA_HUD_LOGS"));
                Time.timeScale = recording ? 1f : 4f;
                double startedAt = Time.timeAsDouble;
                double nextCaptureAt = 0;
                int captureIndex = 0;
                bool checkedMidWindow = false;
                while (Time.timeAsDouble - startedAt < 5.15)
                {
                    double elapsed = Time.timeAsDouble - startedAt;
                    if (recording && elapsed >= nextCaptureAt)
                    {
                        Capture(camera, canvas, $"Clip-Timeout-{captureIndex++:D2}", 720, 405);
                        nextCaptureAt += .2;
                    }

                    if (!checkedMidWindow && elapsed >= 2.0)
                    {
                        Assert.That(combo.gameObject.activeSelf, Is.True);
                        Assert.That(timer.localScale.x, Is.LessThan(.7f));
                        Capture(camera, canvas, "Combo-03-MidWindow", 960, 540);
                        checkedMidWindow = true;
                    }

                    yield return null;
                }

                Assert.That(checkedMidWindow, Is.True);
                Assert.That(combo.gameObject.activeSelf, Is.False);
                Capture(camera, canvas, $"Clip-Timeout-{captureIndex:D2}", 720, 405);
                Capture(camera, canvas, "Combo-04-Expired", 960, 540);
            }
            finally
            {
                Time.timeScale = originalScale;
            }
        }

        private static AttackHit ApplyEnemyAttack(PlayerActions player, int amount)
        {
            var attacker = new EnemyActions(new EnemyHealth(100), 1.25f, .75f, amount, .3f, .5f, 1f, 1f, 1f, 1f);
            attacker.Commands.BindTarget(player);
            attacker.Commands.Step(true, 1f, 0f, true, .1f);
            var impact = attacker.Commands.BeginAttackFrame(true, .4f, .1f);
            return attacker.Commands.TryHitRicca(impact, player);
        }

        private static IEnumerator Swing(Animator animator, ScriptedInput input, bool finish = true)
        {
            input.NextAttack = true;
            yield return null;
            yield return null;
            animator.Play(Attack, 0, .45f);
            animator.Update(0f);
            yield return null;
            yield return null;
            if (!finish)
                yield break;

            animator.Play(Attack, 0, 1.05f);
            animator.Update(0f);
            yield return null;
            yield return null;
        }

        private static void PositionTargetOnBlade(RiccaPlayer player, EnemyActor enemy)
        {
            var sample = Object.Instantiate(player.gameObject,
                player.transform.position + Vector3.up * 80f, player.transform.rotation);
            var animator = sample.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Play(Attack, 0, 18f / 70f);
            animator.Update(0f);
            BoxCollider blade = sample.GetComponent<SwordHitbox>().Geometry;
            Vector3 arc = blade.transform.TransformPoint(blade.center) + player.transform.position - sample.transform.position;
            Object.Destroy(sample);

            BoxCollider body = enemy.Body;
            body.size = Vector3.one * .035f;
            enemy.transform.position += arc - body.transform.TransformPoint(body.center);
            Physics.SyncTransforms();
        }

        private static void Capture(Camera camera, Canvas canvas, string name, int width, int height)
        {
            string directory = Environment.GetEnvironmentVariable("IDLERICCA_HUD_LOGS");
            if (string.IsNullOrEmpty(directory))
                return;

            var scaler = canvas.GetComponent<CanvasScaler>();
            float previousScale = canvas.scaleFactor;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                scaler.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + .1f;
                canvas.scaleFactor = height / 1080f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                canvas.scaleFactor = previousScale;
                scaler.enabled = true;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(pixels);
            }
        }

        private sealed class ScriptedInput : IRiccaInput
        {
            public bool NextAttack { get; set; }

            public RiccaInputFrame Read()
            {
                var frame = new RiccaInputFrame(0, 0, Vector2.zero, false, NextAttack);
                NextAttack = false;
                return frame;
            }
        }
    }
}
