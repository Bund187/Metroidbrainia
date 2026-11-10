using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Metroidbrainia.Hand3DExperiment;
using Metroidbrainia.Hand3DExperiment.Editor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using P = Metroidbrainia.Hand3DExperiment.Hand3DPoseProfile;
using S = Metroidbrainia.Hand3DExperiment.Hand3DPoseProfileSet;
using C = Metroidbrainia.Hand3DExperiment.Editor.Hand3DPoseConversion;

namespace Metroidbrainia
{
    public static class Phase3Validation
    {
        [Serializable] private sealed class Row { public string name, status, detail; }
        [Serializable] private sealed class Report { public string runId, scope; public int assertions, profiles, joints, values; public Row[] cases; }
        private static int checks;
        private static readonly List<Row> rows = new List<Row>();
        private static Hand3DRig Load()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/TestArmScene.unity",OpenSceneMode.Single);
            return scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Hand3DRig>(true)).Single();
        }
        private static void Check(bool value,string message) { if(!value) throw new Exception(message); checks++; }
        private static void Q(Quaternion a,Quaternion b,string message) => Check(Quaternion.Angle(a,b)<.05f,message);
        private static void V(Vector3 a,Vector3 b,string message) => Check(Vector3.Distance(a,b)<.000001f,message);
        private static void Call(object target,string name,params object[] args) => target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(target,args);
        private static S Clone(S data) => JsonUtility.FromJson<S>(JsonUtility.ToJson(data));
        private static P Profile(S data,string id) => data.profiles.Single(p=>p.id==id);
        private static Transform Bone(Hand3DRig rig,string path) => path=="."?rig.transform:rig.transform.Find(path);
        private static string Id(UnityEngine.Object item) => GlobalObjectId.GetGlobalObjectIdSlow(item).ToString();
        private static SerializedObject Source(Hand3DRig rig,string prefix)
        {
            Component source=prefix=="rig"?(Component)rig:prefix=="recognizer"?rig.GetComponent<Hand3DPoseRecognizer>():prefix=="modes"?rig.GetComponent<Hand3DModeController>():rig.GetComponent<Hand3DInteractionPose>();
            return new SerializedObject(source);
        }
        private static void Case(string name,Action test)
        {
            try {test();rows.Add(new Row{name=name,status="PASS",detail="Isolated Editor conversion/data comparison; no new runtime consumer."});}
            catch(Exception error){rows.Add(new Row{name=name,status="FAIL",detail=error.ToString()});Debug.LogError(name+": "+error);}
        }
        public static void Run()
        {
            C.IsolatedRoot(); var rig=Load(); var initial=C.Convert(rig);
            Case("non_destructive_schema_idempotence",()=>
            {
                Check(initial.Success,string.Join("\n",initial.errors));
                Check(initial.data.skeleton.Length==22,"Expected only real right WRAD joints + model root");
                Check(initial.data.profiles.Length==13,"Unexpected profile count");
                Check(initial.data.profiles.Sum(p=>p.joints.Length)==79,"Unexpected migrated value count");
                var second=C.Convert(rig);Check(JsonUtility.ToJson(initial)==JsonUtility.ToJson(second),"Non-idempotent conversion");
                foreach(var archived in initial.legacy)
                    Check(EditorJsonUtility.ToJson(rig.GetComponent(Type.GetType(archived.type)))==archived.json,"Conversion changed source component");
                foreach(var joint in initial.data.skeleton)
                {var bone=Bone(rig,joint.path); Check(Id(bone)==joint.globalId,"Wrong persistent joint reference"); V(bone.localPosition,joint.sourceLocalPosition,"Changed local position"); Q(bone.localRotation,joint.sourceLocalRotation,"Changed local rotation"); V(bone.localScale,joint.sourceLocalScale,"Changed scale");}
                C.Export(rig,Path.GetFullPath("phase3-profile-export.json")); string first=File.ReadAllText("phase3-profile-export.json");
                C.Export(rig,Path.GetFullPath("phase3-profile-export.json"));Check(first==File.ReadAllText("phase3-profile-export.json"),"Export accumulated or changed data");
            });
            Case("all_values_spaces_masks_and_neutrals",()=>CompareSources(Load()));
            Case("all_32_masks_against_current_rig",()=>Combinations(Load()));
            Case("timings_and_weighted_curves",()=>Timing(Load()));
            Case("archive_roundtrip_cross_scene_reload_and_events",()=>Archive(Load()));
            Case("invalid_missing_duplicate_and_uncaptured",()=>Invalid(Load()));
            Case("optional_positions_and_inactive_captures",()=>Positions(Load()));
            Case("full_body_mask_and_ik_representation_only",()=>Future(Load()));
            string[] args=Environment.GetCommandLineArgs(); string id=args[Array.IndexOf(args,"-phase3RunId")+1];
            File.WriteAllText("phase3-results.json",JsonUtility.ToJson(new Report{runId=id,scope="Isolated data migration, source recovery and existing rig comparisons. No profile application engine or visual validation.",assertions=checks,profiles=initial.data.profiles.Length,joints=initial.data.skeleton.Length,values=initial.data.profiles.Sum(p=>p.joints.Length),cases=rows.ToArray()},true));
            EditorApplication.Exit(rows.Any(r=>r.status=="FAIL")?1:0);
        }

        private static void CompareSources(Hand3DRig rig)
        {
            var result=C.Convert(rig);Check(result.Success,string.Join("\n",result.errors));var data=result.data;
            foreach(var profile in data.profiles) foreach(var entry in profile.joints)
            {
                var parts=entry.sourceProperty.Split(':');var source=Source(rig,parts[0]);var property=source.FindProperty(parts[1]);
                Check(property!=null,"No source for value");
                if((entry.storedChannels&P.Channels.Rotation)!=0) { V(entry.sourceEuler,property.vector3Value,"Euler not preserved");Q(entry.rotation,Quaternion.Euler(property.vector3Value),"Rotation not preserved"); }
                else V(entry.position,property.vector3Value,"Position not preserved");
                string parent=parts[1].Substring(0,parts[1].LastIndexOf('.'));
                if(parts[0]=="modes")
                {
                    var captured=source.FindProperty(parent+".captured").boolValue;Check(entry.captured==captured,"Mode flag changed");
                    bool camera=parts[1].EndsWith("modelEulerInCamera")||parts[1].EndsWith("handEulerInCamera");
                    Check(entry.space==(camera?P.Space.CameraRelative:P.Space.ParentLocal),"Mode space changed");
                    if(parts[1].EndsWith("modelEulerInCamera")) {V(entry.position,source.FindProperty(parent+".modelPositionInCamera").vector3Value,"Mode position changed");Check(entry.storedChannels==(P.Channels.Position|P.Channels.Rotation),"Root channels changed");}
                }
                else if(parts[0]=="rig")
                {string state=parts[1].EndsWith("extendedLocalEulerAngles")?"extended":"flexed";Check(entry.captured==source.FindProperty(parent+"."+state+"Captured").boolValue,"Finger flag changed");Check(entry.space==P.Space.ParentLocal,"Finger space changed");}
                else if(parts[1].EndsWith("explorationWristEuler"))
                {Check(entry.space==P.Space.ExplorationWristOffset && entry.operation==P.Operation.Offset,"Wrist offset flattened");Check(entry.enabled==source.FindProperty(parent+".useExplorationWrist").boolValue,"Wrist enable changed");Check(profile.contexts==P.Context.Exploration,"Wrist leaked into edit");}
                else if(parts[1].EndsWith("grabLocalEulerAngles"))
                {
                    var neutral=source.FindProperty(parent+".neutralLocalEulerAngles").vector3Value;
                    V(entry.neutralEuler,neutral,"Neutral Euler lost");Q(entry.neutralRotation,Quaternion.Euler(neutral),"Neutral quaternion lost");
                    Check(entry.captured==source.FindProperty(parent+".grabCaptured").boolValue && entry.neutralCaptured==source.FindProperty(parent+".neutralCaptured").boolValue,"Additional flags lost");
                    var bone=Bone(rig,entry.jointPath);var baseline=data.skeleton.Single(b=>b.path==entry.jointPath).sourceLocalRotation;
                    if(bone==rig.GetComponent<Hand3DArmMovement>().Hand)
                    {Check(entry.operation==P.Operation.RestLocalNeutralDelta,"Additional wrist flattened");Q(baseline*Quaternion.Inverse(Quaternion.Euler(neutral))*Quaternion.Euler(property.vector3Value),baseline*Quaternion.Inverse(entry.neutralRotation)*entry.rotation,"Neutral delta changed");}
                    else Check(entry.operation==P.Operation.Absolute,"Twist changed to wrist delta");
                }
                else Check(entry.captured==source.FindProperty(parent+".captured").boolValue,"Override capture flag changed");
                Check(entry.influenceChannels==entry.storedChannels && entry.weight==1f,"Captured influence not preserved");
                if(entry.space==P.Space.ParentLocal) Check(entry.referenceId==Id(Bone(rig,entry.jointPath).parent),"Local parent reference changed");
                else Check(entry.referenceId==data.cameraId,"Camera reference changed");
            }
            Check(Profile(data,"Gesture.0.OK.Overrides").fingerCombination==3 && Profile(data,"Gesture.1.Point.Overrides").fingerCombination==29,"Recognition masks changed");
            Check(Profile(data,"GrabDoor.Joints").joints.Length==15 && Profile(data,"GrabDocument.Joints").joints.Length==15,"Interaction captures missing");
            var shoulder=data.skeleton.Single(b=>b.role==S.Role.Shoulder);
            Check(data.profiles.All(p=>p.Influence(shoulder.path)==0),"Invented shoulder capture");
            Check(Profile(data,"GrabDocument.Positions").joints.Length==0 && Profile(data,"GrabDocument.Additional").joints.Length==0,"Invented optional captures");
        }

        private static void Combinations(Hand3DRig rig)
        {
            var data=C.Convert(rig).data;var movement=rig.GetComponent<Hand3DArmMovement>();Check(movement.Initialize(),"Movement failed");
            var recognizer=rig.GetComponent<Hand3DPoseRecognizer>();Call(recognizer,"OnDisable");Call(recognizer,"OnEnable");Check(rig.Initialize(),"Rig failed");
            var state=rig.GetComponent<Hand3DFingerState>();
            for(int mask=0;mask<32;mask++)
            {
                for(int f=0;f<5;f++)state.SetFlexed((Finger)f,(mask&(1<<f))!=0);
                Call(rig,"RequestTransition");Call(rig,"UpdateRig",1f);
                var gesture=data.profiles.SingleOrDefault(p=>p.fingerCombination==mask && p.id.EndsWith(".Overrides"));
                foreach(var joint in data.skeleton.Where(b=>b.role==S.Role.Finger))
                {
                    var endpoint=Profile(data,"Fingers."+((mask&(1<<joint.finger))!=0?"flexed":"extended")).joints.Single(e=>e.jointPath==joint.path);
                    var value=gesture?.joints.SingleOrDefault(e=>e.jointPath==joint.path)??endpoint;
                    Q(Bone(rig,joint.path).localRotation,value.rotation,"Converted target differs from current rig mask "+mask);
                }
            }
        }

        private static void Curve(AnimationCurve a,AnimationCurve b)
        {
            Check(a.length==b.length && a.preWrapMode==b.preWrapMode && a.postWrapMode==b.postWrapMode,"Curve structure/wrap changed");
            for(int i=0;i<a.length;i++)
            {var x=a.keys[i];var y=b.keys[i];Check(x.time==y.time && x.value==y.value && x.inTangent==y.inTangent && x.outTangent==y.outTangent && x.inWeight==y.inWeight && x.outWeight==y.outWeight && x.weightedMode==y.weightedMode,"Curve key changed");}
            foreach(float t in new[]{0f,.13f,.5f,.91f,1f})Check(Mathf.Abs(a.Evaluate(t)-b.Evaluate(t))<.000001f,"Curve evaluation changed");
        }
        private static void Timing(Hand3DRig rig)
        {
            var data=C.Convert(rig).data;
            foreach(var profile in data.profiles)
            {
                float duration=profile.id.StartsWith("Mode.")?new SerializedObject(rig.GetComponent<Hand3DModeController>()).FindProperty("transitionDuration").floatValue
                    :profile.id.StartsWith("Grab")?new SerializedObject(rig.GetComponent<Hand3DInteractionPose>()).FindProperty(profile.id.StartsWith("GrabDoor")?"transitionDuration":"handHoldTransitionDuration").floatValue
                    :new SerializedObject(rig).FindProperty("transitionDuration").floatValue;
                if(profile.id.EndsWith(".Wrist"))
                {var original=rig.GetComponent<Hand3DPoseRecognizer>().Poses[profile.fingerCombination==3?0:1];duration=original.WristTransitionDuration;Curve(original.WristTransitionEasing,profile.transition.curve);Check(profile.transition.retainTimingOnUnrecognizedGesture,"Wrist timing history lost");}
                Check(profile.transition.duration==duration && profile.transition.releaseDuration==duration,"Timing lost");
            }
            var source=new SerializedObject(rig.GetComponent<Hand3DPoseRecognizer>());var property=source.FindProperty("poses").GetArrayElementAtIndex(0).FindPropertyRelative("wristTransitionEasing");
            var keys=new[]{new Keyframe(0f,0f,.3f,2f,.2f,.8f){weightedMode=WeightedMode.Both},new Keyframe(1f,1f,-.4f,.8f,.4f,.7f){weightedMode=WeightedMode.Both}};
            var curve=new AnimationCurve(keys){preWrapMode=WrapMode.Loop,postWrapMode=WrapMode.PingPong};property.animationCurveValue=curve;source.ApplyModifiedPropertiesWithoutUndo();
            var snapshot=C.Convert(rig);Curve(curve,Profile(snapshot.data,"Gesture.0.OK.Wrist").transition.curve);
            Curve(curve,Profile(JsonUtility.FromJson<C.Result>(JsonUtility.ToJson(snapshot)).data,"Gesture.0.OK.Wrist").transition.curve);
        }

        private static void Archive(Hand3DRig rig)
        {
            var gesture=rig.GetComponent<Hand3DPoseRecognizer>().Poses[0];UnityEventTools.AddBoolPersistentListener(gesture.OnAction,rig.gameObject.SetActive,false);
            var original=C.Convert(rig);string normalized=JsonUtility.ToJson(original.data);var portable=JsonUtility.FromJson<C.Result>(JsonUtility.ToJson(original));
            rig=Load();var interaction=new SerializedObject(rig.GetComponent<Hand3DInteractionPose>());interaction.FindProperty("grabOverrides").ClearArray();interaction.ApplyModifiedPropertiesWithoutUndo();
            var modes=new SerializedObject(rig.GetComponent<Hand3DModeController>());modes.FindProperty("exploration").FindPropertyRelative("modelPositionInCamera").vector3Value=Vector3.one*12f;modes.ApplyModifiedPropertiesWithoutUndo();
            C.RestoreLegacy(rig,portable);var recovered=C.Convert(rig);
            Check(normalized==JsonUtility.ToJson(recovered.data),"Profile roundtrip changed data");
            var restoredEvent=rig.GetComponent<Hand3DPoseRecognizer>().Poses[0].OnAction;
            Check(restoredEvent.GetPersistentEventCount()==1 && restoredEvent.GetPersistentTarget(0)==rig.gameObject && restoredEvent.GetPersistentMethodName(0)=="SetActive","Persistent event lost");
            for(int i=0;i<original.legacy.Length;i++)
            {
                var a=original.legacy[i].references;var b=recovered.legacy[i].references;
                Check(a.Length==b.Length && a.Select((r,j)=>r.property==b[j].property && r.globalId==b[j].globalId).All(v=>v),"Persistent references lost");
                string Normalize(string text)=>Regex.Replace(text,"\"instanceID\"\\s*:\\s*(-?\\d+)","\"instanceID\":0");
                Check(Normalize(original.legacy[i].json)==Normalize(recovered.legacy[i].json),"Full source serialization changed: "+original.legacy[i].type);
            }
            var fresh=C.Convert(rig);Check(JsonUtility.ToJson(recovered)==JsonUtility.ToJson(fresh),"Recovery not idempotent");
            var wrong=JsonUtility.FromJson<C.Result>(JsonUtility.ToJson(portable));wrong.legacy[0].references[0].globalId="not-a-global-id";
            string before=EditorJsonUtility.ToJson(rig);bool rejected=false;try{C.RestoreLegacy(rig,wrong);}catch(InvalidOperationException){rejected=true;}
            Check(rejected && EditorJsonUtility.ToJson(rig)==before,"Bad reference caused partial restore");
        }

        private static void Invalid(Hand3DRig rig)
        {
            var data=C.Convert(rig).data;
            Action<Action<S>> invalid=mutate=>{var test=Clone(data);mutate(test);Check(test.Validate().Length>0,"Invalid profile accepted");};
            invalid(s=>s.profiles[0].joints[0].jointPath="missing");
            invalid(s=>s.profiles[0].joints[1]=s.profiles[0].joints[0]);
            invalid(s=>s.profiles[0].joints[0].influenceChannels=P.Channels.Position);
            invalid(s=>s.profiles[0].joints[0].weight=float.NaN);
            invalid(s=>s.profiles[0].joints[0].captured=false);
            invalid(s=>Profile(s,"Mode.exploration").joints[0].referenceId="wrong-camera");
            invalid(s=>s.profiles[0].joints[0].referenceId="wrong-parent");
            invalid(s=>Profile(s,"GrabDoor.Additional").joints[0].neutralJointPath="wrong-neutral");
            invalid(s=>Profile(s,"Gesture.0.OK.Wrist").contexts=P.Context.HandEdit);
            invalid(s=>s.profiles[0].joints[0].storedChannels=(P.Channels)4);
            var source=new SerializedObject(rig.GetComponent<Hand3DPoseRecognizer>());var first=source.FindProperty("poses").GetArrayElementAtIndex(0).FindPropertyRelative("visualOverrides").GetArrayElementAtIndex(0);
            var bone=first.FindPropertyRelative("bone").objectReferenceValue;first.FindPropertyRelative("bone").objectReferenceValue=null;source.ApplyModifiedPropertiesWithoutUndo();Check(!C.Convert(rig).Success,"Missing source reference accepted");
            first.FindPropertyRelative("bone").objectReferenceValue=bone;first.FindPropertyRelative("captured").boolValue=false;source.ApplyModifiedPropertiesWithoutUndo();Check(!C.Convert(rig).Success,"Legacy uncaptured gesture silently changed");
            first.FindPropertyRelative("captured").boolValue=true;
            source.FindProperty("poses").GetArrayElementAtIndex(0).FindPropertyRelative("visualOverrides").GetArrayElementAtIndex(1).FindPropertyRelative("bone").objectReferenceValue=bone;
            source.ApplyModifiedPropertiesWithoutUndo();Check(!C.Convert(rig).Success,"Duplicate source bone silently accepted");
            string before=File.ReadAllText("phase3-profile-export.json");bool rejected=false;try{C.Export(rig,Path.GetFullPath("phase3-profile-export.json"));}catch(InvalidOperationException){rejected=true;}
            Check(rejected && File.ReadAllText("phase3-profile-export.json")==before,"Failed conversion overwrote previous export");
        }

        private static void Positions(Hand3DRig rig)
        {
            var source=new SerializedObject(rig.GetComponent<Hand3DInteractionPose>());var positions=source.FindProperty("handHoldPositions");positions.arraySize=1;
            var entry=positions.GetArrayElementAtIndex(0);var bone=rig.Fingers[0].Joints[0].Bone;
            entry.FindPropertyRelative("bone").objectReferenceValue=bone;entry.FindPropertyRelative("neutral").vector3Value=bone.localPosition;entry.FindPropertyRelative("hold").vector3Value=bone.localPosition+Vector3.right*.001f;source.ApplyModifiedPropertiesWithoutUndo();
            var disabled=C.Convert(rig);Check(disabled.Success,string.Join("\n",disabled.errors));var value=Profile(disabled.data,"GrabDocument.Positions").joints.Single();
            Check(value.influenceChannels==0 && value.weight==0 && !value.captureFlagPresent,"Inactive position gained influence or invented flag");
            source.FindProperty("captureHandHoldFingerPositions").boolValue=true;source.ApplyModifiedPropertiesWithoutUndo();value=Profile(C.Convert(rig).data,"GrabDocument.Positions").joints.Single();
            V(value.position,bone.localPosition+Vector3.right*.001f,"Hold position lost");V(value.neutralPosition,bone.localPosition,"Neutral position lost");Check(value.influenceChannels==P.Channels.Position,"Position mask lost");
            var mode=new SerializedObject(rig.GetComponent<Hand3DModeController>());mode.FindProperty("exploration").FindPropertyRelative("captured").boolValue=false;mode.ApplyModifiedPropertiesWithoutUndo();
            var uncaptured=C.Convert(rig);Check(uncaptured.Success,string.Join("\n",uncaptured.errors));Check(Profile(uncaptured.data,"Mode.exploration").joints.All(v=>v.influenceChannels==0 && v.weight==0),"Invented uncaptured mode");
            var rigData=new SerializedObject(rig);rigData.FindProperty("fingers").GetArrayElementAtIndex(0).FindPropertyRelative("joints").GetArrayElementAtIndex(0).FindPropertyRelative("extendedCaptured").boolValue=false;rigData.ApplyModifiedPropertiesWithoutUndo();
            var partial=C.Convert(rig);Check(partial.Success,string.Join("\n",partial.errors));Check(Profile(partial.data,"Fingers.extended").joints[0].influenceChannels==0 && Profile(partial.data,"Fingers.flexed").joints[0].influenceChannels==P.Channels.Rotation,"Independent capture flags changed");
            var additional=source.FindProperty("additionalHandHoldBones");additional.arraySize=1;var added=additional.GetArrayElementAtIndex(0);
            added.FindPropertyRelative("bone").objectReferenceValue=rig.GetComponent<Hand3DArmMovement>().Hand;added.FindPropertyRelative("grabCaptured").boolValue=true;added.FindPropertyRelative("neutralCaptured").boolValue=false;source.ApplyModifiedPropertiesWithoutUndo();
            var neutralMissing=C.Convert(rig);Check(neutralMissing.Success && neutralMissing.notes.Any(n=>n.Contains("missing/mismatched neutral")),"Inactive neutral reference was not reported");
            Check(Profile(neutralMissing.data,"GrabDocument.Additional").joints.Single().influenceChannels==0,"Invented missing neutral influence");
        }

        private static void Future(Hand3DRig rig)
        {
            var data=C.Convert(rig).data;
            Check(data.solverJointPaths.Length==2 && data.solverJointPaths.All(p=>data.skeleton.Single(b=>b.path==p).role==S.Role.UpperArm || data.skeleton.Single(b=>b.path==p).role==S.Role.Forearm),"Wrong solver ownership");
            Check(data.preserveWristAfterIK && data.deriveElbowPreferenceFromReference,"IK intent not representable");
            // Synthetic format fixture only; never exported as an authored capture.
            var full=new P{id="Synthetic.FullBody",contexts=P.Context.Exploration,joints=data.skeleton.Select(b=>new P.JointValue{jointPath=b.path,referenceId=b.parentId,storedChannels=P.Channels.Rotation,influenceChannels=P.Channels.Rotation,weight=.5f,captured=true,rotation=b.sourceLocalRotation}).ToArray()};
            data.profiles=new[]{full};Check(data.Validate().Length==0,"Cannot represent actual complete skeleton");
            Check(full.joints.Any(j=>data.skeleton.Single(b=>b.path==j.jointPath).role==S.Role.Shoulder),"No shoulder representation");
            full.fingersOnly=true;full.contexts=P.Context.HandEdit;Check(data.Validate().Length>0,"HandEdit finger mask accepted body channels");
            full.joints=full.joints.Where(j=>data.skeleton.Single(b=>b.path==j.jointPath).role==S.Role.Finger).ToArray();Check(data.Validate().Length==0,"Finger-only edit mask rejected");
            full.contexts=P.Context.Action;Check(data.Validate().Length==0,"Temporary action mask not representable");
        }
    }
}
