using System.Collections;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rokas.Tests
{
    public sealed class ReactiveSingleAttackAudioPlayModeTests
    {
        [UnityTest]
        public IEnumerator NormalHeavyAndSweepEmitOneContactCueEachThroughExistingVolumeControls()
        {
            var root = new GameObject("ReactiveSingleAttackAudioFixture");
            RokasAudio audio = null;
            try
            {
                RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
                AudioClip impact = Resources.Load<AudioClip>(
                    "Combat/ReactiveTurns/Audio/Keiko/Keiko hit attack");
                Assert.That(assets, Is.Not.Null);
                Assert.That(impact, Is.Not.Null);
                var settings = new SettingsData { masterVolume = .8f, sfxVolume = .5f };
                audio = new RokasAudio(root, assets, settings);
                var cues = new ReactiveCombatAudio(audio);

                foreach (string detail in new[] { "Basic", "heavy", "sweep" })
                    cues.Present(new CombatEvent(CombatEventKind.CommandCommitted,
                        actorId: ReactiveDuelDefinitions.HunterId, actionId: detail, detail: detail));
                Assert.That(AssignedSources(root), Is.Zero,
                    "Command commitment and anticipation must not emit a swing cue.");

                var normal = new CombatEvent(CombatEventKind.HitResolved,
                    actorId: ReactiveDuelDefinitions.HunterId, targetId: "E1", actionId: "normal", amount: 20);
                Assert.That(cues.Present(normal), Is.True);
                Assert.That(cues.Present(normal), Is.True,
                    "A repeated resolved contact stays handled without another sound.");
                Assert.That(AssignedSources(root), Is.EqualTo(1));
                AssertImpactVolumes(root, impact, .8f * .5f * .56f);

                settings.masterVolume = .3f;
                settings.sfxVolume = .2f;
                Assert.That(cues.Present(new CombatEvent(CombatEventKind.HitResolved,
                    actorId: ReactiveDuelDefinitions.HunterId, targetId: "E2", actionId: "heavy", amount: 78)), Is.True);
                Assert.That(AssignedSources(root), Is.EqualTo(2));
                AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
                Assert.That(System.Array.Exists(sources, source => source.clip == impact &&
                    Mathf.Abs(source.volume - .3f * .2f * .56f) < .001f), Is.True,
                    "The contact cue follows changed master and SFX settings.");

                foreach (string target in new[] { "E1", "E2", "E3" })
                    Assert.That(cues.Present(new CombatEvent(CombatEventKind.HitResolved,
                        actorId: ReactiveDuelDefinitions.HunterId, targetId: target,
                        actionId: "sweep", amount: 14)), Is.True);
                Assert.That(AssignedSources(root), Is.EqualTo(3),
                    "One multi-target action owns one contact cue.");
                Assert.That(cues.Present(new CombatEvent(CombatEventKind.HitResolved,
                    actorId: ReactiveDuelDefinitions.HunterId, targetId: "E1", actionId: "miss", amount: 0)), Is.False);
                Assert.That(cues.Present(new CombatEvent(CombatEventKind.HitResolved,
                    actorId: "E1", targetId: ReactiveDuelDefinitions.HunterId,
                    actionId: "incoming", amount: 8)), Is.False);
                Assert.That(AssignedSources(root), Is.EqualTo(3));
                foreach (AudioSource source in sources)
                    if (source.clip) Assert.That(source.clip, Is.EqualTo(impact));
                yield return null;
            }
            finally
            {
                audio?.Dispose();
                Object.Destroy(root);
            }
        }

        private static int AssignedSources(GameObject root)
        {
            int count = 0;
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
                if (source.clip) count++;
            return count;
        }

        private static void AssertImpactVolumes(GameObject root, AudioClip clip, float expected)
        {
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
                if (source.clip)
                {
                    Assert.That(source.clip, Is.EqualTo(clip));
                    Assert.That(source.volume, Is.EqualTo(expected).Within(.001f));
                }
        }
    }
}
