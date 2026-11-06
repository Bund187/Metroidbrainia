using UnityEditor;
using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment.Editor
{
    internal static class Hand3DModeEditing
    {
        internal static bool Ready(Hand3DArmMovement movement) => movement != null && movement.ArmCamera != null
            && movement.UpperArm != null && movement.Forearm != null && movement.Hand != null
            && Hand3DEditing.CanEdit(movement)
            && Hand3DEditing.ValidBone(movement, movement.UpperArm)
            && Hand3DEditing.ValidBone(movement, movement.Forearm)
            && Hand3DEditing.ValidBone(movement, movement.Hand)
            && movement.ArmCamera.gameObject.scene == movement.gameObject.scene
            && !movement.ArmCamera.transform.IsChildOf(movement.transform);

        internal static void Capture(Hand3DModeController modes, Hand3DArmMovement movement, string property)
        {
            if (!Ready(movement)) return;
            Undo.RecordObject(modes, "Capture arm mode configuration");
            ArmFrame frame = movement.CaptureFrame();
            var data = new SerializedObject(modes);
            SerializedProperty config = data.FindProperty(property);
            config.FindPropertyRelative("captured").boolValue = true;
            config.FindPropertyRelative("modelPositionInCamera").vector3Value = frame.position;
            config.FindPropertyRelative("modelEulerInCamera").vector3Value = frame.rotation.eulerAngles;
            config.FindPropertyRelative("upperLocalEuler").vector3Value = frame.upper.eulerAngles;
            config.FindPropertyRelative("forearmLocalEuler").vector3Value = frame.forearm.eulerAngles;
            config.FindPropertyRelative("handEulerInCamera").vector3Value = frame.hand.eulerAngles;
            data.ApplyModifiedProperties();
            Hand3DEditing.Save(modes);
            Debug.Log($"[Hand3D Capture] Captured {property}; finger poses and overrides were preserved. Save TestArmScene.", modes);
        }

        internal static void Preview(Hand3DArmMovement movement, ArmFrame frame, float roll = 0f)
        {
            if (!Ready(movement)) return;
            Undo.RecordObjects(new Object[] { movement.transform, movement.UpperArm, movement.Forearm, movement.Hand }, "Preview arm mode");
            movement.ApplyFrame(frame);
            Quaternion longitudinalRoll = Quaternion.AngleAxis(roll, movement.LongitudinalAxis);
            Quaternion worldRoll = movement.Forearm.rotation * longitudinalRoll * Quaternion.Inverse(movement.Forearm.rotation);
            movement.Forearm.localRotation *= longitudinalRoll;
            movement.Hand.rotation = worldRoll * movement.ArmCamera.transform.rotation * frame.hand;
            foreach (Transform bone in new[] { movement.transform, movement.UpperArm, movement.Forearm, movement.Hand })
                Hand3DEditing.Save(bone);
            SceneView.RepaintAll();
        }

        internal static void Wrist(Hand3DPoseRecognizer recognizer, SerializedObject data, SerializedProperty pose, bool capture)
        {
            var movement = recognizer.GetComponent<Hand3DArmMovement>();
            var modes = recognizer.GetComponent<Hand3DModeController>();
            if (!Ready(movement) || modes == null || !modes.Exploration.Captured)
            {
                Debug.LogWarning("Capture Exploration on Hand3DModeController first. Wrist captures are separate from Visual Overrides.", recognizer);
                return;
            }
            Quaternion baseline = modes.Exploration.Frame.hand;
            if (capture)
            {
                Undo.RecordObject(recognizer, "Capture exploration wrist orientation");
                Quaternion offset = Quaternion.Inverse(baseline) * Quaternion.Inverse(movement.ArmCamera.transform.rotation) * movement.Hand.rotation;
                pose.FindPropertyRelative("explorationWristEuler").vector3Value = offset.eulerAngles;
                pose.FindPropertyRelative("useExplorationWrist").boolValue = true;
                data.ApplyModifiedProperties();
                Hand3DEditing.Save(recognizer);
                Debug.Log($"[Hand3D Capture] Captured Exploration wrist for {pose.FindPropertyRelative("poseName").stringValue}; Visual Overrides unchanged.", recognizer);
            }
            else
            {
                Undo.RecordObject(movement.Hand, "Preview exploration wrist");
                movement.Hand.rotation = movement.ArmCamera.transform.rotation * baseline
                    * Quaternion.Euler(pose.FindPropertyRelative("explorationWristEuler").vector3Value);
                Hand3DEditing.Save(movement.Hand);
                SceneView.RepaintAll();
            }
        }
    }

    [CustomEditor(typeof(Hand3DArmMovement))]
    public sealed class Hand3DArmMovementEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var movement = (Hand3DArmMovement)target;
            if (!Hand3DEditing.CanEdit(movement)) return;
            if (movement.GetComponent<Hand3DModeController>() == null && GUILayout.Button("Add Mode Controller (preserve finger data)"))
            {
                var modes = Undo.AddComponent<Hand3DModeController>(movement.gameObject);
                Hand3DModeEditing.Capture(modes, movement, "exploration");
                Hand3DEditing.Save(modes);
            }
        }
    }

    [CustomEditor(typeof(Hand3DModeController))]
    public sealed class Hand3DModeControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var modes = (Hand3DModeController)target;
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Current mode", modes.Mode.ToString());
                return;
            }
            if (!Hand3DEditing.CanEdit(modes)) return;
            var movement = modes.GetComponent<Hand3DArmMovement>();
            EditorGUILayout.HelpBox("Capture neutral Exploration first. Pose the arm and palm for inspection, then Capture HandEdit: this is the right endpoint (0 degrees). Capture the left endpoint by rolling only the forearm around its longitudinal axis. Restore Preview Exploration and save before Play. Finger data is never captured here.", MessageType.Info);
            if (GUILayout.Button("Capture Exploration")) Hand3DModeEditing.Capture(modes, movement, "exploration");
            if (modes.Exploration.Captured && GUILayout.Button("Preview Exploration")) Hand3DModeEditing.Preview(movement, modes.Exploration.Frame);
            if (GUILayout.Button("Capture HandEdit / Right Limit")) Hand3DModeEditing.Capture(modes, movement, "handEdit");
            if (!modes.HandEdit.Captured) return;
            if (GUILayout.Button("Preview HandEdit / Right Limit")) Hand3DModeEditing.Preview(movement, modes.HandEdit.Frame);
            if (GUILayout.Button("Capture Left Inspection Limit")) CaptureLeft(modes, movement);
            if (GUILayout.Button("Preview Left Inspection Limit"))
            {
                var data = new SerializedObject(modes);
                float roll = data.FindProperty("minimumInspectionAngle").floatValue * data.FindProperty("inspectionAxisSign").floatValue;
                Hand3DModeEditing.Preview(movement, modes.HandEdit.Frame, roll);
            }
        }

        private static void CaptureLeft(Hand3DModeController modes, Hand3DArmMovement movement)
        {
            if (!Hand3DModeEditing.Ready(movement)) return;
            Vector3 axis = movement.LongitudinalAxis;
            Quaternion delta = Quaternion.Inverse(modes.HandEdit.Frame.forearm) * movement.Forearm.localRotation;
            float projected = Vector3.Dot(new Vector3(delta.x, delta.y, delta.z), axis);
            float signed = Mathf.DeltaAngle(0f, 2f * Mathf.Atan2(projected, delta.w) * Mathf.Rad2Deg);
            if (Mathf.Abs(signed) < 0.1f || Quaternion.Angle(delta, Quaternion.AngleAxis(signed, axis)) > 3f)
            {
                Debug.LogWarning("[Hand3D Capture] Roll the forearm only around its longitudinal axis, starting from Preview HandEdit. Do not change elbow bend or wrist/finger rotations.", modes);
                return;
            }
            Undo.RecordObject(modes, "Capture left inspection limit");
            var data = new SerializedObject(modes);
            data.FindProperty("minimumInspectionAngle").floatValue = -Mathf.Abs(signed);
            data.FindProperty("maximumInspectionAngle").floatValue = 0f;
            data.FindProperty("inspectionAxisSign").floatValue = signed > 0f ? -1f : 1f;
            data.ApplyModifiedProperties();
            Hand3DEditing.Save(modes);
            Debug.Log("[Hand3D Capture] Left inspection limit captured; the palm-facing initial frame remains the right endpoint.", modes);
        }
    }
}
