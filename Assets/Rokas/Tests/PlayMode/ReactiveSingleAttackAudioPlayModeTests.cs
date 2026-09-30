using System.Collections;
using System.Linq;
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
        [Test]
        public void ImportedPolishIILayersContainReadableStereoPcmData()
        {
            foreach (string name in new[] { "NormalWhoosh", "HeavyWhoosh", "NormalFleshContact",
                "HeavyFleshContact", "MonsterFleshContact", "GuardMetalContact", "DodgeBackstep" })
            {
                var clip = Resources.Load<AudioClip>("Combat/ReactiveTurns/Audio/PolishII/" + name);
                Assert.That(clip, Is.Not.Null, name);
                Assert.That(clip.channels, Is.EqualTo(2), name);
                Assert.That(clip.frequency, Is.EqualTo(22050), name);
                var samples = new float[clip.samples * clip.channels];
                Assert.That(clip.GetData(samples, 0), Is.True, name);
                Assert.That(samples.Any(value => Mathf.Abs(value) > .001f), Is.True, name);
            }
        }
        [TestCase(false)]
        [TestCase(true)]
        public void VocalSwingAndContactStayDistinctAndPlayOnceAcrossCoreAliases(bool heavy)
        {
            var root = new GameObject("ReactiveSingleAttackAudioFixture");
            RokasAudio audio = null;
            try
            {
                RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
                Assert.That(assets, Is.Not.Null);
                var settings = new SettingsData { masterVolume = .8f, sfxVolume = .5f };
                audio = new RokasAudio(root, assets, settings);
                var cues = new ReactiveCombatAudio(audio);
                cues.PresentAttackStarted("animation-1", "P", heavy);
                cues.BindActionId("core-1", "animation-1");
                cues.Present(new CombatEvent(CombatEventKind.CommandCommitted,
                    actorId: "P", actionId: "core-1", detail: heavy ? "heavy" : "Basic"));
                Assert.That(cues.PlaybackCount, Is.EqualTo(1),
                    "The animation callback and Core commit refer to the same vocal event.");
                Assert.That(cues.Dispatches.Any(d => d.EventId == ReactiveCombatAudioEvent.NormalWhoosh ||
                    d.EventId == ReactiveCombatAudioEvent.HeavyWhoosh), Is.False,
                    "Selection and commitment cannot substitute for the actual acceleration window.");
                cues.PresentSwing("animation-1", "P", heavy);
                cues.PresentSwing("core-1", "P", heavy);
                Assert.That(cues.PlaybackCount, Is.EqualTo(2));
                settings.masterVolume = .3f;
                settings.sfxVolume = .2f;
                var contact = new CombatEvent(CombatEventKind.HitResolved,
                    actorId: "P", targetId: "E1", actionId: "core-1", amount: 20);
                Assert.That(cues.Present(contact), Is.True);
                Assert.That(cues.Present(contact), Is.True);
                Assert.That(cues.PlaybackCount, Is.EqualTo(3));
                Assert.That(cues.Dispatches.Select(d => d.EventId).Distinct().Count(), Is.EqualTo(3));
                Assert.That(cues.Dispatches.Last().EventId, Is.EqualTo(heavy
                    ? ReactiveCombatAudioEvent.HeavyFleshContact : ReactiveCombatAudioEvent.NormalFleshContact));
                Assert.That(cues.Dispatches.Last().ClipName, Is.EqualTo(heavy ? "HeavyFleshContact" : "NormalFleshContact"));
                Assert.That(cues.Dispatches[1].ClipName, Is.EqualTo(heavy ? "HeavyWhoosh" : "NormalWhoosh"));
                AudioSource contactSource = root.GetComponentsInChildren<AudioSource>(true)
                    .Single(s => s.clip && s.clip.name == cues.Dispatches.Last().ClipName);
                Assert.That(contactSource.volume, Is.EqualTo(.3f * .2f * (heavy ? .72f : .56f)).Within(.001f),
                    "Each new layer uses the current shared master and SFX controls.");
            }
            finally
            {
                audio?.Dispose();
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("Parry", ReactiveCombatAudioEvent.GuardMetalContact)]
        [TestCase("Perfect", ReactiveCombatAudioEvent.GuardMetalContact)]
        [TestCase("Dodge", ReactiveCombatAudioEvent.DodgeBackstep)]
        public void SuccessfulDefenseSuppressesFleshEvenWhenContractCarriesDamage(
            string outcome, ReactiveCombatAudioEvent expected)
        {
            var root = new GameObject("ReactiveDefenseAudioFixture");
            var audio = new RokasAudio(root, Resources.Load<RokasAssets>("RokasAssets"), new SettingsData());
            try
            {
                var cues = new ReactiveCombatAudio(audio);
                cues.Present(new CombatEvent(CombatEventKind.AttackStarted, "E1", "P", "enemy-1"));
                cues.PresentSwing("enemy-1", "E1", false, "hit-1");
                var contact = new CombatEvent(CombatEventKind.HitResolved, "E1", "P", "enemy-1",
                    "hit-1", amount: 7, detail: outcome);
                Assert.That(cues.Present(contact), Is.True);
                Assert.That(cues.Present(contact), Is.True);
                Assert.That(cues.PlaybackCount, Is.EqualTo(3));
                Assert.That(cues.Dispatches.Last().EventId, Is.EqualTo(expected));
                Assert.That(cues.Dispatches.Any(d => d.EventId == ReactiveCombatAudioEvent.MonsterFleshContact), Is.False);
                Assert.That(root.GetComponentsInChildren<AudioSource>(true)
                    .Any(s => s.clip && s.clip.name.Contains("FleshContact")), Is.False);
            }
            finally { audio.Dispose(); Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator EnemyTwoHitSequenceHasOneVocalAndSeparateOnceOnlyContacts()
        {
            var root = new GameObject("ReactiveEnemySequenceAudioFixture");
            var audio = new RokasAudio(root, Resources.Load<RokasAssets>("RokasAssets"), new SettingsData());
            try
            {
                var cues = new ReactiveCombatAudio(audio);
                cues.PresentAttackStarted("enemy-combo", "E1", false);
                cues.PresentAttackStarted("enemy-combo", "E1", false);
                Assert.That(cues.Dispatches.All(d => d.EventId != ReactiveCombatAudioEvent.MonsterFleshContact), Is.True);
                foreach (string hit in new[] { "first", "second" })
                {
                    cues.PresentSwing("enemy-combo", "E1", false, hit);
                    cues.PresentSwing("enemy-combo", "E1", false, hit);
                    cues.PresentContact("enemy-combo", "E1", false, "Miss", 12, hit);
                    cues.PresentContact("enemy-combo", "E1", false, "Miss", 12, hit);
                }
                Assert.That(cues.PlaybackCount, Is.EqualTo(5));
                Assert.That(cues.Dispatches.Count(d => d.EventId == ReactiveCombatAudioEvent.MonsterVocal), Is.EqualTo(1));
                Assert.That(cues.Dispatches.Count(d => d.EventId == ReactiveCombatAudioEvent.MonsterFleshContact), Is.EqualTo(2));
                yield return null;
            }
            finally { audio.Dispose(); Object.Destroy(root); }
        }

        [Test]
        public void PlayerCounterDoesNotInheritEnemyHeavyContactForSharedActionId()
        {
            var root = new GameObject("ReactiveCounterAudioFixture");
            var audio = new RokasAudio(root, Resources.Load<RokasAssets>("RokasAssets"), new SettingsData());
            try
            {
                var cues = new ReactiveCombatAudio(audio);
                cues.PresentAttackStarted("enemy-heavy-with-counter", "E1", true);
                cues.PresentSwing("enemy-heavy-with-counter", "E1", true, "heavy-hit");
                cues.PresentContact("enemy-heavy-with-counter", "E1", true, "Perfect", 0, "heavy-hit");
                // Core keeps the enemy action ID for the player's accepted counter.
                var counter = new CombatEvent(CombatEventKind.HitResolved, "P", "E1",
                    "enemy-heavy-with-counter", "counter", amount: 18, detail: "Counter");
                Assert.That(cues.Present(counter), Is.True);
                Assert.That(cues.Present(counter), Is.True);
                Assert.That(cues.PlaybackCount, Is.EqualTo(4));
                Assert.That(cues.Dispatches.Last().EventId, Is.EqualTo(ReactiveCombatAudioEvent.NormalFleshContact));
                Assert.That(cues.Dispatches.Last().ClipName, Is.EqualTo("NormalFleshContact"));
                Assert.That(cues.Dispatches.Count(d => d.EventId == ReactiveCombatAudioEvent.HeavyFleshContact), Is.Zero,
                    "The enemy's heavy swing must not select the player's Heavy contact layer.");
            }
            finally { audio.Dispose(); Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator SweepGroupsTargetsWhileDifferentActionsAndResetCanSoundAgain()
        {
            var root = new GameObject("ReactiveSweepAudioFixture");
            var audio = new RokasAudio(root, Resources.Load<RokasAssets>("RokasAssets"), new SettingsData());
            try
            {
                var cues = new ReactiveCombatAudio(audio);
                foreach (string target in new[] { "E1", "E2", "E3" })
                    Assert.That(cues.Present(new CombatEvent(CombatEventKind.HitResolved,
                        "P", target, "sweep-1", amount: 14)), Is.True);
                Assert.That(cues.PlaybackCount, Is.EqualTo(1));
                cues.PresentContact("sweep-2", "P", false, null, 14);
                Assert.That(cues.PlaybackCount, Is.EqualTo(2));
                cues.Reset();
                cues.PresentContact("sweep-1", "P", false, null, 14);
                Assert.That(cues.PlaybackCount, Is.EqualTo(1));
                yield return null;
            }
            finally { audio.Dispose(); Object.Destroy(root); }
        }
    }
}
