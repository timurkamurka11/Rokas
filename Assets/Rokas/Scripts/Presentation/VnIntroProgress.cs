using UnityEngine;

namespace Rokas.Presentation
{
    public interface IVnIntroProgress
    {
        bool IsCompleted { get; }
        void MarkCompleted();
    }

    public sealed class PlayerPrefsVnIntroProgress : IVnIntroProgress
    {
        public const string CompletedKey = "rokas.vn_intro.completed.v1";

        public bool IsCompleted => PlayerPrefs.GetInt(CompletedKey, 0) == 1;

        public void MarkCompleted()
        {
            PlayerPrefs.SetInt(CompletedKey, 1);
            PlayerPrefs.Save();
        }
    }
}
