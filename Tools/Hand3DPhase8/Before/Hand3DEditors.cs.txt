using System.Collections.Generic;
using Metroidbrainia.Hand3DExperiment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment.Editor
{
    internal static class Hand3DEditing
    {
        internal static bool CanEdit(Component component)
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && component != null
                && !EditorUtility.IsPersistent(component) && component.gameObject.scene.name == "TestArmScene";
        }

        internal static bool ValidBone(Component owner, Transform bone)
        {
            return bone != null && bone != owner.transform && !EditorUtility.IsPersistent(bone)
                && bone.gameObject.scene == owner.gameObject.scene && bone.IsChildOf(owner.transform);
        }

        internal static void Save(Component component)
        {
            EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        }

        internal static bool PreviewFinger(Hand3DRig rig, SerializedProperty binding, bool flexed)
        {
            SerializedProperty joints = binding.FindPropertyRelative("joints");
            for (int j = 0; j < joints.arraySize; j++)
            {
                SerializedProperty joint = joints.GetArrayElementAtIndex(j);
                var bone = joint.FindPropertyRelative("bone").objectReferenceValue as Transform;
                string flag = flexed ? "flexedCaptured" : "extendedCaptured";
                if (!ValidBone(rig, bone) || !joint.FindPropertyRelative(flag).boolValue)
                    return false;
            }
            for (int j = 0; j < joints.arraySize; j++)
            {
                SerializedProperty joint = joints.GetArrayElementAtIndex(j);
                var bone = (Transform)joint.FindPropertyRelative("bone").objectReferenceValue;
                Undo.RecordObject(bone, "Preview experimental finger");
                bone.localRotation = Quaternion.Euler(joint.FindPropertyRelative(
                    flexed ? "flexedLocalEulerAngles" : "extendedLocalEulerAngles").vector3Value);
                Save(bone);
            }
            SceneView.RepaintAll();
            return true;
        }

        [MenuItem("GameObject/Metroidbrainia/Test Hand 3D/Add Experimental Controllers", false, 10)]
        private static void AddControllers()
        {
            GameObject owner = Selection.activeGameObject;
            if (owner == null || owner.scene.name != "TestArmScene" || EditorUtility.IsPersistent(owner)
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Select the armsFBX scene instance in TestArmScene, outside Play Mode.");
                return;
            }
            int group = Undo.GetCurrentGroup();
            var state = Add<Hand3DFingerState>(owner);
            var recognizer = Add<Hand3DPoseRecognizer>(owner);
            var actions = Add<Hand3DPoseActions>(owner);
            var movement = Add<Hand3DArmMovement>(owner);
            var modes = Add<Hand3DModeController>(owner);
            if (!modes.Exploration.Captured && Hand3DModeEditing.Ready(movement))
                Hand3DModeEditing.Capture(modes, movement, "exploration");
            var rig = Add<Hand3DRig>(owner);
            var input = Add<Hand3DInputController>(owner);
            Assign(recognizer, "fingerState", state);
            Assign(actions, "poseRecognizer", recognizer);
            Assign(rig, "fingerState", state);
            Assign(rig, "poseRecognizer", recognizer);
            Assign(rig, "poseActions", actions);
            Assign(input, "fingerState", state);
            Assign(input, "rig", rig);
            Assign(input, "armMovement", movement);
            Assign(input, "poseActions", actions);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = owner;
        }

        private static T Add<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(owner);
        }

        private static void Assign(Component target, string property, Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(property).objectReferenceValue = value;
            data.ApplyModifiedProperties();
            Save(target);
        }
    }

    [CustomEditor(typeof(Hand3DRig))]
    public sealed class Hand3DRigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var rig = (Hand3DRig)target;
            if(Application.isPlaying)EditorGUILayout.LabelField("Effective pose backend",rig.UniversalActive?"Universal":"Legacy");
            if(!string.IsNullOrEmpty(rig.UniversalError))EditorGUILayout.HelpBox(rig.UniversalError,MessageType.Error);
            if (!Hand3DEditing.CanEdit(rig))
            {
                EditorGUILayout.HelpBox("Capture/preview is available only on scene instances in TestArmScene, outside Play Mode.", MessageType.Info);
                return;
            }
            EditorGUILayout.HelpBox("Assign each finger's joints. Pose them in the Scene/Transform Inspector, then capture Extended and Flexed. These buttons record scene overrides, never the FBX asset.", MessageType.Info);
            serializedObject.Update();
            SerializedProperty fingers = serializedObject.FindProperty("fingers");
            for (int i = 0; i < fingers.arraySize; i++)
            {
                SerializedProperty binding = fingers.GetArrayElementAtIndex(i);
                Finger finger = (Finger)binding.FindPropertyRelative("finger").enumValueIndex;
                EditorGUILayout.LabelField(finger.ToString(), EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Capture Extended")) Capture(rig, binding, false);
                if (GUILayout.Button("Capture Flexed")) Capture(rig, binding, true);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Preview Extended")) Hand3DEditing.PreviewFinger(rig, binding, false);
                if (GUILayout.Button("Preview Flexed")) Hand3DEditing.PreviewFinger(rig, binding, true);
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Preview All Extended"))
                for (int i = 0; i < fingers.arraySize; i++)
                    Hand3DEditing.PreviewFinger(rig, fingers.GetArrayElementAtIndex(i), false);
        }

        private void Capture(Hand3DRig rig, SerializedProperty binding, bool flexed)
        {
            SerializedProperty joints = binding.FindPropertyRelative("joints");
            for (int i = 0; i < joints.arraySize; i++)
                if (!Hand3DEditing.ValidBone(rig, joints.GetArrayElementAtIndex(i).FindPropertyRelative("bone").objectReferenceValue as Transform))
                {
                    Debug.LogWarning("Assign every joint to a bone below this scene instance before capturing.", rig);
                    return;
                }
            Undo.RecordObject(rig, "Capture experimental finger");
            for (int i = 0; i < joints.arraySize; i++)
            {
                SerializedProperty joint = joints.GetArrayElementAtIndex(i);
                var bone = (Transform)joint.FindPropertyRelative("bone").objectReferenceValue;
                joint.FindPropertyRelative(flexed ? "flexedLocalEulerAngles" : "extendedLocalEulerAngles").vector3Value = bone.localEulerAngles;
                joint.FindPropertyRelative(flexed ? "flexedCaptured" : "extendedCaptured").boolValue = true;
            }
            serializedObject.ApplyModifiedProperties();
            Hand3DEditing.Save(rig);
        }
    }

    [CustomEditor(typeof(Hand3DPoseRecognizer))]
    public sealed class Hand3DPoseRecognizerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var recognizer = (Hand3DPoseRecognizer)target;
            if (EditorApplication.isPlaying)
                EditorGUILayout.LabelField("Recognized pose", recognizer.CurrentPose != null ? recognizer.CurrentPose.Name : "None");
            if (!Hand3DEditing.CanEdit(recognizer)) return;
            var rig = recognizer.GetComponent<Hand3DRig>();
            EditorGUILayout.HelpBox("Preview the base combination, adjust the bones, then capture. Changed finger joints are added automatically; other bones must be assigned explicitly in Visual Overrides. Save the scene after capture.", MessageType.Info);
            serializedObject.Update();
            SerializedProperty poses = serializedObject.FindProperty("poses");
            for (int i = 0; i < poses.arraySize; i++)
            {
                SerializedProperty pose = poses.GetArrayElementAtIndex(i);
                EditorGUILayout.LabelField(pose.FindPropertyRelative("poseName").stringValue, EditorStyles.boldLabel);
                if (GUILayout.Button("Capture Exploration Wrist (separate from finger overrides)"))
                    Hand3DModeEditing.Wrist(recognizer, serializedObject, pose, true);
                if (GUILayout.Button("Preview Exploration Wrist"))
                    Hand3DModeEditing.Wrist(recognizer, serializedObject, pose, false);
                if (rig != null && GUILayout.Button("Preview Base Finger Combination"))
                {
                    var data = new SerializedObject(rig);
                    SerializedProperty fingers = data.FindProperty("fingers");
                    int mask = pose.FindPropertyRelative("flexedFingers").intValue;
                    for (int j = 0; j < fingers.arraySize; j++)
                    {
                        SerializedProperty finger = fingers.GetArrayElementAtIndex(j);
                        int bit = 1 << finger.FindPropertyRelative("finger").enumValueIndex;
                        Hand3DEditing.PreviewFinger(rig, finger, (mask & bit) != 0);
                    }
                }
                if (GUILayout.Button("Capture Visual Overrides"))
                    CaptureOverrides(recognizer, rig, pose);
                SerializedProperty overrides = pose.FindPropertyRelative("visualOverrides");
                EditorGUILayout.LabelField("Stored visual overrides", overrides.arraySize.ToString());
                if (GUILayout.Button("Preview Captured Visual Overrides"))
                {
                    for (int j = 0; j < overrides.arraySize; j++)
                    {
                        SerializedProperty entry = overrides.GetArrayElementAtIndex(j);
                        var bone = entry.FindPropertyRelative("bone").objectReferenceValue as Transform;
                        if (!Hand3DEditing.ValidBone(recognizer, bone) || !entry.FindPropertyRelative("captured").boolValue)
                            continue;
                        Undo.RecordObject(bone, "Preview experimental pose override");
                        bone.localRotation = Quaternion.Euler(entry.FindPropertyRelative("localEulerAngles").vector3Value);
                        Hand3DEditing.Save(bone);
                    }
                    SceneView.RepaintAll();
                }
            }
        }

        private void CaptureOverrides(Hand3DPoseRecognizer recognizer, Hand3DRig rig, SerializedProperty pose)
        {
            SerializedProperty overrides = pose.FindPropertyRelative("visualOverrides");
            var bones = new HashSet<Transform>();
            var movement = recognizer.GetComponent<Hand3DArmMovement>();
            for (int i = 0; i < overrides.arraySize; i++)
            {
                var bone = overrides.GetArrayElementAtIndex(i).FindPropertyRelative("bone").objectReferenceValue as Transform;
                if (!Hand3DEditing.ValidBone(recognizer, bone) || !bones.Add(bone)
                    || movement != null && movement.ControlsRotationOf(bone))
                {
                    Debug.LogWarning("[Hand3D Capture] Assign unique scene bones, excluding the arm bones controlled by movement.", recognizer);
                    return;
                }
            }
            var added = new List<Transform>();
            if (rig != null)
            {
                var combination = (FlexedFingers)pose.FindPropertyRelative("flexedFingers").intValue;
                foreach (Hand3DRig.FingerBinding binding in rig.Fingers)
                {
                    if (binding == null || binding.Joints == null) continue;
                    foreach (Hand3DRig.Joint joint in binding.Joints)
                    {
                        if (joint == null || !joint.Captured || !Hand3DEditing.ValidBone(rig, joint.Bone)
                            || movement != null && movement.ControlsRotationOf(joint.Bone))
                        {
                            Debug.LogWarning("[Hand3D Capture] Configure and capture valid finger joints before capturing pose overrides.", recognizer);
                            return;
                        }
                        Quaternion baseline = (combination & Hand3DFingerState.Mask(binding.Finger)) != 0 ? joint.Flexed : joint.Extended;
                        if (Quaternion.Angle(joint.Bone.localRotation, baseline) > 0.05f && bones.Add(joint.Bone))
                            added.Add(joint.Bone);
                    }
                }
            }
            Undo.RecordObject(recognizer, "Capture experimental pose adjustments");
            foreach (Transform bone in added)
            {
                int index = overrides.arraySize++;
                overrides.GetArrayElementAtIndex(index).FindPropertyRelative("bone").objectReferenceValue = bone;
            }
            for (int i = 0; i < overrides.arraySize; i++)
            {
                SerializedProperty adjustment = overrides.GetArrayElementAtIndex(i);
                var bone = (Transform)adjustment.FindPropertyRelative("bone").objectReferenceValue;
                adjustment.FindPropertyRelative("localEulerAngles").vector3Value = bone.localEulerAngles;
                adjustment.FindPropertyRelative("captured").boolValue = true;
            }
            serializedObject.ApplyModifiedProperties();
            Hand3DEditing.Save(recognizer);
            string message = $"[Hand3D Capture] Pose '{pose.FindPropertyRelative("poseName").stringValue}': stored {overrides.arraySize} local rotations. Save the scene to persist them.";
            if (overrides.arraySize == 0) Debug.LogWarning(message + " No changed finger joints or explicit overrides found.", recognizer);
            else Debug.Log(message, recognizer);
        }
    }
}
