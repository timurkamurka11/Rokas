using System;
using UnityEngine;

namespace Rokas.Presentation
{
    // Core owns the action and its timing; this component only plays a pose.
    public sealed class ReactiveCombatActorVisual : MonoBehaviour
    {
        private const string LibraryPath = "Combat/ReactiveCombatActorLibrary";
        private ReactiveCombatActorClips clips;
        private Transform modelRoot;
        private Animation animationPlayer;
        private Transform[] poseTransforms;
        private Vector3[] blendPositions;
        private Quaternion[] blendRotations;
        private Vector3[] blendScales;
        private Renderer[] actorRenderers;
        private ReactiveCombatWeaponAttachment weaponAttachment;
        private ReactiveCombatTwoHandGrip twoHandGrip;
        private GameObject grippedWeapon;
        private readonly Transform[] gripFingers = new Transform[4];
        private readonly Quaternion[] sampledFingerRotations = new Quaternion[4];
        private bool gripApplied;
        private string activeAlias;
        private string previousAlias;
        private float activeSpeed = 1f;
        private float previousSpeed = 1f;
        private float blendElapsed;
        private const float BlendDuration = .08f;
        private float hitStopRemaining;
        private float recoilRemaining;
        private float recoilDistance;
        private float dodgeRemaining;
        private float anchoredY;
        private float poseSecondsLeft;
        private float proceduralSecondsLeft;
        private float proceduralDuration;
        private float proceduralLean;
        private float fallbackDeathAngle;
        private float fallbackDeathElapsed;
        private float fallbackDeathDuration;
        private bool dead;
        private bool facingRight;
        private Vector2 anchoredXZ;

        public Transform ModelRoot => modelRoot;
        public bool IsDead => dead;
        public bool IsFacingRight => facingRight;
        public float HitStopRemaining => hitStopRemaining;
        public string CurrentPose => activeAlias;
        public bool ActionRecoveryComplete => poseSecondsLeft <= 0f && hitStopRemaining <= 0f;
        public bool IdleSettled => activeAlias == "Idle" &&
            (previousAlias == null || blendElapsed >= BlendDuration) && hitStopRemaining <= 0f;
        public ReactiveCombatWeaponAttachment WeaponAttachment => weaponAttachment;
        public float WeaponGripCurlDegrees { get; set; } = 28f;
        public float EnterBattleDuration => clips.enterBattle == null ? 1.2f : clips.enterBattle.length;

        public float PreparationDuration(bool heavy)
        {
            AnimationClip clip = heavy ? clips.heavyPreparation : clips.preparation;
            return clip == null ? (heavy ? .4f : .27f) : clip.length;
        }

        public float AttackDuration(bool heavy)
        {
            AnimationClip clip = heavy ? clips.heavy : clips.attack;
            return clip == null ? (heavy ? .8f : .55f) : clip.length;
        }

        public float AttackContactSeconds(bool heavy)
        {
            AnimationClip clip = heavy ? clips.heavy : clips.attack;
            float contact = heavy ? clips.heavyContactNormalized : clips.attackContactNormalized;
            return clip == null ? .2f : Mathf.Max(.2f, clip.length * Mathf.Clamp(contact, .01f, 1f));
        }

        public static ReactiveCombatActorVisual Spawn(CombatActorKind kind, Transform parent)
        {
            ReactiveCombatActorLibrary library = Resources.Load<ReactiveCombatActorLibrary>(LibraryPath);
            if (library == null)
                throw new InvalidOperationException("Missing Resources/" + LibraryPath + ".asset");
            ReactiveCombatActorClips definition = library.Get(kind);
            if (definition == null || definition.model == null || definition.idle == null)
                throw new InvalidOperationException("Incomplete combat actor assets for " + kind);

            var root = new GameObject("CombatActor_" + kind);
            root.transform.SetParent(parent, false);
            ReactiveCombatActorVisual visual = root.AddComponent<ReactiveCombatActorVisual>();
            visual.Initialize(definition);
            return visual;
        }

