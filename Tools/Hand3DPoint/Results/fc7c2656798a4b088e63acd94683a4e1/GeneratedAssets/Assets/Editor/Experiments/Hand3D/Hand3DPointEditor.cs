using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Metroidbrainia.Hand3DExperiment.Editor
{
    public static class Hand3DPointPreparation
    {
        public const string Folder = "Assets/Experiments/Hand3D/Actions";
        [MenuItem("GameObject/Metroidbrainia/Test Hand 3D/Prepare Point Actions", false, 15)]
        private static void PrepareSelected()
        {
            var rig = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Hand3DRig>() : null;
            if (rig == null) throw new InvalidOperationException("Select the TestArmScene rig.");
            Prepare(rig);
        }
        public static void Prepare(Hand3DRig rig)
        {
            Hand3DPoseLibrary.Guard(rig);
            var movement = rig.GetComponent<Hand3DArmMovement>();var actions = rig.GetComponent<Hand3DPoseActions>();
            var finger = rig.Fingers.Single(f => f.Finger == Finger.Index);
            Transform distal = finger.Joints.Last().Bone;
            Vector3 offset = FitTip(rig, distal, finger.Joints[finger.Joints.Length - 2].Bone);
            var adapter = rig.GetComponent<Hand3DPointInteractor>() ?? Undo.AddComponent<Hand3DPointInteractor>(rig.gameObject);
            var data = new SerializedObject(adapter);
            data.FindProperty("pointFingertip").objectReferenceValue = distal;data.FindProperty("fingertipOffset").vector3Value = offset;
            var door = rig.GetComponent<Hand3DDoorInteractor>();var reference = new SerializedObject(door);
            var detection = data.FindProperty("detection");
            detection.FindPropertyRelative("worldCamera").objectReferenceValue = door.WorldCamera;
            detection.FindPropertyRelative("handGripPoint").objectReferenceValue = distal;
            detection.FindPropertyRelative("armDisplay").objectReferenceValue = reference.FindProperty("armDisplay").objectReferenceValue;
            detection.FindPropertyRelative("worldDisplay").objectReferenceValue = reference.FindProperty("worldDisplay").objectReferenceValue;
            detection.FindPropertyRelative("interactionLayers").intValue = reference.FindProperty("interactionLayers").intValue;
            var original = movement.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FirstPersonArmController>(true)).SingleOrDefault();
            if (original != null)
            {
                var old = new SerializedObject(original);data.FindProperty("maximumDistance").floatValue = old.FindProperty("interactionDistance").floatValue;
                detection.FindPropertyRelative("interactionLayers").intValue = old.FindProperty("interactionLayers").intValue;
            }
            var targets = rig.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(c => c is IHandActionInteractable).OrderBy(c => Path(c.transform),StringComparer.Ordinal).ToArray();
            var entries = targets.SelectMany(t => t.GetComponentsInChildren<Collider>(true).Where(c => ReferenceEquals(c.GetComponentInParent<IHandActionInteractable>(),t))
                .Select(c => (target:t, collider:c))).ToArray();
            var bindings = data.FindProperty("targets");bindings.arraySize = entries.Length;
            for(int i=0;i<entries.Length;i++)
            {
                var binding=bindings.GetArrayElementAtIndex(i);binding.FindPropertyRelative("receiver").objectReferenceValue=entries[i].target;
                binding.FindPropertyRelative("collider").objectReferenceValue=entries[i].collider;
                binding.FindPropertyRelative("interactionPoint").objectReferenceValue=null;binding.FindPropertyRelative("tolerancePixels").floatValue=0f;
                var reach=new SerializedObject(entries[i].target).FindProperty("maxInteractionDistance");
                if(reach==null)throw new InvalidOperationException("Set a target's explicit reach before using this preparation helper: "+entries[i].target.name);
                binding.FindPropertyRelative("maximumDistance").floatValue=reach.floatValue;
            }
            data.ApplyModifiedProperties();Hand3DEditing.Save(adapter);
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Experiments/Hand3D","Actions");
            string clickPath=Folder+"/TestArmPointClick.asset",worldPath=Folder+"/TestArmPointWorld.asset";
            var click=AssetDatabase.LoadAssetAtPath<Hand3DPointClickAction>(clickPath);
            if(click==null)
            {
                click=ScriptableObject.CreateInstance<Hand3DPointClickAction>();var a=new SerializedObject(actions);
                var config=new SerializedObject(click);config.FindProperty("duration").floatValue=a.FindProperty("pointClickDuration").floatValue;
                config.FindProperty("amplitude").floatValue=a.FindProperty("pointClickAmplitude").floatValue;config.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(click,clickPath);
            }
            var world=AssetDatabase.LoadAssetAtPath<Hand3DPointWorldAction>(worldPath);
            if(world==null)
            {
                world=ScriptableObject.CreateInstance<Hand3DPointWorldAction>();var a=new SerializedObject(world);
                a.FindProperty("allowedModes").intValue=(int)Hand3DActionModes.Exploration;a.FindProperty("requiresArmControl").boolValue=true;
                a.FindProperty("affectsWorld").boolValue=true;a.FindProperty("priority").intValue=10;a.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.CreateAsset(world,worldPath);
            }
            // Preserve existing bindings/events. Re-running preparation never duplicates environmental actions.
            var actionData=new SerializedObject(actions);var list=actionData.FindProperty("bindings");int index=-1;
            for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).FindPropertyRelative("gesture").intValue==29)
            {if(index>=0)throw new InvalidOperationException("Duplicate Point bindings must be resolved first.");index=i;}
            if(index<0)
            {
                index=list.arraySize++;var entry=list.GetArrayElementAtIndex(index);entry.FindPropertyRelative("gesture").intValue=29;
                entry.FindPropertyRelative("invokeOriginalGestureEvent").boolValue=true;
                entry.FindPropertyRelative("onEvent").FindPropertyRelative("m_PersistentCalls").FindPropertyRelative("m_Calls").arraySize=0;
                entry.FindPropertyRelative("actions").arraySize=1;entry.FindPropertyRelative("actions").GetArrayElementAtIndex(0).objectReferenceValue=click;
            }
            var definitions=list.GetArrayElementAtIndex(index).FindPropertyRelative("actions");
            if(!Enumerable.Range(0,definitions.arraySize).Any(i=>definitions.GetArrayElementAtIndex(i).objectReferenceValue is Hand3DPointClickAction))
                definitions.GetArrayElementAtIndex(definitions.arraySize++).objectReferenceValue=click;
            if(!Enumerable.Range(0,definitions.arraySize).Any(i=>definitions.GetArrayElementAtIndex(i).objectReferenceValue is Hand3DPointWorldAction))
                definitions.GetArrayElementAtIndex(definitions.arraySize++).objectReferenceValue=world;
            actionData.ApplyModifiedProperties();Hand3DEditing.Save(actions);
            Debug.Log("[Hand3D Point] "+entries.Length+" collider bindings. Fingertip "+Path(distal)+" + "+offset+". Exact visual contact; gameplay targets unchanged.",adapter);
        }
        private static Vector3 FitTip(Hand3DRig rig,Transform distal,Transform previous)
        {
            var skin=rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s=>Array.IndexOf(s.bones,distal)>=0);
            int bone=Array.IndexOf(skin.bones,distal);var weights=skin.sharedMesh.boneWeights;var mesh=new Mesh();
            try
            {
                skin.BakeMesh(mesh,true);var vertices=mesh.vertices;Vector3 axis=(distal.position-previous.position).normalized;
                var points=vertices.Select((v,i)=>(v:skin.transform.TransformPoint(v),w:Weight(weights[i],bone))).Where(p=>p.w>=.99f).Select(p=>p.v).ToArray();
                if(points.Length==0)throw new InvalidOperationException("No fully distal-weighted index geometry found; configure PointFingertip manually.");
                float min=points.Min(p=>Vector3.Dot(p-distal.position,axis)),max=points.Max(p=>Vector3.Dot(p-distal.position,axis));
                var cap=points.Where(p=>Vector3.Dot(p-distal.position,axis)>=max-Mathf.Max(.000001f,(max-min)*.02f)).ToArray();
                Vector3 center=Vector3.zero;foreach(var point in cap)center+=point;return distal.InverseTransformPoint(center/cap.Length);
            }
            finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        private static float Weight(BoneWeight w,int bone)=>(w.boneIndex0==bone?w.weight0:0)+(w.boneIndex1==bone?w.weight1:0)+(w.boneIndex2==bone?w.weight2:0)+(w.boneIndex3==bone?w.weight3:0);
        public static string Path(Transform t){string path=t.name;while(t.parent!=null){t=t.parent;path=t.name+"/"+path;}return path;}
    }
    [CustomEditor(typeof(Hand3DPointInteractor))]
    public sealed class Hand3DPointInteractorEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint()=>Application.isPlaying;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var adapter=(Hand3DPointInteractor)target;
            EditorGUILayout.HelpBox("Shared IHandActionInteractable receivers only. Bind one or more colliders per receiver. Zero tolerance = actual collider under the displayed fingertip. Copy each original reach; the target revalidates its own rules. No input, grab pose or light/puzzle state lives here.",MessageType.Info);
            if(Application.isPlaying)EditorGUILayout.HelpBox(adapter.Diagnostic,MessageType.Info);
        }
    }
}
