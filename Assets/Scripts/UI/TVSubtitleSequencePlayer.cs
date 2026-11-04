using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace Metroidbrainia
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class TVSubtitleSequencePlayer : MonoBehaviour
    {
        [SerializeField] private Animator sourceAnimator;
        [SerializeField] private TVBroadcastSequence sequence;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField, Min(0)] private int animatorLayer;
        [Tooltip("Optional full state path, e.g. Base Layer.TV_News_Test. Empty matches by clip only.")]
        [SerializeField] private string expectedStateFullPath = string.Empty;

        private readonly List<AnimatorClipInfo> clipInfo = new List<AnimatorClipInfo>(2);
        private TVBroadcastSequence validatedSequence;
        private int validatedRevision = -1;
        private bool sequenceValid;
        private string cachedStatePath;
        private int expectedStateHash;
        private TVBroadcastSequence boundSequence;
        private int boundRevision = -1;
        private TVBroadcastSequence.Segment boundSegment;
        private LocalizedString boundString;
        private LocalizedString.ChangeHandler stringHandler;
        private uint bindingGeneration;
        private LocalizeStringEvent previousLocalizer;
        private bool previousLocalizerWasEnabled;

        private void OnEnable()
        {
            validatedSequence = null;
            validatedRevision = -1;
            sequenceValid = false;
            // The former static-message localizer must not write over timed phrases or silent gaps.
            if (subtitleText != null)
            {
                previousLocalizer = subtitleText.GetComponent<LocalizeStringEvent>();
                previousLocalizerWasEnabled = previousLocalizer != null && previousLocalizer.enabled;
                if (previousLocalizerWasEnabled)
                    previousLocalizer.enabled = false;
            }
            Unbind();
        }

        private void LateUpdate()
        {
            ValidateConfiguration();
            TVBroadcastSequence.Segment segment = TryGetClipTime(out float time) ? sequence.GetSegmentAt(time) : null;
            SetSegment(segment);
        }

        private void ValidateConfiguration()
        {
            if (sequence == null)
            {
                validatedSequence = null;
                sequenceValid = false;
                return;
            }
            if (sequence == validatedSequence && sequence.Revision == validatedRevision)
                return;
            validatedSequence = sequence;
            validatedRevision = sequence.Revision;
            sequenceValid = sequence.TryValidate(out string error);
            if (!sequenceValid)
                Debug.LogWarning($"TV subtitles disabled: {error}", this);
        }

        private bool TryGetClipTime(out float clipTime)
        {
            clipTime = 0f;
            if (!sequenceValid || sequence == null || sequence != validatedSequence
                || sequence.Revision != validatedRevision || subtitleText == null
                || sourceAnimator == null || !sourceAnimator.isActiveAndEnabled
                || sourceAnimator.runtimeAnimatorController == null || !sourceAnimator.isInitialized
                || animatorLayer < 0 || animatorLayer >= sourceAnimator.layerCount
                || sourceAnimator.IsInTransition(animatorLayer)
                || animatorLayer > 0 && sourceAnimator.GetLayerWeight(animatorLayer) <= 0f)
                return false;

            if (cachedStatePath != expectedStateFullPath)
            {
                cachedStatePath = expectedStateFullPath;
                expectedStateHash = string.IsNullOrEmpty(cachedStatePath) ? 0 : Animator.StringToHash(cachedStatePath);
            }
            AnimatorStateInfo state = sourceAnimator.GetCurrentAnimatorStateInfo(animatorLayer);
            if (!string.IsNullOrEmpty(cachedStatePath) && state.fullPathHash != expectedStateHash)
                return false;
            clipInfo.Clear();
            sourceAnimator.GetCurrentAnimatorClipInfo(animatorLayer, clipInfo);
            // A blend tree has no single unambiguous clip clock; remain silent in that case.
            if (clipInfo.Count != 1 || clipInfo[0].clip != sequence.AnimationClip || clipInfo[0].weight <= 0f)
                return false;
            float normalizedTime = state.normalizedTime;
            if (float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime))
                return false;
            float progress = state.loop ? Mathf.Repeat(normalizedTime, 1f) : Mathf.Clamp01(normalizedTime);
            // normalizedTime already includes Animator/state speed and pauses. Use clip seconds, not a separate timer.
            clipTime = progress * sequence.AnimationClip.length;
            return true;
        }

        private void SetSegment(TVBroadcastSequence.Segment segment)
        {
            LocalizedString text = segment != null ? segment.Text : null;
            int revision = sequence != null ? sequence.Revision : -1;
            if (boundSequence == sequence && boundRevision == revision
                && boundSegment == segment && ReferenceEquals(boundString, text))
                return;
            Unbind();
            boundSequence = sequence;
            boundRevision = revision;
            boundSegment = segment;
            if (segment == null || text == null || text.IsEmpty)
                return;

            boundString = text;
            uint generation = bindingGeneration;
            TVBroadcastSequence owner = sequence;
            stringHandler = value =>
            {
                if (!isActiveAndEnabled || generation != bindingGeneration || sequence != owner
                    || owner.Revision != revision || boundSegment != segment || !ReferenceEquals(boundString, text))
                    return;
                // Localization may finish before this frame's LateUpdate, after the phrase has already ended.
                if (!TryGetClipTime(out float time) || owner.GetSegmentAt(time) != segment)
                    return;
                SetText(value);
            };
            boundString.StringChanged += stringHandler;
        }

        private void Unbind()
        {
            bindingGeneration++;
            if (boundString != null && stringHandler != null)
                boundString.StringChanged -= stringHandler;
            boundString = null;
            stringHandler = null;
            boundSequence = null;
            boundRevision = -1;
            boundSegment = null;
            SetText(string.Empty);
        }

        private void SetText(string value)
        {
            value = value ?? string.Empty;
            if (subtitleText != null && subtitleText.text != value)
                subtitleText.text = value;
        }

        private void OnDisable()
        {
            Unbind();
            if (previousLocalizer != null && previousLocalizerWasEnabled)
                previousLocalizer.enabled = true;
            previousLocalizer = null;
            previousLocalizerWasEnabled = false;
        }
    }
}
