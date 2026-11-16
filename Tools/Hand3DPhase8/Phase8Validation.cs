using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Metroidbrainia.Hand3DExperiment;
using Metroidbrainia.Hand3DExperiment.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using P=Metroidbrainia.Hand3DExperiment.Hand3DPoseProfile;

namespace Metroidbrainia
{
    public static class Phase8Validation
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        [Serializable] class Row { public string name,status,detail; }
        [Serializable] class Report { public string runId,scope; public int assertions; public Row[] cases; }
        static readonly List<Row> rows=new List<Row>(); static int checks; static string id;
        static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,Flags).GetValue(o);
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethods(Flags).Single(m=>m.Name==name && m.GetParameters().Length==args.Length).Invoke(o,args);
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
        static void Case(string name,Action test){try{test();rows.Add(new Row{name=name,status="PASS"});}catch(Exception e){Debug.LogException(e);rows.Add(new Row{name=name,status="FAIL",detail=e.ToString()});}}
        static Hand3DRig Load(){EditorSceneManager.OpenScene("Assets/Scenes/TestArmScene.unity");return Object.FindObjectsByType<Hand3DRig>().Single();}
        public static void Run()
        {
            Hand3DPoseConversion.IsolatedRoot();var args=Environment.GetCommandLineArgs();id=args[Array.IndexOf(args,"-phase5RunId")+1];
            Case("selective_publication_review_undo_save_reload",Publication);
            Case("published_profile_runs_with_actions_and_legacy_compatibility",PublishedAction);
            Case("rejection_atomicity_and_dependencies",Rejections);
            Case("universal_values_independent_from_legacy_captures",Independence);
            Case("legacy_capture_flags_still_required_at_startup",InitializationBoundary);
            File.WriteAllText("phase8-results.json",JsonUtility.ToJson(new Report{runId=id,scope="Isolated Unity Editor; source assets/scene preserved. Publication commits only disposable copies. Legacy structural startup dependency is explicitly retained; no visual approval.",assertions=checks,cases=rows.ToArray()},true));
            EditorApplication.Exit(rows.Any(r=>r.status=="FAIL")?1:0);
        }
        static Hand3DPoseLibrary Source(Hand3DRig rig,string suffix)
        {
            var original=AssetDatabase.LoadAssetAtPath<Hand3DPoseLibrary>(Hand3DPoseLibrary.Folder+"/HandPoses.asset");
            var copy=Object.Instantiate(original);copy.hideFlags=HideFlags.None;copy.Save(Hand3DPoseLibrary.Folder+"/Phase8-"+id+suffix+".asset");return copy;
        }
        static Hand3DRuntimePoses Target(Hand3DRig rig,string suffix)
        {
            var copy=Object.Instantiate(rig.RuntimePoses);copy.hideFlags=HideFlags.None;Hand3DRuntimePoseExport.SaveNew(copy,Hand3DRuntimePoseExport.Folder+"/Phase8-"+id+suffix+".asset");return copy;
        }
        static Dictionary<Object,string> SerializedRig(Hand3DRig rig)=>rig.GetComponentsInChildren<Component>(true).Where(c=>c is MonoBehaviour || c is Transform).ToDictionary(c=>(Object)c,c=>EditorJsonUtility.ToJson(c));
        static void SameRig(Dictionary<Object,string> snapshot){foreach(var pair in snapshot)Check(pair.Value==EditorJsonUtility.ToJson(pair.Key),"Publication changed serialized scene component: "+pair.Key);}
        static void Publication()
        {
            var rig=Load();var scene=SerializedRig(rig);var source=Source(rig,"publish");var target=Target(rig,"publish");
            var sourceOriginal=AssetDatabase.LoadAssetAtPath<Hand3DPoseLibrary>(Hand3DPoseLibrary.Folder+"/HandPoses.asset");string original=EditorJsonUtility.ToJson(sourceOriginal);
            var data=Hand3DPoseLibrary.Clone(source.Data);string update=target.Extended,newId="phase8.explicit.profile";
            var entry=data.profiles.Single(p=>p.id==update).joints.First();entry.rotation=Quaternion.Euler(11,22,33);entry.sourceEuler=new Vector3(11,22,33);
            var extra=JsonUtility.FromJson<P>(JsonUtility.ToJson(data.profiles.Single(p=>p.id==update)));extra.id=newId;extra.displayName="OK_Test_Capture";
            data.profiles=data.profiles.Concat(new[]{extra}).ToArray();source.Replace(data,"Phase8 source edit");source.Save();
            var initialCopy=Hand3DRuntimePoseExport.CreateCopy(rig,source);
            Check(initialCopy.Data.profiles.Length==13 && initialCopy.Data.profiles.All(p=>Hand3DRuntimePoseExport.FunctionalIds.Contains(p.id)),"Initial export leaked unselected profiles");
            Check(initialCopy.Find(newId)==null,"Initial copy included an unselected test-named profile");Object.DestroyImmediate(initialCopy);
            var untouched=target.Data.profiles.Where(p=>p.id!=update).ToDictionary(p=>p.id,JsonUtility.ToJson);
            string before=EditorJsonUtility.ToJson(target),sourceBefore=EditorJsonUtility.ToJson(source);var file=AssetDatabase.GetAssetPath(target);byte[] diskBefore=File.ReadAllBytes(file);
            string review=Hand3DRuntimePoseExport.ReviewSelected(rig,source,target,new[]{update,newId});
            Check(review.Contains("UPDATE  "+update) && review.Contains("ADD  "+newId),"Review does not show exact add/update IDs");
            Check(EditorJsonUtility.ToJson(target)==before && File.ReadAllBytes(file).SequenceEqual(diskBefore),"Review mutated runtime");
            Hand3DRuntimePoseExport.PublishSelected(rig,source,target,new[]{update,newId});
            Check(JsonUtility.ToJson(target.Find(update))==JsonUtility.ToJson(source.Data.profiles.Single(p=>p.id==update)),"Selected update differs");
            Check(target.Find(newId)!=null,"Explicit test-named profile not published");
            foreach(var pair in untouched)Check(JsonUtility.ToJson(target.Find(pair.Key))==pair.Value,"Unselected profile changed: "+pair.Key);
            Check(target.Data.profiles.Length==untouched.Count+2,"Unselected test profile leaked into runtime");
            Check(!target.Gestures.Any(g=>g.exploration.Contains(newId) || g.handEdit.Contains(newId)),"Publication activated a new profile implicitly");
            string published=EditorJsonUtility.ToJson(target);Undo.FlushUndoRecordObjects();Undo.PerformUndo();
            Check(EditorJsonUtility.ToJson(target)==before,"Undo did not recover previous runtime");
            EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target);Check(File.ReadAllBytes(file).SequenceEqual(diskBefore),"Saving Undo did not recover file");
            Undo.PerformRedo();Check(EditorJsonUtility.ToJson(target)==published,"Redo did not recover publication");
            EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target);AssetDatabase.ImportAsset(file,ImportAssetOptions.ForceUpdate);
            target=AssetDatabase.LoadAssetAtPath<Hand3DRuntimePoses>(file);Check(EditorJsonUtility.ToJson(target)==published,"Reload lost published profiles");
            Check(EditorJsonUtility.ToJson(source)==sourceBefore && EditorJsonUtility.ToJson(sourceOriginal)==original,"Publication altered Editor library");
            Check(Hand3DRuntimePoseExport.ReviewSelected(rig,source,target,new[]{newId}).Contains("UNCHANGED"),"Repeated publication is not stable");
            SameRig(scene);Check(!rig.gameObject.scene.isDirty,"Publication dirtied scene");
        }
        static void Rejections()
        {
            var rig=Load();var source=Source(rig,"reject");var target=Target(rig,"reject");
            string sourceJson=EditorJsonUtility.ToJson(source),targetJson=EditorJsonUtility.ToJson(target),file=AssetDatabase.GetAssetPath(target);byte[] disk=File.ReadAllBytes(file);
            void Reject(Action damage,string[] selected)
            {
                EditorJsonUtility.FromJsonOverwrite(sourceJson,source);EditorUtility.ClearDirty(source);
                damage();bool rejected=false;try{Hand3DRuntimePoseExport.PublishSelected(rig,source,target,selected);}catch{rejected=true;}
                Check(rejected,"Invalid publication accepted");Check(EditorJsonUtility.ToJson(target)==targetJson && File.ReadAllBytes(file).SequenceEqual(disk),"Rejected publication mutated target");
            }
            string endpoint=target.Extended;
            Reject(()=>{},Array.Empty<string>());Reject(()=>{},new[]{endpoint,endpoint});Reject(()=>{},new[]{"unknown"});
            Reject(()=>EditorUtility.SetDirty(source),new[]{endpoint});
            Reject(()=>source.Data.profiles=source.Data.profiles.Concat(new[]{source.Data.profiles.First()}).ToArray(),new[]{endpoint});
            Reject(()=>source.Data.profiles.Single(p=>p.id==endpoint).joints[0].jointPath="missing/joint",new[]{endpoint});
            Reject(()=>source.Data.profiles.Single(p=>p.id==endpoint).joints[0].referenceId="wrong parent",new[]{endpoint});
            Reject(()=>source.Data.profiles.Single(p=>p.id==endpoint).joints[0].weight=float.NaN,new[]{endpoint});
            Reject(()=>source.Data.profiles.Single(p=>p.id==endpoint).joints[0].rotation=new Quaternion(float.NaN,0,0,1),new[]{endpoint});
            Reject(()=>source.Data.profiles.Single(p=>p.id==endpoint).joints[0].influenceChannels=(P.Channels)8,new[]{endpoint});
            Reject(()=>source.Data.profiles.Single(p=>p.id==target.Exploration).fingersOnly=true, new[]{target.Exploration});
            Reject(()=>source.Data.profiles.Single(p=>p.id==endpoint).joints[0].space=(P.Space)99,new[]{endpoint});
            Reject(()=>source.Data.profiles.Single(p=>p.id==endpoint).joints[0].captured=false,new[]{endpoint});
            Reject(()=>source.Data.profiles.Single(p=>p.id==endpoint).joints=Array.Empty<P.JointValue>(),new[]{endpoint});
            Reject(()=>source.Data.profiles=source.Data.profiles.Concat(new[]{new P{id="empty.new",contexts=P.Context.Exploration}}).ToArray(),new[]{"empty.new"});
            Reject(()=>source.Data.profiles.Single(p=>p.id==target.Exploration).joints=Array.Empty<P.JointValue>(),new[]{target.Exploration});
            Reject(()=>source.Data.profiles.Single(p=>p.id=="GrabDoor.Additional").joints.First(j=>j.neutralCaptured).neutralRotation=new Quaternion(0,0,0,0),new[]{"GrabDoor.Additional"});
            Reject(()=>{var p=source.Data.profiles.Single(p=>p.id==target.HandEdit);p.fingerCombination=29;},new[]{target.HandEdit});
            Reject(()=>source.Data.cameraId="wrong camera",new[]{endpoint});
            Reject(()=>source.Data.profiles.Single(p=>p.id==target.Exploration).joints.First(j=>j.jointPath==source.Data.wristPath).weight=.5f,new[]{target.Exploration,"Gesture.0.OK.Wrist"});
            Reject(()=>
            {
                var relative=source.Data.profiles.Single(p=>p.id=="GrabDoor.Additional").joints.First(j=>j.operation==P.Operation.RestLocalNeutralDelta);
                var grab=source.Data.profiles.Single(p=>p.id=="GrabDoor.Joints");grab.joints=grab.joints.Concat(new[]{JsonUtility.FromJson<P.JointValue>(JsonUtility.ToJson(relative))}).ToArray();
            },new[]{"GrabDoor.Joints"});
            EditorJsonUtility.FromJsonOverwrite(sourceJson,source);EditorUtility.ClearDirty(source);
            var temporary=Object.Instantiate(target);bool blocked=false;try{Hand3DRuntimePoseExport.PublishSelected(rig,source,temporary,new[]{endpoint});}catch{blocked=true;}finally{Object.DestroyImmediate(temporary);}
            Check(blocked,"Nonpersistent runtime target accepted");
        }
        static void PublishedAction()
        {
            var rig=Load();var source=Source(rig,"execution");var target=Target(rig,"execution");
            var data=Hand3DPoseLibrary.Clone(source.Data);var profile=JsonUtility.FromJson<P>(JsonUtility.ToJson(data.profiles.Single(p=>p.id==target.Flexed)));
            profile.id="phase8.published.action";profile.contexts=P.Context.Action;profile.fingerCombination=-1;
            data.profiles=data.profiles.Concat(new[]{profile}).ToArray();source.Replace(data,"Create published action probe");source.Save();
            Hand3DRuntimePoseExport.PublishSelected(rig,source,target,new[]{profile.id});
            var fixture=typeof(Phase5Validation).GetMethod("Create",Flags).Invoke(null,new object[]{true,target});
            rig=Get<Hand3DRig>(fixture,"rig");var actions=Get<Hand3DPoseActions>(fixture,"actions");Call(fixture,"Mask",29);Call(fixture,"Step",1f);
            var pulse=ScriptableObject.CreateInstance<Hand3DProfileAction>();var properties=new SerializedObject(pulse);properties.FindProperty("profileId").stringValue=profile.id;properties.ApplyModifiedPropertiesWithoutUndo();
            var ev=ScriptableObject.CreateInstance<Hand3DEventAction>();var binding=new Hand3DPoseActions.Binding{gesture=(FlexedFingers)29,actions=new Hand3DGestureAction[]{pulse,ev}};
            int events=0;binding.onEvent.AddListener(()=>events++);actions.GetType().GetField("bindings",Flags).SetValue(actions,new[]{binding});
            Check(actions.TryExecute() && actions.IsRunning && events==1,"Published Profile Pulse did not start with one event");Call(actions,"Tick",.05f);Call(fixture,"Step",.05f);
            Check(rig.UniversalActive && actions.VisualContributions.Any(c=>c.profileId==profile.id && c.weight>0),"Published profile did not reach compositor");
            Call(actions,"Tick",.5f);Call(fixture,"Step",.5f);Check(!actions.IsRunning && events==1,"Published pulse/event did not finish cleanly");
            rig.SelectPoseSystem(Hand3DRig.PoseSystem.Legacy);Check(actions.TryExecute() && !actions.IsRunning && events==2,"Legacy did not preserve the eligible instant event while rejecting Profile Pulse");
            Check(typeof(Hand3DRuntimePoses).Assembly!=typeof(Hand3DPoseLibrary).Assembly,"Player data depends on Editor assembly");Object.DestroyImmediate(pulse);Object.DestroyImmediate(ev);
        }
        sealed class Pose { public Vector3[] p; public Quaternion[] q; }
        static Pose[] Timeline(bool alter)
        {
            var rig=Load();var arm=rig.GetComponent<Hand3DArmMovement>();var state=rig.GetComponent<Hand3DFingerState>();
            if(alter)foreach(var component in new Component[]{rig,rig.GetComponent<Hand3DPoseRecognizer>(),rig.GetComponent<Hand3DModeController>(),rig.GetComponent<Hand3DInteractionPose>()})
            {
                var serialized=new SerializedObject(component);var property=serialized.GetIterator();while(property.Next(true))
                {
                    if(property.propertyType==SerializedPropertyType.Vector3 && (property.name.IndexOf("euler",StringComparison.OrdinalIgnoreCase)>=0 || property.name=="modelPositionInCamera"))property.vector3Value=new Vector3(41,73,-29);
                    if(property.propertyType==SerializedPropertyType.Float && property.name.IndexOf("transitionDuration",StringComparison.OrdinalIgnoreCase)>=0)property.floatValue=1.7f;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Check(arm.Initialize(),"Arm init failed");var recognizer=rig.GetComponent<Hand3DPoseRecognizer>();Call(recognizer,"OnDisable");Call(recognizer,"OnEnable");
            var actions=rig.GetComponent<Hand3DPoseActions>();Call(actions,"OnDisable");Call(actions,"OnEnable");
            var serializedRig=new SerializedObject(rig);serializedRig.FindProperty("poseSystem").enumValueIndex=1;serializedRig.ApplyModifiedPropertiesWithoutUndo();Check(rig.Initialize(),"Universal startup failed");
            var bones=rig.RuntimePoses.Data.skeleton.Select(b=>b.path=="."?rig.transform:rig.transform.Find(b.path)).ToArray();var samples=new List<Pose>();
            var interaction=rig.GetComponent<Hand3DInteractionPose>();
            for(int mode=0;mode<2;mode++)
            {
                if(mode==1)arm.ToggleMode();
                for(int mask=0;mask<32;mask++)
                {
                    for(int finger=0;finger<5;finger++)state.SetFlexed((Finger)finger,(mask&(1<<finger))!=0);Call(rig,"RequestTransition");
                    if(mask==5)interaction.SetGrab(true);if(mask==9)interaction.SetGrab(false);if(mask==14)interaction.SetHandHold(true);if(mask==18)interaction.SetHandHold(false);
                    arm.BeginControl();arm.MoveHand(new Vector2(mask%2==0?8:-7,3),true);
                    for(int step=0;step<4;step++)
                    {Call(rig,"UpdateRig",1f/60);Check(rig.UniversalActive,"Unexpected Legacy fallback");samples.Add(new Pose{p=bones.Select(b=>b.position).ToArray(),q=bones.Select(b=>b.rotation).ToArray()});}
                }
            }
            return samples.ToArray();
        }
        static void Independence()
        {
            var reference=Timeline(false);var changed=Timeline(true);Check(reference.Length==changed.Length,"Different timelines");
            for(int frame=0;frame<reference.Length;frame++)for(int bone=0;bone<reference[frame].p.Length;bone++)
            {Check(Vector3.Distance(reference[frame].p[bone],changed[frame].p[bone])<.00005f,"Legacy capture affected Universal position");Check(Quaternion.Angle(reference[frame].q[bone],changed[frame].q[bone])<.05f,"Legacy capture affected Universal rotation");}
        }
        static void InitializationBoundary()
        {
            var rig=Load();Check(rig.GetComponent<Hand3DArmMovement>().Initialize(),"Arm init");var data=new SerializedObject(rig);data.FindProperty("poseSystem").enumValueIndex=1;
            data.FindProperty("fingers").GetArrayElementAtIndex(0).FindPropertyRelative("joints").GetArrayElementAtIndex(0).FindPropertyRelative("extendedCaptured").boolValue=false;data.ApplyModifiedPropertiesWithoutUndo();
            Check(!rig.Initialize(),"Legacy startup dependency changed unexpectedly; update documented boundary");
        }
    }
}
