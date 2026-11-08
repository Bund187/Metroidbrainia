using System.Collections.Generic;
using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment
{
    [DefaultExecutionOrder(225)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Hand3DInteractionCoordinator))]
    public sealed class Hand3DDocumentInteractor : MonoBehaviour
    {
        [SerializeField] private Hand3DDoorInteractor contact;
        [SerializeField] private Hand3DInputController input;
        [SerializeField] private Hand3DArmMovement movement;
        [SerializeField] private Hand3DInteractionPose pose;
        [SerializeField] private Hand3DDocumentDisplay display;
        [SerializeField, Min(0.01f)] private float maximumReach = 0.9f;
        [Tooltip("Final-image pixels at 1080p, scaled with Game height.")]
        [SerializeField, Min(1f)] private float screenContactRadius = 55f;
        [SerializeField] private bool debugContact;
        private Hand3DInteractionCoordinator coordinator;
        private readonly List<Hand3DDocumentBinding> documents = new List<Hand3DDocumentBinding>();
        private Hand3DDocumentBinding active, candidate;
        private Vector2 previousHand, previousSize, handScreen, documentScreen;
        private bool hasPrevious, handVisible, documentVisible;
        private float visualDistance, worldDistance;
        private string reason = "Mantén R2/RMB en Exploration", blocker = "ninguno";
        public bool IsHolding => active != null;
        public Hand3DDocumentBinding ActiveDocument => active;

        private void Awake()
        {
            coordinator = GetComponent<Hand3DInteractionCoordinator>();
            if (contact == null) contact = GetComponent<Hand3DDoorInteractor>();
            if (input == null) input = GetComponent<Hand3DInputController>();
            if (movement == null) movement = GetComponent<Hand3DArmMovement>();
            if (pose == null) pose = GetComponent<Hand3DInteractionPose>();
            if (display == null) display = GetComponent<Hand3DDocumentDisplay>();
        }

        private void LateUpdate() => Tick(Time.deltaTime);

        private void Tick(float deltaTime)
        {
            if (!ExperimentScope.IsAllowed(this)) return;
            if (contact == null || !contact.isActiveAndEnabled || input == null || movement == null || pose == null
                || !movement.isActiveAndEnabled || !pose.isActiveAndEnabled || display == null || !display.isActiveAndEnabled)
            { Cancel(); reason = "Referencias o componentes de interacción desactivados"; return; }
            bool held = input.IsArmModeActive && !movement.Modes.IsEditing && !movement.Modes.IsTransitioning;
            if (!held) { Cancel(); hasPrevious = false; reason = "Mantén R2/RMB en Exploration"; return; }
            if (active != null)
            {
                if (!active.IsHeld || active.Document == null || !active.Document.isActiveAndEnabled) Cancel();
                else reason = "HandHold activo: alcance inicial no se vuelve a exigir";
                return;
            }
            // A destroyed binding can compare null while still leaving this hand's lease active.
            if (coordinator.Owns(this)) { Cancel(); return; }
            if (deltaTime <= 0f) { hasPrevious = false; return; }
            bool visible = contact.TryGetHandScreen(out Vector2 hand, out string handReason);
            Vector2 size = new Vector2(Screen.width, Screen.height);
            bool sweep = hasPrevious && visible && previousSize == size;
            if (!coordinator.CanAcquire(this))
            { hasPrevious = false; reason = "Puerta u otra interacción ocupa la mano / restaurando pose"; return; }
            Hand3DDocumentBinding.Collect(gameObject.scene, documents);
            candidate = null;
            float best = float.PositiveInfinity, rejected = float.PositiveInfinity;
            bool hasRejected = false;
            float radius = screenContactRadius * Screen.height / 1080f;
            foreach (var item in documents)
            {
                bool projected = contact.TryGetWorldScreen(item.ContactPosition, out Vector2 screen, out string projectionReason);
                float distance = Vector3.Distance(contact.WorldCamera.transform.position, item.ContactPosition);
                float separation = visible && projected ? Vector2.Distance(hand, screen) : float.PositiveInfinity;
                float swept = sweep && projected ? Hand3DDoorInteractor.DistanceToScreenSegment(screen, previousHand, hand) : separation;
                bool obstacle = contact.HasWorldObstacle(item.ContactPosition, item.transform, out Collider hit);
                string failure = !item.IsReady ? "Documento sin datos visuales válidos o no disponible"
                    : !visible ? "Mano: " + handReason : !projected ? "Documento: " + projectionReason
                    : distance > Mathf.Min(maximumReach, item.MaximumDistance) ? "Fuera del alcance inicial"
                    : obstacle ? "Obstáculo: " + hit.name : swept > radius ? "Fuera de la zona visual" : null;
                float score = separation <= radius ? separation : radius + swept;
                if (failure == null && score < best)
                { candidate = item; best = score; SaveDebug(hand, screen, visible, projected, separation, distance, "Contacto válido", hit); }
                else if (candidate == null && (!hasRejected || separation < rejected))
                { hasRejected = true; rejected = separation; SaveDebug(hand, screen, visible, projected, separation, distance, failure, hit); }
            }
            previousHand = hand; previousSize = size; hasPrevious = visible;
            if (documents.Count == 0) reason = "No hay documentos preparados activos";
            if (candidate == null || !coordinator.TryAcquire(this)) return;
            active = candidate;
            if (!active.BeginHold(display)) { Cancel(); reason = "No se pudo mostrar el documento"; return; }
            pose.SetHandHold(true);
            reason = "HandHold iniciado";
        }

        private void SaveDebug(Vector2 hand, Vector2 doc, bool hv, bool dv, float pixels, float metres, string rejection, Collider hit)
        {
            handScreen = hand; documentScreen = doc; handVisible = hv; documentVisible = dv;
            visualDistance = pixels; worldDistance = metres; reason = rejection;
            blocker = hit != null ? hit.name + " (capa " + hit.gameObject.layer + ")" : "ninguno";
        }

        public void Cancel()
        {
            var previous = active;
            active = null;
            if (previous != null) previous.EndHold();
            if (coordinator != null && coordinator.Owns(this))
            { if (pose != null) pose.SetHandHold(false); coordinator.Release(this); }
            hasPrevious = false;
        }
        private void OnDisable() => Cancel();
        private void OnGUI()
        {
            if (!Application.isPlaying || !debugContact) return;
            GUI.Box(new Rect(12, 282, Mathf.Min(800, Screen.width - 24), 126), GUIContent.none);
            GUI.Label(new Rect(22, 288, Screen.width - 44, 120),
                $"DOCUMENTOS: {documents.Count} | candidata: {candidate?.name ?? "ninguna"} | HandHold: {active?.name ?? "inactivo"}\n"
                + $"Visual: {visualDistance:F1} px / {screenContactRadius * Screen.height / 1080f:F1} | física: {worldDistance:F2} m / {maximumReach:F2}\n"
                + $"Proyección mano/documento: {handVisible}/{documentVisible} | obstáculo: {blocker}\n{reason}");
            if (documentVisible) GUI.Label(new Rect(documentScreen.x - 5, Screen.height - documentScreen.y - 10, 30, 25), "◯");
            if (handVisible) GUI.Label(new Rect(handScreen.x - 5, Screen.height - handScreen.y - 10, 30, 25), "+");
        }
    }
}
