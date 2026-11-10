using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment.Editor
{
    public static class Hand3DRuntimePoseExport
    {
        public const string Folder="Assets/Experiments/Hand3D/RuntimePoses";
        // Explicit migration assignments, never used to recognize gestures or select poses in the player.
        public static readonly string[] FunctionalIds={"Fingers.extended","Fingers.flexed","Gesture.0.OK.Overrides","Gesture.0.OK.Wrist","Gesture.1.Point.Overrides","Gesture.1.Point.Wrist","Mode.exploration","Mode.handEdit","GrabDoor.Joints","GrabDoor.Additional","GrabDocument.Joints","GrabDocument.Additional","GrabDocument.Positions"};
        public static Hand3DRuntimePoses CreateCopy(Hand3DRig rig,Hand3DPoseLibrary source,bool includeUnassigned=false)
        {
            Hand3DPoseLibrary.ValidateBinding(rig,source.Data);
            var data=Hand3DPoseLibrary.Clone(source.Data);
            foreach(string id in FunctionalIds)if(!data.profiles.Any(p=>p.id==id))throw new InvalidOperationException("Explicit functional import missing: "+id);
            if(!includeUnassigned)data.profiles=data.profiles.Where(p=>FunctionalIds.Contains(p.id)).ToArray();
            var result=ScriptableObject.CreateInstance<Hand3DRuntimePoses>();
            result.Configure(data,"Mode.exploration","Mode.handEdit",source.ExtendedId,source.FlexedId,new[]{
                new Hand3DRuntimePoses.Gesture{combination=FlexedFingers.Thumb|FlexedFingers.Index,exploration=new[]{"Gesture.0.OK.Overrides","Gesture.0.OK.Wrist"},handEdit=new[]{"Gesture.0.OK.Overrides"}},
                new Hand3DRuntimePoses.Gesture{combination=FlexedFingers.Thumb|FlexedFingers.Middle|FlexedFingers.Ring|FlexedFingers.Little,exploration=new[]{"Gesture.1.Point.Overrides","Gesture.1.Point.Wrist"},handEdit=new[]{"Gesture.1.Point.Overrides"}}
            },new[]{"GrabDoor.Joints","GrabDoor.Additional"},new[]{"GrabDocument.Joints","GrabDocument.Additional","GrabDocument.Positions"});
            var errors=result.ValidateConfiguration();if(errors.Length>0){UnityEngine.Object.DestroyImmediate(result);throw new InvalidOperationException(string.Join("\n",errors));}
            return result;
        }
        public static void SaveNew(Hand3DRuntimePoses poses,string path)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || !path.StartsWith(Folder+"/",StringComparison.Ordinal) || path.Contains("..") || Path.GetExtension(path)!=".asset")throw new InvalidOperationException("Save a new runtime copy under "+Folder);
            if(File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path)!=null)throw new InvalidOperationException("Existing assets are never overwritten by export.");
            string parent=Path.GetDirectoryName(path).Replace('\\','/'),current="Assets";
            foreach(string part in parent.Substring(7).Split('/')){string child=current+"/"+part;if(!AssetDatabase.IsValidFolder(child))AssetDatabase.CreateFolder(current,part);current=child;}
            AssetDatabase.CreateAsset(poses,path);AssetDatabase.SaveAssetIfDirty(poses);
        }
        public static void ExportDialog(Hand3DRig rig,Hand3DPoseLibrary source,bool includeUnassigned)
        {
            if(!EditorUtility.DisplayDialog("Explicit runtime copy","Create a separate runtime asset with the 13 migrated functional assignments? Unassigned test poses are "+(includeUnassigned?"copied but remain inactive.":"excluded.")+" The Editor library and scene are unchanged.","Create runtime copy","Cancel"))return;
            string path=EditorUtility.SaveFilePanelInProject("New runtime pose copy","TestArmRuntimePoses","asset","Choose a new file under "+Folder,Folder);
            if(string.IsNullOrEmpty(path))return;var copy=CreateCopy(rig,source,includeUnassigned);
            try{SaveNew(copy,path);Selection.activeObject=copy;}catch{UnityEngine.Object.DestroyImmediate(copy);throw;}
        }
    }
    [CustomEditor(typeof(Hand3DRuntimePoses))]
    public sealed class Hand3DRuntimePosesEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Runtime-only data copy. IDs below are explicit assignments; unassigned profiles never activate. Configure in Edit Mode, then select Universal on Hand3DRig.",MessageType.Info);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))DrawDefaultInspector();
            if(GUILayout.Button("Validate runtime assignments"))
            {var errors=((Hand3DRuntimePoses)target).ValidateConfiguration();if(errors.Length==0)Debug.Log("[Hand3D] Runtime assignments valid.",target);else Debug.LogError(string.Join("\n",errors),target);}
        }
    }
}
