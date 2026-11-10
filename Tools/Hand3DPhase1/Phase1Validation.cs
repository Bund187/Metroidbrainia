using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Metroidbrainia.Hand3DExperiment;
using Metroidbrainia.Hand3DExperiment.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Metroidbrainia
{
    // Editor-only, staged in a marked copy. Before detectors are verbatim snapshots with renamed types and counters.
    public static class Phase1Validation
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const float Step = 1f / 60f;
        private static int checks;
        private static int beforeRays { get => Phase1Counters.raycasts; set => Phase1Counters.raycasts = value; }
        private static int beforeHands { get => Phase1Counters.handProjections; set => Phase1Counters.handProjections = value; }
        private static readonly List<Row> rows = new List<Row>();
        [Serializable] private sealed class Observation { public string outcome; public int raycasts, handProjections; }
        [Serializable] private sealed class Row { public string name, status, detail; public Observation before, after; }
        [Serializable] private sealed class Report { public string runId, unity, scope; public int assertions; public Row[] cases; }
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(Flags)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length && m.GetParameters().Select((p, i) => p.ParameterType.IsInstanceOfType(args[i])).All(v => v)).Invoke(target, args);
        private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        private static T[] All<T>() where T : Component => UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static void Guard() { if (!File.Exists("phase0-isolated-copy.marker")) throw new InvalidOperationException("Isolated project required."); }
        public static RaycastHit[] BeforeRaycastAll(Vector3 origin, Vector3 direction, float distance, int layers, QueryTriggerInteraction triggers)
        { beforeRays++; return Physics.RaycastAll(origin, direction, distance, layers, triggers); }
        public static bool BeforeHandProjection(Camera camera, RawImage image, Vector3 world, out Vector2 screen, out string reason)
        { beforeHands++; return Hand3DScreenProjection.TryWorldToScreen(camera, image, world, out screen, out reason); }

        public static void Migrate()
        {
            Guard();
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/TestArmScene.unity", OpenSceneMode.Single);
                var document = All<Hand3DDocumentInteractor>().Single();
                Hand3DDocumentPreparation.ConfigureContact(document, document.GetComponent<Hand3DArmMovement>(), document.GetComponent<Hand3DDoorInteractor>());
                var data = new SerializedObject(document).FindProperty("detection");
                var source = new SerializedObject(document.GetComponent<Hand3DDoorInteractor>());
                foreach (string field in new[] { "worldCamera", "handGripPoint", "armDisplay", "worldDisplay" })
                    Check(data.FindPropertyRelative(field).objectReferenceValue == source.FindProperty(field).objectReferenceValue, "Reference migration: " + field);
                Check(data.FindPropertyRelative("gripScreenOffset").vector2Value == source.FindProperty("gripScreenOffset").vector2Value, "Offset migration");
                Check(data.FindPropertyRelative("interactionLayers").intValue == source.FindProperty("interactionLayers").intValue, "Layer migration");
                Check(EditorSceneManager.SaveScene(scene, "Assets/Phase1Migrated.unity", true), "Cannot export migration copy");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void Run()
        {
            Guard();
            foreach (string name in new[] {
                "door_without_documents", "door_slow", "door_sweep", "door_far", "door_wall", "door_own_frame", "door_outside_zone", "door_no_knob", "door_edge", "door_clipped",
                "door_multiple", "door_fallback", "door_latched", "door_cancel", "door_locked", "door_dynamic", "priority",
                "document", "document_slow", "document_sweep", "document_far", "document_wall", "document_edge", "document_clipped", "document_multiple", "document_latched_release", "document_pause",
                "document_door_disabled", "document_door_removed" })
            {
                try
                {
                    var before = Scenario(name, true); var after = Scenario(name, false);
                    bool independence = name == "document_door_disabled" || name == "document_door_removed";
                    Check(independence ? before.outcome == "False" && after.outcome == "True" : before.outcome == after.outcome, name + " changed outcome: " + before.outcome + " -> " + after.outcome);
                    if (name == "door_far" || name == "door_outside_zone" || name == "door_no_knob" || name == "document_far")
                        Check(before.raycasts > 0 && after.raycasts == 0, name + " did not avoid rejected-candidate raycasts");
                    if (name == "door_multiple" || name == "door_fallback") Check(after.handProjections == 1 && before.handProjections > 1, "Hand was projected repeatedly");
                    rows.Add(new Row { name = name, status = "PASS", detail = independence ? "Intentional independence fix; old detector rejects, new detector acquires." : "Same outcome as original detectors.", before = before, after = after });
                }
                catch (Exception error) { rows.Add(new Row { name = name, status = "FAIL", detail = error.ToString() }); Debug.LogError("PHASE1 " + name + " " + error); }
            }
            try { ProjectionAndSweep(); rows.Add(new Row { name = "projection_resolutions_sweep", status = "PASS", detail = "720/1080/1440 reference-pixel math and sweep history sizes; camera projection at actual Editor display size. No Game resolution switch." }); }
            catch (Exception error) { rows.Add(new Row { name = "projection_resolutions_sweep", status = "FAIL", detail = error.ToString() }); }
            string[] args = Environment.GetCommandLineArgs(); string id = args[Array.IndexOf(args, "-phase1RunId") + 1];
            File.WriteAllText("phase1-results.json", JsonUtility.ToJson(new Report { runId = id, unity = Application.unityVersion, scope = "Current and original detectors in isolated Editor scenes. Simulated effective input/targets. No physical-device or final Game visual validation.", assertions = checks, cases = rows.ToArray() }, true));
            EditorApplication.Exit(rows.Any(r => r.status == "FAIL") ? 1 : 0);
        }

        private sealed class Fixture
        {
            public bool before;
            public MonoBehaviour door, document;
            public Hand3DArmMovement movement;
            public Hand3DRig rig;
            public Hand3DInputController input;
            public Hand3DDocumentDisplay display;
            public Camera camera;
            public RawImage armImage;
            public Transform marker;
            public Vector2 center;
            public float doorRadius, documentRadius;
            public Hand3DContactDetection Detection(bool doc) => Get<Hand3DContactDetection>(doc ? document : door, "detection");
            public void Tick(bool doc, float dt = Step) => Call(doc ? document : door, "Tick", dt);
            public bool Holding(bool doc) => doc ? Get<Hand3DDocumentBinding>(document, "active") != null : Get<bool>(door, "grabbing");
            public void Move(Vector2 screen)
            {
                Rect rect = armImage.rectTransform.rect;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(armImage.rectTransform, screen, null, out Vector2 local);
                Vector2 uv = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
                uv = armImage.uvRect.position + Vector2.Scale(uv, armImage.uvRect.size);
                marker.position = movement.ArmCamera.ViewportToWorldPoint(new Vector3(uv.x, uv.y, .7f));
            }
            public void Settle() => Call(rig, "UpdateRig", 1f);
            public void ResetCounts() { beforeRays = beforeHands = 0; if (!before) { if (door != null) Detection(false).ResetCounters(); Detection(true).ResetCounters(); } }
        }

        private static void CopySerialized(object source, object destination)
        {
            foreach (var field in destination.GetType().GetFields(Flags))
            {
                if (!field.IsDefined(typeof(SerializeField), true)) continue;
                var original = source.GetType().GetField(field.Name, Flags);
                if (original == null) continue;
                object value = original.GetValue(source);
                if (value == null || field.FieldType.IsInstanceOfType(value)) field.SetValue(destination, value);
            }
        }

        private static Fixture Create(bool before)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/TestArmScene.unity", OpenSceneMode.Single);
            var rig = All<Hand3DRig>().Single(); var owner = rig.gameObject; var movement = owner.GetComponent<Hand3DArmMovement>();
            Check(movement.Initialize(), "Movement initialization");
            var recognizer = owner.GetComponent<Hand3DPoseRecognizer>(); Call(recognizer, "OnDisable"); Call(recognizer, "OnEnable");
            Check(rig.Initialize(), "Rig initialization");
            foreach (var collider in All<Collider>()) collider.enabled = false;
            foreach (var binding in All<Hand3DDoorBinding>()) { binding.enabled = false; binding.Door.enabled = false; }
            foreach (var binding in All<Hand3DDocumentBinding>()) binding.enabled = false;
            var mainDoor = owner.GetComponent<Hand3DDoorInteractor>(); var mainDoc = owner.GetComponent<Hand3DDocumentInteractor>();
            Call(mainDoor, "Awake"); Call(mainDoor, "Start"); Call(mainDoc, "Awake");
            MonoBehaviour door = mainDoor, document = mainDoc;
            if (before)
            {
                mainDoor.enabled = mainDoc.enabled = false;
                door = owner.AddComponent<BeforeDoorInteractor>(); CopySerialized(mainDoor, door); Call(door, "Awake"); Call(door, "Start");
                document = owner.AddComponent<BeforeDocumentInteractor>(); CopySerialized(mainDoc, document); Set(document, "contact", door); Call(document, "Awake");
            }
            Set(door, "debugContact", false); Set(door, "logInteractions", false);
            var input = owner.GetComponent<Hand3DInputController>(); Set(input, "<IsArmModeActive>k__BackingField", true);
            movement.BeginControl();
            var state = owner.GetComponent<Hand3DFingerState>(); for (int i = 0; i < 5; i++) state.SetFlexed((Finger)i, (3 & (1 << i)) != 0);
            Call(rig, "RequestTransition"); Call(rig, "UpdateRig", 1f);
            var fixture = new Fixture { before = before, door = door, document = document, movement = movement, rig = rig, input = input, display = owner.GetComponent<Hand3DDocumentDisplay>(),
                camera = Get<Camera>(door, "worldCamera"), armImage = Get<RawImage>(door, "armDisplay"), marker = Get<Transform>(door, "handGripPoint"), center = new Vector2(Screen.width * .5f, Screen.height * .5f),
                doorRadius = Get<float>(door, "screenContactRadius") * Screen.height / 1080f, documentRadius = Get<float>(document, "screenContactRadius") * Screen.height / 1080f };
            fixture.Move(fixture.center); fixture.ResetCounts(); return fixture;
        }

        private static DoorHandInteractable Door(Fixture f, string name, Vector2 screen, float depth, bool locked = false, bool bind = true)
        {
            var root = new GameObject(name + " root").transform;
            var hinge = new GameObject(name).transform; hinge.SetParent(root, false);
            var knob = new GameObject("Knob").transform; knob.SetParent(hinge, false); knob.localPosition = Vector3.right * .2f;
            hinge.position = f.camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth)) - knob.localPosition;
            var collider = knob.gameObject.AddComponent<SphereCollider>(); collider.isTrigger = true; collider.radius = .015f;
            var point = new GameObject("InteractionPoint").transform; point.SetParent(knob, false);
            var engine = hinge.gameObject.AddComponent<DoorHandInteractable>();
            Set(engine, "doorHinge", hinge); Set(engine, "knob", knob); Set(engine, "interactionPoint", point); Set(engine, "canOpenByHand", !locked); Call(engine, "Awake");
            if (bind) hinge.gameObject.AddComponent<Hand3DDoorBinding>().Configure(engine, root);
            return engine;
        }

        private static Hand3DDocumentBinding Document(Fixture f, string name, Vector2 screen, float depth)
        {
            var sprite = Get<Sprite>(All<Hand3DDocumentBinding>().First(), "heldSprite");
            var paper = GameObject.CreatePrimitive(PrimitiveType.Quad); paper.name = name; paper.transform.localScale = Vector3.one * .02f;
            paper.transform.position = f.camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            var original = paper.AddComponent<DocumentHandInteractable>(); var binding = paper.AddComponent<Hand3DDocumentBinding>();
            Set(binding, "document", original); Set(binding, "worldRenderer", paper.GetComponent<MeshRenderer>()); Set(binding, "worldCollider", paper.GetComponent<Collider>());
            Set(binding, "heldSprite", sprite); Set(binding, "localizedText", new LocalizedString()); Call(binding, "OnEnable"); return binding;
        }

        private static Observation Scenario(string name, bool before)
        {
            var f = Create(before); bool doc = name.StartsWith("document"); float radius = doc ? f.documentRadius : f.doorRadius;
            Vector2 center = f.center; bool edge = name.EndsWith("_edge"), clipped = name.EndsWith("_clipped");
            if (edge) { center.x = 2f; f.Move(center); }
            Vector2 target = clipped ? new Vector2(-2f, center.y) : center;
            if (name == "door_outside_zone") target += Vector2.right * radius * 3f;
            DoorHandInteractable door = null; Hand3DDocumentBinding paper = null;
            if (doc) paper = Document(f, "near", target, name.EndsWith("_far") ? 1.2f : .7f);
            else door = Door(f, "near", target, name.EndsWith("_far") ? 2f : .7f, name == "door_locked", name != "door_dynamic");
            if (name.EndsWith("_multiple"))
            {
                if (doc) Document(f, "far", center + Vector2.right * radius * .8f, .7f);
                else Door(f, "far", center + Vector2.right * radius * .8f, .7f);
            }
            if (name == "door_fallback") { Set(door, "maxInteractionDistance", .2f); Door(f, "fallback", center + Vector2.right * radius * .4f, .7f); }
            if (name == "door_no_knob") Get<Transform>(door, "knob").GetComponent<Collider>().enabled = false;
            if (name.EndsWith("_wall") || name == "door_own_frame")
            {
                Vector3 point = doc ? paper.ContactPosition : door.InteractionPoint.position;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = Vector3.Lerp(f.camera.transform.position, point, .5f); wall.transform.localScale = Vector3.one * .1f;
                if (name == "door_own_frame") wall.transform.SetParent(door.transform.parent, true);
            }
            if (name == "priority") paper = Document(f, "paper", center, .7f);
            if (name == "door_without_documents") f.document.enabled = false;
            if (name == "document_door_disabled") { f.door.enabled = false; Call(f.door, "OnDisable"); }
            if (name == "document_door_removed") Object.DestroyImmediate(f.door);
            if (name.EndsWith("_slow") || name.EndsWith("_sweep")) f.Move(center - Vector2.right * radius * 2f);
            Physics.SyncTransforms(); f.ResetCounts();
            f.Tick(doc, name == "door_dynamic" ? 1.1f : Step);
            if (name.EndsWith("_slow") || name.EndsWith("_sweep"))
            { Check(!f.Holding(doc), "Outside initial endpoint acquired"); f.Move(center + Vector2.right * radius * (name.EndsWith("_slow") ? -.8f : 2f)); f.Tick(doc); }
            string outcome;
            if (name == "priority") { f.Tick(true); outcome = f.Holding(false) + "/" + f.Holding(true); Check(outcome == "True/False", "Door priority changed"); }
            else if (name.EndsWith("_multiple") || name == "door_fallback")
            {
                var active = doc ? (Object)Get<Hand3DDocumentBinding>(f.document, "active") : Get<DoorHandInteractable>(f.door, "activeDoor");
                outcome = active != null ? active.name : "none"; Check(outcome == (name == "door_fallback" ? "fallback" : "near"), "Wrong candidate");
            }
            else if (name == "door_locked")
            {
                Check(f.Holding(false) && Get<bool>(door, "lockedInteraction"), "Locked door did not acquire");
                Set(door, "shaking", false); Call(f.door, "ProcessMovement", Vector2.zero, true); f.Tick(false);
                Check(!f.Holding(false), "Locked attempt repeated without release");
                Call(f.door, "EndArmSession"); f.Settle(); f.Move(center); f.Tick(false);
                outcome = f.Holding(false).ToString(); Check(outcome == "True", "Locked retry after release failed");
            }
            else if (name == "door_latched")
            { Check(f.Holding(false), "Latch fixture did not acquire"); f.Move(center + Vector2.right * radius * 4f); f.Tick(false); outcome = f.Holding(false).ToString(); Check(outcome == "True", "Visual distance cancelled door"); }
            else if (name == "door_cancel")
            {
                Check(f.Holding(false), "Cancel fixture did not acquire"); f.camera.transform.position -= f.camera.transform.forward * 2f; f.Tick(false); f.Settle();
                outcome = f.Holding(false).ToString(); Check(outcome == "False" && f.rig.GetComponent<Hand3DFingerState>().Current == (FlexedFingers)3, "Reach cancellation/restoration failed");
            }
            else if (name == "document_latched_release")
            {
                Check(f.Holding(true), "Document latch fixture did not acquire"); Vector3 position = paper.transform.position;
                f.camera.transform.position -= f.camera.transform.forward * 2f; f.Tick(true); Check(f.Holding(true), "Document rechecked reach");
                Call(f.document, "Cancel"); Call(f.display, "Tick", 1f); f.Settle();
                outcome = f.Holding(true).ToString(); Check(outcome == "False" && paper.GetComponent<Renderer>().enabled && paper.GetComponent<Collider>().enabled && paper.transform.position == position, "Document return changed");
                Check(f.rig.GetComponent<Hand3DFingerState>().Current == (FlexedFingers)3, "Document changed logical gesture");
            }
            else if (name == "document_pause")
            { Check(f.Holding(true), "Pause fixture did not acquire"); Set(f.input, "<IsArmModeActive>k__BackingField", false); f.Tick(true, 0f); outcome = f.Holding(true).ToString(); Check(outcome == "False", "Paused arm release changed"); }
            else outcome = f.Holding(doc).ToString();
            if (name.EndsWith("_far") || name.EndsWith("_wall") || clipped || name == "door_outside_zone" || name == "door_no_knob") Check(outcome == "False", name + " acquired");
            else if (name.EndsWith("_edge") || name.EndsWith("_slow") || name.EndsWith("_sweep") || name == "document" || name == "door_without_documents" || name == "door_dynamic" || name == "door_own_frame") Check(outcome == "True", name + " rejected");
            var counts = before ? null : f.Detection(doc);
            return new Observation { outcome = outcome, raycasts = before ? beforeRays : counts.ObstacleQueries, handProjections = before ? beforeHands : counts.HandProjections };
        }

        private static void ProjectionAndSweep()
        {
            var f = Create(false); var query = f.Detection(true);
            foreach (int height in new[] { 720, 1080, 1440 })
            {
                int width = height * 16 / 9;
                foreach (float radius in new[] { 55f, 100f }) Check(Mathf.Abs(Hand3DContactDetection.ReferencePixels(radius, height) - radius * height / 1080f) < .0001f, "Reference radius changed");
                var point = new Vector2(f.camera.pixelRect.xMin + 2f, f.camera.pixelRect.center.y); var world = f.camera.ScreenToWorldPoint(new Vector3(point.x, point.y, .7f));
                bool visible = query.TryWorldToScreen(world, out Vector2 projected, out string projectionReason);
                Check(visible && Vector2.Distance(projected, point) < .25f, $"Camera edge projection failed at {height}: {point} -> {projected}, visible={visible}, reason={projectionReason}");
                var hand = new Hand3DContactDetection.HandSample { visible = true, point = point - Vector2.right * 100f };
                var sweep = new Hand3DContactDetection.Sweep(); sweep.Advance(hand, new Vector2(width, height));
                hand.point = point + Vector2.right * 100f; sweep.Advance(hand, new Vector2(width, height));
                var contact = query.Evaluate(hand, sweep, world, world, 55f, true);
                Check(contact.UsedSweep && contact.WithinReach(.9f), "Swept contact changed");
                sweep.Advance(hand, new Vector2(width + 1, height)); Check(!sweep.Available, "Resize retained stale sweep");
                hand.visible = false; sweep.Advance(hand, new Vector2(width + 1, height)); Check(!sweep.Available, "Invisible hand retained sweep");
                hand.visible = true; sweep.Advance(hand, new Vector2(width + 1, height)); Check(!sweep.Available, "Invisible-to-visible sweep bridged frames");
            }
            Check(Attribute.GetCustomAttribute(typeof(Hand3DDoorInteractor), typeof(DefaultExecutionOrder)) is DefaultExecutionOrder door && door.order == 220, "Door order changed");
            Check(Attribute.GetCustomAttribute(typeof(Hand3DDocumentInteractor), typeof(DefaultExecutionOrder)) is DefaultExecutionOrder document && document.order == 225, "Document order changed");
        }
    }
}
