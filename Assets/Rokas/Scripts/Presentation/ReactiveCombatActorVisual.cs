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
        private float poseSecondsLeft;
        private float proceduralSecondsLeft;
        private float proceduralDuration;
        private float proceduralLean;
        private float fallbackDeathAngle;
        private bool dead;
        private bool facingRight;
        private Vector2 anchoredXZ;

        public Transform ModelRoot => modelRoot;
        public bool IsDead => dead;
        public bool IsFacingRight => facingRight;

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
            animationPlayer = model.GetComponent<Animation>();
            if (animationPlayer == null) animationPlayer = model.AddComponent<Animation>();
            AddClip(clips.idle, "Idle", WrapMode.Loop);
            AddClip(clips.attack, "Attack", WrapMode.Once);
            AddClip(clips.heavy, "Heavy", WrapMode.Once);
            AddClip(clips.hit, "Hit", WrapMode.Once);
            AddClip(clips.stagger, "Stagger", WrapMode.Once);
            AddClip(clips.death, "Death", WrapMode.ClampForever);
            AddClip(clips.walk, "Walk", WrapMode.Loop);
            AddClip(clips.approach, "Approach", WrapMode.Loop);
            AddClip(clips.returnHome, "ReturnHome", WrapMode.Loop);
            SetFacing(true);
            SetStandingHeight(Mathf.Max(.2f, clips.standingHeight));
            anchoredXZ = new Vector2(modelRoot.localPosition.x, modelRoot.localPosition.z);
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
        }

        public bool TryGetBounds(out Bounds bounds)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in renderers)
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
                animationPlayer["Idle"].speed = 1f;
                animationPlayer.CrossFade("Idle", .14f);
            }
        }

        public void PlayAttack(float playbackSeconds = 0f) =>
            PlayOneShot("Attack", .55f, -7f, null, playbackSeconds);
        public void PlayHeavy(float playbackSeconds = 0f) =>
            PlayOneShot("Heavy", .8f, -12f, "Attack", playbackSeconds);
        public void PlayHit() => PlayOneShot("Hit", .42f, 12f);
        public void PlayStagger() => PlayOneShot("Stagger", .65f, 18f, "Hit");
        public void PlayDefend() => PlayOneShot(null, .45f, 14f);

        public void PlayWalk(float seconds = .7f)
        {
            if (dead) return;
            AnimationClip walk = animationPlayer == null ? null : animationPlayer.GetClip("Walk");
            if (walk == null) { PlayOneShot(null, seconds, -4f); return; }
            animationPlayer.CrossFade("Walk", .12f);
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
            animationPlayer[alias].speed = Mathf.Max(.1f, clip.length / duration);
            animationPlayer.CrossFade(alias, .1f);
            poseSecondsLeft = duration;
            proceduralSecondsLeft = 0f;
        }

        public void PlayDeath()
        {
            if (dead) return;
            dead = true;
            poseSecondsLeft = 0f;
            proceduralSecondsLeft = 0f;
            AnimationClip death = animationPlayer == null ? null : animationPlayer.GetClip("Death");
            if (death != null)
            {
                animationPlayer["Death"].speed = Mathf.Max(.1f, death.length / 1.1f);
                animationPlayer.CrossFade("Death", .08f);
            }
            else
            {
                fallbackDeathAngle = 68f;
                if (animationPlayer != null && animationPlayer.GetClip("Idle") != null)
                    animationPlayer["Idle"].speed = 0f;
            }
        }

        public void ResetForPool()
        {
            dead = false;
            fallbackDeathAngle = 0f;
            poseSecondsLeft = 0f;
            proceduralSecondsLeft = 0f;
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
                animationPlayer[alias].speed = playbackSeconds > 0f
                    ? clip.length / Mathf.Max(.1f, playbackSeconds) : 1f;
                animationPlayer.CrossFade(alias, .08f);
                poseSecondsLeft = Mathf.Max(.1f, playbackSeconds > 0f ? playbackSeconds : clip.length);
                proceduralSecondsLeft = 0f;
            }
            else
            {
                if (animationPlayer != null && animationPlayer.GetClip("Idle") != null)
                    animationPlayer.CrossFade("Idle", .06f);
                poseSecondsLeft = fallbackSeconds;
                proceduralDuration = fallbackSeconds;
                proceduralSecondsLeft = fallbackSeconds;
                proceduralLean = lean;
            }
        }

        private void Update()
        {
            float step = Mathf.Max(0f, Time.deltaTime);
            if (poseSecondsLeft > 0f)
            {
                poseSecondsLeft -= step;
                if (poseSecondsLeft <= 0f && !dead) PlayIdle();
            }
            if (proceduralSecondsLeft > 0f)
                proceduralSecondsLeft = Mathf.Max(0f, proceduralSecondsLeft - step);
        }

        private void LateUpdate()
        {
            if (modelRoot == null) return;
            float lean = 0f;
            if (proceduralSecondsLeft > 0f && proceduralDuration > 0f)
                lean = Mathf.Sin((1f - proceduralSecondsLeft / proceduralDuration) * Mathf.PI) * proceduralLean;
            float yaw = clips.forwardYaw + (facingRight ? 0f : 180f);
            modelRoot.localRotation = Quaternion.Euler(0f, yaw,
                (facingRight ? -1f : 1f) * (lean + fallbackDeathAngle));
            // Imported walk and attack root curves may move the model. The stage owns position.
            modelRoot.localPosition = new Vector3(anchoredXZ.x, modelRoot.localPosition.y, anchoredXZ.y);
            AlignFeet();
        }
    }
}
