using UnityEngine;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class TelevisionLightFlicker : MonoBehaviour
    {
        [SerializeField] private Light televisionLight;
        [SerializeField] private bool useExistingIntensity = true;
        [SerializeField, Min(0f)] private float baseIntensity = 1f;
        [SerializeField, Min(0f)] private float minimumMultiplier = 0.75f;
        [SerializeField, Min(0f)] private float maximumMultiplier = 1.2f;
        [SerializeField, Min(0f)] private float variationSpeed = 4f;
        [SerializeField, Min(0.001f)] private float transitionSmoothTime = 0.08f;
        [Tooltip("Average changes per second. Zero disables occasional changes.")]
        [SerializeField, Min(0f)] private float occasionalChangeFrequency = 0.12f;
        [SerializeField, Min(0f)] private float occasionalChangeMagnitude = 0.2f;

        private float originalIntensity;
        private float multiplier;
        private float multiplierVelocity;
        private float occasionalOffset;
        private float offsetVelocity;
        private float nextChangeTime;
        private float noiseSeed;
        private float scheduledFrequency;
        private bool initialized;

        private void Reset()
        {
            televisionLight = GetComponent<Light>();
            if (televisionLight != null)
                baseIntensity = televisionLight.intensity;
        }

        private void OnEnable()
        {
            if (televisionLight == null)
                televisionLight = GetComponent<Light>();
            if (televisionLight == null)
            {
                Debug.LogError("Assign the existing television light.", this);
                enabled = false;
                return;
            }
            originalIntensity = televisionLight.intensity;
            multiplier = Mathf.Clamp(1f, minimumMultiplier, maximumMultiplier);
            multiplierVelocity = offsetVelocity = occasionalOffset = 0f;
            noiseSeed = Random.Range(0f, 1000f);
            ScheduleNextChange();
            initialized = true;
        }

        private void ScheduleNextChange()
        {
            scheduledFrequency = occasionalChangeFrequency;
            nextChangeTime = occasionalChangeFrequency > 0f
                ? Time.time + Random.Range(0.6f, 1.4f) / occasionalChangeFrequency : float.PositiveInfinity;
        }

        private void Update()
        {
            if (!initialized || televisionLight == null || !televisionLight.isActiveAndEnabled || Time.deltaTime <= 0f)
                return;
            if (scheduledFrequency != occasionalChangeFrequency)
                ScheduleNextChange();
            if (Time.time >= nextChangeTime)
            {
                occasionalOffset = Random.Range(-occasionalChangeMagnitude, occasionalChangeMagnitude);
                ScheduleNextChange();
            }
            float noise = Mathf.PerlinNoise(noiseSeed, Time.time * variationSpeed);
            float target = Mathf.Clamp(Mathf.Lerp(minimumMultiplier, maximumMultiplier, noise) + occasionalOffset,
                minimumMultiplier, maximumMultiplier);
            multiplier = Mathf.SmoothDamp(multiplier, target, ref multiplierVelocity, transitionSmoothTime);
            multiplier = Mathf.Clamp(multiplier, minimumMultiplier, maximumMultiplier);
            occasionalOffset = Mathf.SmoothDamp(occasionalOffset, 0f, ref offsetVelocity, transitionSmoothTime * 2f);
            float effectiveBase = useExistingIntensity ? originalIntensity : Mathf.Max(0f, baseIntensity);
            televisionLight.intensity = effectiveBase * multiplier;
        }

        private void OnDisable()
        {
            if (initialized && televisionLight != null)
                televisionLight.intensity = originalIntensity;
            initialized = false;
        }

        private void OnValidate()
        {
            baseIntensity = Mathf.Max(0f, baseIntensity);
            minimumMultiplier = Mathf.Max(0f, minimumMultiplier);
            maximumMultiplier = Mathf.Max(minimumMultiplier, maximumMultiplier);
            variationSpeed = Mathf.Max(0f, variationSpeed);
            transitionSmoothTime = Mathf.Max(0.001f, transitionSmoothTime);
            occasionalChangeFrequency = Mathf.Max(0f, occasionalChangeFrequency);
            occasionalChangeMagnitude = Mathf.Max(0f, occasionalChangeMagnitude);
        }
    }
}
