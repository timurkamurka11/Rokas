using NUnit.Framework;
using Rokas.Presentation;

namespace Rokas.Core.Tests
{
    public sealed class HubDialoguePlaybackTests
    {
        [Test]
        public void Open_StartsTypingAndTalkPortrait()
        {
            var state = NewState();
            Assert.That(
                state.Open(
                    new HubDialogueDefinition(
                        "window", "КЕЙКО", "Тестовая строка."),
                    FastSettings()),
                Is.True);

            Assert.That(state.IsOpen, Is.True);
            Assert.That(state.IsTyping, Is.True);
            Assert.That(
                state.PortraitState,
                Is.EqualTo(HubDialoguePortraitState.Talk));
            Assert.That(state.ShowCompletionIndicator, Is.False);
        }

        [Test]
        public void FirstAdvanceWhileTyping_CompletesOnlyCurrentLine()
        {
            var state = NewState();
            state.Open(
                new HubDialogueDefinition(
                    "window", "КЕЙКО", "Первая.", "Вторая."),
                SlowSettings());

            HubDialogueAdvanceResult result = state.RequestAdvance();

            Assert.That(
                result,
                Is.EqualTo(
                    HubDialogueAdvanceResult.CompletedCurrentLine));
            Assert.That(state.CurrentLineIndex, Is.EqualTo(0));
            Assert.That(state.IsTyping, Is.False);
            Assert.That(state.ShowCompletionIndicator, Is.True);
            Assert.That(
                state.PortraitState,
                Is.EqualTo(HubDialoguePortraitState.Idle));
        }

        [Test]
        public void CompletedLine_AdvanceMovesToNextAndRestartsTalk()
        {
            var state = NewState();
            state.Open(
                new HubDialogueDefinition(
                    "window", "КЕЙКО", "Первая.", "Вторая."),
                SlowSettings());
            state.RequestAdvance();

            HubDialogueAdvanceResult result = state.RequestAdvance();

            Assert.That(
                result,
                Is.EqualTo(HubDialogueAdvanceResult.AdvancedLine));
            Assert.That(state.CurrentLineIndex, Is.EqualTo(1));
            Assert.That(state.IsTyping, Is.True);
            Assert.That(state.ShowCompletionIndicator, Is.False);
            Assert.That(
                state.PortraitState,
                Is.EqualTo(HubDialoguePortraitState.Talk));
        }

        [Test]
        public void FinalCompletedLine_AdvanceClosesAndCanReopenCleanly()
        {
            var state = NewState();
            var definition =
                new HubDialogueDefinition(
                    "cat", "КЕЙКО", "Мамэ довольно щурится.");

            state.Open(definition, SlowSettings());
            state.RequestAdvance();
            Assert.That(
                state.RequestAdvance(),
                Is.EqualTo(HubDialogueAdvanceResult.Closed));
            Assert.That(state.IsOpen, Is.False);

            Assert.That(
                state.Open(definition, SlowSettings()),
                Is.True);
            Assert.That(state.CurrentLineIndex, Is.EqualTo(0));
            Assert.That(state.IsTyping, Is.True);
        }

        [Test]
        public void DuplicateOpen_DoesNotReplaceActiveDialogue()
        {
            var state = NewState();
            state.Open(
                new HubDialogueDefinition(
                    "window", "КЕЙКО", "Окно."),
                SlowSettings());

            bool opened = state.Open(
                new HubDialogueDefinition(
                    "cat", "КЕЙКО", "Кот."),
                SlowSettings());

            Assert.That(opened, Is.False);
            Assert.That(state.Definition.Id, Is.EqualTo("window"));
        }

        [Test]
        public void SharedTypewriter_MatchesVnSkipSemanticsAtBoundary()
        {
            RokasDialogueTypewriterSettings settings = SlowSettings();
            const string text = "AB";

            Assert.That(
                RokasDialogueTypewriter.CalculateVisibleCharacters(
                    text, 0f, settings),
                Is.EqualTo(0));
            Assert.That(
                RokasDialogueTypewriter.CalculateVisibleCharacters(
                    text, .11f, settings),
                Is.EqualTo(1));
            Assert.That(
                RokasDialogueTypewriter.CalculateVisibleCharacters(
                    text, .21f, settings),
                Is.EqualTo(2));
        }

        private static HubDialoguePlaybackState NewState()
        {
            return new HubDialoguePlaybackState();
        }

        private static RokasDialogueTypewriterSettings SlowSettings()
        {
            return new RokasDialogueTypewriterSettings
            {
                Enabled = true,
                CharactersPerSecond = 10f
            };
        }

        private static RokasDialogueTypewriterSettings FastSettings()
        {
            return new RokasDialogueTypewriterSettings
            {
                Enabled = true,
                CharactersPerSecond = 1f
            };
        }
    }
}
