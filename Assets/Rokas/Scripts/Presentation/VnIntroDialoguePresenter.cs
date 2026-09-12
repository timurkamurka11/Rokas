#nullable enable
using System;
using Yarn.Unity;

namespace Rokas.Presentation
{
    public sealed class VnIntroLineGate
    {
        private bool awaitingContinue;
        private bool paused;

        public bool IsAwaitingContinue => awaitingContinue;
        public bool IsPaused => paused;

        public void BeginLine()
        {
            if (awaitingContinue)
            {
                throw new InvalidOperationException("VN intro already has a line awaiting continuation.");
            }
            awaitingContinue = true;
        }

        public void SetPaused(bool value)
        {
            paused = value;
        }

        public bool TryContinue()
        {
            if (!awaitingContinue || paused)
            {
                return false;
            }
            awaitingContinue = false;
            return true;
        }

        public void Cancel()
        {
            awaitingContinue = false;
        }
    }

    public sealed class VnIntroDialoguePresenter : DialoguePresenterBase
    {
        private readonly VnIntroLineGate lineGate = new VnIntroLineGate();
        private YarnTaskCompletionSource? pendingLine;

        public bool HasPendingLine => lineGate.IsAwaitingContinue;
        public bool IsPaused => lineGate.IsPaused;

        public event Action<string, string>? LinePresented;

        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            if (pendingLine != null)
            {
                throw new InvalidOperationException("VN intro received a second line before the current line completed.");
            }

            lineGate.BeginLine();
            YarnTaskCompletionSource completion = new YarnTaskCompletionSource();
            pendingLine = completion;
            Action<string, string>? handler = LinePresented;
            handler?.Invoke(line.CharacterName ?? string.Empty, ExtractLineText(line));

            using (token.NextContentToken.Register(CancelCurrentLine))
            {
                await completion.Task;
            }

            if (ReferenceEquals(pendingLine, completion))
            {
                pendingLine = null;
                lineGate.Cancel();
            }
        }

        public bool Continue()
        {
            if (!lineGate.TryContinue())
            {
                return false;
            }

            YarnTaskCompletionSource? completion = pendingLine;
            pendingLine = null;
            completion?.TrySetResult();
            return completion != null;
        }

        public void SetPaused(bool paused)
        {
            lineGate.SetPaused(paused);
        }

        public void CancelCurrentLine()
        {
            lineGate.Cancel();
            YarnTaskCompletionSource? completion = pendingLine;
            pendingLine = null;
            completion?.TrySetResult();
        }

        public override YarnTask OnDialogueStartedAsync()
        {
            return YarnTask.CompletedTask;
        }

        public override YarnTask OnDialogueCompleteAsync()
        {
            CancelCurrentLine();
            return YarnTask.CompletedTask;
        }

        private static string ExtractLineText(LocalizedLine line)
        {
            string text = line.Text.Text.Trim();
            string characterName = line.CharacterName ?? string.Empty;
            if (!string.IsNullOrEmpty(characterName))
            {
                string prefix = characterName + ":";
                if (text.StartsWith(prefix, StringComparison.Ordinal))
                {
                    text = text.Substring(prefix.Length).TrimStart();
                }
            }
            return text;
        }
    }
}
