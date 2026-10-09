# Missing death Foley

Added one original procedural 0.36s stereo PCM cue (48kHz/16bit, seed907, SHA256 2ac00195b62af7aa5c9dfcba45d2909a2b9cc14b4e2dba01c341077fb7837ef3). No external waveforms or DD2 audio. Existing six Fidelity3 clips remain byte-identical.

MissionView emits it only for positive HitResolved with a nonempty enemy target that Core reports dead. ReactiveCombatAudio deduplicates the dead actor until encounter reset, independently of replayed action IDs. It does not replace existing flesh contact, voice or animation events. Core, HP, input and save rules are unchanged.

Verification includes actual AudioSource DSP progression, duplicate-no-restart and reset, plus the existing real Bootstrap/Core/UI full attack-flow assertion requiring a death dispatch and a dead Core actor. Audible capture is separately blocked: loopback packets contain zero PCM. Windows19044 is below the documented20348 support floor, but successful API activation was observed; the exclusive cause remains unproven. Source/listener PCM is measured independently.

Final affected audio/real-UI suite:15/15PASS (FinalFoleyAndActualUiAcceptance-1791542430472). Independent source/listener PCM test:1/1PASS (DeathFoleyActualPcm-1791542879189), both peak0.04492188, sourceVolume0.128, sourceMutefalse, virtualfalse, ListenerVolume1, pausefalse, EditorMutefalse,48kHz. This proves nonzero Unity DSP mix, not successful Windows loopback capture or subjective sound quality.
