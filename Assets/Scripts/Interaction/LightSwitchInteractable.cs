using UnityEngine;
using UnityEngine.Events;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class LightSwitchInteractable : MonoBehaviour, IHandActionInteractable
    {
        [Header("Interaction")]
        [SerializeField, Min(0.01f)] private float maxInteractionDistance = 1.8f;

        [Header("Switch")]
        [SerializeField] private Transform switchTransform;
        [SerializeField] private bool startsOn;
        [SerializeField] private Vector3 offLocalEulerAngles;
        [SerializeField] private Vector3 onLocalEulerAngles;
        [SerializeField, Min(0f)] private float switchMoveDuration = 0.08f;

        [Header("Lights")]
        [SerializeField] private Light[] controlledLights = new Light[0];

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip toggleClip;
        [Header("Events")]
        [SerializeField] private UnityEvent onTurnedOn = new UnityEvent();
        [SerializeField] private UnityEvent onTurnedOff = new UnityEvent();

        private bool initialized;
        private bool isOn;
        private bool moving;
        private float moveTime;
        private Quaternion moveStartRotation;
        private Quaternion targetRotation;

        public bool IsOn => isOn;
        public UnityEvent OnTurnedOn => onTurnedOn;
        public UnityEvent OnTurnedOff => onTurnedOff;

        public void RestoreState(bool on)
        {
            if (!initialized || switchTransform == null)
                return;
            moving = false;
            moveTime = 0f;
            isOn = on;
            targetRotation = Quaternion.Euler(isOn ? onLocalEulerAngles : offLocalEulerAngles);
            switchTransform.localRotation = targetRotation;
            SynchronizeLights();
        }

        private void Awake()
        {
            if (switchTransform == null)
            {
                Debug.LogError("Assign Switch Transform to the movable rocker, not the switch body.", this);
                enabled = false;
                return;
            }

            isOn = startsOn;
            targetRotation = Quaternion.Euler(isOn ? onLocalEulerAngles : offLocalEulerAngles);
            switchTransform.localRotation = targetRotation;
            SynchronizeLights();
            initialized = true;
        }

        public bool TryPerformHandAction(ArmPose pose, Camera playerCamera, RaycastHit hit)
        {
            if (!initialized || !isActiveAndEnabled || moving || switchTransform == null
                || pose != ArmPose.Point || playerCamera == null || hit.collider == null
                || !ReferenceEquals(hit.collider.GetComponentInParent<IHandActionInteractable>(), this)
                || (playerCamera.transform.position - hit.point).sqrMagnitude
                    > maxInteractionDistance * maxInteractionDistance)
                return false;

            Toggle();
            return true;
        }

        public void Toggle()
        {
            SetState(!isOn);
        }

        public void SetState(bool on)
        {
            if (!initialized || !isActiveAndEnabled || moving || switchTransform == null || isOn == on)
                return;

            isOn = on;
            SynchronizeLights();
            moveStartRotation = switchTransform.localRotation;
            targetRotation = Quaternion.Euler(isOn ? onLocalEulerAngles : offLocalEulerAngles);
            moveTime = 0f;
            moving = switchMoveDuration > 0f;
            if (!moving)
                switchTransform.localRotation = targetRotation;

            if (audioSource != null && toggleClip != null)
                audioSource.PlayOneShot(toggleClip);
            if (isOn)
                onTurnedOn?.Invoke();
            else
                onTurnedOff?.Invoke();
        }

        private void SynchronizeLights()
        {
            if (controlledLights == null)
                return;

            foreach (Light light in controlledLights)
            {
                if (light != null)
                    light.enabled = isOn;
            }
        }

        private void Update()
        {
            if (!moving || switchTransform == null || Time.deltaTime <= 0f)
                return;

            moveTime += Time.deltaTime;
            float progress = switchMoveDuration > 0f ? Mathf.Clamp01(moveTime / switchMoveDuration) : 1f;
            switchTransform.localRotation = Quaternion.Slerp(moveStartRotation, targetRotation, progress);
            if (progress >= 1f)
            {
                switchTransform.localRotation = targetRotation;
                moving = false;
            }
        }

        private void OnDisable()
        {
            // Finish the visual state so re-enabling cannot leave a rocker between its two positions.
            if (initialized && moving && switchTransform != null)
                switchTransform.localRotation = targetRotation;
            moving = false;
        }
    }
}
