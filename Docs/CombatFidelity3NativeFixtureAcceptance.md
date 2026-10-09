# Native choreography and legacy fixture acceptance

The affected legacy assertions still assumed autoplay Animation components, orthographic size4.6, and return completion at Core ActionSettled. They now observe actual Native profile/explicit sampled anticipation and the current tactical camera mode/lens/pose; production input gates are unchanged. Lethal visualization is checked immediately at accepted contact, then the Victory bound uses the actual remaining source tail and return travel rather than a fixed3s cut-off.

ViewportAndLegacyNativeAcceptance-1791539961397 passed15/17; its only failures were the first Basic full-round18s wait in both Heavy timing scenarios, before selecting Heavy. HeavyTimingSourceRoundGate-1791541959690 then passed2/2 with all early/timed/late input windows, AP, target locks and damage assertions intact.

Actual18s owner snapshot: Core PlayerCommand and camera/home restored, E2 root at home, motionNone, poseAttack, IdleSettledfalse, Native foot_soldier_atrophic_cut clock2.6245/4.733334s. At20s clock4.6243; readiness arrived21.062s (second run21.074s). The queue waits for the authored tail, final Idle handoff and announcement. This is observed progressing playback, not a stuck transform owner.

Only this complete first Normal round has a finite source-derived maximum45s, calculated from loaded Native profiles, actual Core enemy sequences/hit count, travel and settle/announcement budget. It is conservative because some tails overlap; it is not a new gameplay delay. The real observable readiness predicate terminates the wait immediately. Every shorter Heavy input deadline and physical grip threshold remains strict.

Existing full-suite failures and Heavy physical mesh/support-palm failures remain separately recorded. These focused passes do not convert the saved294-case global PlayMode run into a new globalPASS. The editor bridge additionally exposes read-only audio-mute/listener state to investigate silence; it never alters audio settings.
