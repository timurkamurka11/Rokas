using System;
using System.IO;
using System.Linq;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;

namespace Rokas.Editor
{
    // Imports an explicitly supplied, licensed retarget manifest. Raw source assets
    // are outside this project; only its own-rig derivative clips are referenced.
    public static class LicensedCombatMotionImporter
    {
        [Serializable] private sealed class Manifest { public ProfileData[] profiles; }
        [Serializable] private sealed class KeyData
        {
            public float time, value, inTangent, outTangent, inWeight, outWeight;
            public int weightedMode;
            public bool stepIn, stepOut;
        }
        [Serializable] private sealed class CurveData { public string property; public KeyData[] keys; }
        [Serializable] private sealed class SegmentData
        {
            public string name;
            public float start, duration, clipIn, speed, easeIn, easeOut, blendIn, blendOut;
            public KeyData[] mixIn, mixOut;
        }
        [Serializable] private sealed class ShakeData
        {
            public string name;
            public float start, duration, clipIn, speed;
            public int priority;
            public bool muted;
            public KeyData[] x, y, z;
        }
        [Serializable] private sealed class ProfileData
        {
            public string name, actor, field, sourceSkill, sourceTimeline, sourceCharacter;
            public string anticipation, baseIdle, weapon, sourceBinding, calibration;
            public float idleSpeed, sourceToTargetScale;
            public bool authoredWeaponSocket;
            public string[] sourceAssetIdentifiers;
            public SegmentData[] segments;
            public CurveData[] cameraCurves;
            public ShakeData[] shakes;
            public Vector3 parentPosition, parentEuler, rootPosition;
            public int performerFramingSize;
            public LicensedCameraFramingOffset[] framingOffsets;
        }

        public static void ImportFromManifest()
        {
            try
            {
                string[] arguments = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(arguments, "-licensedMotionManifest");
                if (index < 0 || index + 1 >= arguments.Length) throw new ArgumentException("Missing licensed motion manifest");
                Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(arguments[index + 1]));
                var library = AssetDatabase.LoadAssetAtPath<ReactiveCombatActorLibrary>(
                    "Assets/Rokas/Resources/Combat/ReactiveCombatActorLibrary.asset");
                if (library == null || manifest.profiles == null) throw new InvalidOperationException("Missing actor library or profiles");
                string root = "Assets/Rokas/Resources/Combat/LicensedMotions";
                Directory.CreateDirectory(root);
                AssetDatabase.Refresh();
                foreach (ProfileData data in manifest.profiles)
                {
                    string path = root + "/" + data.name + ".asset";
                    var profile = AssetDatabase.LoadAssetAtPath<LicensedCombatMotionProfile>(path);
                    if (profile == null)
                    {
                        profile = ScriptableObject.CreateInstance<LicensedCombatMotionProfile>();
                        AssetDatabase.CreateAsset(profile, path);
                    }
                    profile.sourceSkill = data.sourceSkill;
                    profile.sourceTimeline = data.sourceTimeline;
                    profile.sourceCharacter = data.sourceCharacter;
                    profile.sourceAssetIdentifiers = data.sourceAssetIdentifiers;
                    profile.anticipation = Clip(data.actor, data.anticipation);
                    profile.baseIdle = Clip(data.actor, data.baseIdle);
                    profile.idleSpeed = data.idleSpeed;
                    profile.sourceToTargetScale = data.sourceToTargetScale;
                    profile.authoredWeaponSocket = data.authoredWeaponSocket;
                    profile.weapon = (LicensedWeaponKind)Enum.Parse(typeof(LicensedWeaponKind), data.weapon);
                    profile.segments = data.segments.Select(segment => new LicensedMotionSegment {
                        clip = Clip(data.actor, segment.name), start = segment.start, duration = segment.duration,
                        clipIn = segment.clipIn, speed = segment.speed, easeIn = segment.easeIn, easeOut = segment.easeOut,
                        blendIn = segment.blendIn, blendOut = segment.blendOut,
                        mixIn = Curve(segment.mixIn), mixOut = Curve(segment.mixOut)
                    }).ToArray();
                    profile.camera = new LicensedCameraData {
                        parentPosition = data.parentPosition, parentEuler = data.parentEuler, rootPosition = data.rootPosition,
                        performerFramingSize = (LicensedCameraFramingSize)data.performerFramingSize,
                        framingOffsets = data.framingOffsets, sourceBinding = data.sourceBinding,
                        routeShakeToTimelineActiveCamera = true,
                        calibration = data.calibration, shakes = data.shakes.Select(shake => new LicensedCameraShakeSegment {
                            start = shake.start, duration = shake.duration, clipIn = shake.clipIn, speed = shake.speed,
                            priority = shake.priority, muted = shake.muted,
                            x = Curve(shake.x), y = Curve(shake.y), z = Curve(shake.z)
                        }).ToArray()
                    };
                    foreach (CurveData cameraCurve in data.cameraCurves)
                    {
                        AnimationCurve curve = Curve(cameraCurve.keys);
                        string property = cameraCurve.property;
                        if (property.Contains("Position"))
                        {
                            if (property.EndsWith(".x")) profile.camera.x = curve;
                            else if (property.EndsWith(".y")) profile.camera.y = curve;
                            else if (property.EndsWith(".z")) profile.camera.z = curve;
                        }
                        else if (property.Contains("Euler"))
                        {
                            if (property.EndsWith(".x")) profile.camera.pitch = curve;
                            else if (property.EndsWith(".y")) profile.camera.yaw = curve;
                            else if (property.EndsWith(".z")) profile.camera.roll = curve;
                        }
                    }
                    ReactiveCombatActorClips definition = data.actor == "keiko" ? library.keiko : library.yokai;
                    typeof(ReactiveCombatActorClips).GetField(data.field).SetValue(definition, profile);
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssetIfDirty(profile);
                    Debug.Log("LICENSED_PROFILE_IMPORTED " + data.name + " duration=" + profile.Duration + " source=" + data.sourceTimeline);
                }
                EditorUtility.SetDirty(library);
                // Saving all assets would rewrite read-only binary motion clips
                // under ForceText. Persist only this importer's generated settings.
                AssetDatabase.SaveAssetIfDirty(library);
                AssetDatabase.Refresh();
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        private static AnimationClip Clip(string actor, string name)
        {
            string target = actor == "keiko" ? "Keiko" : "Yokai";
            string path = "Assets/Rokas/Art/CombatActors/LicensedMotions/" + target + "/" + target + "_DD2_" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null || !clip.legacy) throw new InvalidOperationException("Missing Legacy derivative clip: " + path);
            return clip;
        }

        private static AnimationCurve Curve(KeyData[] keys)
        {
            return new AnimationCurve(keys.Select(key => new Keyframe(key.time, key.value,
                key.stepIn ? float.PositiveInfinity : key.inTangent,
                key.stepOut ? float.PositiveInfinity : key.outTangent,
                key.inWeight, key.outWeight) { weightedMode = (WeightedMode)key.weightedMode }).ToArray());
        }
    }
}
