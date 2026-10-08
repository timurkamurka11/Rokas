using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rokas.Tests
{
    // Validates the actual formation with spawned imported rigs, not UI mockups.
    // DD2 source video timing/ratios must be verified separately.
    public sealed class ReactiveCombatFormationFidelityPlayModeTests
    {
        [UnityTest]
        public IEnumerator ThreeYokaiAreCompactInCameraAndKeepDistinctFormationSlots()
        {
            var root = new GameObject("CompactCombatFormationFixture", typeof(RectTransform));
            var arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            try
            {
                arena.SetEnemies(new[] { "E1", "E2", "E3" }, null,
                    new[] { "E1", "E2", "E3" });
                for (int tick = 0; !arena.PresentationReady && tick < 140; tick++)
                    arena.Tick(.025f);
                Assert.That(arena.PresentationReady, Is.True);

                Vector3 hero = arena.HunterHome;
                Vector3 first = arena.EnemyHome("E1");
                Vector3 middle = arena.EnemyHome("E2");
                Vector3 back = arena.EnemyHome("E3");
                Assert.That(hero.x, Is.EqualTo(ReactiveCombatArena.KeikoTacticalX));
                Assert.That(first, Is.EqualTo(ReactiveCombatArena.TacticalEnemySlot(3, 0)));
                Assert.That(middle, Is.EqualTo(ReactiveCombatArena.TacticalEnemySlot(3, 1)));
                Assert.That(back, Is.EqualTo(ReactiveCombatArena.TacticalEnemySlot(3, 2)));
                Assert.That(first.x - hero.x, Is.InRange(5f, 5.7f),
                    "Frontline gap must be materially tighter than the 7.55m legacy layout.");
                Assert.That(middle.x-first.x, Is.GreaterThan(1.7f));
                Assert.That(back.x-middle.x, Is.GreaterThan(1.7f));
                Assert.That(middle.z, Is.GreaterThan(first.z));
                Assert.That(arena.EnemyAtHome("E1") && arena.EnemyAtHome("E2") &&
                    arena.EnemyAtHome("E3"), Is.True);

                Camera cam = GameObject.Find("ReactiveActorCamera").GetComponent<Camera>();
                Assert.That(cam.orthographicSize, Is.EqualTo(4.6f).Within(.001f),
                    "Existing verified cinematic camera baseline must remain unchanged.");
                float halfWidth = cam.orthographicSize * cam.aspect;
                Assert.That(hero.x, Is.GreaterThan(-halfWidth + .5f));
                Assert.That(back.x, Is.LessThan(halfWidth - .5f));
                TestContext.WriteLine("ROKAS_FORMATION_METRICS frontline_gap=" +
                    (first.x - hero.x).ToString("F3") +
                    " legacy_frontline_gap=7.550 front_x=" + first.x.ToString("F3") +
                    " rear_x=" + back.x.ToString("F3") +
                    " camera_ortho=" + cam.orthographicSize.ToString("F3"));

                CaptureActorCamera(cam, "formation-keiko-vs-three-yokai.png");
                yield return null;
            }
            finally
            {
                arena.Dispose();
                UnityEngine.Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator NormalHeavyAndEnemyApproachUseUpdatedSlotsAndReturnExactly()
        {
            var root = new GameObject("CompactFormationTravelFixture", typeof(RectTransform));
            var arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            try
            {
                arena.SetEnemies(new[] { "E1", "E2", "E3" }, null,
                    new[] { "E1", "E2", "E3" });
                for (int i = 0; !arena.PresentationReady && i < 140; i++)
                    arena.Tick(.025f);
                Assert.That(arena.PresentationReady, Is.True);

                foreach (bool heavy in new[] { false, true })
                {
                    string target = heavy ? "E2" : "E1";
                    Vector3 stableHome = arena.HunterHome;
                    Vector3 targetSlot = arena.EnemyHome(target);
                    Assert.That(arena.StartHunterApproach(target, heavy), Is.True);
                    int n = 0;
                    while (!arena.HunterApproachComplete && n++ < 180) arena.Tick(.025f);
                    Assert.That(arena.HunterApproachComplete, Is.True);
                    Assert.That(arena.HunterPosition.x,
                        Is.EqualTo(targetSlot.x-arena.HunterAttackDistance).Within(.002f),
                        "Melee end point must use the new actual target slot.");

                    arena.CancelHunterMotion();
                    for (n = 0; !arena.PresentationReady && n < 180; n++) arena.Tick(.025f);
                    Assert.That(arena.PresentationReady, Is.True);
                    Assert.That(arena.HunterPosition, Is.EqualTo(stableHome));
                    foreach (string id in new[] { "E1", "E2", "E3" })
                        Assert.That(arena.EnemyAtHome(id), Is.True);
                }
                yield return null;
            }
            finally
            {
                arena.Dispose();
                UnityEngine.Object.Destroy(root);
            }
        }

        private static void CaptureActorCamera(Camera cam, string name)
        {
            Assert.That(cam, Is.Not.Null);
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = cam.targetTexture;
            Assert.That(target, Is.Not.Null);
            try
            {
                cam.Render();
                RenderTexture.active = target;
                Texture2D pixels = new Texture2D(target.width, target.height,
                    TextureFormat.RGBA32, false);
                try
                {
                    pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    pixels.Apply();
                    string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                        "artifacts", "combat-formation");
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, name);
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                    Assert.That(new FileInfo(path).Length, Is.GreaterThan(1000));
                    TestContext.WriteLine("ROKAS_FORMATION_CAPTURE " + path);
                }
                finally { UnityEngine.Object.DestroyImmediate(pixels); }
            }
            finally { RenderTexture.active = previous; }
        }
    }
}
