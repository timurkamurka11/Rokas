using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class VnSceneComposerMainGameIntegrationPlayModeTests
    {
        private GameObject root;
        private string storyMediaPath;
        private string hiddenStoryMediaPath;
        private bool previousIgnoreFailingMessages;
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        [UnityTest]
        public IEnumerator EnterWorldRoutesThroughPackagedRuntimeVnBeforeExistingHome()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsVnIntroProgress.CompletedKey);
            PlayerPrefs.Save();
            HideStoryMediaForDeterministicLinuxFallback();

            RokasBootstrap boot = CreateRealLaunchIgnoringHostDecoderErrors();
            RokasVnRuntimeIntroPackage package = CreateIntroPackage();
            FieldInfo packageOverride = typeof(RokasBootstrap).GetField(
                "runtimeVnIntroPackageOverride",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(packageOverride, Is.Not.Null,
                "Bootstrap needs a narrow package override seam so the same Start path can be integration-tested without Library authoring storage.");
            packageOverride.SetValue(boot, package);

            yield return WaitFor("RokasMainMenu", 1.5f);

            Button enter = FindButton("EnterWorldButton");
            Assert.That(enter, Is.Not.Null);
            enter.onClick.Invoke();
            enter.onClick.Invoke();
            yield return null;

            Assert.That(Find("RokasMainMenu"), Is.Not.Null,
                "Main Menu must remain under the existing partial fade until the full-black handoff.");
            Assert.That(Find("HomeTitle"), Is.Null,
                "Start must not construct Home before the packaged VN.");
            Assert.That(Find("RokasVnRuntimeRoot"), Is.Null,
                "Packaged VN must not appear before the Main Menu reaches full black.");

            yield return WaitFor("RokasVnRuntimeRoot", 1.5f);

            Assert.That(Find("RokasMainMenu"), Is.Null,
                "The consumed Main Menu must be disposed at the existing full-black handoff.");
            Assert.That(Find("HomeTitle"), Is.Null,
                "Gameplay/Home must not exist behind the active VN.");
            Assert.That(Find("VnIntroRoot"), Is.Null,
                "The old Yarn intro presentation must no longer own the Start path.");
            Assert.That(Count("RokasVnRuntimeRoot"), Is.EqualTo(1),
                "Repeated Start input must never create duplicate runtime VN players.");
            Assert.That(PlayerPrefs.GetInt(PlayerPrefsVnIntroProgress.CompletedKey, 0), Is.Zero,
                "Intro completion must remain unset while the packaged VN is active.");
            Assert.That(boot.View, Is.Null);
        }

        private RokasVnRuntimeIntroPackage CreateIntroPackage()
        {
            Texture2D background = MakeTexture("IntegrationBackground");
            Texture2D character = MakeTexture("IntegrationMina");
            Texture2D plaque = MakeTexture("IntegrationPlaque");
            Texture2D controls = MakeTexture("IntegrationControls");
            Texture2D muted = MakeTexture("IntegrationMuted");
            Texture2D triangle = MakeTexture("IntegrationTriangle");

            var scene = new RokasVnRuntimeSceneSnapshot
            {
                sceneId = "11111111111111111111111111111111",
                label = "Integration Final",
                isTerminal = true,
                terminalFadeDuration = .12f,
                media = new RokasVnRuntimeMediaSnapshot
                {
                    kind = 1,
                    reference = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    runtimeAssetKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    scaleMode = 1
                }
            };
            scene.characters.Add(new RokasVnRuntimeCharacterSnapshot
            {
                characterId = "Mina",
                stateId = "mina_integration",
                stageSlot = 1
            });
            scene.dialogueBeats.Add(new RokasVnRuntimeBeatSnapshot
            {
                beatId = "22222222222222222222222222222222",
                speaker = "Mina",
                text = "Интеграционный кадр"
            });

            var snapshot = new RokasVnRuntimeIntroSnapshot
            {
                projectId = RokasVnRuntimeIntroPackage.ExpectedProjectId,
                sourceProjectSha256 = "main-game-integration-fixture",
                sceneCount = 1,
                beatCount = 1
            };
            snapshot.scenes.Add(scene);

            var package = ScriptableObject.CreateInstance<RokasVnRuntimeIntroPackage>();
            owned.Add(package);
            package.Configure(
                RokasVnRuntimeIntroPackage.ExpectedProjectId,
                "main-game-integration-fixture",
                "{}",
                snapshot,
                new[]
                {
                    new RokasVnRuntimeAssetBinding
                    {
                        authoredKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        displayName = "IntegrationBackground",
                        kind = RokasVnRuntimeAssetKind.Texture,
                        asset = background
                    }
                },
                new[]
                {
                    new RokasVnRuntimeCharacterStateBinding
                    {
                        stateId = "mina_integration",
                        characterId = "Mina",
                        texture = character,
                        bodyUv = new Rect(0f, 0f, 1f, 1f)
                    }
                },
                plaque, controls, muted, triangle);
            return package;
        }

        private Texture2D MakeTexture(string name)
        {
            var texture = new Texture2D(8, 8) { name = name };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            owned.Add(texture);
            return texture;
        }

        private void HideStoryMediaForDeterministicLinuxFallback()
        {
            storyMediaPath = Path.Combine(
                Application.streamingAssetsPath,
                "RokasVideo",
                "StoryIntro.mp4");
            Assert.That(File.Exists(storyMediaPath), Is.True,
                "Focused integration CI must contain the committed StoryIntro.mp4.");
            hiddenStoryMediaPath = Path.Combine(
                Path.GetTempPath(),
                "rokas-vn-main-game-hidden-story-" +
                Guid.NewGuid().ToString("N") + ".mp4");
            File.Move(storyMediaPath, hiddenStoryMediaPath);
        }

        private RokasBootstrap CreateRealLaunchIgnoringHostDecoderErrors()
        {
            previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            root = new GameObject("VnMainGameIntegrationFixture");
            if (!EventSystem.current)
            {
                var events = new GameObject(
                    "VnMainGameIntegrationEventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
                events.transform.SetParent(root.transform, false);
            }
            return root.AddComponent<RokasBootstrap>();
        }

        private IEnumerator WaitFor(string objectName, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Find(objectName) == null &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Find(objectName), Is.Not.Null,
                "Timed out waiting for: " + objectName);
        }

        private GameObject Find(string name)
        {
            if (root == null) return null;
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            return null;
        }

        private Button FindButton(string name)
        {
            GameObject go = Find(name);
            return go != null ? go.GetComponent<Button>() : null;
        }

        private int Count(string name)
        {
            int count = 0;
            if (root == null) return count;
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) count++;
            return count;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            PlayerPrefs.DeleteKey(PlayerPrefsVnIntroProgress.CompletedKey);
            PlayerPrefs.Save();

            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;

            if (!string.IsNullOrEmpty(hiddenStoryMediaPath) &&
                File.Exists(hiddenStoryMediaPath) &&
                !string.IsNullOrEmpty(storyMediaPath) &&
                !File.Exists(storyMediaPath))
                File.Move(hiddenStoryMediaPath, storyMediaPath);

            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
            owned.Clear();
            root = null;
            storyMediaPath = null;
            hiddenStoryMediaPath = null;
        }
    }
}
