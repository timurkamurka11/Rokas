# ROKAS — Anatomical Hand Reconstruction Checkpoint
Status: **SOURCE AUDIT / NOT RELEASE-READY**. PR #13 remains draft.

## Required production target
Existing Home Hub → Bed Click → Sleep Cinematic → Physical Argon Hand & Watch → Confirm → Exit.
No changes to camera framing, OLED hierarchy, sleeve, game clock, or hand entry direction.
Do not merge this work or claim "AAA hand completed" without in-game visual QA.

## Findings grounded in checked-in sources and earlier gameplay logs
- Authentic source: `Assets/Rokas/BedSleep/ArgonSource/Models/ROKAS_Keiko_FPS_Arm_Rigged.fbx`.
- Current gameplay assembly: `RokasArgonConnectedHandRuntime.cs`, `ArgonLivingRig.cs`, `ArgonBedOrganicGesture.cs`.
- Unity skin importer expects **12,235 vertices**, **25 bones** and **6 connected mesh components**: palm + five separated fingers.
- The active runtime remap reported **6,970 influenced finger vertices** and **20 compensated bone slots** with minimal neutral drift in user-provided Unity logs. This proves retarget acceptance, **NOT** smooth geometry or visible articulation.
- `ArgonFingerRootSocketBlend` adjusted **842 vertices** in the same logs without changing connected components. It cannot eliminate the mesh disconnection.
- Blender source FBX and PBR textures exist in the branch, but there is **no accessible running Blender or Unity instance in this session**. Visual completion cannot be claimed.

## Measured source FBX results — Blender/GitHub Actions, 2026-10-19
- Verified workflow: `Argon anatomical source audit`, successful source run #39304378174.
- Blender import: **12,240 mesh vertices**, **23,440 triangles**, **6 disconnected components**.
  Unity's imported runtime representation is separately measured as 12,235 vertices;
  do not assume vertex indices match across importers.
- Palm: 5,265 vertices, exactly **one connected 288-edge open boundary loop**.
- Each individual finger: 1,395 vertices, exactly **one 148-edge open boundary loop**.
- No non-manifold edges in the inspected source; one 4K-compatible UV channel,
  **zero existing corrective Blend Shapes**.
- Proximal finger/palm minimum distances: thumb 1.895mm, **index 0.428mm**,
  middle 1.089mm, ring 0.713mm, **little 0.256mm**.
- Bad source weights are directly measured: index island has major
  `Thumb_Tip`/`Thumb_IP` influences; middle uses `Index_PIP/Index_DIP`,
  ring uses middle-finger bones; little uses ring/middle-finger bones.
- Because **each shell has one long open perimeter, not separately matched
  palm and finger socket rings**, a naive zipper/nearest-vertex bridge is
  unsafe. First identify and cut/reshape the correct anatomical proximal
  boundary with a wireframe view; then join with appropriate supporting loops.
- Actual report is retained as GitHub workflow artifact
  `argon-anatomy-source-defect-map` (run #39304378174).
- **No corrected FBX or in-game visual proof has been produced yet**.

## Source mesh wireframes — actual Blender evidence (2026-10-19)
- Headless Blender workflow `Argon anatomical source audit` run **#39305668827 PASSED**.
- Unmodified imported model rendered to actual projected **SVG wireframes**:
  `full_hand_topology.svg`, `index_base_wireframe.svg`,
  `little_base_wireframe.svg`. Artifact:
  `argon-anatomy-source-defect-map` from workflow run #39305668827.
- Each finger's exposed 148-edge perimeter wraps the visible finger outline,
  including the proximal side, **not** a clean circular proximal socket.
  The palm has its own long exposed 288-edge contour. At the finger roots,
  polygons overlap but do not share connecting topology.
- The colored wireframes show that **moving the finger mesh socket vertices,
  adding more bend keys, or joining arbitrary nearest edges will not create
  anatomically correct connected skin**.
- This requires authoring a continuous palm-to-digit surface, including
  real interdigital webbing, new supporting edge rings and controlled
  weight transfer. Corrective shapes must be sculpted for MCP/PIP closure.
- The current generated PNG/SVG views are SOURCE-SPACE orthographic
  topology diagnostics; they are **not** final in-game screenshots.
- Full surface rebuild, animation pose library, render comparison and
  installation into the game remain **NOT DONE**.

## Source geometry analysis — new audit
Run the read-only `audit_argon_hand_anatomy.py` against the genuine source FBX.
It writes `Artifacts/ArgonAnatomyAudit/ROKAS_Argon_Anatomy_DefectMap.json`.
It measures:
1. True per-vertex connected components and boundary edges.
2. Index/little proximal joint locations and nearest distance to palm.
3. Skin groups influencing each disconnected finger shell.
4. Whether shells have open rings or are capped/closed.
5. Existing UV and shape-key data that could be lost during reconstruction.

**If palm or finger shells are closed**, do **not** weld the closest arbitrary vertices.
First select and open physically matched proximal loops with coherent winding.
Then bridge with proper edge flow, re-normalize skin weights, restore UV continuity,
and sculpt corrective shapes for MCP/PIP bending. Maintain watertight anatomical
surface, no inverted normals, no intersecting fingers, no over-smoothing.

## Defect to solution map
| Defect | Suspected/known source | Mandatory measurable validation |
|---|---|---|
| Index finger appears "inserted" | separate finger island / proximal socket silhouette | continuous edge topology + closed silhouette across 0%-100% curl |
| Pinky collar/jagged seam | separate finger island and proximal deformation | stable silhouette in neutral, halfway and close-fist frames |
| Finger bones rotate but pixels barely move | incorrect original bone weight targets / small animation envelope | BakeMesh movement of individual finger vertices + same-camera filmstrip |
| Arm feels like floating stick | all motion concentrated in arm root, insufficient key pose articulation | shoulder/forearm/wrist/fingers staged arrival with no camera drift |
| Skin looks synthetic | material settings + texture/normal detail | same-light A/B shots, watch OLED legibility retained |

## Sequence and acceptance
**Stage 1: inspect geometry before editing.** Save topology audit, mesh section diagrams,
joint axes, UV/seam maps, baseline in-game frame and full 5-finger deform reference.

**Stage 2: index and little anatomy.** On a safe working mesh, join each proximal
ring to the palm with anatomically aligned edge loops. Refine webbing and knuckles.
UV preserve/transfer, skin-group normalization and corrective shapes. Original
FBX remains a fallback until acceptance.

**Stage 3: full rig and pose authoring.** Build reproducible poses:
open/soft relaxed/half curl/fist/fist release/spread/gather/index flex/thumb
opposition/sleep drowsy. Thumb uses independent CMC/MCP/IP orientation.
Blend poses with hand entry/settle/idle/confirm/exit. Add only bounded
procedural movement after the authored clips.

**Stage 4: game integration.** Connect the accepted mesh/rig and clips to
existing sleep cinematic. Retire the inferior active source only after verifying
parity: live OLED, physical crown/button, input, E/Esc, repeated sleep cycles.

**Stage 5: proof.** Capture before/after from same approved game POV.
Show index and pinky close-ups in relaxed/25/50/75/100% bent states, full
fist open→close→open with no clipping, and actual game hand raise/wrist idle/exit.
Record actual CPU/GPU frame times, regression tests and player-visible video.
Only mark DONE when all of these pass, not merely `6970 vertices` or compile.

## Release guard
No unverified procedural welding, arbitrary nearest-neighbour bridges, source
asset overwrite, unlicensed FBX, extra executable/review clone, or automatic
production merge. Record actual Blender/Unity measurements, failed checks and
commit IDs rather than fabricating completion.
