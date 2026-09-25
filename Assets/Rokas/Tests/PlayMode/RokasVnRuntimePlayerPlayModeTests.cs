using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class RokasVnRuntimePlayerPlayModeTests
    {
        private GameObject host;
        private RokasVnRuntimeIntroPackage package;
        private readonly List<Object> owned = new List<Object>();

        [UnityTest]
        public IEnumerator PackagedPlayerBuildsRuntimePresentationAndCompletesExactlyOnce()
        {
            package = CreatePackage();
            host = new GameObject("RuntimeVnPlayerFixture");
            int completed = 0;

            RokasVnRuntimePlayer player =
                RokasVnRuntimePlayer.Create(host.transform, package, () => completed++);
            yield return null;

            Assert.That(Find("RokasVnRuntimeRoot"), Is.Not.Null);
            RawImage background = Find("VnBackground").GetComponent<RawImage>();
            Assert.That(background.texture, Is.SameAs(package.Assets[0].asset));

            RawImage mina = Find("VnCharacter_Mina").GetComponent<RawImage>();
            Assert.That(mina.texture, Is.SameAs(package.CharacterStates[0].texture));
            Assert.That(mina.gameObject.activeSelf, Is.True);

            RawImage plaque = Find("VnDialoguePlaque").GetComponent<RawImage>();
            Assert.That(plaque.texture, Is.SameAs(package.DialoguePlaque));
            Assert.That(Find("VnSpeaker").GetComponent<Text>().text, Is.EqualTo("Mina"));
            string initialDialogue = Find("VnDialogue").GetComponent<Text>().text;
            Assert.That("Первый кадр".StartsWith(initialDialogue), Is.True,
                "Initial runtime dialogue must be the authored typewriter prefix.");

            Assert.That(FindButton("VnForwardButton"), Is.Not.Null);
            Assert.That(FindButton("VnMuteButton"), Is.Not.Null);
            Assert.That(FindButton("VnMenuButton"), Is.Not.Null);

            Assert.That(player.RequestAdvance(), Is.False,
                "First advance while typewriter is incomplete must reveal the line only.");
            Assert.That(Find("VnDialogue").GetComponent<Text>().text,
                Is.EqualTo("Первый кадр"));
            Assert.That(player.RequestAdvance(), Is.True,
                "Second advance must move to the next Beat.");
            string terminalPrefix = Find("VnDialogue").GetComponent<Text>().text;
            Assert.That("Финал".StartsWith(terminalPrefix), Is.True,
                "Entering the terminal Beat must restart its authored typewriter.");

            Assert.That(player.RequestAdvance(), Is.False,
                "The terminal Beat still completes its typewriter before advancing.");
            Assert.That(Find("VnDialogue").GetComponent<Text>().text,
                Is.EqualTo("Финал"));
            Assert.That(player.RequestAdvance(), Is.True,
                "The next advance must enter the authored terminal fade.");
            player.TickForTests(.2f);
            Assert.That(Find("VnTerminalFade").GetComponent<CanvasGroup>().alpha,
                Is.GreaterThan(0f).And.LessThan(1f));
            player.TickForTests(.3f);
            Assert.That(completed, Is.EqualTo(1));

            player.TickForTests(5f);
            player.RequestAdvance();
            Assert.That(completed, Is.EqualTo(1),
                "Runtime player completion callback must be exactly-once.");

            player.Dispose();
            yield return null;
            Assert.That(Find("RokasVnRuntimeRoot"), Is.Null);
        }

        [UnityTest]
        public IEnumerator AuthoredBgmAndSfxRespectMuteAndDisposeWithRuntime()
        {
            package = CreateAudioPackage();
            host = new GameObject("RuntimeVnAudioFixture");

            RokasVnRuntimePlayer player =
                RokasVnRuntimePlayer.Create(host.transform, package, null);
            yield return null;

            GameObject runtimeRoot = Find("RokasVnRuntimeRoot");
            Assert.That(runtimeRoot, Is.Not.Null);
            AudioSource[] sources =
                runtimeRoot.GetComponentsInChildren<AudioSource>(true);

            AudioClip bgm = package.Assets
                .First(binding => binding.authoredKey == "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")
                .asset as AudioClip;
            AudioClip sfx = package.Assets
                .First(binding => binding.authoredKey == "cccccccccccccccccccccccccccccccc")
                .asset as AudioClip;

            AudioSource bgmSource = sources.FirstOrDefault(source => source.clip == bgm);
            AudioSource sfxSource = sources.FirstOrDefault(source => source.clip == sfx);
            Assert.That(bgmSource, Is.Not.Null,
                "Scene-authored BGM must be owned by the runtime VN player.");
            Assert.That(sfxSource, Is.Not.Null,
                "Scene/Beat-authored SFX must be owned by the runtime VN player.");
            Assert.That(bgmSource.volume, Is.GreaterThan(0f));
            Assert.That(sfxSource.volume, Is.GreaterThan(0f));

            player.SetMuted(true);
            Assert.That(bgmSource.volume, Is.Zero);
            Assert.That(sfxSource.volume, Is.Zero);

            player.SetMuted(false);
            player.TickForTests(.02f);
            Assert.That(bgmSource.volume, Is.GreaterThan(0f));
            Assert.That(sfxSource.volume, Is.GreaterThan(0f));

            player.Dispose();
            yield return null;
            Assert.That(Find("RokasVnRuntimeRoot"), Is.Null,
                "VN-owned audio and controls must disappear before gameplay owns input/audio.");
        }

        [Test]
        public void RuntimePlayerLivesInPlayerAssemblyWithoutUnityEditorReference()
        {
            string[] references = typeof(RokasVnRuntimePlayer).Assembly
                .GetReferencedAssemblies()
                .Select(item => item.Name)
                .ToArray();
            Assert.That(references, Does.Not.Contain("UnityEditor"));
        }

        private RokasVnRuntimeIntroPackage CreatePackage()
        {
            var background = MakeTexture("RuntimeBackground");
            var character = MakeTexture("RuntimeMina");
            var plaque = MakeTexture("RuntimePlaque");
            var controls = MakeTexture("RuntimeControls");
            var mute = MakeTexture("RuntimeMuted");
            var triangle = MakeTexture("RuntimeTriangle");

            var scene = new RokasVnRuntimeSceneSnapshot
            {
                sceneId = "11111111111111111111111111111111",
                label = "Final",
                isTerminal = true,
                terminalFadeDuration = .4f,
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
                stateId = "mina_runtime",
                stageSlot = 1
            });
            scene.dialogueBeats.Add(new RokasVnRuntimeBeatSnapshot
            {
                beatId = "22222222222222222222222222222222",
                speaker = "Mina",
                text = "Первый кадр"
            });
            scene.dialogueBeats.Add(new RokasVnRuntimeBeatSnapshot
            {
                beatId = "33333333333333333333333333333333",
                speaker = "Mina",
                text = "Финал"
            });

            var snapshot = new RokasVnRuntimeIntroSnapshot
            {
                projectId = RokasVnRuntimeIntroPackage.ExpectedProjectId,
                sourceProjectSha256 = "runtime-player-fixture",
                sceneCount = 1,
                beatCount = 2
            };
            snapshot.scenes.Add(scene);

            var result = ScriptableObject.CreateInstance<RokasVnRuntimeIntroPackage>();
            owned.Add(result);
            result.Configure(
                RokasVnRuntimeIntroPackage.ExpectedProjectId,
                "runtime-player-fixture",
                "{}",
                snapshot,
                new[]
                {
                    new RokasVnRuntimeAssetBinding
                    {
                        authoredKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        displayName = "RuntimeBackground",
                        kind = RokasVnRuntimeAssetKind.Texture,
                        asset = background
                    }
                },
                new[]
                {
                    new RokasVnRuntimeCharacterStateBinding
                    {
                        stateId = "mina_runtime",
                        characterId = "Mina",
                        texture = character,
                        bodyUv = new Rect(0f, 0f, 1f, 1f)
                    }
                },
                plaque, controls, mute, triangle);
            return result;
        }

        private RokasVnRuntimeIntroPackage CreateAudioPackage()
        {
            var result = CreatePackage();
            RokasVnRuntimeSceneSnapshot scene = result.Snapshot.scenes[0];

            AudioClip bgm = AudioClip.Create(
                "RuntimeBgm", 4410, 1, 44100, false);
            AudioClip sfx = AudioClip.Create(
                "RuntimeSfx", 4410, 1, 44100, false);
            owned.Add(bgm);
            owned.Add(sfx);

            scene.music = new RokasVnRuntimeMusicSnapshot
            {
                mode = 1,
                assetGuid = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                displayName = "Runtime BGM",
                volume = .65f,
                loop = true
            };
            scene.additionalAudioCues.Add(
                new RokasVnRuntimeAudioCueSnapshot
                {
                    cueId = "dddddddddddddddddddddddddddddddd",
                    displayName = "Runtime SFX",
                    assetGuid = "cccccccccccccccccccccccccccccccc",
                    enabled = true,
                    category = 0,
                    volume = .55f,
                    loop = true,
                    trigger = 0,
                    startDelaySeconds = 0f,
                    stopMode = 1
                });

            var assets = result.Assets.ToList();
            assets.Add(new RokasVnRuntimeAssetBinding
            {
                authoredKey = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                displayName = "Runtime BGM",
                kind = RokasVnRuntimeAssetKind.Audio,
                asset = bgm
            });
            assets.Add(new RokasVnRuntimeAssetBinding
            {
                authoredKey = "cccccccccccccccccccccccccccccccc",
                displayName = "Runtime SFX",
                kind = RokasVnRuntimeAssetKind.Audio,
                asset = sfx
            });

            result.Configure(
                result.ProjectId,
                result.SourceProjectSha256,
                result.PortableProjectJson,
                result.Snapshot,
                assets,
                result.CharacterStates.ToList(),
                result.DialoguePlaque,
                result.ControlSheet,
                result.MutedSpeaker,
                result.CompletionTriangle);
            return result;
        }

        private Texture2D MakeTexture(string name)
        {
            var texture = new Texture2D(8, 8) { name = name };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            owned.Add(texture);
            return texture;
        }

        private GameObject Find(string name)
        {
            if (host == null) return null;
            foreach (Transform item in host.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            return null;
        }

        private Button FindButton(string name)
        {
            GameObject go = Find(name);
            return go != null ? go.GetComponent<Button>() : null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (host != null) Object.Destroy(host);
            yield return null;
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
            host = null;
            package = null;
        }
    }
}