        private void Initialize(ReactiveCombatActorClips definition)
        {
            clips = definition;
            GameObject model = Instantiate(clips.model, transform, false);
            model.name = clips.model.name;
            modelRoot = model.transform;
            if (clips.material != null)
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = clips.material;
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                animator.enabled = false;
            }
            // Weapons have their own bounds. Keep actor grounding and stature independent
            // of an equipped blade, including its wide bounds during a swing.
            actorRenderers = model.GetComponentsInChildren<Renderer>(true);
            animationPlayer = model.GetComponent<Animation>();
            if (animationPlayer == null) animationPlayer = model.AddComponent<Animation>();
            // Imported models carry their own autoplay take. Only the arena's
            // explicit sample may write the rig, including after Update and on
            // paused frames; native animation would overwrite the authored grip.
            animationPlayer.Stop();
            animationPlayer.playAutomatically = false;
            animationPlayer.enabled = false;
            poseTransforms = model.GetComponentsInChildren<Transform>(true);
            blendPositions = new Vector3[poseTransforms.Length];
            blendRotations = new Quaternion[poseTransforms.Length];
            blendScales = new Vector3[poseTransforms.Length];
            AddClip(clips.idle, "Idle", WrapMode.Loop);
            AddClip(clips.attack, "Attack", WrapMode.Once);
            AddClip(clips.heavy, "Heavy", WrapMode.Once);
            AddClip(clips.preparation, "Preparation", WrapMode.ClampForever);
            AddClip(clips.heavyPreparation, "HeavyPreparation", WrapMode.ClampForever);
            AddClip(clips.enterBattle, "EnterBattle", WrapMode.Once);
            AddClip(clips.hit, "Hit", WrapMode.Once);
            AddClip(clips.stagger, "Stagger", WrapMode.Once);
            AddClip(clips.death, "Death", WrapMode.ClampForever);
            AddClip(clips.walk, "Walk", WrapMode.Loop);
            AddClip(clips.approach, "Approach", WrapMode.Loop);
            AddClip(clips.returnHome, "ReturnHome", WrapMode.Loop);
            SetFacing(true);
            SetStandingHeight(Mathf.Max(.2f, clips.standingHeight));
            anchoredXZ = new Vector2(modelRoot.localPosition.x, modelRoot.localPosition.z);
            anchoredY = modelRoot.localPosition.y;
            if (clips.weaponPrefab != null)
            {
                weaponAttachment = gameObject.AddComponent<ReactiveCombatWeaponAttachment>();
                weaponAttachment.Configure(modelRoot, clips);
                grippedWeapon = weaponAttachment.CurrentWeapon;
                twoHandGrip = new ReactiveCombatTwoHandGrip(modelRoot,
                    weaponAttachment.CurrentWeapon == null ? null : weaponAttachment.CurrentWeapon.transform);
                foreach (Transform bone in weaponAttachment.Socket.parent.GetComponentsInChildren<Transform>(true))
                    for (int i = 0; i < gripFingers.Length; i++)
                        if (bone.name.EndsWith("RightHandIndex" + (i + 1), StringComparison.Ordinal))
                            gripFingers[i] = bone;
            }
            PlayIdle();
        }

        private void AddClip(AnimationClip clip, string alias, WrapMode wrapMode)
        {
            if (clip == null) return;
            if (!clip.legacy)
            {
                Debug.LogError("Combat clip must use Legacy rig: " + clip.name, this);
                return;
            }
            animationPlayer.AddClip(clip, alias);
            animationPlayer[alias].wrapMode = wrapMode;
        }

