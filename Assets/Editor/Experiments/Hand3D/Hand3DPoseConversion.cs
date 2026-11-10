using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using P = Metroidbrainia.Hand3DExperiment.Hand3DPoseProfile;
using S = Metroidbrainia.Hand3DExperiment.Hand3DPoseProfileSet;
using Object = UnityEngine.Object;

namespace Metroidbrainia.Hand3DExperiment.Editor
{
    // Explicit, isolated conversion only. No menus, lifecycle hooks, pose previews or runtime consumers.
    public static class Hand3DPoseConversion
    {
        [Serializable] public sealed class Reference { public string property, globalId; }
        [Serializable] public sealed class SourceArchive { public string type, json; public Reference[] references; }
        [Serializable] public sealed class Result
        {
            public S data = new S();
            public SourceArchive[] legacy;
            public string[] errors, notes;
            public bool Success => errors != null && errors.Length == 0;
        }

        public static string IsolatedRoot()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string marker = Path.Combine(root, "phase0-isolated-copy.marker");
            if (!File.Exists(marker) || string.Equals(File.ReadAllText(marker), root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Conversion/restore requires the separate marked validation project.");
            return root;
        }

        private static string Id(Object value) => value == null ? "" : GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();
        private static string PathOf(Transform root, Transform bone) => bone == root ? "." : bone != null && bone.IsChildOf(root) ? AnimationUtility.CalculateTransformPath(bone, root) : "";
        private static SerializedProperty At(SerializedProperty item, string field) => item.FindPropertyRelative(field);
        private static AnimationCurve CopyCurve(AnimationCurve curve) => curve == null ? null : new AnimationCurve(curve.keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };

        public static Result Convert(Hand3DRig rig)
        {
            IsolatedRoot();
            if (rig == null || rig.gameObject.scene.name != "TestArmScene" || Application.isPlaying) throw new InvalidOperationException("Load the isolated TestArmScene outside Play.");
            var movement = rig.GetComponent<Hand3DArmMovement>();
            var recognizer = rig.GetComponent<Hand3DPoseRecognizer>();
            var modes = rig.GetComponent<Hand3DModeController>();
            var interaction = rig.GetComponent<Hand3DInteractionPose>();
            var actions = rig.GetComponent<Hand3DPoseActions>();
            if (movement == null || recognizer == null || modes == null || interaction == null || actions == null
                || movement.ArmCamera == null || movement.UpperArm == null || movement.Forearm == null || movement.Hand == null)
                throw new InvalidOperationException("Missing source components/chain; no fallback captures generated.");
            var result = new Result(); var errors = new List<string>(); var notes = new List<string>(); var profiles = new List<P>(); var bones = new List<S.JointReference>();
            Transform root = rig.transform;
            result.data.sourceRootId = Id(root); result.data.cameraId = Id(movement.ArmCamera.transform);
            Action<Transform,S.Role,int,int> addBone = (bone, role, finger, joint) =>
            {
                string path = PathOf(root,bone);
                if (string.IsNullOrEmpty(path)) { errors.Add("Missing/outside skeleton joint: " + role); return; }
                if (bones.Any(b=>b.path==path)) { errors.Add("Duplicate skeleton joint: " + path); return; }
                bones.Add(new S.JointReference { path=path, globalId=Id(bone), parentId=Id(bone.parent), role=role, finger=finger,joint=joint,
                    sourceLocalPosition=bone.localPosition, sourceLocalRotation=bone.localRotation, sourceLocalScale=bone.localScale });
            };
            addBone(root,S.Role.Root,-1,-1);
            Transform shoulder=movement.UpperArm.parent;
            if (shoulder == null || shoulder.name != "shoulder.r" || movement.UpperArm.name != "bicep.r" || movement.Forearm.name != "forearm.r" || movement.Hand.name != "wrist.r")
                errors.Add("Unexpected WRAD right-arm chain; inspect instead of guessing bones.");
            else addBone(shoulder,S.Role.Shoulder,-1,-1);
            addBone(movement.UpperArm,S.Role.UpperArm,-1,-1); addBone(movement.Forearm,S.Role.Forearm,-1,-1); addBone(movement.Hand,S.Role.Wrist,-1,-1);
            foreach(var bone in movement.Forearm.GetComponentsInChildren<Transform>(true).Where(b=>b.name=="forearm.Twist0.r" || b.name=="forearm.Twist1.r")) addBone(bone,S.Role.Twist,-1,-1);
            var rigData=new SerializedObject(rig); var fingers=rigData.FindProperty("fingers");
            for(int i=0;i<fingers.arraySize;i++)
            {
                var finger=fingers.GetArrayElementAtIndex(i); var joints=At(finger,"joints");
                if(joints.arraySize!=3) errors.Add("Expected three captured joints per finger");
                for(int j=0;j<joints.arraySize;j++) addBone(At(joints.GetArrayElementAtIndex(j),"bone").objectReferenceValue as Transform,S.Role.Finger,At(finger,"finger").enumValueIndex,j);
            }
            if(fingers.arraySize!=5 || bones.Count(b=>b.role==S.Role.Finger)!=15 || bones.Where(b=>b.role==S.Role.Finger).Select(b=>b.finger).Distinct().Count()!=5) errors.Add("Expected five distinct fingers / fifteen joints");
            result.data.skeleton=bones.ToArray(); result.data.solverJointPaths=new[]{PathOf(root,movement.UpperArm),PathOf(root,movement.Forearm)}; result.data.wristPath=PathOf(root,movement.Hand);
            Func<string,P.Context,float,P> profile = (id, context, duration) =>
            { var p=new P{id=id,contexts=context,transition=new P.Transition{duration=duration,releaseDuration=duration}}; profiles.Add(p); return p; };
            Func<SerializedProperty,string,string,bool,P.JointValue> rotation = (item, angleField, source, captured) =>
            {
                var bone=At(item,"bone").objectReferenceValue as Transform; Vector3 euler=At(item,angleField).vector3Value;
                return new P.JointValue{jointPath=PathOf(root,bone),referenceId=Id(bone!=null?bone.parent:null),sourceProperty=source,
                    storedChannels=P.Channels.Rotation,influenceChannels=captured?P.Channels.Rotation:P.Channels.None,weight=captured?1f:0f,
                    rotation=Quaternion.Euler(euler),sourceEuler=euler,captured=captured};
            };
            foreach(string state in new[]{"extended","flexed"})
            {
                var p=profile("Fingers."+state,P.Context.Exploration|P.Context.HandEdit,rigData.FindProperty("transitionDuration").floatValue); p.fingersOnly=true;
                var entries=new List<P.JointValue>();
                for(int i=0;i<fingers.arraySize;i++)
                {
                    var joints=At(fingers.GetArrayElementAtIndex(i),"joints");
                    for(int j=0;j<joints.arraySize;j++) {var item=joints.GetArrayElementAtIndex(j);entries.Add(rotation(item,state+"LocalEulerAngles","rig:"+item.propertyPath+"."+state+"LocalEulerAngles",At(item,state+"Captured").boolValue));}
                }
                p.joints=entries.ToArray();
            }
            var poseData=new SerializedObject(recognizer); var gestures=poseData.FindProperty("poses");
            for(int i=0;i<gestures.arraySize;i++)
            {
                var item=gestures.GetArrayElementAtIndex(i); string key="Gesture."+i+"."+At(item,"poseName").stringValue;
                var p=profile(key+".Overrides",P.Context.Exploration|P.Context.HandEdit,rigData.FindProperty("transitionDuration").floatValue);
                p.fingerCombination=At(item,"flexedFingers").intValue; p.fingersOnly=true;
                var entries=new List<P.JointValue>(); var overrides=At(item,"visualOverrides");
                for(int j=0;j<overrides.arraySize;j++)
                {
                    var value=overrides.GetArrayElementAtIndex(j); bool captured=At(value,"captured").boolValue;
                    if(!captured) errors.Add(value.propertyPath+": current gesture application ignores captured=false; cannot silently deactivate this legacy value.");
                    entries.Add(rotation(value,"localEulerAngles","recognizer:"+value.propertyPath+".localEulerAngles",captured));
                }
                p.joints=entries.ToArray();
                p=profile(key+".Wrist",P.Context.Exploration,At(item,"wristTransitionDuration").floatValue);
                p.fingerCombination=At(item,"flexedFingers").intValue;
                p.transition.easing=P.Easing.CurveClampedOrSmoothStep; p.transition.curve=CopyCurve(At(item,"wristTransitionEasing").animationCurveValue); p.transition.retainTimingOnUnrecognizedGesture=true;
                bool enabled=At(item,"useExplorationWrist").boolValue; Vector3 wrist=At(item,"explorationWristEuler").vector3Value;
                p.joints=new[]{new P.JointValue{jointPath=result.data.wristPath,referenceId=result.data.cameraId,sourceProperty="recognizer:"+item.propertyPath+".explorationWristEuler",
                    storedChannels=P.Channels.Rotation,influenceChannels=enabled?P.Channels.Rotation:0,weight=enabled?1f:0f,captured=true,captureFlagPresent=false,enabled=enabled,
                    space=P.Space.ExplorationWristOffset,operation=P.Operation.Offset,sourceEuler=wrist,rotation=Quaternion.Euler(wrist)}};
            }
            var modeData=new SerializedObject(modes);
            foreach(string name in new[]{"exploration","handEdit"})
            {
                var config=modeData.FindProperty(name); bool captured=At(config,"captured").boolValue;
                var p=profile("Mode."+name,name=="exploration"?P.Context.Exploration:P.Context.HandEdit,modeData.FindProperty("transitionDuration").floatValue);
                var entries=new List<P.JointValue>();
                foreach(var pair in new[]{(root,"modelEulerInCamera",P.Space.CameraRelative),(movement.UpperArm,"upperLocalEuler",P.Space.ParentLocal),(movement.Forearm,"forearmLocalEuler",P.Space.ParentLocal),(movement.Hand,"handEulerInCamera",P.Space.CameraRelative)})
                {
                    Vector3 euler=At(config,pair.Item2).vector3Value;
                    entries.Add(new P.JointValue{jointPath=PathOf(root,pair.Item1),referenceId=pair.Item3==P.Space.CameraRelative?result.data.cameraId:Id(pair.Item1.parent),space=pair.Item3,
                        sourceProperty="modes:"+config.propertyPath+"."+pair.Item2,storedChannels=P.Channels.Rotation,influenceChannels=captured?P.Channels.Rotation:0,weight=captured?1f:0f,captured=captured,sourceEuler=euler,rotation=Quaternion.Euler(euler)});
                }
                entries[0].storedChannels|=P.Channels.Position; if(captured) entries[0].influenceChannels|=P.Channels.Position;
                entries[0].position=At(config,"modelPositionInCamera").vector3Value;
                p.joints=entries.ToArray();
            }
            var interactionData=new SerializedObject(interaction);
            foreach(bool document in new[]{false,true})
            {
                string id=document?"GrabDocument":"GrabDoor"; var context=document?P.Context.GrabDocument:P.Context.GrabDoor;
                float duration=interactionData.FindProperty(document?"handHoldTransitionDuration":"transitionDuration").floatValue;
                var p=profile(id+".Joints",context,duration); p.fallbackToFlexedFingers=true;
                var entries=new List<P.JointValue>(); var values=interactionData.FindProperty(document?"handHoldOverrides":"grabOverrides");
                for(int i=0;i<values.arraySize;i++){var v=values.GetArrayElementAtIndex(i);entries.Add(rotation(v,"localEulerAngles","interaction:"+v.propertyPath+".localEulerAngles",At(v,"captured").boolValue));}
                p.joints=entries.ToArray();
                p=profile(id+".Additional",context,duration); entries.Clear(); values=interactionData.FindProperty(document?"additionalHandHoldBones":"additionalGrabBones");
                for(int i=0;i<values.arraySize;i++)
                {
                    var v=values.GetArrayElementAtIndex(i); var entry=rotation(v,"grabLocalEulerAngles","interaction:"+v.propertyPath+".grabLocalEulerAngles",At(v,"grabCaptured").boolValue);
                    var bone=At(v,"bone").objectReferenceValue as Transform; var neutral=At(v,"neutralBone").objectReferenceValue as Transform;
                    entry.neutralJointPath=PathOf(root,neutral); entry.neutralCaptured=At(v,"neutralCaptured").boolValue;
                    entry.neutralEuler=At(v,"neutralLocalEulerAngles").vector3Value; entry.neutralRotation=Quaternion.Euler(entry.neutralEuler);
                    if(!entry.neutralCaptured || neutral!=bone) {entry.influenceChannels=0;entry.weight=0f;notes.Add(v.propertyPath+": missing/mismatched neutral; retained without influence, matching legacy Captured semantics.");}
                    if(bone==movement.Hand) entry.operation=P.Operation.RestLocalNeutralDelta;
                    entries.Add(entry);
                }
                p.joints=entries.ToArray();
            }
            var positions=interactionData.FindProperty("handHoldPositions");
            var positionsProfile=profile("GrabDocument.Positions",P.Context.GrabDocument,interactionData.FindProperty("handHoldTransitionDuration").floatValue);
            var positionEntries=new List<P.JointValue>();
            for(int i=0;i<positions.arraySize;i++)
            {
                var v=positions.GetArrayElementAtIndex(i); var bone=At(v,"bone").objectReferenceValue as Transform; bool enabled=interactionData.FindProperty("captureHandHoldFingerPositions").boolValue;
                positionEntries.Add(new P.JointValue{jointPath=PathOf(root,bone),referenceId=Id(bone!=null?bone.parent:null),sourceProperty="interaction:"+v.propertyPath+".hold",
                    storedChannels=P.Channels.Position,influenceChannels=enabled?P.Channels.Position:0,weight=enabled?1f:0f,enabled=enabled,captured=true,captureFlagPresent=false,
                    position=At(v,"hold").vector3Value,neutralPosition=At(v,"neutral").vector3Value});
            }
            positionsProfile.joints=positionEntries.ToArray();
            result.data.profiles=profiles.ToArray(); errors.AddRange(result.data.Validate());
            result.legacy=new Component[]{rig,recognizer,modes,interaction,movement,actions}.Select(Archive).ToArray();
            result.errors=errors.ToArray(); notes.AddRange(new[]{"Sparse profiles: missing joints/channels have zero influence; skeleton source transforms are references, not captures.",
                "Gesture finger masks select Extended/Flexed endpoints; overrides do not duplicate those endpoints.",
                "Additional profiles override joint targets as in the original rig; wrist neutral delta requires the source rest local reference.",
                "Inspection, gesture action/events, movement settings and transition history are preserved in the legacy archive; they are not static pose captures."}); result.notes=notes.ToArray();
            return result;
        }

        private static SourceArchive Archive(Component source)
        {
            var data=new SerializedObject(source); var property=data.GetIterator(); var references=new List<Reference>();
            while(property.Next(true)) if(property.propertyType==SerializedPropertyType.ObjectReference)
                references.Add(new Reference{property=property.propertyPath,globalId=Id(property.objectReferenceValue)});
            return new SourceArchive{type=source.GetType().AssemblyQualifiedName,json=EditorJsonUtility.ToJson(source),references=references.ToArray()};
        }

        // Explicit recovery of the untouched source archive, never an approximation from normalized quaternions.
        public static void RestoreLegacy(Hand3DRig rig, Result snapshot)
        {
            IsolatedRoot();
            if(Application.isPlaying || rig==null || rig.gameObject.scene.name!="TestArmScene" || snapshot.data.sourceRootId!=Id(rig.transform)) throw new InvalidOperationException("Wrong restore target.");
            var targets=snapshot.legacy.Select(a=>rig.GetComponent(Type.GetType(a.type,true))).ToArray();
            if(targets.Any(t=>t==null)) throw new InvalidOperationException("Missing source component; restore aborted.");
            var bindings=new List<(int,string,Object)>();
            for(int i=0;i<snapshot.legacy.Length;i++) foreach(var reference in snapshot.legacy[i].references)
            {
                Object value=null;
                if(!string.IsNullOrEmpty(reference.globalId))
                {
                    if(!GlobalObjectId.TryParse(reference.globalId,out var id) || (value=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id))==null) throw new InvalidOperationException("Unresolved reference: "+reference.property);
                }
                bindings.Add((i,reference.property,value));
            }
            // Never interpret transient JSON IDs from another load. All references were resolved above and are rebound below.
            var json=snapshot.legacy.Select(a=>Regex.Replace(a.json,"\"instanceID\"\\s*:\\s*(-?\\d+)","\"instanceID\":0")).ToArray();
            for(int i=0;i<targets.Length;i++) EditorJsonUtility.FromJsonOverwrite(json[i],targets[i]);
            foreach(var group in bindings.GroupBy(b=>b.Item1))
            {
                var data=new SerializedObject(targets[group.Key]);
                foreach(var binding in group) data.FindProperty(binding.Item2).objectReferenceValue=binding.Item3;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static void Export(Hand3DRig rig, string destination)
        {
            string root=IsolatedRoot(); string full=Path.GetFullPath(destination);
            if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Export only inside the validation project.");
            var result=Convert(rig); if(!result.Success) throw new InvalidOperationException(string.Join("\n",result.errors));
            File.WriteAllText(full,JsonUtility.ToJson(result,true));
        }
    }
}
