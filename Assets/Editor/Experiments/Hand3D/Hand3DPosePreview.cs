using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using P = Metroidbrainia.Hand3DExperiment.Hand3DPoseProfile;
using S = Metroidbrainia.Hand3DExperiment.Hand3DPoseProfileSet;

namespace Metroidbrainia.Hand3DExperiment.Editor
{
    // Unity's disposable editing stage owns every preview object. No source Transform is ever written.
    public sealed class Hand3DPosePreview : PreviewSceneStage
    {
        public enum SelectionMask { FullBody, ArmOnly, WristOnly, FingersOnly, Custom }
        private Hand3DRig source;
        private Hand3DPoseLibrary library;
        private string profileId, referenceId;
        private string[] companions;
        private P.Context activeContext;
        private readonly Dictionary<Transform,Transform> copies=new Dictionary<Transform,Transform>();
        private Transform root;
        private float upperLength, lowerLength;
        public Transform PreviewRoot => root;
        public Hand3DRig Source => source;

        public static Hand3DPosePreview Open(Hand3DRig rig,Hand3DPoseLibrary library,string profile,string reference=null,string[] companions=null,bool apply=true,P.Context? context=null)
        {
            Hand3DPoseLibrary.ValidateBinding(rig,library.Data);
            if(profile==reference)throw new InvalidOperationException("A profile cannot be its own preview reference.");
            if(StageUtility.GetCurrentStage() is Hand3DPosePreview)StageUtility.GoToMainStage();
            var stage=CreateInstance<Hand3DPosePreview>();stage.source=rig;stage.library=library;stage.profileId=profile;stage.referenceId=reference;stage.companions=companions??Array.Empty<string>();
            try
            {
                var selected=stage.Profile(profile);var baseProfile=library.Data.profiles.SingleOrDefault(p=>p.id==reference);
                stage.activeContext=context??(baseProfile!=null && (baseProfile.contexts&selected.contexts)!=0?baseProfile.contexts&selected.contexts:selected.contexts);
                if((selected.contexts&stage.activeContext)==0 || stage.companions.Any(id=>(stage.Profile(id).contexts&stage.activeContext)==0))throw new InvalidOperationException("A selected layer does not apply in this preview context.");
                StageUtility.GoToStage(stage,true);
                if(stage.root==null)throw new InvalidOperationException("Could not create preview stage.");
                if(apply)stage.Apply();
                return stage;
            }
            catch {if(StageUtility.GetCurrentStage()==stage)StageUtility.GoToMainStage();else if(stage!=null)DestroyImmediate(stage);throw;}
        }
        protected override GUIContent CreateHeaderContent()=>new GUIContent("Hand 3D Pose Preview — disposable copy");
        protected override bool OnOpenStage()
        {
            if(!base.OnOpenStage())return false;
            var movement=source.GetComponent<Hand3DArmMovement>();
            upperLength=Vector3.Distance(movement.UpperArm.position,movement.Forearm.position);lowerLength=Vector3.Distance(movement.Forearm.position,movement.Hand.position);
            Transform parent=null;var ancestors=new Stack<Transform>();for(var t=source.transform.parent;t!=null;t=t.parent)ancestors.Push(t);
            while(ancestors.Count>0){var t=ancestors.Pop();parent=CopyTransform(t,parent,false);}
            root=CopyTransform(source.transform,parent,true);
            foreach(var pair in copies)
            {
                var mesh=pair.Key.GetComponent<MeshFilter>();if(mesh!=null)pair.Value.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
                var renderer=pair.Key.GetComponent<MeshRenderer>();if(renderer!=null)EditorUtility.CopySerialized(renderer,pair.Value.gameObject.AddComponent<MeshRenderer>());
                var skin=pair.Key.GetComponent<SkinnedMeshRenderer>();
                if(skin!=null)
                {
                    var copy=pair.Value.gameObject.AddComponent<SkinnedMeshRenderer>();EditorUtility.CopySerialized(skin,copy);
                    copy.bones=skin.bones.Select(b=>b!=null && copies.TryGetValue(b,out var mapped)?mapped:throw new InvalidOperationException("Skin bone outside the preview hierarchy.")).ToArray();
                    copy.rootBone=skin.rootBone!=null && copies.TryGetValue(skin.rootBone,out var rootBone)?rootBone:null;
                }
            }
            var lightObject=new GameObject("Pose preview light");SceneManager.MoveGameObjectToScene(lightObject,scene);var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1f;lightObject.transform.rotation=Quaternion.Euler(35f,-25f,0f);
            return true;
        }
        private Transform CopyTransform(Transform original,Transform parent,bool children)
        {
            var go=new GameObject(original.name);SceneManager.MoveGameObjectToScene(go,scene);var copy=go.transform;copy.SetParent(parent,false);
            copy.localPosition=original.localPosition;copy.localRotation=original.localRotation;copy.localScale=original.localScale;
            if(children){copies.Add(original,copy);foreach(Transform child in original)CopyTransform(child,copy,true);}
            return copy;
        }
        public Transform Joint(string path)=>path=="."?root:root.Find(path);
        public void Restore(){if(StageUtility.GetCurrentStage()==this)StageUtility.GoToMainStage();}
        private P Profile(string id)=>library.Data.profiles.Single(p=>p.id==id);
        private Transform CameraTransform
        {
            get {var camera=source.GetComponent<Hand3DArmMovement>().ArmCamera;if(camera==null || GlobalObjectId.GetGlobalObjectIdSlow(camera.transform).ToString()!=library.Data.cameraId)throw new InvalidOperationException("Missing camera reference.");return camera.transform;}
        }
        private Quaternion WristBasis()
        {
            var reference=library.Data.profiles.SingleOrDefault(p=>p.id==referenceId);
            var entry=reference?.joints.SingleOrDefault(j=>j.jointPath==library.Data.wristPath && j.space==P.Space.CameraRelative && j.operation==P.Operation.Absolute && j.captured && j.enabled && (j.influenceChannels&P.Channels.Rotation)!=0 && j.weight==1f);
            if(entry==null)throw new InvalidOperationException("This relative wrist requires an explicit reference profile with a fully weighted camera-relative wrist.");
            return CameraTransform.rotation*entry.rotation;
        }
        private IEnumerable<P> Layers()
        {
            if(!string.IsNullOrEmpty(referenceId))yield return Profile(referenceId);
            var selected=Profile(profileId);
            if(selected.fingerCombination>=0)
            {
                var extended=library.Data.profiles.SingleOrDefault(p=>p.id==library.ExtendedId);var flexed=library.Data.profiles.SingleOrDefault(p=>p.id==library.FlexedId);
                if(extended==null || flexed==null)throw new InvalidOperationException("Configure Extended and Flexed endpoint references for this gesture.");
                var endpoints=new P{id="preview endpoints",joints=library.Data.skeleton.Where(b=>b.role==S.Role.Finger).Select(b=>
                {
                    var value=((selected.fingerCombination&(1<<b.finger))!=0?flexed:extended).joints.SingleOrDefault(j=>j.jointPath==b.path && j.captured && j.enabled && (j.influenceChannels&P.Channels.Rotation)!=0);
                    return value??throw new InvalidOperationException("The gesture needs a captured finger endpoint: "+b.path);
                }).ToArray()};yield return endpoints;
            }
            yield return selected;foreach(string id in companions.Where(id=>id!=profileId && id!=referenceId).Distinct())yield return Profile(id);
        }
        private void Apply()
        {
            var layers=Layers().ToArray();
            var wristValues=layers.SelectMany(p=>p.joints).Where(j=>j.jointPath==library.Data.wristPath && j.enabled && j.captured && j.weight>0 && (j.influenceChannels&P.Channels.Rotation)!=0).ToArray();
            var relative=wristValues.Where(j=>j.space==P.Space.ExplorationWristOffset || j.operation==P.Operation.RestLocalNeutralDelta).ToArray();
            if(relative.Length>1 && relative.Any(j=>j.weight!=1f))throw new InvalidOperationException("Compound relative wrist layers require weight 1 in this Editor preview.");
            if(relative.Count(j=>j.space==P.Space.ExplorationWristOffset)>1 || relative.Count(j=>j.operation==P.Operation.RestLocalNeutralDelta)>1)throw new InvalidOperationException("Choose at most one wrist offset and one neutral-delta layer.");
            foreach(var layer in layers)foreach(var entry in layer.joints.Where(j=>j.jointPath!=library.Data.wristPath).OrderBy(j=>j.jointPath.Count(c=>c=='/')))ApplyValue(entry);
            // Camera wrist orientation is evaluated after parent rotations; special offsets remain explicit.
            foreach(var value in wristValues.OrderBy(j=>j.space==P.Space.ExplorationWristOffset?1:0))ApplyValue(value);
            foreach(var layer in layers)foreach(var entry in layer.joints.Where(j=>j.jointPath==library.Data.wristPath && (j.influenceChannels&P.Channels.Position)!=0))ApplyPosition(entry);
            ValidateGeometry();SceneView.RepaintAll();
        }
        private void ApplyValue(P.JointValue value)
        {
            if(!value.captured || !value.enabled || value.weight<=0f)return;
            var bone=Joint(value.jointPath);if(bone==null)throw new InvalidOperationException("Missing preview joint: "+value.jointPath);
            if(value.jointPath!=library.Data.wristPath && (value.influenceChannels&P.Channels.Position)!=0)ApplyPosition(value);
            if((value.influenceChannels&P.Channels.Rotation)==0)return;
            var reference=library.Data.skeleton.Single(b=>b.path==value.jointPath);
            if(value.space==P.Space.ExplorationWristOffset)
            {var basis=WristBasis();bone.rotation=Quaternion.Slerp(bone.rotation,basis*value.rotation*Quaternion.Inverse(basis)*bone.rotation,value.weight);}
            else if(value.operation==P.Operation.RestLocalNeutralDelta)
            {
                var target=WristBasis()*Quaternion.Inverse(value.neutralRotation)*value.rotation;
                bone.rotation=Quaternion.Slerp(bone.rotation,target,value.weight);
            }
            else if(value.space==P.Space.CameraRelative)
            {
                if(value.operation!=P.Operation.Absolute)throw new InvalidOperationException("Camera offsets need an explicit reference operation; use Absolute or the wrist offset operation.");
                bone.rotation=Quaternion.Slerp(bone.rotation,CameraTransform.rotation*value.rotation,value.weight);
            }
            else bone.localRotation=Quaternion.Slerp(bone.localRotation,value.operation==P.Operation.Offset?reference.sourceLocalRotation*value.rotation:value.rotation,value.weight);
        }
        private void ApplyPosition(P.JointValue value)
        {
            if(!value.captured || !value.enabled || value.weight<=0f || (value.influenceChannels&P.Channels.Position)==0)return;
            var bone=Joint(value.jointPath);
            if(value.space==P.Space.CameraRelative)
            {if(value.operation!=P.Operation.Absolute)throw new InvalidOperationException("Unsupported relative camera position.");bone.position=Vector3.Lerp(bone.position,CameraTransform.TransformPoint(value.position),value.weight);}
            else if(value.space==P.Space.ParentLocal)
            {var reference=library.Data.skeleton.Single(b=>b.path==value.jointPath);bone.localPosition=Vector3.Lerp(bone.localPosition,value.operation==P.Operation.Offset?reference.sourceLocalPosition+value.position:value.position,value.weight);}
            else throw new InvalidOperationException("A wrist orientation offset cannot contain position.");
        }
        public void ValidateGeometry()
        {
            foreach(var pair in copies)if(Vector3.Distance(pair.Key.localScale,pair.Value.localScale)>.000001f)throw new InvalidOperationException("Scale editing is not supported.");
            var movement=source.GetComponent<Hand3DArmMovement>();
            if(Mathf.Abs(Vector3.Distance(copies[movement.UpperArm].position,copies[movement.Forearm].position)-upperLength)>.00001f
                || Mathf.Abs(Vector3.Distance(copies[movement.Forearm].position,copies[movement.Hand].position)-lowerLength)>.00001f)throw new InvalidOperationException("The pose changes IK segment lengths. Restore the joint positions.");
        }
        public static string[] SelectPaths(S data,SelectionMask mask,string[] custom)
        {
            return data.skeleton.Where(b=>mask==SelectionMask.FullBody || mask==SelectionMask.ArmOnly && b.role!=S.Role.Finger && b.role!=S.Role.Wrist
                || mask==SelectionMask.WristOnly && b.role==S.Role.Wrist || mask==SelectionMask.FingersOnly && b.role==S.Role.Finger || mask==SelectionMask.Custom && custom.Contains(b.path)).Select(b=>b.path).ToArray();
        }
        public void Capture(string[] paths,P.Channels channels,bool allowSegmentPositions)
        {
            Hand3DPoseLibrary.ValidateBinding(source,library.Data);ValidateGeometry();
            if(paths.Length==0 || channels==0 || paths.Distinct().Count()!=paths.Length)throw new InvalidOperationException("Choose distinct joints and capture channels.");
            var next=Hand3DPoseLibrary.Clone(library.Data);var selected=next.profiles.Single(p=>p.id==profileId);var values=selected.joints.ToList();
            foreach(string path in paths)
            {
                var reference=next.skeleton.Single(b=>b.path==path);if(selected.fingersOnly && reference.role!=S.Role.Finger)throw new InvalidOperationException("This profile has a fingers-only mask.");
                var chosen=channels;bool segment=reference.role!=S.Role.Root && reference.role!=S.Role.Shoulder && reference.role!=S.Role.Finger;
                if(segment && !allowSegmentPositions)chosen&=~P.Channels.Position;
                foreach(var channel in new[]{P.Channels.Rotation,P.Channels.Position})
                {
                    if((chosen&channel)==0)continue;
                    var value=values.SingleOrDefault(j=>j.jointPath==path && (j.storedChannels&channel)!=0);
                    if(value==null)
                    {bool camera=reference.role==S.Role.Root || reference.role==S.Role.Wrist && channel==P.Channels.Rotation;value=new P.JointValue{jointPath=path,referenceId=camera?next.cameraId:reference.parentId,space=camera?P.Space.CameraRelative:P.Space.ParentLocal,storedChannels=channel,weight=1f};values.Add(value);}
                    ReadValue(value,channel);value.captured=true;value.enabled=true;value.influenceChannels|=channel;if(channel==P.Channels.Position)value.allowSegmentPosition=segment && allowSegmentPositions;
                }
            }
            selected.joints=values.ToArray();library.Replace(next,"Capture universal hand pose");
        }
        private void ReadValue(P.JointValue value,P.Channels channel)
        {
            if(value.space==P.Space.CameraRelative && value.operation!=P.Operation.Absolute)throw new InvalidOperationException("Relative camera contributions require a supported reference operation.");
            var bone=Joint(value.jointPath);var reference=library.Data.skeleton.Single(b=>b.path==value.jointPath);
            if(channel==P.Channels.Position)
            {
                if(value.space==P.Space.ExplorationWristOffset)throw new InvalidOperationException("Orientation offsets cannot capture position.");
                value.position=value.space==P.Space.CameraRelative?CameraTransform.InverseTransformPoint(bone.position):bone.localPosition-(value.operation==P.Operation.Offset?reference.sourceLocalPosition:Vector3.zero);
            }
            else
            {
                if(value.space==P.Space.ExplorationWristOffset)
                {
                    var additional=companions.Select(Profile).SelectMany(p=>p.joints).LastOrDefault(j=>j.jointPath==value.jointPath && j.operation==P.Operation.RestLocalNeutralDelta && j.enabled && j.weight==1f);
                    var visual=additional!=null?reference.sourceLocalRotation*Quaternion.Inverse(additional.neutralRotation)*additional.rotation:reference.sourceLocalRotation;
                    value.rotation=Quaternion.Inverse(WristBasis())*bone.rotation*Quaternion.Inverse(visual)*reference.sourceLocalRotation;
                }
                else if(value.operation==P.Operation.RestLocalNeutralDelta)
                {
                    var offset=companions.Select(Profile).SelectMany(p=>p.joints).LastOrDefault(j=>j.space==P.Space.ExplorationWristOffset && j.enabled && j.weight==1f);
                    value.rotation=value.neutralRotation*Quaternion.Inverse(offset!=null?offset.rotation:Quaternion.identity)*Quaternion.Inverse(WristBasis())*bone.rotation;
                }
                else if(value.space==P.Space.CameraRelative)value.rotation=Quaternion.Inverse(CameraTransform.rotation)*bone.rotation;
                else value.rotation=value.operation==P.Operation.Offset?Quaternion.Inverse(reference.sourceLocalRotation)*bone.localRotation:bone.localRotation;
                value.sourceEuler=value.rotation.eulerAngles;
            }
            value.sourceProperty="PoseEditor:"+value.jointPath;
        }
    }
}
