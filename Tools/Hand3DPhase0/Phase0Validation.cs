using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Metroidbrainia.Hand3DExperiment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Metroidbrainia
{
    // Staged by Run.ps1 into an isolated project only. Never attach this to a scene.
    public static class Phase0Validation
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const float Step = 1f / 60f;
        private static string output;
        private static readonly List<Result> results = new List<Result>();
        private static int checks;
        [Serializable] private sealed class Result { public string id, status, detail; public int assertions; }
        [Serializable] private sealed class Report { public string unity, scope, runId, generatedUtc; public Result[] results; public int assertions; }
        [Serializable] private sealed class Reference { public string property, globalId, asset, hierarchy; }
        [Serializable] private sealed class Entry { public string type, hierarchy, globalId, serialized; public Reference[] references; }
        [Serializable] private sealed class Inventory { public string scene, scope; public Entry[] entries; }
        [Serializable] private sealed class Combination { public int mask; public string gesture; public Quaternion[] localRotations; }
        [Serializable] private sealed class Combinations { public Combination[] combinations; }
        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Fields).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
        private static void Call(object target, string method, params object[] args)
        {
            var match = target.GetType().GetMethods(Fields).Single(m => m.Name == method && m.GetParameters().Length == args.Length
                && m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(v => v));
            match.Invoke(target, args);
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        private static void ActiveInput(Hand3DInputController input, bool active) => Set(input, "<IsArmModeActive>k__BackingField", active);
        private static string PathOf(Transform item) => item.parent != null ? PathOf(item.parent) + "/" + item.name : item.name;
        private static T[] All<T>() where T : Component => UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static void Write(string name, object data) => File.WriteAllText(System.IO.Path.Combine(output, name), JsonUtility.ToJson(data, true));

        public static void Run()
        {
            output = System.IO.Path.GetFullPath(".");
            // Refuse accidental execution against the user's working project.
            if (!File.Exists(System.IO.Path.Combine(output, "phase0-isolated-copy.marker")))
                throw new InvalidOperationException("Run through Tools/Hand3DPhase0/Run.ps1; isolated copy marker missing.");
            Group("REF", InventoryReference);
            Group("F32", FingerCombinations);
            Group("ACT", GestureActions);
            Group("INPUT", InputBlocking);
            Group("MOV", MovementAndModes);
            Group("INT", InteractionsAndLifecycle);
            results.Add(new Result { id = "VISUAL", status = "PENDING", detail = "Real Game/URP, physical input, localization, auto-open/locked animation timing and final thumb pixels require the manual protocol." });
            string[] arguments = Environment.GetCommandLineArgs(); int runArgument = Array.IndexOf(arguments, "-phase0RunId");
            Write("phase0-results.json", new Report { unity = Application.unityVersion, generatedUtc = DateTime.UtcNow.ToString("O"), runId = runArgument >= 0 ? arguments[runArgument + 1] : "direct", scope = "Current unmodified runtime; isolated Editor stepping and synthetic interaction targets. No Game visual validation.", results = results.ToArray(), assertions = checks });
            EditorApplication.Exit(results.Any(r => r.status == "FAIL") ? 1 : 0);
        }

        private static void Group(string id, Action test)
        {
            int initial = checks;
            try { Load(); test(); results.Add(new Result { id = id, status = "PASS", assertions = checks - initial, detail = "Executed in isolated Unity Editor." }); }
            catch (Exception error)
            {
                Exception cause = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                results.Add(new Result { id = id, status = "FAIL", assertions = checks - initial, detail = cause.ToString() });
                Debug.LogError("PHASE0 " + id + ": " + cause);
            }
            finally { Time.timeScale = 1f; }
        }

        private static void Load() => EditorSceneManager.OpenScene("Assets/Scenes/TestArmScene.unity", OpenSceneMode.Single);

        private static void InventoryReference()
        {
            var rig = All<Hand3DRig>().Single();
            var selected = new HashSet<Object>(rig.GetComponentsInChildren<Component>(true).Where(c => c != null));
            foreach (var root in All<Hand3DDoorBinding>().Cast<Component>().Concat(All<Hand3DDocumentBinding>()).Concat(All<Camera>()).Concat(All<Canvas>()).Concat(All<RawImage>()))
                foreach (var item in root.GetComponents<Component>()) if (item != null) selected.Add(item);
            foreach (var component in rig.GetComponents<Component>())
            {
                var data = new SerializedObject(component); var property = data.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                    {
                        var value = property.objectReferenceValue;
                        selected.Add(value);
                        if (value is Component target) selected.Add(target.transform);
                    }
            }
            var entries = new List<Entry>();
            foreach (var item in selected.OrderBy(o => o.name).ThenBy(o => o.GetType().FullName))
            {
                var references = new List<Reference>(); var property = new SerializedObject(item).GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                    {
                        var value = property.objectReferenceValue;
                        references.Add(new Reference { property = property.propertyPath, globalId = GlobalObjectId.GetGlobalObjectIdSlow(value).ToString(), asset = AssetDatabase.GetAssetPath(value), hierarchy = value is Component component ? PathOf(component.transform) : value.name });
                    }
                entries.Add(new Entry { type = item.GetType().FullName, hierarchy = item is Component owner ? PathOf(owner.transform) : AssetDatabase.GetAssetPath(item), globalId = GlobalObjectId.GetGlobalObjectIdSlow(item).ToString(), serialized = EditorJsonUtility.ToJson(item, true), references = references.ToArray() });
            }
            Write("phase0-inventory.json", new Inventory { scene = "Assets/Scenes/TestArmScene.unity", scope = "Resolved rig hierarchy, experimental components, bindings, cameras, UI and direct rig references. Raw scene and all project source hashes are stored separately.", entries = entries.ToArray() });
            Check(rig.Fingers.Length == 5 && rig.Fingers.Sum(f => f.Joints.Length) == 15, "Expected five fingers and fifteen joints");
            Check(All<Hand3DDoorBinding>().Length == 9, "Expected nine door bindings");
            Check(All<Hand3DDocumentBinding>().Length == 3, "Expected three document bindings");
            Check(rig.Fingers.SelectMany(f => f.Joints).All(j => j.Captured && j.Bone != null), "Missing finger captures/references");
            var modes = rig.GetComponent<Hand3DModeController>();
            Check(modes.Exploration.Captured && modes.HandEdit.Captured, "Missing mode captures");
            var pose = rig.GetComponent<Hand3DInteractionPose>();
            Check(pose.Overrides.Length == 15 && pose.HandHoldOverrides.Length == 15 && pose.AdditionalBones.Length == 3, "Interaction profile count changed");
            var overlay = All<Hand3DThumbOverlay>().Single();
            Check(overlay.IsConfigured && overlay.TriangleCount == 66, "Thumb configuration changed");
            var arm = rig.GetComponent<Hand3DArmMovement>();
            File.WriteAllText(System.IO.Path.Combine(output, "phase0-ik-chain.txt"), "UpperArm=" + PathOf(arm.UpperArm) + "\nForearm=" + PathOf(arm.Forearm) + "\nHand=" + PathOf(arm.Hand) + "\n");
        }

        private static Hand3DRig Initialize()
        {
            var rig = All<Hand3DRig>().Single();
            Check(rig.GetComponent<Hand3DArmMovement>().Initialize(), "Movement initialization failed");
            var recognizer = rig.GetComponent<Hand3DPoseRecognizer>();
            Call(recognizer, "OnDisable"); Call(recognizer, "OnEnable");
            var actions = rig.GetComponent<Hand3DPoseActions>(); Call(actions, "OnDisable"); Call(actions, "OnEnable");
            Check(rig.Initialize(), "Captured rig initialization failed");
            return rig;
        }

        private static void Mask(Hand3DRig rig, int mask)
        {
            var state = rig.GetComponent<Hand3DFingerState>();
            for (int i = 0; i < 5; i++) state.SetFlexed((Finger)i, (mask & (1 << i)) != 0);
            Call(rig, "RequestTransition"); Call(rig, "UpdateRig", 1f);
        }

        private static void FingerCombinations()
        {
            var rig = Initialize(); var recognizer = rig.GetComponent<Hand3DPoseRecognizer>();
            var frames = new List<Combination>();
            for (int mask = 0; mask < 32; mask++)
            {
                Mask(rig, mask);
                var pose = recognizer.CurrentPose;
                Check((pose?.Name) == (mask == 3 ? "OK" : mask == 29 ? "Point" : null), "Recognition mismatch for " + mask);
                Check((int)rig.GetComponent<Hand3DFingerState>().Current == mask, "Logical mask modified");
                foreach (var finger in rig.Fingers)
                    foreach (var joint in finger.Joints)
                    {
                        var entry = pose?.VisualOverrides.FirstOrDefault(p => p.Bone == joint.Bone);
                        var expected = entry != null ? entry.Rotation : (mask & (1 << (int)finger.Finger)) != 0 ? joint.Flexed : joint.Extended;
                        Check(Quaternion.Angle(joint.Bone.localRotation, expected) < 0.05f, "Visual mismatch: " + mask + "/" + joint.Bone.name);
                    }
                frames.Add(new Combination { mask = mask, gesture = pose?.Name ?? "None", localRotations = rig.Fingers.SelectMany(f => f.Joints).Select(j => j.Bone.localRotation).ToArray() });
            }
            Write("phase0-combinations.json", new Combinations { combinations = frames.ToArray() });
            Mask(rig, 3); Call(rig, "UpdateRig", 0f);
            Mask(rig, 29); Mask(rig, 3);
            Check(recognizer.CurrentPose.Name == "OK", "Interrupted gesture transition did not resolve");
        }

        private static void GestureActions()
        {
            var rig = Initialize(); var actions = rig.GetComponent<Hand3DPoseActions>();
            Mask(rig, 3); Check(!actions.TryExecute(), "OK Action=None unexpectedly executed");
            Mask(rig, 29); Check(actions.TryExecute(), "PointClick did not start");
            Check(!actions.TryExecute(), "PointClick accepted overlapping action");
            Mask(rig, 3); Check(!Get<bool>(actions, "clicking") && actions.PointClickWeight == 0f, "Gesture change did not cancel click");
            var recognizer = rig.GetComponent<Hand3DPoseRecognizer>();
            var fixture = new HandPose("Event fixture", (FlexedFingers)31, PoseAction.EventOnly);
            int invoked = 0; fixture.OnAction.AddListener(() => invoked++);
            Set(recognizer, "poses", recognizer.Poses.Concat(new[] { fixture }).ToArray());
            Mask(rig, 31); Check(actions.TryExecute() && invoked == 1 && actions.PointClickWeight == 0f, "EventOnly behavior changed");
            Time.timeScale = 0f; Check(!actions.TryExecute() && invoked == 1, "Paused action executed");
        }

        private static void MovementAndModes()
        {
            var rig = Initialize(); var arm = rig.GetComponent<Hand3DArmMovement>();
            var state = rig.GetComponent<Hand3DFingerState>(); Mask(rig, 3);
            float upperLength = Vector3.Distance(arm.UpperArm.position, arm.Forearm.position), forearmLength = Vector3.Distance(arm.Forearm.position, arm.Hand.position);
            Vector3 forearmPosition = arm.Forearm.localPosition, handPosition = arm.Hand.localPosition;
            arm.BeginControl(); var previous = Get<Vector2>(arm, "targetScreenPosition");
            arm.MoveHand(new Vector2(20, 10), true);
            Check(Get<Vector2>(arm, "targetScreenPosition") != previous, "Mouse displacement had no effect");
            arm.MoveHand(Vector2.one * 100000, true);
            var high = Get<Vector2>(arm, "horizontalScreenLimits"); var vertical = Get<Vector2>(arm, "verticalScreenLimits");
            Check(Get<Vector2>(arm, "targetScreenPosition") == new Vector2(high.y, vertical.y), "Scene upper limits ignored");
            arm.MoveHand(Vector2.one * -100000, true);
            Check(Get<Vector2>(arm, "targetScreenPosition") == new Vector2(high.x, vertical.x), "Scene lower limits ignored");
            for (int i = 0; i < 60; i++) { Call(arm, "UpdateMovement", Step); Call(rig, "UpdateRig", Step); }
            Check(Mathf.Abs(Vector3.Distance(arm.UpperArm.position, arm.Forearm.position) - upperLength) < 0.0001f && Mathf.Abs(Vector3.Distance(arm.Forearm.position, arm.Hand.position) - forearmLength) < 0.0001f, "IK stretched segment lengths");
            Check(arm.Forearm.localPosition == forearmPosition && arm.Hand.localPosition == handPosition, "IK changed segment offsets");
            arm.EndControl(); var pausedPosition = arm.Hand.position; Call(arm, "UpdateMovement", 0f); Call(rig, "UpdateRig", 0f);
            Check(Vector3.Distance(arm.Hand.position, pausedPosition) < 0.00001f, "Zero timestep changed movement");
            for (int i = 0; i < 600; i++) { Call(arm, "UpdateMovement", Step); Call(rig, "UpdateRig", Step); }
            Check(!Get<bool>(arm, "returning") && Vector2.Distance(Get<Vector2>(arm, "screenPosition"), Get<Vector2>(arm, "restScreenPosition")) < 0.0001f, "Return did not settle");
            arm.ToggleMode(); Check(arm.Modes.IsEditing && arm.Modes.IsTransitioning, "Mode toggle did not begin transition");
            var from = arm.transform.position; Call(arm, "UpdateMovement", 0f); Check(arm.transform.position == from, "Mode advanced at zero dt");
            for (int i = 0; i < 60; i++) { Call(arm, "UpdateMovement", Step); Call(rig, "UpdateRig", Step); }
            Check(!arm.Modes.IsTransitioning && state.Current == (FlexedFingers)3, "Mode transition changed logical fingers");
            Vector3 fixedWrist = arm.Hand.position; Quaternion inspectionStart = arm.Hand.rotation;
            arm.Modes.Inspect(-100f);
            for (int i = 0; i < 120; i++) { Call(arm, "UpdateMovement", Step); Call(rig, "UpdateRig", Step); }
            Check(Vector3.Distance(fixedWrist, arm.Hand.position) < 0.0001f, "Inspection moved wrist position");
            Check(Quaternion.Angle(inspectionStart, arm.Hand.rotation) > 1f, "Inspection did not rotate hand");
            arm.ToggleMode(); for (int i = 0; i < 120; i++) { Call(arm, "UpdateMovement", Step); Call(rig, "UpdateRig", Step); }
            Check(!arm.Modes.IsEditing && !arm.Modes.IsTransitioning && state.Current == (FlexedFingers)3, "Exploration restoration failed");
        }

        private static void InputBlocking()
        {
            var rig = Initialize(); var input = rig.GetComponent<Hand3DInputController>();
            var player = Get<UnityEngine.InputSystem.PlayerInput>(input, "playerInput");
            var original = player.actions; string serialized = original.ToJson();
            // Loaded Editor scenes have not run PlayerInput.OnEnable. Restoration is intentionally guarded by inputIsActive.
            bool originalActive = player.inputIsActive;
            player.ActivateInput(); Check(player.inputIsActive, "PlayerInput activation fixture failed");
            var character = player.GetComponent<StarterAssets.FirstPersonController>(); bool wasEnabled = character.enabled;
            Call(input, "Start");
            var runtime = player.actions;
            try
            {
                Check(Get<bool>(input, "initialized") && runtime != original, "Input did not create private action clone");
                var actions = Get<UnityEngine.InputSystem.InputAction[]>(input, "blockedGameplay");
                for (int i = 0; i < actions.Length; i++) { if (i % 2 == 0) actions[i].Enable(); else actions[i].Disable(); }
                bool[] enabled = actions.Select(a => a.enabled).ToArray();
                Call(input, "SetGameplayBlocked", true);
                Check(actions.All(a => !a.enabled), "Gameplay actions not blocked");
                Call(input, "SuspendCharacter", true); Check(!character.enabled, "Inspection did not suspend character");
                Call(input, "SuspendCharacter", false); Check(character.enabled == wasEnabled, "Character enabled state not restored");
                Call(input, "SetGameplayBlocked", false);
                Check(actions.Select((a, i) => a.enabled == enabled[i]).All(v => v), "Action enabled states not restored");
                Check(original.ToJson() == serialized, "Shared input bindings were changed");
                ActiveInput(input, true); Time.timeScale = 0f; Call(input, "Update");
                Check(!input.IsArmModeActive, "Paused Update did not end arm session");
            }
            finally
            {
                Call(input, "SetGameplayBlocked", false); Call(input, "SuspendCharacter", false);
                player.actions = original;
                Call(input, "RestoreEnabledActions", original);
                Get<UnityEngine.InputSystem.InputActionMap>(input, "controls")?.Dispose();
                if (runtime != original) Object.DestroyImmediate(runtime);
                if (!originalActive) player.DeactivateInput();
            }
        }

        private static void InteractionsAndLifecycle()
        {
            var rig = Initialize(); var arm = rig.GetComponent<Hand3DArmMovement>();
            var owner = rig.gameObject; var input = owner.GetComponent<Hand3DInputController>();
            var contact = owner.GetComponent<Hand3DDoorInteractor>(); var documents = owner.GetComponent<Hand3DDocumentInteractor>();
            var display = owner.GetComponent<Hand3DDocumentDisplay>(); var pose = owner.GetComponent<Hand3DInteractionPose>();
            var coordinator = owner.GetComponent<Hand3DInteractionCoordinator>();
            foreach (var collider in All<Collider>()) collider.enabled = false;
            foreach (var binding in All<Hand3DDoorBinding>()) { binding.enabled = false; binding.Door.enabled = false; }
            foreach (var binding in All<Hand3DDocumentBinding>()) binding.enabled = false;
            Call(contact, "Awake"); Call(contact, "Start"); Call(documents, "Awake");
            Set(input, "doorInteractor", contact); Set(input, "documentInteractor", documents);
            Mask(rig, 3); Quaternion[] before = rig.Fingers.SelectMany(f => f.Joints).Select(j => j.Bone.localRotation).ToArray();
            ActiveInput(input, true); arm.BeginControl();
            // Only the contact marker is positioned by this fixture; it does not validate perceived WRAD contact.
            contact.HandGripPoint.position = arm.ArmCamera.ViewportToWorldPoint(new Vector3(.5f, .5f, .7f));
            Check(contact.TryGetHandScreen(out Vector2 hand, out _), "Synthetic marker is not projectable");
            var worldCamera = contact.WorldCamera;
            var root = new GameObject("Phase0 synthetic door").transform;
            var hinge = new GameObject("DoorHinge").transform; hinge.SetParent(root, false);
            var knob = new GameObject("Knob").transform; knob.SetParent(hinge, false); knob.localPosition = Vector3.right * .2f;
            hinge.position = worldCamera.ScreenToWorldPoint(new Vector3(hand.x, hand.y, .7f)) - knob.localPosition;
            var shape = knob.gameObject.AddComponent<SphereCollider>(); shape.radius = .02f; shape.isTrigger = true;
            var point = new GameObject("InteractionPoint").transform; point.SetParent(knob, false);
            var engine = hinge.gameObject.AddComponent<DoorHandInteractable>();
            Set(engine, "doorHinge", hinge); Set(engine, "knob", knob); Set(engine, "interactionPoint", point); Call(engine, "Awake");
            var doorBinding = hinge.gameObject.AddComponent<Hand3DDoorBinding>(); doorBinding.Configure(engine, root);
            Physics.SyncTransforms(); contact.RefreshDoors(); Call(contact, "Tick", Step);
            Check(contact.IsGrabbing && coordinator.Owns(contact), "Door contact did not acquire");
            Call(rig, "UpdateRig", 1f); Check(rig.GetComponent<Hand3DFingerState>().Current == (FlexedFingers)3, "Grab changed logical OK");
            contact.EndArmSession(); Call(rig, "UpdateRig", 1f);
            Check(!contact.IsGrabbing && !coordinator.IsOccupied && Get<bool>(engine, "closing"), "Door cancellation did not request closing");
            Check(rig.Fingers.SelectMany(f => f.Joints).Select((j, i) => Quaternion.Angle(j.Bone.localRotation, before[i]) < .05f).All(v => v), "Grab did not restore logical pose visuals");
            contact.HandGripPoint.position = arm.ArmCamera.ViewportToWorldPoint(new Vector3(.5f, .5f, .7f));
            engine.RestoreOpenState(false); Set(engine, "canOpenByHand", false); Physics.SyncTransforms(); Call(contact, "Tick", Step);
            Check(contact.IsGrabbing && Get<bool>(engine, "lockedInteraction") && Get<bool>(engine, "shaking"), "Locked door feedback did not begin: " + contact.DebugInfo.reason);
            contact.EndArmSession(); Call(rig, "UpdateRig", 1f); engine.RestoreOpenState(false); engine.Unlock();
            contact.HandGripPoint.position = arm.ArmCamera.ViewportToWorldPoint(new Vector3(.5f, .5f, .7f));
            Physics.SyncTransforms(); Call(contact, "Tick", Step); Check(contact.IsGrabbing, "Door retry failed");
            float direction = Get<float>(engine, "openDirection");
            var next = (Vector3)engine.GetType().GetMethod("GetPointAtAngle", Fields).Invoke(engine, new object[] { direction * 5f });
            Vector2 tangent = (Vector2)worldCamera.WorldToScreenPoint(next) - (Vector2)worldCamera.WorldToScreenPoint(point.position);
            Check(tangent.sqrMagnitude > .0001f, "Synthetic door tangent is degenerate");
            Vector2 pixels = tangent.normalized * 200f;
            Vector2 reference = Get<Vector2>(arm, "referenceResolution");
            contact.ProcessMovement(new Vector2(pixels.x / worldCamera.pixelWidth * reference.x, pixels.y / worldCamera.pixelHeight * reference.y), true);
            Check(engine.IsAutoOpening && !contact.IsGrabbing, "Door threshold did not hand off: " + contact.DebugInfo.reason + " angle=" + Get<float>(engine, "currentAngle"));
            shape.enabled = false; engine.enabled = false; doorBinding.enabled = false; Call(rig, "UpdateRig", 1f);
            contact.HandGripPoint.position = arm.ArmCamera.ViewportToWorldPoint(new Vector3(.5f, .5f, .7f));
            Check(contact.TryGetHandScreen(out hand, out _), "Document fixture projection failed");
            var paper = GameObject.CreatePrimitive(PrimitiveType.Quad); paper.name = "Phase0 synthetic document"; paper.transform.localScale = Vector3.one * .02f;
            paper.transform.position = worldCamera.ScreenToWorldPoint(new Vector3(hand.x, hand.y, .7f));
            Vector3 worldPosition = paper.transform.position;
            var source = paper.AddComponent<DocumentHandInteractable>();
            var bindingDocument = paper.AddComponent<Hand3DDocumentBinding>();
            Set(bindingDocument, "document", source); Set(bindingDocument, "worldRenderer", paper.GetComponent<MeshRenderer>()); Set(bindingDocument, "worldCollider", paper.GetComponent<Collider>());
            Set(bindingDocument, "heldSprite", Get<UnityEngine.UI.Image>(display, "documentImage").sprite ?? Get<Sprite>(All<Hand3DDocumentBinding>().First(b => b != bindingDocument), "heldSprite"));
            Set(bindingDocument, "localizedText", new LocalizedString());
            int picked = 0; var pickedEvent = new UnityEvent(); pickedEvent.AddListener(() => picked++); Set(bindingDocument, "onPickedUp", pickedEvent); Call(bindingDocument, "OnEnable");
            Physics.SyncTransforms(); Call(documents, "Tick", Step);
            Check(documents.IsHolding && picked == 1 && pose.IsHandHolding, "Document did not acquire");
            Check(!paper.GetComponent<Renderer>().enabled && !paper.GetComponent<Collider>().enabled && !coordinator.TryAcquire(contact), "Document ownership/hiding failed");
            Call(rig, "UpdateRig", 1f); Call(display, "Tick", 1f);
            var holdPoint = Get<Transform>(display, "documentHoldPoint");
            var armImage = Get<RawImage>(display, "armDisplay"); var visual = Get<RectTransform>(display, "visualRoot");
            var grip = Get<RectTransform>(display, "paperGrip");
            bool projected = Hand3DScreenProjection.TryWorldToScreen(arm.ArmCamera, armImage, holdPoint.position, out Vector2 holdScreen, out _);
            if (projected) Check(Vector2.Distance(grip.position, holdScreen + Get<Vector2>(display, "documentHoldOffset") * Screen.height / 1080f) < .5f, "Paper grip is detached");
            else results.Add(new Result { id = "DOC_TRACK", status = "PENDING", detail = "Authored hold point offscreen in this Editor fixture; tracking requires Game inspection." });
            var overlay = All<Hand3DThumbOverlay>().Single();
            Check(overlay.gameObject.activeInHierarchy && overlay.ThumbImage.texture == armImage.texture && overlay.transform.GetSiblingIndex() > visual.GetSiblingIndex() && visual.GetSiblingIndex() > armImage.transform.GetSiblingIndex(), "Thumb texture/order/activation invalid");
            overlay.RefreshMask(); using (var mesh = new VertexHelper()) { Call(overlay, "OnPopulateMesh", mesh); Check(mesh.currentVertCount > 0, "Thumb generated empty geometry"); }
            Call(input, "OnApplicationFocus", false);
            Check(!input.IsArmModeActive && !documents.IsHolding && Get<bool>(input, "waitForRelease"), "Focus loss did not cancel/wait for release");
            Call(display, "Tick", 0f); Check(display.IsReleasing, "Document release skipped transition");
            Call(display, "Tick", 1f); Call(rig, "UpdateRig", 1f);
            Check(paper.GetComponent<Renderer>().enabled && paper.GetComponent<Collider>().enabled && paper.transform.position == worldPosition && !visual.gameObject.activeSelf, "Document return failed");
            Call(input, "OnApplicationFocus", true); ActiveInput(input, true); Call(documents, "Tick", Step);
            Check(documents.IsHolding, "Document reacquisition failed");
            arm.ToggleMode(); Call(documents, "Tick", Step);
            Check(!documents.IsHolding && !pose.IsHandHolding, "HandEdit did not cancel document");
            ActiveInput(input, true); Call(input, "OnApplicationPause", true);
            Check(!input.IsArmModeActive && Get<bool>(input, "waitForRelease"), "Application pause did not cancel");
            Check(rig.GetComponent<Hand3DFingerState>().Current == (FlexedFingers)3, "Lifecycle changed logical fingers");
        }
    }
}
