using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Metroidbrainia
{
    // Sample after player/camera updates and procedural obstacle movement.
    [DefaultExecutionOrder(400)]
    [DisallowMultipleComponent]
    public sealed class ArmLightingTint : MonoBehaviour
    {
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Image armImage;
        [SerializeField] private Image handImage;
        [SerializeField, Range(0f, 1f)] private float minimumBrightness = 0.75f;
        [SerializeField, Range(0f, 1f)] private float maximumBrightness = 1f;
        [SerializeField, Min(0f)] private float lightInfluence = 1f;
        [Tooltip("Light intensity that produces full normalized intensity before distance attenuation.")]
        [SerializeField, Min(0.001f)] private float intensityForFullInfluence = 1f;
        [SerializeField, Min(0.01f)] private float lightingCheckInterval = 0.15f;
        [SerializeField, Min(0.01f)] private float brightnessSmoothTime = 0.25f;
        [SerializeField] private LayerMask obstacleMask = Physics.DefaultRaycastLayers;

        private Light[] cachedLights;
        private Color originalArmColor;
        private Color originalHandColor;
        private float currentBrightness;
        private float targetBrightness;
        private float brightnessVelocity;
        private float timeUntilNextCheck;
        private bool initialized;

        private void Awake()
        {
            if (gameplayCamera == null || armImage == null || handImage == null)
            {
                Debug.LogError("Assign Gameplay Camera, ArmBody's Image and Hand's Image.", this);
                enabled = false;
                return;
            }

            originalArmColor = armImage.color;
            originalHandColor = handImage.color;
            var relevantLights = new List<Light>();
            Light[] sceneLights = FindObjectsByType<Light>(FindObjectsInactive.Include);
            foreach (Light light in sceneLights)
            {
                if (light.type == LightType.Point || light.type == LightType.Spot)
                    relevantLights.Add(light);
            }
            cachedLights = relevantLights.ToArray();

            GetBrightnessRange(out float minimum, out float maximum);
            currentBrightness = Mathf.Clamp(1f, minimum, maximum);
            targetBrightness = currentBrightness;
            initialized = true;
        }

        private void OnEnable()
        {
            timeUntilNextCheck = 0f;
            brightnessVelocity = 0f;
            if (initialized)
                ApplyTint();
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            if (!initialized || gameplayCamera == null || deltaTime <= 0f)
                return;

            timeUntilNextCheck -= deltaTime;
            if (timeUntilNextCheck <= 0f)
            {
                EvaluateLighting();
                timeUntilNextCheck = Mathf.Max(0.01f, lightingCheckInterval);
            }

            GetBrightnessRange(out float minimum, out float maximum);
            currentBrightness = Mathf.SmoothDamp(currentBrightness, targetBrightness, ref brightnessVelocity,
                Mathf.Max(0.01f, brightnessSmoothTime), Mathf.Infinity, deltaTime);
            currentBrightness = Mathf.Clamp(currentBrightness, minimum, maximum);
            ApplyTint();
        }

        private void EvaluateLighting()
        {
            Vector3 samplePosition = gameplayCamera.transform.position;
            float totalContribution = 0f;
            // Doors and other obstacles move without Rigidbodies. Sync only at the sampling interval.
            Physics.SyncTransforms();
            for (int i = 0; i < cachedLights.Length; i++)
            {
                Light light = cachedLights[i];
                if (light == null || !light.enabled || !light.gameObject.activeInHierarchy
                    || (light.type != LightType.Point && light.type != LightType.Spot)
                    || light.range <= 0f || light.intensity <= 0f)
                    continue;

                Vector3 toLight = light.transform.position - samplePosition;
                float distance = toLight.magnitude;
                if (distance >= light.range)
                    continue;

                if (light.type == LightType.Spot && distance > 0f)
                {
                    float coneCosine = Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
                    if (Vector3.Dot(light.transform.forward, -toLight / distance) < coneCosine)
                        continue;
                }

                float attenuation = 1f - distance / light.range;
                float intensity = Mathf.Clamp01(light.intensity / Mathf.Max(0.001f, intensityForFullInfluence));
                float contribution = attenuation * attenuation * intensity;
                if (contribution <= 0f || lightInfluence <= 0f)
                    continue;
                if (distance > 0f && Physics.Raycast(samplePosition, toLight / distance, distance,
                    obstacleMask, QueryTriggerInteraction.Ignore))
                    continue;

                totalContribution += contribution;
                if (totalContribution * lightInfluence >= 1f)
                    break;
            }

            GetBrightnessRange(out float minimum, out float maximum);
            // Per light: (1 - distance/range)^2 * Clamp01(intensity/referenceIntensity).
            float influence = Mathf.Clamp01(totalContribution * Mathf.Max(0f, lightInfluence));
            targetBrightness = Mathf.Lerp(minimum, maximum, influence);
        }

        private void GetBrightnessRange(out float minimum, out float maximum)
        {
            minimum = Mathf.Clamp01(minimumBrightness);
            maximum = Mathf.Max(minimum, Mathf.Clamp01(maximumBrightness));
        }

        private void ApplyTint()
        {
            if (armImage != null)
                armImage.color = TintRGB(originalArmColor, currentBrightness);
            if (handImage != null)
                handImage.color = TintRGB(originalHandColor, currentBrightness);
        }

        private static Color TintRGB(Color original, float brightness)
        {
            return new Color(original.r * brightness, original.g * brightness,
                original.b * brightness, original.a);
        }

        private void OnDisable()
        {
            if (!initialized)
                return;
            if (armImage != null)
                armImage.color = originalArmColor;
            if (handImage != null)
                handImage.color = originalHandColor;
            brightnessVelocity = 0f;
        }

        private void OnValidate()
        {
            minimumBrightness = Mathf.Clamp01(minimumBrightness);
            maximumBrightness = Mathf.Clamp(maximumBrightness, minimumBrightness, 1f);
        }
    }
}
