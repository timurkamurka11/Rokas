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
    public sealed class CombatFidelity3AudioPlayModeTests
    {
        private GameObject root;
        private RokasAudio audio;
        private ReactiveCombatAudio cues;
        private SettingsData settings;

        [SetUp]
        public void CreateOwnAudioFixture()
        {
            root = new GameObject("CombatFidelity3OwnAudioFixture");
            root.AddComponent<AudioListener>();
            settings = new SettingsData { masterVolume = .8f, sfxVolume = .5f };
            var assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            audio = new RokasAudio(root, assets, settings);
            cues = new ReactiveCombatAudio(audio);
            Assert.That(cues.FidelityClipsReady, Is.True, "Own short PCM clips must preload before a contact.");
        }

        [UnityTest]
        public IEnumerator PreviewConfirmCancelHaveDistinctRealPlaybackAndRejectStaleTokens()
        {
            Assert.That(cues.PresentPreview("selected-normal", "normal"), Is.True);
            AudioSource preview = Source("StancePreview");
            Assert.That(preview.isPlaying, Is.True);
            Assert.That(cues.PresentPreview("selected-normal", "normal"), Is.True);
            Assert.That(cues.PlaybackCount, Is.EqualTo(1));
            Assert.That(cues.PresentConfirm("selected-normal"), Is.True);
            AudioSource confirm = Source("StanceConfirm");
            Assert.That(confirm.isPlaying, Is.True);
            Assert.That(confirm, Is.Not.SameAs(preview));
            Assert.That(cues.PresentConfirm("selected-normal"), Is.True);
            Assert.That(cues.PlaybackCount, Is.EqualTo(2));
            Assert.That(cues.PresentCancel("selected-normal"), Is.False, "Committed confirmation is not a cancellation.");
            Assert.That(cues.PresentPreview("selected-heavy", "heavy"), Is.True);
            Assert.That(cues.PresentCancel("selected-heavy"), Is.True);
            AudioSource cancel = Source("StanceCancel");
            Assert.That(cancel.isPlaying, Is.True);
            Assert.That(cues.PresentCancel("selected-heavy"), Is.True);
            Assert.That(cues.PresentConfirm("selected-heavy"), Is.False);
            Assert.That(cues.PresentPreview("selected-heavy", "heavy"), Is.False, "A canceled token cannot be rearmed.");
            Assert.That(cues.PresentPreview("selected-throw", "throw_blade"), Is.True);
            Assert.That(cues.PresentConfirm("selected-normal"), Is.False, "A stale token cannot confirm the new preview.");
            Assert.That(cues.PlaybackCount, Is.EqualTo(5));
            Assert.That(audio.ClickCount, Is.Zero, "Combat cues must not invoke the generic click route.");
            Assert.That(cues.Dispatches.Select(d => d.EventId).ToArray(), Is.EqualTo(new[] {
                ReactiveCombatAudioEvent.StancePreview, ReactiveCombatAudioEvent.StanceConfirm,
                ReactiveCombatAudioEvent.StancePreview, ReactiveCombatAudioEvent.StanceCancel,
                ReactiveCombatAudioEvent.StancePreview }));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReadinessAndWarningPreserveVocalAliasAndAccelerationOwnership()
        {
            Assert.That(cues.PresentReadiness("normal-visual", false), Is.True);
            Assert.That(Source("SwordReadiness").isPlaying, Is.True);
            cues.BindActionId("normal-core", "normal-visual");
            Assert.That(cues.PresentReadiness("normal-core", false), Is.True);
            Assert.That(cues.PlaybackCount, Is.EqualTo(1));
            cues.PresentAttackStarted("normal-visual", "P", false);
            cues.Present(new CombatEvent(CombatEventKind.CommandCommitted, actorId: "P", actionId: "normal-core", detail: "Basic"));
            Assert.That(cues.Dispatches.Count(d => d.EventId == ReactiveCombatAudioEvent.KeikoNormalVocal), Is.EqualTo(1));
            Assert.That(cues.Dispatches.Any(d => d.EventId == ReactiveCombatAudioEvent.NormalWhoosh), Is.False);
            settings.masterVolume = .3f; settings.sfxVolume = .2f;
            Assert.That(cues.PresentReadiness("heavy-visual", true), Is.True);
            AudioSource heavy = Source("HeavyWindup");
            Assert.That(heavy.isPlaying, Is.True);
            Assert.That(heavy.volume, Is.EqualTo(.3f * .2f * .26f).Within(.001f));
            cues.PresentAttackStarted("heavy-visual", "P", true);
            cues.BindActionId("heavy-core", "heavy-visual");
            cues.Present(new CombatEvent(CombatEventKind.CommandCommitted, actorId: "P", actionId: "heavy-core", detail: "heavy"));
            Assert.That(cues.Dispatches.Count(d => d.EventId == ReactiveCombatAudioEvent.KeikoHeavyVocal), Is.EqualTo(1));
            Assert.That(cues.PresentEnemyWarning("enemy-sequence", "E1"), Is.True);
            AudioSource warning = Source("EnemyWarning");
            Assert.That(warning.isPlaying, Is.True);
            Assert.That(cues.PresentEnemyWarning("enemy-sequence", "E1"), Is.True);
            cues.PresentAttackStarted("enemy-sequence", "E1", false);
            cues.PresentAttackStarted("enemy-sequence", "E1", false);
            Assert.That(cues.Dispatches.Count(d => d.EventId == ReactiveCombatAudioEvent.EnemyWarning), Is.EqualTo(1));
            Assert.That(cues.Dispatches.Count(d => d.EventId == ReactiveCombatAudioEvent.MonsterVocal), Is.EqualTo(1));
            Assert.That(cues.PresentEnemyWarning("invalid-hunter-warning", "P"), Is.False);
            Assert.That(cues.Dispatches.Any(d => d.EventId == ReactiveCombatAudioEvent.HeavyWhoosh ||
                d.EventId == ReactiveCombatAudioEvent.GuardMetalContact || d.EventId == ReactiveCombatAudioEvent.ThrowRelease), Is.False);
            Assert.That(audio.ClickCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DuplicateActualGuardDoesNotRestartSourceAndThrowCommitStaysSilent()
        {
            int sourcesBefore = root.GetComponentsInChildren<AudioSource>(true).Length;
            cues.PresentContact("incoming-one", "E1", false, "Parry", 7, "hit-one");
            AudioSource guard = Source("GuardMetalContact");
            Assert.That(guard.clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
            Assert.That(guard.isPlaying, Is.True);
            yield return new WaitForSecondsRealtime(.025f);
            int progressed = guard.timeSamples;
            Assert.That(progressed, Is.GreaterThan(0), "Real AudioSource/DSP progression must be observed, not just a counter.");
            cues.PresentContact("incoming-one", "E1", false, "Parry", 7, "hit-one");
            Assert.That(Source("GuardMetalContact"), Is.SameAs(guard));
            Assert.That(guard.timeSamples, Is.GreaterThanOrEqualTo(progressed), "A duplicate must not restart the clip.");
            Assert.That(cues.Dispatches.Count(d => d.EventId == ReactiveCombatAudioEvent.GuardMetalContact), Is.EqualTo(1));
            Assert.That(cues.Dispatches.Any(d => d.EventId == ReactiveCombatAudioEvent.MonsterFleshContact), Is.False);
            cues.BindActionId("throw-core", "throw-visual");
            int beforeCommit = cues.PlaybackCount;
            cues.Present(new CombatEvent(CombatEventKind.CommandCommitted, actorId: "P", actionId: "throw-core", detail: "throw_blade"));
            Assert.That(cues.PlaybackCount, Is.EqualTo(beforeCommit));
            cues.PresentThrowRelease("throw-visual"); cues.PresentThrowRelease("throw-core");
            Assert.That(Source("ThrowRelease").isPlaying, Is.True);
            cues.PresentContact("throw-core", "P", false, null, 20);
            cues.PresentContact("throw-visual", "P", false, null, 20);
            Assert.That(Source("ThrowContact").isPlaying, Is.True);
            Assert.That(cues.Dispatches.Count(d => d.EventId == ReactiveCombatAudioEvent.ThrowRelease), Is.EqualTo(1));
            Assert.That(cues.Dispatches.Count(d => d.EventId == ReactiveCombatAudioEvent.ThrowFleshContact), Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(sourcesBefore));
        }

        [UnityTest]
        public IEnumerator DeathCuePlaysOncePerDeadActorAndResetAllowsTheNextEncounter()
        {
            Assert.That(cues.PresentDeath("lethal-action", "E1"), Is.True);
            AudioSource source = Source("YokaiDeath");
            Assert.That(source.isPlaying, Is.True);
            Assert.That(source.volume, Is.EqualTo(.8f * .5f * .32f).Within(.001f));
            yield return new WaitForSecondsRealtime(.025f);
            int progressed = source.timeSamples;
            Assert.That(progressed, Is.GreaterThan(0));
            var sourcePcm = new float[512]; var mixPcm = new float[512];
            float sourcePeak = 0f, mixPeak = 0f;
            float until = Time.realtimeSinceStartup + .12f;
            while (Time.realtimeSinceStartup < until)
            {
                source.GetOutputData(sourcePcm, 0); AudioListener.GetOutputData(mixPcm, 0);
                sourcePeak = Mathf.Max(sourcePeak, sourcePcm.Max(v => Mathf.Abs(v)));
                mixPeak = Mathf.Max(mixPeak, mixPcm.Max(v => Mathf.Abs(v)));
                yield return null;
            }
            var editorUtility = System.Type.GetType("UnityEditor.EditorUtility, UnityEditor.CoreModule", false);
            object editorMuted = editorUtility?.GetProperty("audioMasterMute", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null);
            TestContext.WriteLine($"Death DSP: sourcePeak={sourcePeak:R}, mixPeak={mixPeak:R}, sourceVolume={source.volume:R}, sourceMute={source.mute}, sourceVirtual={source.isVirtual}, listenerVolume={AudioListener.volume:R}, listenerPause={AudioListener.pause}, editorMute={editorMuted}, frequency={AudioSettings.outputSampleRate}");
            Assert.That(sourcePeak, Is.GreaterThan(.00001f), "Actual source PCM must be nonzero; timeSamples alone cannot prove sound output.");
            Assert.That(cues.PresentDeath("replayed-action", "E1"), Is.True);
            Assert.That(source.timeSamples, Is.GreaterThanOrEqualTo(progressed), "A duplicate death cannot restart its DSP source.");
            Assert.That(cues.PlaybackCount, Is.EqualTo(1));
            Assert.That(cues.PresentDeath("invalid", "P"), Is.False);
            Assert.That(cues.PresentDeath("", "E2"), Is.False);
            Assert.That(cues.PresentDeath("second-lethal", "E2"), Is.True);
            Assert.That(cues.Dispatches.Count(d => d.EventId == ReactiveCombatAudioEvent.YokaiDeath), Is.EqualTo(2));
            cues.Reset();
            Assert.That(cues.PresentDeath("next-encounter", "E1"), Is.True);
            Assert.That(cues.PlaybackCount, Is.EqualTo(1));
        }

        [Test]
        public void OriginalFidelityClipsHavePcmAndResetAllowsNewSelection()
        {
            foreach (string name in new[] { "StancePreview", "StanceConfirm", "StanceCancel", "SwordReadiness", "HeavyWindup", "EnemyWarning", "YokaiDeath" })
            {
                var clip = Resources.Load<AudioClip>("Audio/CombatFidelity3/" + name);
                Assert.That(clip, Is.Not.Null, name);
                Assert.That(clip.frequency, Is.EqualTo(48000), name);
                Assert.That(clip.channels, Is.EqualTo(2), name);
                Assert.That(clip.length, Is.InRange(.07f, .5f), name);
                var pcm = new float[clip.samples * clip.channels];
                Assert.That(clip.GetData(pcm, 0), Is.True);
                Assert.That(pcm.Any(s => Mathf.Abs(s) > .001f), Is.True);
                Assert.That(pcm.Max(s => Mathf.Abs(s)), Is.LessThan(.7f));
            }
            Assert.That(cues.PresentPreview("first", "normal"), Is.True);
            Assert.That(cues.PresentPreview("invalid", "unavailable-stance"), Is.False);
            cues.Reset();
            Assert.That(cues.PresentPreview("first", "normal"), Is.True);
            Assert.That(cues.PlaybackCount, Is.EqualTo(1));
        }

        private AudioSource Source(string clipName) => root.GetComponentsInChildren<AudioSource>(true)
            .Single(s => s.clip != null && s.clip.name == clipName);
        [TearDown]
        public void Cleanup() { audio?.Dispose(); if (root != null) Object.DestroyImmediate(root); }
    }
}
