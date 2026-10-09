using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Rokas.Tests
{
    public sealed class CombatHeavySkinningGeometryRegressionTests
    {
        private const string TypeName = "Rokas.Editor.CombatHeavySkinningGeometryAudit";
        private static Type Tool => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(TypeName)).FirstOrDefault(t => t != null)
            ?? throw new InvalidOperationException("The isolated Editor geometry tool was not loaded.");
        [Serializable] private sealed class Row
        {
            public string action, phase;
            public int triangleCount, samples, insideSamples, ambiguousSamples;
            public float maximumDepth, bakeSkinError;
        }
        [Serializable] private sealed class Report
        {
            public string status;
            public int meshVertices, selectedHandTriangles, changedVertices, invertedBindTriangles;
            public bool immutableAssetsExact, originalTopologyAndWeightsExact, outsideMaskVerticesExact, candidateAccepted;
            public Row[] originalRows, candidateRows;
        }

        [Test]
        public void ClosedSurfaceMetricMeasuresTriangleDistanceAndIgnoresWindingAndDuplicateSeams()
        {
            Vector3[] v = {
                new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(0,1,0),
                new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1) };
            int[] t = { 0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4, 3,7,6, 3,6,2, 0,4,7, 0,7,3, 1,2,6, 1,6,5 };
            Vector3[] p = { new Vector3(.5f,.5f,.5f), new Vector3(.01f,.4f,.6f), new Vector3(2,.5f,.5f), new Vector3(.5f,.5f,0) };
            float[] expected = { .5f, .01f, 0, 0 };
            AssertDistances(v, t, p, expected);
            int[] reversed = t.Reverse().ToArray();
            AssertDistances(v, reversed, p, expected);
            Vector3[] split = t.Select(i => v[i]).ToArray();
            AssertDistances(split, Enumerable.Range(0, t.Length).ToArray(), p, expected);
        }

        [Test]
        public void OpenSurfaceCannotBeReportedAsAPassingPhysicalGrip()
        {
            Vector3[] v = { Vector3.zero, Vector3.right, Vector3.up, Vector3.forward };
            int[] open = { 0,2,1, 0,1,3, 0,3,2 };
            float[] result = Evaluate(v, open, new[] { new Vector3(.1f,.1f,.1f) });
            Assert.That(float.IsNaN(result[0]), Is.True, "An open triangle soup must block a closed-surface proof.");
        }

        [Test]
        public void HeavyAuditUsesActualHandTrianglesAndBakeMeshWithoutChangingOriginalAssets()
        {
            Report report = Run(false);
            Assert.That(report.immutableAssetsExact, Is.True);
            Assert.That(report.originalTopologyAndWeightsExact, Is.True);
            Assert.That(report.meshVertices, Is.GreaterThan(100));
            Assert.That(report.selectedHandTriangles, Is.GreaterThan(10));
            Assert.That(report.originalRows.Length, Is.GreaterThan(20));
            foreach (string action in new[] { "Heavy", "Normal", "Throw", "Guard", "NormalIdle", "HeavyIdle" })
                Assert.That(report.originalRows.Any(r => r.action == action && r.samples > r.triangleCount), Is.True, action);
            Assert.That(report.originalRows.Where(r => r.action == "Heavy").Max(r => r.maximumDepth), Is.GreaterThan(.001f),
                "The existing physical defect must be measured from triangles, not accepted by palm markers.");
            Assert.That(report.originalRows.Max(r => r.bakeSkinError), Is.LessThanOrEqualTo(.00005f));
        }

        [Test, Explicit("Run deliberately after the original geometry audit; failure records a bounded unresolved mesh candidate.")]
        public void BoundedDerivativeMustClearActualTrianglesAcrossHeavyNormalThrowGuardAndIdle()
        {
            Report report = Run(true);
            Assert.That(report.immutableAssetsExact && report.originalTopologyAndWeightsExact && report.outsideMaskVerticesExact, Is.True);
            Assert.That(report.changedVertices, Is.GreaterThan(0));
            Assert.That(report.invertedBindTriangles, Is.Zero, "The patch cannot fold the bind mesh.");
            Assert.That(report.candidateAccepted, Is.True, "Inspect the recorded per-phase depth/ambiguity; a blocked candidate is not a delivered repair.");
            Assert.That(report.candidateRows.All(r => r.maximumDepth <= .001f && r.ambiguousSamples == 0 && r.bakeSkinError <= .00005f), Is.True);
        }

        [Serializable] private sealed class FocusedPatchResult
        {
            public bool originalAssetExact;
            public float sourceClock;
            public int[] triangleVertices;
            public Row measurement;
        }

        [Test]
        public void FocusedHeavyRecoveryExportsExactOriginalIndexSkinningForAnatomicalRepair()
        {
            string json = (string)Tool.GetMethod("ExportFocusedHeavyIndexPatch", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { null });
            var result = JsonUtility.FromJson<FocusedPatchResult>(json);
            Assert.That(result.originalAssetExact, Is.True);
            Assert.That(result.sourceClock, Is.EqualTo(2.216667f).Within(.0001f));
            Assert.That(result.triangleVertices.Length, Is.EqualTo(3));
            Assert.That(result.measurement.bakeSkinError, Is.LessThanOrEqualTo(.00005f));
            Assert.That(result.measurement.maximumDepth, Is.GreaterThan(.001f), "The unresolved actual triangle defect is exported, not hidden.");
        }

        private static void AssertDistances(Vector3[] vertices, int[] triangles, Vector3[] samples, float[] expected)
        {
            float[] measured = Evaluate(vertices, triangles, samples);
            for (int i = 0; i < measured.Length; i++) Assert.That(measured[i], Is.EqualTo(expected[i]).Within(.00001f), "sample " + i);
        }
        private static float[] Evaluate(Vector3[] vertices, int[] triangles, Vector3[] samples) =>
            (float[])Tool.GetMethod("EvaluateClosedSurface", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { vertices, triangles, samples });
        private static Report Run(bool candidate)
        {
            string json = (string)Tool.GetMethod("RunAudit", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { candidate, null });
            return JsonUtility.FromJson<Report>(json);
        }
    }
}
