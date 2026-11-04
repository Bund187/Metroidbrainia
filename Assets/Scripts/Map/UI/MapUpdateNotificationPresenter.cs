using System.Collections;
using UnityEngine;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class MapUpdateNotificationPresenter : MonoBehaviour
    {
        [SerializeField] private MapJournalState journalState;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip notificationClip;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.10f;
        [SerializeField, Min(0f)] private float visibleDuration = 0.8f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.20f;

        private MapJournalState subscribedJournal;
        private Coroutine animationRoutine;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (journalState == null || canvasGroup == null)
            {
                Debug.LogError("Assign MapJournalState and DiscoveryIcon's CanvasGroup.", this);
                enabled = false;
                return;
            }
            canvasGroup.alpha = 0f;
            subscribedJournal = journalState;
            subscribedJournal.RoomStateChanged += OnRoomStateChanged;
            subscribedJournal.DiscoveryStateChanged += OnDiscoveryStateChanged;
            subscribedJournal.RoomVariantChanged += OnRoomVariantChanged;
            subscribedJournal.DiscoveryResolutionChanged += OnDiscoveryResolutionChanged;
        }

        private void OnRoomVariantChanged(MapRoomDefinition room, MapRoomVariantDefinition previous, MapRoomVariantDefinition current)
        {
            if (subscribedJournal != null && subscribedJournal.GetRoomState(room) == MapEntryState.Revealed)
                ShowNotification();
        }

        private void OnDiscoveryResolutionChanged(MapDiscoveryDefinition discovery, bool resolved)
        {
            if (resolved && discovery != null && discovery.Type == MapDiscoveryType.Question)
                ShowNotification();
        }

        private void OnRoomStateChanged(MapRoomDefinition room, MapEntryState state)
        {
            if (state == MapEntryState.PendingReveal)
                ShowNotification();
        }

        private void OnDiscoveryStateChanged(MapDiscoveryDefinition discovery, MapEntryState state)
        {
            if (state == MapEntryState.PendingReveal)
                ShowNotification();
        }

        private void ShowNotification()
        {
            if (!isActiveAndEnabled || canvasGroup == null)
                return;
            if (animationRoutine != null)
                StopCoroutine(animationRoutine);
            canvasGroup.alpha = 0f;
            if (audioSource != null && notificationClip != null)
                audioSource.PlayOneShot(notificationClip);
            animationRoutine = StartCoroutine(AnimateNotification());
        }

        private IEnumerator AnimateNotification()
        {
            yield return Fade(0f, 1f, fadeInDuration);
            float elapsed = 0f;
            while (elapsed < visibleDuration && canvasGroup != null)
            {
                elapsed += Time.deltaTime;
                float phase = Mathf.Clamp01(elapsed / visibleDuration) * Mathf.PI * 2f;
                float pulse = Mathf.Sin(phase);
                canvasGroup.alpha = 1f - 0.5f * pulse * pulse;
                yield return null;
            }
            yield return Fade(1f, 0f, fadeOutDuration);
            animationRoutine = null;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && canvasGroup != null)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            if (canvasGroup != null)
                canvasGroup.alpha = to;
        }

        private void OnDisable()
        {
            if (subscribedJournal != null)
            {
                subscribedJournal.RoomStateChanged -= OnRoomStateChanged;
                subscribedJournal.DiscoveryStateChanged -= OnDiscoveryStateChanged;
                subscribedJournal.RoomVariantChanged -= OnRoomVariantChanged;
                subscribedJournal.DiscoveryResolutionChanged -= OnDiscoveryResolutionChanged;
            }
            subscribedJournal = null;
            if (animationRoutine != null)
                StopCoroutine(animationRoutine);
            animationRoutine = null;
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
        }
    }
}
