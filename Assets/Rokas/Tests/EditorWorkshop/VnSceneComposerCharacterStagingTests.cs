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
                c.PlaySceneFromNeutralStart(0);
                c.Advance(10f); c.AdvanceDialogue();
                c.Advance(10f); c.AdvanceDialogue();
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

        [Test] public void MCS_33_UiExposesOneAuthoritativeBeatCharacterStagingSection()
        {
            string path=Path.Combine(Application.dataPath,"Rokas","Scripts","Editor","VnUiWorkshop","VnPresentationWorkshopWindow.SceneComposerCharacterStaging.cs");
            Assert.That(File.Exists(path),Is.True);
            string source=File.ReadAllText(path);
            Assert.That(source,Does.Contain("Персонажи текущей реплики"));
            Assert.That(source,Does.Contain("[наследуется]").And.Contain("[изменено здесь]"));
            Assert.That(source,Does.Contain("Сбросить изменения этой реплики"));
            Assert.That(source,Does.Contain("Оставить предыдущую").And.Contain("По умолчанию / Базовая"));
            Assert.That(source,Does.Not.Contain("+ Добавить персонажа"),
                "Beat staging must operate on the existing Scene cast rather than create Scene membership.");
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

        [Test] public void MCS_DirectDrag_RED_01_FreeBeatDragUpdatesCurrentBeatXY()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var second=AddBeat(scene);
            object row=AddStaging(second,"Mina","KeepPrevious","Custom",false,"",0f);
            Set(row,"customPositionOffset",new Vector2(10f,20f));
            VnPresentationWorkshopWindow w=DragWindow(scene,second,"Mina",row);
            try
            {
                Invoke(w,"ApplySceneComposerCharacterDrag",new Vector2(5f,-3f));
                AssertVector(Get<Vector2>(row,"customPositionOffset"),new Vector2(15f,17f));
                Assert.That(Get<object>(row,"position").ToString(),Is.EqualTo("Custom"));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_DirectDrag_RED_02_SceneBaseTransformRemainsUnchanged()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var second=AddBeat(scene);
            object row=AddStaging(second,"Mina","KeepPrevious","Custom",false,"",0f);
            VnSceneComposerCharacter baseCharacter=scene.characters[0];
            Vector2 before=baseCharacter.positionOffset;
            bool hadOffset=baseCharacter.hasPositionOffset;
            VnPresentationWorkshopWindow w=DragWindow(scene,second,"Mina",row);
            try
            {
                Invoke(w,"ApplySceneComposerCharacterDrag",new Vector2(25f,10f));
                Assert.That(baseCharacter.hasPositionOffset,Is.EqualTo(hadOffset));
                AssertVector(baseCharacter.positionOffset,before);
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_DirectDrag_RED_03_PreviousBeatRemainsUnchanged()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            object first=AddStaging(scene.dialogueBeats[0],"Mina","KeepPrevious","Custom",false,"",0f);
            Set(first,"customPositionOffset",new Vector2(-40f,12f));
            var second=AddBeat(scene);
            object current=AddStaging(second,"Mina","KeepPrevious","KeepPrevious",false,"",0f);
            VnPresentationWorkshopWindow w=DragWindow(scene,second,"Mina",current);
            try
            {
                Invoke(w,"ApplySceneComposerCharacterDrag",new Vector2(11f,-7f));
                Assert.That(Get<object>(first,"position").ToString(),Is.EqualTo("Custom"));
                AssertVector(Get<Vector2>(first,"customPositionOffset"),new Vector2(-40f,12f));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_DirectDrag_RED_04_KeepPreviousBecomesCurrentBeatCustomFromInheritedPosition()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            object first=AddStaging(scene.dialogueBeats[0],"Mina","KeepPrevious","Custom",false,"",0f);
            Set(first,"customPositionOffset",new Vector2(30f,10f));
            var second=AddBeat(scene);
            object current=AddStaging(second,"Mina","KeepPrevious","KeepPrevious",false,"",0f);
            VnPresentationWorkshopWindow w=DragWindow(scene,second,"Mina",current);
            try
            {
                Invoke(w,"ApplySceneComposerCharacterDrag",new Vector2(5f,-2f));
                Assert.That(Get<object>(current,"position").ToString(),Is.EqualTo("Custom"));
                AssertVector(Get<Vector2>(current,"customPositionOffset"),new Vector2(35f,8f));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [TestCase("Left")]
        [TestCase("Center")]
        [TestCase("Right")]
        public void MCS_DirectDrag_RED_05_07_PresetBecomesCustomWithoutVisualSnap(string preset)
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var second=AddBeat(scene);
            object row=AddStaging(second,"Mina","KeepPrevious",preset,false,"",0f);
            Rect before=Character(Build(scene,second,0f),"Mina").Body;
            Vector2 delta=new Vector2(18f,-9f);
            VnPresentationWorkshopWindow w=DragWindow(scene,second,"Mina",row);
            try
            {
                Invoke(w,"ApplySceneComposerCharacterDrag",delta);
                Rect after=Character(Build(scene,second,0f),"Mina").Body;
                Assert.That(Get<object>(row,"position").ToString(),Is.EqualTo("Custom"));
                AssertVector(after.center,before.center+delta);
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_DirectDrag_RED_08_NumericCustomXYEqualsDraggedXY()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var second=AddBeat(scene);
            object row=AddStaging(second,"Mina","KeepPrevious","Custom",false,"",0f);
            Set(row,"customPositionOffset",new Vector2(4f,6f));
            VnPresentationWorkshopWindow w=DragWindow(scene,second,"Mina",row);
            try
            {
                Invoke(w,"ApplySceneComposerCharacterDrag",new Vector2(12f,14f));
                AssertVector(Get<Vector2>(row,"customPositionOffset"),new Vector2(16f,20f));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_DirectDrag_RED_09_SecondVisibleCharacterRemainsUnchanged()
        {
            var scene=SceneWithCharacters(
                ("Mina","mina_neutral",VnWorkshopStageSlot.Left),
                ("Keiko","keiko_neutral",VnWorkshopStageSlot.Right));
            var second=AddBeat(scene);
            object mina=AddStaging(second,"Mina","KeepPrevious","KeepPrevious",false,"",0f);
            AddStaging(second,"Keiko","KeepPrevious","KeepPrevious",false,"",0f);
            Rect keikoBefore=Character(Build(scene,second,0f),"Keiko").Body;
            VnPresentationWorkshopWindow w=DragWindow(scene,second,"Mina",mina);
            try
            {
                Invoke(w,"ApplySceneComposerCharacterDrag",new Vector2(40f,0f));
                Rect keikoAfter=Character(Build(scene,second,0f),"Keiko").Body;
                Assert.That(keikoAfter,Is.EqualTo(keikoBefore));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_DirectDrag_10_PreviewResizeConvertsToSameReferenceDelta()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            VnWorkshopPreviewFrame frame=Build(scene,scene.dialogueBeats[0],0f);
            Rect small=new Rect(0f,0f,960f,540f);
            Rect large=new Rect(0f,0f,1920f,1080f);
            Vector2 smallA=VnPresentationWorkshopPreviewRenderer.PreviewToLogical(small,new Vector2(100f,100f),frame);
            Vector2 smallB=VnPresentationWorkshopPreviewRenderer.PreviewToLogical(small,new Vector2(150f,125f),frame);
            Vector2 largeA=VnPresentationWorkshopPreviewRenderer.PreviewToLogical(large,new Vector2(200f,200f),frame);
            Vector2 largeB=VnPresentationWorkshopPreviewRenderer.PreviewToLogical(large,new Vector2(300f,250f),frame);
            AssertVector(smallB-smallA,largeB-largeA);
        }

        [Test] public void MCS_DirectDrag_RED_11_UndoRestoresPriorModeAndXY()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var second=AddBeat(scene);
            object row=AddStaging(second,"Mina","KeepPrevious","Left",false,"",0f);
            string rowId=Get<string>(row,"stagingId");
            string sceneId=scene.sceneId;
            string beatId=second.beatId;
            VnPresentationWorkshopWindow w=DragWindow(scene,second,"Mina",row);
            try
            {
                Undo.ClearAll();
                Invoke(w,"RecordSceneComposerUndo","Move VN Scene Character");
                Invoke(w,"ApplySceneComposerCharacterDrag",new Vector2(20f,5f));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();

                VnSceneComposerProject restored=(VnSceneComposerProject)PrivateField(w,"_sceneComposerProject").GetValue(w);
                VnSceneComposerScene restoredScene=restored.scenes.First(x=>x.sceneId==sceneId);
                VnSceneComposerDialogueBeat restoredBeat=restoredScene.dialogueBeats.First(x=>x.beatId==beatId);
                object restoredRow=((IList)Field(restoredBeat,"characterStaging").GetValue(restoredBeat))
                    .Cast<object>().First(x=>Get<string>(x,"stagingId")==rowId);
                Assert.That(Get<object>(restoredRow,"position").ToString(),Is.EqualTo("Left"));
                AssertVector(Get<Vector2>(restoredRow,"customPositionOffset"),Vector2.zero);
            }
            finally { Undo.ClearAll(); UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_DirectDrag_RED_12_SaveReopenPreservesDraggedBeat()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var second=AddBeat(scene);
            object row=AddStaging(second,"Mina","KeepPrevious","Custom",false,"",0f);
            VnPresentationWorkshopWindow w=DragWindow(scene,second,"Mina",row);
            try
            {
                Invoke(w,"ApplySceneComposerCharacterDrag",new Vector2(21f,-13f));
                string json=VnSceneComposerSerialization.SerializePortable(
                    (VnSceneComposerProject)PrivateField(w,"_sceneComposerProject").GetValue(w));
                VnSceneComposerImportResult loaded=VnSceneComposerSerialization.DeserializePortable(json);
                Assert.That(loaded.Success,Is.True,loaded.Error);
                object loadedRow=((IList)Field(loaded.Project.scenes[0].dialogueBeats[1],"characterStaging")
                    .GetValue(loaded.Project.scenes[0].dialogueBeats[1]))[0];
                Assert.That(Get<object>(loadedRow,"position").ToString(),Is.EqualTo("Custom"));
                AssertVector(Get<Vector2>(loadedRow,"customPositionOffset"),new Vector2(21f,-13f));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_DirectDrag_RED_13_PreviousReconstructsUndraggedPriorBeat()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            object first=AddStaging(scene.dialogueBeats[0],"Mina","KeepPrevious","Left",false,"",0f);
            var second=AddBeat(scene);
            object row=AddStaging(second,"Mina","KeepPrevious","KeepPrevious",false,"",0f);
            VnPresentationWorkshopWindow w=DragWindow(scene,second,"Mina",row);
            try
            {
                Invoke(w,"ApplySceneComposerCharacterDrag",new Vector2(30f,0f));
                var p=(VnSceneComposerProject)PrivateField(w,"_sceneComposerProject").GetValue(w);
                using(var playback=new VnSceneComposerPlaybackController(p))
                {
                    playback.PlayFromHereFromNeutralStart(0,1);
                    playback.PreviousDialogue();
                    Assert.That(playback.CurrentBeatIndex,Is.EqualTo(0));
                    Assert.That(Character(playback.CurrentFrame.WorkshopFrame,"Mina").Slot,
                        Is.EqualTo(VnWorkshopStageSlot.Left));
                }
                Assert.That(Get<object>(first,"position").ToString(),Is.EqualTo("Left"));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_DirectDrag_RED_14_PlayFromHereReconstructsDraggedBeat()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var second=AddBeat(scene);
            object row=AddStaging(second,"Mina","KeepPrevious","Custom",false,"",0f);
            VnPresentationWorkshopWindow w=DragWindow(scene,second,"Mina",row);
            try
            {
                Invoke(w,"ApplySceneComposerCharacterDrag",new Vector2(26f,-8f));
                Rect expected=Character(Build(scene,second,0f),"Mina").Body;
                using(var playback=new VnSceneComposerPlaybackController(
                    (VnSceneComposerProject)PrivateField(w,"_sceneComposerProject").GetValue(w)))
                {
                    playback.PlayFromHereFromNeutralStart(0,1);
                    Assert.That(Character(playback.CurrentFrame.WorkshopFrame,"Mina").Body,
                        Is.EqualTo(expected));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_DirectDrag_RED_15_StaticPreviewAndPlaybackResolveSameDraggedPosition()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var second=AddBeat(scene);
            object row=AddStaging(second,"Mina","KeepPrevious","Custom",false,"",0f);
            VnPresentationWorkshopWindow w=DragWindow(scene,second,"Mina",row);
            try
            {
                Invoke(w,"ApplySceneComposerCharacterDrag",new Vector2(-17f,22f));
                Rect preview=Character(Build(scene,second,0f),"Mina").Body;
                using(var playback=new VnSceneComposerPlaybackController(
                    (VnSceneComposerProject)PrivateField(w,"_sceneComposerProject").GetValue(w)))
                {
                    playback.PlayFromHereFromNeutralStart(0,1);
                    Assert.That(Character(playback.CurrentFrame.WorkshopFrame,"Mina").Body,
                        Is.EqualTo(preview));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_DirectDrag_RED_16_DragDoesNotRecreatePlaybackOrRestartBgm()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            SetMusic(scene);
            object row=AddStaging(scene.dialogueBeats[0],"Mina","KeepPrevious","Custom",false,"",0f);
            VnPresentationWorkshopWindow w=DragWindow(scene,scene.dialogueBeats[0],"Mina",row);
            try
            {
                w.ComposerPlayScene();
                object before=PrivateField(w,"_sceneComposerPlayback").GetValue(w);
                Assert.That(before,Is.Not.Null);
                int starts=((VnSceneComposerPlaybackController)before).MusicStartCount;
                Invoke(w,"ApplySceneComposerCharacterDrag",new Vector2(9f,4f));
                object after=PrivateField(w,"_sceneComposerPlayback").GetValue(w);
                Assert.That(after,Is.SameAs(before),
                    "Direct Beat staging drag must not reset the environment playback object.");
                Assert.That(((VnSceneComposerPlaybackController)after).MusicStartCount,Is.EqualTo(starts));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_DirectDrag_RED_17_SelectedStagingRowWinsOverlappingCharacterHitTest()
        {
            var scene=SceneWithCharacters(
                ("Mina","mina_neutral",VnWorkshopStageSlot.Center),
                ("Keiko","keiko_neutral",VnWorkshopStageSlot.Center));
            object mina=AddStaging(scene.dialogueBeats[0],"Mina","KeepPrevious","Center",false,"",0f);
            AddStaging(scene.dialogueBeats[0],"Keiko","KeepPrevious","Center",false,"",0f);
            VnPresentationWorkshopWindow w=DragWindow(scene,scene.dialogueBeats[0],"Mina",mina);
            try
            {
                VnWorkshopPreviewFrame frame=(VnWorkshopPreviewFrame)Invoke(w,"ComposerBuildSelectedPreviewFrame");
                Rect a=Character(frame,"Mina").Body;
                Rect b=Character(frame,"Keiko").Body;
                Rect overlap=Rect.MinMaxRect(
                    Mathf.Max(a.xMin,b.xMin),Mathf.Max(a.yMin,b.yMin),
                    Mathf.Min(a.xMax,b.xMax),Mathf.Min(a.yMax,b.yMax));
                Assert.That(overlap.width,Is.GreaterThan(0f));
                Assert.That(overlap.height,Is.GreaterThan(0f));

                Assert.That(w.ComposerSelectPreviewObjectAt(overlap.center),Is.True);
                Assert.That(w.ComposerGetSelectedCharacterIndex(),Is.EqualTo(0),
                    "When characters overlap, the explicitly selected staging row owns the hit.");
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_DirectDrag_RED_18_PreviewCharacterDragUsesOneAuthoritativeHotControl()
        {
            string path=Path.Combine(Application.dataPath,"Rokas","Scripts","Editor","VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string source=File.ReadAllText(path);
            Assert.That(source,Does.Contain("GUIUtility.hotControl")
                .And.Contain("_sceneComposerPreviewDragControlId"));
        }


        [Test] public void MCS_56_TextInspectorUsesThreeCompactFoldoutSections()
        {
            string source=File.ReadAllText(Path.Combine(Application.dataPath,"Rokas","Scripts","Editor","VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposer.cs"));
            Assert.That(source,Does.Contain("_sceneComposerTextDialogueExpanded").And
                .Contain("_sceneComposerTextStagingExpanded").And
                .Contain("_sceneComposerTextPresentationExpanded"));
            Assert.That(source,Does.Contain("Реплика").And
                .Contain("Персонажи и постановка").And
                .Contain("Оформление диалога"));
            Assert.That(source,Does.Not.Contain("Состояние персонажа в этой реплике"));
        }

        [Test] public void MCS_57_TextContainsOnlyOnePoseAndAnimationEditor()
        {
            string main=File.ReadAllText(Path.Combine(Application.dataPath,"Rokas","Scripts","Editor","VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposer.cs"));
            string staging=File.ReadAllText(Path.Combine(Application.dataPath,"Rokas","Scripts","Editor","VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposerCharacterStaging.cs"));
            Assert.That(CountOccurrences(main,"\"Эмоция / поза\"")+
                        CountOccurrences(staging,"\"Эмоция / поза\""),Is.EqualTo(1));
            Assert.That(CountOccurrences(main,"\"Анимация реплики\"")+
                        CountOccurrences(staging,"\"Анимация реплики\""),Is.EqualTo(1));
            Assert.That(main,Does.Not.Contain("Персонаж реплики"));
        }

        [Test] public void MCS_58_TextStagingRowsComeFromSceneCastAndSceneCastStillOwnsAdd()
        {
            string staging=File.ReadAllText(Path.Combine(Application.dataPath,"Rokas","Scripts","Editor","VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposerCharacterStaging.cs"));
            string main=File.ReadAllText(Path.Combine(Application.dataPath,"Rokas","Scripts","Editor","VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposer.cs"));
            Assert.That(staging,Does.Contain("GetSceneComposerBeatTargetCharacterIds(scene)"));
            Assert.That(staging,Does.Not.Contain("+ Добавить персонажа"));
            Assert.That(main,Does.Contain("+ Добавить персонажа"),
                "Scene membership remains in the Scene Characters inspector.");
        }

        [Test] public void MCS_59_LegacyBeatStateMigratesIntoAuthoritativeStagingRow()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var beat=scene.dialogueBeats[0];
            beat.targetCharacterId="Mina";
            beat.hasStateOverride=true;
            beat.stateId="mina_happy";
            var result=VnSceneComposerSerialization.DeserializePortable(
                VnSceneComposerSerialization.SerializePortable(Project(scene)));
            Assert.That(result.Success,Is.True,result.Error);
            var migrated=result.Project.scenes[0].dialogueBeats[0];
            Assert.That(migrated.characterStaging,Has.Count.EqualTo(1));
            Assert.That(migrated.characterStaging[0].characterId,Is.EqualTo("Mina"));
            Assert.That(migrated.characterStaging[0].hasStateOverride,Is.True);
            Assert.That(migrated.characterStaging[0].stateId,Is.EqualTo("mina_happy"));
            Assert.That(migrated.targetCharacterId,Is.Empty);
            Assert.That(migrated.hasStateOverride,Is.False);
            Assert.That(migrated.stateId,Is.Empty);
        }

        [Test] public void MCS_60_LegacyAnimationMigratesWithoutOverwritingNewerStaging()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var beat=scene.dialogueBeats[0];
            beat.targetCharacterId="Mina";
            beat.effect=VnSceneComposerBeatEffect.Accent;
            beat.effectStrength=31f;
            beat.effectDuration=.7f;
            object row=AddStaging(beat,"Mina","KeepPrevious","KeepPrevious",false,"",0f);
            Set(row,"effect",VnSceneComposerBeatEffect.Hop);
            Set(row,"effectStrength",44f);
            Set(row,"effectDuration",.4f);

            var result=VnSceneComposerSerialization.DeserializePortable(
                VnSceneComposerSerialization.SerializePortable(Project(scene)));
            Assert.That(result.Success,Is.True,result.Error);
            var migrated=result.Project.scenes[0].dialogueBeats[0];
            Assert.That(migrated.characterStaging,Has.Count.EqualTo(1));
            Assert.That(migrated.characterStaging[0].effect,Is.EqualTo(VnSceneComposerBeatEffect.Hop));
            Assert.That(migrated.characterStaging[0].effectStrength,Is.EqualTo(44f).Within(.001f));
            Assert.That(migrated.effect,Is.EqualTo(VnSceneComposerBeatEffect.None));
            Assert.That(migrated.targetCharacterId,Is.Empty);
        }

        [Test] public void MCS_61_LegacyMigrationPreservesVisibleCharacterState()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var beat=scene.dialogueBeats[0];
            beat.targetCharacterId="Mina";
            beat.hasStateOverride=true;
            beat.stateId="mina_happy";
            string before=Character(Build(scene,beat,0f),"Mina").StateId;

            var result=VnSceneComposerSerialization.DeserializePortable(
                VnSceneComposerSerialization.SerializePortable(Project(scene)));
            Assert.That(result.Success,Is.True,result.Error);
            var migratedScene=result.Project.scenes[0];
            string after=Character(Build(migratedScene,migratedScene.dialogueBeats[0],0f),"Mina").StateId;
            Assert.That(after,Is.EqualTo(before));
        }

        [Test] public void MCS_62_SilentCharacterCanBeSelectedWithoutChangingSpeaker()
        {
            var scene=SceneWithCharacters(
                ("Keiko","keiko_neutral",VnWorkshopStageSlot.Left),
                ("Mina","mina_neutral",VnWorkshopStageSlot.Right));
            scene.dialogueBeats[0].speaker="Keiko";
            var w=DragWindow(scene,scene.dialogueBeats[0],"Keiko",null);
            try
            {
                Invoke(w,"ComposerSelectDialogueBeatStagingCharacter","Mina");
                Assert.That(w.ComposerGetSelectedCharacterIndex(),Is.EqualTo(1));
                Assert.That(scene.dialogueBeats[0].speaker,Is.EqualTo("Keiko"));
                Assert.That(scene.dialogueBeats[0].characterStaging,Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_63_FirstEditMaterializesExactlyOneCurrentBeatRow()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var beat=scene.dialogueBeats[0];
            var w=DragWindow(scene,beat,"Mina",null);
            try
            {
                Invoke(w,"ComposerSelectDialogueBeatStagingCharacter","Mina");
                w.ComposerSetSelectedDialogueBeatCharacterStaging(
                    "Mina",VnSceneComposerBeatCharacterVisibility.Show,
                    VnSceneComposerBeatCharacterPosition.Left,Vector2.zero,
                    false,string.Empty,VnSceneComposerBeatEffect.None,18f,.28f,.2f);
                Assert.That(beat.characterStaging,Has.Count.EqualTo(1));
                Assert.That(beat.characterStaging[0].characterId,Is.EqualTo("Mina"));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_64_FirstEditLeavesPreviousBeatUntouched()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var second=AddBeat(scene);
            var w=DragWindow(scene,second,"Mina",null);
            try
            {
                Invoke(w,"ComposerSelectDialogueBeatStagingCharacter","Mina");
                w.ComposerSetSelectedDialogueBeatCharacterStaging(
                    "Mina",VnSceneComposerBeatCharacterVisibility.KeepPrevious,
                    VnSceneComposerBeatCharacterPosition.Right,Vector2.zero,
                    false,string.Empty,VnSceneComposerBeatEffect.None,18f,.28f,0f);
                Assert.That(scene.dialogueBeats[0].characterStaging,Is.Empty);
                Assert.That(second.characterStaging,Has.Count.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_65_PoseAnimationAndDelayModifyTheSameStagingRow()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            var beat=scene.dialogueBeats[0];
            var w=DragWindow(scene,beat,"Mina",null);
            try
            {
                Invoke(w,"ComposerSelectDialogueBeatStagingCharacter","Mina");
                w.ComposerSetSelectedDialogueBeatCharacterStaging(
                    "Mina",VnSceneComposerBeatCharacterVisibility.Show,
                    VnSceneComposerBeatCharacterPosition.Left,Vector2.zero,
                    true,"mina_happy",VnSceneComposerBeatEffect.None,18f,.28f,0f);
                string id=beat.characterStaging[0].stagingId;
                w.ComposerSetSelectedDialogueBeatCharacterStaging(
                    "Mina",VnSceneComposerBeatCharacterVisibility.Show,
                    VnSceneComposerBeatCharacterPosition.Left,Vector2.zero,
                    true,"mina_happy",VnSceneComposerBeatEffect.Hop,42f,.5f,.75f);
                Assert.That(beat.characterStaging,Has.Count.EqualTo(1));
                Assert.That(beat.characterStaging[0].stagingId,Is.EqualTo(id));
                Assert.That(beat.characterStaging[0].stateId,Is.EqualTo("mina_happy"));
                Assert.That(beat.characterStaging[0].effect,Is.EqualTo(VnSceneComposerBeatEffect.Hop));
                Assert.That(beat.characterStaging[0].delaySeconds,Is.EqualTo(.75f).Within(.001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_66_PreviewClickSelectsSilentCharacterAndKeepsSpeaker()
        {
            var scene=SceneWithCharacters(
                ("Keiko","keiko_neutral",VnWorkshopStageSlot.Left),
                ("Mina","mina_neutral",VnWorkshopStageSlot.Right));
            scene.dialogueBeats[0].speaker="Keiko";
            var w=DragWindow(scene,scene.dialogueBeats[0],"Keiko",null);
            try
            {
                VnWorkshopPreviewFrame frame=(VnWorkshopPreviewFrame)Invoke(w,"ComposerBuildSelectedPreviewFrame");
                Vector2 point=Character(frame,"Mina").Body.center;
                Assert.That(w.ComposerSelectPreviewObjectAt(point),Is.True);
                Assert.That(w.ComposerGetSelectedCharacterIndex(),Is.EqualTo(1));
                Assert.That(scene.dialogueBeats[0].speaker,Is.EqualTo("Keiko"));
                Assert.That(scene.dialogueBeats[0].characterStaging,Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_67_DraggingInheritedCharacterMaterializesLocalOverride()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            scene.dialogueBeats[0].speaker="Keiko";
            var beat=scene.dialogueBeats[0];
            var w=DragWindow(scene,beat,"Mina",null);
            try
            {
                Invoke(w,"ComposerSelectDialogueBeatStagingCharacter","Mina");
                Invoke(w,"ApplySceneComposerCharacterDrag",new Vector2(24f,-11f));
                Assert.That(beat.characterStaging,Has.Count.EqualTo(1));
                Assert.That(beat.characterStaging[0].position,
                    Is.EqualTo(VnSceneComposerBeatCharacterPosition.Custom));
                Assert.That(scene.dialogueBeats[0].speaker,Is.EqualTo("Keiko"));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MCS_68_PosePngShortcutWritesTheAuthoritativeStagingRow()
        {
            string source=File.ReadAllText(Path.Combine(Application.dataPath,"Rokas","Scripts","Editor","VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposerPoseImport.cs"));
            Assert.That(source,Does.Contain("ComposerSetSelectedDialogueBeatCharacterStaging"));
            Assert.That(source,Does.Not.Contain("ComposerSetSelectedDialogueBeatCharacterState("));
        }

        [Test] public void MCS_69_TextFoldoutsAreEditorWindowStateNotProjectData()
        {
            string window=File.ReadAllText(Path.Combine(Application.dataPath,"Rokas","Scripts","Editor","VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposer.cs"));
            string types=File.ReadAllText(Path.Combine(Application.dataPath,"Rokas","Scripts","Editor","VnUiWorkshop",
                "VnSceneComposerTypes.cs"));
            Assert.That(window,Does.Contain("[SerializeField] private bool _sceneComposerTextDialogueExpanded = true;"));
            Assert.That(window,Does.Contain("[SerializeField] private bool _sceneComposerTextStagingExpanded = true;"));
            Assert.That(window,Does.Contain("[SerializeField] private bool _sceneComposerTextPresentationExpanded;"));
            Assert.That(types,Does.Not.Contain("_sceneComposerTextDialogueExpanded")
                .And.Not.Contain("_sceneComposerTextStagingExpanded")
                .And.Not.Contain("_sceneComposerTextPresentationExpanded"));
        }

        [Test] public void MCS_70_ResetCurrentBeatChangesDoesNotRemoveSceneCharacter()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            object row=AddStaging(scene.dialogueBeats[0],"Mina","Show","Left",false,"",0f);
            var w=DragWindow(scene,scene.dialogueBeats[0],"Mina",row);
            try
            {
                Invoke(w,"ComposerResetSelectedDialogueBeatCharacterStaging");
                Assert.That(scene.characters,Has.Count.EqualTo(1));
                Assert.That(scene.dialogueBeats[0].characterStaging,Is.Empty);
                Assert.That(w.ComposerGetSelectedCharacterIndex(),Is.EqualTo(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        private static VnPresentationWorkshopWindow DragWindow(
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            string characterId,
            object stagingRow)
        {
            var w=ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            var project=Project(scene);
            PrivateField(w,"_sceneComposerProject").SetValue(w,project);
            PrivateField(w,"_sceneComposerSelectedSceneId").SetValue(w,scene.sceneId);
            PrivateField(w,"_sceneComposerSelectedDialogueBeatId").SetValue(w,beat.beatId);
            int index=scene.characters.FindIndex(c=>c!=null&&
                string.Equals(c.characterId,characterId,StringComparison.OrdinalIgnoreCase));
            Assert.That(index,Is.GreaterThanOrEqualTo(0));
            PrivateField(w,"_sceneComposerSelectedCharacterIndex").SetValue(w,index);
            if(stagingRow!=null)
                PrivateField(w,"_sceneComposerSelectedCharacterStagingId").SetValue(
                    w,Get<string>(stagingRow,"stagingId"));
            return w;
        }

        [Test] public void MOV_01_ExitRightMovesAndFadesWithoutTeleport()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            VnSceneComposerDialogueBeat beat=scene.dialogueBeats[0];
            beat.movement.primary.characterId="Mina";
            beat.movement.primary.action=VnSceneComposerMovementActionType.ExitRight;
            beat.movement.primary.duration=1f;
            using(var c=new VnSceneComposerPlaybackController(Project(scene)))
            {
                c.PlaySceneFromNeutralStart(0);
                VnWorkshopPreviewCharacter start=Character(c.CurrentFrame.WorkshopFrame,"Mina");
                float startX=start.Body.center.x;
                c.Advance(.5f);
                VnWorkshopPreviewCharacter mid=Character(c.CurrentFrame.WorkshopFrame,"Mina");
                Assert.That(mid.Body.center.x,Is.GreaterThan(startX));
                Assert.That(mid.Alpha,Is.GreaterThan(0f).And.LessThan(1f));
                c.Advance(.6f);
                Assert.That(Character(c.CurrentFrame.WorkshopFrame,"Mina").Alpha,Is.EqualTo(0f).Within(.001f));
            }
        }

        [Test] public void MOV_02_DisappearFadesWithoutTranslation()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            VnSceneComposerDialogueBeat beat=scene.dialogueBeats[0];
            beat.movement.primary.characterId="Mina";
            beat.movement.primary.action=VnSceneComposerMovementActionType.Disappear;
            beat.movement.primary.duration=1f;
            using(var c=new VnSceneComposerPlaybackController(Project(scene)))
            {
                c.PlaySceneFromNeutralStart(0);
                Vector2 start=Character(c.CurrentFrame.WorkshopFrame,"Mina").Body.center;
                c.Advance(.5f);
                VnWorkshopPreviewCharacter mid=Character(c.CurrentFrame.WorkshopFrame,"Mina");
                AssertVector(mid.Body.center,start);
                Assert.That(mid.Alpha,Is.GreaterThan(0f).And.LessThan(1f));
            }
        }

        [Test] public void MOV_03_MoveCenterPersistsIntoFollowingBeat()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Left));
            VnSceneComposerDialogueBeat first=scene.dialogueBeats[0];
            first.movement.primary.characterId="Mina";
            first.movement.primary.action=VnSceneComposerMovementActionType.MoveCenter;
            first.movement.primary.duration=.5f;
            AddBeat(scene);
            using(var c=new VnSceneComposerPlaybackController(Project(scene)))
            {
                c.PlaySceneFromNeutralStart(0);
                c.Advance(10f);
                float settled=Character(c.CurrentFrame.WorkshopFrame,"Mina").Body.center.x;
                c.AdvanceDialogue();
                Assert.That(c.CurrentBeatIndex,Is.EqualTo(1));
                Assert.That(Character(c.CurrentFrame.WorkshopFrame,"Mina").Body.center.x,
                    Is.EqualTo(settled).Within(.01f));
                Assert.That(Character(c.CurrentFrame.WorkshopFrame,"Mina").Slot,
                    Is.EqualTo(VnWorkshopStageSlot.Center));
            }
        }

        [Test] public void MOV_04_ExitPersistsAbsentUntilExplicitRestore()
        {
            var scene=SceneWithCharacters(("Mina","mina_neutral",VnWorkshopStageSlot.Center));
            VnSceneComposerDialogueBeat first=scene.dialogueBeats[0];
            first.movement.primary.characterId="Mina";
            first.movement.primary.action=VnSceneComposerMovementActionType.ExitLeft;
            first.movement.primary.duration=.5f;
            VnSceneComposerDialogueBeat second=AddBeat(scene);
            using(var c=new VnSceneComposerPlaybackController(Project(scene)))
            {
                c.PlaySceneFromNeutralStart(0);
                c.Advance(10f);
                c.AdvanceDialogue();
                Assert.That(c.CurrentBeatIndex,Is.EqualTo(1));
                Assert.That(Character(c.CurrentFrame.WorkshopFrame,"Mina").Alpha,Is.EqualTo(0f).Within(.001f));

                second.movement.primary.characterId="Mina";
                second.movement.primary.action=VnSceneComposerMovementActionType.MoveCenter;
                second.movement.primary.duration=.5f;
                c.RefreshCurrentFrame();
                c.Advance(.6f);
                Assert.That(Character(c.CurrentFrame.WorkshopFrame,"Mina").Alpha,Is.EqualTo(1f).Within(.001f));
                Assert.That(Character(c.CurrentFrame.WorkshopFrame,"Mina").Slot,
                    Is.EqualTo(VnWorkshopStageSlot.Center));
            }
        }

        [Test] public void MOV_05_SecondarySimultaneousMovesBothCharacters()
        {
            var scene=SceneWithCharacters(
                ("Mina","mina_neutral",VnWorkshopStageSlot.Left),
                ("Keiko","keiko_neutral",VnWorkshopStageSlot.Right));
            VnSceneComposerDialogueBeat beat=scene.dialogueBeats[0];
            beat.movement.primary.characterId="Mina";
            beat.movement.primary.action=VnSceneComposerMovementActionType.MoveCenter;
            beat.movement.primary.duration=1f;
            beat.movement.secondary.characterId="Keiko";
            beat.movement.secondary.action=VnSceneComposerMovementActionType.MoveCenter;
            beat.movement.secondary.duration=1f;
            beat.movement.secondaryTiming=VnSceneComposerMovementTiming.Simultaneous;
            using(var c=new VnSceneComposerPlaybackController(Project(scene)))
            {
                c.PlaySceneFromNeutralStart(0);
                float minaStart=Character(c.CurrentFrame.WorkshopFrame,"Mina").Body.center.x;
                float keikoStart=Character(c.CurrentFrame.WorkshopFrame,"Keiko").Body.center.x;
                c.Advance(.5f);
                Assert.That(Character(c.CurrentFrame.WorkshopFrame,"Mina").Body.center.x,Is.GreaterThan(minaStart));
                Assert.That(Character(c.CurrentFrame.WorkshopFrame,"Keiko").Body.center.x,Is.LessThan(keikoStart));
            }
        }

        [Test] public void MOV_06_SecondaryAfterPrimaryWaitsForPrimaryDuration()
        {
            var scene=SceneWithCharacters(
                ("Mina","mina_neutral",VnWorkshopStageSlot.Left),
                ("Keiko","keiko_neutral",VnWorkshopStageSlot.Right));
            VnSceneComposerDialogueBeat beat=scene.dialogueBeats[0];
            beat.movement.primary.characterId="Mina";
            beat.movement.primary.action=VnSceneComposerMovementActionType.MoveCenter;
            beat.movement.primary.duration=1f;
            beat.movement.secondary.characterId="Keiko";
            beat.movement.secondary.action=VnSceneComposerMovementActionType.MoveCenter;
            beat.movement.secondary.duration=1f;
            beat.movement.secondaryTiming=VnSceneComposerMovementTiming.AfterPrimary;
            using(var c=new VnSceneComposerPlaybackController(Project(scene)))
            {
                c.PlaySceneFromNeutralStart(0);
                float keikoStart=Character(c.CurrentFrame.WorkshopFrame,"Keiko").Body.center.x;
                c.Advance(.5f);
                Assert.That(Character(c.CurrentFrame.WorkshopFrame,"Keiko").Body.center.x,
                    Is.EqualTo(keikoStart).Within(.01f));
                c.Advance(1f);
                Assert.That(Character(c.CurrentFrame.WorkshopFrame,"Keiko").Body.center.x,
                    Is.LessThan(keikoStart));
            }
        }

        [Test] public void MOV_07_SpeakerFocusStillOwnsBrightnessDuringMovement()
        {
            var scene=SceneWithCharacters(
                ("Mina","mina_neutral",VnWorkshopStageSlot.Left),
                ("Keiko","keiko_neutral",VnWorkshopStageSlot.Right));
            VnSceneComposerDialogueBeat beat=scene.dialogueBeats[0];
            beat.speaker="Mina";
            beat.movement.primary.characterId="Mina";
            beat.movement.primary.action=VnSceneComposerMovementActionType.MoveCenter;
            beat.movement.primary.duration=1f;
            using(var c=new VnSceneComposerPlaybackController(Project(scene)))
            {
                c.PlaySceneFromNeutralStart(0);
                c.Advance(.5f);
                VnWorkshopPreviewCharacter mina=Character(c.CurrentFrame.WorkshopFrame,"Mina");
                VnWorkshopPreviewCharacter keiko=Character(c.CurrentFrame.WorkshopFrame,"Keiko");
                Assert.That(mina.Brightness,Is.GreaterThan(keiko.Brightness));
            }
        }

        private static int CountOccurrences(string source,string needle)
        {
            if(string.IsNullOrEmpty(source)||string.IsNullOrEmpty(needle)) return 0;
            int count=0,index=0;
            while((index=source.IndexOf(needle,index,StringComparison.Ordinal))>=0)
            {
                count++; index+=needle.Length;
            }
            return count;
        }

        private static void AssertVector(Vector2 actual,Vector2 expected)
        {
            Assert.That(actual.x,Is.EqualTo(expected.x).Within(.01f));
            Assert.That(actual.y,Is.EqualTo(expected.y).Within(.01f));
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
