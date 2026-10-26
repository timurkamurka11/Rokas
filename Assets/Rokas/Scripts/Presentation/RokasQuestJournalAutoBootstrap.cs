using Rokas.Core;
using UnityEngine;

namespace Rokas.Presentation
{
    /// <summary>
    /// Journal V1.1: independent bridge into the existing runtime hub.
    /// No text-anchor patching of the user's local RokasView.cs is needed.
    /// Avoids new dependencies in combat, laptop, VN, cursor or save code.
    /// </summary>
    [DefaultExecutionOrder(400)]
    public sealed class RokasQuestJournalAutoBootstrap : MonoBehaviour
    {
        private static RokasQuestJournalAutoBootstrap current;
        private RokasBootstrap bootstrap;
        private RokasView boundView;
        private RokasQuestJournalHud journal;
        private float nextSearchAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartJournalRuntime()
        {
            if (current != null) return;
            var host = new GameObject("ROKAS_QuestJournal_V1_1_AutoBootstrap");
            DontDestroyOnLoad(host);
            host.AddComponent<RokasQuestJournalAutoBootstrap>();
        }

        private void Awake()
        {
            if (current != null && current != this)
            {
                Destroy(gameObject);
                return;
            }
            current = this;
        }

        private void Update()
        {
            if (bootstrap == null || bootstrap.View == null ||
                bootstrap.View != boundView)
            {
                ClearBoundJournal();
                if (Time.unscaledTime < nextSearchAt) return;
                nextSearchAt = Time.unscaledTime + .5f;
                TryBindToLiveHub();
                return;
            }

            if (journal == null) return;
            RunPhase phase = bootstrap.Session.State.phase;
            bool mainRoom = phase == RunPhase.Home ||
                            phase == RunPhase.Accepted ||
                            phase == RunPhase.Payment;
            bool allowed = mainRoom &&
                bootstrap.View.CurrentHomeLocation == HomeLocation.MainRoom &&
                bootstrap.Session.State.hubGuildIntroSeen &&
                !bootstrap.View.Paused &&
                !bootstrap.View.LaptopOpen &&
                !bootstrap.View.HubDialogueOpen;

            journal.Tick(Time.unscaledDeltaTime, allowed);
            // This is a visual pass, not a rewrite of RokasView.Escape().
            // Click the journal's dimmed backdrop or book to close the menu.
            // Escape remains owned by existing ROKAS gameplay input.
        }

        private void TryBindToLiveHub()
        {
            bootstrap = FindFirstObjectByType<RokasBootstrap>();
            if (bootstrap == null || bootstrap.View == null ||
                bootstrap.Session == null) return;
            var all = FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            RectTransform stage = null;
            RectTransform panels = null;
            for (int i = 0; i < all.Length; i++)
            {
                RectTransform r = all[i];
                if (r.name != "AuthoredStage" || !r.IsChildOf(bootstrap.transform))
                    continue;
                stage = r;
                panels = stage.Find("Panels") as RectTransform;
                break;
            }
            if (stage == null || panels == null) return;

            // If a local or GitHub-integrated journal already exists, do not
            // create a second book or steal its input; let that implementation run.
            if (stage.Find("ROKAS_Journal_HUD_StageOverlay") != null)
            {
                nextSearchAt = Time.unscaledTime + 2f;
                return;
            }

            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            if (assets == null) return;
            journal = new RokasQuestJournalHud(
                new UiKit(assets, null), stage, panels, null);
            boundView = bootstrap.View;
            Debug.Log("[ROKAS Journal V1.1] Standalone journal attached to Home stage " +
                "without modifying RokasView.cs. Three authored PNGs are required.");
        }

        private void ClearBoundJournal()
        {
            if (journal != null)
            {
                journal.Dispose();
                journal = null;
            }
            boundView = null;
        }

        private void OnDestroy()
        {
            ClearBoundJournal();
            if (current == this) current = null;
        }
    }
}
