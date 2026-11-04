using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Metroidbrainia
{
    [CreateAssetMenu(menuName = "Metroidbrainia/Television/Broadcast Sequence", fileName = "TVBroadcast")]
    public sealed class TVBroadcastSequence : ScriptableObject
    {
        [Serializable]
        public sealed class Segment
        {
            [SerializeField, Min(0f)] private float startTime;
            [SerializeField, Min(0f)] private float endTime = 1f;
            [SerializeField] private LocalizedString text = new LocalizedString();

            public float StartTime => startTime;
            public float EndTime => endTime;
            public LocalizedString Text => text;
        }

        [SerializeField] private AnimationClip animationClip;
        [SerializeField] private List<Segment> segments = new List<Segment>();

        public AnimationClip AnimationClip => animationClip;
        public IReadOnlyList<Segment> Segments => segments;
        public int Revision { get; private set; }

        public Segment GetSegmentAt(float clipTime)
        {
            if (segments == null)
                return null;
            foreach (Segment segment in segments)
            {
                if (segment == null)
                    continue;
                if (clipTime < segment.StartTime)
                    break;
                // Half-open intervals allow adjacent phrases without showing both at a boundary.
                if (clipTime >= segment.StartTime && clipTime < segment.EndTime)
                    return segment;
            }
            return null;
        }

        public bool TryValidate(out string error)
        {
            if (animationClip == null || !IsFinite(animationClip.length) || animationClip.length <= 0f)
            {
                error = "Assign an Animation Clip with a positive duration.";
                return false;
            }
            return ValidateSegments(animationClip.length, out error);
        }

        private bool ValidateSegments(float clipLength, out string error)
        {
            error = null;
            if (segments == null)
            {
                error = "The segment list cannot be null. An empty list is valid for a silent broadcast.";
                return false;
            }
            float previousEnd = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                Segment segment = segments[i];
                if (segment == null || !IsFinite(segment.StartTime) || !IsFinite(segment.EndTime)
                    || segment.StartTime < 0f || segment.EndTime <= segment.StartTime)
                    error = $"Segment {i + 1}: use finite times with 0 <= Start Time < End Time.";
                else if (segment.StartTime < previousEnd)
                    error = $"Segment {i + 1}: segments must be ordered and must not overlap.";
                else if (segment.EndTime > clipLength)
                    error = $"Segment {i + 1}: End Time exceeds the clip duration ({clipLength:0.###} seconds).";
                else if (segment.Text == null || segment.Text.IsEmpty)
                    error = $"Segment {i + 1}: assign a localized phrase. Leave gaps by omitting segments.";
                if (error != null)
                    return false;
                previousEnd = segment.EndTime;
            }
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnValidate()
        {
            Revision++;
            // Clip.length is a native API; only validate managed segment data in OnValidate.
            if (!ValidateSegments(float.PositiveInfinity, out string error))
                Debug.LogWarning(error, this);
        }

        [ContextMenu("Validate Sequence")]
        private void ValidateSequence()
        {
            if (!TryValidate(out string error))
                Debug.LogWarning(error, this);
        }
    }
}
