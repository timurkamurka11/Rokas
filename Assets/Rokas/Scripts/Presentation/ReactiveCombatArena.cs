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
        private readonly Dictionary<string, int> formationRanks = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, CorpseState> corpses =
            new Dictionary<string, CorpseState>(StringComparer.Ordinal);
        private readonly List<string> retired = new List<string>();
        private enum MotionPhase { None, Preview, PreviewRestore, PreviewConfirmed, CancelRestore, Prepare, FrameRestore, Entrance, Approach, AtStrike, ReturnOrient, Return, StageReturn, HomeOrient }
        private enum HunterCommandPose { None, Attack, Heavy, Throw, Defend }
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
            public bool Throw;
            public bool Released;
            public float StrikeLead;
            public float ReturnDelay;
            public bool ReturnRequested;
            public bool PreserveNativeRecovery;
            public bool StageOffsetActive;
            public Vector3 StageOffset, StageBasePosition;
            public float StageReturnInitialStep = -1f;
            public float ActionElapsed;
            public int NextHitIndex;
            public AttackSequenceDefinition AttackSequence;
            public string ActionId;
            public float StrikeElapsed;
            public float StrikeContact;
            public string StrikeHitId;
            public bool SwingSent;
        }

        private sealed class CorpseState
        {
            public ReactiveCombatActorVisual Actor;
            public Vector3 Origin;
            public ReactiveCombatAshDissolve Ash;
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
        private float formationSourceScale = 1f;
        private static readonly Vector3 SourceZoomFloor = new Vector3(0f, 0f, -7f);
        private LicensedCombatCameraPlayer licensedCamera;
        private ReactiveCombatActorVisual stageOffsetPerformer;
        private ReactiveCombatActorVisual licensedCameraPerformer;
        private bool tacticalCameraOrthographic = true;
        private float tacticalCameraSize = 4.6f;
        private float tacticalCameraFov = 60f;
        private Vector3 tacticalCameraPosition = new Vector3(0f, 2.25f, -20f);
        private Quaternion tacticalCameraRotation = Quaternion.identity;
        private readonly HashSet<string> presentedContacts = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> contactHistory = new Queue<string>();
        private RenderTexture texture;
        private RawImage image;
        private Material actorComposite;
        private ReactiveCombatActorVisual hunter;
        private string heavyHunterActionId;
        private string throwHunterActionId;
        public bool ThrowReady => hunter != null && hunter.ThrowReady;
        private string heavyEnemyActionId;
        private float settleRemaining;
        private bool cinematicEnabled;
        private enum IntroPhase { None, HunterWalk, HunterStance, HunterSettle, PortalOpen, EnemyWait, EnemyWalk, PortalResidual, PortalClose }
        public enum WavePortalPhase { Inactive, Forming, Open, EmittingEnemies, Residual, Collapsing, Closed }
        private IntroPhase introPhase;
        private float introElapsed;
        private readonly Queue<string> entranceQueue = new Queue<string>();
        private string enteringEnemy;
        private bool enteringCrossed;
        private int waveEntered;
        private float enemyInterval;
        public int PortalFormationCount { get; private set; }
        public int PortalOpenCount { get; private set; }
        public int PortalCloseCount { get; private set; }
        public int PortalCrossingCount { get; private set; }
        public WavePortalPhase PortalPhase => introPhase == IntroPhase.PortalOpen ? WavePortalPhase.Forming :
            introPhase == IntroPhase.EnemyWait ? WavePortalPhase.Open :
            introPhase == IntroPhase.EnemyWalk ? WavePortalPhase.EmittingEnemies :
            introPhase == IntroPhase.PortalResidual ? WavePortalPhase.Residual :
            introPhase == IntroPhase.PortalClose ? WavePortalPhase.Collapsing :
            PortalFormationCount > 0 && introPhase == IntroPhase.None ? WavePortalPhase.Closed : WavePortalPhase.Inactive;
        private ReactiveCombatPortalEffect portal;
        private ReactiveCombatPortalEmergence emergence;
        private ReactiveCombatSwordEffect swordEffect;
        private ReactiveCombatImpactEffect impactEffect;
        private ReactiveCombatThrowEffect throwEffect;
        private Vector3 entranceStart;
        private Vector3 cameraFocus;
        private float cameraEmphasis;
        private float selectionFrameSize;
        private Vector3 selectionFramePosition;
        private float cameraImpulseRemaining;
        private float cameraImpulseStrength;
        private const float HunterEntranceSeconds = 3.2f;
        // Native reference PTS measured from the first floor seed at .800 seconds:
        // reveal 2.7667, exit 4.8667, contraction 5.2667, core gone 5.8667.
        private const float PortalOpeningSeconds = 2.766667f;
        private const float EnemyEntranceSeconds = 2.1f;
        private const float PortalResidualSeconds = .55f;
        private const float PortalClosingSeconds = .6f;
        private const float PortalGhostSeconds = .2f;
        private const float SelectionFrameReturnSeconds = .26f;
        public bool HunterEntryComplete { get; private set; }
        public string HunterPresentationActionId { get; private set; }
        public bool SelectionVisible => hunterMotion.Phase == MotionPhase.Preview || hunterMotion.Phase == MotionPhase.PreviewRestore ||
            hunterMotion.Phase == MotionPhase.PreviewConfirmed || hunterMotion.Phase == MotionPhase.Prepare || hunterMotion.Phase == MotionPhase.FrameRestore;
        public bool PreviewConfirmed => hunterMotion.Phase == MotionPhase.PreviewConfirmed && CameraAtHome;
        public bool CommandReady => PresentationReady || hunterMotion.Phase == MotionPhase.Preview || PreviewConfirmed;
        public bool SelectedHeavy => hunterMotion.Heavy;
        public bool SelectedThrow => hunterMotion.Throw;
        public event Action<string> ThrowReleased;
        public int ThrowReleaseCount => throwEffect == null ? 0 : throwEffect.ReleaseCount;
        public int ThrowContactCount => throwEffect == null ? 0 : throwEffect.ContactCount;
        public float EnemyContactResolutionDelay { get; set; } = .10f;
        public float PlayerContactResolutionDelay { get; set; } = .04f;
        public float HeavyContactResolutionDelay { get; set; } = .09f;
        public event Action<string, string, bool> AttackPresentationStarted;
        public event Action<string, string, bool, string> SwingStarted;
        public bool LicensedCameraActive => licensedCamera != null && licensedCamera.Active;
        public float LicensedCameraClock => licensedCamera == null ? 0f : licensedCamera.Clock;
        public string HunterMotionPhase => hunterMotion.Phase.ToString();
        public string EnemyMotionPhase(string id) => id != null && enemyMotions.TryGetValue(id, out ActorMotion motion)
            ? motion.Phase.ToString() : null;
        public bool CameraAtHome => camera != null && camera.orthographic == tacticalCameraOrthographic &&
            (licensedCamera == null || licensedCamera.IsHome) &&
            camera.orthographicSize == tacticalCameraSize && camera.fieldOfView == tacticalCameraFov &&
            camera.transform.localPosition.Equals(tacticalCameraPosition) &&
            camera.transform.localRotation.Equals(tacticalCameraRotation);
        public bool IntroComplete => introPhase == IntroPhase.None && entranceQueue.Count == 0;
        public bool PresentationReady
        {
            get
            {
                if (!IntroComplete || settleRemaining > 0f ||
                    hunterMotion.Phase != MotionPhase.None || hunterMotion.ReturnRequested ||
                    hunter != null && !hunter.IsDead && !hunter.IdleSettled) return false;
                foreach (var motion in enemyMotions.Values)
                    if (motion.Phase != MotionPhase.None || motion.ReturnRequested ||
                        motion.Actor != null && !motion.Actor.IsDead && !motion.Actor.IdleSettled) return false;
                return true;
            }
        }

        // Presentation timing is independent of Core's attack and defense windows.
        public float HunterApproachDuration { get; set; } = .84f;
        public float HunterReturnDuration { get; set; } = .72f;
        public float HunterAttackDistance { get; set; } = 1.35f;
        public float EnemyApproachDuration { get; set; } = .65f;
        public float EnemyReturnDuration { get; set; } = .68f;
        public float EnemyAttackDistance { get; set; } = 1.3f;
        public float DeathFallDuration { get; set; } = 1.35f;
        public float CorpseHoldDuration { get; set; } = 1.0f;
        public float CorpseDissolveDuration { get; set; } = 1.2f;
        public float HitStopDuration { get; set; } = .08f;
        public float HeavyHitStopDuration { get; set; } = .10f;
        public float ResultLingerDuration { get; set; } = .26f;

        public bool Ready { get { return world != null && hunter != null && texture != null; } }
        public int VisibleEnemyCount
        {
            get { int count = 0; foreach (var actor in enemies.Values)
                if (actor != null && actor.gameObject.activeSelf && !actor.IsDead) count++; return count; }
        }
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
            // The transparent actor camera already stores premultiplied RGB.
            // Keep that RGB intact when placing its render texture over the arena.
            var compositeShader = Resources.Load<Shader>("Combat/ReactiveCombatPremultipliedUi");
            if (compositeShader != null)
            {
                actorComposite = new Material(compositeShader) { name = "Reactive actor UI composite" };
                image.material = actorComposite;
            }
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
                if (hunter.DefaultLicensedProfile != null)
                    formationSourceScale = hunter.ModelRoot.lossyScale.y * hunter.DefaultLicensedProfile.sourceToTargetScale;
                hunter.transform.localPosition = new Vector3(-.7f * formationSourceScale, -.62f, 0f);
                hunterMotion.Home = hunter.transform.localPosition;
                SetLayerRecursive(hunter.gameObject, ActorLayer);
                hunter.SetFacing(true);
                hunter.PlayIdle();
                swordEffect = new ReactiveCombatSwordEffect(hunter);
            }
            ConfigureTacticalCamera();
            licensedCamera = new LicensedCombatCameraPlayer(camera);
            impactEffect = new ReactiveCombatImpactEffect(world.transform, ActorLayer);
        }

        // The encounter owns the horizontal entrance. The imported clip supplies the final stance.
        public void BeginEncounterIntro()
        {
            if (hunter == null || cinematicEnabled) return;
            cinematicEnabled = true;
            introPhase = IntroPhase.HunterWalk;
            introElapsed = 0f;
            float rightExtent = 0f;
            foreach (Renderer renderer in hunter.GetComponentsInChildren<Renderer>(true))
                rightExtent = Mathf.Max(rightExtent, renderer.bounds.max.x - hunter.transform.position.x);
            float leftEdge = -camera.orthographicSize * camera.aspect;
            entranceStart = new Vector3(leftEdge - rightExtent - 1.5f, hunterMotion.Home.y, hunterMotion.Home.z);
            hunter.transform.localPosition = entranceStart;
            hunterMotion.Phase = MotionPhase.Entrance;
            HunterEntryComplete = false;
            hunter.PlayEntranceWalk(HunterEntranceSeconds);
        }

        private void TickIntro(float step)
        {
            if (introPhase == IntroPhase.None) return;
            introElapsed += step;
            if (introPhase == IntroPhase.HunterWalk)
            {
                MoveEntrance(hunter, entranceStart, hunterMotion.Home, introElapsed, HunterEntranceSeconds, step);
                if (introElapsed < HunterEntranceSeconds) return;
                hunter.transform.localPosition = hunterMotion.Home;
                hunter.PlayEnterBattle(hunter.EnterBattleDuration);
                introPhase = IntroPhase.HunterStance;
                introElapsed = 0f;
            }
            else if (introPhase == IntroPhase.HunterStance)
            {
                if (!hunter.ActionRecoveryComplete) return;
                hunterMotion.Phase = MotionPhase.None;
                hunter.PlayIdle();
                introPhase = IntroPhase.HunterSettle;
                introElapsed = 0f;
            }
            else if (introPhase == IntroPhase.HunterSettle)
            {
                if (!hunter.IdleSettled || introElapsed < .16f) return;
                HunterEntryComplete = true;
                BeginWavePortal();
            }
            else if (introPhase == IntroPhase.PortalOpen)
            {
                portal.SetProgress(Mathf.Clamp01(introElapsed / PortalOpeningSeconds));
                portal.Tick(step);
                if (introElapsed < PortalOpeningSeconds) return;
                PortalOpenCount++;
                if (PrepareNextYokai(false)) StartPreparedYokai();
                else BeginPortalResidual();
            }
            else if (introPhase == IntroPhase.EnemyWait)
            {
                portal.Tick(step);
                if (!TryGetEntering(out ActorMotion motion)) { CompleteYokai(false); return; }
                emergence.SetSilhouette(Mathf.Lerp(1f, .72f, Mathf.Clamp01(introElapsed / enemyInterval)));
                emergence.Tick();
                if (introElapsed >= enemyInterval) StartPreparedYokai();
            }
            else if (introPhase == IntroPhase.EnemyWalk)
            {
                portal.Tick(step);
                if (!TryGetEntering(out ActorMotion motion)) { CompleteYokai(false); return; }
                MoveEntrance(motion.Actor, entranceStart, motion.Home, introElapsed, EnemyEntranceSeconds, step);
                emergence.Tick();
                if (!enteringCrossed && emergence.RootPlaneDistance >= 0f)
                {
                    enteringCrossed = true;
                    PortalCrossingCount++;
                    portal.ReactToCrossing(motion.Actor.TorsoPoint);
                }
                if (introElapsed < EnemyEntranceSeconds) return;
                // The shared plane is behind every final slot, including its animated body bounds.
                if (!emergence.ClearedPortal) return;
                motion.Actor.transform.localPosition = motion.Home;
                CompleteYokai(true);
            }
            else if (introPhase == IntroPhase.PortalResidual)
            {
                portal.Tick(step);
                if (entranceQueue.Count > 0 && PrepareNextYokai(true))
                {
                    introPhase = IntroPhase.EnemyWait; introElapsed = 0; return;
                }
                if (introElapsed < PortalResidualSeconds) return;
                PortalCloseCount++;
                introPhase = IntroPhase.PortalClose;
                introElapsed = 0f;
            }
            else if (introPhase == IntroPhase.PortalClose)
            {
                portal.SetProgress(1f - Mathf.Clamp01(introElapsed / PortalClosingSeconds));
                portal.Tick(step);
                if (introElapsed < PortalClosingSeconds + PortalGhostSeconds) return;
                portal.Dispose(); portal = null;
                introPhase = IntroPhase.None;
                enteringEnemy = null;
                settleRemaining = .16f;
                if (entranceQueue.Count > 0) BeginWavePortal();
            }
        }

        private static void MoveEntrance(ReactiveCombatActorVisual actor, Vector3 start, Vector3 goal,
            float elapsed, float duration, float step)
        {
            Vector3 prior = actor.transform.localPosition;
            float t = Mathf.Clamp01(elapsed / duration);
            actor.transform.localPosition = Vector3.Lerp(start, goal, TravelProgress(t, .22f));
            actor.SetLocomotionSpeed(step > 0f ? Vector3.Distance(prior, actor.transform.localPosition) / step : 0f);
        }

        private bool QueuedActorExists(string id)
        {
            if (id == null || !enemyMotions.TryGetValue(id, out ActorMotion motion)) return false;
            if (motion.Actor != null && !motion.Actor.IsDead && !corpses.ContainsKey(id)) return true;
            // A destroyed queued body must release its entrance gate as well as its queue item.
            if (motion.Phase == MotionPhase.Entrance) motion.Phase = MotionPhase.None;
            return false;
        }

        private void BeginWavePortal()
        {
            while (entranceQueue.Count > 0 && !QueuedActorExists(entranceQueue.Peek())) entranceQueue.Dequeue();
            if (entranceQueue.Count == 0)
            {
                introPhase = IntroPhase.None;
                enteringEnemy = null;
                settleRemaining = .16f;
                return;
            }
            Vector3 origin = enemyMotions[entranceQueue.Peek()].Home;
            foreach (string id in entranceQueue)
            {
                if (!QueuedActorExists(id)) continue;
                Vector3 home = enemyMotions[id].Home;
                origin.x = Mathf.Max(origin.x, home.x);
                origin.y = Mathf.Min(origin.y, home.y);
                origin.z = Mathf.Max(origin.z, home.z);
            }
            // One fixed aperture belongs to the whole queue. Keep all final body
            // volumes in front of its plane so removing each mask cannot reveal a clipped limb.
            origin.x += 1.5f;
            origin.x = Mathf.Min(origin.x, 6.15f);
            origin.z += 2.5f;
            Vector3 normal = Quaternion.Euler(0, 15, 0) * Vector3.back;
            foreach (string id in entranceQueue)
            {
                if (!QueuedActorExists(id)) continue;
                float clearance = Vector3.Dot(enemyMotions[id].Home - origin, normal);
                if (clearance < 2.5f) origin.z += (2.5f - clearance) / -normal.z;
            }
            portal = new ReactiveCombatPortalEffect(world.transform, origin, ActorLayer);
            PortalFormationCount++;
            waveEntered = 0;
            introPhase = IntroPhase.PortalOpen;
            introElapsed = 0f;
        }

        private bool PrepareNextYokai(bool silhouette)
        {
            while (entranceQueue.Count > 0)
            {
                string id = entranceQueue.Dequeue();
                if (!QueuedActorExists(id)) continue;
                enteringEnemy = id; enteringCrossed = false;
                ActorMotion motion = enemyMotions[id];
                motion.Phase = MotionPhase.Entrance;
                motion.Actor.transform.position = portal.FloorPosition - portal.PlaneNormal * .9f;
                entranceStart = motion.Actor.transform.localPosition;
                emergence = new ReactiveCombatPortalEmergence(motion.Actor, portal);
                emergence.Begin(); emergence.SetSilhouette(silhouette ? 1f : 0f);
                motion.Actor.gameObject.SetActive(true);
                enemyInterval = waveEntered % 3 == 1 ? .45f : waveEntered % 3 == 2 ? .65f : .50f;
                return true;
            }
            return false;
        }

        private bool TryGetEntering(out ActorMotion motion)
        {
            motion = null;
            return enteringEnemy != null && enemyMotions.TryGetValue(enteringEnemy, out motion) &&
                motion.Actor != null && !motion.Actor.IsDead && motion.Actor.gameObject.activeInHierarchy;
        }

        private void StartPreparedYokai()
        {
            if (!TryGetEntering(out ActorMotion motion)) { CompleteYokai(false); return; }
            emergence.SetSilhouette(0);
            motion.Actor.PlayApproach(EnemyEntranceSeconds);
            introPhase = IntroPhase.EnemyWalk; introElapsed = 0;
        }

        private void CompleteYokai(bool arrived)
        {
            emergence?.Dispose(); emergence = null;
            if (enteringEnemy != null && enemyMotions.TryGetValue(enteringEnemy, out ActorMotion motion))
            {
                motion.Phase = MotionPhase.None;
                if (motion.Actor != null && !motion.Actor.IsDead) motion.Actor.PlayIdle();
            }
            if (arrived) waveEntered++;
            enteringEnemy = null;
            if (PrepareNextYokai(true)) { introPhase = IntroPhase.EnemyWait; introElapsed = 0; }
            else BeginPortalResidual();
        }

        private void BeginPortalResidual()
        {
            introPhase = IntroPhase.PortalResidual; introElapsed = 0;
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
                        if (cinematicEnabled)
                        {
                            actor.gameObject.SetActive(false);
                            entranceQueue.Enqueue(id);
                        }
                    }
                }
            }
            retired.Clear();
            foreach (var pair in enemies)
                if (!active.Contains(pair.Key) && !corpses.ContainsKey(pair.Key)) retired.Add(pair.Key);
            for (int i = 0; i < retired.Count; i++) Retire(retired[i]);
            Layout(activeIds, waveEnemyIds != null && waveEnemyIds.Count > 0 ? waveEnemyIds : activeIds);
            if (cinematicEnabled && entranceQueue.Count > 0)
            {
                foreach (string pending in entranceQueue)
                    if (enemyMotions.TryGetValue(pending, out ActorMotion pendingMotion)) pendingMotion.Phase = MotionPhase.Entrance;
                if (introPhase == IntroPhase.None) BeginWavePortal();
            }
            if (hunterMotion.Phase == MotionPhase.Approach && hunterMotionTargetId != null &&
                !active.Contains(hunterMotionTargetId)) CancelHunterMotion();
        }

        private void Layout(IReadOnlyList<string> activeIds, IReadOnlyList<string> waveEnemyIds)
        {
            if (activeIds == null) return;
            // Source layout packs enabled ranks continuously. Retain occupied corpse slots
            // through the current shot, then compact the living formation after cleanup.
            bool compact = corpses.Count == 0 && CanReflowFormation();
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
                if (compact || !formationRanks.ContainsKey(id)) formationRanks[id] = compact ? activeIndex : i;
                int rank = formationRanks[id];
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
                // Source Front origin .2 plus size-one half-width .5; adjacent centers
                // advance Width(1) + spacing(-.1). Existing depth/stature adaptation stays.
                x = (.7f + .9f * rank) * formationSourceScale;
                Vector3 slot = new Vector3(x, y, z);
                ActorMotion motion;
                if (!enemyMotions.TryGetValue(id, out motion))
                {
                    motion = new ActorMotion { Actor = actor };
                    enemyMotions.Add(id, motion);
                }
                motion.Home = slot;
                motion.Actor = actor;
                // Refresh updates the durable slot while active shot placement owns the root.
                // A passive target has no attack motion; retain its source offset until stage recovery.
                if (motion.Phase == MotionPhase.None)
                {
                    if (motion.StageOffsetActive) motion.StageBasePosition = slot;
                    else actor.transform.localPosition = slot;
                }
                actor.transform.localScale = Vector3.one * scale;
            }
        }

        private bool CanReflowFormation()
        {
            if (LicensedCameraActive || hunterMotion.Phase != MotionPhase.None ||
                hunterMotion.StageOffsetActive || hunterMotion.ReturnRequested || hunter != null && !hunter.IdleSettled) return false;
            foreach (ActorMotion motion in enemyMotions.Values)
                if (motion.Phase != MotionPhase.None || motion.StageOffsetActive || motion.ReturnRequested ||
                    motion.Actor != null && !motion.Actor.IsDead && !motion.Actor.IdleSettled) return false;
            return true;
        }

        private static float ContactDistance(ReactiveCombatActorVisual actor, bool heavy, float fallback)
        {
            LicensedCombatMotionProfile source = actor == null ? null : actor.AttackLicensedProfile(heavy);
            return source == null ? fallback : Mathf.Max(.1f, actor.ModelRoot.lossyScale.y * source.sourceToTargetScale);
        }

        // Movement precedes the committed command. Core still owns the impact and damage.
        public bool SelectHunterPreview(string action)
        {
            if (hunter == null || !CommandReady) return false;
            bool heavy = action == "heavy";
            selectionFrameSize = camera.orthographicSize;
            selectionFramePosition = camera.transform.localPosition;
            hunterMotion.Phase = MotionPhase.Preview;
            hunterMotion.Elapsed = 0f;
            hunterMotion.Heavy = heavy;
            hunterMotion.Throw = action == "throw_blade";
            if (hunterMotion.Throw) hunter.PlayThrowPreparation();
            else hunter.PlayPreparation(heavy);
            if (hunterMotion.Throw && hunter.ThrowingDaggerPrefab != null)
                throwEffect ??= new ReactiveCombatThrowEffect(world.transform, ActorLayer, hunter.ThrowingDaggerPrefab);
            cameraFocus = hunterMotion.Home;
            cameraEmphasis = heavy ? .09f : .065f;
            return true;
        }

        public bool StartHunterThrow(string targetId)
        {
            if (hunter == null || !hunter.ThrowReady || hunter.HeldDagger == null || !CommandReady ||
                !enemies.TryGetValue(targetId ?? "", out ReactiveCombatActorVisual target) || target.IsDead) return false;
            hunterMotionTargetId = targetId;
            hunterApproachComplete = false;
            hunterPosePreplayed = true;
            hunterMotion.Heavy = false;
            hunterMotion.Throw = true;
            hunterMotion.Released = false;
            hunterMotion.StrikeElapsed = 0f;
            hunterMotion.StrikeContact = hunter.ThrowContactSeconds;
            hunterMotion.SwingSent = false;
            hunterMotion.Phase = MotionPhase.AtStrike;
            hunterCommandPose = HunterCommandPose.Throw;
            HunterPresentationActionId = "presentation-" + Guid.NewGuid().ToString("N");
            hunterMotion.ActionId = HunterPresentationActionId;
            hunterAnticipationRemaining = Mathf.Max(0f, hunter.ThrowContactSeconds - .2f);
            throwEffect ??= new ReactiveCombatThrowEffect(world.transform, ActorLayer, hunter.ThrowingDaggerPrefab);
            hunter.PlayThrow();
            return true;
        }

        public bool ConfirmHunterPreview()
        {
            if (hunterMotion.Phase != MotionPhase.Preview) return false;
            RestorePreviewFrame(MotionPhase.PreviewRestore);
            return true;
        }

        public void CancelHunterPreview()
        {
            if (hunterMotion.Phase != MotionPhase.Preview && hunterMotion.Phase != MotionPhase.PreviewRestore &&
                hunterMotion.Phase != MotionPhase.PreviewConfirmed) return;
            hunter.PlayIdle();
            RestorePreviewFrame(MotionPhase.CancelRestore);
        }

        private void RestorePreviewFrame(MotionPhase phase)
        {
            if (hunter.DefaultLicensedProfile != null && CameraAtHome)
            {
                // Native selection never left tactical framing: no empty restore wait.
                hunterMotion.Phase = phase == MotionPhase.CancelRestore ? MotionPhase.None : MotionPhase.PreviewConfirmed;
                hunterMotion.Elapsed = hunterMotion.Duration = 0f;
                return;
            }
            selectionFrameSize = camera.orthographicSize;
            selectionFramePosition = camera.transform.localPosition;
            hunterMotion.Phase = phase;
            hunterMotion.Elapsed = 0f;
            hunterMotion.Duration = SelectionFrameReturnSeconds;
        }

        public bool StartHunterApproach(string targetId, bool heavy = false)
        {
            ReactiveCombatActorVisual target;
            if (hunter == null || string.IsNullOrEmpty(targetId) ||
                !enemies.TryGetValue(targetId, out target) || target == null ||
                target.IsDead || corpses.ContainsKey(targetId)) return false;
            if (hunterMotion.Phase == MotionPhase.Approach && hunterMotionTargetId == targetId) return true;
            // Input is gated until every actor has returned to its permanent slot.
            if (!CommandReady) return false;
            bool prepared = PreviewConfirmed;
            Vector3 targetPosition = enemyMotions.TryGetValue(targetId, out ActorMotion targetMotion)
                ? targetMotion.Home : target.transform.localPosition;
            Vector3 attackPoint = new Vector3(targetPosition.x - ContactDistance(hunter, heavy, HunterAttackDistance), hunterMotion.Home.y,
                targetPosition.z - .45f);
            hunterMotionTargetId = targetId;
            hunterApproachComplete = false;
            hunterPosePreplayed = false;
            hunterMotion.Heavy = heavy;
            hunterMotion.Throw = false;
            HunterPresentationActionId = "presentation-" + Guid.NewGuid().ToString("N");
            hunterMotion.ActionId = HunterPresentationActionId;
            hunterCommandPose = heavy ? HunterCommandPose.Heavy : HunterCommandPose.Attack;
            hunterAnticipationRemaining = Mathf.Max(0f, hunter.AttackContactSeconds(heavy) - .2f);
            if (prepared)
            {
                BeginMotion(hunterMotion, MotionPhase.Approach, attackPoint, HunterApproachDuration);
                hunter.PlayApproach(hunterMotion.Duration);
            }
            else
            {
                BeginMotion(hunterMotion, MotionPhase.Prepare, attackPoint,
                    Mathf.Max(hunter.PreparationDuration(heavy), heavy ? .9f : .65f));
                hunter.PlayPreparation(heavy);
            }
            cameraFocus = hunterMotion.Home;
            cameraEmphasis = heavy ? .09f : .065f;
            return true;
        }

        public void StartHunterReturn() => BeginHunterReturn(false);

        private void BeginHunterReturn(bool preserveNativeRecovery)
        {
            if (hunter == null || hunterMotion.Phase == MotionPhase.Return) return;
            hunterMotion.ReturnRequested = false;
            hunterCommandPose = HunterCommandPose.None;
            hunterMotionTargetId = null;
            hunterApproachComplete = false;
            hunterPosePreplayed = false;
            hunterAnticipationRemaining = 0f;
            hunterMotion.Throw = hunterMotion.Released = false;
            if (!preserveNativeRecovery) ResetCamera(true);
            hunterMotion.PreserveNativeRecovery = preserveNativeRecovery;
            hunterMotion.StageOffsetActive = false;
            if ((hunter.transform.localPosition - hunterMotion.Home).sqrMagnitude < .0001f)
            {
                hunter.transform.localPosition = hunterMotion.Home;
                hunterMotion.Phase = MotionPhase.None;
                if (!preserveNativeRecovery) hunter.PlayIdle();
                settleRemaining = .16f;
                return;
            }
            BeginMotion(hunterMotion, MotionPhase.Return, hunterMotion.Home, HunterReturnDuration);
            hunterMotion.PreserveNativeRecovery = preserveNativeRecovery;
            if (!preserveNativeRecovery) hunter.PlayReturnHome(hunterMotion.Duration);
        }

        public void CancelHunterMotion(bool returnToHome = true)
        {
            bool nativeExit = hunter != null && hunter.ActiveLicensedProfile != null && hunter.LicensedContactConfirmed;
            throwEffect?.Cancel();
            hunterMotion.Throw = hunterMotion.Released = false;
            throwHunterActionId = null;
            hunterMotion.Phase = MotionPhase.None;
            hunterMotion.ReturnDelay = 0f;
            hunterMotion.ReturnRequested = false;
            hunterCommandPose = HunterCommandPose.None;
            hunterMotionTargetId = null;
            hunterApproachComplete = false;
            hunterPosePreplayed = false;
            hunterAnticipationRemaining = 0f;
            HunterPresentationActionId = null;
            ResetCamera(true);
            if (hunter == null) return;
            if (nativeExit)
            {
                // Preserve the displayed root along with the sampled bones/weapon.
                // Travel back through the existing return motion; defeat stays at its current floor position.
                if (returnToHome && (hunter.transform.localPosition - hunterMotion.Home).sqrMagnitude > .0001f)
                {
                    BeginMotion(hunterMotion, MotionPhase.Return, hunterMotion.Home, HunterReturnDuration);
                    hunter.PlayReturnHome(hunterMotion.Duration);
                }
                else hunter.PlayIdle();
                return;
            }
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
            motion.ReturnRequested = false;
            motion.PreserveNativeRecovery = false;
        }

        private void StartEnemyApproach(string id, bool heavy, AttackSequenceDefinition sequence)
        {
            ActorMotion motion;
            if (id == null || !enemyMotions.TryGetValue(id, out motion) ||
                motion.Actor == null || motion.Actor.IsDead || corpses.ContainsKey(id)) return;
            motion.Actor.SetBossPresentation(sequence != null && sequence.Id == "final_triple");
            motion.Heavy = heavy;
            motion.ActionId = null;
            motion.AttackSequence = sequence;
            motion.ActionElapsed = 0f;
            motion.NextHitIndex = 0;
            motion.StrikeLead = Mathf.Max(.1f, (heavy ? 1.45f : 1f) - EnemyApproachDuration);
            // AttackStarted is delivered only after the prior actor has settled at home.
            Vector3 goal = new Vector3(hunterMotion.Home.x + ContactDistance(motion.Actor, motion.Heavy, EnemyAttackDistance),
                motion.Home.y, hunterMotion.Home.z + .1f);
            BeginMotion(motion, MotionPhase.Prepare, goal, .16f);
            motion.Actor.PlayAttackToContact(heavy ? 1.45f : 1f);
        }

        private void StartEnemyReturn(string id)
        {
            ActorMotion motion;
            if (id == null || !enemyMotions.TryGetValue(id, out motion) ||
                motion.Actor == null || motion.Actor.IsDead) return;
            BeginEnemyReturn(motion, false);
        }

        private static void BeginEnemyReturn(ActorMotion motion, bool preserveNativeRecovery, float seconds = .68f)
        {
            if (preserveNativeRecovery)
            {
                BeginMotion(motion, MotionPhase.Return, motion.Home, seconds);
                motion.PreserveNativeRecovery = true;
                motion.StageOffsetActive = false;
            }
            else
            {
                BeginMotion(motion, MotionPhase.ReturnOrient, motion.Home, .14f);
                motion.Actor.PlayIdle();
            }
        }

        private void QueueEnemyReturn(string id, string actionId)
        {
            if (id == null || !enemyMotions.TryGetValue(id, out ActorMotion motion) || motion.Actor == null || motion.Actor.IsDead) return;
            if (actionId != null && motion.ActionId != null && actionId != motion.ActionId) return;
            // Core can interrupt an attack after seal break and cancel its suffix.
            // ActionSettled ends that sequence even if a presentation swing was
            // already waiting for a now-cancelled HitResolved watermark.
            motion.AttackSequence = null;
            motion.NextHitIndex = 0;
            motion.Actor.CancelPendingAttackContact();
            motion.ReturnRequested = true;
            motion.ReturnDelay = Mathf.Max(motion.ReturnDelay, .12f);
        }

        public void BeginDefense(bool dodge, float secondsToContact)
        {
            hunter?.BeginDefense(dodge, secondsToContact);
            if (dodge && hunter != null) impactEffect.DodgeDust(hunter.transform.position);
        }

        private void ConfigureTacticalCamera()
        {
            LicensedCombatMotionProfile profile = hunter?.DefaultLicensedProfile;
            if (profile != null && profile.camera != null)
            {
                // The source wide camera is independent of BasicDamage's -6 root.
                // Preserve the source perspective lens with a measured standing rig scale.
                tacticalCameraOrthographic = false;
                float scale = hunter.ModelRoot.lossyScale.y * profile.sourceToTargetScale;
                tacticalCameraPosition = new Vector3(0f, hunterMotion.Home.y, 0f) +
                    profile.camera.basePosition * scale;
                tacticalCameraFov = profile.camera.baseFov;
                float depth = -profile.camera.basePosition.z * scale;
                tacticalCameraSize = depth * Mathf.Tan(tacticalCameraFov * Mathf.Deg2Rad * .5f);
            }
            ApplyTacticalCamera();
        }

        private void ApplyTacticalCamera()
        {
            camera.orthographic = tacticalCameraOrthographic;
            camera.orthographicSize = tacticalCameraSize;
            camera.fieldOfView = tacticalCameraFov;
            camera.transform.localRotation = tacticalCameraRotation;
            camera.transform.localPosition = tacticalCameraPosition;
        }

        private void ResetCamera(bool blendNativeExit = false)
        {
            if (camera == null) return;
            if (blendNativeExit && licensedCamera != null && licensedCamera.Active)
            {
                licensedCamera.BeginCancel(.08f);
                return;
            }
            licensedCamera?.Cancel();
            licensedCameraPerformer = null;
            ApplyTacticalCamera();
        }

        private void CancelInterruptedLicensedCamera()
        {
            if (licensedCamera != null && licensedCamera.Active && licensedCameraPerformer != null &&
                (licensedCameraPerformer.IsDead || licensedCameraPerformer.ActiveLicensedProfile == null))
                licensedCamera.BeginCancel(.08f);
        }

        public void Present(CombatEvent combatEvent, AttackSequenceDefinition attackSequence = null)
        {
            if (combatEvent == null) return;
            ReactiveCombatActorVisual actor = null;
            switch (combatEvent.Kind)
            {
                case CombatEventKind.CommandCommitted:
                    heavyHunterActionId = combatEvent.Detail == "heavy" ? combatEvent.ActionId : null;
                    throwHunterActionId = combatEvent.Detail == "throw_blade" ? combatEvent.ActionId : null;
                    if (hunter == null) break;
                    if (combatEvent.Detail == "Basic") hunter.CommitCombatStance(CombatIdleStance.Normal);
                    else if (combatEvent.Detail == "heavy") hunter.CommitCombatStance(CombatIdleStance.Heavy);
                    hunterCommandPose = combatEvent.Detail == "throw_blade" ? HunterCommandPose.Throw :
                        combatEvent.Detail == "heavy" ? HunterCommandPose.Heavy :
                        combatEvent.Detail == "Defend" ? HunterCommandPose.Defend :
                        combatEvent.Detail == "Retreat" ? HunterCommandPose.None : HunterCommandPose.Attack;
                    if (hunterMotion.Phase != MotionPhase.Approach && !hunterPosePreplayed)
                        PlayHunterCommandPose(.2f);
                    hunter.BindLicensedContact(combatEvent.ActionId);
                    break;
                case CombatEventKind.AttackStarted:
                    heavyEnemyActionId = combatEvent.Detail == "heavy" ? combatEvent.ActionId : null;
                    StartEnemyApproach(combatEvent.ActorId, combatEvent.Detail == "heavy", attackSequence);
                    if (combatEvent.ActorId != null && enemyMotions.TryGetValue(combatEvent.ActorId, out ActorMotion started))
                        started.ActionId = combatEvent.ActionId;
                    break;
                case CombatEventKind.HitResolved:
                    if (!AcceptPresentationContact(combatEvent)) break;
                    bool heavyContact = IsHeavyContact(combatEvent);
                    bool hunterContact = combatEvent.ActorId == ReactiveDuelDefinitions.HunterId;
                    ReactiveCombatActorVisual performer = hunterContact ? hunter :
                        combatEvent.ActorId != null && enemies.TryGetValue(combatEvent.ActorId, out ReactiveCombatActorVisual sourceActor) ? sourceActor : null;
                    if (performer != null && performer.ConfirmLicensedContact(combatEvent.ActionId, hunterContact ? null : combatEvent.HitId))
                        BeginLicensedCamera(performer, hunterContact ? ActorForTarget(combatEvent.TargetId) : hunter);
                    bool dodge = combatEvent.Detail == "Dodge";
                    bool guard = combatEvent.Detail == "Parry" || combatEvent.Detail == "Perfect";
                    // Native action/shot timelines already contain their source pose holds.
                    // Extra R11 hit stop would stretch the recorded camera/body clocks.
                    bool nativeContact = performer != null && performer.ActiveLicensedProfile != null &&
                        performer.LicensedContactConfirmed;
                    bool sourceClockActive = nativeContact || licensedCamera != null && licensedCamera.Active;
                    float contactStop = sourceClockActive || dodge ? 0f : guard ? .055f :
                        heavyContact ? HeavyHitStopDuration : HitStopDuration;
                    if (combatEvent.TargetId == ReactiveDuelDefinitions.HunterId)
                    {
                        if (hunter != null)
                        {
                            if (dodge || guard) hunter.ResolveDefenseContact(dodge, contactStop);
                            else if (combatEvent.Amount > 0)
                            {
                                if (heavyEnemyActionId != null && combatEvent.ActionId == heavyEnemyActionId)
                                    hunter.PlayStagger();
                                else hunter.PlayHit();
                                hunter.Recoil(.24f);
                            }
                            hunter.HoldPresentation(contactStop);
                        }
                        if (combatEvent.ActorId != null && enemies.TryGetValue(combatEvent.ActorId, out actor) && actor != null)
                        {
                            actor.HoldAttackAtContact(heavyContact, contactStop);
                        }
                        if (hunter != null && !dodge)
                        {
                            Vector3 claw = actor != null ? actor.ClawPointNearest(hunter.BodyBounds.center) : hunter.DefenseContactPoint;
                            Vector3 point = guard && swordEffect != null ? swordEffect.BladePointNearest(claw) :
                                VisibleBodyContact(hunter, claw);
                            if (guard)
                            {
                                // Preserve the blade/claw contact in XY, but expose its
                                // small sparks in front of the two overlapping skins.
                                point.z = Mathf.Min(point.z, hunter.BodyBounds.min.z,
                                    actor != null ? actor.BodyBounds.min.z : point.z) - .1f;
                                impactEffect.Guard(point, Vector3.left);
                            }
                            else if (combatEvent.Amount > 0) impactEffect.Claw(point, heavyContact, Vector3.left);
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
                        actor.HoldPresentation(contactStop);
                        if (hunter != null)
                            hunter.HoldAttackAtContact(heavyContact, contactStop);
                        bool thrownContact = throwHunterActionId != null && combatEvent.ActionId == throwHunterActionId &&
                            combatEvent.ActorId == ReactiveDuelDefinitions.HunterId;
                        Vector3 bladeContact = thrownContact && throwEffect != null ? throwEffect.ContactPosition : swordEffect == null ? actor.BodyBounds.center :
                            swordEffect.BladePointNearest(actor.BodyBounds.center);
                        Vector3 point = VisibleBodyContact(actor, bladeContact);
                        bool projectileResolved = thrownContact && throwEffect != null && throwEffect.ResolveContact();
                        if (combatEvent.Amount > 0)
                        {
                            if (thrownContact)
                            {
                                if (projectileResolved) impactEffect.Throw(point, Vector3.right);
                            }
                            else
                            {
                                swordEffect?.Contact(point, heavyContact);
                                impactEffect.Flesh(point, heavyContact, Vector3.right);
                            }
                        }
                    }
                    CancelInterruptedLicensedCamera();
                    hitStopRemaining = Mathf.Max(hitStopRemaining, contactStop);
                    if (!dodge && !sourceClockActive)
                    {
                        cameraImpulseRemaining = .13f;
                        cameraImpulseStrength = guard ? .025f : heavyContact ? .065f : .035f;
                    }
                    break;
                case CombatEventKind.Defeat:
                    CancelHunterMotion(false);
                    if (hunter != null) hunter.PlayDeath(DeathFallDuration);
                    foreach (var pair in enemyMotions)
                        if (pair.Value.Phase != MotionPhase.None) StartEnemyReturn(pair.Key);
                    break;
                case CombatEventKind.Victory:
                    QueueHunterReturn();
                    break;
                case CombatEventKind.WaveCleared:
                    QueueHunterReturn();
                    foreach (var pair in enemies) Retire(pair.Key);
                    break;
                case CombatEventKind.ActionSettled:
                    if (combatEvent.ActorId == ReactiveDuelDefinitions.HunterId) QueueHunterReturn();
                    else QueueEnemyReturn(combatEvent.ActorId, combatEvent.ActionId);
                    break;
                case CombatEventKind.CommandCancelled:
                    throwEffect?.Cancel();
                    throwHunterActionId = null;
                    StartHunterReturn();
                    break;
            }
        }

        private ReactiveCombatActorVisual ActorForTarget(string id)
        {
            return id != null && enemies.TryGetValue(id, out ReactiveCombatActorVisual target) ? target : null;
        }

        private bool AcceptPresentationContact(CombatEvent evt)
        {
            if (string.IsNullOrEmpty(evt.ActionId)) return true;
            string hit = evt.ActorId == ReactiveDuelDefinitions.HunterId ? "" : evt.HitId ?? "";
            string key = evt.ActorId + ":" + evt.TargetId + ":" + evt.ActionId + ":" + hit;
            if (!presentedContacts.Add(key)) return false;
            contactHistory.Enqueue(key);
            while (contactHistory.Count > 32) presentedContacts.Remove(contactHistory.Dequeue());
            return true;
        }

        private void BeginLicensedCamera(ReactiveCombatActorVisual performer, ReactiveCombatActorVisual target)
        {
            LicensedCombatMotionProfile profile = performer.ActiveLicensedProfile;
            if (profile == null || profile.camera == null || licensedCamera == null) return;
            RestoreStageOffsetsBeforeShot();
            ResetCamera();
            int externalTargets = 1;
            Vector3 targetFloor = target == null ? performer.transform.position : target.transform.position;
            if (performer == hunter && hunterMotion.Heavy)
            {
                externalTargets = 0;
                targetFloor = Vector3.zero;
                foreach (ReactiveCombatActorVisual candidate in enemies.Values)
                {
                    if (candidate == null || candidate.IsDead || !candidate.gameObject.activeSelf) continue;
                    externalTargets++;
                    targetFloor += candidate.transform.position;
                }
                if (externalTargets > 0) targetFloor /= externalTargets;
                else targetFloor = performer.transform.position;
            }
            Vector3 origin = (performer.transform.position + targetFloor) * .5f;
            // ROKAS targets occupy one logical slot and use Medium framing.
            // The source performer's classification and target-count offsets remain authored.
            profile.camera.ResolveParentPose(externalTargets, LicensedCameraFramingSize.Medium,
                out Vector3 parentPosition, out Vector3 parentEuler);
            float scale = performer.ModelRoot.lossyScale.y * profile.sourceToTargetScale;
            // Team placement shifts actors in source stage units, not the camera root.
            // Capture composition origin first so common offsets do not cancel themselves.
            ApplyLicensedStageOffsets(profile, performer, target, scale);
            // Raw camera parent/child positions use the source world frame.
            // Map its ZoomIn floor Z=-7 onto this composition exactly once.
            origin -= world.transform.rotation * (SourceZoomFloor * scale);
            licensedCamera.Begin(profile.camera, origin, Mathf.Max(.001f, scale), world.transform.rotation,
                parentPosition, parentEuler);
            licensedCameraPerformer = performer;
        }

        private ActorMotion MotionForActor(ReactiveCombatActorVisual actor)
        {
            if (actor == hunter) return hunterMotion;
            foreach (ActorMotion motion in enemyMotions.Values)
                if (motion.Actor == actor) return motion;
            return null;
        }

        private static void RestoreStageOffset(ActorMotion motion)
        {
            if (motion == null || !motion.StageOffsetActive || motion.Actor == null) return;
            motion.Actor.transform.localPosition -= motion.StageOffset;
            motion.StageOffsetActive = false;
        }

        private void RestoreStageOffsetsBeforeShot()
        {
            RestoreStageOffset(hunterMotion);
            foreach (ActorMotion motion in enemyMotions.Values) RestoreStageOffset(motion);
            stageOffsetPerformer = null;
        }

        private void ApplyStageOffset(ReactiveCombatActorVisual actor, Vector2 sourceOffset, float scale)
        {
            ActorMotion motion = MotionForActor(actor);
            if (motion == null || actor.IsDead || sourceOffset == Vector2.zero) return;
            motion.StageBasePosition = actor.transform.localPosition;
            Vector3 worldOffset = world.transform.rotation * new Vector3(actor == hunter ? sourceOffset.x : -sourceOffset.x, 0f, sourceOffset.y) * scale;
            motion.StageOffset = world.transform.InverseTransformVector(worldOffset);
            actor.transform.localPosition += motion.StageOffset;
            motion.StageOffsetActive = true;
        }

        private void ApplyLicensedStageOffsets(LicensedCombatMotionProfile profile,
            ReactiveCombatActorVisual performer, ReactiveCombatActorVisual target, float scale)
        {
            if (!profile.sourceStageOffsets) return;
            ApplyStageOffset(performer, profile.performerTeamOffset, scale);
            if (performer == hunter && hunterMotion.Heavy)
            {
                foreach (ReactiveCombatActorVisual candidate in enemies.Values)
                    if (candidate != null && candidate.gameObject.activeSelf)
                        ApplyStageOffset(candidate, profile.targetTeamOffset, scale);
            }
            else if (target != null) ApplyStageOffset(target, profile.targetTeamOffset, scale);
            stageOffsetPerformer = performer;
        }

        private void ReleaseStageOffset(ActorMotion motion, ReactiveCombatActorVisual performer, float initialStep)
        {
            if (!motion.StageOffsetActive || motion.Actor == null) return;
            motion.StageOffsetActive = false;
            // The performer returns from its displayed stage pose to its permanent slot.
            // Targets only undo placement; they retain their own hit/guard bone recovery.
            if (motion.Actor == performer) return;
            Vector3 destination = motion.StageBasePosition;
            if (motion.Actor.IsDead)
            {
                // Corpse persistence must not bake the temporary source team placement.
                motion.Actor.transform.localPosition = destination;
                foreach (CorpseState corpse in corpses.Values)
                    if (corpse.Actor == motion.Actor) corpse.Origin = destination;
                return;
            }
            BeginMotion(motion, MotionPhase.StageReturn, destination,
                motion.Actor == hunter ? HunterReturnDuration : EnemyReturnDuration);
            motion.PreserveNativeRecovery = true;
            motion.StageReturnInitialStep = initialStep;
        }

        private void ReleaseLicensedStageOffsets(float step)
        {
            ReactiveCombatActorVisual performer = stageOffsetPerformer;
            if (performer == null) return;
            float remainingStep = step;
            if (performer.ActiveLicensedProfile != null &&
                !performer.ReachesLicensedStageRecovery(step, out remainingStep)) return;
            ReleaseStageOffset(hunterMotion, performer, remainingStep);
            foreach (ActorMotion motion in enemyMotions.Values) ReleaseStageOffset(motion, performer, remainingStep);
            stageOffsetPerformer = null;
        }

        private bool IsHeavyContact(CombatEvent evt)
        {
            // Counter contacts reuse the enemy action ID. The attacker still
            // determines which authored swing/contact window belongs to it.
            if (evt.ActorId == ReactiveDuelDefinitions.HunterId)
                return evt.ActionId != null && evt.ActionId == heavyHunterActionId;
            if (evt.ActionId != null && evt.ActionId == heavyEnemyActionId) return true;
            if (evt.ActorId == null || !enemyMotions.TryGetValue(evt.ActorId, out ActorMotion motion) ||
                motion.AttackSequence == null) return false;
            foreach (HitDefinition hit in motion.AttackSequence.Hits)
                if (hit.Id == evt.HitId) return hit.IsHeavy;
            return false;
        }

        private static Vector3 VisibleBodyContact(ReactiveCombatActorVisual target, Vector3 source)
        {
            Bounds bounds = target.BodyBounds;
            Vector3 point = bounds.ClosestPoint(source);
            // The actor camera looks along +Z. A root-centred burst was hidden inside
            // the skin; keep contact height from the actual blade/claw and expose
            // only its small effect on the front surface of that same body.
            point.z = bounds.min.z - .1f;
            return point;
        }

        private void QueueHunterReturn()
        {
            if (hunter == null || hunter.IsDead || hunterMotion.Phase == MotionPhase.Return) return;
            if (!hunterMotion.Throw && (hunter.transform.localPosition - hunterMotion.Home).sqrMagnitude < .0001f)
            {
                StartHunterReturn();
                return;
            }
            hunterMotion.ReturnDelay = Mathf.Max(hunterMotion.ReturnDelay, ResultLingerDuration);
            hunterMotion.ReturnRequested = true;
        }

        private void PlayHunterCommandPose(float secondsUntilContact)
        {
            if (hunter == null) return;
            switch (hunterCommandPose)
            {
                case HunterCommandPose.Attack: hunter.PlayAttackToContact(secondsUntilContact); break;
                case HunterCommandPose.Heavy: hunter.PlayHeavyToContact(secondsUntilContact); break;
                case HunterCommandPose.Throw: hunter.PlayThrow(); break;
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
            // Death cleanup must capture the actor's durable materials, never the
            // temporary emergence clones which the cancelled entry is about to destroy.
            if (enteringEnemy == id) { emergence?.Dispose(); emergence = null; }
            actor.PlayDeath(DeathFallDuration);
            CancelInterruptedLicensedCamera();
            corpses[id] = new CorpseState { Actor = actor, Origin = actor.transform.localPosition,
                Ash = new ReactiveCombatAshDissolve(actor) };
        }

        public void Tick(float deltaTime)
        {
            if (world == null) return;
            CancelInterruptedLicensedCamera();
            float step = Mathf.Max(0f, deltaTime);
            float motionStep = step;
            if (hitStopRemaining > 0f)
            {
                float held = Mathf.Min(motionStep, hitStopRemaining);
                hitStopRemaining -= held;
                motionStep -= held;
            }
            ReleaseLicensedStageOffsets(motionStep);
            settleRemaining = Mathf.Max(0f, settleRemaining - motionStep);
            TickIntro(motionStep);
            if (hunterMotion.Phase == MotionPhase.Preview)
            {
                hunterMotion.Elapsed += motionStep;
                if (hunter.ActiveLicensedProfile == null)
                {
                    float push = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(hunterMotion.Elapsed / .3f));
                    camera.orthographicSize = Mathf.Lerp(selectionFrameSize, 4.6f * (1f - cameraEmphasis), push);
                    camera.transform.localPosition = Vector3.Lerp(selectionFramePosition,
                        new Vector3(cameraFocus.x * .22f, 2.25f, -20f), push);
                }
                else ResetCamera();
            }
            else if (hunterMotion.Phase == MotionPhase.Prepare)
            {
                if (hunter.ActiveLicensedProfile == null)
                {
                    float push = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(hunterMotion.Elapsed / .3f));
                    camera.orthographicSize = Mathf.Lerp(4.6f, 4.6f * (1f - cameraEmphasis), push);
                    camera.transform.localPosition = Vector3.Lerp(new Vector3(0f, 2.25f, -20f),
                        new Vector3(cameraFocus.x * .22f, 2.25f, -20f), push);
                }
                else ResetCamera();
            }
            else if (hunterMotion.Phase == MotionPhase.FrameRestore || hunterMotion.Phase == MotionPhase.PreviewRestore ||
                hunterMotion.Phase == MotionPhase.CancelRestore)
            {
                float restore = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((hunterMotion.Elapsed + motionStep) / SelectionFrameReturnSeconds));
                camera.orthographicSize = Mathf.Lerp(selectionFrameSize, tacticalCameraSize, restore);
                camera.transform.localPosition = Vector3.Lerp(selectionFramePosition, tacticalCameraPosition, restore);
            }
            else if (licensedCamera == null || !licensedCamera.Active) ResetCamera();
            cameraImpulseRemaining = Mathf.Max(0f, cameraImpulseRemaining - step);
            if (cameraImpulseRemaining > 0f && (licensedCamera == null || !licensedCamera.Active))
                camera.transform.localPosition += Vector3.right *
                    (Mathf.Sin((.13f - cameraImpulseRemaining) * 72f) * cameraImpulseStrength * cameraImpulseRemaining / .13f);
            if (hunter != null)
            {
                if (hunterMotion.Phase == MotionPhase.PreviewRestore || hunterMotion.Phase == MotionPhase.CancelRestore)
                {
                    hunterMotion.Elapsed += motionStep;
                    if (hunterMotion.Elapsed >= hunterMotion.Duration)
                    {
                        bool cancelled = hunterMotion.Phase == MotionPhase.CancelRestore;
                        ResetCamera();
                        hunterMotion.Phase = cancelled ? MotionPhase.None : MotionPhase.PreviewConfirmed;
                    }
                }
                else if (hunterMotion.Phase == MotionPhase.Preview || hunterMotion.Phase == MotionPhase.PreviewConfirmed) { }
                else if (hunterMotion.Phase == MotionPhase.Prepare)
                {
                    hunterMotion.Elapsed += motionStep;
                    if (hunterMotion.Elapsed >= hunterMotion.Duration)
                    {
                        Vector3 goal = hunterMotion.Goal;
                        float remainder = hunterMotion.Elapsed - hunterMotion.Duration;
                        if (hunter.ActiveLicensedProfile != null && CameraAtHome)
                        {
                            BeginMotion(hunterMotion, MotionPhase.Approach, goal, HunterApproachDuration);
                            hunter.PlayApproach(hunterMotion.Duration);
                            TickMotion(hunterMotion, remainder);
                        }
                        else
                        {
                            selectionFrameSize = camera.orthographicSize;
                            selectionFramePosition = camera.transform.localPosition;
                            BeginMotion(hunterMotion, MotionPhase.FrameRestore, goal, SelectionFrameReturnSeconds);
                            hunterMotion.Elapsed = remainder;
                        }
                    }
                }
                else if (hunterMotion.Phase == MotionPhase.FrameRestore)
                {
                    hunterMotion.Elapsed += motionStep;
                    if (hunterMotion.Elapsed >= hunterMotion.Duration)
                    {
                        Vector3 goal = hunterMotion.Goal;
                        float remainder = hunterMotion.Elapsed - hunterMotion.Duration;
                        ResetCamera();
                        BeginMotion(hunterMotion, MotionPhase.Approach, goal, HunterApproachDuration);
                        hunter.PlayApproach(hunterMotion.Duration);
                        TickMotion(hunterMotion, remainder);
                    }
                }
                else if (hunterMotion.ReturnRequested)
                {
                    if (hunter.LicensedContactConfirmed)
                    {
                        if (hunter.ReachesLicensedStageRecovery(motionStep, out float remainingTravel))
                        {
                            BeginHunterReturn(true);
                            TickMotion(hunterMotion, remainingTravel);
                        }
                    }
                    else
                    {
                        hunterMotion.ReturnDelay = Mathf.Max(0f, hunterMotion.ReturnDelay - motionStep);
                        if (hunterMotion.ReturnDelay <= 0f && hunter.ActionRecoveryComplete) StartHunterReturn();
                    }
                }
                else if (TickMotion(hunterMotion, motionStep))
                {
                    if (hunterMotion.Phase == MotionPhase.AtStrike)
                    {
                        float contact = hunter.AttackContactSeconds(hunterMotion.Heavy);
                        float resultDelay = hunterMotion.Heavy ? HeavyContactResolutionDelay : PlayerContactResolutionDelay;
                        PlayHunterCommandPose(contact + resultDelay);
                        hunter.AwaitAttackContact(hunterMotion.Heavy);
                        hunterMotion.StrikeElapsed = 0f;
                        hunterMotion.StrikeContact = contact + resultDelay;
                        hunterMotion.SwingSent = false;
                        AttackPresentationStarted?.Invoke(HunterPresentationActionId, ReactiveDuelDefinitions.HunterId, hunterMotion.Heavy);
                        hunterPosePreplayed = true;
                        hunterApproachComplete = hunterAnticipationRemaining <= 0f;
                    }
                    else
                    {
                        if (!hunterMotion.PreserveNativeRecovery) hunter.PlayIdle();
                        hunterMotion.PreserveNativeRecovery = false;
                        settleRemaining = .16f;
                    }
                }
                else if (hunterMotion.Phase == MotionPhase.AtStrike && !hunterApproachComplete)
                {
                    hunterAnticipationRemaining = Mathf.Max(0f, hunterAnticipationRemaining - motionStep);
                    hunterApproachComplete = hunterAnticipationRemaining <= 0f;
                }
                if (hunterMotion.Phase == MotionPhase.AtStrike)
                {
                    hunterMotion.StrikeElapsed += motionStep;
                    if (hunterMotion.Throw)
                    {
                        if (!hunterMotion.Released && hunter.CurrentPoseSeconds >= hunter.ThrowReleaseSeconds)
                        {
                            Transform dagger = hunter.HeldDagger;
                            if (dagger != null && enemies.TryGetValue(hunterMotionTargetId, out ReactiveCombatActorVisual target))
                            {
                                Vector3 point = VisibleBodyContact(target, target.TorsoPoint);
                                if (throwEffect.Release(HunterPresentationActionId, dagger.position, point, dagger.lossyScale,
                                    Mathf.Max(.08f, hunter.ThrowContactSeconds - hunter.ThrowReleaseSeconds)))
                                {
                                    hunter.ReleaseThrowWeapon();
                                    hunterMotion.Released = true;
                                    ThrowReleased?.Invoke(HunterPresentationActionId);
                                }
                            }
                        }
                    }
                    else EmitSwing(hunterMotion, ReactiveDuelDefinitions.HunterId);
                }
            }
            foreach (var pair in enemyMotions)
            {
                ActorMotion motion = pair.Value;
                if (corpses.ContainsKey(pair.Key) || motion.Actor == null) continue;
                if (motion.ReturnRequested)
                {
                    if (motion.Actor.LicensedContactConfirmed)
                    {
                        if (motion.Actor.ReachesLicensedStageRecovery(motionStep, out float remainingTravel))
                        {
                            BeginEnemyReturn(motion, true, EnemyReturnDuration);
                            TickMotion(motion, remainingTravel);
                        }
                    }
                    else
                    {
                        motion.ReturnDelay = Mathf.Max(0f, motion.ReturnDelay - motionStep);
                        if (motion.ReturnDelay <= 0f && motion.Actor.ActionRecoveryComplete) StartEnemyReturn(pair.Key);
                    }
                    continue;
                }
                if (motion.Phase == MotionPhase.ReturnOrient || motion.Phase == MotionPhase.HomeOrient)
                {
                    motion.Elapsed = Mathf.Min(motion.Duration, motion.Elapsed + motionStep);
                    float t = Mathf.SmoothStep(0f, 1f, motion.Elapsed / motion.Duration);
                    bool returning = motion.Phase == MotionPhase.ReturnOrient;
                    motion.Actor.transform.localRotation = Quaternion.Euler(0f, returning ? 180f * t : 180f * (1f - t), 0f);
                    if (motion.Elapsed >= motion.Duration)
                    {
                        if (returning)
                        {
                            BeginMotion(motion, MotionPhase.Return, motion.Home, EnemyReturnDuration);
                            motion.Actor.PlayReturnHome(motion.Duration);
                        }
                        else
                        {
                            motion.Actor.transform.localRotation = Quaternion.identity;
                            motion.Phase = MotionPhase.None;
                            motion.Actor.PlayIdle(); settleRemaining = .16f;
                        }
                    }
                    continue;
                }
                if (motion.Phase == MotionPhase.Prepare || motion.Phase == MotionPhase.Approach ||
                    motion.Phase == MotionPhase.AtStrike)
                    motion.ActionElapsed += step;
                float travelStep = motionStep;
                if (motion.Phase == MotionPhase.Prepare)
                {
                    motion.Elapsed += motionStep;
                    if (motion.Elapsed < motion.Duration) continue;
                    travelStep = motion.Elapsed - motion.Duration;
                    Vector3 goal = motion.Goal;
                    BeginMotion(motion, MotionPhase.Approach, goal, EnemyApproachDuration);
                    motion.Actor.PlayApproach(motion.Duration);
                }
                if (TickMotion(motion, travelStep))
                {
                    if (motion.Phase == MotionPhase.AtStrike)
                        PlayEnemyStrike(motion);
                    else
                    {
                        if (motion.PreserveNativeRecovery)
                        {
                            motion.PreserveNativeRecovery = false;
                            settleRemaining = .16f;
                        }
                        else
                        {
                            BeginMotion(motion, MotionPhase.HomeOrient, motion.Home, .14f);
                            motion.Actor.PlayIdle();
                        }
                    }
                }
                else if (motion.Phase == MotionPhase.AtStrike && motion.AttackSequence != null &&
                    motion.NextHitIndex < motion.AttackSequence.Hits.Count &&
                    !motion.Actor.AwaitingAttackContact &&
                    motion.Actor.HitStopRemaining <= 0f)
                {
                    float until = (float)(motion.AttackSequence.Hits[motion.NextHitIndex].ImpactUs / 1000000d) -
                        motion.ActionElapsed;
                    if (until <= Mathf.Min(.55f, motion.Actor.AttackContactSeconds(motion.Heavy)))
                        PlayEnemyStrike(motion);
                }
                if (motion.Phase == MotionPhase.AtStrike)
                {
                    motion.StrikeElapsed += motionStep;
                    EmitSwing(motion, pair.Key);
                }
            }
            // Travel computes this frame's actual distance before locomotion is sampled.
            licensedCamera?.Tick(motionStep);
            hunter?.TickPresentation(step);
            foreach (var pair in enemies) pair.Value?.TickPresentation(step);
            float beforeContact = hunterMotion.StrikeContact - hunterMotion.StrikeElapsed;
            float swingStart = hunter != null && hunter.AttackContactSeconds(hunterMotion.Heavy) > 0f
                ? hunter.AttackSwingStartSeconds(hunterMotion.Heavy) * hunterMotion.StrikeContact /
                    hunter.AttackContactSeconds(hunterMotion.Heavy) : float.MaxValue;
            bool swordSwing = !hunterMotion.Throw && hunterMotion.Phase == MotionPhase.AtStrike &&
                hunterMotion.StrikeElapsed >= swingStart &&
                beforeContact >= -.15f && !hunterMotion.ReturnRequested;
            float charge = hunterMotion.Phase == MotionPhase.Prepare && hunterMotion.Heavy
                ? Mathf.Clamp01(hunterMotion.Elapsed / hunterMotion.Duration)
                : hunterMotion.Phase == MotionPhase.Preview && hunterMotion.Heavy ? .45f : 0f;
            swordEffect?.Tick(motionStep, swordSwing, hunterMotion.Heavy, charge);
            impactEffect?.Tick(motionStep);
            throwEffect?.Tick(motionStep);
            if (throwEffect != null && hunter != null)
                throwEffect.PreviewCharge(hunter.HeldDagger == null ? hunter.transform.position : hunter.HeldDagger.position,
                    hunterMotion.Throw && (hunterMotion.Phase == MotionPhase.Preview ||
                    hunterMotion.Phase == MotionPhase.AtStrike && !hunterMotion.Released));
            retired.Clear();
            float dissolveStarts = Mathf.Max(.1f, DeathFallDuration) + Mathf.Max(0f, CorpseHoldDuration);
            float dissolveDuration = Mathf.Max(.1f, CorpseDissolveDuration);
            foreach (var pair in corpses)
            {
                CorpseState corpse = pair.Value;
                corpse.Elapsed += motionStep;
                if (corpse.Elapsed >= dissolveStarts + dissolveDuration) { retired.Add(pair.Key); continue; }
                if (corpse.Actor != null && corpse.Elapsed >= dissolveStarts)
                {
                    corpse.Ash.Begin();
                    corpse.Ash.SetProgress(Mathf.Clamp01((corpse.Elapsed - dissolveStarts) / dissolveDuration));
                    corpse.Actor.transform.localPosition = corpse.Origin;
                }
            }
            for (int i = 0; i < retired.Count; i++)
            {
                string id = retired[i];
                ReactiveCombatActorVisual actor;
                if (enemies.TryGetValue(id, out actor) && actor != null)
                    UnityEngine.Object.Destroy(actor.gameObject);
                corpses[id].Ash.Dispose();
                enemies.Remove(id);
                corpses.Remove(id);
                enemyMotions.Remove(id);
            }
        }

        private void EmitSwing(ActorMotion motion, string actorId)
        {
            float contact = motion.Actor.AttackContactSeconds(motion.Heavy);
            float acceleration = contact > 0f ? motion.Actor.AttackSwingStartSeconds(motion.Heavy) / contact : .7f;
            if (motion.SwingSent || string.IsNullOrEmpty(motion.ActionId) ||
                motion.StrikeElapsed < Mathf.Max(0f, motion.StrikeContact * acceleration)) return;
            motion.SwingSent = true;
            SwingStarted?.Invoke(motion.ActionId, actorId, motion.Heavy, motion.StrikeHitId);
        }

        private void PlayEnemyStrike(ActorMotion motion)
        {
            float until = motion.StrikeLead;
            bool heavy = motion.Heavy;
            if (motion.AttackSequence != null && motion.NextHitIndex < motion.AttackSequence.Hits.Count)
            {
                HitDefinition hit = motion.AttackSequence.Hits[motion.NextHitIndex];
                until = (float)(hit.ImpactUs / 1000000d) + EnemyContactResolutionDelay - motion.ActionElapsed;
                heavy = hit.IsHeavy;
                motion.StrikeHitId = hit.Id;
            }
            motion.StrikeElapsed = 0f;
            motion.StrikeContact = Mathf.Max(.08f, until);
            motion.SwingSent = false;
            motion.Heavy = heavy;
            motion.NextHitIndex++;
            if (heavy) motion.Actor.PlayHeavyToContact(Mathf.Max(.08f, until));
            else motion.Actor.PlayAttackToContact(Mathf.Max(.08f, until));
            motion.Actor.AwaitAttackContact(heavy);
            motion.Actor.BindLicensedContact(motion.ActionId, motion.StrikeHitId);
        }

        private static bool TickMotion(ActorMotion motion, float step)
        {
            if (motion.Actor == null || motion.Actor.IsDead ||
                (motion.Phase != MotionPhase.Approach && motion.Phase != MotionPhase.Return &&
                 motion.Phase != MotionPhase.StageReturn)) return false;
            if (motion.Phase == MotionPhase.StageReturn && motion.StageReturnInitialStep >= 0f)
            {
                step = Mathf.Min(step, motion.StageReturnInitialStep);
                motion.StageReturnInitialStep = -1f;
            }
            motion.Elapsed = Mathf.Min(motion.Duration, motion.Elapsed + step);
            float fraction = Mathf.Clamp01(motion.Elapsed / motion.Duration);
            // Ease only the first/last tenth. Cruising speed is nearly constant and
            // never has SmoothStep's pronounced 1.5x middle-of-run speed peak.
            float edge = motion.Phase == MotionPhase.Return ? .24f : .20f;
            float progress = TravelProgress(fraction, edge);
            Vector3 prior = motion.Actor.transform.localPosition;
            motion.Actor.transform.localPosition = Vector3.Lerp(motion.Start, motion.Goal, progress);
            motion.Actor.SetLocomotionSpeed(step > 0f ? Vector3.Distance(prior, motion.Actor.transform.localPosition) / step : 0f);
            if (fraction < 1f) return false;
            motion.Actor.transform.localPosition = motion.Goal;
            motion.Phase = motion.Phase == MotionPhase.Approach ? MotionPhase.AtStrike : MotionPhase.None;
            return true;
        }

        private static float TravelProgress(float fraction, float edge)
        {
            return fraction < edge ? fraction * fraction / (2f * edge * (1f - edge)) :
                fraction > 1f - edge ? 1f - (1f - fraction) * (1f - fraction) /
                    (2f * edge * (1f - edge)) : (fraction - edge * .5f) / (1f - edge);
        }

        public void Dispose()
        {
            licensedCamera?.Cancel();
            licensedCamera = null;
            presentedContacts.Clear();
            contactHistory.Clear();
            if (camera != null) camera.targetTexture = null;
            if (image != null) { image.texture = null; image.material = null; }
            ReactiveCombatAshDissolve.DestroyOwned(actorComposite);
            actorComposite = null;
            portal?.Dispose(); portal = null;
            emergence?.Dispose(); emergence = null;
            swordEffect?.Dispose(); swordEffect = null;
            impactEffect?.Dispose(); impactEffect = null;
            throwEffect?.Dispose(); throwEffect = null;
            foreach (var corpse in corpses.Values) corpse.Ash?.Dispose();
            entranceQueue.Clear();
            introPhase = IntroPhase.None;
            enteringEnemy = null;
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
