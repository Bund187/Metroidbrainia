using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Metroidbrainia.Hand3DExperiment.Editor
{
    public static class Hand3DDocumentVisualPreparation
    {
        [MenuItem("GameObject/Metroidbrainia/Test Hand 3D/Prepare Document Visuals", false, 14)]
        public static void Prepare()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.name != "TestArmScene") return;
            foreach (var display in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Hand3DDocumentDisplay>(true)))
                PrepareFor(display);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void PrepareFor(Hand3DDocumentDisplay display)
        {
            if (Application.isPlaying || display.gameObject.scene.name != "TestArmScene") return;
            var rig = display.GetComponent<Hand3DRig>();
            var movement = display.GetComponent<Hand3DArmMovement>();
            var settings = new SerializedObject(display);
            var armDisplay = settings.FindProperty("armDisplay").objectReferenceValue as RawImage;
            if (rig == null || movement == null || armDisplay == null) return;
            Transform[] thumb = rig.Fingers.Where(f => f.Finger == Finger.Thumb).SelectMany(f => f.Joints).Select(j => j.Bone).Where(b => b != null).ToArray();
            SkinnedMeshRenderer renderer = null;
            int[] triangles = null;
            foreach (var candidate in display.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (candidate.sharedMesh == null) continue;
                var thumbIndices = new HashSet<int>();
                for (int i = 0; i < candidate.bones.Length; i++)
                    if (candidate.bones[i] != null && thumb.Any(b => candidate.bones[i].IsChildOf(b))) thumbIndices.Add(i);
                if (thumbIndices.Count == 0) continue;
                // Editor-only read. Build runtime only needs BakeMesh plus these serialized triangle indices.
                BoneWeight[] weights = candidate.sharedMesh.boneWeights;
                int[] source = candidate.sharedMesh.triangles;
                var selected = new List<int>();
                for (int i = 0; i < source.Length; i += 3)
                    if (Weight(weights[source[i]], thumbIndices) >= 0.5f
                        && Weight(weights[source[i + 1]], thumbIndices) >= 0.5f
                        && Weight(weights[source[i + 2]], thumbIndices) >= 0.5f)
                    { selected.Add(source[i]); selected.Add(source[i + 1]); selected.Add(source[i + 2]); }
                if (triangles == null || selected.Count > triangles.Length) { renderer = candidate; triangles = selected.ToArray(); }
            }
            if (renderer == null || triangles == null || triangles.Length == 0)
            { Debug.LogWarning("[Hand3D Documents] No thumb-weighted triangles found. Check the right thumb bindings on this rig.", display); return; }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Prepare document thumb stencil");
            var overlay = settings.FindProperty("thumbOverlay").objectReferenceValue as Hand3DThumbOverlay;
            RawImage image;
            if (overlay == null)
            {
                var root = new GameObject("Hand3DThumbOverlay", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(root, "Create thumb stencil");
                Undo.SetTransformParent(root.transform, armDisplay.transform.parent, "Parent thumb stencil");
                root.layer = armDisplay.gameObject.layer;
                var rect = root.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
                overlay = Undo.AddComponent<Hand3DThumbOverlay>(root);
                var child = new GameObject("PixelThumb", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                Undo.RegisterCreatedObjectUndo(child, "Create masked pixel image");
                Undo.SetTransformParent(child.transform, rect, "Parent pixel thumb");
                child.layer = root.layer;
                image = child.GetComponent<RawImage>();
                settings.FindProperty("thumbOverlay").objectReferenceValue = overlay;
                settings.ApplyModifiedProperties();
            }
            else image = overlay.GetComponentInChildren<RawImage>(true);
            Undo.RecordObject(overlay.GetComponent<Mask>(), "Configure thumb stencil");
            overlay.GetComponent<Mask>().showMaskGraphic = false;
            overlay.raycastTarget = false;
            image.raycastTarget = false;
            image.texture = armDisplay.texture;
            var data = new SerializedObject(overlay);
            data.FindProperty("thumbRenderer").objectReferenceValue = renderer;
            data.FindProperty("armCamera").objectReferenceValue = movement.ArmCamera;
            data.FindProperty("armDisplay").objectReferenceValue = armDisplay;
            data.FindProperty("thumbImage").objectReferenceValue = image;
            var indices = data.FindProperty("thumbTriangles"); indices.arraySize = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) indices.GetArrayElementAtIndex(i).intValue = triangles[i];
            data.ApplyModifiedProperties();
            Undo.RecordObject(image.rectTransform, "Align pixel thumb with arm display");
            overlay.SynchronizeImageLayout();
            overlay.gameObject.SetActive(false);
            EditorUtility.SetDirty(overlay);
            Undo.CollapseUndoOperations(group);
            Debug.Log($"[Hand3D Documents] Prepared {triangles.Length / 3} thumb triangles from {renderer.name}. No FBX, cameras or shared materials changed.", display);
        }

        private static float Weight(BoneWeight w, HashSet<int> bones) => (bones.Contains(w.boneIndex0) ? w.weight0 : 0f)
            + (bones.Contains(w.boneIndex1) ? w.weight1 : 0f) + (bones.Contains(w.boneIndex2) ? w.weight2 : 0f) + (bones.Contains(w.boneIndex3) ? w.weight3 : 0f);
    }

    [CustomEditor(typeof(Hand3DThumbOverlay))]
    public sealed class Hand3DThumbOverlayEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var overlay = (Hand3DThumbOverlay)target;
            var image = overlay.ThumbImage;
            var stencil = overlay.GetComponent<Mask>();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Active (hierarchy / component)", $"{overlay.gameObject.activeInHierarchy} / {overlay.enabled}");
            EditorGUILayout.LabelField("Texture assigned", overlay.HasTexture.ToString());
            var texture = overlay.ArmDisplay != null ? overlay.ArmDisplay.texture : null;
            if (texture != null)
            {
                EditorGUILayout.LabelField("Texture size", $"{texture.width} x {texture.height}");
                if (texture is RenderTexture renderTexture)
                    EditorGUILayout.LabelField("RenderTexture created", renderTexture.IsCreated().ToString());
            }
            EditorGUILayout.LabelField("Mask references valid", overlay.HasValidMask.ToString());
            EditorGUILayout.LabelField("Mask enabled / Graphic", $"{stencil.enabled} / {stencil.graphic == overlay}");
            EditorGUILayout.LabelField("Stored thumb triangles", overlay.TriangleCount.ToString());
            EditorGUILayout.LabelField("Generated vertices / triangles", $"{overlay.GeneratedVertexCount} / {overlay.GeneratedTriangleCount}");
            EditorGUILayout.LabelField("Overlay rect", overlay.rectTransform.rect.ToString());
            if (image != null)
            {
                EditorGUILayout.LabelField("PixelThumb active / enabled", $"{image.gameObject.activeInHierarchy} / {image.enabled}");
                EditorGUILayout.LabelField("PixelThumb alpha / culled", $"{image.color.a:F2} / {image.canvasRenderer.cull}");
                EditorGUILayout.LabelField("PixelThumb rect", image.rectTransform.rect.ToString());
                EditorGUILayout.LabelField("PixelThumb shader", image.material != null ? image.material.shader.name : "Missing");
                var material = image.material;
                bool compatible = material != null && material.HasProperty("_Stencil") && material.HasProperty("_StencilComp")
                    && material.HasProperty("_StencilOp") && material.HasProperty("_StencilReadMask")
                    && material.HasProperty("_StencilWriteMask") && material.HasProperty("_ColorMask");
                EditorGUILayout.LabelField("UI stencil shader properties", compatible.ToString());
                if (!compatible) EditorGUILayout.HelpBox("The PixelThumb material does not expose the UI stencil properties.", MessageType.Warning);
            }
            var source = overlay.ArmDisplay;
            if (source != null)
                EditorGUILayout.LabelField("Sibling order (arm / overlay)", $"{source.transform.GetSiblingIndex()} / {overlay.transform.GetSiblingIndex()}");
            EditorGUILayout.HelpBox("Full Arm bypasses the mask. Thumb Mask draws a solid silhouette and hides PixelThumb. Thumb Overlay uses the original arm texture through the stencil. Test while holding a document.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !overlay.isActiveAndEnabled))
                if (GUILayout.Button("Sample Source Pixels (GPU readback once)")) overlay.SampleSourcePixels();
            EditorGUILayout.HelpBox(overlay.PixelDiagnostic, MessageType.Info);
        }

    }

    [CustomEditor(typeof(Hand3DDocumentDisplay))]
    public sealed class Hand3DDocumentDisplayEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Held Document Screen Height is relative to Game height. Tune Hold Offset/Rotation during Play; copy desired values back after Play. Select the hold/grip transforms to adjust their local placement without changing the rig.", MessageType.Info);
            if (GUILayout.Button("Select DocumentHoldPoint")) Selection.activeObject = serializedObject.FindProperty("documentHoldPoint").objectReferenceValue;
            if (GUILayout.Button("Select Paper Grip")) Selection.activeObject = serializedObject.FindProperty("paperGrip").objectReferenceValue;
            if (!Application.isPlaying && GUILayout.Button("Prepare / Rebuild Thumb Stencil")) Hand3DDocumentVisualPreparation.PrepareFor((Hand3DDocumentDisplay)target);
        }
    }
}
