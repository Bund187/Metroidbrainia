using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Metroidbrainia.Hand3DExperiment;
using Metroidbrainia.Hand3DExperiment.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.Profiling;
using Object = UnityEngine.Object;

namespace Metroidbrainia
{
    // Editor-only synthetic measurements. No reflection or fixture allocations inside measured delegates.
    public static class Phase7Validation
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly ProfilerMarker Marker = new ProfilerMarker("Hand3D.Phase7.Synthetic");
        [Serializable] class Measurement { public string operation; public int iterations; public double[] microseconds, bytes; public double bakeCalls, projections, worldUpdates; }
        [Serializable] class Pose { public string backend; public int mask; public Vector3[] positions; public Quaternion[] rotations; }
        [Serializable] class Report { public string runId, scope; public int assertions, meshVertices, thumbVertices, triangles; public long allocationPositiveControl; public bool allocationCounterSupported; public int[] pickupBakes; public Measurement[] measurements; }
        static int checks;
        static readonly List<Measurement> measurements = new List<Measurement>();
        static readonly List<Pose> poses = new List<Pose>();
        static readonly List<int> pickupBakes = new List<int>();
        static long allocationPositiveControl;
        static bool allocationCounterSupported;
        static long Counter(Type type, string field) => (long)type.GetField(field, Flags).GetValue(null);
        static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Flags).GetValue(o);
        static Action<float> Tick(object o, string name) => (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), o, o.GetType().GetMethod(name, Flags));
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        static void Measure(string name, Action operation, int iterations = 100)
        {
            for (int i = 0; i < 40; i++) operation();
            var row = new Measurement { operation = name, iterations = iterations, microseconds = new double[5], bytes = new double[5] };
            var watch = new Stopwatch();
            long bakes = Counter(typeof(Hand3DThumbOverlay), "Phase7BakeCalls"), projections = Counter(typeof(Hand3DThumbOverlay), "Phase7ProjectionCalls"), worlds = Counter(typeof(Hand3DPoseComposition), "Phase7WorldUpdates");
            for (int batch = 0; batch < 5; batch++)
            {
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                watch.Restart();
                using (Marker.Auto()) for (int i = 0; i < iterations; i++) operation();
                watch.Stop();
                row.bytes[batch] = allocationCounterSupported ? (GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)iterations : -1;
                row.microseconds[batch] = watch.Elapsed.TotalMilliseconds * 1000 / iterations;
            }
            row.bakeCalls = (Counter(typeof(Hand3DThumbOverlay), "Phase7BakeCalls") - bakes) / (double)(iterations * 5);
            row.projections = (Counter(typeof(Hand3DThumbOverlay), "Phase7ProjectionCalls") - projections) / (double)(iterations * 5);
            row.worldUpdates = (Counter(typeof(Hand3DPoseComposition), "Phase7WorldUpdates") - worlds) / (double)(iterations * 5);
            measurements.Add(row);
        }
        public static void Run()
        {
            Hand3DPoseConversion.IsolatedRoot();
            try
            {
                long allocationStart = GC.GetAllocatedBytesForCurrentThread();
                var allocationProbe = new byte[4096];
                GC.KeepAlive(allocationProbe);
                allocationPositiveControl = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
                allocationCounterSupported = allocationPositiveControl >= 4096;
                int meshVertices = 0, thumbVertices = 0, triangles = 0;
                foreach (bool universal in new[] { false, true })
                {
                    var fixture = typeof(Phase5Validation).GetMethod("Create", Flags).Invoke(null, new object[] { universal, null });
                    var rig = Get<Hand3DRig>(fixture, "rig");
                    var arm = Get<Hand3DArmMovement>(fixture, "arm");
                    var state = Get<Hand3DFingerState>(fixture, "state");
                    var actions = Get<Hand3DPoseActions>(fixture, "actions");
                    var movementTick = Tick(arm, "UpdateMovement"); var rigTick = Tick(rig, "UpdateRig"); var actionTick = Tick(actions, "Tick");
                    string prefix = universal ? "Universal/" : "Legacy/";
                    Action step = () => { movementTick(1f / 60); actionTick(1f / 60); rigTick(1f / 60); };
                    Measure(prefix + "rest_movement_actions_rig", step);
                    Measure(prefix + "component_rig_rest", () => rigTick(1f / 60));
                    Measure(prefix + "component_movement_rest", () => movementTick(1f / 60));
                    Measure(prefix + "component_actions_idle", () => actionTick(1f / 60));
                    arm.BeginControl();
                    int frame = 0;
                    Measure(prefix + "continuous_motion", () => { arm.MoveHand(new Vector2(Mathf.Sin(frame++ * .03f) * 8, 2), true); step(); });
                    Measure(prefix + "rapid_fingers", () => { int mask = frame++ & 31; for (int f = 0; f < 5; f++) state.SetFlexed((Finger)f, (mask & (1 << f)) != 0); rig.GetType().GetMethod("RequestTransition", Flags).Invoke(rig, null); step(); });
                    // Above reflection belongs to the input driver; report it separately, never as rig GC.
                    arm.EndControl(); Measure(prefix + "return_then_rest", step);
                    arm.ToggleMode(); Measure(prefix + "HandEdit", () => { arm.Modes.Inspect(.01f); step(); });
                    arm.ToggleMode(); for (int i = 0; i < 60; i++) step();
                    Action requestTransition = (Action)Delegate.CreateDelegate(typeof(Action), rig, rig.GetType().GetMethod("RequestTransition", Flags));
                    foreach (int mask in new[] { 3, 29 })
                    {
                        for (int f = 0; f < 5; f++) state.SetFlexed((Finger)f, (mask & (1 << f)) != 0);
                        requestTransition(); for (int i = 0; i < 30; i++) step();
                        if (mask == 29) Check(actions.TryExecute() && actions.IsRunning, "PointClick benchmark did not start; pose=" + rig.GetComponent<Hand3DPoseRecognizer>().CurrentPose?.Name + "; error=" + actions.LastActionError + "; gate=" + actions.Interactions.CanExecuteGestureAction + "; timescale=" + Time.timeScale);
                        Measure(prefix + (mask == 3 ? "OK" : "PointClick_cycles"), () => { if (mask == 29 && !actions.IsRunning) actions.TryExecute(); step(); });
                        actions.CancelAction();
                    }
                    var interaction = Get<Hand3DInteractionPose>(fixture, "interaction");
                    arm.BeginControl(); interaction.SetGrab(true);
                    Vector3 contactTarget = arm.Hand.position;
                    Measure(prefix + "GrabDoor_pose_contact_IK", () => { arm.FollowContact(arm.Hand, contactTarget + arm.ArmCamera.transform.right * (.01f * Mathf.Sin(frame++ * .02f))); step(); });
                    arm.ReleaseContact(); interaction.SetGrab(false); for (int i = 0; i < 30; i++) step();
                    var display = rig.GetComponent<Hand3DDocumentDisplay>();
                    var overlay = Object.FindObjectsByType<Hand3DThumbOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
                    overlay.gameObject.SetActive(true);
                    Canvas.ForceUpdateCanvases();
                    var renderer = Get<SkinnedMeshRenderer>(overlay, "thumbRenderer");
                    var indices = Get<int[]>(overlay, "thumbTriangles");
                    meshVertices = renderer.sharedMesh.vertexCount; thumbVertices = indices.Distinct().Count(); triangles = indices.Length / 3;
                    foreach (var mode in new[] { Hand3DThumbOverlay.VisualizationMode.ThumbOverlay, Hand3DThumbOverlay.VisualizationMode.ThumbMask, Hand3DThumbOverlay.VisualizationMode.FullArm })
                    {
                        overlay.Visualization = mode;
                        Measure(prefix + "mask_" + mode, overlay.RefreshMask);
                    }
                    overlay.Visualization = Hand3DThumbOverlay.VisualizationMode.ThumbOverlay;
                    // Compare every used projection against the original formula on animated geometry/layout.
                    var source = overlay.ArmDisplay; var camera = Get<Camera>(overlay, "armCamera");
                    var baked = new Mesh(); var vertices = new List<Vector3>();
                    var originalUV = source.uvRect; var originalSize = source.rectTransform.sizeDelta;
                    for (int sample = 0; sample < 32; sample++)
                    {
                        for (int f = 0; f < 5; f++) state.SetFlexed((Finger)f, (sample & (1 << f)) != 0);
                        rig.GetType().GetMethod("RequestTransition", Flags).Invoke(rig, null);
                        arm.BeginControl(); arm.MoveHand(new Vector2(sample % 2 == 0 ? 30 : -25, 10), true); step();
                        var bones = Get<Transform[]>(fixture, "bones");
                        poses.Add(new Pose { backend = prefix, mask = sample, positions = bones.Select(b => b.position).ToArray(), rotations = bones.Select(b => b.rotation).ToArray() });
                        source.uvRect = sample % 2 == 0 ? originalUV : new Rect(.1f, .05f, .8f, .9f);
                        source.rectTransform.sizeDelta = originalSize + new Vector2(sample, -sample);
                        overlay.RefreshMask(); baked.Clear(); renderer.BakeMesh(baked, true); baked.GetVertices(vertices);
                        var projected = Get<List<Vector3>>(overlay, "projected");
                        foreach (int index in indices.Distinct())
                        {
                            Vector3 viewport = camera.WorldToViewportPoint(renderer.transform.TransformPoint(vertices[index]));
                            Rect uv = source.uvRect, rect = source.rectTransform.rect;
                            Vector2 p = new Vector2((viewport.x - uv.x) / uv.width, (viewport.y - uv.y) / uv.height);
                            Vector3 expected = overlay.rectTransform.InverseTransformPoint(source.rectTransform.TransformPoint(rect.min + Vector2.Scale(p, rect.size)));
                            expected.z = viewport.z;
                            Check(Vector3.Distance(expected, projected[index]) < .003f, "Animated thumb projection differs: " + index);
                        }
                        Canvas.ForceUpdateCanvases(); Check(overlay.GeneratedTriangleCount > 0, "Empty thumb stencil");
                        Check(overlay.ThumbImage.texture == source.texture && overlay.ThumbImage.uvRect == source.uvRect, "Texture/UV mismatch");
                        using (var geometry = new VertexHelper())
                        {
                            overlay.GetType().GetMethod("OnPopulateMesh", Flags, null, new[] { typeof(VertexHelper) }, null).Invoke(overlay, new object[] { geometry });
                            int expectedVertices = 0;
                            for (int triangle = 0; triangle < indices.Length; triangle += 3)
                                if (projected[indices[triangle]].z > camera.nearClipPlane && projected[indices[triangle + 1]].z > camera.nearClipPlane && projected[indices[triangle + 2]].z > camera.nearClipPlane) expectedVertices += 3;
                            Check(geometry.currentVertCount == expectedVertices, "Thumb triangles changed");
                        }
                    }
                    source.uvRect = originalUV; source.rectTransform.sizeDelta = originalSize; Object.DestroyImmediate(baked);
                    Texture originalTexture = source.texture;
                    var resizedTexture = new RenderTexture(640, 360, 0);
                    source.texture = resizedTexture; source.rectTransform.sizeDelta = originalSize + new Vector2(300, 150);
                    overlay.RefreshMask(); Check(overlay.ThumbImage.texture == resizedTexture, "Resized RT not synchronized");
                    Check(overlay.ThumbImage.rectTransform.sizeDelta == source.rectTransform.sizeDelta, "Resized layout not synchronized");
                    source.texture = originalTexture; source.rectTransform.sizeDelta = originalSize; Object.DestroyImmediate(resizedTexture);
                    overlay.Visualization = Hand3DThumbOverlay.VisualizationMode.FullArm;
                    var sampleMethod = overlay.GetType().GetMethod("RefreshMask", Flags, null, new[] { typeof(bool) }, null);
                    if (sampleMethod != null) sampleMethod.Invoke(overlay, new object[] { true }); else overlay.RefreshMask();
                    Check(Get<List<Vector3>>(overlay, "projected").Count == meshVertices, "Full Arm explicit diagnostic lost thumb geometry");
                    overlay.Visualization = Hand3DThumbOverlay.VisualizationMode.ThumbOverlay;
                    overlay.gameObject.SetActive(false); overlay.gameObject.SetActive(true); overlay.RefreshMask();
                    Check(overlay.HasValidMask, "Mask lost after reactivation");
                    var binding = Object.FindObjectsByType<Hand3DDocumentBinding>(FindObjectsSortMode.None).First(b => b.IsReady);
                    interaction.SetHandHold(true);
                    long beforePickup = Counter(typeof(Hand3DThumbOverlay), "Phase7BakeCalls");
                    Check(binding.BeginHold(display), "Cannot show real document");
                    pickupBakes.Add((int)(Counter(typeof(Hand3DThumbOverlay), "Phase7BakeCalls") - beforePickup));
                    var displayTick = Tick(display, "Tick");
                    Measure(prefix + "held_document_display_with_mask", () => { arm.MoveHand(new Vector2(1, -1), true); step(); displayTick(1f / 60); });
                    binding.EndHold(); for (int i = 0; i < 30; i++) displayTick(1f / 60);
                    interaction.SetHandHold(false);
                    Check(!binding.IsHeld && binding.IsReady && !display.IsReleasing, "Document not returned");
                    Measure(prefix + "empty_cancel", actions.CancelAction, 1000);
                    Check(!universal || rig.UniversalActive, "Unexpected fallback");
                }
                string[] args = Environment.GetCommandLineArgs();
                File.WriteAllText("phase7-poses.json", JsonUtility.ToJson(new PoseTrace { poses = poses.ToArray() }, true));
                File.WriteAllText("phase7-results.json", JsonUtility.ToJson(new Report { runId = args[Array.IndexOf(args, "-phase5RunId") + 1], scope = "Synthetic Edit Mode CPU/thread managed allocation per invocation; 40 warmups, 5 batches. Not Game frames, GPU, Player or Unity Profiler capture. bytes=-1 means allocation counter failed positive control, not zero allocations. rapid_fingers includes reflective input driver. GrabDoor measures visual pose/contact IK, not door physics/opening. Initialization excluded.", assertions = checks, meshVertices = meshVertices, thumbVertices = thumbVertices, triangles = triangles, allocationPositiveControl = allocationPositiveControl, allocationCounterSupported = allocationCounterSupported, pickupBakes = pickupBakes.ToArray(), measurements = measurements.ToArray() }, true));
                EditorApplication.Exit(0);
            }
            catch (Exception e) { UnityEngine.Debug.LogException(e); EditorApplication.Exit(1); }
        }
        [Serializable] class PoseTrace { public Pose[] poses; }
    }
}
