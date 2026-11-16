using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment.Editor
{
    [CustomEditor(typeof(Hand3DInteractionPose))]
    public sealed class Hand3DInteractionPoseEditor : UnityEditor.Editor
    {
        private bool handHold;
        private string OverridesPath => handHold ? "handHoldOverrides" : "grabOverrides";
        private string BonesPath => handHold ? "additionalHandHoldBones" : "additionalGrabBones";
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var pose = (Hand3DInteractionPose)target;
            if (!Hand3DEditing.CanEdit(pose)) return;
            var rig = pose.GetComponent<Hand3DRig>();
            if (rig == null) return;
            EditorGUILayout.HelpBox("Universal: edit/publish GrabDoor and GrabDocument profiles with Pose Editor. Capture/preview buttons here maintain the Legacy backup only. This component's interaction weight, request state and wrist offset are still shared runtime behavior.", MessageType.Info);
            handHold = GUILayout.Toolbar(handHold ? 1 : 0, new[] { "Grab", "HandHold" }) == 1;
            if (handHold && pose.UseHoldPositions && GUILayout.Button("Capture HandHold Finger Position Base")) CapturePositionBase(pose, rig);
            if (handHold && pose.UseHoldPositions && GUILayout.Button("Preview HandHold Finger Position Base"))
                foreach (var entry in pose.HandHoldPositions)
                    if (entry.Bone != null)
                    {
                        Undo.RecordObject(entry.Bone, "Preview HandHold position base");
                        entry.Bone.localPosition = entry.Neutral;
                        Hand3DEditing.Save(entry.Bone);
                    }
            EditorGUILayout.HelpBox("Add wrist.r and/or forearm.Twist0.r / Twist1.r to Additional Grab Bones. Capture their Neutral BEFORE posing, then Capture Grab. forearm.r and the upper arm remain owned by IK. Only local rotations are stored; logical poses and the FBX are preserved.", MessageType.Info);
            if (GUILayout.Button("Capture Additional Neutral (uncaptured bones only)")) CaptureNeutral(pose, rig);
            if (GUILayout.Button("Preview Additional Neutral"))
                foreach (var entry in (handHold ? pose.HandHoldBones : pose.AdditionalBones))
                    if (entry != null && entry.HasNeutral && Hand3DEditing.ValidBone(pose, entry.Bone)) PreviewBone(entry.Bone, entry.Neutral);
            if (GUILayout.Button(handHold ? "Preview HandHold / Flexed Base" : "Preview Grab / Flexed Base"))
            {
                var rigData = new SerializedObject(rig);
                SerializedProperty fingers = rigData.FindProperty("fingers");
                for (int i = 0; i < fingers.arraySize; i++)
                    Hand3DEditing.PreviewFinger(rig, fingers.GetArrayElementAtIndex(i), true);
                foreach (LocalBonePose entry in (handHold ? pose.HandHoldOverrides : pose.Overrides))
                    if (entry != null && entry.Captured && Hand3DEditing.ValidBone(pose, entry.Bone))
                    {
                        Undo.RecordObject(entry.Bone, "Preview Grab");
                        entry.Bone.localRotation = entry.Rotation;
                        Hand3DEditing.Save(entry.Bone);
                    }
                foreach (var entry in (handHold ? pose.HandHoldBones : pose.AdditionalBones))
                    if (entry != null && entry.HasNeutral && Hand3DEditing.ValidBone(pose, entry.Bone))
                        PreviewBone(entry.Bone, entry.Captured ? entry.Grab : entry.Neutral);
                if (handHold && pose.UseHoldPositions)
                    foreach (var entry in pose.HandHoldPositions)
                        if (entry.Bone != null)
                        {
                            Undo.RecordObject(entry.Bone, "Preview HandHold position");
                            entry.Bone.localPosition = entry.Hold;
                            Hand3DEditing.Save(entry.Bone);
                        }
            }
            if (GUILayout.Button(handHold ? "Capture HandHold (preserve logical poses)" : "Capture Grab (preserve logical poses)")) Capture(pose, rig);
        }

        private void Capture(Hand3DInteractionPose pose, Hand3DRig rig)
        {
            if (!ValidateAdditional(pose, rig, true)) return;
            if (handHold && pose.UseHoldPositions && pose.HandHoldPositions.Length == 0)
            { Debug.LogWarning("Capture HandHold Finger Position Base before translating finger joints.", pose); return; }
            var bones = new List<Transform>();
            var movement = rig.GetComponent<Hand3DArmMovement>();
            foreach (Hand3DRig.FingerBinding finger in rig.Fingers)
                foreach (Hand3DRig.Joint joint in finger.Joints)
                {
                    if (joint == null || !joint.Captured || !Hand3DEditing.ValidBone(pose, joint.Bone))
                    {
                        Debug.LogWarning("Configure and capture all finger joints before Grab.", pose);
                        return;
                    }
                    if (!bones.Contains(joint.Bone)) bones.Add(joint.Bone);
                }
            foreach (LocalBonePose entry in (handHold ? pose.HandHoldOverrides : pose.Overrides))
            {
                if (entry == null || !Hand3DEditing.ValidBone(pose, entry.Bone)
                    || movement != null && movement.ControlsRotationOf(entry.Bone))
                {
                    Debug.LogWarning("Grab overrides must reference this rig's bones, excluding the upper arm and forearm.", pose);
                    return;
                }
                if (!bones.Contains(entry.Bone)) bones.Add(entry.Bone);
            }
            Undo.RecordObject(pose, "Capture Grab rotations");
            serializedObject.Update();
            SerializedProperty entries = serializedObject.FindProperty(OverridesPath);
            entries.arraySize = bones.Count;
            for (int i = 0; i < bones.Count; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("bone").objectReferenceValue = bones[i];
                entry.FindPropertyRelative("localEulerAngles").vector3Value = bones[i].localEulerAngles;
                entry.FindPropertyRelative("captured").boolValue = true;
            }
            SerializedProperty additional = serializedObject.FindProperty(BonesPath);
            for (int i = 0; i < additional.arraySize; i++)
            {
                SerializedProperty entry = additional.GetArrayElementAtIndex(i);
                var bone = (Transform)entry.FindPropertyRelative("bone").objectReferenceValue;
                entry.FindPropertyRelative("grabLocalEulerAngles").vector3Value = bone.localEulerAngles;
                entry.FindPropertyRelative("grabCaptured").boolValue = true;
            }
            serializedObject.ApplyModifiedProperties();
            if (handHold && pose.UseHoldPositions)
                foreach (var entry in pose.HandHoldPositions)
                    if (entry.Bone != null) entry.CaptureHold();
            Hand3DEditing.Save(pose);
            Debug.Log($"[Hand3D Capture] {(handHold ? "HandHold" : "Grab")} stored {bones.Count} existing/finger rotations and {additional.arraySize} additional local rotations; logical poses unchanged. Save TestArmScene.", pose);
        }

        private void CapturePositionBase(Hand3DInteractionPose pose, Hand3DRig rig)
        {
            if (pose.HandHoldPositions.Length > 0)
            { Debug.LogWarning("Position base already captured; existing neutral data is preserved.", pose); return; }
            Undo.RecordObject(pose, "Capture HandHold finger position base");
            serializedObject.Update();
            var entries = serializedObject.FindProperty("handHoldPositions");
            var bones = new List<Transform>();
            foreach (var finger in rig.Fingers)
                foreach (var joint in finger.Joints)
                    if (joint.Bone != null && !bones.Contains(joint.Bone)) bones.Add(joint.Bone);
            entries.arraySize = bones.Count;
            for (int i = 0; i < bones.Count; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("bone").objectReferenceValue = bones[i];
                entry.FindPropertyRelative("neutral").vector3Value = bones[i].localPosition;
                entry.FindPropertyRelative("hold").vector3Value = bones[i].localPosition;
            }
            serializedObject.ApplyModifiedProperties();
            Hand3DEditing.Save(pose);
        }

        private static void PreviewBone(Transform bone, Quaternion rotation)
        {
            Undo.RecordObject(bone, "Preview Grab additional bone");
            bone.localRotation = rotation;
            Hand3DEditing.Save(bone);
            SceneView.RepaintAll();
        }

        private bool ValidateAdditional(Hand3DInteractionPose pose, Hand3DRig rig, bool requireNeutral)
        {
            var movement = rig.GetComponent<Hand3DArmMovement>();
            var seen = new HashSet<Transform>();
            foreach (var entry in (handHold ? pose.HandHoldBones : pose.AdditionalBones))
            {
                bool finger = false;
                foreach (var binding in rig.Fingers)
                    foreach (var joint in binding.Joints)
                        if (entry != null && joint.Bone == entry.Bone) finger = true;
                if (entry == null || !Hand3DEditing.ValidBone(pose, entry.Bone) || !seen.Add(entry.Bone) || finger
                    || movement == null || !entry.Bone.IsChildOf(movement.Forearm) || movement.ControlsRotationOf(entry.Bone)
                    || requireNeutral && !entry.HasNeutral)
                {
                    Debug.LogWarning("Use unique wrist/forearm Twist bones, excluding finger joints and the main IK forearm. Capture Additional Neutral before modifying/capturing Grab.", pose);
                    return false;
                }
            }
            return true;
        }

        private void CaptureNeutral(Hand3DInteractionPose pose, Hand3DRig rig)
        {
            if (!ValidateAdditional(pose, rig, false)) return;
            Undo.RecordObject(pose, "Capture additional neutral rotations");
            serializedObject.Update();
            var entries = serializedObject.FindProperty(BonesPath);
            for (int i = 0; i < entries.arraySize; i++)
            {
                if ((handHold ? pose.HandHoldBones : pose.AdditionalBones)[i].HasNeutral) continue;
                var entry = entries.GetArrayElementAtIndex(i);
                var bone = (Transform)entry.FindPropertyRelative("bone").objectReferenceValue;
                entry.FindPropertyRelative("neutralLocalEulerAngles").vector3Value = bone.localEulerAngles;
                entry.FindPropertyRelative("neutralBone").objectReferenceValue = bone;
                entry.FindPropertyRelative("neutralCaptured").boolValue = true;
                entry.FindPropertyRelative("grabCaptured").boolValue = false;
            }
            serializedObject.ApplyModifiedProperties();
            Hand3DEditing.Save(pose);
        }
    }

    [CustomEditor(typeof(Hand3DDoorInteractor))]
    public sealed class Hand3DDoorInteractorEditor : UnityEditor.Editor
    {
        [MenuItem("GameObject/Metroidbrainia/Test Hand 3D/Prepare Door Interaction", false, 11)]
        private static void Prepare()
        {
            var owner = Selection.activeGameObject;
            var movement = owner != null ? owner.GetComponent<Hand3DArmMovement>() : null;
            if (!Hand3DModeEditing.Ready(movement))
            {
                Debug.LogWarning("Select the configured armsFBX instance in TestArmScene, outside Play Mode.");
                return;
            }
            int group = Undo.GetCurrentGroup();
            var pose = owner.GetComponent<Hand3DInteractionPose>() ?? Undo.AddComponent<Hand3DInteractionPose>(owner);
            var interactor = owner.GetComponent<Hand3DDoorInteractor>() ?? Undo.AddComponent<Hand3DDoorInteractor>(owner);
            Transform grip = movement.Hand.Find("HandGripPoint");
            if (grip == null)
            {
                var point = new GameObject("HandGripPoint");
                Undo.RegisterCreatedObjectUndo(point, "Create hand grip point");
                Undo.SetTransformParent(point.transform, movement.Hand, "Attach hand grip point");
                grip = point.transform;
                grip.localPosition = Vector3.zero;
                grip.localRotation = Quaternion.identity;
                grip.localScale = Vector3.one;
            }
            var data = new SerializedObject(interactor);
            AssignIfEmpty(data, "armMovement", movement);
            AssignIfEmpty(data, "input", owner.GetComponent<Hand3DInputController>());
            AssignIfEmpty(data, "grabPose", pose);
            AssignIfEmpty(data, "handGripPoint", grip);
            Camera camera = null;
            foreach (Camera candidate in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == owner.scene && candidate.CompareTag("MainCamera") && candidate != movement.ArmCamera)
                { camera = candidate; break; }
            AssignIfEmpty(data, "worldCamera", camera);
            AssignIfEmpty(data, "armDisplay", Hand3DScreenProjection.FindDisplay(movement.ArmCamera));
            AssignIfEmpty(data, "worldDisplay", Hand3DScreenProjection.FindDisplay(camera));
            data.ApplyModifiedProperties();
            Hand3DEditing.Save(interactor);
            Hand3DEditing.Save(pose);
            Hand3DEditing.Save(grip);
            Undo.CollapseUndoOperations(group);
            Hand3DSceneDoorsEditor.PrepareSceneDoors();
            Selection.activeObject = interactor;
            Debug.Log("[Hand3D Door] Scene-only setup complete. Review RawImage/door root, position HandGripPoint in the palm, capture Grab and save TestArmScene. Existing door/prefab and pose data were preserved.", interactor);
        }

        private static void AssignIfEmpty(SerializedObject data, string name, Object value)
        {
            SerializedProperty property = data.FindProperty(name);
            if (property.objectReferenceValue == null) property.objectReferenceValue = value;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var interactor = (Hand3DDoorInteractor)target;
            if (!Hand3DEditing.CanEdit(interactor)) return;
            EditorGUILayout.HelpBox("All compatible TestArmScene doors are discovered automatically. Opening settings remain on each original DoorHandInteractable; per-handle offsets and frame root live on Hand3DDoorBinding.", MessageType.Info);
            if (GUILayout.Button("Prepare Scene Doors (instances only)")) Hand3DSceneDoorsEditor.PrepareSceneDoors();
        }
    }
}


