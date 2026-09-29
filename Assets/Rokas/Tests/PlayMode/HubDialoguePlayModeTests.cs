using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class HubDialoguePlayModeTests
    {
        private GameObject root;
        private RokasBootstrap bootstrap;
        private string directory;

        [UnityTest]
        public IEnumerator Window_UsesTalkThenIdle_ArrowAdvanceCloseAndCleanReopen()
        {
            Initialize("rokas-hub-dialogue-window-");
            yield return null;

            Button window = Find<Button>("WindowHotspot");
            Assert.That(window, Is.Not.Null);
            int clicksBefore = bootstrap.View.GenericClickCount;
            window.onClick.Invoke();
            yield return null;

            Assert.That(
                bootstrap.View.GenericClickCount,
                Is.EqualTo(clicksBefore),
                "Hub-opening inspect hotspots must not play generic UI click.");

            RectTransform hub = FindRect("HubDialogueRoot");
            Assert.That(hub, Is.Not.Null);
            Assert.That(hub.gameObject.activeInHierarchy, Is.True);

            RawImage portrait = Find<RawImage>("PortraitImage");
            Assert.That(portrait, Is.Not.Null);
            Assert.That(portrait.texture, Is.Not.Null);
            Assert.That(portrait.uvRect.y, Is.EqualTo(0f).Within(.001f),
                "Typewriter start must use the Talk atlas row.");

            RectTransform arrow = FindRect("CompletionArrow");
            Assert.That(arrow, Is.Not.Null);
            Assert.That(arrow.gameObject.activeInHierarchy, Is.False,
                "Completion arrow must be hidden while the line is typing.");
            Assert.That(
                bootstrap.View.HubVoicePlaying,
                Is.True,
                "Hub text voice must play while Typewriter is active.");

            Text dialogue = Find<Text>("DialogueText");
            Assert.That(dialogue, Is.Not.Null);
            string partial = dialogue.text;

            Button advance = Find<Button>("HubForwardButton");
            Assert.That(advance, Is.Not.Null);
            advance.onClick.Invoke();
            yield return null;

            Assert.That(hub.gameObject.activeInHierarchy, Is.True,
                "The first click while typing completes the current line only.");
            Assert.That(dialogue.text.Length, Is.GreaterThanOrEqualTo(partial.Length));
            Assert.That(
                portrait.uvRect.y,
                Is.EqualTo(.5f).Within(.001f),
                "Completed text must switch the portrait to Idle.");
            Assert.That(arrow.gameObject.activeInHierarchy, Is.True,
                "Completion arrow must appear only after the line is complete.");
            Assert.That(
                bootstrap.View.HubVoicePlaying,
                Is.False,
                "Completing the line must stop Hub text voice.");
            Assert.That(
                bootstrap.View.GenericClickCount,
                Is.EqualTo(clicksBefore),
                "Hub advance must not play generic UI click.");

            advance.onClick.Invoke();
            yield return null;
            Assert.That(hub.gameObject.activeInHierarchy, Is.False,
                "The next click on the final completed line must close the plaque.");

            window.onClick.Invoke();
            yield return null;
            Assert.That(hub.gameObject.activeInHierarchy, Is.True,
                "The same inspect hotspot must reopen a clean Hub Dialogue.");
            Assert.That(portrait.uvRect.y, Is.EqualTo(0f).Within(.001f));
            Assert.That(arrow.gameObject.activeInHierarchy, Is.False);
            Assert.That(dialogue.text.Length, Is.LessThan(
                "Поезд проходит без остановки. На этот раз — настоящий.".Length));

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InspectHotspots_WindowFloorLampAndCat_OpenHub()
        {
            Initialize("rokas-hub-dialogue-routing-");
            yield return null;

            string[] hotspotNames =
            {
                "WindowHotspot",
                "LampHotspot",
                "MameHotspot"
            };

            for (int i = 0; i < hotspotNames.Length; i++)
            {
                Button hotspot = Find<Button>(hotspotNames[i]);
                Assert.That(hotspot, Is.Not.Null, hotspotNames[i]);
                hotspot.onClick.Invoke();
                yield return null;

                RectTransform hub = FindRect("HubDialogueRoot");
                Assert.That(
                    hub.gameObject.activeInHierarchy,
                    Is.True,
                    hotspotNames[i] + " must open Hub Dialogue.");

                Button advance = Find<Button>("HubForwardButton");
                advance.onClick.Invoke();
                yield return null;
                advance.onClick.Invoke();
                yield return null;

                Assert.That(
                    hub.gameObject.activeInHierarchy,
                    Is.False,
                    hotspotNames[i] + " dialogue must close cleanly.");
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Laptop_RemainsDirectAction_AndNeverOpensHub()
        {
            Initialize("rokas-hub-dialogue-laptop-");
            yield return null;

            int clicksBefore = bootstrap.View.GenericClickCount;
            Find<Button>("LaptopHotspot").onClick.Invoke();
            yield return null;

            Assert.That(bootstrap.View.GenericClickCount, Is.GreaterThan(clicksBefore));
            Assert.That(FindRect("HubDialogueRoot").gameObject.activeInHierarchy, Is.False);
            Assert.That(FindRect("YomiLaptop"), Is.Not.Null,
                "Laptop hotspot must keep opening the existing laptop directly.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Swords_RemainDirectAction_AndNeverOpenHub()
        {
            Initialize("rokas-hub-dialogue-swords-");
            yield return null;

            int clicksBefore = bootstrap.View.GenericClickCount;
            Find<Button>("WorkbenchHotspot").onClick.Invoke();
            yield return null;

            Assert.That(bootstrap.View.GenericClickCount, Is.GreaterThan(clicksBefore));
            Assert.That(FindRect("HubDialogueRoot").gameObject.activeInHierarchy, Is.False);
            Assert.That(FindRect("WorkbenchEyebrow"), Is.Not.Null,
                "Swords hotspot must keep opening the existing workbench directly.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Door_RemainsDirectAction_AndNeverOpensHub()
        {
            Initialize("rokas-hub-dialogue-door-");
            yield return null;

            int clicksBefore = bootstrap.View.GenericClickCount;
            Find<Button>("DoorHotspot").onClick.Invoke();
            yield return null;

            Assert.That(bootstrap.View.GenericClickCount, Is.GreaterThan(clicksBefore));
            Assert.That(FindRect("HubDialogueRoot").gameObject.activeInHierarchy, Is.False);
            Assert.That(FindRect("YomiLaptop"), Is.Not.Null,
                "At the initial Home phase Door must keep its existing direct route to YOMI.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GuildIntro_IsSaveScopedAndDoesNotReplay()
        {
            Initialize("rokas-hub-dialogue-story-intro-");
            yield return null;

            Assert.That(bootstrap.Session.State.hubGuildIntroSeen, Is.False);
            var method = typeof(RokasBootstrap).GetMethod(
                "TryOpenHubGuildIntroAfterVn",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            bool opened = (bool)method.Invoke(bootstrap, null);
            yield return null;

            Assert.That(opened, Is.True);
            Assert.That(bootstrap.Session.State.hubGuildIntroSeen, Is.True);
            Assert.That(bootstrap.View.HubDialogueOpen, Is.True);
            Text speakerName = Find<Text>("SpeakerName");
            Assert.That(speakerName, Is.Not.Null);
            Assert.That(speakerName.text, Is.EqualTo("Keiko"));
            Assert.That(File.Exists(Path.Combine(directory, "save.json")), Is.True);
            SaveData durable = JsonUtility.FromJson<SaveData>(
                File.ReadAllText(Path.Combine(directory, "save.json")));
            Assert.That(durable, Is.Not.Null);
            Assert.That(durable.hubGuildIntroSeen, Is.True);

            Button advance = Find<Button>("HubForwardButton");
            advance.onClick.Invoke();
            yield return null;
            advance.onClick.Invoke();
            yield return null;
            Assert.That(bootstrap.View.HubDialogueOpen, Is.False);

            bool replayed = (bool)method.Invoke(bootstrap, null);
            Assert.That(replayed, Is.False);
            Assert.That(bootstrap.View.HubDialogueOpen, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AuthoredLayout_IsAppliedToRuntimeWithoutReinterpretation()
        {
            Initialize(
                "rokas-hub-dialogue-layout-parity-",
                config =>
                {
                    config.plaqueArt.x = 421f;
                    config.plaqueArt.y = 233f;
                    config.plaqueArt.width = 777f;
                    config.plaqueArt.height = 311f;
                    config.plaqueArt.scaleX = 1.17f;
                    config.plaqueArt.scaleY = .84f;
                    config.plaqueArt.anchorMinX = .15f;
                    config.plaqueArt.anchorMinY = .72f;
                    config.plaqueArt.anchorMaxX = .15f;
                    config.plaqueArt.anchorMaxY = .72f;
                    config.plaqueArt.pivotX = .31f;
                    config.plaqueArt.pivotY = .66f;
                    config.plaqueArt.rotationZ = 4.25f;
                });
            yield return null;

            RectTransform plaque =
                FindRect("PlaqueArt");
            Assert.That(plaque, Is.Not.Null);
            Assert.That(
                plaque.anchoredPosition.x,
                Is.EqualTo(421f).Within(.001f));
            Assert.That(
                plaque.anchoredPosition.y,
                Is.EqualTo(-233f).Within(.001f));
            Assert.That(
                plaque.sizeDelta.x,
                Is.EqualTo(777f).Within(.001f));
            Assert.That(
                plaque.sizeDelta.y,
                Is.EqualTo(311f).Within(.001f));
            Assert.That(
                plaque.localScale.x,
                Is.EqualTo(1.17f).Within(.001f));
            Assert.That(
                plaque.localScale.y,
                Is.EqualTo(.84f).Within(.001f));
            Assert.That(
                plaque.anchorMin.x,
                Is.EqualTo(.15f).Within(.001f));
            Assert.That(
                plaque.anchorMin.y,
                Is.EqualTo(.72f).Within(.001f));
            Assert.That(
                plaque.pivot.x,
                Is.EqualTo(.31f).Within(.001f));
            Assert.That(
                plaque.pivot.y,
                Is.EqualTo(.66f).Within(.001f));
            Assert.That(
                Mathf.DeltaAngle(
                    0f,
                    plaque.localEulerAngles.z),
                Is.EqualTo(4.25f).Within(.01f));

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ExplicitArrowCoverDeletion_IsNotRecreatedByRuntime()
        {
            Initialize(
                "rokas-hub-dialogue-delete-cover-",
                config =>
                {
                    config.arrowCover.exists = false;
                    config.arrowCover.active = false;
                });
            yield return null;

            Assert.That(
                FindRect("BakedArrowCover"),
                Is.Null,
                "Runtime must respect the Workshop deletion tombstone.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RepeatedInspectClick_DoesNotCreateDuplicatePlaques()
        {
            Initialize("rokas-hub-dialogue-duplicate-");
            yield return null;

            Button window = Find<Button>("WindowHotspot");
            window.onClick.Invoke();
            window.onClick.Invoke();
            yield return null;

            int count = 0;
            foreach (RectTransform rect in
                     root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == "HubDialogueRoot")
                    count++;

            Assert.That(count, Is.EqualTo(1));
            Assert.That(FindRect("HubDialogueRoot").gameObject.activeInHierarchy, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private void Initialize(
            string prefix,
            Action<HubDialogueConfigData> configure = null)
        {
            HubDialogueConfig.ResetCache();
            HubDialogueConfigData config =
                HubDialogueConfig.Current;
            configure?.Invoke(config);

            LogAssert.Expect(
                LogType.Log,
                "[ROKAS HOME] FINAL DIRECT UI READY: outlines=3 connectors=2 icons=6");
            directory = Path.Combine(
                Path.GetTempPath(),
                prefix + Guid.NewGuid().ToString("N"));
            root = new GameObject("HubDialogueFixture");
            bootstrap = root.AddComponent<RokasBootstrap>();
            bootstrap.Initialize(directory);
        }

        private T Find<T>(string name) where T : Component
        {
            if (root == null) return null;
            foreach (T component in root.GetComponentsInChildren<T>(true))
                if (component.name == name) return component;
            return null;
        }

        private RectTransform FindRect(string name)
        {
            return Find<RectTransform>(name);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            bootstrap = null;
            HubDialogueConfig.ResetCache();
            yield return null;
            if (!string.IsNullOrEmpty(directory) &&
                Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
