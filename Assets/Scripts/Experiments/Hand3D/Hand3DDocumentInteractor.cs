using System.Collections.Generic;
using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment
{
    [DefaultExecutionOrder(225)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Hand3DInteractionCoordinator))]
    public sealed class Hand3DDocumentInteractor : MonoBehaviour, IHand3DInteractionSession
    {
        // Kept only to preserve old scene references. Runtime detection never reads this field.
        [SerializeField, HideInInspector] private MonoBehaviour contact;
        [SerializeField] private Hand3DContactDetection detection = new Hand3DContactDetection();
        [SerializeField] private Hand3DInputController input;
        [SerializeField] private Hand3DArmMovement movement;
        [SerializeField] private Hand3DInteractionPose pose;
        [SerializeField] private Hand3DDocumentDisplay display;
        [SerializeField, Min(0.01f)] private float maximumReach = 0.9f;
        [Tooltip("Final-image pixels at 1080p, scaled with Game height.")]
        [SerializeField, Min(1f)] private float screenContactRadius = 55f;
        [SerializeField] private bool debugContact;
        [SerializeField, Min(0.1f)] private float documentRefreshInterval = 1f;
        private float discoveryElapsed = float.PositiveInfinity;
        private Collider[] nearbyColliders = new Collider[32];
        private Hand3DInteractionCoordinator coordinator;
        private readonly List<Hand3DDocumentBinding> documents = new List<Hand3DDocumentBinding>();
        private Hand3DDocumentBinding active, candidate;
        private Hand3DContactDetection.Sweep sweep;
        private Vector2 handScreen, documentScreen;
        private bool handVisible, documentVisible;
        private float visualDistance, worldDistance;
        private string reason = "Mantén R2/RMB en Exploration", blocker = "ninguno";
        private string diagnosticStage, lastDiagnosticKey, lastDiagnosticStage, diagnosticPickup;
        private Hand3DDocumentBinding diagnosticDocument;
        private Hand3DContactDetection.Contact diagnosticContact;
        private Hand3DContactDetection.HandSample diagnosticHand;
        private Collider diagnosticObstacle;
        private bool diagnosticEvaluated, diagnosticObstacleChecked, diagnosticEnabled;
        private int diagnosticNearby, diagnosticReady, diagnosticEvaluations, diagnosticSummaries;
        private int diagnosticPrepared, diagnosticAdded;
        private double diagnosticNextChange, diagnosticNextSummary;
        private float diagnosticObstacleDistance;
        private Hand3DPoseRecognizer diagnosticRecognizer;
        private Hand3DFingerState diagnosticFingers;
        public bool IsHolding => active != null;
        public Hand3DDocumentBinding ActiveDocument => active;

        private void Awake()
        {
            coordinator = GetComponent<Hand3DInteractionCoordinator>();
            if (input == null) input = GetComponent<Hand3DInputController>();
            if (movement == null) movement = GetComponent<Hand3DArmMovement>();
            if (pose == null) pose = GetComponent<Hand3DInteractionPose>();
            if (display == null) display = GetComponent<Hand3DDocumentDisplay>();
            detection.Bind(movement, input, transform);
        }

        public bool TryGetWorldScreen(Vector3 position, out Vector2 screen, out string reason) =>
            detection.TryWorldToScreen(position, out screen, out reason);

        private void RefreshDocuments()
        {
            if (!ExperimentScope.IsAllowed(this)) return;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (var document in root.GetComponentsInChildren<DocumentHandInteractable>())
                {
                    PrepareDocument(document);
                }
            discoveryElapsed = 0f;
        }

        private void PrepareDocument(DocumentHandInteractable document)
        {
            if (document == null || !document.isActiveAndEnabled || document.gameObject.scene != gameObject.scene) return;
            if (debugContact) diagnosticPrepared++;
            var binding = document.GetComponent<Hand3DDocumentBinding>();
            if (binding == null)
            {
                binding = document.gameObject.AddComponent<Hand3DDocumentBinding>();
                binding.ConfigureAutomatic(document);
                if (debugContact) diagnosticAdded++;
            }
            else binding.RefreshAutomaticData();
        }

        private void LateUpdate() => Tick(Time.deltaTime);

        private void Tick(float deltaTime)
        {
            if (!debugContact) { diagnosticEnabled = false; TickInteraction(deltaTime); return; }
            diagnosticStage = "Entry"; diagnosticDocument = null; diagnosticObstacle = null; diagnosticPickup = null;
            diagnosticContact = default; diagnosticHand = default; diagnosticEvaluated = diagnosticObstacleChecked = false;
            diagnosticNearby = -1; diagnosticReady = diagnosticEvaluations = 0;
            diagnosticPrepared = diagnosticAdded = 0;
            try { TickInteraction(deltaTime); }
            finally { ReportDiagnostic(Time.unscaledTimeAsDouble); }
        }

        private void DiagnosticStage(string stage) { if (debugContact) diagnosticStage = stage; }

        private void TickInteraction(float deltaTime)
        {
            DiagnosticStage("Scope");
            if (!ExperimentScope.IsAllowed(this)) return;
            DiagnosticStage("References");
            if (input == null || movement == null || pose == null || !detection.IsReady
                || !movement.isActiveAndEnabled || !pose.isActiveAndEnabled || display == null || !display.isActiveAndEnabled)
            { Cancel(); reason = "Referencias o componentes de interacción desactivados"; return; }
            if (deltaTime > 0f)
            {
                discoveryElapsed += deltaTime;
                if (discoveryElapsed >= documentRefreshInterval) RefreshDocuments();
            }
            DiagnosticStage("ControlMode");
            bool held = input.IsArmModeActive && !movement.Modes.IsEditing && !movement.Modes.IsTransitioning;
            if (!held) { Cancel(); reason = "Mantén R2/RMB en Exploration"; return; }
            if (active != null)
            {
                DiagnosticStage("Maintaining"); if (debugContact) diagnosticDocument = active;
                if (!active.IsHeld || active.Document == null || !active.Document.isActiveAndEnabled) Cancel();
                else reason = "HandHold activo: alcance inicial no se vuelve a exigir";
                return;
            }
            // A destroyed binding can compare null while still leaving this hand's lease active.
            if (coordinator.Owns(this)) { DiagnosticStage("StaleLease"); Cancel(); return; }
            if (deltaTime <= 0f) { DiagnosticStage("ZeroDeltaTime"); sweep.Reset(); return; }
            detection.ResolveDisplays();
            var hand = detection.SampleHand();
            if (debugContact) diagnosticHand = hand;
            DiagnosticStage("Coordinator");
            if (!coordinator.CanAcquire(this))
            { sweep.Reset(); reason = "Puerta u otra interacción ocupa la mano / restaurando pose"; return; }
            sweep.Advance(hand, new Vector2(Screen.width, Screen.height));
            DiagnosticStage("Discovery");
            bool synchronized = false;
            if (hand.visible)
            {
                // Do not wait for the scene scan when a new/activated document is
                // already in reach. This only populates candidates, never acquires them.
                int count = detection.OverlapNearCamera(maximumReach, ref nearbyColliders, ref synchronized);
                if (debugContact) diagnosticNearby = count;
                for (int i = 0; i < count; i++)
                    PrepareDocument(nearbyColliders[i].GetComponentInParent<DocumentHandInteractable>());
            }
            Hand3DDocumentBinding.Collect(gameObject.scene, documents);
            if (!hand.visible) DiagnosticStage("HandProjection");
            else if (documents.Count == 0) DiagnosticStage("NoDocuments");
            candidate = null;
            float best = float.PositiveInfinity, rejected = float.PositiveInfinity;
            bool hasRejected = false;
            foreach (var item in documents)
            {
                if (!item.IsReady)
                {
                    if (debugContact && diagnosticDocument == null) { diagnosticDocument = item; diagnosticStage = "BindingNotReady"; }
                    continue;
                }
                if (debugContact) { diagnosticReady++; diagnosticEvaluations++; }
                var result = detection.Evaluate(hand, sweep, item.ContactPosition, item.ContactPosition, screenContactRadius, true);
                string failure = !hand.visible ? "Mano: " + hand.reason : !result.visible ? "Documento: " + result.reason
                    : !result.WithinReach(Mathf.Min(maximumReach, item.MaximumDistance)) ? "Fuera del alcance inicial"
                    : !result.WithinContact ? "Fuera de la zona visual" : null;
                Collider hit = null;
                bool obstacleChecked = failure == null;
                if (failure == null && detection.HasObstacle(item.ContactPosition, item.transform, ref synchronized, out hit))
                    failure = "Obstáculo: " + hit.name;
                float score = result.Score;
                if (failure == null && score < best)
                { candidate = item; best = score; SaveDebug(hand.point, result.point, hand.visible, result.visible, result.separation, result.distance, "Contacto válido", hit); TraceCandidate(item, hand, result, obstacleChecked, hit); }
                else if (candidate == null && (!hasRejected || result.separation < rejected))
                { hasRejected = true; rejected = result.separation; SaveDebug(hand.point, result.point, hand.visible, result.visible, result.separation, result.distance, failure, hit); TraceCandidate(item, hand, result, obstacleChecked, hit); }
            }
            if (documents.Count == 0) reason = "No hay documentos preparados activos";
            if (candidate != null)
            {
                // Source content/reach may have changed since the periodic discovery.
                // Refresh only a pickup candidate, without reflecting over all documents every frame.
                candidate.RefreshAutomaticData();
                DiagnosticStage("CandidateRevalidation");
                if (!candidate.IsReady || Vector3.Distance(detection.WorldCamera.transform.position, candidate.ContactPosition)
                    > Mathf.Min(maximumReach, candidate.MaximumDistance))
                { DiagnosticStage(candidate.IsReady ? "CandidateReachAfterRefresh" : "CandidateNotReadyAfterRefresh"); return; }
            }
            if (candidate == null) return;
            DiagnosticStage("AcquireRejected");
            if (!coordinator.TryAcquire(this, Hand3DInteractionState.Document, false, Quaternion.identity)) return;
            active = candidate;
            DiagnosticStage("BeginHold");
            var attempted = active;
            bool tracingPickup = debugContact;
            if (tracingPickup) attempted.DebugPickup = true;
            try
            {
                if (!active.BeginHold(display)) { DiagnosticStage("PickupRejected"); Cancel(); reason = "No se pudo mostrar el documento"; return; }
            }
            finally
            {
                if (tracingPickup) { diagnosticPickup = attempted.PickupDiagnostic; attempted.DebugPickup = false; }
            }
            DiagnosticStage("Acquired");
            reason = "HandHold iniciado";
        }

        private void TraceCandidate(Hand3DDocumentBinding item, Hand3DContactDetection.HandSample hand,
            Hand3DContactDetection.Contact contact, bool obstacleChecked, Collider obstacle)
        {
            if (!debugContact) return;
            diagnosticDocument = item; diagnosticContact = contact; diagnosticEvaluated = true;
            diagnosticObstacleChecked = obstacleChecked; diagnosticObstacle = obstacle;
            diagnosticObstacleDistance = obstacle != null ? detection.LastObstacleDistance : float.PositiveInfinity;
            diagnosticStage = !hand.visible ? "HandProjection" : !contact.visible ? "DocumentProjection"
                : !contact.WithinReach(Mathf.Min(maximumReach,item.MaximumDistance)) ? "Reach"
                : !contact.WithinContact ? "VisualContact" : obstacle != null ? "Obstacle" : "ContactValid";
        }

        private void ReportDiagnostic(double now)
        {
            if (!debugContact) return;
            bool attempting = input != null && (input.IsArmModeActive || input.IsArmButtonHeld);
            if (!attempting) diagnosticSummaries = 0;
            if (diagnosticRecognizer == null) diagnosticRecognizer = GetComponent<Hand3DPoseRecognizer>();
            if (diagnosticFingers == null) diagnosticFingers = GetComponent<Hand3DFingerState>();
            var recognizer = diagnosticRecognizer; var fingers = diagnosticFingers;
            var item = diagnosticDocument;
            string key = diagnosticStage + "|" + (item != null ? item.GetEntityId().ToString() : "none")
                + "|" + (diagnosticObstacle != null ? diagnosticObstacle.GetEntityId().ToString() : "none")
                + "|" + (item != null ? item.ReadinessDiagnostic : "")
                + "|" + (input != null && input.IsArmModeActive) + "|" + (movement != null && movement.Modes != null ? movement.Modes.Mode.ToString() : "missing")
                + "|" + (movement != null && movement.Modes != null && movement.Modes.IsTransitioning)
                + "|" + (fingers != null ? fingers.Current.ToString() : "missing") + "|" + (recognizer?.CurrentPose?.Name ?? "none")
                + "|" + detection.ReadinessDiagnostic
                + "|" + diagnosticPickup;
            bool changed = !diagnosticEnabled || key != lastDiagnosticKey && (now >= diagnosticNextChange || diagnosticStage == "Acquired"
                || diagnosticStage == "PickupRejected" && lastDiagnosticStage != "PickupRejected");
            bool summary = diagnosticEnabled && !changed && attempting && !IsHolding && diagnosticSummaries < 3 && now >= diagnosticNextSummary;
            if (!changed && !summary) return;
            if (summary) diagnosticSummaries++;
            diagnosticEnabled = true; lastDiagnosticKey = key; lastDiagnosticStage = diagnosticStage;
            diagnosticNextChange = now + .5; diagnosticNextSummary = now + 2;
            string detail = diagnosticEvaluated
                ? $"handVisible={diagnosticHand.visible}, handPx={diagnosticHand.point}, documentVisible={diagnosticContact.visible}, documentPx={diagnosticContact.point}, projectionReason={diagnosticContact.reason ?? diagnosticHand.reason ?? "none"}; separation={diagnosticContact.separation:F2}px, swept={diagnosticContact.sweptDistance:F2}px, tolerance={diagnosticContact.radius:F2}px ({screenContactRadius:F2}@1080p); distance={diagnosticContact.distance:F3}m, limit={Mathf.Min(maximumReach,item != null ? item.MaximumDistance : maximumReach):F3}m; obstacles={(diagnosticObstacleChecked ? diagnosticObstacle != null ? diagnosticObstacle.name : "clear" : "not evaluated")}, blockerDistance={diagnosticObstacleDistance:F3}m, occlusionRay={Mathf.Max(0f,diagnosticContact.distance-.001f):F3}m (triggers ignored)."
                : $"contact not evaluated; binding={(item != null ? item.ReadinessDiagnostic : "none")}; handProjection={diagnosticHand.visible}/{diagnosticHand.reason ?? "not sampled"}.";
            var collider = item != null ? item.DiagnosticCollider : null;
            Debug.Log($"[Hand3D Documents] {(summary ? "Summary" : "State")} stage={diagnosticStage}; "
                + $"armActive={input != null && input.IsArmModeActive}, armHeld={input != null && input.IsArmButtonHeld}, mode={(movement != null && movement.Modes != null ? movement.Modes.Mode.ToString() : "missing")}, transitioning={movement != null && movement.Modes != null && movement.Modes.IsTransitioning}; "
                + $"gesture={recognizer?.CurrentPose?.Name ?? "none"}, fingers={fingers?.Current.ToString() ?? "missing"} (no gesture gate in current pickup logic); "
                + $"inputPresent={input != null}, movementEnabled={movement != null && movement.isActiveAndEnabled}, poseEnabled={pose != null && pose.isActiveAndEnabled}, displayEnabled={display != null && display.isActiveAndEnabled}; detection={detection.ReadinessDiagnostic}; "
                + $"scene={gameObject.scene.path}, worldCamera={DiagnosticName(detection.WorldCamera)}, handPoint={DiagnosticName(detection.HandPoint)}; "
                + $"registry(last collect)={documents.Count}, prepared={diagnosticPrepared}, added={diagnosticAdded}, ready={diagnosticReady}, evaluated={diagnosticEvaluations}, localColliders={diagnosticNearby} (-1=not queried); "
                + $"document={DiagnosticName(item)}, collider={DiagnosticName(collider)}, colliderEnabled={collider != null && collider.enabled}, colliderActive={collider != null && collider.gameObject.activeInHierarchy}, colliderLayer={(collider != null ? collider.gameObject.layer : -1)}; "
                + detail + $" coordinator={coordinator?.State.ToString() ?? "missing"}, owner={DiagnosticName(coordinator != null ? coordinator.Owner : null)}, coordinatorEnabled={coordinator != null && coordinator.isActiveAndEnabled}, poseWeight={pose?.Weight:F4}; "
                + $"pickup={diagnosticPickup ?? "not attempted"}. reason={reason}",this);
        }

        private static string DiagnosticName(UnityEngine.Object value) => value != null ? value.name : "none";

        private void SaveDebug(Vector2 hand, Vector2 doc, bool hv, bool dv, float pixels, float metres, string rejection, Collider hit)
        {
            handScreen = hand; documentScreen = doc; handVisible = hv; documentVisible = dv;
            visualDistance = pixels; worldDistance = metres; reason = rejection;
            blocker = hit != null ? hit.name + " (capa " + hit.gameObject.layer + ")" : "ninguno";
        }

        public void Cancel()
        {
            if (coordinator != null) coordinator.Release(this);
            sweep.Reset();
        }
        bool IHand3DInteractionSession.ProcessMovement(Vector2 integratedInput, bool usingMouse) => false;
        void IHand3DInteractionSession.ReleaseInteraction()
        {
            var previous = active;
            active = null;
            if (previous != null) previous.EndHold();
            sweep.Reset();
        }
        private void OnDisable()
        {
            Cancel();
            if (debugContact) Debug.Log("[Hand3D Documents] Component disabled; document session cancelled.",this);
        }
        private void OnGUI()
        {
            if (!Application.isPlaying || !debugContact) return;
            GUI.Box(new Rect(12, 282, Mathf.Min(800, Screen.width - 24), 126), GUIContent.none);
            GUI.Label(new Rect(22, 288, Screen.width - 44, 120),
                $"DOCUMENTOS: {documents.Count} | candidata: {DiagnosticName(candidate)} | HandHold: {DiagnosticName(active)}\n"
                + $"Visual: {visualDistance:F1} px / {screenContactRadius * Screen.height / 1080f:F1} | física: {worldDistance:F2} m / {maximumReach:F2}\n"
                + $"Proyección mano/documento: {handVisible}/{documentVisible} | obstáculo: {blocker}\n{reason}");
            if (documentVisible) GUI.Label(new Rect(documentScreen.x - 5, Screen.height - documentScreen.y - 10, 30, 25), "◯");
            if (handVisible) GUI.Label(new Rect(handScreen.x - 5, Screen.height - handScreen.y - 10, 30, 25), "+");
        }
    }
}
