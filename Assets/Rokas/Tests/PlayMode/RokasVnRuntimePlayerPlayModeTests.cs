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
            Assert.That(Find("VnDialogue").GetComponent<Text>().text, Is.EqualTo("Первый кадр"));

            Assert.That(FindButton("VnForwardButton"), Is.Not.Null);
            Assert.That(FindButton("VnMuteButton"), Is.Not.Null);
            Assert.That(FindButton("VnMenuButton"), Is.Not.Null);

            Assert.That(player.RequestAdvance(), Is.False,
                "First advance while typewriter is incomplete must reveal the line only.");
            Assert.That(player.RequestAdvance(), Is.True,
                "Second advance must move to the next Beat.");
            Assert.That(Find("VnDialogue").GetComponent<Text>().text, Is.EqualTo("Финал"));

            Assert.That(player.RequestAdvance(), Is.True,
                "Last Beat must enter the authored terminal fade.");
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