        public void SetStandingHeight(float height)
        {
            if (modelRoot == null || height <= 0f || !TryGetBounds(out Bounds before)) return;
            modelRoot.localScale *= height / Mathf.Max(.001f, before.size.y);
            AlignFeet();
            anchoredY = modelRoot.localPosition.y;
        }

        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            if (actorRenderers == null) return false;
            foreach (Renderer renderer in actorRenderers)
            {
                if (renderer == null || !renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        public void AlignFeet()
        {
            if (modelRoot == null || !TryGetBounds(out Bounds bounds)) return;
            Vector3 foot = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            modelRoot.localPosition -= new Vector3(0f, foot.y, 0f);
        }

        public void SetFacing(bool right)
        {
            facingRight = right;
            if (modelRoot != null)
                modelRoot.localRotation = Quaternion.Euler(0f, clips.forwardYaw + (right ? 0f : 180f), 0f);
        }

        public void PlayIdle()
        {
            if (dead) return;
            poseSecondsLeft = 0f;
            proceduralSecondsLeft = 0f;
            fallbackDeathAngle = 0f;
            if (animationPlayer != null && animationPlayer.GetClip("Idle") != null)
            {
                BeginPose("Idle", 1f);
            }
        }

        public void PlayAttack(float playbackSeconds = 0f) =>
            PlayOneShot("Attack", .55f, -7f, null, playbackSeconds);
        public void PlayHeavy(float playbackSeconds = 0f) =>
            PlayOneShot("Heavy", .8f, -12f, "Attack", playbackSeconds);
        public void PlayPreparation(bool heavy)
        {
            if (dead) return;
            string alias = heavy ? "HeavyPreparation" : "Preparation";
            AnimationClip clip = animationPlayer == null ? null : animationPlayer.GetClip(alias);
            if (clip == null) { PlayIdle(); return; }
            BeginPose(alias, 1f);
            // The stage advances into approach once this authored take ends.
            // Hold its final ready pose if that transition waits on another beat.
            poseSecondsLeft = 0f;
            proceduralSecondsLeft = 0f;
        }
        public void PlayEnterBattle(float duration = 0f) =>
            PlayOneShot("EnterBattle", 1.2f, 0f, "Idle", duration > 0f ? duration : EnterBattleDuration);
        public void PlayAttackToContact(float secondsUntilContact) =>
            PlayAttack(ContactPlaybackSeconds(false, secondsUntilContact));
        public void PlayHeavyToContact(float secondsUntilContact) =>
            PlayHeavy(ContactPlaybackSeconds(true, secondsUntilContact));
        public void PlayHit() => PlayOneShot("Hit", .42f, 12f);
        public void PlayStagger() => PlayOneShot("Stagger", .65f, 18f, "Hit");
        public void PlayDefend() => PlayOneShot(null, .45f, 14f);
        public void PlayDodge()
        {
            PlayOneShot(null, .38f, -20f);
            dodgeRemaining = .38f;
        }

        public void PlayGuard() => PlayOneShot(null, .36f, 13f);

        public void Recoil(float distance = .2f)
        {
            if (dead) return;
            recoilDistance = Mathf.Max(.01f, distance);
            recoilRemaining = .28f;
        }

        public void HoldPresentation(float seconds)
        {
            hitStopRemaining = Mathf.Max(hitStopRemaining, Mathf.Max(0f, seconds));
        }

        public void HoldAttackAtContact(bool heavy, float seconds)
        {
            string alias = heavy ? "Heavy" : "Attack";
            AnimationClip clip = animationPlayer == null ? null : animationPlayer.GetClip(alias);
            if (clip != null && activeAlias == alias)
            {
                animationPlayer[alias].time = clip.length * Mathf.Clamp01(heavy
                    ? clips.heavyContactNormalized : clips.attackContactNormalized);
                SamplePose();
            }
            HoldPresentation(seconds);
        }

        private float ContactPlaybackSeconds(bool heavy, float secondsUntilContact)
        {
            float normalized = heavy ? clips.heavyContactNormalized : clips.attackContactNormalized;
            return Mathf.Max(.1f, secondsUntilContact) / Mathf.Clamp(normalized, .01f, 1f);
        }

        public void PlayWalk(float seconds = .7f)
        {
            if (dead) return;
            AnimationClip walk = animationPlayer == null ? null : animationPlayer.GetClip("Walk");
            if (walk == null) { PlayOneShot(null, seconds, -4f); return; }
            BeginPose("Walk", 1f);
            poseSecondsLeft = Mathf.Max(.1f, seconds);
            proceduralSecondsLeft = 0f;
        }

        public void PlayWaveEnter() => PlayWalk(.7f);

        public void PlayApproach(float seconds) => PlayMovement("Approach", seconds);

        public void PlayReturnHome(float seconds) => PlayMovement("ReturnHome", seconds);

        private void PlayMovement(string alias, float seconds)
        {
            if (dead) return;
            float duration = Mathf.Max(.1f, seconds);
            AnimationClip clip = animationPlayer == null ? null : animationPlayer.GetClip(alias);
            if (clip == null) { PlayWalk(duration); return; }
            BeginPose(alias, Mathf.Max(.1f, clip.length / duration));
            poseSecondsLeft = duration;
            proceduralSecondsLeft = 0f;
        }

        public void PlayDeath(float seconds = 1.35f)
        {
            if (dead) return;
            dead = true;
            poseSecondsLeft = 0f;
            proceduralSecondsLeft = 0f;
            fallbackDeathElapsed = 0f;
            fallbackDeathDuration = Mathf.Max(.1f, seconds);
            AnimationClip death = animationPlayer == null ? null : animationPlayer.GetClip("Death");
            if (death != null)
            {
                BeginPose("Death", death.length / Mathf.Max(.1f, seconds));
            }
            else
            {
                fallbackDeathAngle = 68f;
                activeSpeed = 0f;
            }
        }

        public void ResetForPool()
        {
            dead = false;
            fallbackDeathAngle = 0f;
            fallbackDeathElapsed = 0f;
            poseSecondsLeft = 0f;
            proceduralSecondsLeft = 0f;
            hitStopRemaining = 0f;
            recoilRemaining = 0f;
            dodgeRemaining = 0f;
            PlayIdle();
        }

        private void PlayOneShot(string alias, float fallbackSeconds, float lean,
            string alternate = null, float playbackSeconds = 0f)
        {
            if (dead) return;
            AnimationClip clip = animationPlayer == null || string.IsNullOrEmpty(alias)
                ? null : animationPlayer.GetClip(alias);
            if (clip == null && alternate != null) { alias = alternate; clip = animationPlayer.GetClip(alias); }
            if (clip != null)
            {
                BeginPose(alias, playbackSeconds > 0f
                    ? clip.length / Mathf.Max(.1f, playbackSeconds) : 1f);
                poseSecondsLeft = Mathf.Max(.1f, playbackSeconds > 0f ? playbackSeconds : clip.length);
                proceduralSecondsLeft = 0f;
            }
            else
            {
                if (animationPlayer != null && animationPlayer.GetClip("Idle") != null)
                    BeginPose("Idle", 1f);
                poseSecondsLeft = fallbackSeconds;
                proceduralDuration = fallbackSeconds;
                proceduralSecondsLeft = fallbackSeconds;
                proceduralLean = lean;
            }
        }

        private void BeginPose(string alias, float speed)
        {
            if (animationPlayer == null || animationPlayer.GetClip(alias) == null) return;
            previousAlias = activeAlias == alias ? null : activeAlias;
            previousSpeed = activeSpeed;
            float previousTime = previousAlias == null ? 0f : animationPlayer[previousAlias].time;
            activeAlias = alias;
            activeSpeed = Mathf.Max(0f, speed);
            blendElapsed = 0f;
            animationPlayer.Play(alias, PlayMode.StopAll);
            AnimationState current = animationPlayer[alias];
            current.time = 0f;
            current.speed = 0f;
            current.enabled = true;
            current.weight = previousAlias == null ? 1f : 0f;
            if (previousAlias != null)
            {
                AnimationState previous = animationPlayer[previousAlias];
                previous.enabled = true;
                previous.time = previousTime;
                previous.speed = 0f;
                previous.weight = 1f;
            }
            SamplePose();
        }

        // Arena supplies the presentation clock. Unity's animation states stay at speed
        // zero, so focus/settings pauses and hit stop never change gameplay time.
        public void TickPresentation(float deltaTime)
        {
            float step = Mathf.Max(0f, deltaTime);
            if (hitStopRemaining > 0f)
            {
                float held = Mathf.Min(step, hitStopRemaining);
                hitStopRemaining -= held;
                step -= held;
            }
            if (step <= 0f) return;
            if (dead) fallbackDeathElapsed += step;
            blendElapsed += step;
            if (animationPlayer != null && activeAlias != null)
            {
                AnimationState current = animationPlayer[activeAlias];
                AdvancePose(current, step * activeSpeed);
                current.weight = previousAlias == null ? 1f : Mathf.Clamp01(blendElapsed / BlendDuration);
                if (previousAlias != null)
                {
                    AnimationState previous = animationPlayer[previousAlias];
                    AdvancePose(previous, step * previousSpeed);
                    previous.weight = 1f - current.weight;
                    if (blendElapsed >= BlendDuration)
                    {
                        previous.enabled = false;
                        previousAlias = null;
                    }
                }
                SamplePose();
            }
            if (poseSecondsLeft > 0f)
            {
                poseSecondsLeft -= step;
                if (poseSecondsLeft <= 0f && !dead) PlayIdle();
            }
            if (proceduralSecondsLeft > 0f)
                proceduralSecondsLeft = Mathf.Max(0f, proceduralSecondsLeft - step);
            recoilRemaining = Mathf.Max(0f, recoilRemaining - step);
            dodgeRemaining = Mathf.Max(0f, dodgeRemaining - step);
        }

        private static void AdvancePose(AnimationState state, float seconds)
        {
            float next = state.time + seconds;
            state.time = state.wrapMode == WrapMode.Loop && state.length > 0f
                ? next % state.length : Mathf.Min(next, state.length);
        }

        private void SamplePose()
        {
            twoHandGrip?.RestoreSample();
            // Restore the last authored sample before a fresh one so a missing finger
            // curve or a repeated paused render can never accumulate the grip offset.
            if (gripApplied)
                for (int i = 0; i < gripFingers.Length; i++)
                    if (gripFingers[i] != null) gripFingers[i].localRotation = sampledFingerRotations[i];
            gripApplied = false;
            // Sample the authored take directly. Native Animation.Sample can
            // retain the imported model's default take, and a live Animation
            // component can write the rig again after the arena's Update.
            // Keep state times for inspection while blending only this rig's
            // transform poses under the single presentation clock.
            if (previousAlias != null)
            {
                AnimationState previous = animationPlayer[previousAlias];
                animationPlayer.GetClip(previousAlias).SampleAnimation(modelRoot.gameObject, previous.time);
                for (int i = 0; i < poseTransforms.Length; i++)
                {
                    blendPositions[i] = poseTransforms[i].localPosition;
                    blendRotations[i] = poseTransforms[i].localRotation;
                    blendScales[i] = poseTransforms[i].localScale;
                }
            }
            AnimationState current = animationPlayer[activeAlias];
            animationPlayer.GetClip(activeAlias).SampleAnimation(modelRoot.gameObject, current.time);
            if (previousAlias != null)
                for (int i = 0; i < poseTransforms.Length; i++)
                {
                    Transform bone = poseTransforms[i];
                    bone.localPosition = Vector3.Lerp(blendPositions[i], bone.localPosition, current.weight);
                    bone.localRotation = Quaternion.Slerp(blendRotations[i], bone.localRotation, current.weight);
                    bone.localScale = Vector3.Lerp(blendScales[i], bone.localScale, current.weight);
                }
            if (weaponAttachment == null || weaponAttachment.CurrentWeapon == null) return;
            if (grippedWeapon != weaponAttachment.CurrentWeapon)
            {
                grippedWeapon = weaponAttachment.CurrentWeapon;
                twoHandGrip = new ReactiveCombatTwoHandGrip(modelRoot, grippedWeapon.transform);
            }
            // Attack/preparation/entrance FBX takes already contain both hands
            // and the closed index fingers. Keep extra finger curl for legacy
            // movement only; the support palm correction also covers blends.
            twoHandGrip?.Apply();
            if (activeAlias != "Walk") return;
            for (int i = 0; i < gripFingers.Length; i++)
                if (gripFingers[i] != null) sampledFingerRotations[i] = gripFingers[i].localRotation;
            gripApplied = true;
            ApplyWeaponGrip();
        }

        private void ApplyWeaponGrip()
        {
            if (!gripApplied) return;
            if (weaponAttachment == null || weaponAttachment.CurrentWeapon == null)
            {
                for (int i = 0; i < gripFingers.Length; i++)
                    if (gripFingers[i] != null) gripFingers[i].localRotation = sampledFingerRotations[i];
                gripApplied = false;
                return;
            }
            for (int i = 0; i < gripFingers.Length; i++)
            {
                if (gripFingers[i] == null) continue;
                float jointWeight = i == 0 ? 1f : i == 1 ? 1.25f : i == 2 ? .9f : 0f;
                gripFingers[i].localRotation = sampledFingerRotations[i] *
                    Quaternion.Euler(WeaponGripCurlDegrees * jointWeight, 0f, 0f);
            }
        }

        private void LateUpdate()
        {
            if (modelRoot == null) return;
            ApplyWeaponGrip();
            float lean = 0f;
            if (proceduralSecondsLeft > 0f && proceduralDuration > 0f)
                lean = Mathf.Sin((1f - proceduralSecondsLeft / proceduralDuration) * Mathf.PI) * proceduralLean;
            float yaw = clips.forwardYaw + (facingRight ? 0f : 180f);
            float deathAngle = fallbackDeathAngle * Mathf.Clamp01(fallbackDeathElapsed /
                Mathf.Max(.1f, fallbackDeathDuration));
            modelRoot.localRotation = Quaternion.Euler(0f, yaw,
                (facingRight ? -1f : 1f) * (lean + deathAngle));
            // Imported walk and attack root curves may move the model. The stage owns position.
            float recoil = recoilRemaining > 0f
                ? Mathf.Sin(recoilRemaining / .28f * Mathf.PI) * recoilDistance : 0f;
            float dodge = dodgeRemaining > 0f
                ? Mathf.Sin(dodgeRemaining / .38f * Mathf.PI) * .5f : 0f;
            // Ground only when sizing the standing model. Per-frame bounds grounding
            // removes authored jumps and death falls. Bone-local Y animation remains.
            modelRoot.localPosition = new Vector3(anchoredXZ.x +
                (facingRight ? -1f : 1f) * (recoil + dodge), anchoredY, anchoredXZ.y);
        }
    }
}
