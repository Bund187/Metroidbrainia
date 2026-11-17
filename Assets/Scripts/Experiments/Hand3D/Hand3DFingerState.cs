using System;
using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment
{
    public enum Finger { Thumb, Index, Middle, Ring, Little }

    [Flags]
    public enum FlexedFingers
    {
        None = 0, Thumb = 1, Index = 2, Middle = 4, Ring = 8, Little = 16
    }

    [DisallowMultipleComponent]
    public sealed class Hand3DFingerState : MonoBehaviour
    {
        public FlexedFingers Current { get; private set; }
        public event Action Changed;

        public bool IsFlexed(Finger finger) => (Current & Mask(finger)) != 0;

        public void Toggle(Finger finger) => SetFlexed(finger, !IsFlexed(finger));

        public void SetFlexed(Finger finger, bool flexed)
        {
            if (finger < Finger.Thumb || finger > Finger.Little)
                return;
            FlexedFingers mask = Mask(finger);
            FlexedFingers next = flexed ? Current | mask : Current & ~mask;
            if (next == Current)
                return;
            Current = next;
            Changed?.Invoke();
        }

        public void ExtendAll()
        {
            if (Current == FlexedFingers.None)
                return;
            Current = FlexedFingers.None;
            Changed?.Invoke();
        }

        public static FlexedFingers Mask(Finger finger) => (FlexedFingers)(1 << (int)finger);
    }

    internal static class ExperimentScope
    {
        private const string TestScenePath = "Assets/Scenes/TestArmScene.unity";
        private const string IntegrationScenePath = "Assets/Scenes/Prototype_3DIntegration.unity";

        internal static bool IsAllowed(Component component)
        {
            if (component == null) return false;
            // Use the object's owning scene, independently of the active additive scene.
            string scenePath = component.gameObject.scene.path;
            return scenePath == TestScenePath || scenePath == IntegrationScenePath;
        }
    }
}
