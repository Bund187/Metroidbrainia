using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Metroidbrainia.Hand3DExperiment
{
    // Scene-instance metadata and a small active registry; the original door still owns all opening rules.
    [DisallowMultipleComponent]
    public sealed class Hand3DDoorBinding : MonoBehaviour
    {
        [SerializeField] private DoorHandInteractable door;
        [Tooltip("Only this door/frame instance, never the room root.")]
        [SerializeField] private Transform doorRoot;
        [Tooltip("Optional visual contact offset in InteractionPoint's local space; opening geometry is unchanged.")]
        [SerializeField] private Vector3 gripLocalOffset;
        [Tooltip("Additional hand orientation for this handle, relative to the shared Grab wrist pose.")]
        [SerializeField] private Vector3 grabWristEulerOffset;
        private static readonly HashSet<Hand3DDoorBinding> active = new HashSet<Hand3DDoorBinding>();
        public DoorHandInteractable Door => door;
        public bool HasExplicitRoot => doorRoot != null && door != null && door.transform.IsChildOf(doorRoot);
        public Transform DoorRoot => doorRoot != null && door != null && door.transform.IsChildOf(doorRoot) ? doorRoot : door != null ? door.transform : null;
        public Vector3 GripPosition => door.InteractionPoint.TransformPoint(gripLocalOffset);
        public Quaternion WristOffset => Quaternion.Euler(grabWristEulerOffset);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();

        public void Configure(DoorHandInteractable value, Transform root = null)
        {
            door = value;
            if (root != null) doorRoot = root;
            if (isActiveAndEnabled && ExperimentScope.IsAllowed(this)) active.Add(this);
        }

        private void OnEnable()
        {
            if (!ExperimentScope.IsAllowed(this)) return;
            if (door == null) door = GetComponent<DoorHandInteractable>();
            if (door != null) active.Add(this);
        }

        private void OnDisable() => active.Remove(this);
        private void OnDestroy() => active.Remove(this);

        public static void Collect(Scene scene, List<Hand3DDoorBinding> result)
        {
            result.Clear();
            foreach (Hand3DDoorBinding binding in active)
                if (binding != null && binding.isActiveAndEnabled && binding.gameObject.scene == scene
                    && binding.door != null && binding.door.isActiveAndEnabled && binding.door.InteractionPoint != null)
                    result.Add(binding);
        }
    }
}
