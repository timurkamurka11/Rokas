using UnityEngine;

namespace Rokas.Presentation
{
    public struct RokasDialogueTypewriterSettings
    {
        public bool Enabled;
        public float CharactersPerSecond;
        public float BaseCharacterDelay;
        public float CommaPause;
        public float PeriodPause;
        public float EllipsisPause;
        public float QuestionPause;
        public float ExclamationPause;
        public float LineStartDelay;

        public static RokasDialogueTypewriterSettings From(
            RokasVnRuntimePresentationSnapshot values)
        {
            values = values ?? new RokasVnRuntimePresentationSnapshot();
            return new RokasDialogueTypewriterSettings
            {
                Enabled = values.typewriterEnabled,
                CharactersPerSecond = values.typewriterCharactersPerSecond,
                BaseCharacterDelay = values.typewriterBaseCharacterDelay,
                CommaPause = values.typewriterCommaPause,
                PeriodPause = values.typewriterPeriodPause,
                EllipsisPause = values.typewriterEllipsisPause,
                QuestionPause = values.typewriterQuestionPause,
                ExclamationPause = values.typewriterExclamationPause,
                LineStartDelay = values.typewriterLineStartDelay
            };
        }

        public static RokasDialogueTypewriterSettings HubDefault =>
            new RokasDialogueTypewriterSettings
            {
                Enabled = true,
                CharactersPerSecond = 36f,
                BaseCharacterDelay = 0f,
                CommaPause = 0f,
                PeriodPause = 0f,
                EllipsisPause = 0f,
                QuestionPause = 0f,
                ExclamationPause = 0f,
                LineStartDelay = 0f
            };
    }

    public static class RokasDialogueTypewriter
    {
        public static int CalculateVisibleCharacters(
            string text,
            float elapsedSeconds,
            RokasDialogueTypewriterSettings values)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            if (!values.Enabled) return text.Length;
            if (elapsedSeconds < 0f) return 0;

            float time = Mathf.Max(0f, values.LineStartDelay);
            if (elapsedSeconds < time) return 0;

            float characterDelay =
                1f / Mathf.Max(.0001f, values.CharactersPerSecond) +
                Mathf.Max(0f, values.BaseCharacterDelay);
            int visible = 0;

            for (int i = 0; i < text.Length; i++)
            {
                time += characterDelay;
                if (elapsedSeconds + .00001f < time) return visible;
                visible = i + 1;
                time += GetPunctuationPause(text, i, values);
                if (elapsedSeconds + .00001f < time) return visible;
            }

            return visible;
        }

        private static float GetPunctuationPause(
            string text,
            int index,
            RokasDialogueTypewriterSettings values)
        {
            char c = text[index];
            if (c == ',') return values.CommaPause;
            if (c == '…') return values.EllipsisPause;
            if (c == '?') return values.QuestionPause;
            if (c == '!') return values.ExclamationPause;
            if (c != '.') return 0f;

            bool inAsciiEllipsis =
                (index > 0 && text[index - 1] == '.') ||
                (index + 1 < text.Length && text[index + 1] == '.');
            if (!inAsciiEllipsis) return values.PeriodPause;

            bool terminalEllipsisDot =
                index >= 2 &&
                text[index - 1] == '.' &&
                text[index - 2] == '.' &&
                (index + 1 >= text.Length || text[index + 1] != '.');

            return terminalEllipsisDot ? values.EllipsisPause : 0f;
        }
    }
}
