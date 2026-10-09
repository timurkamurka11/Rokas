#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rokas.Editor
{
    // Research only: no importer, library, prefab, rig, clip or scene is modified.
    public static class CombatHeavySkinningGeometryAudit
    {
        public const string DefaultOutput = "D:/DD2-Research/Reports/CombatFidelity3-2026-10-08/HeavySkinning";
        private const float DepthLimit = .001f;
        private const float SurfaceClearance = .0015f;
        private const int TrainingOrder = 4, ValidationOrder = 6;
        private static Stopwatch auditTimer;
        private static float auditBudget;
        private static string auditOutput;

        [Serializable] public sealed class Measurement
        {
            public string action, phase, renderer, meshPath, weapon, bakeConvention;
            public float clock, maximumDepth, maximumAmbiguousDepth, bakeSkinError;
            public Vector3 weaponWorldScale;
            public int triangleCount, samples, insideSamples, ambiguousSamples, deepestTriangle = -1;
            public int weaponBoundaryEdges, weaponNonManifoldEdges;
            public Vector3 deepestPoint, deepestSurface;
            public string[] deepestVertexWeights;
        }
        [Serializable] public sealed class Patch
        {
            public int vertex;
            public Vector3 original, candidate;
            public string weights;
        }
        [Serializable] public sealed class Evidence
        {
            public string status, method, limitation, modelAsset, swordAsset, originalMesh;
            public string modelShaBefore, modelShaAfter, swordShaBefore, swordShaAfter;
            public string[] nativeAssetHashesBefore, nativeAssetHashesAfter, trainingFrames;
            public int meshVertices, meshTriangles, selectedHandTriangles, changedVertices, invertedBindTriangles;
            public float maximumHandEdgeStretch;
            public float depthLimit = DepthLimit, maximumBindDisplacement, maximumBakeSkinError, elapsedSeconds;
            public bool immutableAssetsExact, originalTopologyAndWeightsExact, outsideMaskVerticesExact, candidateAccepted;
            public Measurement[] originalRows, candidateRows;
            public Patch[] patchVertices;
        }
        [Serializable] public sealed class BakeConventionFailure
        {
            public string action, phase, currentPose, model, renderer, mesh, quality;
            public float clock;
            public int vertexCount, boneCount, bindPoseCount, bakedFalseCount, bakedTrueCount;
            public Matrix4x4 rendererWorld, modelWorld;
            public Vector3 rendererScale, modelScale;
            public float falseTransformPointError, falseRotationOnlyError, trueTransformPointError, trueRotationOnlyError, falseRawWorldError, trueRawWorldError;
            public BoneDiagnostic[] bones;
            public VertexDiagnostic[] worstVertices;
        }
        [Serializable] public sealed class BoneDiagnostic
        {
            public int index;
            public string name, parent;
            public Matrix4x4 world, bindpose, skin;
            public Vector3 localPosition, localScale, worldScale;
            public Quaternion localRotation;
        }
        [Serializable] public sealed class VertexDiagnostic
        {
            public int index, allWeightCount;
            public string fourWeights, allWeights;
            public float fourWeightSum, mismatch;
            public Vector3 bindVertex, lbsWorld, bakeFalseRaw, bakeTrueRaw, bakeFalseTransformPoint, bakeTrueTransformPoint, bakeFalseRotationOnly, bakeTrueRotationOnly;
        }
        private sealed class Frame
        {
            public string action, phase, weapon, bakeConvention;
            public float clock;
            public Vector3 weaponWorldScale;
            public Matrix4x4[] bones;
            public Matrix4x4 weaponToWorld, worldToWeapon;
            public Surface sword;
            public Vector3[] bakedWorld;
            public float bakeError;
        }
        private sealed class Session : IDisposable
        {
            public GameObject root;
            public SkinnedMeshRenderer renderer;
            public Mesh original;
            public Vector3[] vertices;
            public BoneWeight[] weights;
            public Matrix4x4[] bindposes;
            public int[] triangles, handTriangles, handVertexIndices;
            public bool[] handMask;
            public Transform[] bones;
            public string[] boneNames;
            public Vector3[] sampledVertices;
            public Dictionary<string, Surface> surfaces = new Dictionary<string, Surface>();
            public List<Frame> frames = new List<Frame>();
            public void Dispose() { if (root != null) Object.DestroyImmediate(root); }
        }

        // Parent can invoke this through a focused EditMode test. A candidate is
        // transient unless the separately called SaveVerifiedDerivative succeeds.
        public static string RunAudit(bool buildCandidate, string outputDirectory)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run this audit outside Play Mode.");
            string output = string.IsNullOrEmpty(outputDirectory) ? DefaultOutput : outputDirectory;
            Directory.CreateDirectory(output);
            auditTimer = Stopwatch.StartNew(); auditBudget = buildCandidate ? 180f : 90f; auditOutput = output;
            using (Session session = Collect())
            {
                var library = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary");
                string model = AssetDatabase.GetAssetPath(library.keiko.model);
                string sword = AssetDatabase.GetAssetPath(library.keiko.weaponPrefab.GetComponentInChildren<MeshFilter>(true).sharedMesh);
                var e = new Evidence {
                    status = "ORIGINAL_MEASURED_CANDIDATE_NOT_REQUESTED",
                    method = "Actual runtime actor FK; bone-weight hand mask; actual indexed hand triangles; original owned weapon closed triangle surface; nearest-surface BVH and unanimous three-direction ray parity; BakeMesh independently checked against LBS. Candidate changes bind-space vertices only, never weights, bones, sword or clips. Training lattice order 4; withheld validation lattice order 6.",
                    limitation = "Discrete actual runtime keyframes and triangle interior samples, not a continuous collision proof or a visual natural-grip approval. Ambiguous inside votes block derivative saving. Normal, Heavy, Throw, Guard and both confirmed sword idles are included. No resource binding is performed.",
                    modelAsset = model, swordAsset = sword, originalMesh = AssetDatabase.GetAssetPath(session.original) + ":" + session.original.name,
                    modelShaBefore = HashFile(model), swordShaBefore = HashFile(sword),
                    nativeAssetHashesBefore = NativeHashes(library.keiko),
                    meshVertices = session.vertices.Length, meshTriangles = session.triangles.Length / 3,
                    selectedHandTriangles = session.handTriangles.Length / 3,
                    originalTopologyAndWeightsExact = true, outsideMaskVerticesExact = true
                };
                e.originalRows = MeasureAll(session, session.vertices, ValidationOrder);
                Vector3[] candidate = (Vector3[])session.vertices.Clone();
                if (buildCandidate)
                {
                    Frame[] training = SelectTrainingFrames(session, e.originalRows);
                    e.trainingFrames = training.Select(f => f.action + ":" + f.phase + ":" + f.clock.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    SolveBoundedCandidate(session, candidate, e.originalRows.Max(r => r.maximumDepth), training);
                    using (Session candidateSession = Collect(candidate))
                        e.candidateRows = MeasureAll(candidateSession, candidate, ValidationOrder);
                    ValidatePatchShape(session, candidate, e);
                    var patch = new List<Patch>();
                    for (int i = 0; i < candidate.Length; i++)
                    {
                        float d = Vector3.Distance(candidate[i], session.vertices[i]);
                        e.maximumBindDisplacement = Mathf.Max(e.maximumBindDisplacement, d);
                        if (d <= 1e-9f) continue;
                        if (!session.handMask[i]) e.outsideMaskVerticesExact = false;
                        patch.Add(new Patch { vertex = i, original = session.vertices[i], candidate = candidate[i], weights = Describe(session.weights[i], session.boneNames) });
                    }
                    e.changedVertices = patch.Count;
                    e.patchVertices = patch.ToArray();
                    e.candidateAccepted = patch.Count > 0 && e.outsideMaskVerticesExact && e.invertedBindTriangles == 0 && e.maximumHandEdgeStretch <= 2f &&
                        e.candidateRows.All(r => r.maximumDepth <= DepthLimit && r.ambiguousSamples == 0 && r.bakeSkinError <= .00005f && r.weaponBoundaryEdges == 0 && r.weaponNonManifoldEdges == 0);
                    e.status = e.candidateAccepted ? "BOUNDED_CANDIDATE_MEASURED_PASS_NOT_BOUND" : "BOUNDED_CANDIDATE_BLOCKED";
                }
                e.maximumBakeSkinError = e.originalRows.Max(r => r.bakeSkinError);
                e.elapsedSeconds = (float)auditTimer.Elapsed.TotalSeconds;
                e.modelShaAfter = HashFile(model); e.swordShaAfter = HashFile(sword);
                e.nativeAssetHashesAfter = NativeHashes(library.keiko);
                e.immutableAssetsExact = e.modelShaBefore == e.modelShaAfter && e.swordShaBefore == e.swordShaAfter &&
                    e.nativeAssetHashesBefore.SequenceEqual(e.nativeAssetHashesAfter);
                if (!e.immutableAssetsExact) throw new InvalidOperationException("Immutable source assets changed during audit.");
                string json = JsonUtility.ToJson(e, true);
                File.WriteAllText(Path.Combine(output, buildCandidate ? "HeavySkinningCandidateEvidence.json" : "HeavySkinningGeometryEvidence.json"), json);
                return json;
            }
        }

        // Explicit second step only. The original FBX and resource library are
        // untouched. The caller must review visual natural-grip quality separately.
        public static string SaveVerifiedDerivative(string evidencePath, string newAssetPath)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Cannot create a derivative in Play Mode.");
            Evidence e = JsonUtility.FromJson<Evidence>(File.ReadAllText(evidencePath));
            if (!e.candidateAccepted || !e.immutableAssetsExact || !e.outsideMaskVerticesExact ||
                e.patchVertices == null || e.patchVertices.Length == 0 || e.candidateRows == null ||
                e.candidateRows.Any(r => r.maximumDepth > DepthLimit || r.ambiguousSamples != 0 || r.bakeSkinError > .00005f || r.weaponBoundaryEdges != 0 || r.weaponNonManifoldEdges != 0))
                throw new InvalidOperationException("A verified bounded candidate is required.");
            if (!newAssetPath.StartsWith("Assets/Rokas/Art/CombatActors/Keiko/", StringComparison.Ordinal) ||
                !newAssetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), newAssetPath)))
                throw new ArgumentException("Use a new dedicated Keiko derivative .asset path; overwrite is prohibited.");
            auditTimer = Stopwatch.StartNew(); auditBudget = 90f; auditOutput = Path.GetDirectoryName(evidencePath);
            using (Session session = Collect())
            {
                if (HashFile(e.modelAsset) != e.modelShaAfter || HashFile(e.swordAsset) != e.swordShaAfter)
                    throw new InvalidOperationException("Original model or sword changed since measurement.");
                var library = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary");
                if (!NativeHashes(library.keiko).SequenceEqual(e.nativeAssetHashesAfter))
                    throw new InvalidOperationException("Native clips changed since measurement.");
                Vector3[] vertices = (Vector3[])session.vertices.Clone();
                foreach (Patch p in e.patchVertices)
                {
                    if (p.vertex < 0 || p.vertex >= vertices.Length || !session.handMask[p.vertex] || vertices[p.vertex] != p.original)
                        throw new InvalidOperationException("Candidate no longer matches the original hand mask.");
                    vertices[p.vertex] = p.candidate;
                }
                Measurement[] verified;
                using (Session actual = Collect(vertices)) verified = MeasureAll(actual, vertices, ValidationOrder + 1);
                if (verified.Any(r => r.maximumDepth > DepthLimit || r.ambiguousSamples != 0 || r.bakeSkinError > .00005f || r.weaponBoundaryEdges != 0 || r.weaponNonManifoldEdges != 0))
                    throw new InvalidOperationException("The denser independent save gate failed; no asset was created.");
                Mesh derivative = Object.Instantiate(session.original);
                derivative.name = Path.GetFileNameWithoutExtension(newAssetPath);
                derivative.vertices = vertices;
                Vector3[] preservedNormals = session.original.normals;
                derivative.RecalculateNormals();
                Vector3[] normals = derivative.normals;
                for (int i = 0; i < vertices.Length; i++) if (vertices[i] == session.vertices[i]) normals[i] = preservedNormals[i];
                derivative.normals = normals; derivative.RecalculateBounds();
                AssetDatabase.CreateAsset(derivative, newAssetPath);
                return newAssetPath;
            }
        }

        public static float[] EvaluateClosedSurface(Vector3[] vertices, int[] triangles, Vector3[] samples)
        {
            var surface = new Surface(vertices, triangles);
            return samples.Select(p => { bool ambiguous; Vector3 closest; float depth = surface.Depth(p, out closest, out ambiguous); return ambiguous || surface.BoundaryEdges != 0 || surface.NonManifoldEdges != 0 ? float.NaN : depth; }).ToArray();
        }

        private static Session Collect(Vector3[] candidate = null)
        {
            var s = new Session { root = new GameObject("HeavySkinningResearchOnly"), sampledVertices = candidate };
            try
            {
                CollectAction(s, "Heavy"); CollectAction(s, "Normal"); CollectAction(s, "Throw");
                CollectAction(s, "Guard"); CollectAction(s, "NormalIdle"); CollectAction(s, "HeavyIdle");
                if (s.frames.Count < 20 || s.handTriangles.Length == 0) throw new InvalidOperationException("No real hand geometry/keyframes were collected.");
                return s;
            }
            catch { s.Dispose(); throw; }
        }
        private static void CollectAction(Session s, string action)
        {
            CheckBudget("collect " + action);
            var actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Keiko, s.root.transform);
            actor.enabled = false;
            Mesh transientMesh = null;
            try
            {
                Tick(actor, .12f);
                if (action == "Heavy" || action == "HeavyIdle" || action == "Guard") actor.SelectHeavyIdleForGeometryAudit();
                actor.PlayIdle(); Tick(actor, .12f);
                SkinnedMeshRenderer renderer = actor.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(r => r.sharedMesh != null && r.bones.Any(b => b != null && b.name.EndsWith("RightHandIndex1", StringComparison.Ordinal)))
                    .OrderByDescending(r => r.sharedMesh.vertexCount).First();
                if (s.original == null) InitializeMesh(s, renderer);
                if (renderer.sharedMesh != s.original) throw new InvalidOperationException("Action resolved a different skin mesh.");
                if (s.sampledVertices != null)
                {
                    transientMesh = Object.Instantiate(s.original);
                    transientMesh.vertices = s.sampledVertices; renderer.sharedMesh = transientMesh;
                }
                if (action.EndsWith("Idle", StringComparison.Ordinal))
                {
                    float idleElapsed = 0;
                    foreach (float t in new[] { 0f, .2f, .6f }) { Tick(actor, t - idleElapsed); idleElapsed = t; Capture(s, actor, renderer, action, "idle", actor.CurrentPoseSeconds); }
                    return;
                }
                if (action == "Guard")
                {
                    actor.PlayGuard(); float guardElapsed = 0;
                    foreach (float t in new[] { 0f, .1f, .25f, .5f, .85f, 1.2f })
                    {
                        Tick(actor, t - guardElapsed); guardElapsed = t; Capture(s, actor, renderer, action, "guard-or-exit", actor.CurrentPoseSeconds);
                    }
                    return;
                }
                if (action == "Heavy") actor.PlayPreparation(true);
                else if (action == "Normal") actor.PlayPreparation(false);
                else actor.PlayThrowPreparation();
                LicensedCombatMotionProfile profile = actor.ActiveLicensedProfile;
                if (profile == null) throw new InvalidOperationException(action + " did not use its actual native profile.");
                float[] anticipation = action == "Heavy" ? new[] { 0f, .15f, 2.5f, 4.5f } : new[] { 0f, .15f, .4f };
                float elapsed = 0;
                foreach (float t in anticipation)
                {
                    Tick(actor, t - elapsed); elapsed = t;
                    Capture(s, actor, renderer, action, "anticipation", actor.LicensedMotionClock);
                }
                if (action == "Throw") actor.ReleaseThrowWeapon();
                actor.BindLicensedContact("geometry-" + action);
                if (!actor.ConfirmLicensedContact("geometry-" + action)) throw new InvalidOperationException("Native contact was not authorized.");
                var times = new SortedSet<float> { 0f, 1f / 60f, .05f, .2f, .35f,
                    Mathf.Max(0f, profile.StageRecoveryTime - 1f / 60f), profile.StageRecoveryTime,
                    Mathf.Min(profile.Duration - .0001f, profile.StageRecoveryTime + 1f / 60f),
                    Mathf.Max(0f, profile.Duration - .05f), Mathf.Max(0f, profile.Duration - 1f / 120f) };
                foreach (LicensedMotionSegment segment in profile.segments)
                {
                    times.Add(segment.start); times.Add(Mathf.Min(profile.Duration - .0001f, segment.start + segment.duration * .5f));
                }
                elapsed = 0;
                foreach (float t in times.Where(t => t >= 0 && t < profile.Duration))
                {
                    Tick(actor, t - elapsed); elapsed = t;
                    Capture(s, actor, renderer, action, "contact-and-full-recovery", actor.LicensedMotionClock);
                }
                foreach (float t in new[] { profile.Duration + .02f, profile.Duration + .04f, profile.Duration + .08f, profile.Duration + .15f })
                {
                    Tick(actor, t - elapsed); elapsed = t;
                    Capture(s, actor, renderer, action, "owned-idle-handoff", t);
                }
            }
            finally { Object.DestroyImmediate(actor.gameObject); if (transientMesh != null) Object.DestroyImmediate(transientMesh); }
        }
        private static void InitializeMesh(Session s, SkinnedMeshRenderer renderer)
        {
            s.original = renderer.sharedMesh; s.vertices = s.original.vertices;
            s.weights = s.original.boneWeights; s.bindposes = s.original.bindposes; s.bones = renderer.bones;
            s.boneNames = s.bones.Select(b => b == null ? "MISSING" : b.name).ToArray();
            s.triangles = s.original.triangles; s.handMask = new bool[s.vertices.Length];
            if (s.weights.Length != s.vertices.Length) throw new InvalidOperationException("Expected explicit four-weight own skin data.");
            for (int i = 0; i < s.weights.Length; i++) s.handMask[i] = HandWeight(s.weights[i], s.bones) > .05f;
            var selected = new List<int>();
            for (int i = 0; i < s.triangles.Length; i += 3)
                if (s.handMask[s.triangles[i]] || s.handMask[s.triangles[i + 1]] || s.handMask[s.triangles[i + 2]])
                { selected.Add(s.triangles[i]); selected.Add(s.triangles[i + 1]); selected.Add(s.triangles[i + 2]); }
            s.handTriangles = selected.ToArray();
            s.handVertexIndices = s.handTriangles.Distinct().ToArray();
        }
        private static Surface CachedSurface(Session s, Transform weapon, MeshFilter[] filters, List<Vector3> vertices, List<int> triangles)
        {
            string key = string.Join(";", filters.Select(f => f.sharedMesh.GetInstanceID().ToString() + ":" + RelativeMatrix(f.transform, weapon).ToString("R")));
            Surface surface;
            if (!s.surfaces.TryGetValue(key, out surface))
            { surface = new Surface(vertices.ToArray(), triangles.ToArray()); s.surfaces.Add(key, surface); }
            return surface;
        }
        private static Matrix4x4 RelativeMatrix(Transform child, Transform parent)
        {
            Matrix4x4 m = Matrix4x4.identity;
            for (Transform t = child; t != parent && t != null; t = t.parent)
                m = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * m;
            return m;
        }
        private static void ValidatePatchShape(Session s, Vector3[] candidate, Evidence e)
        {
            for (int t = 0; t < s.handTriangles.Length; t += 3)
            {
                int a = s.handTriangles[t], b = s.handTriangles[t + 1], c = s.handTriangles[t + 2];
                Vector3 before = Vector3.Cross(s.vertices[b] - s.vertices[a], s.vertices[c] - s.vertices[a]);
                Vector3 after = Vector3.Cross(candidate[b] - candidate[a], candidate[c] - candidate[a]);
                if (before.sqrMagnitude > 1e-16f && (after.sqrMagnitude < 1e-16f || Vector3.Dot(before, after) <= 0)) e.invertedBindTriangles++;
                int[] ids = { a, b, c };
                for (int edge = 0; edge < 3; edge++)
                {
                    int i = ids[edge], j = ids[(edge + 1) % 3]; float length = Vector3.Distance(s.vertices[i], s.vertices[j]);
                    if (length > 1e-7f) e.maximumHandEdgeStretch = Mathf.Max(e.maximumHandEdgeStretch, Vector3.Distance(candidate[i], candidate[j]) / length);
                }
            }
        }
        private static float HandWeight(BoneWeight w, Transform[] bones)
        {
            float sum = 0;
            for (int i = 0; i < 4; i++)
            {
                int index = BoneIndex(w, i); if (index < 0 || index >= bones.Length || bones[index] == null) continue;
                string n = bones[index].name;
                if (n.Contains("LeftHand") || n.Contains("RightHand")) sum += Weight(w, i);
            }
            return sum;
        }
        private static void Capture(Session s, ReactiveCombatActorVisual actor, SkinnedMeshRenderer renderer, string action, string phase, float clock)
        {
            Transform weapon = actor.HeldDagger != null && actor.HeldDagger.gameObject.activeSelf
                ? actor.HeldDagger : actor.WeaponAttachment.CurrentWeapon.transform;
            CheckBudget("capture " + action + " " + phase);
            Vector3 worldScale = new Vector3(weapon.TransformVector(Vector3.right).magnitude, weapon.TransformVector(Vector3.up).magnitude, weapon.TransformVector(Vector3.forward).magnitude);
            if (Mathf.Max(worldScale.x, Mathf.Max(worldScale.y, worldScale.z)) - Mathf.Min(worldScale.x, Mathf.Min(worldScale.y, worldScale.z)) > .00001f)
                throw new InvalidOperationException("Nonuniform weapon scale needs a world-space BVH, not a local-space distance assumption: " + worldScale);
            var filters = weapon.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            foreach (MeshFilter f in filters)
            {
                int offset = vertices.Count;
                Matrix4x4 m = weapon.worldToLocalMatrix * f.transform.localToWorldMatrix;
                vertices.AddRange(f.sharedMesh.vertices.Select(v => m.MultiplyPoint3x4(v)));
                triangles.AddRange(f.sharedMesh.triangles.Select(i => i + offset));
            }
            if (vertices.Count == 0) throw new InvalidOperationException("No actual owned weapon triangles.");
            var frame = new Frame { action = action, phase = phase + "|" + actor.CurrentPose, clock = clock, weapon = weapon.name, weaponWorldScale = worldScale,
                bones = renderer.bones.Select((b, i) => b.localToWorldMatrix * s.bindposes[i]).ToArray(),
                weaponToWorld = weapon.localToWorldMatrix, worldToWeapon = weapon.worldToLocalMatrix,
                sword = CachedSurface(s, weapon, filters, vertices, triangles) };
            // The actual runtime trace at scale1.800334 proved true+TransformPoint
            // (max error0.75µm). Keep false+rotation-only as a diagnostic, but
            // validate the selected convention against independent LBS every pose.
            var baked = new Mesh();
            try
            {
                Matrix4x4 noScale = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);
                renderer.BakeMesh(baked, false);
                Vector3[] falseRaw = baked.vertices;
                Vector3[] falseWorld = falseRaw.Select(v => noScale.MultiplyPoint3x4(v)).ToArray();
                renderer.BakeMesh(baked, true);
                Vector3[] trueRaw = baked.vertices;
                Vector3[] trueWorld = trueRaw.Select(renderer.transform.TransformPoint).ToArray();
                float errorFalse = 0, errorTrue = 0;
                foreach (int i in s.handVertexIndices)
                {
                    Vector3 expected = Skin(s.weights[i], frame.bones).MultiplyPoint3x4(s.sampledVertices == null ? s.vertices[i] : s.sampledVertices[i]);
                    errorFalse = Mathf.Max(errorFalse, Vector3.Distance(falseWorld[i], expected));
                    errorTrue = Mathf.Max(errorTrue, Vector3.Distance(trueWorld[i], expected));
                }
                frame.bakedWorld = trueWorld;
                frame.bakeConvention = "BakeMesh(true)+renderer.TransformPoint";
                frame.bakeError = errorTrue;
                if (frame.bakeError > .00005f)
                {
                    WriteBakeFailure(s, actor, renderer, frame, falseRaw, trueRaw);
                    throw new InvalidOperationException("BakeMesh/LBS disagreement: " + frame.bakeError + " at " + action + " " + phase + "; exact diagnostic saved to " + auditOutput);
                }
            }
            finally { Object.DestroyImmediate(baked); }
            s.frames.Add(frame);
        }
        private static void WriteBakeFailure(Session s, ReactiveCombatActorVisual actor, SkinnedMeshRenderer renderer, Frame frame, Vector3[] falseRaw, Vector3[] trueRaw)
        {
            Matrix4x4 rotationOnly = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);
            var d = new BakeConventionFailure {
                action = frame.action, phase = frame.phase, currentPose = actor.CurrentPose, clock = frame.clock,
                model = actor.ModelRoot.name, renderer = AnimationUtility.CalculateTransformPath(renderer.transform, actor.ModelRoot),
                mesh = AssetDatabase.GetAssetPath(s.original) + ":" + s.original.name,
                quality = "renderer=" + renderer.quality + "; global=" + QualitySettings.skinWeights,
                vertexCount = s.vertices.Length, boneCount = renderer.bones.Length, bindPoseCount = s.bindposes.Length,
                bakedFalseCount = falseRaw.Length, bakedTrueCount = trueRaw.Length,
                rendererWorld = renderer.transform.localToWorldMatrix, modelWorld = actor.ModelRoot.localToWorldMatrix,
                rendererScale = renderer.transform.lossyScale, modelScale = actor.ModelRoot.lossyScale,
                bones = renderer.bones.Select((b, i) => new BoneDiagnostic { index = i, name = b.name, parent = b.parent == null ? "" : b.parent.name,
                    world = b.localToWorldMatrix, bindpose = s.bindposes[i], skin = frame.bones[i], localPosition = b.localPosition,
                    localRotation = b.localRotation, localScale = b.localScale, worldScale = b.lossyScale }).ToArray()
            };
            var vertices = new List<VertexDiagnostic>();
            using (var counts = s.original.GetBonesPerVertex())
            using (var allWeights = s.original.GetAllBoneWeights())
            {
                var offsets = new int[s.vertices.Length]; int offset = 0;
                for (int i = 0; i < offsets.Length; i++) { offsets[i] = offset; offset += counts[i]; }
                foreach (int i in s.handVertexIndices)
                {
                    Vector3 bind = s.sampledVertices == null ? s.vertices[i] : s.sampledVertices[i];
                    Vector3 expected = Skin(s.weights[i], frame.bones).MultiplyPoint3x4(bind);
                    Vector3 fTransform = renderer.transform.TransformPoint(falseRaw[i]), tTransform = renderer.transform.TransformPoint(trueRaw[i]);
                    Vector3 fRotation = rotationOnly.MultiplyPoint3x4(falseRaw[i]), tRotation = rotationOnly.MultiplyPoint3x4(trueRaw[i]);
                    d.falseTransformPointError = Mathf.Max(d.falseTransformPointError, Vector3.Distance(expected, fTransform));
                    d.falseRotationOnlyError = Mathf.Max(d.falseRotationOnlyError, Vector3.Distance(expected, fRotation));
                    d.trueTransformPointError = Mathf.Max(d.trueTransformPointError, Vector3.Distance(expected, tTransform));
                    d.trueRotationOnlyError = Mathf.Max(d.trueRotationOnlyError, Vector3.Distance(expected, tRotation));
                    d.falseRawWorldError = Mathf.Max(d.falseRawWorldError, Vector3.Distance(expected, falseRaw[i]));
                    d.trueRawWorldError = Mathf.Max(d.trueRawWorldError, Vector3.Distance(expected, trueRaw[i]));
                    var variable = new List<string>();
                    for (int k = 0; k < counts[i]; k++)
                    {
                        BoneWeight1 w = allWeights[offsets[i] + k]; variable.Add(w.boneIndex + ":" + s.boneNames[w.boneIndex] + "=" + w.weight.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    vertices.Add(new VertexDiagnostic { index = i, bindVertex = bind, lbsWorld = expected,
                        bakeFalseRaw = falseRaw[i], bakeTrueRaw = trueRaw[i], bakeFalseTransformPoint = fTransform, bakeTrueTransformPoint = tTransform,
                        bakeFalseRotationOnly = fRotation, bakeTrueRotationOnly = tRotation,
                        mismatch = Vector3.Distance(expected, tTransform),
                        fourWeights = Describe(s.weights[i], s.boneNames), fourWeightSum = s.weights[i].weight0 + s.weights[i].weight1 + s.weights[i].weight2 + s.weights[i].weight3,
                        allWeightCount = counts[i], allWeights = string.Join(";", variable) });
                }
            }
            d.worstVertices = vertices.OrderByDescending(v => v.mismatch).Take(16).ToArray();
            File.WriteAllText(Path.Combine(auditOutput ?? DefaultOutput, "BakeConventionFailure.json"), JsonUtility.ToJson(d, true));
        }
        private static Measurement[] MeasureAll(Session s, Vector3[] candidate, int order)
        {
            var rows = new List<Measurement>();
            foreach (Frame frame in s.frames)
            {
                CheckBudget("measure " + frame.action + " " + frame.phase);
                var row = new Measurement { action = frame.action, phase = frame.phase, clock = frame.clock,
                    renderer = s.original.name, meshPath = AssetDatabase.GetAssetPath(s.original), weapon = frame.weapon, weaponWorldScale = frame.weaponWorldScale,
                    triangleCount = s.handTriangles.Length / 3, bakeSkinError = frame.bakeError, bakeConvention = frame.bakeConvention,
                    weaponBoundaryEdges = frame.sword.BoundaryEdges, weaponNonManifoldEdges = frame.sword.NonManifoldEdges };
                Vector3[] points = new Vector3[s.vertices.Length];
                foreach (int i in s.handVertexIndices)
                    points[i] = frame.worldToWeapon.MultiplyPoint3x4(frame.bakedWorld[i]);
                for (int t = 0; t < s.handTriangles.Length; t += 3)
                {
                    int a = s.handTriangles[t], b = s.handTriangles[t + 1], c = s.handTriangles[t + 2];
                    foreach (Vector3 bary in BarycentricLattice(order))
                    {
                        Vector3 p = points[a] * bary.x + points[b] * bary.y + points[c] * bary.z;
                        bool ambiguous; Vector3 closest;
                        float localDepth = frame.sword.Depth(p, out closest, out ambiguous);
                        row.samples++;
                        float depth = frame.weaponToWorld.MultiplyVector((p - closest).normalized * localDepth).magnitude;
                        if (ambiguous) { row.ambiguousSamples++; row.maximumAmbiguousDepth = Mathf.Max(row.maximumAmbiguousDepth, depth); continue; }
                        if (localDepth <= 0) continue;
                        row.insideSamples++;
                        if (depth <= row.maximumDepth) continue;
                        row.maximumDepth = depth; row.deepestTriangle = t / 3;
                        row.deepestPoint = frame.weaponToWorld.MultiplyPoint3x4(p); row.deepestSurface = frame.weaponToWorld.MultiplyPoint3x4(closest);
                        row.deepestVertexWeights = new[] { a + ":" + Describe(s.weights[a], s.boneNames), b + ":" + Describe(s.weights[b], s.boneNames), c + ":" + Describe(s.weights[c], s.boneNames) };
                    }
                }
                rows.Add(row);
                CheckBudget("measured " + frame.action + " " + frame.phase);
            }
            return rows.ToArray();
        }
        private static Frame[] SelectTrainingFrames(Session s, Measurement[] rows)
        {
            var eligible = Enumerable.Range(0, rows.Length).Where(i => rows[i].weaponBoundaryEdges == 0 && rows[i].weaponNonManifoldEdges == 0).OrderByDescending(i => rows[i].maximumDepth).ToArray();
            var chosen = new List<int>();
            foreach (string action in new[] { "Heavy", "Normal", "Guard", "HeavyIdle", "NormalIdle", "Throw" })
            {
                int index = Array.Find(eligible, i => rows[i].action == action);
                if (!eligible.Any(i => rows[i].action == action)) index = -1;
                if (index >= 0) chosen.Add(index);
            }
            foreach (int index in eligible) if (chosen.Count < 8 && !chosen.Contains(index)) chosen.Add(index);
            return chosen.Select(i => s.frames[i]).ToArray();
        }
        private static void SolveBoundedCandidate(Session s, Vector3[] candidate, float originalDepth, Frame[] training)
        {
            // The movement budget derives from the measured original intrusion;
            // no whole-hand reweighting, arbitrary digit rotations or weapon resize.
            float worldBudget = originalDepth * 2f + SurfaceClearance;
            Vector3[] correction = new Vector3[candidate.Length]; float[] contribution = new float[candidate.Length];
            for (int iteration = 0; iteration < 3; iteration++)
            {
                CheckBudget("candidate iteration " + iteration);
                Array.Clear(correction, 0, correction.Length); Array.Clear(contribution, 0, contribution.Length);
                int violations = 0;
                foreach (Frame frame in training)
                {
                    CheckBudget("candidate " + iteration + " " + frame.action);
                    Matrix4x4[] skin = new Matrix4x4[candidate.Length]; Vector3[] points = new Vector3[candidate.Length];
                    foreach (int i in s.handVertexIndices) { skin[i] = Skin(s.weights[i], frame.bones); points[i] = frame.worldToWeapon.MultiplyPoint3x4(skin[i].MultiplyPoint3x4(candidate[i])); }
                    for (int t = 0; t < s.handTriangles.Length; t += 3)
                    {
                        int[] ids = { s.handTriangles[t], s.handTriangles[t + 1], s.handTriangles[t + 2] };
                        foreach (Vector3 bary in BarycentricLattice(TrainingOrder))
                        {
                            Vector3 p = points[ids[0]] * bary.x + points[ids[1]] * bary.y + points[ids[2]] * bary.z;
                            bool ambiguous; Vector3 closest; float depth = frame.sword.Depth(p, out closest, out ambiguous);
                            if (ambiguous || depth <= 0) continue;
                            Vector3 delta = frame.weaponToWorld.MultiplyVector(closest - p);
                            if (delta.magnitude <= DepthLimit * .5f) continue;
                            violations++;
                            delta += delta.normalized * SurfaceClearance;
                            float norm = 0;
                            for (int j = 0; j < 3; j++) if (s.handMask[ids[j]]) norm += bary[j] * bary[j];
                            if (norm < .0001f) continue;
                            for (int j = 0; j < 3; j++)
                            {
                                int id = ids[j]; if (!s.handMask[id] || bary[j] <= 0) continue;
                                correction[id] += skin[id].inverse.MultiplyVector(delta) * (bary[j] / norm);
                                contribution[id] += 1f;
                            }
                        }
                    }
                }
                if (violations == 0) break;
                for (int i = 0; i < candidate.Length; i++) if (contribution[i] > 0)
                {
                    Vector3 proposal = candidate[i] + correction[i] / contribution[i] * .65f;
                    Vector3 change = proposal - s.vertices[i];
                    float worldChange = s.frames.Max(f => Skin(s.weights[i], f.bones).MultiplyVector(change).magnitude);
                    if (worldChange > worldBudget) change *= worldBudget / worldChange;
                    candidate[i] = s.vertices[i] + change;
                }
            }
        }
        private static IEnumerable<Vector3> BarycentricLattice(int order)
        {
            for (int a = 0; a <= order; a++) for (int b = 0; b <= order - a; b++)
                yield return new Vector3(a / (float)order, b / (float)order, (order - a - b) / (float)order);
            yield return new Vector3(1f / 3f, 1f / 3f, 1f / 3f);
        }
        private static Matrix4x4 Skin(BoneWeight w, Matrix4x4[] bones)
        {
            Matrix4x4 m = new Matrix4x4();
            for (int b = 0; b < 4; b++)
            {
                float weight = Weight(w, b); if (weight <= 0) continue;
                Matrix4x4 bone = bones[BoneIndex(w, b)];
                for (int i = 0; i < 16; i++) m[i] += bone[i] * weight;
            }
            return m;
        }
        private static float Weight(BoneWeight w, int i) => i == 0 ? w.weight0 : i == 1 ? w.weight1 : i == 2 ? w.weight2 : w.weight3;
        private static int BoneIndex(BoneWeight w, int i) => i == 0 ? w.boneIndex0 : i == 1 ? w.boneIndex1 : i == 2 ? w.boneIndex2 : w.boneIndex3;
        private static string Describe(BoneWeight w, string[] bones) => string.Join(";", Enumerable.Range(0, 4).Where(i => Weight(w, i) > 0).Select(i => bones[BoneIndex(w, i)] + "=" + Weight(w, i).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        private static void CheckBudget(string operation)
        {
            if (auditTimer == null || auditTimer.Elapsed.TotalSeconds <= auditBudget) return;
            string message = "Heavy geometry audit exceeded its explicit " + auditBudget + " second budget at " + operation + ". No derivative asset or binding was written.";
            if (!string.IsNullOrEmpty(auditOutput)) File.WriteAllText(Path.Combine(auditOutput, "HeavySkinningAuditTimeout.txt"), message);
            throw new TimeoutException(message);
        }
        private static void Tick(ReactiveCombatActorVisual actor, float duration)
        {
            for (float elapsed = 0; elapsed < duration; elapsed += 1f / 60f) actor.TickPresentation(Mathf.Min(1f / 60f, duration - elapsed));
        }
        private static void TickToPose(ReactiveCombatActorVisual actor, float time)
        {
            if (time > actor.CurrentPoseSeconds) Tick(actor, time - actor.CurrentPoseSeconds);
        }
        private static string HashFile(string asset)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(Path.IsPathRooted(asset) ? asset : Path.Combine(Path.GetDirectoryName(Application.dataPath), asset)))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static string[] NativeHashes(ReactiveCombatActorClips c)
        {
            var clips = new List<AnimationClip>();
            foreach (LicensedCombatMotionProfile p in new[] { c.licensedNormal, c.licensedHeavy, c.licensedThrow })
                if (p != null) { clips.Add(p.anticipation); clips.Add(p.baseIdle); clips.AddRange(p.segments.Select(s => s.clip)); }
            return clips.Where(c2 => c2 != null).Select(AssetDatabase.GetAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => p + "=" + HashFile(p) + ";meta=" + HashFile(p + ".meta")).ToArray();
        }

        private sealed class Surface
        {
            public int BoundaryEdges { get; private set; }
            public int NonManifoldEdges { get; private set; }
            private readonly Vector3[] vertices;
            private readonly int[] triangles, order;
            private readonly List<Node> nodes = new List<Node>();
            private readonly List<float> hits = new List<float>();
            private struct Node { public Bounds box; public int start, count, left, right; }
            private static readonly Vector3[] Rays = {
                new Vector3(1f, .371f, .193f).normalized,
                new Vector3(.217f, 1f, .419f).normalized,
                new Vector3(.337f, .163f, 1f).normalized };
            public Surface(Vector3[] vertices, int[] triangles)
            {
                this.vertices = vertices; this.triangles = triangles;
                order = Enumerable.Range(0, triangles.Length / 3).Where(t => Vector3.Cross(vertices[triangles[t * 3 + 1]] - vertices[triangles[t * 3]], vertices[triangles[t * 3 + 2]] - vertices[triangles[t * 3]]).sqrMagnitude > 1e-18f).ToArray();
                if (order.Length == 0) throw new ArgumentException("No surface triangles.");
                InspectClosure();
                Build(0, order.Length);
            }
            private void InspectClosure()
            {
                // Importers split position-identical UV/normal seams. Weld only
                // for edge incidence analysis; the original mesh is never welded.
                var welded = new Dictionary<string, int>(); var ids = new int[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 p = vertices[i]; string key = Mathf.RoundToInt(p.x * 1000000f) + ":" + Mathf.RoundToInt(p.y * 1000000f) + ":" + Mathf.RoundToInt(p.z * 1000000f);
                    int id; if (!welded.TryGetValue(key, out id)) { id = welded.Count; welded.Add(key, id); } ids[i] = id;
                }
                var incidence = new Dictionary<long, int>();
                for (int triangle = 0; triangle < triangles.Length / 3; triangle++)
                    for (int edge = 0; edge < 3; edge++)
                    {
                        int a = ids[triangles[triangle * 3 + edge]], b = ids[triangles[triangle * 3 + (edge + 1) % 3]];
                        if (a == b) continue;
                        long key = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
                        int count; incidence.TryGetValue(key, out count); incidence[key] = count + 1;
                    }
                BoundaryEdges = incidence.Count(pair => pair.Value == 1);
                NonManifoldEdges = incidence.Count(pair => pair.Value > 2);
            }
            private int Build(int start, int count)
            {
                int id = nodes.Count; nodes.Add(default);
                Bounds bounds = TriangleBounds(order[start]);
                for (int i = start + 1; i < start + count; i++) bounds.Encapsulate(TriangleBounds(order[i]));
                var node = new Node { box = bounds, start = start, count = count, left = -1, right = -1 };
                if (count > 12)
                {
                    Vector3 size = bounds.size; int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
                    Array.Sort(order, start, count, Comparer<int>.Create((a, b) => TriangleCenter(a)[axis].CompareTo(TriangleCenter(b)[axis])));
                    int half = count / 2; node.left = Build(start, half); node.right = Build(start + half, count - half);
                }
                nodes[id] = node; return id;
            }
            private Bounds TriangleBounds(int t)
            {
                var b = new Bounds(vertices[triangles[t * 3]], Vector3.zero);
                b.Encapsulate(vertices[triangles[t * 3 + 1]]); b.Encapsulate(vertices[triangles[t * 3 + 2]]); return b;
            }
            private Vector3 TriangleCenter(int t) => (vertices[triangles[t * 3]] + vertices[triangles[t * 3 + 1]] + vertices[triangles[t * 3 + 2]]) / 3f;
            public float Depth(Vector3 p, out Vector3 closest, out bool ambiguous)
            {
                closest = p; ambiguous = false;
                if (!nodes[0].box.Contains(p)) return 0;
                float d2 = float.PositiveInfinity; Nearest(0, p, ref d2, ref closest);
                if (d2 <= 1e-14f) return 0;
                int inside = 0;
                foreach (Vector3 direction in Rays)
                {
                    hits.Clear(); Cast(0, p, direction); hits.Sort();
                    int unique = 0; float previous = float.NegativeInfinity;
                    foreach (float t in hits) if (t - previous > 1e-6f) { unique++; previous = t; }
                    if ((unique & 1) == 1) inside++;
                }
                ambiguous = inside != 0 && inside != Rays.Length;
                return inside == Rays.Length || ambiguous ? Mathf.Sqrt(d2) : 0;
            }
            private void Nearest(int id, Vector3 p, ref float best, ref Vector3 closest)
            {
                Node n = nodes[id]; if (n.box.SqrDistance(p) >= best) return;
                if (n.left >= 0)
                {
                    int first = nodes[n.left].box.SqrDistance(p) <= nodes[n.right].box.SqrDistance(p) ? n.left : n.right;
                    int second = first == n.left ? n.right : n.left;
                    Nearest(first, p, ref best, ref closest); Nearest(second, p, ref best, ref closest); return;
                }
                for (int i = n.start; i < n.start + n.count; i++)
                {
                    int t = order[i] * 3; Vector3 q = ClosestTriangle(p, vertices[triangles[t]], vertices[triangles[t + 1]], vertices[triangles[t + 2]]);
                    float d = (p - q).sqrMagnitude; if (d < best) { best = d; closest = q; }
                }
            }
            private void Cast(int id, Vector3 p, Vector3 d)
            {
                Node n = nodes[id]; if (!n.box.IntersectRay(new Ray(p, d))) return;
                if (n.left >= 0) { Cast(n.left, p, d); Cast(n.right, p, d); return; }
                for (int i = n.start; i < n.start + n.count; i++)
                {
                    int t = order[i] * 3; Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                    Vector3 e1 = b - a, e2 = c - a, h = Vector3.Cross(d, e2); float det = Vector3.Dot(e1, h);
                    if (Mathf.Abs(det) < 1e-10f) continue;
                    Vector3 v = p - a; float u = Vector3.Dot(v, h) / det; if (u < -1e-6f || u > 1 + 1e-6f) continue;
                    Vector3 q = Vector3.Cross(v, e1); float w = Vector3.Dot(d, q) / det; if (w < -1e-6f || u + w > 1 + 1e-6f) continue;
                    float hit = Vector3.Dot(e2, q) / det; if (hit > 1e-7f) hits.Add(hit);
                }
            }
            private static Vector3 ClosestTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 ab = b - a, ac = c - a, ap = p - a; float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
                if (d1 <= 0 && d2 <= 0) return a;
                Vector3 bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
                if (d3 >= 0 && d4 <= d3) return b;
                float vc = d1 * d4 - d3 * d2;
                if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
                Vector3 cp = p - c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
                if (d6 >= 0 && d5 <= d6) return c;
                float vb = d5 * d2 - d1 * d6;
                if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
                float va = d3 * d6 - d5 * d4;
                if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b + (c - b) * ((d4 - d3) / (d4 - d3 + d5 - d6));
                float inv = 1f / (va + vb + vc); return a + ab * (vb * inv) + ac * (vc * inv);
            }
        }
    }
}
#endif
