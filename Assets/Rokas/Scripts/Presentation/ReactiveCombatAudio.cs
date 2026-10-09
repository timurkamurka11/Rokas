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
        MonsterFleshContact, GuardMetalContact, DodgeBackstep, ThrowRelease, ThrowFleshContact,
        StancePreview, StanceConfirm, StanceCancel, SwordReadiness, HeavyWindup, EnemyWarning
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
        private const string FidelityAudioRoot = "Audio/CombatFidelity3/";

        private readonly RokasAudio audio;
        private readonly AudioClip[] normalVocals, heavyVocals, monsterAttacks, monsterIdle;
        private readonly AudioClip normalSwing, heavySwing, normalContact, heavyContact;
        private readonly AudioClip monsterContact, guardContact, dodgeMovement;
        private readonly AudioClip throwRelease, throwContact;
        private readonly AudioClip stancePreview, stanceConfirm, stanceCancel, swordReadiness, heavyWindup, enemyWarning;
        private readonly AudioClip[] fidelityClips;
        private readonly Dictionary<string, PreviewEntry> previewStates = new Dictionary<string, PreviewEntry>(128, StringComparer.Ordinal);
        private string activePreviewToken;
        private readonly HashSet<string> throwActions = new HashSet<string>(128);
        private readonly Dictionary<string, string> actionAliases = new Dictionary<string, string>(128, StringComparer.Ordinal);
        private readonly Dictionary<ActionActorIdentity, bool> heavyActions = new Dictionary<ActionActorIdentity, bool>(64);
        private readonly HashSet<AudioEventIdentity> played = new HashSet<AudioEventIdentity>(512);
        private readonly List<ReactiveCombatAudioDispatch> dispatches = new List<ReactiveCombatAudioDispatch>(256);
        private int normalVocalIndex, heavyVocalIndex, attackIndex, idleIndex;
        private float idleCountdown;

        public IReadOnlyList<ReactiveCombatAudioDispatch> Dispatches { get { return dispatches; } }
        public int PlaybackCount { get; private set; }
        public bool FidelityClipsReady
        {
            get
            {
                for (int i = 0; i < fidelityClips.Length; i++)
                    if (!fidelityClips[i] || fidelityClips[i].loadState != AudioDataLoadState.Loaded) return false;
                return true;
            }
        }

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
            stancePreview = LoadFidelity("StancePreview");
            stanceConfirm = LoadFidelity("StanceConfirm");
            stanceCancel = LoadFidelity("StanceCancel");
            swordReadiness = LoadFidelity("SwordReadiness");
            heavyWindup = LoadFidelity("HeavyWindup");
            enemyWarning = LoadFidelity("EnemyWarning");
            fidelityClips = new[] { stancePreview, stanceConfirm, stanceCancel, swordReadiness, heavyWindup, enemyWarning };
            // Decode/load and reserve collections before the first gameplay contact.
            Preload(normalVocals); Preload(heavyVocals); Preload(monsterAttacks); Preload(monsterIdle); Preload(fidelityClips);
            Preload(new[] { normalSwing, heavySwing, normalContact, heavyContact, monsterContact, guardContact, dodgeMovement, throwRelease, throwContact });
            Reset();
        }

        /// <summary>Accepted stance selection only. A held hover/sample must reuse the same token.</summary>
        public bool PresentPreview(string previewToken, string stanceId)
        {
            string stance = NormalizeStance(stanceId);
            if (string.IsNullOrWhiteSpace(previewToken) || stance == null) return false;
            PreviewEntry existing;
            if (previewStates.TryGetValue(previewToken, out existing))
            {
                if (activePreviewToken != previewToken || existing.State != PreviewState.Active || existing.Stance != stance) return false;
                return PlayOnce(previewToken, HunterId, null, ReactiveCombatAudioEvent.StancePreview, stancePreview, .18f);
            }
            if (!string.IsNullOrEmpty(activePreviewToken) && previewStates.TryGetValue(activePreviewToken, out existing) && existing.State == PreviewState.Active)
                previewStates[activePreviewToken] = new PreviewEntry(existing.Stance, PreviewState.Canceled);
            previewStates[previewToken] = new PreviewEntry(stance, PreviewState.Active);
            activePreviewToken = previewToken;
            DelayIdle(.75f);
            return PlayOnce(previewToken, HunterId, null, ReactiveCombatAudioEvent.StancePreview, stancePreview, .18f);
        }
        /// <summary>Call only after the current native preview has actually accepted confirmation.</summary>
        public bool PresentConfirm(string previewToken)
        {
            PreviewEntry entry;
            if (string.IsNullOrEmpty(previewToken) || activePreviewToken != previewToken || !previewStates.TryGetValue(previewToken, out entry) || entry.State == PreviewState.Canceled) return false;
            previewStates[previewToken] = new PreviewEntry(entry.Stance, PreviewState.Confirmed);
            DelayIdle(3f);
            return PlayOnce(previewToken, HunterId, null, ReactiveCombatAudioEvent.StanceConfirm, stanceConfirm, .24f);
        }
        /// <summary>Accepted cancel/focus-loss only. Never cancels a confirmed action or emits a later contact.</summary>
        public bool PresentCancel(string previewToken)
        {
            PreviewEntry entry;
            if (string.IsNullOrEmpty(previewToken) || activePreviewToken != previewToken || !previewStates.TryGetValue(previewToken, out entry) || entry.State == PreviewState.Confirmed) return false;
            previewStates[previewToken] = new PreviewEntry(entry.Stance, PreviewState.Canceled);
            return PlayOnce(previewToken, HunterId, null, ReactiveCombatAudioEvent.StanceCancel, stanceCancel, .16f);
        }
        /// <summary>Accepted Normal/Heavy action readiness. Keiko effort remains the existing vocal event.</summary>
        public bool PresentReadiness(string actionId, bool heavy)
        {
            DelayIdle(5f);
            return PlayOnce(Canonical(actionId), HunterId, null,
                heavy ? ReactiveCombatAudioEvent.HeavyWindup : ReactiveCombatAudioEvent.SwordReadiness,
                heavy ? heavyWindup : swordReadiness, heavy ? .26f : .21f);
        }
        /// <summary>Authoritative incoming attack start. This short Foley layer never replaces monster voice.</summary>
        public bool PresentEnemyWarning(string actionId, string enemyId)
        {
            if (string.IsNullOrWhiteSpace(enemyId) || enemyId == HunterId) return false;
            DelayIdle(5f);
            return PlayOnce(Canonical(actionId), enemyId, null, ReactiveCombatAudioEvent.EnemyWarning, enemyWarning, .23f);
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
            previewStates.Clear(); activePreviewToken = null;
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

        private static ActionActorIdentity ActionActorKey(string sequence, string actor)
        { return new ActionActorIdentity(sequence, actor); }

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
            AudioEventIdentity key = Key(sequence, actor, hit, eventId);
            if (played.Contains(key)) return true;
            if (!clip) return false;
            played.Add(key);
            audio.PlayCombatClip(clip, volume);
            PlaybackCount++;
            if (dispatches.Count == 256) dispatches.RemoveAt(0);
            dispatches.Add(new ReactiveCombatAudioDispatch(sequence, actor, hit, eventId, clip));
            return true;
        }

        private static AudioEventIdentity Key(string sequence, string actor, string hit, ReactiveCombatAudioEvent eventId)
        { return new AudioEventIdentity(sequence, actor, hit, eventId); }

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

        private static string NormalizeStance(string stance)
        {
            if (string.Equals(stance, "normal", StringComparison.OrdinalIgnoreCase) || string.Equals(stance, "Basic", StringComparison.OrdinalIgnoreCase)) return "normal";
            if (string.Equals(stance, "heavy", StringComparison.OrdinalIgnoreCase)) return "heavy";
            if (string.Equals(stance, "throw_blade", StringComparison.OrdinalIgnoreCase)) return "throw_blade";
            return null;
        }
        private enum PreviewState { Active, Confirmed, Canceled }
        private readonly struct PreviewEntry
        {
            public readonly string Stance; public readonly PreviewState State;
            public PreviewEntry(string stance, PreviewState state) { Stance = stance; State = state; }
        }
        private readonly struct ActionActorIdentity : IEquatable<ActionActorIdentity>
        {
            private readonly string sequence, actor;
            public ActionActorIdentity(string sequence, string actor) { this.sequence = sequence; this.actor = actor; }
            public bool Equals(ActionActorIdentity other) => string.Equals(sequence, other.sequence, StringComparison.Ordinal) && string.Equals(actor, other.actor, StringComparison.Ordinal);
            public override bool Equals(object other) => other is ActionActorIdentity identity && Equals(identity);
            public override int GetHashCode() { unchecked { return ((sequence == null ? 0 : StringComparer.Ordinal.GetHashCode(sequence)) * 397) ^ (actor == null ? 0 : StringComparer.Ordinal.GetHashCode(actor)); } }
        }
        private readonly struct AudioEventIdentity : IEquatable<AudioEventIdentity>
        {
            private readonly string sequence, actor, hit; private readonly ReactiveCombatAudioEvent eventId;
            public AudioEventIdentity(string sequence, string actor, string hit, ReactiveCombatAudioEvent eventId)
            { this.sequence = sequence; this.actor = actor; this.hit = hit; this.eventId = eventId; }
            public bool Equals(AudioEventIdentity other) => eventId == other.eventId && string.Equals(sequence, other.sequence, StringComparison.Ordinal) && string.Equals(actor, other.actor, StringComparison.Ordinal) && string.Equals(hit, other.hit, StringComparison.Ordinal);
            public override bool Equals(object other) => other is AudioEventIdentity identity && Equals(identity);
            public override int GetHashCode()
            {
                unchecked { int hash = sequence == null ? 0 : StringComparer.Ordinal.GetHashCode(sequence);
                    hash = hash * 397 ^ (actor == null ? 0 : StringComparer.Ordinal.GetHashCode(actor));
                    hash = hash * 397 ^ (hit == null ? 0 : StringComparer.Ordinal.GetHashCode(hit));
                    return hash * 397 ^ (int)eventId; }
            }
        }
        private static AudioClip LoadFidelity(string name) { return Resources.Load<AudioClip>(FidelityAudioRoot + name); }
        private static void Preload(AudioClip[] bank)
        {
            for (int i = 0; i < bank.Length; i++)
                if (bank[i] && bank[i].loadState != AudioDataLoadState.Loaded) bank[i].LoadAudioData();
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
