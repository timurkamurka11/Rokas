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
            speaker.localEulerAngles =
                new Vector3(0f, 0f, 3.5f);
            speakerText.text = "Keiko";
            speakerText.fontSize = 24;
            speakerText.alignment =
                TextAnchor.MiddleCenter;

            HubDialogueConfigData data =
                HubDialogueWorkshopBuilder.CaptureForTests(
                    scene,
                    new HubDialogueConfigData());

            Assert.That(data.speaker.x, Is.EqualTo(271f));
            Assert.That(data.speaker.y, Is.EqualTo(166f));
            Assert.That(data.speaker.width, Is.EqualTo(444f));
            Assert.That(data.speaker.height, Is.EqualTo(48f));
            Assert.That(data.speaker.rotationZ,
                Is.EqualTo(3.5f).Within(.01f));
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
                    new HubDialogueConfigData();
                authored.speakerName = "Keiko";
                authored.guildIntroText =
                    "Workshop persistence probe";
                authored.speaker.x = 333f;
                authored.speaker.y = 177f;
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
