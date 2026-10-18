using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rokas.BedSleep.V10Release
{
    /// <summary>
    /// Production Bed Sleep hand-skin integration. The connected Argon mesh
    /// is shipped to this Unity checkout as an authenticated CI asset under
    /// Resources, not exposed as an unlicensed raw asset in the public repo.
    /// No new scene, editor button, proxy material or dummy geometry.
    /// Original game rig drives animation/watch/OLED; Argon's genuine
    /// 25-bone rig drives its own mesh with semantic joint retargeting.
    /// </summary>
    internal static class RokasArgonConnectedHandRuntime
    {
        internal const string Resource =
            "BedSleep/ArgonConnectedHand/ROKAS_Argon_ConnectedHand_25Bone";

        internal static bool TryInstall(KeikoFPSArmRigController arm)
        {
            if (arm == null) return false;
            GameObject source = Resources.Load<GameObject>(Resource);
            if (source == null)
            {
                Debug.LogWarning("[ROKAS BedSleep] Corrected connected hand is not " +
                    "installed. Keeping original playable V10 arm and watch.");
                return false;
            }
            var current = arm.SkinnedRenderer;
            if (current == null || current.sharedMesh == null ||
                current.sharedMesh.subMeshCount != 3 ||
                current.bones == null || current.bones.Length != 25 ||
                current.sharedMaterials.Length != 3 || current.sharedMaterials[0] == null)
            {
                Debug.LogWarning("[ROKAS BedSleep] Incompatible V10 original rig; " +
                    "keep old playable arm.");
                return false;
            }

            GameObject rig = null;
            Mesh oldHandCopy = null;
            try
            {
                rig = UnityEngine.Object.Instantiate(source, arm.transform.parent);
                rig.name = "ROKAS_Argon_25Bone_Runtime_Integrated";
                var candidate = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(x => x.sharedMesh != null)
                    .OrderByDescending(x => x.sharedMesh.vertexCount).FirstOrDefault();
                if (candidate == null || candidate.bones == null ||
                    candidate.bones.Length != 25 ||
                    candidate.sharedMesh.bindposes.Length != 25)
                    throw new InvalidOperationException("Authentic Argon 25-bone FBX is missing");

                var originalBones = new Dictionary<string,Transform>(StringComparer.Ordinal);
                foreach (var bone in current.bones)
                {
                    if (bone == null || originalBones.ContainsKey(bone.name))
                        throw new InvalidOperationException("Missing or repeated game bone");
                    originalBones.Add(bone.name, bone);
                }
                var importedBones = rig.GetComponentsInChildren<Transform>(true)
                    .GroupBy(x => x.name).ToDictionary(
                        g => g.Key,g => g.First(),StringComparer.Ordinal);
                if (!importedBones.TryGetValue("Arm_Wrist",out var connectedWrist) ||
                    !originalBones.TryGetValue("Wrist_L",out var gameWrist))
                    throw new InvalidOperationException("Both real wrist anchors required");

                string[][] sourceDigits =
                {
                    new [] {"Thumb_CMC","Thumb_MCP","Thumb_IP"},
                    new [] {"Index_MCP","Index_PIP","Index_DIP"},
                    new [] {"Middle_MCP","Middle_PIP","Middle_DIP"},
                    new [] {"Ring_MCP","Ring_PIP","Ring_DIP"},
                    new [] {"Little_MCP","Little_PIP","Little_DIP"}
                };
                string[] gameDigits={"Thumb","Index","Middle","Ring","Pinky"};
                var src = new Transform[15];
                var dst = new Transform[15];
                for (int digit=0;digit<5;digit++)
                for (int joint=0;joint<3;joint++)
                {
                    int idx=digit*3+joint;
                    var from=sourceDigits[digit][joint];
                    var to=gameDigits[digit]+"_0"+(joint+1)+"_L";
                    if (!importedBones.TryGetValue(from,out src[idx]) ||
                        !originalBones.TryGetValue(to,out dst[idx]))
                        throw new InvalidOperationException(
                            "Missing authentic MCP/PIP/DIP bone correspondence "+from+" -> "+to);
                }

                float oldSpan=current.bounds.size.magnitude;
                float newSpan=candidate.bounds.size.magnitude;
                float ratio=newSpan/Mathf.Max(oldSpan,1e-5f);
                if (oldSpan<1e-5f || ratio<.67f || ratio>1.65f)
                    throw new InvalidOperationException(
                        "Source and game world arm bounds mismatch, ratio="+ratio.ToString("G7"));

                // Render only connected original source skin. Keep original
                // game watch, seven imported case/strap/OLED meshes, sleeve
                // and nails. Preserve real source materials and texture UVs.
                foreach (var renderer in rig.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled=(renderer==candidate);
                foreach (var t in rig.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer=current.gameObject.layer;
                candidate.gameObject.name="ROKAS_ConnectedHand_GameSkin";
                candidate.sharedMaterial=current.sharedMaterials[0];
                candidate.updateWhenOffscreen=true;
                var follow=rig.AddComponent<RokasArgonRuntimeHandFollower>();
                follow.Configure(rig.transform,connectedWrist,gameWrist,src,dst);
                follow.SyncNow();

                // NEVER change source V10 assets: original renderer receives
                // an in-memory mesh clone with skin (submesh0) suppressed;
                // sleeve and nails remain original and animated. If any
                // invariant fails before here, keep original untouched.
                oldHandCopy=UnityEngine.Object.Instantiate(current.sharedMesh);
                oldHandCopy.name="ROKAS_V10_SleeveNails_RuntimeOnly";
                oldHandCopy.SetTriangles(Array.Empty<int>(),0,false);
                if (oldHandCopy.GetTriangles(1).Length == 0 ||
                    oldHandCopy.GetTriangles(2).Length == 0)
                    throw new InvalidOperationException("V10 sleeve/nails were lost");
                current.sharedMesh=oldHandCopy;
                Debug.Log("[ROKAS Bed Sleep] Actual game uses connected Argon " +
                    "hand. Real watch, OLED, sleeve, nails, camera and bed logic remain.");
                return true;
            }
            catch(Exception e)
            {
                Debug.LogWarning("[ROKAS Bed Sleep] Connected hand rejected; " +
                    "original V10 untouched. "+e.Message);
                if (rig!=null) UnityEngine.Object.Destroy(rig);
                if (oldHandCopy!=null) UnityEngine.Object.Destroy(oldHandCopy);
                return false;
            }
        }
    }

    [DefaultExecutionOrder(32000)]
    internal sealed class RokasArgonRuntimeHandFollower : MonoBehaviour
    {
        private Transform root,sourceWrist,targetWrist;
        private Transform[] sourceJoints,targetJoints;
        private Quaternion[] sourceRest,targetRest;
        private bool ready;

        internal void Configure(Transform rootRig,Transform source,Transform target,
            Transform[] sourceFingers,Transform[] gameFingers)
        {
            if (rootRig==null || source==null || target==null ||
                sourceFingers==null || gameFingers==null ||
                sourceFingers.Length!=15 || gameFingers.Length!=15)
                throw new InvalidOperationException("Real source/game finger chains missing");
            root=rootRig; sourceWrist=source; targetWrist=target;
            sourceJoints=sourceFingers;targetJoints=gameFingers;
            sourceRest=sourceFingers.Select(x=>x.localRotation).ToArray();
            targetRest=gameFingers.Select(x=>x.localRotation).ToArray();
            ready=true;
        }
        internal void SyncNow()
        {
            if (!ready) return;
            // The native Argon armature remains the skin owner: never
            // swap V10 bone objects/bindposes into an unrelated FBX.
            Quaternion correction=targetWrist.rotation*
                Quaternion.Inverse(sourceWrist.rotation);
            root.rotation=correction*root.rotation;
            root.position+=targetWrist.position-sourceWrist.position;
            for (int i=0;i<15;i++)
                sourceJoints[i].localRotation=sourceRest[i]*
                    (Quaternion.Inverse(targetRest[i])*targetJoints[i].localRotation);
        }
        private void LateUpdate() { SyncNow(); }
    }
}
