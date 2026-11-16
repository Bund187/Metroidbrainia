using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Metroidbrainia.Hand3DExperiment;
using Metroidbrainia.Hand3DExperiment.Editor;
using UnityEditor;
using UnityEngine;
using P=Metroidbrainia.Hand3DExperiment.Hand3DPoseProfile;
using S=Metroidbrainia.Hand3DExperiment.Hand3DPoseProfileSet;

namespace Metroidbrainia
{
    public static class Phase6Validation
    {
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        [Serializable] class Row{public string name,status,detail;}
        [Serializable] class Report{public string runId,scope;public int assertions;public Row[] cases;}
        static int checks;static readonly List<Row> rows=new List<Row>();
        static FieldInfo Field(Type type,string name){while(type!=null){var f=type.GetField(name,Flags);if(f!=null)return f;type=type.BaseType;}throw new Exception("Missing field "+name);}
        static T Get<T>(object instance,string field)=>(T)Field(instance.GetType(),field).GetValue(instance);
        static void Set(object instance,string field,object value)=>Field(instance.GetType(),field).SetValue(instance,value);
        static object Call(object instance,string method,params object[] args)=>instance.GetType().GetMethods(Flags).Single(m=>m.Name==method && m.GetParameters().Length==args.Length).Invoke(instance,args);
        static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
        static object Create(bool universal){var f=typeof(Phase5Validation).GetMethod("Create",Flags).Invoke(null,new object[]{universal,null});Call(f,"Mask",29);Call(f,"Step",1f);return f;}
        static void Step(object f,float dt){Call(Get<Hand3DPoseActions>(f,"actions"),"Tick",dt);Call(f,"Step",dt);}
        static void Case(string name,Action test){try{test();rows.Add(new Row{name=name,status="PASS"});}catch(Exception e){rows.Add(new Row{name=name,status="FAIL",detail=e.ToString()});Debug.LogException(e);}}
        static T Definition<T>() where T:ScriptableObject=>ScriptableObject.CreateInstance<T>();
        static Hand3DPoseActions.Binding Bind(object f,params Hand3DGestureAction[] actions)
        {var binding=new Hand3DPoseActions.Binding{gesture=(FlexedFingers)29,actions=actions};Set(Get<Hand3DPoseActions>(f,"actions"),"bindings",new[]{binding});return binding;}
        public static void Run()
        {
            Hand3DPoseConversion.IsolatedRoot();
            foreach(bool universal in new[]{false,true})
            {
                bool u=universal;
                Case("32_combinations_"+u,()=>Combinations(u));
                Case("point_curve_modes_restore_"+u,()=>Point(u));
                Case("instant_multiple_events_reentrancy_"+u,()=>Events(u));
                Case("cancellation_lifecycle_"+u,()=>Cancellation(u));
                Case("actual_door_document_interrupt_"+u,()=>Contact(u));
                Case("continuous_actions_movement_"+u,()=>Stress(u));
            }
            Case("profile_channels_priority_ik_restore",Profiles);
            Case("environment_mode_and_arm_gate",WorldPolicy);
            string[] args=Environment.GetCommandLineArgs();File.WriteAllText("phase6-results.json",JsonUtility.ToJson(new Report{runId=args[Array.IndexOf(args,"-phase5RunId")+1],scope="Isolated Unity numeric/state tests; no final Game visual approval.",assertions=checks,cases=rows.ToArray()},true));
            EditorApplication.Exit(rows.Any(r=>r.status=="FAIL")?1:0);
        }
        static void Combinations(bool universal)
        {
            var f=Create(universal);var actions=Get<Hand3DPoseActions>(f,"actions");
            for(int mask=0;mask<32;mask++)
            {
                Call(f,"Mask",mask);Step(f,.3f);bool ran=actions.TryExecute();Check(ran==(mask==29),"Recognition/action mapping "+mask);actions.CancelAction();Step(f,.3f);
                Check((int)Get<Hand3DFingerState>(f,"state").Current==mask,"Action changed logical fingers");Check(actions.VisualContributions.Count==0 && actions.PointClickWeight==0,"Residual visual contribution");
            }
            // A previously unrecognized gesture can receive actions through data only.
            {
                var recognizer=Get<Hand3DRig>(f,"rig").GetComponent<Hand3DPoseRecognizer>();Call(recognizer,"OnDisable");Set(recognizer,"poses",recognizer.Poses.Concat(new[]{new HandPose("Custom fixture",(FlexedFingers)31)}).ToArray());Call(recognizer,"OnEnable");
                if(universal)
                {
                    var rig=Get<Hand3DRig>(f,"rig");var copy=UnityEngine.Object.Instantiate(rig.RuntimePoses);
                    copy.Configure(copy.Data,copy.Exploration,copy.HandEdit,copy.Extended,copy.Flexed,copy.Gestures.Concat(new[]{new Hand3DRuntimePoses.Gesture{combination=(FlexedFingers)31}}).ToArray(),copy.GrabDoor,copy.GrabDocument);rig.SelectPoseSystem(Hand3DRig.PoseSystem.Universal,copy);
                }
                var ev=Definition<Hand3DEventAction>();var binding=Bind(f,ev);binding.gesture=(FlexedFingers)31;int n=0;binding.onEvent.AddListener(()=>n++);Call(f,"Mask",31);Check(actions.TryExecute() && n==1,"New gesture required executor code");
                Step(f,.3f);Check(!universal || Get<Hand3DRig>(f,"rig").UniversalActive,"New gesture visual assignment failed");
            }
        }
        static void Point(bool universal)
        {
            var f=Create(universal);var a=Get<Hand3DPoseActions>(f,"actions");var arm=Get<Hand3DArmMovement>(f,"arm");var bones=Get<Transform[]>(f,"bones");
            float duration=Get<float>(a,"pointClickDuration"),amplitude=Get<float>(a,"pointClickAmplitude");
            for(int mode=0;mode<2;mode++)
            {
                if(mode==1){arm.ToggleMode();Step(f,1f);}var before=bones.Select(b=>b.localRotation).ToArray();
                Check(a.TryExecute(),"Point did not begin");Check(!a.TryExecute(),"Accepted duplicate active request");float time=0;
                for(int i=0;i<30;i++)
                {time+=.01f;Step(f,.01f);float expected=time>=duration?0:Mathf.Sin(Mathf.Clamp01(time/duration)*Mathf.PI)*Mathf.Clamp01(amplitude);Check(Mathf.Abs(a.PointClickWeight-expected)<.00001f,"Point envelope changed");}
                Check(!a.IsRunning && a.PointClickWeight==0,"Point did not finish");
                Check(bones.Select((b,i)=>Quaternion.Angle(b.localRotation,before[i])<.1f).All(v=>v),"Point left residual pose");
            }
        }
        static void Events(bool universal)
        {
            var f=Create(universal);var a=Get<Hand3DPoseActions>(f,"actions");var point=Definition<Hand3DPointClickAction>();var ev=Definition<Hand3DEventAction>();
            var binding=Bind(f,point,ev,ev);int original=0,configured=0;bool recursive=true;
            Get<Hand3DRig>(f,"rig").GetComponent<Hand3DPoseRecognizer>().CurrentPose.OnAction.AddListener(()=>original++);
            binding.onEvent.AddListener(()=>{configured++;recursive=a.TryExecute();});
            Check(a.TryExecute(Hand3DActionInput.Mouse),"Combined actions failed");Check(original==1 && configured==1 && !recursive,"Double/reentrant event execution");Check(a.IsRunning,"Instant event lost duration action");
            Step(f,.3f);Check(!a.IsRunning,"Combined completion failed");
            binding.actions=new Hand3DGestureAction[]{ev};Check(a.TryExecute() && !a.IsRunning,"Instant action required ticking");Check(original==2 && configured==2,"Event count changed");
            binding.actions=Array.Empty<Hand3DGestureAction>();Check(!a.TryExecute(),"Explicit no-action used compatibility fallback");
            binding.actions=new Hand3DGestureAction[]{ev};binding.onEvent.RemoveAllListeners();binding.onEvent.AddListener(a.CancelAction);Check(a.TryExecute() && !a.IsRunning,"Event cancellation reentrancy");
        }
        static void Cancellation(bool universal)
        {
            var f=Create(universal);var a=Get<Hand3DPoseActions>(f,"actions");var arm=Get<Hand3DArmMovement>(f,"arm");
            Action start=()=>{Call(f,"Mask",29);Step(f,1f);Check(a.TryExecute(),"Action did not restart");Step(f,.04f);Check(a.PointClickWeight>0,"Missing pulse");};
            Action clean=()=>Check(!a.IsRunning && a.PointClickWeight==0 && a.VisualContributions.Count==0,"Cancellation left output");
            start();Call(f,"Mask",3);clean();
            start();arm.ToggleMode();clean();Step(f,1f);
            start();Call(a,"OnApplicationFocus",false);clean();
            start();Call(a,"OnApplicationPause",true);clean();
            start();Time.timeScale=0;Call(a,"Tick",0f);clean();Time.timeScale=1;
            // Edit-mode fixtures invoke lifecycle callbacks explicitly, as the earlier harnesses do.
            start();Call(a,"OnDisable");clean();Call(a,"OnEnable");
            // Keep compatibility with a visual action requested without R2: releasing R2 alone is not a cancellation.
            start();Get<Hand3DRig>(f,"rig").GetComponent<Hand3DInteractionCoordinator>().EndArmSession();Check(a.IsRunning,"Visual action incorrectly requires R2");a.CancelAction();
            // Existing coordinator fixtures test real door/document acquisition while PointClick is active.
            Call(a,"OnDisable");Call(a,"OnEnable");Check(a.PointClickWeight==0,"Reactivation retained output");
        }
        static Hand3DRuntimePoses ProfileData(object f)
        {
            var rig=Get<Hand3DRig>(f,"rig");var original=rig.RuntimePoses;var copy=UnityEngine.Object.Instantiate(original);var data=Hand3DPoseLibrary.Clone(original.Data);var shoulder=data.skeleton.Single(b=>b.role==S.Role.Shoulder);
            var profiles=new List<P>(data.profiles);
            foreach(int degrees in new[]{10,30})profiles.Add(new P{id="Action.test."+degrees,contexts=P.Context.Action,joints=new[]{new P.JointValue{jointPath=shoulder.path,referenceId=shoulder.parentId,rotation=shoulder.sourceLocalRotation*Quaternion.Euler(0,degrees,0),storedChannels=P.Channels.Rotation,influenceChannels=P.Channels.Rotation,captured=true,weight=1}}});
            var wrist=data.skeleton.Single(b=>b.role==S.Role.Wrist);
            profiles.Add(new P{id="Action.test.wrist",contexts=P.Context.Action,joints=new[]{new P.JointValue{jointPath=wrist.path,referenceId=data.cameraId,space=P.Space.CameraRelative,rotation=Quaternion.Euler(15,30,10),storedChannels=P.Channels.Rotation,influenceChannels=P.Channels.Rotation,captured=true,weight=1}}});
            data.profiles=profiles.ToArray();copy.Configure(data,copy.Exploration,copy.HandEdit,copy.Extended,copy.Flexed,copy.Gestures,copy.GrabDoor,copy.GrabDocument);rig.SelectPoseSystem(Hand3DRig.PoseSystem.Universal,copy);Check(rig.UniversalActive,"Action fixture rejected");return copy;
        }
        static void Profiles()
        {
            var f=Create(true);var rig=Get<Hand3DRig>(f,"rig");var arm=Get<Hand3DArmMovement>(f,"arm");ProfileData(f);Step(f,1f);
            var reference=rig.RuntimePoses.Data.skeleton.Single(b=>b.role==S.Role.Shoulder);var shoulder=rig.transform.Find(reference.path);var original=shoulder.localRotation;
            var low=Definition<Hand3DProfileAction>();var high=Definition<Hand3DProfileAction>();Set(low,"profileId","Action.test.10");Set(high,"profileId","Action.test.30");Set(low,"priority",10);Set(high,"priority",20);
            var a=Get<Hand3DPoseActions>(f,"actions");Bind(f,high,low);Check(a.TryExecute(),"Profile actions failed");Step(f,.15f);
            Check(Quaternion.Angle(shoulder.localRotation,reference.sourceLocalRotation*Quaternion.Euler(0,30,0))<.05f,"Priority followed accidental list order");
            a.CancelAction();Step(f,1f);Check(Quaternion.Angle(shoulder.localRotation,original)<.05f,"Profile failed to restore shoulder");
            var wristAction=Definition<Hand3DProfileAction>();Set(wristAction,"profileId","Action.test.wrist");var beforeWrist=arm.Hand.rotation;Bind(f,wristAction);Check(a.TryExecute(),"Wrist profile did not begin");Step(f,.15f);Check(Quaternion.Angle(arm.Hand.rotation,arm.ArmCamera.transform.rotation*Quaternion.Euler(15,30,10))<.05f,"Absolute action wrist did not override resolved Point orientation");a.CancelAction();Step(f,1f);Check(Quaternion.Angle(arm.Hand.rotation,beforeWrist)<.05f,"Wrist profile left residual orientation");Bind(f,high,low);
            arm.BeginControl();Check(a.TryExecute(),"Profile during movement rejected");
            for(int i=0;i<30;i++){arm.MoveHand(new Vector2(Mathf.Sin(i)*5,Mathf.Cos(i)*5),true);Step(f,.01f);Check(rig.UniversalActive,"Profile/movement failed");Check(Quaternion.Angle(arm.Hand.rotation,rig.UniversalComposition.FinalWrist)<.05f,"Action lost wrist after IK");}
            arm.EndControl();a.CancelAction();Step(f,1f);Check((int)Get<Hand3DFingerState>(f,"state").Current==29,"Profile changed logical state");
            rig.SelectPoseSystem(Hand3DRig.PoseSystem.Legacy);Check(!a.TryExecute(),"Legacy accepted unsupported full-profile action");
        }
        static void WorldPolicy()
        {
            var f=Create(false);var ev=Definition<Hand3DEventAction>();Set(ev,"affectsWorld",true);var b=Bind(f,ev);int invoked=0;b.onEvent.AddListener(()=>invoked++);var a=Get<Hand3DPoseActions>(f,"actions");var input=Get<Hand3DRig>(f,"rig").GetComponent<Hand3DInputController>();
            Check(!a.TryExecute(),"World action without R2");Set(input,"<IsArmModeActive>k__BackingField",true);Check(a.TryExecute() && invoked==1,"Exploration world event gate");
            var arm=Get<Hand3DArmMovement>(f,"arm");arm.ToggleMode();Step(f,1f);Set(input,"<IsArmModeActive>k__BackingField",false);Check(!a.TryExecute(),"World action in HandEdit without explicit opt-in");
            Set(ev,"allowWorldInHandEdit",true);Check(a.TryExecute() && invoked==2,"Explicit HandEdit permission ignored");
            Set(ev,"allowedModes",Hand3DActionModes.Exploration);Check(!a.TryExecute(),"Per-action mode mask ignored");
        }
        static void Contact(bool universal)
        {
            foreach(bool document in new[]{false,true})
            {
                var factory=typeof(Phase1Validation);var f=factory.GetMethod("Create",Flags).Invoke(null,new object[]{false});var rig=Get<Hand3DRig>(f,"rig");var arm=Get<Hand3DArmMovement>(f,"movement");var a=rig.GetComponent<Hand3DPoseActions>();Call(a,"OnDisable");Call(a,"OnEnable");
                if(universal)rig.SelectPoseSystem(Hand3DRig.PoseSystem.Universal);
                var state=rig.GetComponent<Hand3DFingerState>();for(int i=0;i<5;i++)state.SetFlexed((Finger)i,(29&(1<<i))!=0);Call(rig,"UpdateRig",1f);var center=Get<Vector2>(f,"center");Call(f,"Move",center);
                factory.GetMethod(document?"Document":"Door",Flags).Invoke(null,document?new object[]{f,"Action paper",center,.7f}:new object[]{f,"Action door",center,.7f,false,true});
                Check(a.TryExecute(),"Pre-contact PointClick failed");Call(a,"Tick",.04f);Check(a.PointClickWeight>0,"Pre-contact weight missing");
                var adapter=Get<MonoBehaviour>(f,document?"document":"door");Physics.SyncTransforms();Call(adapter,"Tick",1f/60f);var coordinator=rig.GetComponent<Hand3DInteractionCoordinator>();
                Check(coordinator.IsOccupied && !a.IsRunning && a.PointClickWeight==0 && a.VisualContributions.Count==0,"Acquisition did not cancel action");Check(!a.TryExecute(),"Occupied hand accepted action");
                Call(rig,"UpdateRig",1f);coordinator.CancelSession();Call(rig,"UpdateRig",1f);Call(Get<Hand3DDocumentDisplay>(f,"display"),"Tick",1f);Check(state.Current==(FlexedFingers)29,"Interaction lost logical state");Check(a.TryExecute(),"Action did not recover after interaction");a.CancelAction();
            }
        }
        static void Stress(bool universal)
        {
            var f=Create(universal);var a=Get<Hand3DPoseActions>(f,"actions");var arm=Get<Hand3DArmMovement>(f,"arm");var rig=Get<Hand3DRig>(f,"rig");var bones=Get<Transform[]>(f,"bones");var scales=bones.Select(b=>b.localScale).ToArray();
            int matrixErrors=0;Application.LogCallback count=(text,stack,type)=>{if(text.Contains("Quaternion To Matrix"))matrixErrors++;};Application.logMessageReceived+=count;
            try
            {
                arm.BeginControl();
                for(int frame=0;frame<2000;frame++)
                {
                    if(frame%40==0){Call(f,"Mask",29);a.TryExecute();}
                    if(frame%120==45)Call(f,"Mask",3);
                    if(frame>0 && frame%500==0)arm.ToggleMode();
                    arm.MoveHand(new Vector2(Mathf.Sin(frame*.1f),Mathf.Cos(frame*.12f))*2,true);Step(f,1f/120f);
                    Check(!universal || rig.UniversalActive,"Universal fell back during action stress");
                    foreach(var bone in bones){var q=bone.localRotation;double norm=(double)q.x*q.x+(double)q.y*q.y+(double)q.z*q.z+(double)q.w*q.w;Check(!double.IsNaN(norm) && Math.Abs(norm-1)<1e-5,"Invalid action rotation");}
                }
                a.CancelAction();arm.EndControl();Step(f,1f);Check(matrixErrors==0,"Quaternion error during action stress");Check(bones.Select((b,i)=>b.localScale==scales[i]).All(v=>v),"Action stress changed scales");
            }finally{Application.logMessageReceived-=count;}
        }
    }
}
