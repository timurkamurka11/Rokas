using System;
using System.Collections.Generic;
using Rokas.Core.ReactiveTurns;
using UnityEngine;

namespace Rokas.Presentation
{
    public enum ReactiveCombatAudioEvent
    {
        KeikoNormalVocal, KeikoHeavyVocal, NormalWhoosh, HeavyWhoosh,
        NormalFleshContact, HeavyFleshContact, MonsterVocal, MonsterSwing,
        MonsterFleshContact, GuardMetalContact, DodgeBackstep, ThrowRelease, ThrowFleshContact
    }

    public sealed class ReactiveCombatAudioDispatch
    {
        public string AttackSequenceId { get; private set; }
        public string ActorId { get; private set; }
        public string HitId { get; private set; }
        public ReactiveCombatAudioEvent EventId { get; private set; }
        public string ClipName { get; private set; }

        internal ReactiveCombatAudioDispatch(string sequence, string actor, string hit,
            ReactiveCombatAudioEvent eventId, AudioClip clip)
        {
            AttackSequenceId = sequence; ActorId = actor; HitId = hit;
            EventId = eventId; ClipName = clip.name;
        }
    }

    /// <summary>One playback per logical event, with independent vocal, acceleration and contact layers.</summary>
    public sealed class ReactiveCombatAudio
    {
        private const string AudioRoot = "Combat/ReactiveTurns/Audio/";
        private const string HunterId = "P";

        private readonly RokasAudio audio;
        private readonly AudioClip[] normalVocals, heavyVocals, monsterAttacks, monsterIdle;
        private readonly AudioClip normalSwing, heavySwing, normalContact, heavyContact;
        private readonly AudioClip monsterContact, guardContact, dodgeMovement;
        private readonly AudioClip throwRelease, throwContact;
        private readonly HashSet<string> throwActions = new HashSet<string>();
        private readonly Dictionary<string, string> actionAliases = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> heavyActions = new Dictionary<string, bool>();
        private readonly HashSet<string> played = new HashSet<string>();
        private readonly List<ReactiveCombatAudioDispatch> dispatches = new List<ReactiveCombatAudioDispatch>();
        private int normalVocalIndex, heavyVocalIndex, attackIndex, idleIndex;
        private float idleCountdown;

        public IReadOnlyList<ReactiveCombatAudioDispatch> Dispatches { get { return dispatches; } }
        public int PlaybackCount { get; private set; }

        public ReactiveCombatAudio(RokasAudio audio)
        {
            this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
            // Existing effort clips remain vocal layers; the former "hit attack" is
            // no longer substituted for a body contact.
            normalVocals = LoadBank("Keiko/Keiko attack sound 2", "Keiko/Keiko attack sound 3");
            heavyVocals = LoadBank("Keiko/Keiko hit attack", "Keiko/Keiko attack sound 4");
            monsterAttacks = LoadBank("Monsters/Monster attack sound",
                "Monsters/Monster attack sound 3", "Monsters/Attack monster 2");
            monsterIdle = LoadBank("Monsters/Sound stance", "Monsters/Monster stance sound 2",
                "Monsters/Monster stance sound 4");
            normalSwing = Load("PolishII/NormalWhoosh");
            heavySwing = Load("PolishII/HeavyWhoosh");
            normalContact = Load("PolishII/NormalFleshContact");
            heavyContact = Load("PolishII/HeavyFleshContact");
            monsterContact = Load("PolishII/MonsterFleshContact");
            guardContact = Load("PolishII/GuardMetalContact");
            dodgeMovement = Load("PolishII/DodgeBackstep");
            throwRelease = Load("FinalVfx/ThrowRelease");
            throwContact = Load("FinalVfx/ThrowContact");
            Reset();
        }

        /// <summary>Links the pre-commit animation ID to the authoritative Core action ID.</summary>
        public void BindActionId(string coreActionId, string presentationActionId)
        {
            if (string.IsNullOrEmpty(coreActionId) || string.IsNullOrEmpty(presentationActionId)) return;
            actionAliases[coreActionId] = Canonical(presentationActionId);
        }

