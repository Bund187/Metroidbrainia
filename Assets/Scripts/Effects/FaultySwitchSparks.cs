using System.Collections;
using UnityEngine;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class FaultySwitchSparks : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ParticleSystem sparksParticleSystem;
        [SerializeField] private Light sparkLight;

        [Header("Episode Timing")]
        [SerializeField, Min(0f)] private float minEpisodeInterval = 1.5f;
        [SerializeField, Min(0f)] private float maxEpisodeInterval = 5f;
        [SerializeField, Min(1)] private int minMicroSparks = 1;
        [SerializeField, Min(1)] private int maxMicroSparks = 4;
        [SerializeField, Min(0f)] private float minMicroSparkDelay = 0.02f;
        [SerializeField, Min(0f)] private float maxMicroSparkDelay = 0.12f;

        [Header("Flash")]
        [SerializeField, Min(0f)] private float minFlashIntensity = 1.5f;
        [SerializeField, Min(0f)] private float maxFlashIntensity = 3f;
        [SerializeField, Min(0f)] private float minFlashDuration = 0.03f;
        [SerializeField, Min(0f)] private float maxFlashDuration = 0.07f;
        [SerializeField, Min(0f)] private float maxAngularJitter = 8f;

        private bool isRunning;
        private bool episodeRunning;
        private Quaternion baseLightLocalRotation;
        private bool lightInitialized;
        private float lightOffTime;

        private void Awake()
        {
            InitializeLight();
        }

        private void InitializeLight()
        {
            if (lightInitialized || sparkLight == null)
                return;

            baseLightLocalRotation = sparkLight.transform.localRotation;
            lightInitialized = true;
            if (sparkLight.type != LightType.Spot)
                Debug.LogWarning("Spark Light should be a Spot Light for angular jitter to affect its illumination direction.", this);
        }

        private void ResetLight()
        {
            lightOffTime = 0f;
            if (sparkLight == null)
                return;

            sparkLight.enabled = false;
            if (lightInitialized)
                sparkLight.transform.localRotation = baseLightLocalRotation;
        }

        private void OnEnable()
        {
            ResetLight();
            StartSparking();
        }

        public void StartSparking()
        {
            if (!isActiveAndEnabled || isRunning || !ValidateReferences())
                return;

            isRunning = true;
            StartCoroutine(AutomaticLoop());
        }

        public void StopSparking()
        {
            StopAllCoroutines();
            isRunning = false;
            episodeRunning = false;
            ResetLight();
            if (sparksParticleSystem != null)
                sparksParticleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void TriggerDischarge()
        {
            if (!isActiveAndEnabled || episodeRunning || !ValidateReferences())
                return;

            StartCoroutine(Episode());
        }

        private IEnumerator AutomaticLoop()
        {
            while (isRunning)
            {
                yield return new WaitForSeconds(Random.Range(minEpisodeInterval, maxEpisodeInterval));
                if (!episodeRunning)
                    yield return Episode();
            }
        }

        private IEnumerator Episode()
        {
            episodeRunning = true;
            int count = Random.Range(minMicroSparks, maxMicroSparks + 1);
            for (int i = 0; i < count; i++)
            {
                if (sparksParticleSystem == null || sparkLight == null)
                    break;

                float roll = Random.value;
                int particles = roll < 0.65f ? 1 : roll < 0.90f ? 2 : 3;
                sparksParticleSystem.Emit(particles);
                sparkLight.intensity = Random.Range(minFlashIntensity, maxFlashIntensity);
                float pitch = Random.Range(-maxAngularJitter, maxAngularJitter);
                float yaw = Random.Range(-maxAngularJitter, maxAngularJitter);
                sparkLight.transform.localRotation = baseLightLocalRotation * Quaternion.Euler(pitch, yaw, 0f);
                float duration = Random.Range(minFlashDuration, maxFlashDuration);
                // Overlapping micro-sparks can extend a flash, but never shorten it.
                lightOffTime = Mathf.Max(lightOffTime, Time.time + duration);
                sparkLight.enabled = true;

                if (i < count - 1)
                    yield return new WaitForSeconds(Random.Range(minMicroSparkDelay, maxMicroSparkDelay));
            }
            episodeRunning = false;
        }

        private void Update()
        {
            if (sparkLight != null && sparkLight.enabled
                && Time.time >= lightOffTime)
                ResetLight();
        }

        private bool ValidateReferences()
        {
            if (sparksParticleSystem != null && sparkLight != null)
            {
                InitializeLight();
                return true;
            }

            Debug.LogError("Assign Sparks Particle System and Spark Light before starting the effect.", this);
            return false;
        }

        private void OnDisable()
        {
            StopSparking();
        }

        private void OnValidate()
        {
            minEpisodeInterval = Mathf.Max(0f, minEpisodeInterval);
            maxEpisodeInterval = Mathf.Max(minEpisodeInterval, maxEpisodeInterval);
            minMicroSparks = Mathf.Clamp(minMicroSparks, 1, int.MaxValue - 1);
            maxMicroSparks = Mathf.Clamp(maxMicroSparks, minMicroSparks, int.MaxValue - 1);
            minMicroSparkDelay = Mathf.Max(0f, minMicroSparkDelay);
            maxMicroSparkDelay = Mathf.Max(minMicroSparkDelay, maxMicroSparkDelay);
            minFlashIntensity = Mathf.Max(0f, minFlashIntensity);
            maxFlashIntensity = Mathf.Max(minFlashIntensity, maxFlashIntensity);
            minFlashDuration = Mathf.Max(0f, minFlashDuration);
            maxFlashDuration = Mathf.Max(minFlashDuration, maxFlashDuration);
            maxAngularJitter = Mathf.Max(0f, maxAngularJitter);
        }
    }
}
