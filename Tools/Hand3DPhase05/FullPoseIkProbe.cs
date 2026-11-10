using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Metroidbrainia.Hand3DExperiment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Metroidbrainia
{
    // Disposable Editor experiment. Staged only into a marked copy by Run.ps1.
    public static class FullPoseIkProbe
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static Hand3DRig rig;
        private static Hand3DArmMovement arm;
        private static Transform[] bones, digits, twists;
        private static Transform shoulder, hint, grip;
        private static Vector3[] originalPositions, originalScales;
        private static float upperLength, forearmLength;
        private static string directory, runId;
        private static int assertions;
        private static readonly List<Group> groups = new List<Group>();
        private static readonly List<Measurement> measurements = new List<Measurement>();
        private static readonly List<Profile> captures = new List<Profile>();
        private static readonly List<string> notes = new List<string>();

        [Serializable] private sealed class Profile
        {
            public string name;
            public Vector3[] positions, scales;
            public Quaternion[] rotations;
            public Vector3 modelInCamera;
            public Quaternion modelRotationInCamera, wristInCamera;
        }
        [Serializable] private sealed class CaptureFile { public string[] paths; public Profile[] profiles; }
        [Serializable] private sealed class Measurement
        {
            public string scenario;
            public bool limited, onScreen;
            public Vector3 requested, expected, reached, elbow;
            public float worldError, pixels1080, wristDegrees, jointDegrees, relativeLengthError;
        }
        [Serializable] private sealed class Group { public string name, status, detail; public int assertions; }
        [Serializable] private sealed class Report
        {
            public string runId, utc, unity, scope, pixelSpace;
            public int assertions, samples, reachableSamples, limitedSamples, onScreenSamples;
            public float maxWorldError, maxPixels1080, maxWristDegrees, maxJointDegrees, maxRelativeLengthError;
            public Group[] groups;
            public string[] hierarchy, notes;
        }

        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Flags).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
        private static object Call(object target, string method, params object[] values) => target.GetType().GetMethods(Flags)
            .Single(m => m.Name == method && m.GetParameters().Length == values.Length && m.GetParameters().Select((p, i) => p.ParameterType.IsInstanceOfType(values[i])).All(v => v)).Invoke(target, values);
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); assertions++; }
        private static string PathOf(Transform bone) => bone.parent != null ? PathOf(bone.parent) + "/" + bone.name : bone.name;
        private static void Write(string name, object value) => File.WriteAllText(Path.Combine(directory, name), JsonUtility.ToJson(value, true));

        public static void Run()
        {
            if (!File.Exists("phase0-isolated-copy.marker")) throw new InvalidOperationException("Isolated project marker required.");
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-phase05RunId");
            if (index < 0) throw new InvalidOperationException("Run through Phase05/Run.ps1.");
            runId = args[index + 1]; directory = Path.GetFullPath("phase05-output/" + runId); Directory.CreateDirectory(directory);
            GroupRun("capture_roundtrip_and_elbow", CaptureAndElbow);
            GroupRun("full_pose_motion_grid", MotionGrid);
            GroupRun("GrabDoor_contact", Contact);
            GroupRun("transitions_return_and_100_cycles", TransitionsAndCycles);
            GroupRun("current_rig_gesture_recovery", CurrentRigRecovery);
            GroupRun("isolated_WRAD_URP_renders", RenderEvidence);
            var report = new Report
            {
                runId = runId, utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                scope = "Real WRAD and unmodified solver in an isolated Editor copy. Captures in memory only. No Game/physical-input visual approval.",
                pixelSpace = "Viewport difference scaled to 1920x1080; independent of 320x180 RT; no final UI raster measurement.",
                assertions = assertions, samples = measurements.Count, reachableSamples = measurements.Count(m => !m.limited), limitedSamples = measurements.Count(m => m.limited), onScreenSamples = measurements.Count(m => m.onScreen),
                maxWorldError = Maximum(m => m.worldError), maxPixels1080 = Maximum(m => m.pixels1080), maxWristDegrees = Maximum(m => m.wristDegrees), maxJointDegrees = Maximum(m => m.jointDegrees), maxRelativeLengthError = Maximum(m => m.relativeLengthError),
                groups = groups.ToArray(), notes = notes.ToArray(), hierarchy = bones != null ? bones.Select(PathOf).ToArray() : new string[0]
            };
            Write("report.json", report); Write("measurements.json", new MeasurementFile { measurements = measurements.ToArray() });
            Debug.Log("PHASE05_RESULT " + JsonUtility.ToJson(report));
            EditorApplication.Exit(groups.Any(g => g.status == "FAIL") ? 1 : 0);
        }
        [Serializable] private sealed class MeasurementFile { public Measurement[] measurements; }
        private static float Maximum(Func<Measurement, float> select) => measurements.Count > 0 ? measurements.Max(select) : 0f;
        private static void GroupRun(string name, Action test)
        {
            int start = assertions;
            try { Setup(); test(); groups.Add(new Group { name = name, status = "PASS", assertions = assertions - start, detail = "Executed in isolated Unity Editor." }); }
            catch (Exception error)
            {
                var cause = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                groups.Add(new Group { name = name, status = "FAIL", assertions = assertions - start, detail = cause.ToString() });
                Debug.LogError("PHASE05 " + name + " " + cause);
            }
        }

        private static void Setup()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/TestArmScene.unity", OpenSceneMode.Single);
            rig = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Hand3DRig>(true)).Single();
            arm = rig.GetComponent<Hand3DArmMovement>();
            Check(arm.Initialize(), "Movement initialization");
            var recognizer = rig.GetComponent<Hand3DPoseRecognizer>(); Call(recognizer, "OnDisable"); Call(recognizer, "OnEnable");
            Check(rig.Initialize(), "Rig initialization");
            bones = rig.GetComponentsInChildren<Transform>(true);
            shoulder = bones.Single(b => b.name == "shoulder.r");
            digits = rig.Fingers.SelectMany(f => f.Joints).Select(j => j.Bone).ToArray();
            twists = bones.Where(b => b.name.StartsWith("forearm.Twist") && b.name.EndsWith(".r")).ToArray();
            Check(arm.UpperArm.name == "bicep.r" && arm.Forearm.name == "forearm.r" && arm.Hand.name == "wrist.r" && arm.UpperArm.parent == shoulder, "Unexpected WRAD chain");
            Check(digits.Length == 15 && twists.Length == 2, "Unexpected finger/twist bindings");
            originalPositions = bones.Select(b => b.localPosition).ToArray(); originalScales = bones.Select(b => b.localScale).ToArray();
            upperLength = Vector3.Distance(arm.UpperArm.position, arm.Forearm.position); forearmLength = Vector3.Distance(arm.Forearm.position, arm.Hand.position);
            hint = new GameObject("Discardable captured elbow reference").transform;
            Set(arm, "elbowHint", hint);
            grip = rig.GetComponent<Hand3DDoorInteractor>().HandGripPoint;
            arm.BeginControl();
        }

        private static void Gesture(string name)
        {
            arm.ReleaseContact();
            var interaction = rig.GetComponent<Hand3DInteractionPose>(); interaction.SetGrab(false); interaction.SetHandHold(false); Call(rig, "UpdateRig", 1f);
            int mask = name == "Point" || name == "GrabDocument" ? 29 : 3;
            var state = rig.GetComponent<Hand3DFingerState>(); for (int i = 0; i < 5; i++) state.SetFlexed((Finger)i, (mask & (1 << i)) != 0);
            arm.ApplyFrame(arm.Modes.Exploration.Frame); Call(rig, "RequestTransition"); Call(rig, "UpdateRig", 1f);
            if (name == "GrabDoor") interaction.SetGrab(true);
            if (name == "GrabDocument") interaction.SetHandHold(true);
            Call(rig, "UpdateRig", 1f);
        }

        private static Profile Capture(string name) => new Profile
        {
            name = name, positions = bones.Select(b => b.localPosition).ToArray(), scales = bones.Select(b => b.localScale).ToArray(), rotations = bones.Select(b => b.localRotation).ToArray(),
            modelInCamera = arm.ArmCamera.transform.InverseTransformPoint(rig.transform.position), modelRotationInCamera = Quaternion.Inverse(arm.ArmCamera.transform.rotation) * rig.transform.rotation,
            wristInCamera = Quaternion.Inverse(arm.ArmCamera.transform.rotation) * arm.Hand.rotation
        };
        private static Profile[] Profiles(string gesture)
        {
            Gesture(gesture); var a = Capture(gesture + "/Exploration");
            arm.ApplyFrame(arm.Modes.HandEdit.Frame);
            shoulder.localRotation = shoulder.localRotation * Quaternion.Euler(8f, -12f, 5f);
            Call(rig, "UpdateRig", 1f);
            var b = Capture(gesture + "/HandEdit-derived-shoulder-variant");
            Apply(a, a, 0f); return new[] { a, b };
        }
        private static Quaternion Apply(Profile a, Profile b, float weight)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = Vector3.Lerp(a.positions[i], b.positions[i], weight);
                bones[i].localRotation = Quaternion.Slerp(a.rotations[i], b.rotations[i], weight);
                bones[i].localScale = a.scales[i];
            }
            rig.transform.SetPositionAndRotation(arm.ArmCamera.transform.TransformPoint(Vector3.Lerp(a.modelInCamera, b.modelInCamera, weight)), arm.ArmCamera.transform.rotation * Quaternion.Slerp(a.modelRotationInCamera, b.modelRotationInCamera, weight));
            Quaternion wrist = arm.ArmCamera.transform.rotation * Quaternion.Slerp(a.wristInCamera, b.wristInCamera, weight);
            arm.Hand.rotation = wrist;
            hint.position = arm.Forearm.position;
            return wrist;
        }

        private static void CaptureAndElbow()
        {
            float maxFixedPoleError = 0f, maxCapturedPoleError = 0f;
            var baseline = Capture("Original");
            foreach (string gesture in new[] { "OK", "Point", "GrabDoor", "GrabDocument" })
            {
                Apply(baseline, baseline, 0f); var pair = Profiles(gesture); captures.AddRange(pair);
                foreach (var profile in pair)
                {
                    Quaternion wrist = Apply(profile, profile, 0f); Vector3 target = arm.Hand.position, elbow = arm.Forearm.position;
                    var captured = Capture("roundtrip");
                    Check(bones.Select((bone, i) => Quaternion.Angle(bone.localRotation, profile.rotations[i]) < .1f).All(v => v), "Capture/apply roundtrip");
                    Set(arm, "elbowHint", null); Call(arm, "SolveArm", target); arm.Hand.rotation = wrist;
                    maxFixedPoleError = Mathf.Max(maxFixedPoleError, Vector3.Distance(elbow, arm.Forearm.position));
                    Apply(profile, profile, 0f); Set(arm, "elbowHint", hint); Call(arm, "SolveArm", target); arm.Hand.rotation = wrist;
                    float error = Vector3.Distance(elbow, arm.Forearm.position); maxCapturedPoleError = Mathf.Max(maxCapturedPoleError, error);
                    Check(error < .001f, "Captured pole did not reproduce elbow: " + error);
                    Measure("rest/" + profile.name, target, target, wrist, captured, false);
                }
            }
            notes.Add("Elbow reproduction at rest: fixed initialization pole max world error=" + maxFixedPoleError.ToString("R", CultureInfo.InvariantCulture) + "; captured elbow pole=" + maxCapturedPoleError.ToString("R", CultureInfo.InvariantCulture));
            Write("captures.json", new CaptureFile { paths = bones.Select(PathOf).ToArray(), profiles = captures.ToArray() });
        }

        private static IEnumerable<Vector3> Targets()
        {
            var camera = arm.ArmCamera; Vector3 wrist = arm.Hand.position; float radius = (upperLength + forearmLength) * .08f;
            yield return wrist;
            foreach (Vector2 offset in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down, new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                yield return wrist + camera.transform.right * offset.x * radius + camera.transform.up * offset.y * radius;
            Vector3 direction = (wrist - arm.UpperArm.position).normalized;
            float minimum = Mathf.Abs(upperLength - forearmLength) + .000001f, maximum = upperLength + forearmLength - .000001f;
            foreach (float weight in new[] { .1f, .5f, .85f, .99f }) yield return arm.UpperArm.position + direction * Mathf.Lerp(minimum, maximum, weight);
            yield return arm.UpperArm.position + direction * maximum * 1.5f;
            yield return arm.UpperArm.position + direction * minimum * .25f;
            float depth = camera.WorldToViewportPoint(wrist).z;
            foreach (float x in new[] { .15f, .5f, .85f }) foreach (float y in new[] { .15f, .5f, .85f }) yield return camera.ViewportToWorldPoint(new Vector3(x, y, depth));
        }

        private static Vector3 Clamped(Vector3 target, out bool limited)
        {
            Vector3 offset = target - arm.UpperArm.position; float raw = offset.magnitude;
            float distance = Mathf.Clamp(raw, Mathf.Abs(upperLength - forearmLength) + .000001f, upperLength + forearmLength - .000001f);
            limited = Mathf.Abs(raw - distance) > .00001f;
            return arm.UpperArm.position + (raw > .000001f ? offset / raw : arm.ArmCamera.transform.forward) * distance;
        }

        private static void MotionGrid()
        {
            var original = Capture("Original");
            foreach (string gesture in new[] { "OK", "Point", "GrabDoor", "GrabDocument" })
            {
                Apply(original, original, 0f); var pair = Profiles(gesture);
                foreach (float weight in new[] { 0f, .5f, 1f })
                {
                    Apply(pair[0], pair[1], weight); var targets = Targets().ToArray(); var reference = Capture("frame");
                    for (int i = 0; i < targets.Length; i++)
                    {
                        Quaternion wrist = Apply(pair[0], pair[1], weight); Vector3 expected = Clamped(targets[i], out bool limited);
                        bool reportedLimit = (bool)Call(arm, "SolveArm", targets[i]); arm.Hand.rotation = wrist;
                        Check(reportedLimit == limited, "Reach classification mismatch");
                        Measure("motion/" + gesture + "/w=" + weight + "/target=" + i, targets[i], expected, wrist, reference, limited);
                    }
                }
            }
            // Negative control: applying the captured arm rotations after solving must break a moved target.
            var p = Profiles("OK")[0]; Quaternion orientation = Apply(p, p, 0f); var moved = arm.Hand.position + arm.ArmCamera.transform.right * .04f;
            Vector3 wanted = Clamped(moved, out _); Call(arm, "SolveArm", moved); arm.Hand.rotation = orientation;
            float solved = Vector3.Distance(arm.Hand.position, wanted); Apply(p, p, 0f); float overwritten = Vector3.Distance(arm.Hand.position, wanted);
            notes.Add("Negative control, late full-pose overwrite: solved error=" + solved.ToString("R", CultureInfo.InvariantCulture) + "; overwritten error=" + overwritten.ToString("R", CultureInfo.InvariantCulture));
            Check(overwritten > .005f && solved < .001f, "Negative control failed to detect competing pose writer");
            Apply(p, p, 0f); Vector3 far = arm.UpperArm.position + (arm.Hand.position - arm.UpperArm.position).normalized * (upperLength + forearmLength) * 2f;
            Vector3 previous = Vector3.zero;
            for (int i = 0; i < 100; i++)
            {
                Quaternion wrist = Apply(p, p, 0f); var reference = Capture("limit stability"); Vector3 expected = Clamped(far, out bool limited);
                Call(arm, "SolveArm", far); arm.Hand.rotation = wrist; Measure("limit-repeat/" + i, far, expected, wrist, reference, limited);
                Check(limited && (i == 0 || Vector3.Distance(previous, arm.Hand.position) < .001f), "Unreachable limit is unstable"); previous = arm.Hand.position;
            }
        }

        private static void Contact()
        {
            var pair = Profiles("GrabDoor");
            float maximumGripError = 0f, maximumGripPixels = 0f;
            foreach (float weight in new[] { 0f, .5f, 1f })
            {
                Apply(pair[0], pair[1], weight); Vector3 origin = grip.position; var reference = Capture("contact");
                for (int i = 0; i < 120; i++)
                {
                    Quaternion wrist = Apply(pair[0], pair[1], weight) * Quaternion.Euler(0f, 9f, 0f); arm.Hand.rotation = wrist;
                    Vector3 targetGrip = origin + arm.ArmCamera.transform.right * (.025f * Mathf.Sin(i * .1f)) + arm.ArmCamera.transform.up * (.025f * Mathf.Cos(i * .1f));
                    Vector3 offset = grip.position - arm.Hand.position; Vector3 expectedWrist = Clamped(targetGrip - offset, out bool limited);
                    arm.FollowContact(grip, targetGrip); arm.ResolveContactAfterPose(); arm.Hand.rotation = wrist;
                    Vector3 expectedGrip = expectedWrist + offset;
                    maximumGripError = Mathf.Max(maximumGripError, Vector3.Distance(grip.position, expectedGrip));
                    maximumGripPixels = Mathf.Max(maximumGripPixels, Vector2.Scale((Vector2)(arm.ArmCamera.WorldToViewportPoint(grip.position) - arm.ArmCamera.WorldToViewportPoint(expectedGrip)), new Vector2(1920, 1080)).magnitude);
                    Measure("contact/w=" + weight + "/frame=" + i, targetGrip - offset, expectedWrist, wrist, reference, limited);
                    if (!limited) Check(Vector3.Distance(grip.position, targetGrip) <= .001f, "GrabDoor grip detached");
                    arm.ReleaseContact();
                }
            }
            notes.Add("GrabDoor moving synthetic contact: max grip error=" + maximumGripError.ToString("R", CultureInfo.InvariantCulture) + "; reference pixels=" + maximumGripPixels.ToString("R", CultureInfo.InvariantCulture) + ". Includes a synthetic 9-degree per-handle wrist offset.");
        }

        private static void TransitionsAndCycles()
        {
            var a = Profiles("OK")[0]; Apply(a, a, 0f); var b = Profiles("Point")[1];
            Apply(a, a, 0f); Vector3 rest = arm.Hand.position; Quaternion restWrist = arm.Hand.rotation;
            float maxEndpointDrift = 0f, maxTransitionStep = 0f, maxElbowStep = 0f;
            for (int cycle = 0; cycle < 100; cycle++)
            {
                Quaternion previous = restWrist;
                Apply(a, a, 0f); Vector3 previousElbow = arm.Forearm.position;
                for (int frame = 0; frame <= 60; frame++)
                {
                    float weight = Mathf.SmoothStep(0, 1, frame / 60f); Quaternion wrist = Apply(a, b, weight);
                    Vector3 target = arm.Hand.position + arm.ArmCamera.transform.right * (.025f * Mathf.Sin(frame * Mathf.PI / 60f));
                    Vector3 expected = Clamped(target, out bool limited); var reference = Capture("transition"); Call(arm, "SolveArm", target); arm.Hand.rotation = wrist;
                    maxTransitionStep = Mathf.Max(maxTransitionStep, Quaternion.Angle(previous, wrist)); previous = wrist;
                    maxElbowStep = Mathf.Max(maxElbowStep, Vector3.Distance(previousElbow, arm.Forearm.position)); previousElbow = arm.Forearm.position;
                    Measure("transition/cycle=" + cycle + "/frame=" + frame, target, expected, wrist, reference, limited);
                }
                // Return both posture and displacement smoothly to A; no accumulated IK result is captured as the next base.
                for (int frame = 0; frame <= 60; frame++)
                {
                    float weight = Mathf.SmoothStep(0, 1, frame / 60f); Quaternion wrist = Apply(b, a, weight);
                    Vector3 target = arm.Hand.position + arm.ArmCamera.transform.up * (.025f * Mathf.Sin(frame * Mathf.PI / 60f));
                    Vector3 expected = Clamped(target, out bool limited); var reference = Capture("return"); Call(arm, "SolveArm", target); arm.Hand.rotation = wrist;
                    Measure("return/cycle=" + cycle + "/frame=" + frame, target, expected, wrist, reference, limited);
                    maxElbowStep = Mathf.Max(maxElbowStep, Vector3.Distance(previousElbow, arm.Forearm.position)); previousElbow = arm.Forearm.position;
                }
                maxEndpointDrift = Mathf.Max(maxEndpointDrift, Vector3.Distance(rest, arm.Hand.position));
                Check(Vector3.Distance(rest, arm.Hand.position) < .001f && Quaternion.Angle(restWrist, arm.Hand.rotation) < .1f, "Cycle endpoint drift");
            }
            Check(maxElbowStep < (upperLength + forearmLength) * .25f, "Discontinuous elbow branch on sampled transition");
            notes.Add("100 cycles: max endpoint world drift=" + maxEndpointDrift.ToString("R", CultureInfo.InvariantCulture) + "; maximum wrist step on 60-step outbound transition=" + maxTransitionStep.ToString("R", CultureInfo.InvariantCulture) + " degrees; max consecutive elbow step=" + maxElbowStep.ToString("R", CultureInfo.InvariantCulture) + ". This is not perceptual approval.");
            Quaternion interrupted = Apply(a, b, .4f); var displayed = Capture("Interrupted displayed pose");
            Apply(displayed, b, 0f); Check(Quaternion.Angle(interrupted, arm.Hand.rotation) < .1f, "Interrupted blend begins with wrist jump");
            Check(bones.Select((bone, i) => Quaternion.Angle(bone.localRotation, displayed.rotations[i]) < .1f && Vector3.Distance(bone.localPosition, displayed.positions[i]) < .00001f).All(v => v), "Interrupted blend begins with a bone jump");
            var initialCameraRotation = arm.ArmCamera.transform.rotation;
            arm.ArmCamera.transform.rotation = initialCameraRotation * Quaternion.Euler(8f, 13f, 0f);
            Quaternion cameraWrist = Apply(a, a, 0f); Vector3 cameraTarget = arm.Hand.position + arm.ArmCamera.transform.right * .02f;
            var cameraReference = Capture("camera relative"); var cameraExpected = Clamped(cameraTarget, out bool cameraLimited); Call(arm, "SolveArm", cameraTarget); arm.Hand.rotation = cameraWrist;
            Measure("camera-relative", cameraTarget, cameraExpected, cameraWrist, cameraReference, cameraLimited);
            arm.ArmCamera.transform.rotation = initialCameraRotation;
        }

        private static void CurrentRigRecovery()
        {
            foreach (string interactionName in new[] { "GrabDoor", "GrabDocument" })
            {
                Gesture("Point"); var point = Capture("Point expected"); Gesture("OK");
                var interaction = rig.GetComponent<Hand3DInteractionPose>();
                if (interactionName == "GrabDoor") interaction.SetGrab(true); else interaction.SetHandHold(true);
                Call(rig, "UpdateRig", 1f); var heldDigits = digits.Select(d => d.localRotation).ToArray();
                var state = rig.GetComponent<Hand3DFingerState>(); for (int i = 0; i < 5; i++) state.SetFlexed((Finger)i, (29 & (1 << i)) != 0);
                Call(rig, "RequestTransition"); Call(rig, "UpdateRig", 1f);
                Check(digits.Select((d, i) => Quaternion.Angle(d.localRotation, heldDigits[i]) < .05f).All(v => v), "Logical gesture change replaced held fingers");
                if (interactionName == "GrabDoor") interaction.SetGrab(false); else interaction.SetHandHold(false);
                Call(rig, "UpdateRig", 1f);
                Check(state.Current == (FlexedFingers)29, "Interaction changed logical fingers");
                Check(digits.All(d => Quaternion.Angle(d.localRotation, point.rotations[Array.IndexOf(bones, d)]) < .05f), "Point fingers not restored");
                Check(Quaternion.Angle(Quaternion.Inverse(arm.ArmCamera.transform.rotation) * arm.Hand.rotation, point.wristInCamera) < .1f, "Point special wrist not restored");
            }
        }

        private static void Measure(string scenario, Vector3 requested, Vector3 expected, Quaternion wrist, Profile reference, bool limited)
        {
            Vector3 reached = arm.Hand.position; var camera = arm.ArmCamera; Vector3 a = camera.WorldToViewportPoint(expected), b = camera.WorldToViewportPoint(reached);
            float world = Vector3.Distance(reached, expected), pixels = a.z > camera.nearClipPlane && b.z > camera.nearClipPlane ? Vector2.Scale((Vector2)(a - b), new Vector2(1920, 1080)).magnitude : 0f;
            float wristError = Quaternion.Angle(wrist, arm.Hand.rotation);
            float joints = digits.Concat(twists).Max(d => Quaternion.Angle(d.localRotation, reference.rotations[Array.IndexOf(bones, d)]));
            float length = Mathf.Max(Mathf.Abs(Vector3.Distance(arm.UpperArm.position, arm.Forearm.position) / upperLength - 1f), Mathf.Abs(Vector3.Distance(arm.Forearm.position, arm.Hand.position) / forearmLength - 1f));
            measurements.Add(new Measurement { scenario = scenario, requested = requested, expected = expected, reached = reached, elbow = arm.Forearm.position, limited = limited, onScreen = b.z > camera.nearClipPlane && b.x >= 0f && b.x <= 1f && b.y >= 0f && b.y <= 1f, worldError = world, pixels1080 = pixels, wristDegrees = wristError, jointDegrees = joints, relativeLengthError = length });
            Check(!float.IsNaN(world) && !float.IsInfinity(world) && world <= .001f, scenario + " target error=" + world);
            Check(pixels <= .5f && wristError <= .1f && joints <= .05f && length <= .0001f, scenario + " orientation/length/pixel tolerance");
            Check(bones.Select((bone, i) => bone.localScale == originalScales[i] && (bone == rig.transform || bone.localPosition == originalPositions[i])).All(v => v), scenario + " changed offsets or scales");
        }

        private static void RenderEvidence()
        {
            Check(GraphicsSettings.currentRenderPipeline != null, "URP isolated renders unavailable: no active pipeline");
            var root = Capture("Original"); var rt = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32); rt.Create();
            var overlay = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Hand3DThumbOverlay>(true)).Single();
            var skin = Get<SkinnedMeshRenderer>(overlay, "thumbRenderer");
            var skinRenderers = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var wasEnabled = skinRenderers.Select(s => s.enabled).ToArray();
            foreach (var source in skinRenderers) source.enabled = false;
            var snapshotObject = new GameObject("Discardable CPU-skinned render evidence", typeof(MeshFilter), typeof(MeshRenderer)); snapshotObject.layer = skin.gameObject.layer;
            snapshotObject.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
            var baked = new Mesh(); snapshotObject.GetComponent<MeshFilter>().sharedMesh = baked;
            var preview = new GameObject("Discardable inspection camera").AddComponent<Camera>(); preview.CopyFrom(arm.ArmCamera); preview.enabled = false; preview.targetTexture = rt;
            preview.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var signatures = new HashSet<string>();
            try
            {
                foreach (string gesture in new[] { "OK", "Point", "GrabDoor", "GrabDocument" })
                {
                    Apply(root, root, 0f); var pair = Profiles(gesture);
                    foreach (int variant in new[] { 0, 1, 2 })
                    {
                        var profile = pair[variant == 2 ? 1 : 0]; Quaternion wrist = Apply(profile, profile, 0f);
                        Vector3 target = arm.Hand.position + (variant == 0 ? Vector3.zero : arm.ArmCamera.transform.right * .04f);
                        var reference = Capture("render"); var expected = Clamped(target, out bool limited); Call(arm, "SolveArm", target); arm.Hand.rotation = wrist;
                        Measure("render/" + gesture + "/" + variant, target, expected, wrist, reference, limited);
                        skin.BakeMesh(baked, true);
                        baked.RecalculateBounds(); snapshotObject.GetComponent<MeshFilter>().sharedMesh = baked;
                        snapshotObject.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation); snapshotObject.transform.localScale = skin.transform.lossyScale;
                        // Same viewing orientation, reframed on the hand. This is not the Game camera/composition.
                        Vector3 center = (arm.Hand.position + digits.Select(d => d.position).Aggregate(Vector3.zero, (sum, p) => sum + p) / digits.Length) * .5f;
                        preview.transform.rotation = arm.ArmCamera.transform.rotation; preview.transform.position = center - preview.transform.forward * .65f;
                        preview.ResetWorldToCameraMatrix(); preview.ResetProjectionMatrix();
                        preview.cullingMask = 1 << snapshotObject.layer; preview.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                        Physics.SyncTransforms();
                        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                        RenderPipeline.SubmitRenderRequest(preview, request);
                        var previous = RenderTexture.active; RenderTexture.active = rt;
                        var image = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                        image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
                        byte[] png = image.EncodeToPNG(); File.WriteAllBytes(Path.Combine(directory, gesture + "-" + variant + "-baked-URP.png"), png);
                        int opaque = image.GetPixels32().Count(c => c.a > 0);
                        if (opaque <= 100) notes.Add("Empty render diagnostics: vertices=" + baked.vertexCount + "; local bounds=" + baked.bounds + "; world bounds=" + snapshotObject.GetComponent<Renderer>().bounds + "; wrist=" + arm.Hand.position + "; center=" + center + "; camera=" + preview.transform.position + "; scale=" + skin.transform.lossyScale + "; layer=" + snapshotObject.layer);
                        Check(opaque > 100, "Empty CPU-skinned evidence render");
                        signatures.Add(Convert.ToBase64String(png));
                        notes.Add(gesture + " inspection render variant " + variant + ": alpha pixels=" + opaque + ". CPU baked deformation, original shared materials, reframed inspection camera; no Game composition.");
                        RenderTexture.active = previous; Object.DestroyImmediate(image);
                    }
                }
                Check(signatures.Count >= 8, "Rendered gestures/variants did not update their deformation");
            }
            finally
            {
                for (int i = 0; i < skinRenderers.Length; i++) skinRenderers[i].enabled = wasEnabled[i];
                Object.DestroyImmediate(preview.gameObject); Object.DestroyImmediate(snapshotObject); Object.DestroyImmediate(baked); rt.Release(); Object.DestroyImmediate(rt);
            }
        }
    }
}