        public bool PresentAttackStarted(string attackSequenceId, string actorId, bool heavy)
        {
            string sequence = Canonical(attackSequenceId);
            if (string.IsNullOrEmpty(sequence) || string.IsNullOrEmpty(actorId)) return false;
            heavyActions[ActionActorKey(sequence, actorId)] = heavy;
            DelayIdle(5f);
            if (actorId != HunterId)
                return PlayBankOnce(sequence, actorId, null, ReactiveCombatAudioEvent.MonsterVocal,
                    monsterAttacks, ref attackIndex, .34f);
            return heavy
                ? PlayBankOnce(sequence, actorId, null, ReactiveCombatAudioEvent.KeikoHeavyVocal,
                    heavyVocals, ref heavyVocalIndex, .38f)
                : PlayBankOnce(sequence, actorId, null, ReactiveCombatAudioEvent.KeikoNormalVocal,
                    normalVocals, ref normalVocalIndex, .32f);
        }

        /// <summary>Called by the sampled animation's acceleration window, never by button selection.</summary>
        public bool PresentSwing(string attackSequenceId, string actorId, bool heavy, string hitId = null)
        {
            string sequence = Canonical(attackSequenceId);
            if (string.IsNullOrEmpty(sequence) || string.IsNullOrEmpty(actorId)) return false;
            heavyActions[ActionActorKey(sequence, actorId)] = heavy;
            DelayIdle(5f);
            if (actorId != HunterId)
                return PlayOnce(sequence, actorId, hitId, ReactiveCombatAudioEvent.MonsterSwing, normalSwing, .22f);
            return PlayOnce(sequence, actorId, null,
                heavy ? ReactiveCombatAudioEvent.HeavyWhoosh : ReactiveCombatAudioEvent.NormalWhoosh,
                heavy ? heavySwing : normalSwing, heavy ? .43f : .30f);
        }

        public bool PresentThrowRelease(string actionId)
        {
            string sequence = Canonical(actionId);
            throwActions.Add(sequence);
            DelayIdle(5f);
            return PlayOnce(sequence, HunterId, null, ReactiveCombatAudioEvent.ThrowRelease, throwRelease, .32f);
        }

        /// <summary>All resolved contacts are handled here, including silent misses and defended damage.</summary>
        public bool PresentContact(string attackSequenceId, string actorId, bool heavy,
            string defenseOutcome, int amount, string hitId = null)
        {
            string sequence = Canonical(attackSequenceId);
            DelayIdle(5f);
            if (actorId == HunterId)
            {
                if (throwActions.Contains(sequence))
                {
                    if (amount > 0) PlayOnce(sequence, actorId, null, ReactiveCombatAudioEvent.ThrowFleshContact, throwContact, .49f);
                    return true;
                }
                // Sweep resolves several logical targets but owns one visual sword contact.
                if (amount <= 0) return true;
                PlayOnce(sequence, actorId, null,
                    heavy ? ReactiveCombatAudioEvent.HeavyFleshContact : ReactiveCombatAudioEvent.NormalFleshContact,
                    heavy ? heavyContact : normalContact, heavy ? .72f : .56f);
                return true;
            }
            if (IsDodge(defenseOutcome))
            {
                PlayOnce(sequence, actorId, hitId, ReactiveCombatAudioEvent.DodgeBackstep, dodgeMovement, .26f);
                return true;
            }
            if (IsGuard(defenseOutcome))
            {
                PlayOnce(sequence, actorId, hitId, ReactiveCombatAudioEvent.GuardMetalContact, guardContact, .64f);
                return true;
            }
            if (amount <= 0) return true;
            PlayOnce(sequence, actorId, hitId,
                ReactiveCombatAudioEvent.MonsterFleshContact, monsterContact, .57f);
            return true;
        }

