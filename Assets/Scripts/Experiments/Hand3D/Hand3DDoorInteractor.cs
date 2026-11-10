using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Metroidbrainia.Hand3DExperiment
{
    public struct Hand3DDoorDebug
    {
        public Vector2 handScreen, handleScreen;
        public bool handVisible, handleVisible, withinReach, obstacle, validCandidate;
        public float pixelDistance, playerDistance;
        public float sweptDistance, radius;
        public bool withinContact, usedSweep, knobReady;
        public string doorName, reason, blocker;
    }

    [DefaultExecutionOrder(220)]
    [DisallowMultipleComponent]
    public sealed class Hand3DDoorInteractor : MonoBehaviour, IHand3DInteractionSession
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform handGripPoint;
        [SerializeField] private Hand3DArmMovement armMovement;
        [SerializeField] private Hand3DInputController input;
        [SerializeField] private Hand3DInteractionPose grabPose;
        [Tooltip("RawImage displaying PixelArmCamera's RenderTexture. Found by texture if not assigned.")]
        [SerializeField] private RawImage armDisplay;
        [Tooltip("Only needed if MainCamera also renders to a RawImage instead of directly to Game.")]
        [SerializeField] private RawImage worldDisplay;
        [SerializeField, HideInInspector] private DoorHandInteractable testDoor;
        [Tooltip("Root of the selected door instance, including its frame. Never assign the room or mansion root.")]
        [SerializeField, HideInInspector] private Transform targetDoorRoot;
        [Tooltip("Scene-local discovery for new doors without a binding. Prepared bindings register immediately.")]
        [SerializeField, Min(0.1f)] private float doorRefreshInterval = 1f;
        // Retained for serialized compatibility; acquisition and retention no longer use these legacy limits.
        [SerializeField, HideInInspector] private float contactTolerancePixels = 18f;
        [SerializeField, HideInInspector] private float maximumContactSeparationPixels = 80f;
        [Tooltip("Contact radius in final-image pixels at 1080p, scaled by the current Game height.")]
        [SerializeField, Min(1f)] private float screenContactRadius = 55f;
        [Tooltip("Perceived palm contact offset in 1080p pixels. Positive Y moves the marker up. Also used by grip alignment.")]
        [SerializeField] private Vector2 gripScreenOffset;
        [SerializeField, Min(0.01f)] private float maximumReach = 1.8f;
        [SerializeField, Min(0f)] private float gripSnapDuration = 0.08f;
        [Tooltip("Include independent walls and obstacles. Player and ViewModel hierarchy are ignored.")]
        [SerializeField] private LayerMask interactionLayers = Physics.DefaultRaycastLayers;
        [SerializeField] private bool debugContact;
        [SerializeField] private bool logInteractions;
        private DoorHandInteractable activeDoor, feedbackDoor;
        private readonly List<Hand3DDoorBinding> doors = new List<Hand3DDoorBinding>();
        private readonly HashSet<DoorHandInteractable> rejectedThisFrame = new HashSet<DoorHandInteractable>();
        private Hand3DDoorBinding activeBinding;
        private DoorHandInteractable selectedCandidate;
        private float discoveryElapsed;
        private bool attemptedThisHold, wasArmMode;
        private Vector3 snapFrom;
        private float snapElapsed, gripViewDepth;
        private FirstPersonArmController legacyController;
        private Hand3DInteractionCoordinator coordinator;
        private Hand3DDoorDebug diagnostics;
        private Hand3DContactDetection.Sweep sweep;
        private readonly Hand3DContactDetection detection = new Hand3DContactDetection();
        public bool IsGrabbing => coordinator != null && coordinator.Owns(this);
        public Camera WorldCamera => worldCamera;
        public Transform HandGripPoint => handGripPoint;
        public Hand3DDoorDebug DebugInfo => diagnostics;
        public float ScreenContactRadiusPixels => Hand3DContactDetection.ReferencePixels(screenContactRadius, Screen.height);
        public int CompatibleDoorCount => doors.Count;
        public bool TryGetWorldScreen(Vector3 position, out Vector2 screen, out string reason) =>
            detection.TryWorldToScreen(position, out screen, out reason);

        private void Awake()
        {
            if (!ExperimentScope.IsAllowed(this)) return;
            if (input == null) input = GetComponent<Hand3DInputController>();
            legacyController = input != null && input.PlayerRoot != null ? input.PlayerRoot.GetComponent<FirstPersonArmController>() : null;
            if (legacyController != null) legacyController.enabled = false;
        }

        private void Start()
        {
            coordinator = GetComponent<Hand3DInteractionCoordinator>();
            if (coordinator != null) { coordinator.ArmControlEnded -= EndArmSession; coordinator.ArmControlEnded += EndArmSession; }
            if (armMovement == null) armMovement = GetComponent<Hand3DArmMovement>();
            if (input == null) input = GetComponent<Hand3DInputController>();
            if (grabPose == null) grabPose = GetComponent<Hand3DInteractionPose>();
            if (!ExperimentScope.IsAllowed(this) || armMovement == null || input == null || grabPose == null
                || worldCamera == null || worldCamera.gameObject.scene != gameObject.scene || handGripPoint == null
                || handGripPoint.parent != armMovement.Hand || armMovement.gameObject != gameObject
                || grabPose.gameObject != gameObject || input.gameObject != gameObject)
            {
                diagnostics.reason = "Referencias de interacción incompletas o de otra escena";
                Debug.LogError("[Hand3D Door] Assign this scene's world camera, arm/input/Grab and HandGripPoint directly under the wrist.", this);
                enabled = false;
                return;
            }
            ResolveDisplays();
            RefreshDoors();
        }

        private void ResolveDisplays()
        {
            if (armDisplay == null) armDisplay = Hand3DScreenProjection.FindDisplay(armMovement.ArmCamera);
            if (worldDisplay == null) worldDisplay = Hand3DScreenProjection.FindDisplay(worldCamera);
            detection.Configure(worldCamera, handGripPoint, armDisplay, worldDisplay, gripScreenOffset, interactionLayers);
            detection.Bind(armMovement, input, transform);
        }

        public void RefreshDoors()
        {
            if (!ExperimentScope.IsAllowed(this)) return;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (DoorHandInteractable door in root.GetComponentsInChildren<DoorHandInteractable>(true))
                {
                    var binding = door.GetComponent<Hand3DDoorBinding>();
                    if (binding == null) binding = door.gameObject.AddComponent<Hand3DDoorBinding>();
                    binding.Configure(door, !binding.HasExplicitRoot && door == testDoor ? targetDoorRoot : null);
                }
            discoveryElapsed = 0f;
            Hand3DDoorBinding.Collect(gameObject.scene, doors);
        }

        private Hand3DDoorBinding BindingFor(DoorHandInteractable door) => activeDoor == door && activeBinding != null
            ? activeBinding : door.GetComponent<Hand3DDoorBinding>();

        private Vector3 GripPosition(DoorHandInteractable door)
        {
            var binding = BindingFor(door);
            return binding != null ? binding.GripPosition : door.InteractionPoint.position;
        }

        public bool TryGetHandScreen(out Vector2 point, out string reason)
        {
            ResolveDisplays();
            var hand = detection.SampleHand(); point = hand.point; reason = hand.reason;
            return hand.visible;
        }

        public static float DistanceToScreenSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            return Hand3DContactDetection.DistanceToScreenSegment(point, start, end);
        }

        private Collider KnobCollider(DoorHandInteractable door)
        {
            for (Transform current = door.InteractionPoint; current != null; current = current.parent)
            {
                Collider collider = current.GetComponent<Collider>();
                if (collider != null && collider.isTrigger && collider.enabled && collider.gameObject.activeInHierarchy
                    && collider.GetComponentInParent<DoorHandInteractable>() == door) return collider;
                if (current == door.transform) break;
            }
            return null;
        }

        private bool Evaluate(DoorHandInteractable door, bool acquiring, Hand3DContactDetection.HandSample hand, ref bool synchronized, out Hand3DDoorDebug result)
        {
            result = new Hand3DDoorDebug { pixelDistance = float.PositiveInfinity, playerDistance = float.PositiveInfinity,
                doorName = door != null ? (door.transform.parent != null ? door.transform.parent.name + "/" : "") + door.name : "Ninguna" };
            result.handVisible = hand.visible; result.handScreen = hand.point;
            if (door == null || door.InteractionPoint == null || door.gameObject.scene != gameObject.scene)
            { result.reason = "No hay puerta/pomo candidato en TestArmScene"; return false; }
            if (!door.isActiveAndEnabled) { result.reason = "DoorHandInteractable deshabilitado"; return false; }
            if (door.IsOpen || door.IsAutoOpening) { result.reason = "Puerta abierta o en apertura automática"; return false; }
            var contact = detection.Evaluate(hand, sweep, GripPosition(door), door.InteractionPoint.position, screenContactRadius, acquiring);
            result.handleVisible = contact.visible; result.handleScreen = contact.point;
            result.playerDistance = contact.distance; result.withinReach = contact.WithinReach(maximumReach);
            result.pixelDistance = contact.separation; result.radius = contact.radius; result.sweptDistance = contact.sweptDistance;
            result.usedSweep = contact.UsedSweep; result.withinContact = contact.WithinContact;
            result.knobReady = KnobCollider(door) != null;
            Collider blocker = null;
            if ((!acquiring || result.handVisible && result.withinContact) && result.handleVisible && result.withinReach && result.knobReady)
                result.obstacle = FindObstacle(door, ref synchronized, out blocker);
            result.blocker = blocker != null ? blocker.name + " (capa " + blocker.gameObject.layer + ")" : "Ninguno";
            if (acquiring && !result.handVisible) result.reason = "Mano: " + hand.reason;
            else if (!result.handleVisible) result.reason = "Pomo: " + contact.reason;
            else if (!result.withinReach) result.reason = $"Fuera de alcance: {result.playerDistance:F3} > {maximumReach:F2} m";
            else if (result.obstacle) result.reason = "Obstáculo independiente: " + result.blocker;
            else if (!result.knobReady) result.reason = "Pomo sin trigger Collider habilitado";
            else if (acquiring && !result.withinContact) result.reason = $"Fuera de zona visual: recorrido {result.sweptDistance:F1} > radio {result.radius:F1} px";
            else { result.validCandidate = true; result.reason = acquiring
                ? result.usedSweep ? "Entrada detectada durante el recorrido entre fotogramas" : "Dentro de zona: contacto y alcance válidos"
                : "Grab fijado: la distancia visual ya no cancela el agarre"; }
            return result.validCandidate;
        }

        private bool FindObstacle(DoorHandInteractable door, ref bool synchronized, out Collider blocker)
        {
            var binding = BindingFor(door);
            return detection.HasObstacle(door.InteractionPoint.position,
                binding != null ? binding.DoorRoot : door.transform, ref synchronized, out blocker);
        }

        public bool HasWorldObstacle(Vector3 point, Transform targetRoot, out Collider blocker)
        {
            ResolveDisplays(); bool synchronized = false;
            return detection.HasObstacle(point, targetRoot, ref synchronized, out blocker);
        }

        public bool IsReachableAndVisible(DoorHandInteractable door)
        {
            if (door == null || door.InteractionPoint == null || worldCamera == null || !door.isActiveAndEnabled) return false;
            ResolveDisplays(); bool synchronized = false;
            return Vector3.Distance(worldCamera.transform.position, door.InteractionPoint.position) <= maximumReach
                && Hand3DScreenProjection.TryWorldToScreen(worldCamera, worldDisplay, door.InteractionPoint.position, out _, out _)
                && !FindObstacle(door, ref synchronized, out _);
        }

        public bool ProcessMovement(Vector2 integratedInput, bool usingMouse)
        {
            if (!isActiveAndEnabled || !IsGrabbing) return false;
            if (!CanContinue()) { Cancel(); return true; }
            if (!activeDoor.UpdateInteraction(worldCamera, armMovement.GetScreenDisplacement(integratedInput, usingMouse, worldCamera)))
            {
                diagnostics.reason = "DoorHandInteractable finalizó UpdateInteraction (incluye fin de intento bloqueado)";
                attemptedThisHold = true;
                Cancel();
                return true;
            }
            if (activeDoor.IsAutoOpening || activeDoor.IsOpen)
            {
                diagnostics.reason = "Umbral alcanzado: apertura automática, Grab liberado";
                attemptedThisHold = true;
                if (logInteractions) Debug.Log("[Hand3D Door] Automatic opening started; releasing Grab.", activeDoor);
                Cancel();
            }
            else FollowHandle();
            return true;
        }

        private bool CanContinue()
        {
            ResolveDisplays();
            return CanContinue(detection.SampleHand());
        }

        private bool CanContinue(Hand3DContactDetection.HandSample hand)
        {
            if (activeDoor == null) { diagnostics.reason = "Puerta/pomo eliminado"; diagnostics.validCandidate = false; return false; }
            if (!input.IsArmModeActive || armMovement.Modes.IsEditing || armMovement.Modes.IsTransitioning
                || !armMovement.isActiveAndEnabled || !grabPose.isActiveAndEnabled)
            { diagnostics.reason = "Modo de brazo, movimiento o capa Grab desactivados"; diagnostics.validCandidate = false; return false; }
            bool synchronized = false;
            return Evaluate(activeDoor, false, hand, ref synchronized, out diagnostics);
        }

        private void LateUpdate() => Tick(Time.deltaTime);

        private void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) { sweep.Reset(); return; }
            if (worldCamera == null || armMovement == null || handGripPoint == null || input == null || grabPose == null)
            { diagnostics.reason = "Faltan referencias de interacción"; EndArmSession(); return; }
            ResolveDisplays();
            discoveryElapsed += deltaTime;
            if (discoveryElapsed >= doorRefreshInterval) RefreshDoors();
            else Hand3DDoorBinding.Collect(gameObject.scene, doors);
            bool armMode = input.IsArmModeActive && !armMovement.Modes.IsEditing && !armMovement.Modes.IsTransitioning;
            if (!armMode)
            {
                EndArmSession();
                if (debugContact) { bool synchronized = false; SelectCandidate(detection.SampleHand(), ref synchronized, out _); }
                diagnostics.reason = armMovement.Modes.IsEditing ? "HandEdit: interacción desactivada"
                    : armMovement.Modes.IsTransitioning ? "Transición de modo" : "Mantén R2/RMB en Exploration";
                diagnostics.validCandidate = false;
                return;
            }
            if (!wasArmMode) { attemptedThisHold = false; sweep.Reset(); RefreshDoors(); }
            wasArmMode = true;
            var hand = detection.SampleHand();
            sweep.Advance(hand, new Vector2(Screen.width, Screen.height));
            if (IsGrabbing)
            {
                if (!CanContinue(hand)) Cancel();
                else FollowHandle(deltaTime);
                return;
            }
            if (attemptedThisHold)
            { if (debugContact) { bool synchronized = false; SelectCandidate(hand, ref synchronized, out _); } diagnostics.validCandidate = false; diagnostics.reason = "Intento finalizado: suelta R2/RMB para reintentar"; return; }
            if (coordinator != null && !coordinator.CanAcquire(this))
            { diagnostics.reason = "Otra interacción ocupa la mano"; diagnostics.validCandidate = false; return; }
            rejectedThisFrame.Clear();
            bool physicsSynchronized = false;
            DoorHandInteractable candidate = SelectCandidate(hand, ref physicsSynchronized, out Collider knob);
            Hand3DDoorDebug lastRefusal = default;
            bool refused = false;
            while (candidate != null && !candidate.TryBeginInteraction(worldCamera, knob))
            {
                lastRefusal = diagnostics;
                lastRefusal.validCandidate = false;
                lastRefusal.reason = "DoorHandInteractable.TryBeginInteraction devolvió false (estado o alcance de esta instancia)";
                refused = true;
                rejectedThisFrame.Add(candidate);
                candidate = SelectCandidate(hand, ref physicsSynchronized, out knob);
            }
            if (candidate == null) { if (refused) diagnostics = lastRefusal; return; }
            var binding = BindingFor(candidate);
            if (coordinator == null || !coordinator.TryAcquire(this, Hand3DInteractionState.Door, true,
                binding != null ? binding.WristOffset : Quaternion.identity)) { candidate.EndInteraction(); return; }
            activeDoor = feedbackDoor = candidate;
            activeBinding = BindingFor(candidate);
            snapElapsed = 0f;
            snapFrom = handGripPoint.position;
            // Keep the ViewModel depth. The real door's depth must never drive the visual mesh into a wall.
            gripViewDepth = armMovement.ArmCamera.WorldToViewportPoint(handGripPoint.position).z;
            FollowHandle(deltaTime);
            if (!IsGrabbing) return;
            if (logInteractions) Debug.Log("[Hand3D Door] Projected contact and world reach accepted; Grab started.", activeDoor);
        }

        private DoorHandInteractable SelectCandidate(Hand3DContactDetection.HandSample hand, ref bool synchronized, out Collider knob)
        {
            knob = null;
            DoorHandInteractable selected = null;
            Hand3DDoorDebug bestContact = default;
            float bestRejected = float.PositiveInfinity;
            selectedCandidate = null;
            diagnostics = new Hand3DDoorDebug { reason = "No hay puertas candidatas", doorName = "Ninguna", pixelDistance = float.PositiveInfinity };
            diagnostics.handVisible = hand.visible; diagnostics.handScreen = hand.point;
            foreach (Hand3DDoorBinding binding in doors)
            {
                DoorHandInteractable door = binding.Door;
                if (rejectedThisFrame.Contains(door)) continue;
                bool valid = Evaluate(door, true, hand, ref synchronized, out Hand3DDoorDebug result);
                if (valid && (selected == null || BetterContact(result, bestContact)))
                { selected = door; bestContact = result; diagnostics = result; }
                else if (selected == null && result.sweptDistance <= bestRejected)
                { bestRejected = result.sweptDistance; diagnostics = result; }
            }
            if (selected != null) knob = KnobCollider(selected);
            selectedCandidate = selected;
            return selected;
        }

        private static bool BetterContact(Hand3DDoorDebug candidate, Hand3DDoorDebug current)
        {
            bool inside = candidate.pixelDistance <= candidate.radius;
            bool selectedInside = current.pixelDistance <= current.radius;
            if (inside != selectedInside) return inside;
            float distance = inside ? candidate.pixelDistance : candidate.sweptDistance;
            float selectedDistance = selectedInside ? current.pixelDistance : current.sweptDistance;
            if (Mathf.Abs(distance - selectedDistance) > 0.001f) return distance < selectedDistance;
            if (Mathf.Abs(candidate.pixelDistance - current.pixelDistance) > 0.001f)
                return candidate.pixelDistance < current.pixelDistance;
            if (Mathf.Abs(candidate.playerDistance - current.playerDistance) > 0.001f)
                return candidate.playerDistance < current.playerDistance;
            // Keep the first candidate when both contact scores are identical.
            return false;
        }

        private bool TryHandleViewTarget(out Vector3 target)
        {
            target = default;
            if (activeDoor == null) return false;
            if (!Hand3DScreenProjection.TryWorldToScreen(worldCamera, worldDisplay, GripPosition(activeDoor), out Vector2 screen, out string reason)
                || !Hand3DScreenProjection.TryScreenToWorld(armMovement.ArmCamera, armDisplay,
                    screen - gripScreenOffset * Screen.height / 1080f, gripViewDepth, out target, out reason))
            { diagnostics.reason = "No se puede alinear el agarre: " + reason; return false; }
            return true;
        }

        private void FollowHandle(float deltaTime = 0f)
        {
            if (!TryHandleViewTarget(out Vector3 target)) { Cancel(); return; }
            snapElapsed += deltaTime;
            float t = gripSnapDuration > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(snapElapsed / gripSnapDuration)) : 1f;
            armMovement.FollowContact(handGripPoint, Vector3.Lerp(snapFrom, target, t));
        }

        public void Cancel()
        {
            if (coordinator != null) coordinator.Release(this);
            sweep.Reset();
        }

        void IHand3DInteractionSession.ReleaseInteraction()
        {
            DoorHandInteractable previous = activeDoor;
            activeDoor = null;
            activeBinding = null;
            sweep.Reset();
            if (previous != null) previous.EndInteraction();
        }

        public void EndArmSession()
        {
            Cancel();
            if (feedbackDoor != null) feedbackDoor.EndInteraction();
            feedbackDoor = null;
            wasArmMode = false;
            attemptedThisHold = false;
            rejectedThisFrame.Clear();
        }

        private void OnDisable()
        {
            EndArmSession();
            if (coordinator != null) coordinator.ArmControlEnded -= EndArmSession;
        }
        private void OnEnable()
        {
            if (legacyController != null) legacyController.enabled = false;
            coordinator = GetComponent<Hand3DInteractionCoordinator>();
            if (coordinator != null) { coordinator.ArmControlEnded -= EndArmSession; coordinator.ArmControlEnded += EndArmSession; }
        }

        private static string State(bool value) => value ? "SI" : "NO";

        private static void DebugLine(Vector2 a, Vector2 b, Color color, float width = 1.5f)
        {
            a.y = Screen.height - a.y;
            b.y = Screen.height - b.y;
            Matrix4x4 matrix = GUI.matrix;
            Color previous = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, Vector2.Distance(a, b), width), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = previous;
        }

        private static void DebugCircle(Vector2 center, float radius, Color color)
        {
            const int segments = 48;
            Vector2 previous = center + Vector2.right * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                DebugLine(previous, next, color);
                previous = next;
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !debugContact || Event.current.type != EventType.Repaint) return;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            Hand3DDoorDebug d = diagnostics;
            bool held = input != null && input.IsArmButtonHeld;
            bool active = input != null && input.IsArmModeActive;
            bool exploration = armMovement != null && armMovement.Modes != null && !armMovement.Modes.IsEditing;
            bool transitioning = armMovement != null && armMovement.Modes != null && armMovement.Modes.IsTransitioning;
            float panelWidth = Mathf.Min(800f, Screen.width - 24f);
            GUI.Box(new Rect(12, 12, panelWidth, 264), GUIContent.none);
            string distance = float.IsInfinity(d.pixelDistance) ? "--" : d.pixelDistance.ToString("F1");
            string sweep = float.IsInfinity(d.sweptDistance) ? "--" : d.sweptDistance.ToString("F1");
            GUI.Label(new Rect(22, 20, panelWidth - 20f, 250), $"ADQUISICIÓN — {d.doorName}\n"
                + $"INPUT: R2/RMB mantenido {State(held)}, modo brazo activo {State(active)} | Exploration {State(exploration)}, transición {State(transitioning)}\n"
                + $"PROYECCIÓN: mano {State(d.handVisible)} {d.handScreen}, pomo {State(d.handleVisible)} {d.handleScreen}\n"
                + $"ZONA: {State(d.withinContact)} | distancia {distance} px, recorrido {sweep} px, radio {ScreenContactRadiusPixels:F1} px\n"
                + $"ALCANCE: {State(d.withinReach)} | jugador-pomo {d.playerDistance:F3} / {maximumReach:F2} m\n"
                + $"RAYCAST: {(d.obstacle ? "BLOQUEADO" : "LIBRE")} | collider: {d.blocker ?? "ninguno"}\n"
                + $"CAPAS: máscara obstáculos 0x{interactionLayers.value:X8} | trigger pomo habilitado {State(d.knobReady)}\n"
                + $"Candidato válido: {State(d.validCandidate)} | Grab fijado: {State(IsGrabbing)} | entrada por recorrido: {State(d.usedSweep)}\n"
                + $"Puertas compatibles: {doors.Count} | candidata: {(selectedCandidate != null ? selectedCandidate.transform.parent?.name + "/" + selectedCandidate.name : "ninguna")} | agarrada: {(activeDoor != null ? activeDoor.transform.parent?.name + "/" + activeDoor.name : "ninguna")}\n"
                + $"{(Time.timeScale <= 0f ? "PAUSA — " : "")}{d.reason}");
            if (d.handVisible)
            {
                DebugCircle(d.handScreen, 7f, Color.cyan);
                if (this.sweep.Available) DebugLine(this.sweep.Start, d.handScreen, Color.cyan);
            }
            if (d.handleVisible)
            {
                DebugCircle(d.handleScreen, 5f, Color.yellow);
                DebugCircle(d.handleScreen, ScreenContactRadiusPixels, d.validCandidate ? Color.green : Color.yellow);
            }
            GUI.matrix = previousMatrix;
        }
    }
}
