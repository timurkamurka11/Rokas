using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Core;
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

        [UnityTest]
        public IEnumerator KaitoDialogueRunsThroughChoicesToCoordinateAttachment()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-messages-kaito-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("MessagesKaitoFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            yield return null;

            Press("LaptopHotspot");
            Press("LaptopMessages");
            Press("MessagesContact_kaito");

            yield return WaitForButton("MessagesChoice_kaito_choice_details", 180);
            Assert.That(ContainsTmpText("Нужно обсудить то, что я видел сегодня ночью."), Is.True,
                "Kaito_Start must render its first incoming Yarn line before choices.");
            Assert.That(FindButton("MessagesChoice_kaito_choice_skeptic"), Is.Not.Null,
                "First Kaito branch point must expose the authored alternative choice.");

            Press("MessagesChoice_kaito_choice_details");
            yield return WaitForButton("MessagesChoice_kaito_choice_guild", 180);

            Assert.That(ContainsTmpText("Что именно ты видел?"), Is.True,
                "Selected Yarn option must become a rendered outgoing player bubble.");
            Assert.That(ContainsTmpText("Ты правильно спрашиваешь. Я видел след, которого там быть не должно."), Is.True,
                "Kaito must continue through the selected details branch.");
            Assert.That(ContainsTmpText("Похоже, в восточном районе появилась нестабильная аномалия. Это не обычный ёкай."), Is.True,
                "Kaito must advance to the second authored branch point.");
            Assert.That(FindButton("MessagesChoice_kaito_choice_direct"), Is.Not.Null,
                "Second Kaito branch point must expose 2–4 real Yarn choices.");

            ConversationState afterFirstChoice = boot.Session.Messages.GetConversation("kaito");
            Assert.That(afterFirstChoice, Is.Not.Null);
            Assert.That(afterFirstChoice.branchState, Is.EqualTo("details"),
                "Selected Kaito branch state must be persisted through MessageService.");
            Assert.That(HasOutgoingChoice(afterFirstChoice, "kaito_choice_details"), Is.True,
                "The first player response must be persisted as an outgoing MessageService entry.");

            Press("MessagesChoice_kaito_choice_guild");
            yield return WaitForRect("MessagesAttachment_kaito_coordinates_attachment", 180);

            Assert.That(ContainsTmpText("Сначала проверю сведения Гильдии."), Is.True,
                "Second selected option must become an outgoing player bubble.");
            Assert.That(ContainsTmpText("Я отправляю тебе координаты. Будь осторожен."), Is.True,
                "Yarn must continue after the second selection.");
            Assert.That(ContainsTmpText("Координаты"), Is.True);
            Assert.That(ContainsTmpText("Восточный район, Сектор B-7"), Is.True,
                "The authored rokas_coordinates command must render the production coordinate attachment.");

            ConversationState finished = boot.Session.Messages.GetConversation("kaito");
            Assert.That(HasOutgoingChoice(finished, "kaito_choice_guild"), Is.True,
                "The second player response must be persisted exactly once.");
            Assert.That(HasCoordinateAttachment(finished, "kaito_coordinates_attachment"), Is.True,
                "Coordinate attachment identity/state must come from MessageService history.");
            Assert.That(FindButton("MessagesChoice_kaito_choice_guild"), Is.Null,
                "Consumed Yarn choices must disappear and cannot be double-submitted.");
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator WaitForButton(string name, int frames)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                if (FindButton(name) != null)
                {
                    yield break;
                }
                yield return null;
            }
            Assert.Fail("Timed out waiting for active Messages choice: " + name);
        }

        private IEnumerator WaitForRect(string name, int frames)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                if (FindRect(name) != null)
                {
                    yield break;
                }
                yield return null;
            }
            Assert.Fail("Timed out waiting for Messages UI element: " + name);
        }

        private bool ContainsTmpText(string value)
        {
            foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (string.Equals(text.text, value, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasOutgoingChoice(ConversationState conversation, string choiceId)
        {
            if (conversation == null || conversation.entries == null)
            {
                return false;
            }
            int matches = 0;
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && entry.outgoing && string.Equals(entry.choiceId, choiceId, StringComparison.Ordinal))
                {
                    matches++;
                }
            }
            return matches == 1;
        }

        private static bool HasCoordinateAttachment(ConversationState conversation, string attachmentId)
        {
            if (conversation == null || conversation.entries == null)
            {
                return false;
            }
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && entry.attachment != null &&
                    entry.attachment.kind == MessageAttachmentKind.Coordinates &&
                    string.Equals(entry.attachment.id, attachmentId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
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
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
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
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
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
