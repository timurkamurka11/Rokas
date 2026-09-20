using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerCharacterStagingTests
    {
        private const string TrackAGuid = "7540bd61684b4a768cad4691dbc54972";

        private sealed class StagingVideoPreview : VnSceneComposerVideoPreview
        {
            public int PrepareCalls;
            public int PlayCalls;
            public int RestartCalls;
            public int DisposeCalls;
            private bool playing;

            public StagingVideoPreview(int serial) : base(null, 16, 16, true)
            {
                warning = string.Empty;
                texture = new RenderTexture(64, 36, 0) { name = "ROKAS_StagingVideo_" + serial };
                texture.Create();
            }

            public override bool IsPrepared { get { return true; } }
            public override bool IsPreparing { get { return false; } }
            public override bool IsPlaying { get { return playing; } }
            public override bool HasVisibleFrame { get { return true; } }
            public override bool HasFailed { get { return false; } }
            public override bool IsRequestReusable { get { return true; } }
            public override bool IsReadyForCurrentRequest { get { return true; } }
            public override void Prepare() { PrepareCalls++; }
            public override void Play() { PlayCalls++; playing = true; }
            public override void Pause() { playing = false; }
            public override void Restart() { RestartCalls++; playing = true; }
            public override void Dispose() { DisposeCalls++; playing = false; base.Dispose(); }
        }

        private sealed class StagingVideoFactory : IVnSceneComposerVideoPreviewFactory
        {
            public readonly List<StagingVideoPreview> Created = new List<StagingVideoPreview>();
            public int TotalPrepareCalls { get { return Created.Sum(p => p.PrepareCalls); } }

            public VnSceneComposerVideoPreview Open(VnSceneComposerMediaReference media, int width, int height)
            {
                var preview = new StagingVideoPreview(Created.Count);
                Created.Add(preview);
                return preview;
            }
        }

        [Test] public void MCS_01_DataModelExposesBeatStagingCollection()
        {
            var beat = new VnSceneComposerDialogueBeat();
            Assert.That(Field(beat, "characterStaging"), Is.Not.Null);
            Assert.That(RequireType("VnSceneComposerBeatCharacterStaging"), Is.Not.Null);
        }

        [Test] public void MCS_02_NoBeatStagingPreservesLegacyVisibleCharacter()
        {
            var project = Project(SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center)));
            var scene = project.scenes[0];
            AssertVisible(Build(scene, scene.dialogueBeats[0], 0f), "Mina", true);
        }

        [Test] public void MCS_03_CharacterCanAppearOnLaterBeat()
        {
            var scene = SceneWithCharacters(
                ("Mina","mina_neutral",VnWorkshopStageSlot.Center),
                ("Keiko","keiko_neutral",VnWorkshopStageSlot.Right));
            var second = AddBeat(scene);
            AddStaging(second, "Keiko", "Show", "KeepPrevious", false, "", 0f);
            var project = Project(scene);
            AssertVisible(Build(scene, scene.dialogueBeats[0], 0f), "Keiko", false);
            AssertVisible(Build(scene, second, 0f), "Keiko", true);
        }

        [Test] public void MCS_04_CharacterCanHideOnLaterBeat()
        {
            var scene = SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var second=AddBeat(scene);
            AddStaging(second,"Mina","Hide","KeepPrevious",false,"",0f);
            AssertVisible(Build(scene,second,0f),"Mina",false);
        }

        [Test] public void MCS_05_PositionMovesCenterToLeft()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var second=AddBeat(scene);
            AddStaging(second,"Mina","KeepPrevious","Left",false,"",0f);
            Assert.That(Character(Build(scene,second,0f),"Mina").Slot,Is.EqualTo(VnWorkshopStageSlot.Left));
        }

        [Test] public void MCS_06_SecondCharacterAppearsRightOnSameBeat()
        {
            var scene=SceneWithCharacters(
                ("Mina","mina_neutral",VnWorkshopStageSlot.Center),
                ("Keiko","keiko_neutral",VnWorkshopStageSlot.Center));
            var second=AddBeat(scene);
            AddStaging(second,"Mina","Show","Left",false,"",0f);
            AddStaging(second,"Keiko","Show","Right",false,"",0f);
            var frame=Build(scene,second,0f);
            Assert.That(Character(frame,"Mina").Slot,Is.EqualTo(VnWorkshopStageSlot.Left));
            Assert.That(Character(frame,"Keiko").Slot,Is.EqualTo(VnWorkshopStageSlot.Right));
        }

        [Test] public void MCS_07_MultipleCharactersRemainVisible()
        {
            var scene=SceneWithCharacters(
                ("Mina","mina_neutral",VnWorkshopStageSlot.Left),
                ("Keiko","keiko_neutral",VnWorkshopStageSlot.Right));
            AssertVisible(Build(scene,scene.dialogueBeats[0],0f),"Mina",true);
            AssertVisible(Build(scene,scene.dialogueBeats[0],0f),"Keiko",true);
        }

        [Test] public void MCS_08_NonSpeakerRemainsVisible()
        {
            var scene=SceneWithCharacters(
                ("Mina","mina_neutral",VnWorkshopStageSlot.Left),
                ("Keiko","keiko_neutral",VnWorkshopStageSlot.Right));
            scene.dialogueBeats[0].speaker="Mina";
            AssertVisible(Build(scene,scene.dialogueBeats[0],0f),"Keiko",true);
        }

        [Test] public void MCS_09_NarratorDoesNotHideCharacters()
        {
            var scene=SceneWithCharacters(
                ("Mina","mina_neutral",VnWorkshopStageSlot.Left),
                ("Keiko","keiko_neutral",VnWorkshopStageSlot.Right));
            scene.dialogueBeats[0].narration=true;
            scene.dialogueBeats[0].speaker="";
            var frame=Build(scene,scene.dialogueBeats[0],0f);
            AssertVisible(frame,"Mina",true); AssertVisible(frame,"Keiko",true);
        }

        [Test] public void MCS_10_StateChangeUsesExistingM_F1StateIds()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var second=AddBeat(scene);
            AddStaging(second,"Mina","KeepPrevious","KeepPrevious",true,"mina_happy",0f);
            Assert.That(Character(Build(scene,second,0f),"Mina").StateId,Is.EqualTo("mina_happy"));
        }

        [Test] public void MCS_11_KeepPreviousVisibilityInherits()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            AddStaging(scene.dialogueBeats[0],"Mina","Show","KeepPrevious",false,"",0f);
            var second=AddBeat(scene);
            AddStaging(second,"Mina","KeepPrevious","KeepPrevious",false,"",0f);
            AssertVisible(Build(scene,second,0f),"Mina",true);
        }

        [Test] public void MCS_12_KeepPreviousPositionInherits()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            AddStaging(scene.dialogueBeats[0],"Mina","KeepPrevious","Left",false,"",0f);
            var second=AddBeat(scene);
            AddStaging(second,"Mina","KeepPrevious","KeepPrevious",false,"",0f);
            Assert.That(Character(Build(scene,second,0f),"Mina").Slot,Is.EqualTo(VnWorkshopStageSlot.Left));
        }

        [Test] public void MCS_13_KeepPreviousStateInherits()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            AddStaging(scene.dialogueBeats[0],"Mina","KeepPrevious","KeepPrevious",true,"mina_happy",0f);
            var second=AddBeat(scene);
            AddStaging(second,"Mina","KeepPrevious","KeepPrevious",false,"",0f);
            Assert.That(Character(Build(scene,second,0f),"Mina").StateId,Is.EqualTo("mina_happy"));
        }

        [Test] public void MCS_14_LegacyAccentStillTargetsCharacter()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var beat=scene.dialogueBeats[0];
            beat.targetCharacterId="Mina";
            beat.effect=VnSceneComposerBeatEffect.Accent;
            beat.effectStrength=35f; beat.effectDuration=.4f;
            var project=Project(scene);
            using(var c=new VnSceneComposerPlaybackController(project))
            {
                c.PlayScene(0); c.Advance(.2f);
                Assert.That(c.CurrentFrame.WorkshopFrame.ComposerCharacters.Length,Is.EqualTo(1));
            }
        }

        [Test] public void MCS_15_BeatWithoutOverrideInheritsResolvedState()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            AddStaging(scene.dialogueBeats[0],"Mina","Show","Left",true,"mina_happy",0f);
            var second=AddBeat(scene);
            var ch=Character(Build(scene,second,0f),"Mina");
            Assert.That(ch.Slot,Is.EqualTo(VnWorkshopStageSlot.Left));
            Assert.That(ch.StateId,Is.EqualTo("mina_happy"));
        }

        [Test] public void MCS_16_DelayedShowWaitsForBeatElapsed()
        {
            var scene=SceneWithCharacters(("Keiko","keiko_neutral",VnWorkshopStageSlot.Right));
            AddStaging(scene.dialogueBeats[0],"Keiko","Show","Right",false,"",1f);
            AssertVisible(Build(scene,scene.dialogueBeats[0],.5f),"Keiko",false);
            AssertVisible(Build(scene,scene.dialogueBeats[0],1.01f),"Keiko",true);
        }

        [Test] public void MCS_17_DelayedHideWaitsForBeatElapsed()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            AddStaging(scene.dialogueBeats[0],"Mina","Hide","KeepPrevious",false,"",1f);
            AssertVisible(Build(scene,scene.dialogueBeats[0],.5f),"Mina",true);
            AssertVisible(Build(scene,scene.dialogueBeats[0],1.01f),"Mina",false);
        }

        [Test] public void MCS_18_DelayedMovementWaitsForBeatElapsed()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            AddStaging(scene.dialogueBeats[0],"Mina","KeepPrevious","Left",false,"",1f);
            Assert.That(Character(Build(scene,scene.dialogueBeats[0],.5f),"Mina").Slot,Is.EqualTo(VnWorkshopStageSlot.Center));
            Assert.That(Character(Build(scene,scene.dialogueBeats[0],1.01f),"Mina").Slot,Is.EqualTo(VnWorkshopStageSlot.Left));
        }

        [Test] public void MCS_19_StaleDelayedActionIsCancelledAfterBeatAdvance()
        {
            var scene=SceneWithCharacters(("Keiko","keiko_neutral",VnWorkshopStageSlot.Right));
            AddStaging(scene.dialogueBeats[0],"Keiko","Show","Right",false,"",2f);
            AddBeat(scene);
            var project=Project(scene);
            using(var c=new VnSceneComposerPlaybackController(project))
            {
                c.PlayScene(0); c.Advance(.4f); c.AdvanceDialogue(); c.Advance(3f);
                AssertVisible(c.CurrentFrame.WorkshopFrame,"Keiko",false);
            }
        }

        [Test] public void MCS_20_PreviousDialogueReconstructsTargetBeat()
        {
            var scene=SceneWithCharacters(
                ("Mina","mina_neutral",VnWorkshopStageSlot.Center),
                ("Keiko","keiko_neutral",VnWorkshopStageSlot.Right));
            var b1=scene.dialogueBeats[0]; AddStaging(b1,"Mina","Show","Center",false,"",0f);
            var b2=AddBeat(scene); AddStaging(b2,"Mina","KeepPrevious","Left",false,"",0f); AddStaging(b2,"Keiko","Show","Right",false,"",0f);
            var b3=AddBeat(scene); AddStaging(b3,"Keiko","Hide","KeepPrevious",false,"",0f);
            var project=Project(scene);
            using(var c=new VnSceneComposerPlaybackController(project))
            {
                c.PlaySceneFromNeutralStart(0); c.AdvanceDialogue(); c.AdvanceDialogue();
                Invoke(c,"PreviousDialogue");
                Assert.That(c.CurrentBeatIndex,Is.EqualTo(1));
                Assert.That(Character(c.CurrentFrame.WorkshopFrame,"Mina").Slot,Is.EqualTo(VnWorkshopStageSlot.Left));
                AssertVisible(c.CurrentFrame.WorkshopFrame,"Keiko",true);
            }
        }

        [Test] public void MCS_21_PlayFromHereBeatReconstructsPriorStaging()
        {
            var scene=SceneWithCharacters(
                ("Mina","mina_neutral",VnWorkshopStageSlot.Center),
                ("Keiko","keiko_neutral",VnWorkshopStageSlot.Right));
            AddStaging(scene.dialogueBeats[0],"Mina","Show","Center",false,"",0f);
            var b2=AddBeat(scene); AddStaging(b2,"Mina","KeepPrevious","Left",true,"mina_happy",0f); AddStaging(b2,"Keiko","Show","Right",false,"",1f);
            AddBeat(scene);
            var project=Project(scene);
            using(var c=new VnSceneComposerPlaybackController(project))
            {
                c.PlayFromHereFromNeutralStart(0,2);
                Assert.That(c.CurrentBeatIndex,Is.EqualTo(2));
                Assert.That(Character(c.CurrentFrame.WorkshopFrame,"Mina").StateId,Is.EqualTo("mina_happy"));
                AssertVisible(c.CurrentFrame.WorkshopFrame,"Keiko",true);
            }
        }

        [Test] public void MCS_22_StaticPreviewMatchesSettledPlayback()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            AddStaging(scene.dialogueBeats[0],"Mina","Show","Left",true,"mina_happy",.2f);
            var settled=VnSceneComposerComposition.BuildFrame(Project(scene),scene,scene.dialogueBeats[0],VnWorkshopResolution.Reference1920x1080,null);
            var timed=Build(scene,scene.dialogueBeats[0],1f);
            Assert.That(Character(settled,"Mina").StateId,Is.EqualTo(Character(timed,"Mina").StateId));
            Assert.That(Character(settled,"Mina").Slot,Is.EqualTo(Character(timed,"Mina").Slot));
        }

        [Test] public void MCS_23_SaveReopenPreservesStaging()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            object row=AddStaging(scene.dialogueBeats[0],"Mina","Show","Left",true,"mina_happy",.75f);
            var project=Project(scene);
            string json=JsonUtility.ToJson(project);
            var clone=JsonUtility.FromJson<VnSceneComposerProject>(json);
            IList rows=(IList)Field(clone.scenes[0].dialogueBeats[0],"characterStaging").GetValue(clone.scenes[0].dialogueBeats[0]);
            Assert.That(rows,Has.Count.EqualTo(1));
            Assert.That(Get<float>(rows[0],"delaySeconds"),Is.EqualTo(.75f).Within(.001f));
        }

        [Test] public void MCS_24_DuplicateSceneCopiesIndependentStagingIds()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            object row=AddStaging(scene.dialogueBeats[0],"Mina","Show","Left",false,"",0f);
            var project=Project(scene);
            string originalId=Get<string>(row,"stagingId");
            var copy=VnSceneComposerEditing.DuplicateScene(project,scene.sceneId);
            IList rows=(IList)Field(copy.dialogueBeats[0],"characterStaging").GetValue(copy.dialogueBeats[0]);
            Assert.That(rows,Has.Count.EqualTo(1));
            Assert.That(Get<string>(rows[0],"stagingId"),Is.Not.EqualTo(originalId));
        }

        [Test] public void MCS_25_WindowStagingMutationParticipatesInUndo()
        {
            VnPresentationWorkshopWindow w=ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                w.ComposerAddScene(); w.ComposerAddCharacter("Mina","mina_neutral");
                Undo.ClearAll();
                Invoke(w,"ComposerAddSelectedDialogueBeatCharacterStaging","Mina");
                Undo.FlushUndoRecordObjects();
                var project=(VnSceneComposerProject)PrivateField(w,"_sceneComposerProject").GetValue(w);
                Assert.That(((IList)Field(project.scenes[0].dialogueBeats[0],"characterStaging").GetValue(project.scenes[0].dialogueBeats[0])).Count,Is.EqualTo(1));
                Undo.PerformUndo();
                project=(VnSceneComposerProject)PrivateField(w,"_sceneComposerProject").GetValue(w);
                Assert.That(((IList)Field(project.scenes[0].dialogueBeats[0],"characterStaging").GetValue(project.scenes[0].dialogueBeats[0])).Count,Is.EqualTo(0));
            }
            finally { Undo.ClearAll(); UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_26_MissingStagingStateIsSafeAndKeepsPrevious()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            AddStaging(scene.dialogueBeats[0],"Mina","Show","Left",true,"missing_state",0f);
            VnWorkshopPreviewFrame frame=null;
            Assert.DoesNotThrow(()=>frame=Build(scene,scene.dialogueBeats[0],0f));
            Assert.That(Character(frame,"Mina").StateId,Is.EqualTo("mina_neutral"));
            string[] warnings=(string[])Property(frame,"ComposerCharacterWarnings").GetValue(frame);
            Assert.That(warnings.Any(x=>x.Contains("missing_state")),Is.True);
        }

        [Test] public void MCS_27_CharacterStagingDoesNotRestartVideoPlayer()
        {
            WithVideo((factory,scene,controller)=>{
                controller.PlayScene(0);
                var p=factory.Created[0]; int restarts=p.RestartCalls;
                controller.AdvanceDialogue();
                Assert.That(p.RestartCalls,Is.EqualTo(restarts));
            });
        }

        [Test] public void MCS_28_CharacterStagingDoesNotReprepareSameVideo()
        {
            WithVideo((factory,scene,controller)=>{
                controller.PlayScene(0);
                int prepares=factory.TotalPrepareCalls;
                controller.AdvanceDialogue();
                Assert.That(factory.TotalPrepareCalls,Is.EqualTo(prepares));
            });
        }

        [Test] public void MCS_29_CharacterStagingDoesNotRestartBgm()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            SetMusic(scene); AddBeat(scene);
            AddStaging(scene.dialogueBeats[1],"Mina","KeepPrevious","Left",false,"",0f);
            using(var c=new VnSceneComposerPlaybackController(Project(scene)))
            {
                c.PlayScene(0); int starts=c.MusicStartCount; c.AdvanceDialogue();
                Assert.That(c.MusicStartCount,Is.EqualTo(starts));
            }
        }

        [Test] public void MCS_30_CharacterStagingDoesNotRetriggerActiveSfx()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var cue=new VnSceneComposerAdditionalAudioCue{assetGuid=TrackAGuid,enabled=true,loop=true,trigger=VnSceneComposerAudioTrigger.SceneStart,stopMode=VnSceneComposerAudioStopMode.SceneEnd};
            scene.additionalAudioCues.Add(cue); AddBeat(scene);
            AddStaging(scene.dialogueBeats[1],"Mina","KeepPrevious","Left",false,"",0f);
            using(var c=new VnSceneComposerPlaybackController(Project(scene)))
            {
                c.PlayScene(0); int starts=c.GetAdditionalAudioCueStartCount(cue.cueId); c.AdvanceDialogue();
                Assert.That(c.GetAdditionalAudioCueStartCount(cue.cueId),Is.EqualTo(starts));
                Assert.That(c.IsAdditionalAudioCueActive(cue.cueId),Is.True);
            }
        }

        [Test] public void MCS_31_CharacterStagingDoesNotTriggerSceneTransition()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            AddBeat(scene); AddStaging(scene.dialogueBeats[1],"Mina","KeepPrevious","Left",false,"",0f);
            using(var c=new VnSceneComposerPlaybackController(Project(scene)))
            {
                c.PlayScene(0); c.AdvanceDialogue();
                Assert.That(c.CurrentSceneIndex,Is.EqualTo(0));
                Assert.That(c.IsSceneTransitionActive,Is.False);
            }
        }

        [Test] public void MCS_32_ExistingM_F1SingleStateOverrideRemainsCompatible()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            scene.dialogueBeats[0].targetCharacterId="Mina";
            scene.dialogueBeats[0].hasStateOverride=true;
            scene.dialogueBeats[0].stateId="mina_happy";
            Assert.That(Character(Build(scene,scene.dialogueBeats[0],0f),"Mina").StateId,Is.EqualTo("mina_happy"));
        }

        [Test] public void MCS_33_UiExposesBeatCharacterStagingSection()
        {
            string path=Path.Combine(Application.dataPath,"Rokas","Scripts","Editor","VnUiWorkshop","VnPresentationWorkshopWindow.SceneComposerCharacterStaging.cs");
            Assert.That(File.Exists(path),Is.True);
            string source=File.ReadAllText(path);
            Assert.That(source,Does.Contain("Персонажи в этой реплике"));
            Assert.That(source,Does.Contain("+ Добавить персонажа"));
            Assert.That(source,Does.Contain("Оставить предыдущее"));
        }

        [Test] public void MCS_34_MultipleOverridesBelongToOneBeat()
        {
            var scene=SceneWithCharacters(
                ("Mina","mina_neutral",VnWorkshopStageSlot.Left),
                ("Keiko","keiko_neutral",VnWorkshopStageSlot.Right));
            AddStaging(scene.dialogueBeats[0],"Mina","Show","Left",false,"",0f);
            AddStaging(scene.dialogueBeats[0],"Keiko","Show","Right",false,"",0f);
            IList rows=(IList)Field(scene.dialogueBeats[0],"characterStaging").GetValue(scene.dialogueBeats[0]);
            Assert.That(rows,Has.Count.EqualTo(2));
        }

        [Test] public void MCS_35_CustomPositionUsesExistingOffsetCoordinateSystem()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Left));
            object row=AddStaging(scene.dialogueBeats[0],"Mina","Show","Custom",false,"",0f);
            Set(row,"customPositionOffset",new Vector2(123f,-45f));
            var ch=Character(Build(scene,scene.dialogueBeats[0],0f),"Mina");
            Assert.That(ch.Slot,Is.EqualTo(VnWorkshopStageSlot.Left),
                "Custom X/Y must extend the existing slot+offset coordinate model.");
        }

        [Test] public void MCS_36_DuplicateBeatCopiesStagingWithNewIds()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            object row=AddStaging(scene.dialogueBeats[0],"Mina","Show","Left",false,"",0f);
            string id=Get<string>(row,"stagingId");
            Type dialogue=RequireType("VnSceneComposerDialogue");
            MethodInfo duplicate=dialogue.GetMethod("DuplicateBeat",BindingFlags.Static|BindingFlags.NonPublic);
            object copy=duplicate.Invoke(null,new object[]{scene,scene.dialogueBeats[0].beatId});
            IList rows=(IList)Field(copy,"characterStaging").GetValue(copy);
            Assert.That(rows,Has.Count.EqualTo(1));
            Assert.That(Get<string>(rows[0],"stagingId"),Is.Not.EqualTo(id));
        }

        [Test] public void MCS_37_PlayFromHereBeatDoesNotTriggerSkippedBeatSfx()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var first=scene.dialogueBeats[0];
            var second=AddBeat(scene);
            var third=AddBeat(scene);
            var skippedCue=new VnSceneComposerAdditionalAudioCue
            {
                assetGuid=TrackAGuid, enabled=true, loop=false,
                trigger=VnSceneComposerAudioTrigger.BeatStart,
                startBeatId=first.beatId,
                stopMode=VnSceneComposerAudioStopMode.Natural
            };
            var targetCue=new VnSceneComposerAdditionalAudioCue
            {
                assetGuid=TrackAGuid, enabled=true, loop=false,
                trigger=VnSceneComposerAudioTrigger.BeatStart,
                startBeatId=third.beatId,
                stopMode=VnSceneComposerAudioStopMode.Natural
            };
            scene.additionalAudioCues.Add(skippedCue);
            scene.additionalAudioCues.Add(targetCue);

            using(var c=new VnSceneComposerPlaybackController(Project(scene)))
            {
                c.PlayFromHereFromNeutralStart(0,2);
                Assert.That(c.CurrentBeatIndex,Is.EqualTo(2));
                Assert.That(c.GetAdditionalAudioCueStartCount(skippedCue.cueId),Is.EqualTo(0),
                    "Direct Beat start must not trigger skipped Beat audio.");
                Assert.That(c.GetAdditionalAudioCueStartCount(targetCue.cueId),Is.EqualTo(1));
            }
        }

        private static VnSceneComposerDialogueBeat AddBeat(VnSceneComposerScene scene)
        {
            var b=new VnSceneComposerDialogueBeat { text="beat" };
            scene.dialogueBeats.Add(b); return b;
        }

        private static VnSceneComposerScene SceneWithCharacters(params (string id,string state,VnWorkshopStageSlot slot)[] chars)
        {
            var s=new VnSceneComposerScene(); s.dialogueBeats[0].text="beat";
            foreach(var c in chars) s.characters.Add(new VnSceneComposerCharacter{characterId=c.id,stateId=c.state,stageSlot=c.slot});
            return s;
        }

        private static VnSceneComposerProject Project(params VnSceneComposerScene[] scenes)
        {
            var p=new VnSceneComposerProject(); p.scenes.Clear(); p.scenes.AddRange(scenes); return p;
        }

        private static object AddStaging(VnSceneComposerDialogueBeat beat,string character,string visibility,string position,bool stateOverride,string state,float delay)
        {
            Type type=RequireType("VnSceneComposerBeatCharacterStaging");
            object row=Activator.CreateInstance(type);
            Set(row,"characterId",character);
            SetEnum(row,"visibility",visibility);
            SetEnum(row,"position",position);
            Set(row,"hasStateOverride",stateOverride);
            Set(row,"stateId",state);
            Set(row,"delaySeconds",delay);
            IList rows=(IList)Field(beat,"characterStaging").GetValue(beat);
            rows.Add(row); return row;
        }

        private static VnWorkshopPreviewFrame Build(VnSceneComposerScene scene,VnSceneComposerDialogueBeat beat,float elapsed)
        {
            var p=Project(scene);
            MethodInfo method=typeof(VnSceneComposerComposition).GetMethod("BuildFrame",
                BindingFlags.Public|BindingFlags.Static,null,
                new[]{typeof(VnSceneComposerProject),typeof(VnSceneComposerScene),typeof(VnSceneComposerDialogueBeat),
                    typeof(VnWorkshopResolution),typeof(Texture2D),typeof(float)},null);
            Assert.That(method,Is.Not.Null,"Missing elapsed-aware staging BuildFrame overload.");
            return (VnWorkshopPreviewFrame)method.Invoke(null,new object[]{p,scene,beat,VnWorkshopResolution.Reference1920x1080,null,elapsed});
        }

        private static VnWorkshopPreviewCharacter Character(VnWorkshopPreviewFrame frame,string id)
        {
            var ch=(frame.ComposerCharacters??Array.Empty<VnWorkshopPreviewCharacter>())
                .FirstOrDefault(x=>x!=null&&string.Equals(x.CharacterId,id,StringComparison.OrdinalIgnoreCase));
            Assert.That(ch,Is.Not.Null,"Missing character "+id); return ch;
        }

        private static void AssertVisible(VnWorkshopPreviewFrame frame,string id,bool visible)
        {
            var ch=Character(frame,id);
            Assert.That(ch.Alpha>0.001f,Is.EqualTo(visible),"Visibility mismatch for "+id);
        }

        private static void WithVideo(Action<StagingVideoFactory,VnSceneComposerScene,VnSceneComposerPlaybackController> action)
        {
            var f=new StagingVideoFactory(); VnSceneComposerMediaEditing.VideoPreviewFactory=f;
            VnSceneComposerPlaybackController c=null;
            try
            {
                var s=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
                s.media=new VnSceneComposerMediaReference{kind=VnSceneComposerMediaKind.ExternalVideo,reference="staging.mp4",displayName="staging.mp4",contentHash="staging",loop=true};
                AddBeat(s); AddStaging(s.dialogueBeats[1],"Mina","KeepPrevious","Left",false,"",0f);
                c=new VnSceneComposerPlaybackController(Project(s)); action(f,s,c);
            }
            finally
            {
                c?.Dispose(); VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                foreach(var p in f.Created) if(p!=null&&p.texture!=null) p.Dispose();
            }
        }

        private static void SetMusic(VnSceneComposerScene scene)
        {
            string path=AssetDatabase.GUIDToAssetPath(TrackAGuid);
            AudioClip clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Assert.That(clip,Is.Not.Null);
            scene.music.mode=VnSceneComposerMusicMode.Track; scene.music.assetGuid=TrackAGuid;
            scene.music.displayName=clip.name; scene.music.volume=.5f; scene.music.loop=true;
        }

        private static Type RequireType(string shortName)
        {
            Type type=typeof(VnSceneComposerProject).Assembly.GetType("Rokas.EditorTools.VnUiWorkshop."+shortName,false);
            Assert.That(type,Is.Not.Null,"Missing Character Staging type: "+shortName); return type;
        }

        private static FieldInfo Field(object o,string name)
        {
            FieldInfo f=o.GetType().GetField(name,BindingFlags.Public|BindingFlags.Instance);
            Assert.That(f,Is.Not.Null,"Missing field: "+o.GetType().Name+"."+name); return f;
        }
        private static PropertyInfo Property(object o,string name)
        {
            PropertyInfo p=o.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
            Assert.That(p,Is.Not.Null,"Missing property: "+o.GetType().Name+"."+name); return p;
        }
        private static FieldInfo PrivateField(object o,string name)
        {
            FieldInfo f=o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance);
            Assert.That(f,Is.Not.Null,"Missing private field: "+name); return f;
        }
        private static void Set(object o,string name,object value){Field(o,name).SetValue(o,value);}
        private static T Get<T>(object o,string name){return (T)Field(o,name).GetValue(o);}
        private static void SetEnum(object o,string name,string value){FieldInfo f=Field(o,name);f.SetValue(o,Enum.Parse(f.FieldType,value));}
        private static object Invoke(object o,string name,params object[] args)
        {
            MethodInfo m=o.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
                .FirstOrDefault(x=>x.Name==name&&x.GetParameters().Length==args.Length);
            Assert.That(m,Is.Not.Null,"Missing method: "+o.GetType().Name+"."+name); return m.Invoke(o,args);
        }
    }
}
