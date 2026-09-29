using System.IO;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rokas.EditorTools.Tests
{
    public sealed class HubDialogueWorkshopTests
    {
        [Test]
        public void WorkshopBuild_CreatesEditableRuntimeParityElements()
        {
            Scene scene =
                HubDialogueWorkshopBuilder.CreateUnsavedWorkshopForTests();
            GameObject[] roots = scene.GetRootGameObjects();
            GameObject workshop = null;
            for (int i = 0; i < roots.Length; i++)
                if (roots[i].name == "HubDialogueWorkshop")
                    workshop = roots[i];

            Assert.That(workshop, Is.Not.Null);
            Transform speakerTransform =
                HubDialogueWorkshopBuilder.FindTransform(
                    workshop.transform,
                    "SpeakerName");
            Transform portrait =
                HubDialogueWorkshopBuilder.FindTransform(
                    workshop.transform,
                    "PortraitImage");
            Transform arrow =
                HubDialogueWorkshopBuilder.FindTransform(
                    workshop.transform,
                    "CompletionArrow");
            Assert.That(speakerTransform, Is.Not.Null);
            Assert.That(portrait, Is.Not.Null);
            Assert.That(arrow, Is.Not.Null);
            Assert.That(
                speakerTransform.GetComponent<Text>().text,
                Is.EqualTo("Keiko"));
        }

        [Test]
        public void Capture_PreservesPositionSizeRotationAndTypography()
        {
            Scene scene =
                HubDialogueWorkshopBuilder.CreateUnsavedWorkshopForTests();
            GameObject root =
                scene.GetRootGameObjects()[0];
            RectTransform speaker =
                (RectTransform)
                HubDialogueWorkshopBuilder.FindTransform(
                    root.transform,
                    "SpeakerName");
            Text speakerText =
                speaker.GetComponent<Text>();

            speaker.anchoredPosition =
                new Vector2(271f, -166f);
            speaker.sizeDelta =
                new Vector2(444f, 48f);
            speaker.anchoredPosition3D =
                new Vector3(271f, -166f, 4f);
            speaker.localEulerAngles =
                new Vector3(2f, -1f, 3.5f);
            speaker.localScale =
                new Vector3(1.25f, .75f, 1.1f);
            speaker.anchorMin =
                new Vector2(.2f, .3f);
            speaker.anchorMax =
                new Vector2(.7f, .8f);
            speaker.pivot =
                new Vector2(.25f, .65f);

            speakerText.text = "Keiko";
            speakerText.fontSize = 24;
            speakerText.alignment =
                TextAnchor.MiddleCenter;
            speakerText.color =
                new Color(.2f, .3f, .4f, .8f);

            HubDialogueConfigData data =
                HubDialogueWorkshopBuilder.CaptureForTests(
                    scene,
                    new HubDialogueConfigData());

            Assert.That(data.speaker.x, Is.EqualTo(271f));
            Assert.That(data.speaker.y, Is.EqualTo(166f));
            Assert.That(data.speaker.width, Is.EqualTo(444f));
            Assert.That(data.speaker.height, Is.EqualTo(48f));
            Assert.That(
                data.speaker.anchoredZ,
                Is.EqualTo(4f).Within(.01f));
            Assert.That(
                data.speaker.rotationX,
                Is.EqualTo(2f).Within(.01f));
            Assert.That(
                data.speaker.rotationY,
                Is.EqualTo(-1f).Within(.01f));
            Assert.That(
                data.speaker.rotationZ,
                Is.EqualTo(3.5f).Within(.01f));
            Assert.That(
                data.speaker.scaleX,
                Is.EqualTo(1.25f).Within(.01f));
            Assert.That(
                data.speaker.scaleY,
                Is.EqualTo(.75f).Within(.01f));
            Assert.That(
                data.speaker.scaleZ,
                Is.EqualTo(1.1f).Within(.01f));
            Assert.That(
                data.speaker.anchorMinX,
                Is.EqualTo(.2f).Within(.01f));
            Assert.That(
                data.speaker.anchorMinY,
                Is.EqualTo(.3f).Within(.01f));
            Assert.That(
                data.speaker.anchorMaxX,
                Is.EqualTo(.7f).Within(.01f));
            Assert.That(
                data.speaker.anchorMaxY,
                Is.EqualTo(.8f).Within(.01f));
            Assert.That(
                data.speaker.pivotX,
                Is.EqualTo(.25f).Within(.01f));
            Assert.That(
                data.speaker.pivotY,
                Is.EqualTo(.65f).Within(.01f));
            Assert.That(
                data.speaker.colorR,
                Is.EqualTo(.2f).Within(.01f));
            Assert.That(
                data.speaker.colorA,
                Is.EqualTo(.8f).Within(.01f));
            Assert.That(data.speakerName, Is.EqualTo("Keiko"));
            Assert.That(data.speakerFontSize, Is.EqualTo(24));
            Assert.That(
                data.speakerAlignment,
                Is.EqualTo(TextAnchor.MiddleCenter));
        }

        [Test]
        public void SaveReload_RuntimeReadsSameAuthoredConfig()
        {
            string projectRoot =
                Directory.GetParent(Application.dataPath).FullName;
            string absolute = Path.Combine(
                projectRoot,
                HubDialogueWorkshopBuilder.UserConfigPath);
            string metaAbsolute = absolute + ".meta";
            bool hadConfig = File.Exists(absolute);
            bool hadMeta = File.Exists(metaAbsolute);
            string previousConfig =
                hadConfig ? File.ReadAllText(absolute) : null;
            string previousMeta =
                hadMeta ? File.ReadAllText(metaAbsolute) : null;

            try
            {
                HubDialogueConfigData authored =
                    HubDialogueConfig.NormalizeForAuthoring(
                        new HubDialogueConfigData());
                authored.speakerName = "Keiko";
                authored.guildIntroText =
                    "Workshop persistence probe";
                authored.speaker.x = 333f;
                authored.speaker.y = 177f;
                authored.speaker.scaleX = 1.31f;
                authored.speaker.scaleY = .82f;
                authored.speaker.anchorMinX = .12f;
                authored.speaker.pivotY = .67f;
                authored.voiceVolume = .27f;

                HubDialogueWorkshopBuilder.WriteUserConfig(authored);
                HubDialogueConfig.ResetCache();
                HubDialogueConfigData runtime =
                    HubDialogueConfig.LoadFresh();

                Assert.That(
                    runtime.speakerName,
                    Is.EqualTo("Keiko"));
                Assert.That(
                    runtime.guildIntroText,
                    Is.EqualTo("Workshop persistence probe"));
                Assert.That(runtime.speaker.x, Is.EqualTo(333f));
                Assert.That(runtime.speaker.y, Is.EqualTo(177f));
                Assert.That(runtime.schemaVersion, Is.EqualTo(2));
                Assert.That(
                    runtime.speaker.scaleX,
                    Is.EqualTo(1.31f).Within(.001f));
                Assert.That(
                    runtime.speaker.scaleY,
                    Is.EqualTo(.82f).Within(.001f));
                Assert.That(
                    runtime.speaker.anchorMinX,
                    Is.EqualTo(.12f).Within(.001f));
                Assert.That(
                    runtime.speaker.pivotY,
                    Is.EqualTo(.67f).Within(.001f));
                Assert.That(
                    runtime.voiceVolume,
                    Is.EqualTo(.27f).Within(.001f));
            }
            finally
            {
                if (hadConfig)
                File.WriteAllText(absolute, previousConfig);
                else if (File.Exists(absolute))
                    File.Delete(absolute);

                if (hadMeta)
                    File.WriteAllText(metaAbsolute, previousMeta);
                else if (File.Exists(metaAbsolute))
                    File.Delete(metaAbsolute);

                AssetDatabase.Refresh();
                HubDialogueConfig.ResetCache();
            }
        }

        [Test]
        public void DeletedArrowCover_RemainsDeletedAfterCaptureAndRebuild()
        {
            HubDialogueConfigData authored =
                HubDialogueConfig.NormalizeForAuthoring(
                    new HubDialogueConfigData());

            Scene scene =
                HubDialogueWorkshopBuilder
                    .CreateUnsavedWorkshopForTests(authored);
            GameObject root =
                scene.GetRootGameObjects()[0];

            Transform cover =
                HubDialogueWorkshopBuilder.FindTransform(
                    root.transform,
                    "BakedArrowCover");
            Assert.That(cover, Is.Not.Null);

            Object.DestroyImmediate(
                cover.gameObject);

            HubDialogueWorkshopBuilder.CaptureForTests(
                scene,
                authored);

            Assert.That(
                authored.arrowCover.exists,
                Is.False,
                "Deleting the Workshop object must create an explicit tombstone.");

            Scene rebuilt =
                HubDialogueWorkshopBuilder
                    .CreateUnsavedWorkshopForTests(authored);
            GameObject rebuiltRoot =
                rebuilt.GetRootGameObjects()[0];

            Assert.That(
                HubDialogueWorkshopBuilder.FindTransform(
                    rebuiltRoot.transform,
                    "BakedArrowCover"),
                Is.Null,
                "Rebuild must not resurrect an explicitly deleted element.");
        }

        [Test]
        public void SecondEdit_OverwritesFirstCapturedLayoutState()
        {
            Scene scene =
                HubDialogueWorkshopBuilder
                    .CreateUnsavedWorkshopForTests();
            GameObject root =
                scene.GetRootGameObjects()[0];
            RectTransform plaque =
                (RectTransform)
                    HubDialogueWorkshopBuilder.FindTransform(
                        root.transform,
                        "PlaqueArt");

            HubDialogueConfigData authored =
                HubDialogueConfig.NormalizeForAuthoring(
                    new HubDialogueConfigData());

            plaque.anchoredPosition =
                new Vector2(401f, -222f);
            plaque.localScale =
                new Vector3(1.2f, .9f, 1f);
            HubDialogueWorkshopBuilder.CaptureForTests(
                scene,
                authored);

            Assert.That(
                authored.plaqueArt.x,
                Is.EqualTo(401f));
            Assert.That(
                authored.plaqueArt.scaleX,
                Is.EqualTo(1.2f).Within(.001f));

            plaque.anchoredPosition =
                new Vector2(477f, -255f);
            plaque.localScale =
                new Vector3(.83f, 1.14f, 1f);
            HubDialogueWorkshopBuilder.CaptureForTests(
                scene,
                authored);

            Assert.That(
                authored.plaqueArt.x,
                Is.EqualTo(477f),
                "The second edit must become authoritative.");
            Assert.That(
                authored.plaqueArt.y,
                Is.EqualTo(255f));
            Assert.That(
                authored.plaqueArt.scaleX,
                Is.EqualTo(.83f).Within(.001f));
            Assert.That(
                authored.plaqueArt.scaleY,
                Is.EqualTo(1.14f).Within(.001f));
        }

        [Test]
        public void SecondSave_SurvivesSerializedRebuildAndReopen()
        {
            HubDialogueConfigData authored =
                HubDialogueConfig.NormalizeForAuthoring(
                    new HubDialogueConfigData());

            Scene scene =
                HubDialogueWorkshopBuilder
                    .CreateUnsavedWorkshopForTests(authored);
            GameObject root =
                scene.GetRootGameObjects()[0];

            RectTransform plaque =
                (RectTransform)
                    HubDialogueWorkshopBuilder.FindTransform(
                        root.transform,
                        "PlaqueArt");

            // First authored save.
            plaque.anchoredPosition =
                new Vector2(401f, -222f);
            plaque.localScale =
                new Vector3(1.2f, .9f, 1f);
            HubDialogueWorkshopBuilder.CaptureForTests(
                scene,
                authored);

            HubDialogueConfigData firstSaved =
                HubDialogueConfig.NormalizeForAuthoring(
                    JsonUtility.FromJson<HubDialogueConfigData>(
                        JsonUtility.ToJson(authored)));

            // Rebuild from exactly what the first save serialized.
            HubDialogueWorkshopBuilder.RebuildSceneForTests(
                scene,
                firstSaved);
            root =
                scene.GetRootGameObjects()[0];
            plaque =
                (RectTransform)
                    HubDialogueWorkshopBuilder.FindTransform(
                        root.transform,
                        "PlaqueArt");

            // Second edit/save of the same object plus a deletion.
            plaque.anchoredPosition =
                new Vector2(477f, -255f);
            plaque.sizeDelta =
                new Vector2(933f, 355f);
            plaque.localScale =
                new Vector3(.83f, 1.14f, 1f);

            Transform cover =
                HubDialogueWorkshopBuilder.FindTransform(
                    root.transform,
                    "BakedArrowCover");
            Assert.That(cover, Is.Not.Null);
            Object.DestroyImmediate(cover.gameObject);

            HubDialogueWorkshopBuilder.CaptureForTests(
                scene,
                firstSaved);

            string secondJson =
                JsonUtility.ToJson(firstSaved);
            HubDialogueConfigData secondSaved =
                HubDialogueConfig.NormalizeForAuthoring(
                    JsonUtility.FromJson<HubDialogueConfigData>(
                        secondJson));

            Assert.That(
                secondSaved.schemaVersion,
                Is.EqualTo(2));
            Assert.That(
                secondSaved.arrowCover.exists,
                Is.False);

            // Explicit Rebuild Preview From Saved Config path.
            HubDialogueWorkshopBuilder.RebuildSceneForTests(
                scene,
                secondSaved);
            AssertSecondSaveState(scene);

            // Reopen path: a stale generated scene must be discarded and
            // rehydrated from the same authoritative saved config.
            GameObject staleRoot =
                scene.GetRootGameObjects()[0];
            RectTransform stalePlaque =
                (RectTransform)
                    HubDialogueWorkshopBuilder.FindTransform(
                        staleRoot.transform,
                        "PlaqueArt");
            stalePlaque.anchoredPosition =
                new Vector2(99f, -99f);

            HubDialogueWorkshopBuilder.RebuildSceneForTests(
                scene,
                secondSaved);
            AssertSecondSaveState(scene);
        }

        private static void AssertSecondSaveState(Scene scene)
        {
            GameObject root =
                scene.GetRootGameObjects()[0];
            RectTransform plaque =
                (RectTransform)
                    HubDialogueWorkshopBuilder.FindTransform(
                        root.transform,
                        "PlaqueArt");

            Assert.That(
                plaque.anchoredPosition.x,
                Is.EqualTo(477f).Within(.001f));
            Assert.That(
                plaque.anchoredPosition.y,
                Is.EqualTo(-255f).Within(.001f));
            Assert.That(
                plaque.sizeDelta.x,
                Is.EqualTo(933f).Within(.001f));
            Assert.That(
                plaque.sizeDelta.y,
                Is.EqualTo(355f).Within(.001f));
            Assert.That(
                plaque.localScale.x,
                Is.EqualTo(.83f).Within(.001f));
            Assert.That(
                plaque.localScale.y,
                Is.EqualTo(1.14f).Within(.001f));

            Assert.That(
                HubDialogueWorkshopBuilder.FindTransform(
                    root.transform,
                    "BakedArrowCover"),
                Is.Null,
                "Serialized v2 deletion must survive rebuild/reopen.");
        }

        [Test]
        public void Rebuild_PreservesAuthoredSiblingOrder()
        {
            HubDialogueConfigData authored =
                HubDialogueConfig.NormalizeForAuthoring(
                    new HubDialogueConfigData());

            authored.speaker.siblingIndex = 0;
            authored.dialogue.siblingIndex = 1;
            authored.plaqueArt.siblingIndex = 2;
            authored.portraitMask.siblingIndex = 3;
            authored.arrowCover.siblingIndex = 4;
            authored.completionArrow.siblingIndex = 5;
            authored.muteButton.siblingIndex = 6;
            authored.forwardButton.siblingIndex = 7;
            authored.menuButton.siblingIndex = 8;

            Scene scene =
                HubDialogueWorkshopBuilder
                    .CreateUnsavedWorkshopForTests(authored);
            GameObject root =
                scene.GetRootGameObjects()[0];

            Transform speaker =
                HubDialogueWorkshopBuilder.FindTransform(
                    root.transform,
                    "SpeakerName");
            Transform dialogue =
                HubDialogueWorkshopBuilder.FindTransform(
                    root.transform,
                    "DialogueText");
            Transform plaque =
                HubDialogueWorkshopBuilder.FindTransform(
                    root.transform,
                    "PlaqueArt");

            Assert.That(
                speaker.GetSiblingIndex(),
                Is.EqualTo(0));
            Assert.That(
                dialogue.GetSiblingIndex(),
                Is.EqualTo(1));
            Assert.That(
                plaque.GetSiblingIndex(),
                Is.EqualTo(2));
        }

        [Test]
        public void LegacyConfig_MigratesToV2WithoutLosingLegacyLayout()
        {
            HubDialogueConfigData legacy =
                JsonUtility.FromJson<HubDialogueConfigData>(
                    "{\"layoutScale\":1.25," +
                    "\"arrowCover\":{" +
                    "\"x\":1057,\"y\":249," +
                    "\"width\":52,\"height\":54}}");

            HubDialogueConfigData migrated =
                HubDialogueConfig.NormalizeForAuthoring(
                    legacy);

            Assert.That(
                migrated.schemaVersion,
                Is.EqualTo(2));
            Assert.That(
                migrated.arrowCover.exists,
                Is.True);
            Assert.That(
                migrated.arrowCover.active,
                Is.True);
            Assert.That(
                migrated.layoutRoot.scaleX,
                Is.EqualTo(1.25f).Within(.001f));
            Assert.That(
                migrated.layoutRoot.anchorMinX,
                Is.EqualTo(0f));
            Assert.That(
                migrated.layoutRoot.anchorMinY,
                Is.EqualTo(1f));
            Assert.That(
                migrated.layoutRoot.pivotX,
                Is.EqualTo(0f));
            Assert.That(
                migrated.layoutRoot.pivotY,
                Is.EqualTo(1f));
            Assert.That(
                migrated.arrowCover.uvWidth,
                Is.EqualTo(90f / 2048f).Within(.0001f));
            Assert.That(
                migrated.arrowCover.uvHeight,
                Is.EqualTo(90f / 682f).Within(.0001f));
        }

        [Test]
        public void WorkshopScene_IsNeverAddedToBuildSettings()
        {
            EditorBuildSettingsScene[] scenes =
                EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                Assert.That(
                    scenes[i].path,
                    Is.Not.EqualTo(
                        HubDialogueWorkshopBuilder.ScenePath));
            }
        }

        [Test]
        public void DefaultConfig_ExposesContentAudioAndAllControls()
        {
            HubDialogueConfigData data =
                HubDialogueConfig.LoadFresh();

            Assert.That(data.speakerName, Is.EqualTo("Keiko"));
            Assert.That(data.guildIntroText, Does.Contain("ноутбук"));
            Assert.That(data.voiceEnabled, Is.True);
            Assert.That(data.voiceVolume, Is.GreaterThan(0f));
            Assert.That(data.voicePitch, Is.GreaterThan(0f));
            Assert.That(data.plaque, Is.Not.Null);
            Assert.That(data.portraitMask, Is.Not.Null);
            Assert.That(data.portrait, Is.Not.Null);
            Assert.That(data.speaker, Is.Not.Null);
            Assert.That(data.dialogue, Is.Not.Null);
            Assert.That(data.completionArrow, Is.Not.Null);
            Assert.That(data.forwardButton, Is.Not.Null);
        }
    }
}
