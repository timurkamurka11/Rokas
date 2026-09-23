using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerPrereleaseUiTests
    {
        private static VnSceneComposerProject Project()
        {
            var p = new VnSceneComposerProject();
            p.scenes.Add(new VnSceneComposerScene { dialogueBeats = new List<VnSceneComposerDialogueBeat> { new VnSceneComposerDialogueBeat { text = "Outgoing dialogue with two characters." }, new VnSceneComposerDialogueBeat { text = "Second beat." } } });
            p.scenes.Add(new VnSceneComposerScene { dialogueBeats = new List<VnSceneComposerDialogueBeat> { new VnSceneComposerDialogueBeat { text = "Incoming dialogue." } } });
            p.scenes[0].characters.Add(new VnSceneComposerCharacter { characterId = "Mina", stateId = "mina_neutral", stageSlot = VnWorkshopStageSlot.Left });
            p.scenes[0].characters.Add(new VnSceneComposerCharacter { characterId = "Keiko", stateId = "keiko_neutral", stageSlot = VnWorkshopStageSlot.Right });
            p.scenes[1].transition.sceneTransitionType = VnSceneComposerSceneTransitionType.DarkCurtain;
            p.scenes[1].transition.sceneTransitionDuration = 1f;
            return p;
        }

        [TestCase(0f)] [TestCase(.1f)] [TestCase(.49f)]
        public void CoverPreservesOutgoingCharacterVisibility(float dt)
        {
            using (var c = new VnSceneComposerPlaybackController(Project()))
            {
                c.PlayFromHere(0); c.Advance(3f);
                var before = c.CurrentFrame;
                Assert.That(before.ShowCharacters, Is.True);
                c.Next(); c.Advance(dt);
                Assert.That(c.CurrentSceneIndex, Is.EqualTo(0));
                Assert.That(c.CurrentFrame.ShowCharacters, Is.True, "Outgoing characters disappeared before cover.");
                Assert.That(c.CurrentFrame.ComposerCharacters.Length, Is.EqualTo(before.ComposerCharacters.Length));
                for (int i = 0; i < before.ComposerCharacters.Length; i++)
                {
                    Assert.That(c.CurrentFrame.ComposerCharacters[i].Body, Is.EqualTo(before.ComposerCharacters[i].Body));
                    Assert.That(c.CurrentFrame.ComposerCharacters[i].StateId, Is.EqualTo(before.ComposerCharacters[i].StateId));
                    Assert.That(c.CurrentFrame.ComposerCharacters[i].Alpha, Is.EqualTo(before.ComposerCharacters[i].Alpha));
                }
            }
        }
        [Test]
        public void CoverPreservesOutgoingPlaqueTextAndMedia()
        {
            using (var c = new VnSceneComposerPlaybackController(Project()))
            {
                c.PlayFromHere(0); c.Advance(3f);
                var before = c.CurrentFrame;
                c.Next(); c.Advance(.2f);
                Assert.That(c.CurrentFrame.ShowDialoguePanel, Is.EqualTo(before.ShowDialoguePanel));
                Assert.That(c.CurrentFrame.ShowDialogueText, Is.EqualTo(before.ShowDialogueText));
                Assert.That(c.CurrentFrame.Dialogue, Is.EqualTo(before.Dialogue));
                Assert.That(c.CurrentFrame.TargetBackground, Is.SameAs(before.TargetBackground));
                Assert.That(c.CurrentFrame.WorkshopFrame, Is.SameAs(before.WorkshopFrame), "Cover must retain the final rendered composition, not reconstruct authoring state.");
            }
        }
        [Test]
        public void NoneBoundaryCommitsIncomingCharactersThenUsesCanonicalEntryGate()
        {
            var p = Project(); p.scenes[1].transition.sceneTransitionType = VnSceneComposerSceneTransitionType.None;
            p.scenes[1].characters.Add(new VnSceneComposerCharacter { characterId = "Mina", stateId = "mina_neutral" });
            using (var c = new VnSceneComposerPlaybackController(p))
            {
                c.PlayFromHere(0); c.Advance(3f); c.Next();

                Assert.That(c.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(c.CurrentFrame.ComposerCharacters.Length, Is.EqualTo(1),
                    "Transition=None must atomically commit the incoming character data in the target frame.");
                Assert.That(c.CurrentFrame.ShowDialoguePanel, Is.True);
                Assert.That(c.CurrentFrame.ShowCharacters, Is.False,
                    "Transition=None must preserve the canonical plaque-then-characters Scene-entry gate.");

                c.Advance(.08f);

                Assert.That(c.CurrentFrame.ShowCharacters, Is.True);
                Assert.That(c.CurrentFrame.ComposerCharacters.Length, Is.EqualTo(1));
                Assert.That(c.CurrentFrame.ComposerCharacters[0].Alpha, Is.GreaterThan(.99f));
            }
        }
        [Test]
        public void ReplacingPlaybackCancelsOldTransition()
        {
            using (var c = new VnSceneComposerPlaybackController(Project()))
            {
                c.PlayFromHere(0); c.Advance(3f); c.Next(); c.Advance(.2f);
                c.PlayScene(0); c.Advance(2f);
                Assert.That(c.CurrentSceneIndex, Is.EqualTo(0));
                Assert.That(c.IsSceneTransitionActive, Is.False);
            }
        }
    }
}
