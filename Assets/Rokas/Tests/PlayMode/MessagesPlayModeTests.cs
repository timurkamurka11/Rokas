using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class MessagesPlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator LaptopMessagesBuildsRealConversationShell()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-messages-ui-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("MessagesUiFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            yield return null;

            Press("LaptopHotspot");
            Press("LaptopMessages");

            Assert.That(FindRect("MessagesRoot"), Is.Not.Null, "Messages must replace the laptop placeholder with a dedicated root.");
            Assert.That(FindRect("MessagesSearch"), Is.Not.Null, "Messages must expose a real search control.");
            Assert.That(FindRect("MessagesContactList"), Is.Not.Null, "Messages must render the contact list pane.");
            Assert.That(FindRect("MessagesHeader"), Is.Not.Null, "Messages must render the active-contact header.");
            Assert.That(FindRect("MessagesConversationViewport"), Is.Not.Null, "Messages must render a clipped conversation viewport.");
            Assert.That(FindButton("MessagesContact_kaito"), Is.Not.Null, "Kaito must be selectable from the real contact list.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LaptopMessagesInitializesTmpInCleanPlayMode()
        {
            TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings");
            Assert.That(settings, Is.Not.Null,
                "Canonical TMP Settings must exist at Assets/TextMesh Pro/Resources/TMP Settings.asset.");

            TMP_FontAsset productionFont = Resources.Load<TMP_FontAsset>("RokasSans TMP");
            Assert.That(productionFont, Is.Not.Null,
                "Messages production TMP font must exist at Assets/Rokas/Resources/RokasSans TMP.asset.");
            Assert.That(productionFont.material, Is.Not.Null, "Messages production TMP font must have a persistent material.");
            Assert.That(productionFont.material.shader, Is.Not.Null, "Messages production TMP font must have a valid SDF shader.");
            Assert.That(productionFont.material.shader.name, Is.EqualTo("TextMeshPro/Mobile/Distance Field"));
            Assert.That(productionFont.atlasPopulationMode.ToString(), Is.EqualTo("Static"),
                "Messages production font must use a baked static atlas.");
            bool hasRequiredGlyphs = productionFont.HasCharacters(
                "AZazАЯаяЁё Сообщения Кайто — Всё готово", out uint[] missingGlyphs, false, false);
            Assert.That(hasRequiredGlyphs && (missingGlyphs == null || missingGlyphs.Length == 0), Is.True,
                "Messages production font must contain Latin and required Russian glyphs including Ё/ё.");

            directory = Path.Combine(Path.GetTempPath(), "rokas-messages-tmp-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("MessagesTmpFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            yield return null;

            Press("LaptopHotspot");
            Press("LaptopMessages");

            TMP_InputField input = root.GetComponentInChildren<TMP_InputField>();
            Assert.That(input, Is.Not.Null, "Messages search must initialize a TMP input field in a clean runtime.");
            Assert.That(input.textComponent, Is.Not.Null);
            Assert.That(input.textComponent.font, Is.SameAs(productionFont),
                "Messages TMP text must use the persisted production font asset rather than a runtime-created copy.");
            input.textComponent.ForceMeshUpdate();
            LogAssert.NoUnexpectedReceived();
        }

        private void Press(string name)
        {
            Button button = FindButton(name);
            Assert.That(button, Is.Not.Null, "Missing active interaction: " + name);
            Assert.That(button.IsInteractable(), Is.True, name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        private Button FindButton(string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>())
            {
                if (button.name == name)
                {
                    return button;
                }
            }
            return null;
        }

        private RectTransform FindRect(string name)
        {
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>())
            {
                if (rect.name == name)
                {
                    return rect;
                }
            }
            return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
