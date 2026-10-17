#!/usr/bin/env python3
"""Blender-only original-finger/palm socket matching and safe full-rim weld QA.

Evidence-based first stage of genuine 5-finger surface reconstruction:
preserve 25-bone rig, original skin UV, all original PBR maps. Original Argon
FBX is read-only. Reuse only *existing* source socket loops. NEVER manufacture
fake wrist cylinders or sew a distant digit across intact palm surface.
Stop if spatially acceptable socket topology is not physically present.
"""
import bpy,bmesh,sys,json,pathlib,math,itertools
from collections import defaultdict

def fail(s):raise RuntimeError("ROKAS ANATOMY SOCKET SAFETY: "+s)
def components(bm):
  visited=set();result=[]
  for v in bm.verts:
    if v in visited:continue
    todo=[v];group=set()
    while todo:
      x=todo.pop()
      if x in visited:continue
      visited.add(x);group.add(x)
      todo.extend(y for e in x.link_edges for y in e.verts if y not in visited)
    result.append(group)
  return sorted(result,key=len,reverse=True)
def loops(part):
  neighbors=defaultdict(set)
  for v in part:
    for e in v.link_edges:
      if e.is_boundary and all(x in part for x in e.verts):
        a,b=e.verts;neighbors[a].add(b);neighbors[b].add(a)
  if any(len(x)!=2 for x in neighbors.values()):fail("Branched source boundary")
  rings=[];remaining=set(neighbors)
  while remaining:
    start=min(remaining,key=lambda v:v.index)
    ring=[];v=start;prev=None
    for _ in range(len(neighbors)+1):
      if v not in remaining:fail("Boundary loop repeated")
      ring.append(v);remaining.remove(v)
      nextv=next(w for w in neighbors[v] if w!=prev)
      if nextv==start:break
      prev,v=v,nextv
    else:fail("Unclosed source ring")
    rings.append(ring)
  return sorted(rings,key=len,reverse=True)
def optimize(a,b):
  if len(a)!=len(b):return None
  n=len(a);best=None
  for flipped in [False,True]:
    rhs=b[::-1] if flipped else b
    for shift in range(n):
      paired=[rhs[(i+shift)%n] for i in range(n)]
      lens=[(a[i].co-paired[i].co).length for i in range(n)]
      measure=(sum(x*x for x in lens)/n, max(lens),flipped,shift)
      if best is None or measure<best[0]:
        best=(measure,paired)
  return best
