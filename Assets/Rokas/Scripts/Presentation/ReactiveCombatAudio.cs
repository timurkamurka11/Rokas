using System;
using Rokas.Core.ReactiveTurns;
using UnityEngine;

namespace Rokas.Presentation
{
    /// <summary>Routes the imported ReactiveTurns cues through the game's existing SFX volume controls.</summary>
    public sealed class ReactiveCombatAudio
    {
        private const string AudioRoot = "Combat/ReactiveTurns/Audio/";
        private const string HunterId = "P";

        private readonly RokasAudio audio;
        private readonly AudioClip[] keikoSwings;
        private readonly AudioClip keikoImpact;
        private readonly AudioClip[] monsterAttacks;
        private readonly AudioClip[] monsterIdle;
        private int swingIndex;
        private int attackIndex;
        private int idleIndex;
        private float idleCountdown;
        private string lastHunterImpactActionId;

        public ReactiveCombatAudio(RokasAudio audio)
        {
            this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
            keikoSwings = LoadBank("Keiko/Keiko attack sound 2", "Keiko/Keiko attack sound 3",
                "Keiko/Keiko attack sound 4");
            keikoImpact = Resources.Load<AudioClip>(AudioRoot + "Keiko/Keiko hit attack");
            monsterAttacks = LoadBank("Monsters/Monster attack sound",
                "Monsters/Monster attack sound 3", "Monsters/Attack monster 2");
            monsterIdle = LoadBank("Monsters/Sound stance", "Monsters/Monster stance sound 2",
                "Monsters/Monster stance sound 4");
            Reset();
        }

        /// <summary>Returns true when this event's cue is handled, so the caller can avoid duplicate hit sounds.</summary>
        public bool Present(CombatEvent combatEvent)
        {
            if (combatEvent == null) return false;
            switch (combatEvent.Kind)
            {
                case CombatEventKind.CommandCommitted:
                    if (!IsOffensiveCommand(combatEvent.Detail)) return false;
                    DelayIdle(5f);
                    return PlayNext(keikoSwings, ref swingIndex, .48f);

                case CombatEventKind.AttackStarted:
                    if (combatEvent.ActorId == HunterId) return false;
                    DelayIdle(5f);
                    return PlayNext(monsterAttacks, ref attackIndex, .42f);

                case CombatEventKind.HitResolved:
                    // Dodged, parried, and missed blows carry no player damage cue.
                    if (combatEvent.ActorId != HunterId || combatEvent.Amount <= 0) return false;
                    // Sweep resolves several enemies together; one impact cue is enough.
                    if (!string.IsNullOrEmpty(combatEvent.ActionId) &&
                        combatEvent.ActionId == lastHunterImpactActionId) return true;
                    lastHunterImpactActionId = combatEvent.ActionId;
                    DelayIdle(5f);
                    if (!keikoImpact) return false;
                    audio.PlayCombatClip(keikoImpact, .56f);
                    return true;

                case CombatEventKind.WaveStarted:
                    idleCountdown = 5.5f;
                    return false;

                case CombatEventKind.Victory:
                case CombatEventKind.Defeat:
                case CombatEventKind.Error:
                    idleCountdown = 8f;
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>Call only while combat is visible; idleAllowed should require a living enemy and a quiet command phase.</summary>
        public void Tick(float deltaSeconds, bool idleAllowed)
        {
            if (!idleAllowed)
            {
                idleCountdown = Mathf.Max(idleCountdown, 1.5f);
                return;
            }

            idleCountdown -= Mathf.Max(0f, deltaSeconds);
            if (idleCountdown > 0f) return;
            // Keep ambience sparse and never start multiple variants in the same frame.
            PlayNext(monsterIdle, ref idleIndex, .18f);
            idleCountdown = 8f + (idleIndex % 3) * 1.25f;
        }

        public void Reset()
        {
            idleCountdown = 6f;
            lastHunterImpactActionId = null;
        }

        private static bool IsOffensiveCommand(string detail)
        {
            return string.Equals(detail, "Basic", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(detail, "seal_strike", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(detail, "sweep", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(detail, "heavy", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(detail, "anchor", StringComparison.OrdinalIgnoreCase);
        }

        private void DelayIdle(float minimumSeconds)
        {
            idleCountdown = Mathf.Max(idleCountdown, minimumSeconds);
        }

        private bool PlayNext(AudioClip[] bank, ref int nextIndex, float scale)
        {
            for (int i = 0; i < bank.Length; i++)
            {
                AudioClip clip = bank[nextIndex++ % bank.Length];
                if (!clip) continue;
                audio.PlayCombatClip(clip, scale);
                return true;
            }
            return false;
        }

        private static AudioClip[] LoadBank(params string[] names)
        {
            var bank = new AudioClip[names.Length];
            for (int i = 0; i < bank.Length; i++)
                bank[i] = Resources.Load<AudioClip>(AudioRoot + names[i]);
            return bank;
        }
    }
}
