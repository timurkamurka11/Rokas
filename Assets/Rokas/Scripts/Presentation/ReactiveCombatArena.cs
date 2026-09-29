using System;
using System.Collections.Generic;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    // A separate camera renders only the animated actors into the illustrated arena.
    // Combat rules and targeting remain in Core and the HUD; this layer is visual only.
    public sealed class ReactiveCombatArena
    {
        private const int ActorLayer = 30;
        private readonly Dictionary<string, ReactiveCombatActorVisual> enemies =
            new Dictionary<string, ReactiveCombatActorVisual>(StringComparer.Ordinal);
        private readonly Dictionary<string, ActorMotion> enemyMotions =
            new Dictionary<string, ActorMotion>(StringComparer.Ordinal);
        private readonly Dictionary<string, CorpseState> corpses =
            new Dictionary<string, CorpseState>(StringComparer.Ordinal);
        private readonly List<string> retired = new List<string>();
        private enum MotionPhase { None, Approach, AtStrike, Return }
        private enum HunterCommandPose { None, Attack, Heavy, Defend }
        private sealed class ActorMotion
        {
            public ReactiveCombatActorVisual Actor;
            public Vector3 Home;
            public Vector3 Start;
            public Vector3 Goal;
            public float Elapsed;
            public float Duration;
            public MotionPhase Phase;
            public bool Heavy;
            public float StrikeLead;
            public float ReturnDelay;
            public float ActionElapsed;
            public int NextHitIndex;
            public AttackSequenceDefinition AttackSequence;
        }

        private sealed class CorpseState
        {
            public ReactiveCombatActorVisual Actor;
            public Vector3 Origin;
            public float Elapsed;
        }

        private readonly ActorMotion hunterMotion = new ActorMotion();
        private HunterCommandPose hunterCommandPose;
        private string hunterMotionTargetId;
        private bool hunterApproachComplete;
        private float hunterAnticipationRemaining;
        private bool hunterPosePreplayed;
        private float hitStopRemaining;
        private GameObject world;
        private Camera camera;
        private RenderTexture texture;
        private RawImage image;
        private ReactiveCombatActorVisual hunter;
        private string heavyHunterActionId;
        private string heavyEnemyActionId;

        // Presentation timing is independent of Core's attack and defense windows.
        public float HunterApproachDuration { get; set; } = .84f;
        public float HunterReturnDuration { get; set; } = .72f;
        public float HunterAttackDistance { get; set; } = 1.35f;
        public float EnemyApproachDuration { get; set; } = .65f;
        public float EnemyReturnDuration { get; set; } = .68f;
        public float EnemyAttackDistance { get; set; } = 1.3f;
        public float DeathFallDuration { get; set; } = 1.35f;
        public float CorpseHoldDuration { get; set; } = 1.7f;
        public float CorpseSinkDuration { get; set; } = 1.2f;
        public float CorpseSinkDepth { get; set; } = 3.2f;
        public float HitStopDuration { get; set; } = .08f;
        public float ResultLingerDuration { get; set; } = .26f;

        public bool Ready { get { return world != null && hunter != null && texture != null; } }
        public int VisibleEnemyCount { get { return enemies.Count; } }
        public int CorpseCount => corpses.Count;
        public float HitStopRemaining => hitStopRemaining;
        public Vector3 HunterHome => hunterMotion.Home;
        public Vector3 HunterPosition => hunter == null ? Vector3.zero : hunter.transform.localPosition;
        public bool HunterApproachComplete { get { return hunterApproachComplete &&
            hunterMotion.Phase == MotionPhase.AtStrike; } }
        public bool HunterAtHome { get { return hunter != null && hunterMotion.Phase == MotionPhase.None &&
            (hunter.transform.localPosition - hunterMotion.Home).sqrMagnitude < .0001f; } }

        public bool EnemyAtHome(string id)
        {
            ActorMotion motion;
            return !string.IsNullOrEmpty(id) && enemyMotions.TryGetValue(id, out motion) &&
                motion.Actor != null && motion.Phase == MotionPhase.None &&
                (motion.Actor.transform.localPosition - motion.Home).sqrMagnitude < .0001f;
        }

        public bool EnemyApproachComplete(string id)
        {
            ActorMotion motion;
            return !string.IsNullOrEmpty(id) && enemyMotions.TryGetValue(id, out motion) &&
                motion.Phase == MotionPhase.AtStrike;
        }

        public Vector3 EnemyHome(string id)
        {
            ActorMotion motion;
            return id != null && enemyMotions.TryGetValue(id, out motion) ? motion.Home : Vector3.zero;
        }

        public Vector3 EnemyPosition(string id)
        {
            ReactiveCombatActorVisual actor;
            return id != null && enemies.TryGetValue(id, out actor) && actor != null
                ? actor.transform.localPosition : Vector3.zero;
        }

        public bool IsCorpse(string id) => id != null && corpses.ContainsKey(id);

        public float CorpseElapsed(string id)
        {
            CorpseState corpse;
            return id != null && corpses.TryGetValue(id, out corpse) ? corpse.Elapsed : -1f;
        }

        public ReactiveCombatArena(UiKit ui, RectTransform parent)
        {
            image = ui.Art(parent, "ReactiveAnimatedWorld", null, 0, 0, 1920, 906);
            image.color = Color.white;
            world = new GameObject("ReactiveCombatWorld");
            // Keep models outside all normal cameras, including PlayMode screenshot cameras.
            world.transform.position = new Vector3(1000f, 1000f, 1000f);
            texture = new RenderTexture(1920, 906, 24, RenderTextureFormat.ARGB32)
            {
                name = "ReactiveCombatActors",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                useMipMap = false
            };
            texture.Create();
            image.texture = texture;

            var cameraObject = new GameObject("ReactiveActorCamera", typeof(Camera));
            cameraObject.transform.SetParent(world.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 2.25f, -20f);
            cameraObject.transform.localRotation = Quaternion.identity;
            camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.6f;
            camera.aspect = 1920f / 906f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << ActorLayer;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.targetTexture = texture;

            var keyObject = new GameObject("ReactiveActorKey", typeof(Light));
            keyObject.transform.SetParent(world.transform, false);
            keyObject.transform.localRotation = Quaternion.Euler(32f, -36f, 0f);
            var key = keyObject.GetComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(.76f, .86f, 1f);
            key.intensity = 1.7f;
            key.cullingMask = 1 << ActorLayer;
            key.shadows = LightShadows.None;

            var rimObject = new GameObject("ReactiveActorRim", typeof(Light));
            rimObject.transform.SetParent(world.transform, false);
            rimObject.transform.localRotation = Quaternion.Euler(25f, 145f, 0f);
            var rim = rimObject.GetComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(1f, .19f, .32f);
            rim.intensity = .9f;
            rim.cullingMask = 1 << ActorLayer;
            rim.shadows = LightShadows.None;

            hunter = ReactiveCombatActorVisual.Spawn(CombatActorKind.Keiko, world.transform);
            if (hunter != null)
            {
                hunter.transform.localPosition = new Vector3(-5.25f, -.62f, 0f);
                hunterMotion.Actor = hunter;
                hunterMotion.Home = hunter.transform.localPosition;
                hunter.transform.localRotation = Quaternion.identity;
                hunter.SetStandingHeight(4.0f);
                SetLayerRecursive(hunter.gameObject, ActorLayer);
                hunter.SetFacing(true);
                hunter.PlayIdle();
            }
        }

        public void SetEnemies(IReadOnlyList<string> activeIds, string actingId)
        {
            SetEnemies(activeIds, actingId, activeIds);
        }

        public void SetEnemies(IReadOnlyList<string> activeIds, string actingId,
            IReadOnlyList<string> waveEnemyIds)
        {
            if (world == null) return;
            var active = new HashSet<string>(StringComparer.Ordinal);
            if (activeIds != null)
            {
                for (int i = 0; i < activeIds.Count; i++)
                {
                    string id = activeIds[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    active.Add(id);
                    ReactiveCombatActorVisual actor;
                    if (!enemies.TryGetValue(id, out actor) || actor == null)
                    {
                        actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Yokai, world.transform);
                        if (actor == null) continue;
                        enemies[id] = actor;
                        actor.SetStandingHeight(3.65f);
                        SetLayerRecursive(actor.gameObject, ActorLayer);
                        actor.SetFacing(false);
                        actor.PlayWaveEnter();
                    }
                }
            }
            retired.Clear();
            foreach (var pair in enemies)
                if (!active.Contains(pair.Key) && !corpses.ContainsKey(pair.Key)) retired.Add(pair.Key);
            for (int i = 0; i < retired.Count; i++) Retire(retired[i]);
            Layout(activeIds, waveEnemyIds != null && waveEnemyIds.Count > 0 ? waveEnemyIds : activeIds);
            if (hunterMotion.Phase == MotionPhase.Approach && hunterMotionTargetId != null &&
                !active.Contains(hunterMotionTargetId)) CancelHunterMotion();
        }

        private void Layout(IReadOnlyList<string> activeIds, IReadOnlyList<string> waveEnemyIds)
        {
            if (activeIds == null) return;
            int count = waveEnemyIds == null ? activeIds.Count : waveEnemyIds.Count;
            for (int activeIndex = 0; activeIndex < activeIds.Count; activeIndex++)
            {
                string id = activeIds[activeIndex];
                ReactiveCombatActorVisual actor;
                if (!enemies.TryGetValue(id, out actor) || actor == null) continue;
                int i = activeIndex;
                if (waveEnemyIds != null)
                    for (int waveSlot = 0; waveSlot < waveEnemyIds.Count; waveSlot++)
                        if (waveEnemyIds[waveSlot] == id) { i = waveSlot; break; }
                float x;
                float y;
                float z;
                float scale;
                if (count == 1)
                {
                    x = 4.1f; y = -.54f; z = 0f; scale = 1f;
                }
                else if (count == 2)
                {
                    x = i == 0 ? 2.55f : 5.65f;
                    y = i == 0 ? -.4f : -.64f;
                    z = i == 0 ? 1f : 0f;
                    scale = i == 0 ? .85f : .93f;
                }
                else if (count == 3)
                {
                    x = i == 0 ? 2.3f : i == 1 ? 4.55f : 6.65f;
                    y = i == 1 ? -.38f : -.66f;
                    z = i == 1 ? 1.5f : 0f;
                    scale = i == 1 ? .77f : .83f;
                }
                else
                {
                    x = 1.75f + 1.75f * i;
                    y = i == 1 || i == 2 ? -.38f : -.66f;
                    z = i == 1 || i == 2 ? 1.5f : 0f;
                    scale = .71f;
                }
                Vector3 slot = new Vector3(x, y, z);
                ActorMotion motion;
                if (!enemyMotions.TryGetValue(id, out motion))
                {
                    motion = new ActorMotion { Actor = actor };
                    enemyMotions.Add(id, motion);
                }
                motion.Home = slot;
                // Refresh runs every frame. Keep the attacker's world position while it moves
                // or strikes; only the durable slot is updated from the wave layout.
                if (motion.Phase == MotionPhase.None) actor.transform.localPosition = slot;
                actor.transform.localScale = Vector3.one * scale;
            }
        }

        // Movement precedes the committed command. Core still owns the impact and damage.
        public bool StartHunterApproach(string targetId, bool heavy = false)
        {
            ReactiveCombatActorVisual target;
            if (hunter == null || string.IsNullOrEmpty(targetId) ||
                !enemies.TryGetValue(targetId, out target) || target == null ||
                target.IsDead || corpses.ContainsKey(targetId)) return false;
            if (hunterMotion.Phase == MotionPhase.Approach && hunterMotionTargetId == targetId) return true;
            // A new player turn may begin while the previous enemy is returning.
            // Aim at its durable slot; its return finishes before Keiko's approach.
            Vector3 targetPosition = enemyMotions.TryGetValue(targetId, out ActorMotion targetMotion)
                ? targetMotion.Home : target.transform.localPosition;
            Vector3 attackPoint = new Vector3(targetPosition.x - HunterAttackDistance, hunterMotion.Home.y,
                targetPosition.z - .45f);
            hunterMotionTargetId = targetId;
            hunterApproachComplete = false;
            hunterPosePreplayed = false;
            hunterMotion.Heavy = heavy;
            hunterCommandPose = heavy ? HunterCommandPose.Heavy : HunterCommandPose.Attack;
            hunterAnticipationRemaining = Mathf.Max(0f, hunter.AttackContactSeconds(heavy) - .2f);
            BeginMotion(hunterMotion, MotionPhase.Approach, attackPoint, HunterApproachDuration);
            hunter.PlayApproach(hunterMotion.Duration);
            return true;
        }

        public void StartHunterReturn()
        {
            if (hunter == null || hunterMotion.Phase == MotionPhase.Return) return;
            hunterCommandPose = HunterCommandPose.None;
            hunterMotionTargetId = null;
            hunterApproachComplete = false;
            hunterPosePreplayed = false;
            hunterAnticipationRemaining = 0f;
            if ((hunter.transform.localPosition - hunterMotion.Home).sqrMagnitude < .0001f)
            {
                hunter.transform.localPosition = hunterMotion.Home;
                hunterMotion.Phase = MotionPhase.None;
                return;
            }
            BeginMotion(hunterMotion, MotionPhase.Return, hunterMotion.Home, HunterReturnDuration);
            hunter.PlayReturnHome(hunterMotion.Duration);
        }

        public void CancelHunterMotion()
        {
            hunterMotion.Phase = MotionPhase.None;
            hunterMotion.ReturnDelay = 0f;
            hunterCommandPose = HunterCommandPose.None;
            hunterMotionTargetId = null;
            hunterApproachComplete = false;
            hunterPosePreplayed = false;
            hunterAnticipationRemaining = 0f;
            if (hunter == null) return;
            hunter.transform.localPosition = hunterMotion.Home;
            hunter.PlayIdle();
        }

        private static void BeginMotion(ActorMotion motion, MotionPhase phase, Vector3 goal, float seconds)
        {
            motion.Phase = phase;
            motion.Start = motion.Actor.transform.localPosition;
            motion.Goal = goal;
            motion.Elapsed = 0f;
            motion.Duration = Mathf.Max(.1f, seconds);
            motion.ReturnDelay = 0f;
        }

        private void StartEnemyApproach(string id, bool heavy, AttackSequenceDefinition sequence)
        {
            ActorMotion motion;
            if (id == null || !enemyMotions.TryGetValue(id, out motion) ||
                motion.Actor == null || motion.Actor.IsDead || corpses.ContainsKey(id)) return;
            motion.Heavy = heavy;
            motion.AttackSequence = sequence;
            motion.ActionElapsed = 0f;
            motion.NextHitIndex = 0;
            motion.StrikeLead = Mathf.Max(.1f, (heavy ? 1.45f : 1f) - EnemyApproachDuration);
            // The defender's durable home is used even if a previous player action is
            // still returning. Each attacker keeps its own independent stable slot.
            Vector3 goal = new Vector3(hunterMotion.Home.x + EnemyAttackDistance,
                motion.Home.y, hunterMotion.Home.z + .1f);
            BeginMotion(motion, MotionPhase.Approach, goal, EnemyApproachDuration);
            motion.Actor.PlayApproach(motion.Duration);
        }

        private void StartEnemyReturn(string id)
        {
            ActorMotion motion;
            if (id == null || !enemyMotions.TryGetValue(id, out motion) ||
                motion.Actor == null || motion.Actor.IsDead) return;
            BeginMotion(motion, MotionPhase.Return, motion.Home, EnemyReturnDuration);
            motion.Actor.PlayReturnHome(motion.Duration);
        }

        public void Present(CombatEvent combatEvent, AttackSequenceDefinition attackSequence = null)
        {
            if (combatEvent == null) return;
            ReactiveCombatActorVisual actor;
            switch (combatEvent.Kind)
            {
                case CombatEventKind.CommandCommitted:
                    heavyHunterActionId = combatEvent.Detail == "heavy" ? combatEvent.ActionId : null;
                    if (hunter == null) break;
                    hunterCommandPose = combatEvent.Detail == "heavy" ? HunterCommandPose.Heavy :
                        combatEvent.Detail == "Defend" ? HunterCommandPose.Defend :
                        combatEvent.Detail == "Retreat" ? HunterCommandPose.None : HunterCommandPose.Attack;
                    if (hunterMotion.Phase != MotionPhase.Approach && !hunterPosePreplayed)
                        PlayHunterCommandPose(.2f);
                    break;
                case CombatEventKind.AttackStarted:
                    heavyEnemyActionId = combatEvent.Detail == "heavy" ? combatEvent.ActionId : null;
                    StartEnemyApproach(combatEvent.ActorId, combatEvent.Detail == "heavy", attackSequence);
                    break;
                case CombatEventKind.HitResolved:
                    if (combatEvent.TargetId == ReactiveDuelDefinitions.HunterId)
                    {
                        if (hunter != null)
                        {
                            if (combatEvent.Detail == "Dodge") hunter.PlayDodge();
                            else if (combatEvent.Detail == "Parry" || combatEvent.Detail == "Perfect")
                                hunter.PlayGuard();
                            else if (combatEvent.Amount > 0)
                            {
                                if (heavyEnemyActionId != null && combatEvent.ActionId == heavyEnemyActionId)
                                    hunter.PlayStagger();
                                else hunter.PlayHit();
                                hunter.Recoil(.24f);
                            }
                            hunter.HoldPresentation(HitStopDuration);
                        }
                        if (combatEvent.ActorId != null && enemies.TryGetValue(combatEvent.ActorId, out actor) && actor != null)
                        {
                            actor.HoldAttackAtContact(combatEvent.ActionId == heavyEnemyActionId, HitStopDuration);
                        }
                    }
                    else if (combatEvent.TargetId != null && enemies.TryGetValue(combatEvent.TargetId, out actor) && actor != null)
                    {
                        if (combatEvent.Amount > 0)
                        {
                            if (heavyHunterActionId != null && combatEvent.ActionId == heavyHunterActionId)
                                actor.PlayStagger();
                            else actor.PlayHit();
                            actor.Recoil(combatEvent.ActionId == heavyHunterActionId ? .32f : .2f);
                        }
                        actor.HoldPresentation(HitStopDuration);
                        if (hunter != null)
                            hunter.HoldAttackAtContact(combatEvent.ActionId == heavyHunterActionId, HitStopDuration);
                    }
                    hitStopRemaining = Mathf.Max(hitStopRemaining, HitStopDuration);
                    break;
                case CombatEventKind.Defeat:
                    CancelHunterMotion();
                    if (hunter != null) hunter.PlayDeath(DeathFallDuration);
                    break;
                case CombatEventKind.WaveCleared:
                    QueueHunterReturn();
                    foreach (var pair in enemies) Retire(pair.Key);
                    break;
                case CombatEventKind.ActionSettled:
                    if (combatEvent.ActorId == ReactiveDuelDefinitions.HunterId) QueueHunterReturn();
                    else StartEnemyReturn(combatEvent.ActorId);
                    break;
                case CombatEventKind.CommandCancelled:
                    StartHunterReturn();
                    break;
            }
        }

        private void QueueHunterReturn()
        {
            if (hunter == null || hunter.IsDead || hunterMotion.Phase == MotionPhase.Return) return;
            if ((hunter.transform.localPosition - hunterMotion.Home).sqrMagnitude < .0001f)
            {
                StartHunterReturn();
                return;
            }
            hunterMotion.ReturnDelay = Mathf.Max(hunterMotion.ReturnDelay, ResultLingerDuration);
        }

        private void PlayHunterCommandPose(float secondsUntilContact)
        {
            if (hunter == null) return;
            switch (hunterCommandPose)
            {
                case HunterCommandPose.Attack: hunter.PlayAttackToContact(secondsUntilContact); break;
                case HunterCommandPose.Heavy: hunter.PlayHeavyToContact(secondsUntilContact); break;
                case HunterCommandPose.Defend: hunter.PlayDefend(); break;
            }
        }

        private void Retire(string id)
        {
            ReactiveCombatActorVisual actor;
            if (string.IsNullOrEmpty(id) || corpses.ContainsKey(id) ||
                !enemies.TryGetValue(id, out actor) || actor == null) return;
            if (hunterMotion.Phase == MotionPhase.Approach && hunterMotionTargetId == id)
                CancelHunterMotion();
            ActorMotion motion;
            if (enemyMotions.TryGetValue(id, out motion)) motion.Phase = MotionPhase.None;
            actor.PlayDeath(DeathFallDuration);
            corpses[id] = new CorpseState { Actor = actor, Origin = actor.transform.localPosition };
        }

        public void Tick(float deltaTime)
        {
            if (world == null) return;
            float step = Mathf.Max(0f, deltaTime);
            float motionStep = step;
            if (hitStopRemaining > 0f)
            {
                float held = Mathf.Min(motionStep, hitStopRemaining);
                hitStopRemaining -= held;
                motionStep -= held;
            }
            hunter?.TickPresentation(step);
            foreach (var pair in enemies) pair.Value?.TickPresentation(step);
            if (hunter != null)
            {
                if (hunterMotion.ReturnDelay > 0f)
                {
                    hunterMotion.ReturnDelay = Mathf.Max(0f, hunterMotion.ReturnDelay - motionStep);
                    if (hunterMotion.ReturnDelay <= 0f) StartHunterReturn();
                }
                else if (TickMotion(hunterMotion, motionStep))
                {
                    if (hunterMotion.Phase == MotionPhase.AtStrike)
                    {
                        float contact = hunter.AttackContactSeconds(hunterMotion.Heavy);
                        PlayHunterCommandPose(contact);
                        hunterPosePreplayed = true;
                        hunterApproachComplete = hunterAnticipationRemaining <= 0f;
                    }
                    else hunter.PlayIdle();
                }
                else if (hunterMotion.Phase == MotionPhase.AtStrike && !hunterApproachComplete)
                {
                    hunterAnticipationRemaining = Mathf.Max(0f, hunterAnticipationRemaining - motionStep);
                    hunterApproachComplete = hunterAnticipationRemaining <= 0f;
                }
            }
            foreach (var pair in enemyMotions)
            {
                ActorMotion motion = pair.Value;
                if (corpses.ContainsKey(pair.Key) || motion.Actor == null) continue;
                if (motion.Phase == MotionPhase.Approach || motion.Phase == MotionPhase.AtStrike)
                    motion.ActionElapsed += step;
                if (TickMotion(motion, motionStep))
                {
                    if (motion.Phase == MotionPhase.AtStrike)
                        PlayEnemyStrike(motion);
                    else motion.Actor.PlayIdle();
                }
                else if (motion.Phase == MotionPhase.AtStrike && motion.AttackSequence != null &&
                    motion.NextHitIndex < motion.AttackSequence.Hits.Count &&
                    motion.Actor.HitStopRemaining <= 0f)
                {
                    float until = (float)(motion.AttackSequence.Hits[motion.NextHitIndex].ImpactUs / 1000000d) -
                        motion.ActionElapsed;
                    if (until <= Mathf.Min(.55f, motion.Actor.AttackContactSeconds(motion.Heavy)))
                        PlayEnemyStrike(motion);
                }
            }
            retired.Clear();
            float sinkStarts = Mathf.Max(.1f, DeathFallDuration) + Mathf.Max(0f, CorpseHoldDuration);
            float sinkDuration = Mathf.Max(.1f, CorpseSinkDuration);
            foreach (var pair in corpses)
            {
                CorpseState corpse = pair.Value;
                corpse.Elapsed += motionStep;
                if (corpse.Elapsed >= sinkStarts + sinkDuration) { retired.Add(pair.Key); continue; }
                if (corpse.Actor != null && corpse.Elapsed >= sinkStarts)
                    corpse.Actor.transform.localPosition = corpse.Origin - Vector3.up *
                        (CorpseSinkDepth * Mathf.Clamp01((corpse.Elapsed - sinkStarts) / sinkDuration));
            }
            for (int i = 0; i < retired.Count; i++)
            {
                string id = retired[i];
                ReactiveCombatActorVisual actor;
                if (enemies.TryGetValue(id, out actor) && actor != null)
                    UnityEngine.Object.Destroy(actor.gameObject);
                enemies.Remove(id);
                corpses.Remove(id);
                enemyMotions.Remove(id);
            }
        }

        private static void PlayEnemyStrike(ActorMotion motion)
        {
            float until = motion.StrikeLead;
            bool heavy = motion.Heavy;
            if (motion.AttackSequence != null && motion.NextHitIndex < motion.AttackSequence.Hits.Count)
            {
                HitDefinition hit = motion.AttackSequence.Hits[motion.NextHitIndex];
                until = (float)(hit.ImpactUs / 1000000d) - motion.ActionElapsed;
                heavy = hit.IsHeavy;
            }
            motion.NextHitIndex++;
            if (heavy) motion.Actor.PlayHeavyToContact(Mathf.Max(.08f, until));
            else motion.Actor.PlayAttackToContact(Mathf.Max(.08f, until));
        }

        private static bool TickMotion(ActorMotion motion, float step)
        {
            if (motion.Actor == null || motion.Actor.IsDead ||
                (motion.Phase != MotionPhase.Approach && motion.Phase != MotionPhase.Return)) return false;
            motion.Elapsed = Mathf.Min(motion.Duration, motion.Elapsed + step);
            float fraction = Mathf.Clamp01(motion.Elapsed / motion.Duration);
            // Ease only the first/last tenth. Cruising speed is nearly constant and
            // never has SmoothStep's pronounced 1.5x middle-of-run speed peak.
            const float edge = .1f;
            float progress = fraction < edge ? fraction * fraction / (2f * edge * (1f - edge)) :
                fraction > 1f - edge ? 1f - (1f - fraction) * (1f - fraction) /
                    (2f * edge * (1f - edge)) : (fraction - edge * .5f) / (1f - edge);
            motion.Actor.transform.localPosition = Vector3.Lerp(motion.Start, motion.Goal, progress);
            if (fraction < 1f) return false;
            motion.Actor.transform.localPosition = motion.Goal;
            motion.Phase = motion.Phase == MotionPhase.Approach ? MotionPhase.AtStrike : MotionPhase.None;
            return true;
        }

        public void Dispose()
        {
            if (camera != null) camera.targetTexture = null;
            if (image != null) image.texture = null;
            if (world != null) UnityEngine.Object.Destroy(world);
            if (texture != null)
            {
                texture.Release();
                UnityEngine.Object.Destroy(texture);
            }
            world = null;
            camera = null;
            texture = null;
            image = null;
            hunter = null;
            enemies.Clear();
            corpses.Clear();
            enemyMotions.Clear();
            hunterMotion.Phase = MotionPhase.None;
        }

        private static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursive(child.gameObject, layer);
        }
    }
}