def main():
  args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
  if len(args)!=2:fail("Expected original FBX and QA output dir")
  src=pathlib.Path(args[0]);out=pathlib.Path(args[1]);out.mkdir(parents=True,exist_ok=True)
  bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
  bpy.ops.import_scene.fbx(filepath=str(src),use_anim=False)
  sk=[o for o in bpy.data.objects if o.type=='MESH' and 'Keiko_Hand_Skin_Mesh' in o.name]
  if len(sk)!=1:fail("Authentic hand skin must be unique")
  orig=sk[0];arm=orig.find_armature()
  if arm is None or len(arm.data.bones)!=25:fail("25 original rig bones required")
  clone=orig.copy();clone.data=orig.data.copy()
  clone.name="ROKAS_AuthenticOriginalSocketMatch_Candidate_QA_ONLY"
  bpy.context.collection.objects.link(clone);clone.matrix_world=orig.matrix_world.copy()
  for mod in list(clone.modifiers):
    if mod.type=='ARMATURE':clone.modifiers.remove(mod)
  bm=bmesh.new();bm.from_mesh(clone.data)
  start_edges=sum(e.is_boundary for e in bm.edges)
  start_area=sum(f.calc_area() for f in bm.faces)
  size=max(max(v.co[k] for v in bm.verts)-min(v.co[k] for v in bm.verts)
           for k in range(3))
  if start_edges!=1028 or len(bm.verts)!=12240:fail("Source mesh changed")
  # Source has identical source UV split lips and a 31-vertex collapsed-tip
  # pole. The original authored positions must not shift at all.
  # Blender FBX precision leaves one socket vertex ~1e-7 handspan
  # from its authored identical neighbor. 1e-6 of source span remains
  # a sub-micrometre merge (0.00000041 in Blender coordinates).
  bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=size*1e-6)
  groups=components(bm)
  if len(groups)!=6:fail("Exact source-pair weld fused anatomically distinct digits")
  palm=loops(groups[0]);fingers=[loops(g) for g in groups[1:]]
  rim_sizes=[len(r) for r in palm]
  report={"original_bones":25,"original_open_edges":start_edges,
          "post_exact_position_source_vertices":len(bm.verts),
          "post_exact_position_boundary_edges":sum(e.is_boundary for e in bm.edges),
          "palm_socket_loop_sizes":rim_sizes,
          "finger_root_loop_sizes":[[len(r) for r in f] for f in fingers],
          "matched_existing_sockets":[],"unmatched_fingers":[],
          "game_integration":False}
  print("SOURCE ACTUAL SOCKET RINGS",report,flush=True)
  if len(palm)!=3 or rim_sizes!=[64,30,30] or any(len(fs)!=1 or len(fs[0])!=30 for fs in fingers):
    fail("Source sockets have unanticipated topology; no blindly stitched patches")
  # Sort exact source socket assignments by GLOBAL minimum 30-vertex matching
  # error. Single socket cannot be assigned to two fingers.
  ranked=[]
  for j,socket in enumerate(palm[1:]):
    for k,finger in enumerate(fingers):
      answer=optimize(finger[0],socket)
      if answer:
        fit,paired=answer
        ranked.append((fit[0],fit[1],j,k,paired,fit[2],fit[3]))
  selected=[];used_socket=set();used_finger=set()
  for cost,maxgap,j,k,paired,flipped,shift in sorted(ranked):
    if j in used_socket or k in used_finger:continue
    if maxgap>size*.05:continue
    used_socket.add(j);used_finger.add(k)
    selected.append((cost,maxgap,j,k,paired,flipped,shift))
  # Report measurements BEFORE any polygon surgery.
  report["matching_candidates"]=sorted([
    {"socket":j,"finger":k,"mean_sq":round(float(cost),9),
     "maxgap":round(float(maxgap),7)}
    for cost,maxgap,j,k,_,_,_ in ranked],key=lambda a:a["mean_sq"])
  report["matched_existing_sockets"]=[{
    "socket":j+1,"finger_component":k+1,"maximum_joint_gap":maxgap,
    "rms_gap":math.sqrt(cost),"source_ring_verts":30,
    "reverse_socket_direction":flipped,"ring_rotation":shift
  } for cost,maxgap,j,k,_,flipped,shift in selected]
  report["unmatched_fingers"]=[k+1 for k in range(5) if k not in used_finger]
  # Two existing 30-point palm openings can be bridged with true original
  # finger ring vertices, but three missing sockets require NEW palm cuts.
  # Preserve the measured mismatch in QA even on deliberate rejection.
  # These two original palm openings are 24-26mm beyond the authentic
  # finger-base cross sections in a metre-scale source model; pretending
  # that they directly join creates long webbing and non-anatomical collars.
  report["maximum_allowed_original_ring_gap"]=size*.05
  report["requires_distal_palm_socket_reconstruction"]=len(selected)<2
  (out/"real_source_socket_fit_report.json").write_text(
    json.dumps(report,indent=2),encoding="utf8")
  if len(selected)!=2:
    fail("Existing original palm openings are displaced from genuine finger "+
         "roots; do NOT stretch digits to meet them. Open new palm sockets "+
         "by actual surface surgery; see report for exact fit distances.")
  seams=[]
  for cost,maxgap,j,k,socket_aligned,flipped,shift in selected:
    finger=fingers[k][0]
    # Each source border edge is consumed exactly once; keep ALL true
    # skinned bone and 2048 PBR UV source arrays. UV on new faces is copied
    # from neighboring original face corners, not synthetic checker skin.
    for i in range(30):
      a=finger[i];b=finger[(i+1)%30]
      c=socket_aligned[(i+1)%30];d=socket_aligned[i]
      if len({a,b,c,d})!=4:fail("Source socket vertex overlap")
      face=bm.faces.new((a,b,c,d));face.smooth=True
      seams.append(face)
  uv=bm.loops.layers.uv.active
  if uv is not None:
    for face in seams:
      for corner in face.loops:
        v=corner.vert
        older=next((other for f in v.link_faces if f not in seams
                    for other in f.loops if other.vert==v),None)
        if older is None:fail("Source-based seam UV provenance missing")
        corner[uv].uv=older[uv].uv.copy()
  bm.normal_update()
  report["after_bridge_boundary_edges"]=sum(e.is_boundary for e in bm.edges)
  report["after_bridge_islands"]=len(components(bm))
  report["after_bridge_nonmanifold_over2"]=sum(len(e.link_faces)>2 for e in bm.edges)
  report["after_bridge_added_quads"]=len(seams)
  report["after_bridge_source_area_change"]=(
    sum(f.calc_area() for f in bm.faces)-start_area)/start_area
  (out/"real_source_socket_fit_report.json").write_text(
    json.dumps(report,indent=2),encoding="utf8")
  print("ROKAS TWO ACTUAL SOURCE SOCKETS",json.dumps(report),flush=True)
  if report["after_bridge_islands"]!=4:
    fail("Expected two bridged fingers + three loose fingers + palm = 4 islands")
  if report["after_bridge_nonmanifold_over2"]!=0:
    fail("Real original ring joining creates high degree geometry")
  if report["after_bridge_boundary_edges"]>=report["post_exact_position_boundary_edges"]-115:
    fail("Two full original ring seams not closed")
  if report["after_bridge_source_area_change"]>.01:
    fail("Bridge adds too much surface or folds onto the palm")
  # Keep candidate private and main BedSleep V10 unchanged.
  bm.to_mesh(clone.data);bm.free();clone.data.update()
  skin=clone.modifiers.new("ROKAS_Original_25_Bone_Rig","ARMATURE");skin.object=arm
  skin.use_vertex_groups=True;skin.use_bone_envelopes=False
  bpy.ops.object.select_all(action='DESELECT')
  clone.select_set(True);arm.select_set(True)
  bpy.context.view_layer.objects.active=clone
  bpy.ops.export_scene.fbx(filepath=str(out/"ROKAS_TwoExistingFingerSockets_QA_ONLY.fbx"),
      use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,
      use_mesh_modifiers=False,bake_anim=False)
  print("NOT FINAL: 3 other digits still need real palm socket cutout before V10")
if __name__=='__main__':main()
