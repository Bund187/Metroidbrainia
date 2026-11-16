using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment
{
    [DisallowMultipleComponent]
    public sealed class Hand3DPoseActions : MonoBehaviour
    {
        [SerializeField] private Hand3DPoseRecognizer poseRecognizer;
        [SerializeField, Min(0.01f)] private float pointClickDuration = 0.18f;
        [Tooltip("Blend towards each index joint's captured Flexed rotation; no fixed rotation axis.")]
        [SerializeField, Range(0f, 1f)] private float pointClickAmplitude = 0.18f;

        private bool clicking;
        private Hand3DInteractionCoordinator interactions;
        private float elapsed;
        public float PointClickWeight { get; private set; }

        private void OnEnable()
        {
            interactions = GetComponent<Hand3DInteractionCoordinator>();
            if (poseRecognizer == null)
                poseRecognizer = GetComponent<Hand3DPoseRecognizer>();
            if (poseRecognizer != null)
                poseRecognizer.PoseChanged += CancelAction;
            CancelAction();
        }

        public bool TryExecute()
        {
            if (!isActiveAndEnabled || !ExperimentScope.IsAllowed(this) || Time.timeScale <= 0f || clicking
                || interactions != null && !interactions.CanExecuteGestureAction)
                return false;
            HandPose pose = poseRecognizer != null ? poseRecognizer.CurrentPose : null;
            if (pose == null || pose.Action == PoseAction.None)
                return false;
            if (pose.Action == PoseAction.PointClick)
            {
                clicking = true;
                elapsed = 0f;
            }
            pose.OnAction?.Invoke();
            return true;
        }

        private void Update()
        {
            if (!clicking || Time.deltaTime <= 0f)
                return;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, pointClickDuration));
            PointClickWeight = Mathf.Sin(t * Mathf.PI) * Mathf.Clamp01(pointClickAmplitude);
            if (t >= 1f)
                CancelAction();
        }

        public void CancelAction()
        {
            clicking = false;
            elapsed = 0f;
            PointClickWeight = 0f;
        }

        private void OnDisable()
        {
            if (poseRecognizer != null)
                poseRecognizer.PoseChanged -= CancelAction;
            CancelAction();
        }
    }
}
