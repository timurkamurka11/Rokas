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
        private LicensedCombatMotionPlayer licensedMotion;
        private LicensedCombatMotionProfile licensedStance;
        private CombatIdleStance confirmedCombatStance;
        private AnimationClip registeredIdle;
        public CombatIdleStance ConfirmedCombatStance => confirmedCombatStance;
        public LicensedCombatMotionProfile ConfirmedLicensedProfile => clips == null ? null
            : GetLicensedProfile(confirmedCombatStance == CombatIdleStance.Heavy);
        private bool bossPresentation;
        private string licensedContactKey;
        private Transform licensedWeaponSocket;
        private Transform daggerHomeParent;
        public LicensedCombatMotionProfile DefaultLicensedProfile => clips?.licensedNormal;
        public LicensedCombatMotionProfile ActiveLicensedProfile => licensedMotion?.Profile;
        public float LicensedMotionClock => licensedMotion == null ? 0f : licensedMotion.Clock;
        public bool LicensedContactConfirmed => licensedMotion != null && licensedMotion.Contacted;
        public bool LicensedStageRecovered => licensedMotion != null && licensedMotion.StageRecoveryReached;
        public float LicensedStageRecoveryTime => ActiveLicensedProfile == null ? -1f : ActiveLicensedProfile.StageRecoveryTime;
        public bool ReachesLicensedStageRecovery(float delta, out float remainingStep)
        {
            remainingStep = 0f;
            return licensedMotion != null && licensedMotion.ReachesStageRecovery(delta, out remainingStep);
        }
        public void SetBossPresentation(bool enabled)
        {
            bossPresentation = enabled;
            if (activeAlias == "Idle") PlayIdle();
        }

        // Core CommandCommitted is the only caller that changes the persistent sword stance.
        // Preview, Throw, defense and hit reactions keep their own temporary poses.
        public void CommitCombatStance(CombatIdleStance stance)
        {
            if (stance != CombatIdleStance.Normal && stance != CombatIdleStance.Heavy)
                throw new ArgumentOutOfRangeException(nameof(stance));
            confirmedCombatStance = stance;
        }
        private Transform[] poseTransforms;
        private Transform torsoBone;
        private Vector3[] blendPositions;
        private Quaternion[] blendRotations;
        private Vector3[] blendScales;
        private Renderer[] actorRenderers;
        private ReactiveCombatWeaponAttachment weaponAttachment;
        private ReactiveCombatTwoHandGrip twoHandGrip;
        private GameObject grippedWeapon;
        private GameObject heldDagger;
        private Transform swordHomeParent;
        private Transform swordStow;
        public Transform SwordStowSocket => swordStow;
        public bool SwordStowCalibrationValid { get; private set; }
        public Quaternion SwordStowCalibratedLocalRotation { get; private set; }
        // Existing owned mesh measurement: long blade runs along prefab +Z.
        public Vector3 SwordStowBladeAxis => Vector3.forward;
        private bool throwReleased;
        public Transform HeldDagger => heldDagger == null ? null : heldDagger.transform;
        public GameObject ThrowingDaggerPrefab => clips.throwingDaggerPrefab;
        public float ThrowReleaseSeconds => clips.throwReleaseSeconds;
        public float ThrowContactSeconds => clips.throwContactSeconds;
        public bool ThrowReady => clips != null && animationPlayer != null && heldDagger != null &&
            swordStow != null && clips.throwPreparation != null && clips.throwAttack != null &&
            clips.throwingDaggerPrefab != null && clips.throwPreparation.legacy && clips.throwAttack.legacy &&
            HasRegisteredClip("ThrowPreparation", clips.throwPreparation) &&
            HasRegisteredClip("Throw", clips.throwAttack);

        private bool HasRegisteredClip(string alias, AnimationClip source)
        {
            // Legacy Animation.AddClip creates a named runtime copy, so comparing
            // its object identity with the serialized source would reject valid assets.
            AnimationClip registered = animationPlayer.GetClip(alias);
            return registered != null && registered.legacy &&
                Mathf.Abs(registered.length - source.length) < .001f;
        }
        private readonly Transform[] gripFingers = new Transform[4];
        private readonly Quaternion[] sampledFingerRotations = new Quaternion[4];
        private bool gripApplied;
        private string activeAlias;
        private string previousAlias;
        private float activeSpeed = 1f;
        private float previousSpeed = 1f;
        private float blendElapsed;
        private bool sampledLicensedPose;
        private bool frozenPreviousPose;
        private bool nativeEntryBlend;
        private float nativeEntryElapsed;
        private bool frozenWeaponValid;
        private Vector3 frozenWeaponPosition;
        private Quaternion frozenWeaponRotation;
        private Vector3 frozenWeaponScale;
        private Vector3 frozenWeaponGripPosition;
        private Quaternion frozenWeaponGripRotation;
        private Vector3 destinationWeaponGripPosition;
        private Vector3 destinationWeaponPosition;
        private Quaternion destinationWeaponRotation;
        private Quaternion destinationWeaponGripRotation;
        private Vector3 destinationWeaponScale;
        private bool frozenPrimaryHeldByLeft;
        private bool frozenSupportHeld;
        private bool frozenSwordStowed;
        public enum SwordTransformAuthority { DefaultSocket, NativeMotion, ExitBlend }
        public SwordTransformAuthority SwordTransformOwner { get; private set; }
        private const float BlendDuration = .08f;
        private float hitStopRemaining;
        private float recoilRemaining;
        private float recoilDistance;
        private bool defenseActive;
        private bool defenseDodge;
        private bool defenseContactResolved;
        private float defenseContactTime;
        private float locomotionStrideDistance;
        private string pendingAttackContactAlias;
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
        public Vector3 TorsoPoint => torsoBone == null ? BodyBounds.center : torsoBone.position;
        public bool IsDead => dead;
        public bool IsFacingRight => facingRight;
        public float HitStopRemaining => hitStopRemaining;
        public string CurrentPose => activeAlias;
        public bool ActionRecoveryComplete => poseSecondsLeft <= 0f && hitStopRemaining <= 0f &&
            !AwaitingAttackContact && !defenseActive && (licensedMotion == null || !licensedMotion.Active);
        public bool IdleSettled => activeAlias == "Idle" &&
            (previousAlias == null || blendElapsed >= BlendDuration) && !frozenPreviousPose && hitStopRemaining <= 0f;
        public ReactiveCombatWeaponAttachment WeaponAttachment => weaponAttachment;
        public float WeaponGripCurlDegrees { get; set; } = 28f;
        public float EnterBattleDuration => clips.enterBattle == null ? 1.2f : clips.enterBattle.length;
        public bool DefenseRecoveryComplete => !defenseActive && ActionRecoveryComplete;
        public bool DefenseActive => defenseActive;
        public float CurrentPoseSeconds => animationPlayer == null || activeAlias == null
            ? 0f : licensedMotion != null && licensedMotion.Active ? licensedMotion.Clock : animationPlayer[activeAlias].time;
        public bool AwaitingAttackContact => licensedMotion != null && licensedMotion.Active && !licensedMotion.Contacted || pendingAttackContactAlias != null &&
            pendingAttackContactAlias == activeAlias;
        public float DodgeDisplacement => defenseActive && defenseDodge
            ? DodgeOffset(CurrentPoseSeconds) : 0f;
        public Vector3 DefenseContactPoint => weaponAttachment == null || weaponAttachment.CurrentWeapon == null
            ? transform.position + Vector3.up * 1.2f
            : weaponAttachment.CurrentWeapon.transform.position +
                weaponAttachment.CurrentWeapon.transform.forward * .32f;

        public Bounds BodyBounds
        {
            get
            {
                var bounds = new Bounds(transform.position + Vector3.up, Vector3.zero);
                bool found = false;
                foreach (Renderer renderer in actorRenderers)
                {
                    if (renderer == null) continue;
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                return bounds;
            }
        }

        public Vector3 ClawPointNearest(Vector3 target)
        {
            Vector3 nearest = transform.position + Vector3.up * 1.5f;
            float distance = float.MaxValue;
            foreach (Transform bone in poseTransforms)
            {
                if (!bone.name.EndsWith(":RightHand") && !bone.name.EndsWith(":LeftHand") &&
                    !bone.name.EndsWith(":RightHandMiddle3") && !bone.name.EndsWith(":LeftHandMiddle3")) continue;
                float candidate = (bone.position - target).sqrMagnitude;
                if (candidate >= distance) continue;
                nearest = bone.position;
                distance = candidate;
            }
            return nearest;
        }

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

        public float AttackSwingStartSeconds(bool heavy) => AttackContactSeconds(heavy) *
            (clips.weaponPrefab == null ? (heavy ? .675f : .636f) : (heavy ? .79f : .7333f));

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
            licensedWeaponSocket = new GameObject("LicensedWeaponSocket").transform;
            licensedWeaponSocket.SetParent(modelRoot, false);
            licensedMotion = new LicensedCombatMotionPlayer(animationPlayer, clips.idle);
            poseTransforms = model.GetComponentsInChildren<Transform>(true);
            // Importers may preserve or remove the namespace separator. Cache
            // the actual anatomical anchor, independently of animated skin bounds.
            foreach (string suffix in new[] { "Spine2", "Spine1", "Spine" })
            {
                foreach (Transform bone in poseTransforms)
                    if (bone.name.EndsWith(suffix, StringComparison.Ordinal)) { torsoBone = bone; break; }
                if (torsoBone != null) break;
            }
            blendPositions = new Vector3[poseTransforms.Length];
            blendRotations = new Quaternion[poseTransforms.Length];
            blendScales = new Vector3[poseTransforms.Length];
            AddClip(clips.idle, "Idle", WrapMode.Loop);
            AddClip(clips.attack, "Attack", WrapMode.Once);
            AddClip(clips.heavy, "Heavy", WrapMode.Once);
            AddClip(clips.preparation, "Preparation", WrapMode.ClampForever);
            AddClip(clips.heavyPreparation, "HeavyPreparation", WrapMode.ClampForever);
            AddClip(clips.throwPreparation, "ThrowPreparation", WrapMode.ClampForever);
            AddClip(clips.throwAttack, "Throw", WrapMode.Once);
            AddClip(clips.enterBattle, "EnterBattle", WrapMode.Once);
            AddClip(clips.entranceWalk, "EntranceWalk", WrapMode.Loop);
            AddClip(clips.guard, "Guard", WrapMode.ClampForever);
            AddClip(clips.dodge, "Dodge", WrapMode.ClampForever);
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
                swordHomeParent = grippedWeapon.transform.parent;
                ConfigureSwordStow();
                if (clips.throwingDaggerPrefab != null)
                {
                    heldDagger = Instantiate(clips.throwingDaggerPrefab, weaponAttachment.Socket.parent, false);
                    heldDagger.name = "KeikoHeldThrowingDagger";
                    daggerHomeParent = heldDagger.transform.parent;
                    heldDagger.transform.localPosition = clips.daggerSocketPosition;
                    heldDagger.transform.localRotation = Quaternion.Euler(clips.daggerSocketEuler);
                    foreach (Transform child in heldDagger.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = gameObject.layer;
                    heldDagger.SetActive(false);
                }
                twoHandGrip = new ReactiveCombatTwoHandGrip(modelRoot,
                    weaponAttachment.CurrentWeapon == null ? null : weaponAttachment.CurrentWeapon.transform);
                foreach (Transform bone in weaponAttachment.Socket.parent.GetComponentsInChildren<Transform>(true))
                    for (int i = 0; i < gripFingers.Length; i++)
                        if (bone.name.EndsWith("RightHandIndex" + (i + 1), StringComparison.Ordinal))
                            gripFingers[i] = bone;
            }
            confirmedCombatStance = CombatIdleStance.Normal;
            // Register the normal native bank before interactive entrance/first contact.
            // Prepare performs no Sample/Play/Tick and preserves the rig and contact history.
            licensedMotion.Prepare(GetLicensedProfile(false), "Licensed_Anticipation");
            PlayIdle();
        }

        private void ConfigureSwordStow()
        {
            Transform spine = modelRoot.Find(clips.swordStowBonePath);
            Transform left = null, right = null, neck = null;
            foreach (Transform bone in poseTransforms)
            {
                if (bone.name.EndsWith("LeftShoulder", StringComparison.Ordinal)) left = bone;
                if (bone.name.EndsWith("RightShoulder", StringComparison.Ordinal)) right = bone;
                if (bone.name.EndsWith("Neck", StringComparison.Ordinal)) neck = bone;
            }
            if (spine == null || !TrySkinBindPose(spine, out Matrix4x4 spineBind) ||
                !TrySkinBindPose(left, out Matrix4x4 leftBind) ||
                !TrySkinBindPose(right, out Matrix4x4 rightBind) ||
                !TrySkinBindPose(neck, out Matrix4x4 neckBind))
                throw new InvalidOperationException("Back sword socket requires its configured spine and actual skin bind geometry.");
            Vector3 up = (neckBind.MultiplyPoint3x4(Vector3.zero) - spineBind.MultiplyPoint3x4(Vector3.zero)).normalized;
            Vector3 rightAxis = (rightBind.MultiplyPoint3x4(Vector3.zero) - leftBind.MultiplyPoint3x4(Vector3.zero)).normalized;
            Vector3 forward = Vector3.Cross(rightAxis, up).normalized;
            if (up.sqrMagnitude < .9f || forward.sqrMagnitude < .9f)
                throw new InvalidOperationException("Back sword socket bind frame is degenerate.");
            // Keep the existing own .4 lateral/down carry ratio, now in the rig's
            // anatomical frame. Its local quaternion is calibrated once; FK owns
            // the spine thereafter, including facing changes and paused renders.
            Vector3 bladeDirection = (-up - rightAxis * clips.swordStowBladeSideSlope).normalized;
            SwordStowCalibratedLocalRotation = Quaternion.Inverse(spineBind.rotation) *
                Quaternion.LookRotation(bladeDirection, forward);
            swordStow = new GameObject("SwordStow").transform;
            swordStow.SetParent(spine, false);
            swordStow.localPosition = clips.swordStowPosition;
            swordStow.localRotation = SwordStowCalibratedLocalRotation;
            SwordStowCalibrationValid = true;
        }

        private bool TrySkinBindPose(Transform bone, out Matrix4x4 bindInModel)
        {
            bindInModel = Matrix4x4.identity;
            if (bone == null) return false;
            foreach (Renderer renderer in actorRenderers)
            {
                var skin = renderer as SkinnedMeshRenderer;
                if (skin == null || skin.sharedMesh == null) continue;
                Transform[] bones = skin.bones;
                Matrix4x4[] bindPoses = skin.sharedMesh.bindposes;
                for (int index = 0; index < bones.Length && index < bindPoses.Length; index++)
                    if (bones[index] == bone)
                    {
                        Matrix4x4 skinToModel = Matrix4x4.identity;
                        for (Transform node = skin.transform; node != null && node != modelRoot; node = node.parent)
                            skinToModel = Matrix4x4.TRS(node.localPosition, node.localRotation, node.localScale) * skinToModel;
                        bindInModel = skinToModel * bindPoses[index].inverse;
                        return true;
                    }
            }
            return false;
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

        public LicensedCombatMotionProfile AttackLicensedProfile(bool heavy) => GetLicensedProfile(heavy);

        private LicensedCombatMotionProfile GetLicensedProfile(bool heavy)
        {
            if (bossPresentation)
            {
                LicensedCombatMotionProfile boss = heavy ? clips.licensedBossHeavy : clips.licensedBoss;
                if (boss != null) return boss;
            }
            return heavy ? clips.licensedHeavy : clips.licensedNormal;
        }

        private bool BeginLicensed(LicensedCombatMotionProfile profile, string alias)
        {
            if (dead || profile == null || licensedMotion == null) return false;
            // Only input/travel anticipation uses the existing owned handoff.
            // The source action still cuts at its exact contact-origin clock.
            CaptureDisplayedTransitionPose(alias, true);
            frozenPreviousPose = nativeEntryBlend = true;
            nativeEntryElapsed = 0f;
            twoHandGrip?.RestoreSample();
            gripApplied = false;
            defenseActive = false;
            locomotionStrideDistance = 0f;
            activeAlias = alias;
            previousAlias = null;
            pendingAttackContactAlias = null;
            poseSecondsLeft = proceduralSecondsLeft = 0f;
            licensedStance = profile;
            licensedContactKey = null;
            licensedMotion.Begin(profile, null, "Licensed_Anticipation");
            sampledLicensedPose = true;
            ApplyLicensedSample();
            return true;
        }

        public void BindLicensedContact(string actionId, string hitId = null)
        {
            licensedContactKey = string.IsNullOrEmpty(actionId) ? null : actionId + ":" + (hitId ?? "");
            licensedMotion?.BindContact(licensedContactKey);
        }

        public bool ConfirmLicensedContact(string actionId, string hitId = null)
        {
            string key = string.IsNullOrEmpty(actionId) ? null : actionId + ":" + (hitId ?? "");
            bool accepted = licensedMotion != null && licensedMotion.ConfirmContact(key);
            if (accepted)
            {
                nativeEntryBlend = frozenPreviousPose = frozenWeaponValid = false;
                sampledLicensedPose = true;
                pendingAttackContactAlias = null;
                ApplyLicensedEquipment();
            }
            return accepted;
        }

        private void ApplyLicensedEquipment() => UpdateEquipmentPose(true);

        private void ApplyLicensedSample()
        {
            if (!nativeEntryBlend || licensedMotion.Contacted) { ApplyLicensedEquipment(); return; }
            float weight = Mathf.Clamp01(nativeEntryElapsed / BlendDuration);
            CaptureDestinationWeaponGrip();
            for (int i = 0; i < poseTransforms.Length; i++)
            {
                Transform bone = poseTransforms[i];
                bone.localPosition = Vector3.Lerp(blendPositions[i], bone.localPosition, weight);
                bone.localRotation = Quaternion.Slerp(blendRotations[i], bone.localRotation, weight);
                bone.localScale = Vector3.Lerp(blendScales[i], bone.localScale, weight);
            }
            UpdateEquipmentPose(true, weight);
            if (weight >= 1f) nativeEntryBlend = frozenPreviousPose = frozenWeaponValid = false;
        }

        // This is the only runtime writer of the equipped sword transform.
        // Clips animate the helper socket, never the weapon prefab itself.
        private void UpdateEquipmentPose(bool nativeSample, float weight = 1f)
        {
            if (weaponAttachment == null || weaponAttachment.CurrentWeapon == null) return;
            if (grippedWeapon != weaponAttachment.CurrentWeapon)
            {
                grippedWeapon = weaponAttachment.CurrentWeapon;
                twoHandGrip = new ReactiveCombatTwoHandGrip(modelRoot, grippedWeapon.transform);
            }
            bool throwing = activeAlias == "ThrowPreparation" || activeAlias == "Throw";
            bool sourceIdle = licensedStance != null && activeAlias == "Idle";
            bool authoredSocket = licensedStance != null && licensedStance.authoredWeaponSocket;
            bool sourceSword = authoredSocket && (nativeSample || sourceIdle) &&
                licensedStance.weapon != LicensedWeaponKind.Dagger;
            Transform desired = throwing ? swordStow : sourceSword ? licensedWeaponSocket : swordHomeParent;
            Transform weapon = grippedWeapon.transform;
            bool exit = frozenPreviousPose && frozenWeaponValid && weight < 1f;
            bool destinationSupportHeld = !sourceIdle || licensedStance.weapon == LicensedWeaponKind.TwoHandedSword;
            bool correctExitPrimary = false;
            SwordTransformOwner = exit ? SwordTransformAuthority.ExitBlend : nativeSample
                ? SwordTransformAuthority.NativeMotion : SwordTransformAuthority.DefaultSocket;
            if (exit)
            {
                // Preserve world pose on parent handoff. Snapshot coordinates are
                // relative to the model so arena translation does not lag the blade.
                if (weapon.parent != modelRoot) weapon.SetParent(modelRoot, true);
                Transform primaryHand = twoHandGrip?.PrimaryHand;
                if (weight > 0f && primaryHand != null && !throwing && !frozenSwordStowed)
                {
                    // FK has already blended the hand. Blend only its local grip
                    // adapter here; blending its world position again detached the
                    // blade from both palms during the first exit frames.
                    if (frozenPrimaryHeldByLeft)
                    {
                        // A left-held blade cannot orbit the other wrist through a
                        // large grip adapter while that wrist turns into source idle.
                        // Interpolate once toward the unblended authored destination;
                        // the palm correction performs the continuous hand transfer.
                        weapon.localPosition = Vector3.Lerp(frozenWeaponPosition, destinationWeaponPosition, weight);
                        weapon.localRotation = Quaternion.Slerp(frozenWeaponRotation, destinationWeaponRotation, weight);
                    }
                    else
                    {
                        weapon.position = primaryHand.TransformPoint(Vector3.Lerp(
                            frozenWeaponGripPosition, destinationWeaponGripPosition, weight));
                        weapon.rotation = primaryHand.rotation * Quaternion.Slerp(
                            frozenWeaponGripRotation, destinationWeaponGripRotation, weight);
                    }
                    weapon.localScale = Vector3.Lerp(frozenWeaponScale, destinationWeaponScale, weight);
                }
                else
                {
                    // Keep the exact frozen frame and the existing stowed Throw
                    // transfer, where the sword intentionally is not in either hand.
                    weapon.localPosition = Vector3.Lerp(frozenWeaponPosition,
                        modelRoot.InverseTransformPoint(desired.position), weight);
                    weapon.localRotation = Quaternion.Slerp(frozenWeaponRotation,
                        Quaternion.Inverse(modelRoot.rotation) * desired.rotation, weight);
                    weapon.localScale = Vector3.Lerp(frozenWeaponScale, Vector3.one, weight);
                }
            }
            else
            {
                if (weapon.parent != desired) weapon.SetParent(desired, false);
                weapon.localPosition = Vector3.zero;
                weapon.localRotation = Quaternion.identity;
                weapon.localScale = Vector3.one;
            }
            if (exit && weight > 0f && !throwing && !frozenSwordStowed && twoHandGrip != null)
            {
                Vector3 feasiblePosition = twoHandGrip.ResolveExitPrimaryPosition(weapon.position, weight,
                    frozenPrimaryHeldByLeft, frozenSupportHeld, destinationSupportHeld);
                correctExitPrimary = (feasiblePosition - weapon.position).sqrMagnitude > 1e-12f;
                weapon.position = feasiblePosition;
            }
            if (heldDagger != null)
            {
                Transform desiredDagger = throwing && authoredSocket && nativeSample
                    ? licensedWeaponSocket : daggerHomeParent;
                if (heldDagger.transform.parent != desiredDagger)
                    heldDagger.transform.SetParent(desiredDagger, false);
                heldDagger.transform.localPosition = desiredDagger == licensedWeaponSocket
                    ? Vector3.zero : clips.daggerSocketPosition;
                heldDagger.transform.localRotation = desiredDagger == licensedWeaponSocket
                    ? Quaternion.identity : Quaternion.Euler(clips.daggerSocketEuler);
                heldDagger.SetActive(throwing && !throwReleased);
            }
            // The support hand never owns the weapon. Native samples already own
            // their grip; only the common exit needs the existing palm correction
            // while the body crossfades into the destination hand roles.
            if (exit && !throwing && !frozenSwordStowed)
                twoHandGrip?.ApplyExit(weight, frozenPrimaryHeldByLeft, frozenSupportHeld,
                    destinationSupportHeld, correctExitPrimary);
            else if (!nativeSample && !sourceIdle && !throwing && !exit) twoHandGrip?.Apply();
        }

        private void CaptureDestinationWeaponGrip()
        {
            Transform primaryHand = twoHandGrip?.PrimaryHand;
            if (!frozenPreviousPose || !frozenWeaponValid || primaryHand == null) return;
            bool throwing = activeAlias == "ThrowPreparation" || activeAlias == "Throw";
            bool sourceIdle = licensedStance != null && activeAlias == "Idle";
            bool sourceSword = (sourceIdle || licensedMotion != null && licensedMotion.Active) &&
                licensedStance.authoredWeaponSocket &&
                licensedStance.weapon != LicensedWeaponKind.Dagger;
            Transform desired = throwing ? swordStow : sourceSword ? licensedWeaponSocket : swordHomeParent;
            // Read the destination from the unblended authored sample. Its helper
            // and the hand will subsequently be blended together by SamplePose.
            destinationWeaponPosition = modelRoot.InverseTransformPoint(desired.position);
            destinationWeaponRotation = Quaternion.Inverse(modelRoot.rotation) * desired.rotation;
            destinationWeaponGripPosition = primaryHand.InverseTransformPoint(desired.position);
            destinationWeaponGripRotation = Quaternion.Inverse(primaryHand.rotation) * desired.rotation;
            Vector3 worldScale = desired.lossyScale, modelScale = modelRoot.lossyScale;
            destinationWeaponScale = new Vector3(worldScale.x / modelScale.x,
                worldScale.y / modelScale.y, worldScale.z / modelScale.z);
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
            licensedStance = ConfirmedLicensedProfile;
            AnimationClip idle = licensedStance != null && licensedStance.baseIdle != null
                ? licensedStance.baseIdle : clips.idle;
            if (idle != registeredIdle)
            {
                AddClip(idle, "Idle", WrapMode.Loop);
                registeredIdle = idle;
            }
            defenseActive = false;
            defenseContactResolved = false;
            locomotionStrideDistance = 0f;
            pendingAttackContactAlias = null;
            poseSecondsLeft = 0f;
            proceduralSecondsLeft = 0f;
            fallbackDeathAngle = 0f;
            if (animationPlayer != null && animationPlayer.GetClip("Idle") != null)
            {
                BeginPose("Idle", licensedStance == null ? 1f : licensedStance.idleSpeed);
            }
        }

        public void PlayAttack(float playbackSeconds = 0f)
        {
            if (!BeginLicensed(GetLicensedProfile(false), "Attack"))
                PlayOneShot("Attack", .55f, -7f, null, playbackSeconds);
        }
        public void PlayHeavy(float playbackSeconds = 0f)
        {
            if (!BeginLicensed(GetLicensedProfile(true), "Heavy"))
                PlayOneShot("Heavy", .8f, -12f, "Attack", playbackSeconds);
        }
        public void PlayPreparation(bool heavy)
        {
            if (dead) return;
            if (BeginLicensed(GetLicensedProfile(heavy), heavy ? "HeavyPreparation" : "Preparation")) return;
            string alias = heavy ? "HeavyPreparation" : "Preparation";
            AnimationClip clip = animationPlayer == null ? null : animationPlayer.GetClip(alias);
            if (clip == null) { PlayIdle(); return; }
            BeginPose(alias, 1f);
            // The stage advances into approach once this authored take ends.
            // Hold its final ready pose if that transition waits on another beat.
            poseSecondsLeft = 0f;
            proceduralSecondsLeft = 0f;
        }
        public void PlayThrowPreparation()
        {
            if (dead || animationPlayer.GetClip("ThrowPreparation") == null) return;
            throwReleased = false;
            if (BeginLicensed(clips.licensedThrow, "ThrowPreparation")) return;
            BeginPose("ThrowPreparation", 1f);
            poseSecondsLeft = proceduralSecondsLeft = 0f;
        }
        public void PlayThrow()
        {
            throwReleased = false;
            if (!BeginLicensed(clips.licensedThrow, "Throw")) PlayOneShot("Throw", 1.15f, 0f);
        }
        public void ReleaseThrowWeapon()
        {
            throwReleased = true;
            heldDagger?.SetActive(false);
        }
        public void PlayEnterBattle(float duration = 0f) =>
            PlayOneShot("EnterBattle", 1.2f, 0f, "Idle", duration > 0f ? duration : EnterBattleDuration);
        public void PlayAttackToContact(float secondsUntilContact) =>
            PlayAttack(ContactPlaybackSeconds(false, secondsUntilContact));
        public void PlayHeavyToContact(float secondsUntilContact) =>
            PlayHeavy(ContactPlaybackSeconds(true, secondsUntilContact));
        public void PlayHit() => PlayOneShot("Hit", .42f, 12f);
        public void PlayStagger() => PlayOneShot("Stagger", .65f, 18f, "Hit");
        public void PlayDefend() => PlayGuard();
        public void PlayDodge()
        {
            BeginDefense(true, .24f);
            ResolveDefenseContact(true, 0f);
        }

        public void PlayGuard()
        {
            BeginDefense(false, .34f);
            ResolveDefenseContact(false, 0f);
        }

        // Only an accepted successful reaction enters this lifecycle. Failed input
        // stays in its current stance and gets the ordinary hit reaction.
        public void BeginDefense(bool dodge, float secondsToContact)
        {
            if (dead) return;
            string alias = dodge ? "Dodge" : "Guard";
            AnimationClip clip = animationPlayer == null ? null : animationPlayer.GetClip(alias);
            if (clip == null) return;
            defenseActive = true;
            defenseDodge = dodge;
            defenseContactResolved = false;
            defenseContactTime = Mathf.Clamp(dodge ? clips.dodgeContactSeconds : clips.guardContactSeconds,
                .01f, clip.length);
            BeginPose(alias, defenseContactTime / Mathf.Max(.06f, secondsToContact));
            blendElapsed = Mathf.Max(0f, BlendDuration - Mathf.Max(.06f, secondsToContact));
            poseSecondsLeft = 0f;
            proceduralSecondsLeft = 0f;
            locomotionStrideDistance = 0f;
        }

        // Continue from the same guard/hop at contact: never restart the raise or
        // backstep from a HitResolved callback after the enemy has already swung.
        public void ResolveDefenseContact(bool dodge, float hitStopSeconds)
        {
            if (!defenseActive || defenseDodge != dodge) return;
            animationPlayer[activeAlias].time = defenseContactTime;
            defenseContactResolved = true;
            blendElapsed = Mathf.Max(blendElapsed, BlendDuration);
            if (previousAlias != null)
            {
                animationPlayer[previousAlias].enabled = false;
                previousAlias = null;
            }
            animationPlayer[activeAlias].weight = 1f;
            activeSpeed = 1f;
            poseSecondsLeft = Mathf.Max(.01f, animationPlayer.GetClip(activeAlias).length - defenseContactTime);
            SamplePose();
            HoldPresentation(hitStopSeconds);
        }

        private static float DodgeOffset(float time)
        {
            if (time < .08f) return 0f;
            if (time < .24f) return .78f * Mathf.SmoothStep(0f, 1f, (time - .08f) / .16f);
            if (time <= .38f) return .78f;
            return .78f * (1f - Mathf.SmoothStep(0f, 1f, (time - .38f) / .42f));
        }

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
            if (licensedMotion != null && licensedMotion.Active)
            {
                pendingAttackContactAlias = null;
                HoldPresentation(seconds);
                return;
            }
            string alias = heavy ? "Heavy" : "Attack";
            AnimationClip clip = animationPlayer == null ? null : animationPlayer.GetClip(alias);
            if (clip != null && activeAlias == alias)
            {
                float contact = clip.length * Mathf.Clamp01(heavy
                    ? clips.heavyContactNormalized : clips.attackContactNormalized);
                animationPlayer[alias].time = contact;
                pendingAttackContactAlias = null;
                // A Core watermark may leave the pose waiting at contact. That
                // wait must not consume the authored follow-through/recovery.
                poseSecondsLeft = Mathf.Max(poseSecondsLeft,
                    (clip.length - contact) / Mathf.Max(.01f, activeSpeed));
                SamplePose();
            }
            HoldPresentation(seconds);
        }

        public void AwaitAttackContact(bool heavy)
        {
            string alias = heavy ? "Heavy" : "Attack";
            if (animationPlayer == null || animationPlayer.GetClip(alias) == null) return;
            pendingAttackContactAlias = alias;
        }

        // Core can settle an interrupted multi-hit action without resolving its
        // cancelled suffix. Release that visual wait without inventing contact,
        // damage or audio, and preserve the recovery from the current sample.
        public void CancelPendingAttackContact()
        {
            bool wasAwaiting = AwaitingAttackContact;
            if (licensedMotion != null && licensedMotion.Active && !licensedMotion.Contacted)
            {
                PlayIdle();
                return;
            }
            pendingAttackContactAlias = null;
            if (!wasAwaiting || animationPlayer == null || activeAlias == null) return;
            AnimationClip clip = animationPlayer.GetClip(activeAlias);
            if (clip == null) return;
            poseSecondsLeft = Mathf.Max(.01f,
                (clip.length - Mathf.Clamp(animationPlayer[activeAlias].time, 0f, clip.length)) /
                Mathf.Max(.01f, activeSpeed));
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
            locomotionStrideDistance = Mathf.Max(.1f, clips.approachStrideDistance);
            poseSecondsLeft = Mathf.Max(.1f, seconds);
            proceduralSecondsLeft = 0f;
        }

        public void PlayWaveEnter() => PlayWalk(.7f);

        public void PlayApproach(float seconds) => PlayMovement("Approach", seconds);

        public void PlayReturnHome(float seconds) => PlayMovement("ReturnHome", seconds);

        public void PlayEntranceWalk(float seconds) => PlayMovement("EntranceWalk", seconds);

        // Match foot cycle progress to distance travelled, including acceleration
        // and deceleration. The arena owns translation; this method owns cadence.
        public void SetLocomotionSpeed(float worldUnitsPerSecond)
        {
            if (locomotionStrideDistance <= 0f || animationPlayer == null || activeAlias == null) return;
            AnimationClip clip = animationPlayer.GetClip(activeAlias);
            if (clip == null) return;
            float rigScale = modelRoot.localToWorldMatrix.MultiplyVector(Vector3.forward).magnitude;
            // Stored strides are original FBX rig units. Scaling the actual rig
            // converts them to world distance, including stature and slot scale.
            float worldStride = locomotionStrideDistance * rigScale;
            activeSpeed = Mathf.Max(0f, worldUnitsPerSecond) * clip.length / Mathf.Max(.0001f, worldStride);
        }

        private void PlayMovement(string alias, float seconds)
        {
            if (dead) return;
            defenseActive = false;
            float duration = Mathf.Max(.1f, seconds);
            AnimationClip clip = animationPlayer == null ? null : animationPlayer.GetClip(alias);
            if (clip == null) { PlayWalk(duration); return; }
            BeginPose(alias, Mathf.Max(.1f, clip.length / duration));
            locomotionStrideDistance = Mathf.Max(.1f, alias == "ReturnHome"
                ? clips.returnStrideDistance : alias == "EntranceWalk"
                    ? clips.entranceStrideDistance : clips.approachStrideDistance);
            poseSecondsLeft = duration;
            proceduralSecondsLeft = 0f;
        }

        public void PlayDeath(float seconds = 1.35f)
        {
            if (dead) return;
            licensedMotion?.Cancel();
            defenseActive = false;
            pendingAttackContactAlias = null;
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
            licensedMotion?.ResetForPool();
            licensedContactKey = null;
            bossPresentation = false;
            sampledLicensedPose = frozenPreviousPose = frozenWeaponValid = nativeEntryBlend = false;
            nativeEntryElapsed = 0f;
            SwordTransformOwner = SwordTransformAuthority.DefaultSocket;
            previousAlias = activeAlias = null;
            confirmedCombatStance = CombatIdleStance.Normal;
            dead = false;
            fallbackDeathAngle = 0f;
            fallbackDeathElapsed = 0f;
            poseSecondsLeft = 0f;
            proceduralSecondsLeft = 0f;
            hitStopRemaining = 0f;
            recoilRemaining = 0f;
            defenseActive = false;
            defenseContactResolved = false;
            locomotionStrideDistance = 0f;
            pendingAttackContactAlias = null;
            PlayIdle();
        }

        private void PlayOneShot(string alias, float fallbackSeconds, float lean,
            string alternate = null, float playbackSeconds = 0f)
        {
            if (dead) return;
            defenseActive = false;
            locomotionStrideDistance = 0f;
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

        private bool CaptureDisplayedTransitionPose(string alias, bool force)
        {
            bool sourceIdleTransition = licensedStance != null && (activeAlias == "Idle" || alias == "Idle");
            bool preserveNativeSample = force || sampledLicensedPose || frozenPreviousPose || sourceIdleTransition;
            if (preserveNativeSample)
                for (int i = 0; i < poseTransforms.Length; i++)
                {
                    blendPositions[i] = poseTransforms[i].localPosition;
                    blendRotations[i] = poseTransforms[i].localRotation;
                    blendScales[i] = poseTransforms[i].localScale;
                }
            frozenWeaponValid = preserveNativeSample && grippedWeapon != null;
            if (frozenWeaponValid)
            {
                Transform weapon = grippedWeapon.transform;
                frozenWeaponPosition = modelRoot.InverseTransformPoint(weapon.position);
                frozenWeaponRotation = Quaternion.Inverse(modelRoot.rotation) * weapon.rotation;
                Vector3 worldScale = weapon.lossyScale, modelScale = modelRoot.lossyScale;
                frozenWeaponScale = new Vector3(worldScale.x / modelScale.x,
                    worldScale.y / modelScale.y, worldScale.z / modelScale.z);
                Transform primaryHand = twoHandGrip?.PrimaryHand;
                if (primaryHand != null)
                {
                    frozenWeaponGripPosition = primaryHand.InverseTransformPoint(weapon.position);
                    frozenWeaponGripRotation = Quaternion.Inverse(primaryHand.rotation) * weapon.rotation;
                }
                frozenPrimaryHeldByLeft = twoHandGrip != null && twoHandGrip.PrimaryHeldByLeft;
                frozenSupportHeld = twoHandGrip != null && twoHandGrip.SupportHandHolding;
                frozenSwordStowed = weapon.parent == swordStow ||
                    activeAlias == "ThrowPreparation" || activeAlias == "Throw" ||
                    weapon.parent == modelRoot && frozenSwordStowed;
            }
            return preserveNativeSample;
        }

        private void BeginPose(string alias, float speed)
        {
            if (animationPlayer == null || animationPlayer.GetClip(alias) == null) return;
            // A native Timeline blend has no single legacy alias to resample.
            // Preserve its actual last rig sample when moving back to owned movement/idle.
            bool preserveNativeSample = CaptureDisplayedTransitionPose(alias, false);
            nativeEntryBlend = false;
            licensedMotion?.Cancel();
            sampledLicensedPose = false;
            frozenPreviousPose = preserveNativeSample;
            previousAlias = preserveNativeSample || activeAlias == alias ? null : activeAlias;
            pendingAttackContactAlias = null;
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
            current.weight = previousAlias == null && !frozenPreviousPose ? 1f : 0f;
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
            if (licensedMotion != null && licensedMotion.Active)
            {
                if (nativeEntryBlend) nativeEntryElapsed += step;
                licensedMotion.Tick(step);
                sampledLicensedPose = true;
                if (!licensedMotion.Active) PlayIdle();
                else ApplyLicensedSample();
                recoilRemaining = Mathf.Max(0f, recoilRemaining - step);
                return;
            }
            if (dead) fallbackDeathElapsed += step;
            blendElapsed += step;
            if (animationPlayer != null && activeAlias != null)
            {
                AnimationState current = animationPlayer[activeAlias];
                AdvancePose(current, step * activeSpeed);
                if (AwaitingAttackContact)
                {
                    bool heavy = activeAlias == "Heavy";
                    current.time = Mathf.Min(current.time, animationPlayer.GetClip(activeAlias).length *
                        Mathf.Clamp01(heavy ? clips.heavyContactNormalized : clips.attackContactNormalized));
                }
                if (defenseActive && !defenseContactResolved)
                    current.time = Mathf.Min(current.time, defenseContactTime);
                current.weight = previousAlias == null && !frozenPreviousPose ? 1f : Mathf.Clamp01(blendElapsed / BlendDuration);
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
                if (poseSecondsLeft <= 0f && !dead && !AwaitingAttackContact) PlayIdle();
            }
            if (proceduralSecondsLeft > 0f)
                proceduralSecondsLeft = Mathf.Max(0f, proceduralSecondsLeft - step);
            recoilRemaining = Mathf.Max(0f, recoilRemaining - step);
        }

        private static void AdvancePose(AnimationState state, float seconds)
        {
            float next = state.time + seconds;
            state.time = state.wrapMode == WrapMode.Loop && state.length > 0f
                ? next % state.length : Mathf.Min(next, state.length);
        }

        private void SamplePose()
        {
            if (licensedMotion != null && licensedMotion.Active)
            {
                licensedMotion.Sample();
                sampledLicensedPose = true;
                ApplyLicensedSample();
                return;
            }
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
            CaptureDestinationWeaponGrip();
            if (previousAlias != null || frozenPreviousPose)
                for (int i = 0; i < poseTransforms.Length; i++)
                {
                    Transform bone = poseTransforms[i];
                    bone.localPosition = Vector3.Lerp(blendPositions[i], bone.localPosition, current.weight);
                    bone.localRotation = Quaternion.Slerp(blendRotations[i], bone.localRotation, current.weight);
                    bone.localScale = Vector3.Lerp(blendScales[i], bone.localScale, current.weight);
                }
            UpdateEquipmentPose(false, current.weight);
            if (frozenPreviousPose && current.weight >= 1f)
                frozenPreviousPose = frozenWeaponValid = false;
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
            float dodge = DodgeDisplacement;
            // Ground only when sizing the standing model. Per-frame bounds grounding
            // removes authored jumps and death falls. Bone-local Y animation remains.
            modelRoot.localPosition = new Vector3(anchoredXZ.x +
                (facingRight ? -1f : 1f) * (recoil + dodge), anchoredY, anchoredXZ.y);
        }
    }
}
