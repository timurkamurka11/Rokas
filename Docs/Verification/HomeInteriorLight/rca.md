# Home interior light: pre-fix evidence

Base branch integration/rokas-unified; HEAD and fetched remote 2c8657e1b4ed9237f89d4995fb2b6dabae2965ad; tree c0003c877acc637deff0f58dabc94bbb6f0a3214. Tracked ProjectVersion: m_EditorVersion: 6000.3.19f1. Canonical tracked/staged diffs empty. 28 untracked files inventoried with SHA-256 in baseline.json. Canonical Editor was open; it is not used for experiments.

## Old light source and classification

Mixed composition. Assets/Rokas/Art/Home/ApartmentNight.png (RokasAssets.home GUID 9b1fe41223e65a0bb91140a11bb15a41) visibly contains illuminated paper floor lamp, workbench lamp, hallway, warm bedding/furniture/walls/floor reflections. Opening this source alone, with no Unity overlays, shows the reported persistent light. RokasView creates this as AuthoredStage/WorldIllustration, with UI default rendering and white tint.

WorldEffects adds HomeWarmInteriorGlow using Resources/Weather3/WarmPracticalMask. No separate legacy LampMood/lamp GameObject exists in the current constructor. ColdSpillMask drives HomeColdWindowBounce, StormRoomLiftMask drives HomeStormRoomLift, and WindowGlazingMask clips exterior weather. OutsideParallax samples the home source's window region and foreground frame slices sample the same source. The scene is a runtime authored Canvas rather than a physical-light 3D scene.

## Exact state flow

HomeView LampHotspot -> act -> GameSession.SetLamp(!session.State.lampOn) -> State.lampOn and Changed -> save(). RokasView.Tick passes the same state to WorldEffects.Tick -> ApplyLighting. This is an existing serialized save field, default true, with immediate button save and normal save/reload. No lighting fade currently exists.

ApplyLighting previously changed the warm mask between .115 and .008 times profile glow/pulse, and added flash*.012 even while OFF. It did not change WorldIllustration or any baked artwork. RebuildScene chose assets.home for all Home phases regardless of lamp state. Thus toggling cannot extinguish a lamp drawn in the opaque base PNG.

## Selected bounded fix

Keep the original ON illustration byte-for-byte. Add a separate authored OFF representation removing the internal warm source illumination while retaining room geometry and cold exterior readability. WorldEffects receives the existing home source from WorldIllustration and uses the same lampOn input to select the Home illustration and matching foreground slices only while atHome. Exterior window plane keeps its approved source, and all rain/glazing/cold spill/storm layers remain independent. OFF warm mask alpha becomes exactly zero; lightning is rendered by the existing exterior/room storm masks. No additional boolean owner, darkness card, save schema, animation system or weather lifecycle is introduced.

Imagegen produced a reviewable non-destructive OFF candidate outside the project. It will only become a production asset after the red regression run. Actual runtime captures and hierarchy inventories will verify the composition and alignment.

## Baseline validator discrepancy

Fresh unchanged baseline has 23 findings, not the historical 13: the same historical findings plus ten below-HD Weather3 textures. See validator-baseline.log. These are present at the approved base; no validator rule changes are planned. Final comparison must have zero added findings.
