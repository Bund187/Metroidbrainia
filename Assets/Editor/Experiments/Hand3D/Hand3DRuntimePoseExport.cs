using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment.Editor
{
    public static class Hand3DRuntimePoseExport
    {
        public const string Folder="Assets/Experiments/Hand3D/RuntimePoses";
        // Explicit migration assignments, never used to recognize gestures or select poses in the player.
        public static readonly string[] FunctionalIds={"Fingers.extended","Fingers.flexed","Gesture.0.OK.Overrides","Gesture.0.OK.Wrist","Gesture.1.Point.Overrides","Gesture.1.Point.Wrist","Mode.exploration","Mode.handEdit","GrabDoor.Joints","GrabDoor.Additional","GrabDocument.Joints","GrabDocument.Additional","GrabDocument.Positions"};
        public static Hand3DRuntimePoses CreateCopy(Hand3DRig rig,Hand3DPoseLibrary source)
        {
            Hand3DPoseLibrary.ValidateBinding(rig,source.Data);
            var data=Hand3DPoseLibrary.Clone(source.Data);
            foreach(string id in FunctionalIds)if(!data.profiles.Any(p=>p.id==id))throw new InvalidOperationException("Explicit functional import missing: "+id);
            data.profiles=data.profiles.Where(p=>FunctionalIds.Contains(p.id)).ToArray();
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
        public static void ExportDialog(Hand3DRig rig,Hand3DPoseLibrary source)
        {
            if(!EditorUtility.DisplayDialog("Explicit runtime copy","Create a separate runtime asset with the 13 migrated functional assignments? Unassigned profiles are excluded; publish additional profiles only through explicit selection. The Editor library and scene are unchanged.","Create runtime copy","Cancel"))return;
            string path=EditorUtility.SaveFilePanelInProject("New runtime pose copy","TestArmRuntimePoses","asset","Choose a new file under "+Folder,Folder);
            if(string.IsNullOrEmpty(path))return;var copy=CreateCopy(rig,source);
            try{SaveNew(copy,path);Selection.activeObject=copy;}catch{UnityEngine.Object.DestroyImmediate(copy);throw;}
        }

        // Review and commit share the same validation. The commit rebuilds the candidate rather
        // than trusting a preview that may have become stale while a confirmation dialog was open.
        public static string ReviewSelected(Hand3DRig rig,Hand3DPoseLibrary source,Hand3DRuntimePoses target,string[] selected)
        {
            var candidate=BuildSelected(rig,source,target,selected);
            try
            {
                return AssetDatabase.GetAssetPath(target)+"\n\n"+string.Join("\n",selected.Select(id=>
                    (target.Find(id)==null?"ADD":JsonUtility.ToJson(target.Find(id))==JsonUtility.ToJson(candidate.Find(id))?"UNCHANGED":"UPDATE")+"  "+id))
                    +"\n\nOnly selected profiles are published. Existing assignments and other profiles are preserved. New profiles remain inactive until explicitly assigned in the runtime asset or an action. Undo is available; save the runtime asset again after Undo/Redo.";
            }
            finally{UnityEngine.Object.DestroyImmediate(candidate);}
        }
        public static void PublishSelected(Hand3DRig rig,Hand3DPoseLibrary source,Hand3DRuntimePoses target,string[] selected)
        {
            var candidate=BuildSelected(rig,source,target,selected);
            try
            {
                Undo.IncrementCurrentGroup();Undo.RegisterCompleteObjectUndo(target,"Publish selected universal hand profiles");
                target.Configure(candidate.Data,target.Exploration,target.HandEdit,target.Extended,target.Flexed,target.Gestures,target.GrabDoor,target.GrabDocument);
                EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target);
            }
            finally{UnityEngine.Object.DestroyImmediate(candidate);}
        }
        private static Hand3DRuntimePoses BuildSelected(Hand3DRig rig,Hand3DPoseLibrary source,Hand3DRuntimePoses target,string[] selected)
        {
            Hand3DPoseLibrary.Guard(rig);
            if(source==null || source.Data==null || !EditorUtility.IsPersistent(source) || EditorUtility.IsDirty(source))
                throw new InvalidOperationException("Save the Editor library before publishing.");
            string path=AssetDatabase.GetAssetPath(target);
            if(target==null || !path.StartsWith(Folder+"/",StringComparison.Ordinal) || !EditorUtility.IsPersistent(target))
                throw new InvalidOperationException("Select an existing runtime asset under "+Folder+". Use the initial-copy export only to create a new asset.");
            if(selected==null || selected.Length==0 || selected.Any(string.IsNullOrWhiteSpace) || selected.Distinct().Count()!=selected.Length)
                throw new InvalidOperationException("Select unique, non-empty profile IDs explicitly.");
            Hand3DPoseLibrary.ValidateBinding(rig,source.Data);
            Hand3DPoseLibrary.ValidateBinding(rig,target.Data);
            var errors=target.ValidateConfiguration();if(errors.Length>0)throw new InvalidOperationException(string.Join("\n",errors));
            var next=Hand3DPoseLibrary.Clone(target.Data);
            var movement=rig.GetComponent<Hand3DArmMovement>();
            string cameraId=GlobalObjectId.GetGlobalObjectIdSlow(movement.ArmCamera.transform).ToString();
            if(next.cameraId!=cameraId || !next.solverJointPaths.SequenceEqual(new[]{AnimationUtility.CalculateTransformPath(movement.UpperArm,rig.transform),AnimationUtility.CalculateTransformPath(movement.Forearm,rig.transform)})
                || next.wristPath!=AnimationUtility.CalculateTransformPath(movement.Hand,rig.transform) || !next.deriveElbowPreferenceFromReference || !next.preserveWristAfterIK)
                throw new InvalidOperationException("Runtime camera/IK metadata does not describe the current configured arm chain.");
            if(next.sourceRootId!=source.Data.sourceRootId || next.cameraId!=source.Data.cameraId || next.wristPath!=source.Data.wristPath
                || !next.solverJointPaths.SequenceEqual(source.Data.solverJointPaths)
                || next.deriveElbowPreferenceFromReference!=source.Data.deriveElbowPreferenceFromReference || next.preserveWristAfterIK!=source.Data.preserveWristAfterIK)
                throw new InvalidOperationException("Editor/runtime rig, camera or IK references disagree. Select compatible libraries.");
            var profiles=next.profiles.ToList();var skeleton=next.skeleton.ToList();
            foreach(string id in selected)
            {
                var value=source.Data.profiles.SingleOrDefault(p=>p.id==id);
                if(value==null)throw new InvalidOperationException("Missing selected profile: "+id);
                bool active=value.joints.Any(j=>j.enabled && j.captured && j.weight>0 && j.influenceChannels!=0);
                // Migrated no-op layers are meaningful placeholders in existing assignments.
                if(!active && !(FunctionalIds.Contains(id) && target.Find(id)!=null && !target.Find(id).joints.Any(j=>j.enabled && j.captured && j.weight>0 && j.influenceChannels!=0)))
                    throw new InvalidOperationException(id+": capture at least one influenced channel before publishing.");
                foreach(var entry in value.joints)
                {
                    var reference=source.Data.skeleton.Single(b=>b.path==entry.jointPath);
                    var existing=skeleton.SingleOrDefault(b=>b.path==reference.path);
                    if(existing==null)skeleton.Add(reference);
                    else if(JsonUtility.ToJson(existing)!=JsonUtility.ToJson(reference))throw new InvalidOperationException(id+": skeleton metadata differs for "+reference.path);
                    if(entry.neutralCaptured && !ValidRotation(entry.neutralRotation))throw new InvalidOperationException(id+": invalid neutral quaternion.");
                }
                int index=profiles.FindIndex(p=>p.id==id);
                if(index<0)profiles.Add(value);else profiles[index]=value;
            }
            next.profiles=profiles.ToArray();next.skeleton=skeleton.ToArray();
            var candidate=UnityEngine.Object.Instantiate(target);candidate.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                candidate.Configure(next,target.Exploration,target.HandEdit,target.Extended,target.Flexed,target.Gestures,target.GrabDoor,target.GrabDocument);
                errors=candidate.ValidateConfiguration();if(errors.Length>0)throw new InvalidOperationException(string.Join("\n",errors));
                foreach(var profile in selected.Select(candidate.Find))
                {
                    if(profile.fingerCombination>=0 && (profile.contexts&Hand3DPoseProfile.Context.HandEdit)!=0
                        && profile.joints.Any(j=>j.influenceChannels!=0 && next.skeleton.Single(b=>b.path==j.jointPath).role!=Hand3DPoseProfileSet.Role.Finger))
                        throw new InvalidOperationException(profile.id+": a HandEdit gesture must affect fingers only.");
                    if(profile.joints.Any(j=>j.enabled && j.weight>0 && (j.space==Hand3DPoseProfile.Space.ExplorationWristOffset || j.operation==Hand3DPoseProfile.Operation.RestLocalNeutralDelta)))
                    {
                        RequireWristBasis(candidate.Find(candidate.Exploration),next.wristPath);
                        if((profile.contexts&Hand3DPoseProfile.Context.HandEdit)!=0)RequireWristBasis(candidate.Find(candidate.HandEdit),next.wristPath);
                    }
                }
                var groups=new List<string[]>{candidate.GrabDoor,candidate.GrabDocument};
                groups.AddRange(candidate.Gestures.Select(g=>g.exploration));groups.AddRange(candidate.Gestures.Select(g=>g.handEdit));
                foreach(var group in groups)
                {
                    var relative=group.Select(candidate.Find).SelectMany(p=>p.joints).Where(j=>j.enabled && j.captured && j.weight>0 && j.influenceChannels!=0
                        && (j.space==Hand3DPoseProfile.Space.ExplorationWristOffset || j.operation==Hand3DPoseProfile.Operation.RestLocalNeutralDelta)).ToArray();
                    if(relative.Count(j=>j.space==Hand3DPoseProfile.Space.ExplorationWristOffset)>1 || relative.Count(j=>j.operation==Hand3DPoseProfile.Operation.RestLocalNeutralDelta)>1
                        || relative.Length>1 && relative.Any(j=>j.weight!=1f))throw new InvalidOperationException("Assigned companion layers contain ambiguous relative wrist contributions.");
                }
                return candidate;
            }
            catch{UnityEngine.Object.DestroyImmediate(candidate);throw;}
        }
        private static bool ValidRotation(Quaternion value)
        {
            double norm=(double)value.x*value.x+(double)value.y*value.y+(double)value.z*value.z+(double)value.w*value.w;
            return !double.IsNaN(norm) && !double.IsInfinity(norm) && Math.Abs(norm-1)<.001;
        }
        private static void RequireWristBasis(Hand3DPoseProfile profile,string wrist)
        {
            if(!profile.joints.Any(j=>j.jointPath==wrist && j.enabled && j.captured && j.weight==1f && (j.influenceChannels&Hand3DPoseProfile.Channels.Rotation)!=0
                && j.space==Hand3DPoseProfile.Space.CameraRelative && j.operation==Hand3DPoseProfile.Operation.Absolute))
                throw new InvalidOperationException(profile.id+": relative wrist requires a fully weighted camera-relative mode wrist reference.");
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
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if(GUILayout.Button("Save runtime asset (including Undo/Redo)"))AssetDatabase.SaveAssetIfDirty(target);
        }
    }
}
