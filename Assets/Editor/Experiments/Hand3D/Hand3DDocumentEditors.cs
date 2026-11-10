using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Metroidbrainia.Hand3DExperiment.Editor
{
    public static class Hand3DDocumentPreparation
    {
        [MenuItem("GameObject/Metroidbrainia/Test Hand 3D/Prepare Document Interaction", false, 13)]
        public static void Prepare()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.name != "TestArmScene")
            { Debug.LogWarning("Open TestArmScene outside Play before preparing documents."); return; }
            var movement = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Hand3DArmMovement>(true)).FirstOrDefault();
            var contact = movement != null ? movement.GetComponent<Hand3DDoorInteractor>() : null;
            if (movement == null || movement.Hand == null)
            { Debug.LogWarning("Prepare the experimental arm first."); return; }
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Prepare experimental documents");
            var owner = movement.gameObject;
            Ensure<Hand3DInteractionCoordinator>(owner);
            var pose = Ensure<Hand3DInteractionPose>(owner);
            var display = Ensure<Hand3DDocumentDisplay>(owner);
            var interactor = Ensure<Hand3DDocumentInteractor>(owner);
            Transform point = movement.Hand.Find("DocumentHoldPoint");
            if (point == null)
            {
                var item = new GameObject("DocumentHoldPoint");
                Undo.RegisterCreatedObjectUndo(item, "Create document hold point");
                Undo.SetTransformParent(item.transform, movement.Hand, "Parent hold point");
                point = item.transform;
                point.localPosition = contact != null && contact.HandGripPoint != null ? contact.HandGripPoint.localPosition : Vector3.zero;
                point.localRotation = Quaternion.identity;
            }
            var displayData = new SerializedObject(display);
            Set(displayData, "movement", movement);
            Set(displayData, "documentHoldPoint", point);
            var armDisplay = contact != null ? new SerializedObject(contact).FindProperty("armDisplay").objectReferenceValue as RawImage
                : Hand3DScreenProjection.FindDisplay(movement.ArmCamera);
            Set(displayData, "armDisplay", armDisplay);
            if (displayData.FindProperty("visualRoot").objectReferenceValue == null)
            {
                if (armDisplay == null || !CreateVisual(scene, armDisplay, displayData))
                {
                    Undo.RevertAllDownToGroup(undo);
                    Debug.LogWarning("Could not find the original document UI template in Prototype. Preparation reverted; no original assets were changed.");
                    return;
                }
            }
            displayData.ApplyModifiedProperties();
            var interactionData = new SerializedObject(interactor);
            Set(interactionData, "contact", contact);
            Set(interactionData, "input", owner.GetComponent<Hand3DInputController>());
            Set(interactionData, "movement", movement);
            Set(interactionData, "pose", pose);
            Set(interactionData, "display", display);
            interactionData.ApplyModifiedProperties();
            ConfigureContact(interactor, movement, contact);

            // Seed only the NEW profile from the rig's captured flexed fingers. Existing Grab stays byte-for-byte intact.
            var poseData = new SerializedObject(pose);
            var holds = poseData.FindProperty("handHoldOverrides");
            if (holds.arraySize == 0)
            {
                var joints = owner.GetComponent<Hand3DRig>().Fingers.SelectMany(f => f.Joints).ToArray();
                holds.arraySize = joints.Length;
                for (int i = 0; i < joints.Length; i++)
                {
                    var entry = holds.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("bone").objectReferenceValue = joints[i].Bone;
                    entry.FindPropertyRelative("localEulerAngles").vector3Value = joints[i].Flexed.eulerAngles;
                    entry.FindPropertyRelative("captured").boolValue = true;
                }
                poseData.ApplyModifiedProperties();
            }
            int count = 0;
            foreach (var document in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DocumentHandInteractable>(true)))
            {
                var binding = document.GetComponent<Hand3DDocumentBinding>();
                if (binding == null) { binding = Undo.AddComponent<Hand3DDocumentBinding>(document.gameObject); Sync(binding, document); }
                count++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
            Hand3DDocumentVisualPreparation.PrepareFor(display);
            Debug.Log($"[Hand3D Documents] Prepared {count} scene documents. Adjust DocumentHoldPoint/paper grip and capture HandHold; no shared assets changed.", interactor);
        }

        private static T Ensure<T>(GameObject owner) where T : Component => owner.GetComponent<T>() ?? Undo.AddComponent<T>(owner);
        private static void Set(SerializedObject data, string field, Object value) => data.FindProperty(field).objectReferenceValue = value;

        // Explicit Editor-only migration. Existing scene settings are copied, never guessed from class defaults.
        public static void ConfigureContact(Hand3DDocumentInteractor interactor, Hand3DArmMovement movement, Hand3DDoorInteractor source)
        {
            if (Application.isPlaying || interactor.gameObject.scene.name != "TestArmScene") return;
            var data = new SerializedObject(interactor);
            var detection = data.FindProperty("detection");
            if (detection.FindPropertyRelative("worldCamera").objectReferenceValue != null) return;
            Undo.RecordObject(interactor, "Configure independent document contact");
            if (source != null)
            {
                var original = new SerializedObject(source);
                foreach (string field in new[] { "worldCamera", "handGripPoint", "armDisplay", "worldDisplay" })
                    detection.FindPropertyRelative(field).objectReferenceValue = original.FindProperty(field).objectReferenceValue;
                detection.FindPropertyRelative("gripScreenOffset").vector2Value = original.FindProperty("gripScreenOffset").vector2Value;
                detection.FindPropertyRelative("interactionLayers").intValue = original.FindProperty("interactionLayers").intValue;
            }
            else
            {
                Camera world = interactor.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true))
                    .FirstOrDefault(c => c != movement.ArmCamera && c.CompareTag("MainCamera"));
                Transform grip = movement.Hand.Find("HandGripPoint");
                if (grip == null)
                {
                    var point = new GameObject("HandGripPoint"); Undo.RegisterCreatedObjectUndo(point, "Create visual contact point");
                    point.transform.SetParent(movement.Hand, false); grip = point.transform;
                }
                detection.FindPropertyRelative("worldCamera").objectReferenceValue = world;
                detection.FindPropertyRelative("handGripPoint").objectReferenceValue = grip;
                detection.FindPropertyRelative("armDisplay").objectReferenceValue = Hand3DScreenProjection.FindDisplay(movement.ArmCamera);
                detection.FindPropertyRelative("worldDisplay").objectReferenceValue = Hand3DScreenProjection.FindDisplay(world);
            }
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(interactor);
            EditorUtility.SetDirty(interactor);
            EditorSceneManager.MarkSceneDirty(interactor.gameObject.scene);
        }

        public static void Sync(Hand3DDocumentBinding binding, DocumentHandInteractable document)
        {
            if (document == null || binding.gameObject.scene.name != "TestArmScene" || Application.isPlaying) return;
            Undo.RecordObject(binding, "Copy original document presentation data");
            var source = new SerializedObject(document);
            var destination = new SerializedObject(binding);
            Set(destination, "document", document);
            foreach (string field in new[] { "worldRenderer", "worldCollider", "heldSprite", "localizedText", "maxInteractionDistance", "onPickedUp" })
                destination.CopyFromSerializedProperty(source.FindProperty(field));
            destination.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(binding);
            EditorUtility.SetDirty(binding);
        }

        private static bool CreateVisual(Scene scene, RawImage armDisplay, SerializedObject destination)
        {
            Scene preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Prototype.unity");
            try
            {
                var template = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HeldDocumentDisplay>(true)).FirstOrDefault();
                if (template == null) return false;
                var copy = Object.Instantiate(template.gameObject);
                copy.SetActive(false);
                copy.name = "Hand3DHeldDocument";
                SceneManager.MoveGameObjectToScene(copy, scene);
                Undo.RegisterCreatedObjectUndo(copy, "Create experimental full-resolution document UI");
                var oldDisplay = copy.GetComponent<HeldDocumentDisplay>();
                var data = new SerializedObject(oldDisplay);
                Set(destination, "visualRoot", copy.GetComponent<RectTransform>());
                Set(destination, "documentImage", data.FindProperty("documentImage").objectReferenceValue);
                Set(destination, "documentText", data.FindProperty("documentText").objectReferenceValue);
                Set(destination, "paperGrip", data.FindProperty("documentGripPoint").objectReferenceValue);
                Undo.DestroyObjectImmediate(oldDisplay);
                Undo.SetTransformParent(copy.transform, armDisplay.transform.parent, "Parent document UI");
                copy.transform.SetSiblingIndex(armDisplay.transform.GetSiblingIndex());
                // Keep the original authored size/layout; its position will be aligned by the grip each frame.
                copy.transform.localScale = template.transform.localScale;
                copy.transform.localRotation = Quaternion.identity;
                return true;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
    }

    [CustomEditor(typeof(Hand3DDocumentBinding))]
    public sealed class Hand3DDocumentBindingEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var binding = (Hand3DDocumentBinding)target;
            EditorGUILayout.HelpBox("Original sprite/localization/event data is copied once. Styles use the original document's public properties. Sync after changing the original data; scene instances only.", MessageType.Info);
            if (GUILayout.Button("Sync From Original Document"))
                Hand3DDocumentPreparation.Sync(binding, binding.GetComponent<DocumentHandInteractable>());
        }
    }
}
