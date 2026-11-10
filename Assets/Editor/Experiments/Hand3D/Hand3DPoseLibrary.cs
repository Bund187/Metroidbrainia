using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using P = Metroidbrainia.Hand3DExperiment.Hand3DPoseProfile;
using S = Metroidbrainia.Hand3DExperiment.Hand3DPoseProfileSet;

namespace Metroidbrainia.Hand3DExperiment.Editor
{
    // One optional Editor-only collection. Never referenced by a runtime component or scene.
    public sealed class Hand3DPoseLibrary : ScriptableObject
    {
        public const string Folder = "Assets/Editor/Experiments/Hand3D/PoseProfiles";
        [SerializeField] private S data;
        [SerializeField] private string extendedId, flexedId;
        public S Data => data;
        public string ExtendedId => extendedId;
        public string FlexedId => flexedId;
        public static S Clone(S value) => JsonUtility.FromJson<S>(JsonUtility.ToJson(value));

        public static void Guard(Hand3DRig rig)
        {
            if (!Hand3DEditing.CanEdit(rig) || rig.gameObject.scene.path != "Assets/Scenes/TestArmScene.unity"
                || EditorSceneManager.IsPreviewSceneObject(rig.gameObject) || PrefabStageUtility.GetCurrentPrefabStage()!=null)
                throw new InvalidOperationException("Select the TestArmScene rig instance in Edit Mode, outside Prefab Mode.");
            if(!Hand3DModeEditing.Ready(rig.GetComponent<Hand3DArmMovement>()))throw new InvalidOperationException("Configure the experimental arm chain and camera before editing profiles.");
        }
        public static Hand3DPoseLibrary Import(Hand3DRig rig, string json)
        {
            Guard(rig);
            // Parsing an explicit export does not invoke or relax the protected Phase 3 converter.
            var export=JsonUtility.FromJson<Hand3DPoseConversion.Result>(json);
            if(export==null || !export.Success || export.data==null || export.data.version!=1) throw new InvalidOperationException("Select a successful Phase 3 export.");
            ValidateBinding(rig,export.data);
            var library=CreateInstance<Hand3DPoseLibrary>();library.hideFlags=HideFlags.DontSave;
            library.data=Clone(export.data);
            foreach(var profile in library.data.profiles)
            {
                if(profile.joints.Length>0 && profile.joints.All(j=>j.sourceProperty!=null && j.sourceProperty.EndsWith(".extendedLocalEulerAngles"))) library.extendedId=profile.id;
                if(profile.joints.Length>0 && profile.joints.All(j=>j.sourceProperty!=null && j.sourceProperty.EndsWith(".flexedLocalEulerAngles"))) library.flexedId=profile.id;
            }
            return library;
        }
        public static void ValidateBinding(Hand3DRig rig,S value)
        {
            Guard(rig);
            if(value==null || value.sourceRootId!=GlobalObjectId.GetGlobalObjectIdSlow(rig.transform).ToString()) throw new InvalidOperationException("The library belongs to another rig/scene.");
            ValidateValues(value);
            foreach(var bone in value.skeleton)
            {
                var transform=bone.path=="."?rig.transform:rig.transform.Find(bone.path);
                if(transform==null || GlobalObjectId.GetGlobalObjectIdSlow(transform).ToString()!=bone.globalId
                    || GlobalObjectId.GetGlobalObjectIdSlow(transform.parent).ToString()!=bone.parentId) throw new InvalidOperationException("Missing or ambiguous joint: "+bone.path);
            }
        }
        private static void ValidateValues(S value)
        {
            var errors=value.Validate();if(errors.Length>0) throw new InvalidOperationException(string.Join("\n",errors));
            foreach(var profile in value.profiles)
            {
                if(profile.contexts==0 || ((int)profile.contexts&~31)!=0 || profile.fingerCombination< -1 || profile.fingerCombination>31)throw new InvalidOperationException("Invalid profile context/finger mask.");
                if(float.IsNaN(profile.transition.duration) || float.IsInfinity(profile.transition.duration) || float.IsNaN(profile.transition.releaseDuration) || float.IsInfinity(profile.transition.releaseDuration))throw new InvalidOperationException("Invalid transition duration.");
                foreach(var entry in profile.joints)
                    if(!Enum.IsDefined(typeof(P.Space),entry.space) || !Enum.IsDefined(typeof(P.Operation),entry.operation)
                        || float.IsNaN(entry.position.sqrMagnitude) || float.IsInfinity(entry.position.sqrMagnitude))throw new InvalidOperationException("Invalid pose value/reference operation.");
            }
        }
        public void Replace(S value,string undoName)
        {
            GuardStorage();
            ValidateValues(value);
            Undo.IncrementCurrentGroup();Undo.RegisterCompleteObjectUndo(this,undoName);data=Clone(value);EditorUtility.SetDirty(this);
        }
        public void AddProfile(string id,string name,P.Context context,bool fingersOnly)
        {
            if(string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("A stable ID is required.");
            var next=Clone(data);next.profiles=next.profiles.Concat(new[]{new P{id=id,displayName=name,contexts=context,fingersOnly=fingersOnly}}).ToArray();Replace(next,"Create hand pose profile");
        }
        public void DuplicateProfile(string sourceId,string id,string name)
        {
            var next=Clone(data);var copy=JsonUtility.FromJson<P>(JsonUtility.ToJson(next.profiles.Single(p=>p.id==sourceId)));copy.id=id;copy.displayName=name;
            next.profiles=next.profiles.Concat(new[]{copy}).ToArray();Replace(next,"Duplicate hand pose profile");
        }
        public void SetEndpoints(string extended,string flexed)
        {
            GuardStorage();
            if(!data.profiles.Any(p=>p.id==extended) || !data.profiles.Any(p=>p.id==flexed)) throw new InvalidOperationException("Select both endpoint profiles.");
            foreach(var id in new[]{extended,flexed})foreach(var joint in data.skeleton.Where(b=>b.role==S.Role.Finger))
                if(!data.profiles.Single(p=>p.id==id).joints.Any(j=>j.jointPath==joint.path && j.captured && j.enabled && (j.influenceChannels&P.Channels.Rotation)!=0))throw new InvalidOperationException("Endpoint profile is missing a captured finger rotation: "+joint.path);
            Undo.IncrementCurrentGroup();Undo.RegisterCompleteObjectUndo(this,"Set finger endpoint references");extendedId=extended;flexedId=flexed;EditorUtility.SetDirty(this);
        }
        public void AddJoint(Hand3DRig rig,Transform bone,S.Role role,int finger)
        {
            Guard(rig);if(!Hand3DEditing.ValidBone(rig,bone))throw new InvalidOperationException("Choose a real child joint of this rig.");
            if(role==S.Role.Finger && (finger<0 || finger>4))throw new InvalidOperationException("Select one of the five logical fingers.");
            string path=AnimationUtility.CalculateTransformPath(bone,rig.transform);var next=Clone(data);
            next.skeleton=next.skeleton.Concat(new[]{new S.JointReference{path=path,globalId=GlobalObjectId.GetGlobalObjectIdSlow(bone).ToString(),parentId=GlobalObjectId.GetGlobalObjectIdSlow(bone.parent).ToString(),role=role,finger=finger,joint=role==S.Role.Finger?data.skeleton.Count(b=>b.role==S.Role.Finger && b.finger==finger):-1,
                sourceLocalPosition=bone.localPosition,sourceLocalRotation=bone.localRotation,sourceLocalScale=bone.localScale}}).ToArray();Replace(next,"Register hand joint");
        }
        public void Save(string path=null)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Save poses in Edit Mode.");
            ValidateValues(data);
            string existing=AssetDatabase.GetAssetPath(this);if(!string.IsNullOrEmpty(existing))path=existing;
            if(string.IsNullOrEmpty(path) || !path.StartsWith(Folder+"/",StringComparison.Ordinal) || path.Contains("..") || Path.GetExtension(path)!=".asset")throw new InvalidOperationException("Save only inside "+Folder);
            if(string.IsNullOrEmpty(existing))
            {
                if(AssetDatabase.LoadMainAssetAtPath(path)!=null || File.Exists(path))throw new InvalidOperationException("Choose a new asset name; existing assets are not overwritten.");
                string parent=Path.GetDirectoryName(path).Replace('\\','/');string current="Assets";
                foreach(string part in parent.Substring(7).Split('/')){string child=current+"/"+part;if(!AssetDatabase.IsValidFolder(child))AssetDatabase.CreateFolder(current,part);current=child;}
                hideFlags=HideFlags.None;AssetDatabase.CreateAsset(this,path);
            }
            EditorUtility.SetDirty(this);AssetDatabase.SaveAssetIfDirty(this);
        }
        private void GuardStorage()
        {
            string path=AssetDatabase.GetAssetPath(this);if(!string.IsNullOrEmpty(path) && !path.StartsWith(Folder+"/",StringComparison.Ordinal))throw new InvalidOperationException("This library is outside the experimental Editor folder.");
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit profiles outside Play Mode.");
        }
    }
}
