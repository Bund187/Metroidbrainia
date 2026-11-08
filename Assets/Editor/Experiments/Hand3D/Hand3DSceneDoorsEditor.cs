using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Metroidbrainia.Hand3DExperiment.Editor
{
    public static class Hand3DSceneDoorsEditor
    {
        [MenuItem("GameObject/Metroidbrainia/Test Hand 3D/Prepare Scene Doors", false, 12)]
        public static void PrepareSceneDoors()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "TestArmScene" || EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("Open TestArmScene outside Play Mode before preparing its door instances."); return; }
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Prepare TestArmScene doors");
            var transforms = new List<Transform>();
            foreach (GameObject root in scene.GetRootGameObjects()) transforms.AddRange(root.GetComponentsInChildren<Transform>(true));
            var report = new StringBuilder("[Hand3D Doors] Scene instance audit/preparation:\n");
            int addedEngines = 0, addedBindings = 0, addedPoints = 0, compatible = 0, incomplete = 0;
            // Only the known legacy hierarchy can be prepared automatically; unknown models are reported.
            foreach (Transform hinge in transforms)
            {
                if (hinge.name != "DoorHinge" || hinge.GetComponent<DoorHandInteractable>() != null) continue;
                Transform knob = Find(hinge, "Knob");
                Collider collider = knob != null ? knob.GetComponent<Collider>() : null;
                if (knob == null || collider == null || !collider.isTrigger)
                { report.AppendLine(hinge.name + ": missing door engine/valid knob trigger; manual configuration needed."); incomplete++; continue; }
                Transform point = Find(knob, "InteractionPoint") ?? Find(knob, "DoorHandleGripPoint");
                if (point == null) { point = CreatePoint(knob); addedPoints++; }
                var door = Undo.AddComponent<DoorHandInteractable>(hinge.gameObject);
                var data = new SerializedObject(door);
                data.FindProperty("doorHinge").objectReferenceValue = hinge;
                data.FindProperty("knob").objectReferenceValue = knob;
                data.FindProperty("interactionPoint").objectReferenceValue = point;
                data.ApplyModifiedProperties();
                Hand3DEditing.Save(door);
                addedEngines++;
            }
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (DoorHandInteractable door in root.GetComponentsInChildren<DoorHandInteractable>(true))
                {
                    var data = new SerializedObject(door);
                    var hinge = data.FindProperty("doorHinge").objectReferenceValue as Transform;
                    var knob = data.FindProperty("knob").objectReferenceValue as Transform;
                    var point = data.FindProperty("interactionPoint").objectReferenceValue as Transform;
                    Collider collider = knob != null ? knob.GetComponent<Collider>() : null;
                    if (point == null && knob != null && collider != null && collider.isTrigger)
                    {
                        point = Find(knob, "InteractionPoint") ?? Find(knob, "DoorHandleGripPoint");
                        if (point == null) { point = CreatePoint(knob); addedPoints++; }
                        data.FindProperty("interactionPoint").objectReferenceValue = point;
                        data.ApplyModifiedProperties();
                        Hand3DEditing.Save(door);
                    }
                    if (hinge == null || knob == null || point == null || collider == null || !collider.isTrigger
                        || !knob.IsChildOf(hinge) || !point.IsChildOf(knob))
                    { report.AppendLine(door.name + ": incomplete hinge/knob/InteractionPoint/trigger; existing properties preserved."); incomplete++; continue; }
                    var binding = door.GetComponent<Hand3DDoorBinding>();
                    if (binding == null) { binding = Undo.AddComponent<Hand3DDoorBinding>(door.gameObject); addedBindings++; }
                    var bindingData = new SerializedObject(binding);
                    if (bindingData.FindProperty("door").objectReferenceValue == null)
                        bindingData.FindProperty("door").objectReferenceValue = door;
                    if (bindingData.FindProperty("doorRoot").objectReferenceValue == null)
                    {
                        var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(door.gameObject);
                        bindingData.FindProperty("doorRoot").objectReferenceValue = instanceRoot != null
                            && instanceRoot.GetComponentsInChildren<DoorHandInteractable>(true).Length == 1 ? instanceRoot.transform : door.transform;
                    }
                    if (bindingData.ApplyModifiedProperties()) Hand3DEditing.Save(binding);
                    compatible++;
                    report.AppendLine((door.transform.parent != null ? door.transform.parent.name : door.name)
                        + ": compatible, " + (data.FindProperty("canOpenByHand").boolValue ? "hand opening enabled" : "locked; original rules preserved"));
                }
            foreach (Transform item in transforms)
                if (item.name.StartsWith("Wall_Door") && item.GetComponentInChildren<DoorHandInteractable>(true) == null)
                    report.AppendLine(item.name + ": no compatible DoorHandInteractable; inspect hinge/knob manually.");
            Undo.CollapseUndoOperations(group);
            if (addedBindings + addedEngines + addedPoints > 0) EditorSceneManager.MarkSceneDirty(scene);
            report.AppendLine($"Compatible: {compatible}; incomplete: {incomplete}; added bindings: {addedBindings}; added engines: {addedEngines}; added points: {addedPoints}. No prefab assets applied; existing opening settings unchanged.");
            Debug.Log(report.ToString());
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true)) if (item.name == name) return item;
            return null;
        }

        private static Transform CreatePoint(Transform knob)
        {
            var point = new GameObject("DoorHandleGripPoint");
            Undo.RegisterCreatedObjectUndo(point, "Create missing door grip point");
            Undo.SetTransformParent(point.transform, knob, "Attach missing door grip point");
            point.transform.localPosition = Vector3.zero;
            point.transform.localRotation = Quaternion.identity;
            point.transform.localScale = Vector3.one;
            return point.transform;
        }
    }
}
