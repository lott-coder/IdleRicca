using System.Collections;
using System.IO;
using System.Linq;
using IdleRicca.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IdleRicca.Tests
{
    public sealed class DemoRenderingTests
    {
        [UnityTest]
        public IEnumerator FixedCityLightingIsStableOver120Frames()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("This test requires the graphics device.");

            yield return SceneManager.LoadSceneAsync(0);
            yield return EnemyGameplayTests.WaitForRoster();
            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            var orbit = camera.GetComponent<ThirdPersonCamera>();
            Assert.That(orbit, Is.Not.Null);
            orbit.enabled = false;
            var originalPosition = camera.transform.position;
            var originalRotation = camera.transform.rotation;
            var originalTarget = camera.targetTexture;
            var originalActive = RenderTexture.active;
            var renderTarget = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(renderTarget.width, renderTarget.height, TextureFormat.RGB24, false);
            var actors = Object.FindObjectsOfType<Animator>();
            Assert.That(actors.Select(actor => actor.name),
                Is.EquivalentTo(new[] { "Ricca", "Goblin", "Skeleton", "Hellhound" }));
            Object.FindObjectOfType<EnemySpawner>().enabled = false;
            Directory.CreateDirectory("Logs/CityDemo");

            try
            {
                camera.targetTexture = renderTarget;
                foreach (var actor in actors)
                    actor.Update(0.35f);

                ReadFrame(camera, renderTarget, pixels);
                SaveImage(pixels, "Main");
                foreach (var actor in actors)
                {
                    var target = actor.transform.position + Vector3.up * (actor.name == "Hellhound" ? 0.45f : 0.90f);
                    var position = Vector3.MoveTowards(target, originalPosition, 1.8f);
                    camera.transform.position = position;
                    camera.transform.LookAt(target);
                    ReadFrame(camera, renderTarget, pixels);
                    SaveImage(pixels, actor.name);
                }

                camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
                int attack = Animator.StringToHash("Attack");
                foreach (var actor in actors)
                    actor.SetTrigger(attack);

                for (int step = 1; step <= 48; step++)
                {
                    foreach (var actor in actors)
                        actor.Update(1f / 60f);

                    if (step % 12 != 0)
                        continue;

                    ReadFrame(camera, renderTarget, pixels);
                    SaveImage(pixels, "Attack-" + step);
                }

                foreach (var actor in actors)
                    actor.gameObject.SetActive(false);

                // Let Unity process the renderer removals before recording the static lighting baseline.
                yield return null;
                yield return null;
                ReadFrame(camera, renderTarget, pixels);
                var reference = pixels.GetRawTextureData<byte>().ToArray();
                Assert.That(reference.Distinct().Count(), Is.GreaterThan(32), "City must render with tonal variation.");
                SaveImage(pixels, "City-Frame000");

                for (int frame = 1; frame <= 120; frame++)
                {
                    yield return null;
                    if (frame % 30 != 0)
                        continue;

                    ReadFrame(camera, renderTarget, pixels);
                    var sample = pixels.GetRawTextureData<byte>();
                    SaveImage(pixels, "City-Frame" + frame.ToString("D3"));
                    int changedPixels = 0;
                    int maxChannelDelta = 0;
                    for (int i = 0; i < sample.Length; i += 3)
                    {
                        int delta = Mathf.Max(Mathf.Abs(sample[i] - reference[i]),
                            Mathf.Max(Mathf.Abs(sample[i + 1] - reference[i + 1]),
                                Mathf.Abs(sample[i + 2] - reference[i + 2])));
                        if (delta > 0)
                            changedPixels++;
                        maxChannelDelta = Mathf.Max(maxChannelDelta, delta);
                    }

                    // D3D11 readback can vary by one 8-bit step at a few pixels.
                    int pixelCount = pixels.width * pixels.height;
                    Debug.Log($"Demo City frame {frame}: {changedPixels}/{pixelCount} varied; max RGB delta {maxChannelDelta}.");
                    Assert.That(maxChannelDelta, Is.LessThanOrEqualTo(1), $"Static City frame {frame}.");
                    Assert.That(changedPixels, Is.LessThanOrEqualTo(pixelCount / 1000),
                        $"Static City frame {frame} varied at more than 0.1% of pixels.");
                }
            }
            finally
            {
                orbit.enabled = true;
                foreach (var actor in actors)
                {
                    if (actor)
                        actor.gameObject.SetActive(true);
                }
                camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
                camera.targetTexture = originalTarget;
                RenderTexture.active = originalActive;
                RenderTexture.ReleaseTemporary(renderTarget);
                Object.Destroy(pixels);
            }
        }

        private static void ReadFrame(Camera camera, RenderTexture target, Texture2D pixels)
        {
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
        }

        private static void SaveImage(Texture2D pixels, string fileName)
        {
            File.WriteAllBytes(Path.Combine("Logs", "CityDemo", fileName + ".png"), pixels.EncodeToPNG());
        }
    }
}