        /// <summary>True on a handled contact suppresses the legacy generic hit hook.</summary>
        public bool Present(CombatEvent combatEvent)
        {
            if (combatEvent == null) return false;
            string sequence = Canonical(combatEvent.ActionId);
            switch (combatEvent.Kind)
            {
                case CombatEventKind.CommandCommitted:
                    if (combatEvent.Detail == "throw_blade")
                    {
                        throwActions.Add(sequence);
                        DelayIdle(5f);
                        return true;
                    }
                    if (!IsOffensiveCommand(combatEvent.Detail)) return false;
                    return PresentAttackStarted(sequence, combatEvent.ActorId,
                        string.Equals(combatEvent.Detail, "heavy", StringComparison.OrdinalIgnoreCase));

                case CombatEventKind.AttackStarted:
                    return PresentAttackStarted(sequence, combatEvent.ActorId,
                        IsHeavy(sequence, combatEvent.ActorId, combatEvent.Detail));

                case CombatEventKind.HitResolved:
                    return PresentContact(sequence, combatEvent.ActorId,
                        IsHeavy(sequence, combatEvent.ActorId, combatEvent.Detail),
                        combatEvent.Detail, combatEvent.Amount, combatEvent.HitId);

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
            AudioClip clip = Next(monsterIdle, ref idleIndex);
            if (clip) audio.PlayCombatClip(clip, .15f);
            idleCountdown = 8f + (idleIndex % 3) * 1.25f;
        }

        public void Reset()
        {
            idleCountdown = 6f;
            actionAliases.Clear(); heavyActions.Clear(); throwActions.Clear(); played.Clear(); dispatches.Clear();
            PlaybackCount = 0;
            normalVocalIndex = heavyVocalIndex = attackIndex = idleIndex = 0;
        }

        private string Canonical(string actionId)
        {
            string alias;
            return !string.IsNullOrEmpty(actionId) && actionAliases.TryGetValue(actionId, out alias)
                ? alias : actionId;
        }

        private bool IsHeavy(string sequence, string actor, string detail)
        {
            bool heavy;
            // Core counter events share the incoming enemy's sequence ID, while
            // the actor changes to P. Its animation metadata belongs to that actor.
            return !string.IsNullOrEmpty(sequence) &&
                heavyActions.TryGetValue(ActionActorKey(sequence, actor), out heavy) ? heavy :
                string.Equals(detail, "heavy", StringComparison.OrdinalIgnoreCase);
        }

        private static string ActionActorKey(string sequence, string actor)
        { return sequence + "|" + actor; }

        private bool PlayBankOnce(string sequence, string actor, string hit, ReactiveCombatAudioEvent eventId,
            AudioClip[] bank, ref int index, float volume)
        {
            if (played.Contains(Key(sequence, actor, hit, eventId))) return true;
            return PlayOnce(sequence, actor, hit, eventId, Next(bank, ref index), volume);
        }

        private bool PlayOnce(string sequence, string actor, string hit, ReactiveCombatAudioEvent eventId,
            AudioClip clip, float volume)
        {
            if (string.IsNullOrEmpty(sequence) || string.IsNullOrEmpty(actor)) return false;
            string key = Key(sequence, actor, hit, eventId);
            if (played.Contains(key)) return true;
            if (!clip) return false;
            played.Add(key);
            audio.PlayCombatClip(clip, volume);
            PlaybackCount++;
            if (dispatches.Count == 256) dispatches.RemoveAt(0);
            dispatches.Add(new ReactiveCombatAudioDispatch(sequence, actor, hit, eventId, clip));
            return true;
        }

        private static string Key(string sequence, string actor, string hit, ReactiveCombatAudioEvent eventId)
        { return sequence + "|" + actor + "|" + hit + "|" + eventId; }

        private static bool IsDodge(string detail)
        { return string.Equals(detail, "Dodge", StringComparison.OrdinalIgnoreCase); }

        private static bool IsGuard(string detail)
        {
            return string.Equals(detail, "Parry", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(detail, "Block", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(detail, "Perfect", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(detail, "Counter", StringComparison.OrdinalIgnoreCase);
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

        private static AudioClip Load(string name) { return Resources.Load<AudioClip>(AudioRoot + name); }

        private static AudioClip Next(AudioClip[] bank, ref int nextIndex)
        {
            for (int i = 0; i < bank.Length; i++)
            {
                AudioClip clip = bank[nextIndex++ % bank.Length];
                if (!clip) continue;
                return clip;
            }
            return null;
        }

        private static AudioClip[] LoadBank(params string[] names)
        {
            var bank = new AudioClip[names.Length];
            for (int i = 0; i < bank.Length; i++)
                bank[i] = Load(names[i]);
            return bank;
        }
    }
}
