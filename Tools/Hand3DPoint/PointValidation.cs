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
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Metroidbrainia
{
    public static class PointValidation
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        [Serializable] class Row { public string name,status,detail; }
        [Serializable] class Report { public string runId,scope; public int assertions; public float maxTipError; public Row[] cases; }
        [Serializable] class Preparation { public string runId,fingertip,bellSequence,door; public Vector3 offset; public int bindings; public string[] originalComponents; }
        static readonly List<Row> rows=new List<Row>();static int checks;static float maxTipError;
        static string RunId(){var args=Environment.GetCommandLineArgs();return args[Array.IndexOf(args,"-phase5RunId")+1];}
        static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,Flags).GetValue(o);
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethods(Flags).Single(m=>m.Name==name && m.GetParameters().Length==args.Length).Invoke(o,args);
        static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
        static string caseFilter;
        static bool Selected(string name)=>string.IsNullOrEmpty(caseFilter) || System.Text.RegularExpressions.Regex.IsMatch(name,"^"+System.Text.RegularExpressions.Regex.Escape(caseFilter).Replace("\\*",".*").Replace("\\?",".")+"$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static void Case(string name,Action test){if(!Selected(name))return;try{test();rows.Add(new Row{name=name,status="PASS"});}catch(Exception e){Debug.LogException(e);rows.Add(new Row{name=name,status="FAIL",detail=e.ToString()});}}
        static Hand3DRig Load(){EditorSceneManager.OpenScene("Assets/Scenes/TestArmScene.unity");return Object.FindObjectsByType<Hand3DRig>().Single();}
        public static void Prepare()
        {
            Hand3DPoseConversion.IsolatedRoot();try
            {
                var rig=Load();var scene=rig.gameObject.scene;Hand3DPointPreparation.Prepare(rig);
                string sequence="",doorPath="";
                // Read the original puzzle's configuration, never its state or another map subsystem.
                var prototype=EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity",OpenSceneMode.Additive);
                try
                {
                    var original=prototype.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BellSequencePuzzle>(true)).Single();
                    var source=new SerializedObject(original);var required=source.FindProperty("requiredSequence");
                    var bells=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BellInteractable>(true)).ToDictionary(b=>Hand3DPointPreparation.Path(b.transform));
                    var existing=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BellSequencePuzzle>(true)).SingleOrDefault();
                    if(existing==null)
                    {
                        var go=new GameObject("Hand3DPointBellSequence");SceneManager.MoveGameObjectToScene(go,scene);existing=go.AddComponent<BellSequencePuzzle>();
                    }
                    var puzzle=new SerializedObject(existing);puzzle.FindProperty("requiredSequence").arraySize=required.arraySize;
                    var names=new List<string>();
                    for(int i=0;i<required.arraySize;i++)
                    {
                        var originalBell=(BellInteractable)required.GetArrayElementAtIndex(i).objectReferenceValue;
                        var bell=bells[Hand3DPointPreparation.Path(originalBell.transform)];names.Add(bell.name);
                        puzzle.FindProperty("requiredSequence").GetArrayElementAtIndex(i).objectReferenceValue=bell;
                        var b=new SerializedObject(bell);b.FindProperty("puzzle").objectReferenceValue=existing;b.ApplyModifiedPropertiesWithoutUndo();
                    }
                    var originalDoor=(DoorHandInteractable)source.FindProperty("targetDoor").objectReferenceValue;doorPath=Hand3DPointPreparation.Path(originalDoor.transform);
                    puzzle.FindProperty("targetDoor").objectReferenceValue=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DoorHandInteractable>(true)).Single(d=>Hand3DPointPreparation.Path(d.transform)==doorPath);
                    puzzle.ApplyModifiedPropertiesWithoutUndo();sequence=string.Join(" -> ",names);
                }
                finally{EditorSceneManager.CloseScene(prototype,true);}
                EditorSceneManager.SaveScene(scene,"Assets/PointConfigured.unity",true);
                var adapter=rig.GetComponent<Hand3DPointInteractor>();
                File.WriteAllText("point-preparation.json",JsonUtility.ToJson(new Preparation{runId=RunId(),fingertip=Hand3DPointPreparation.Path(adapter.PointFingertip),offset=Get<Vector3>(adapter,"fingertipOffset"),bindings=Get<Hand3DPointInteractor.Target[]>(adapter,"targets").Length,bellSequence=sequence,door=doorPath,originalComponents=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<IHandActionInteractable>(true)).Select(c=>c.GetType().Name).ToArray()},true));
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void Run()
        {
            Hand3DPoseConversion.IsolatedRoot();
            rows.Clear();checks=0;maxTipError=0;
            var args=Environment.GetCommandLineArgs();int selection=Array.IndexOf(args,"-pointCase");
            caseFilter=selection>=0?args[selection+1]:null;
            Case("configured_scene_and_original_puzzle",Configured);
            foreach(bool universal in new[]{false,true})
            {
                bool u=universal;
                foreach(var origin in new[]{Hand3DActionInput.Mouse,Hand3DActionInput.Gamepad})
                {var source=origin;Case("bell_"+u+"_"+origin,()=>Bell(u,source));Case("switch_"+u+"_"+origin,()=>Switch(u,source));
                    Case("auto_bell_"+u+"_"+origin,()=>Bell(u,source,false));Case("auto_switch_"+u+"_"+origin,()=>Switch(u,source,false));}
                foreach(string scenario in new[]{"empty","beside","original_reach","override_reach","positive_tolerance","aligned","disabled_front","destroyed","destroyed_before","collider_disabled_before","receiver_disabled_before","late_blocker","duplicate_targets","closed_door","parent_receiver","trigger_receiver","player_collider","other_scene"})
                {string test=scenario;Case("auto_"+test+"_"+u,()=>Automatic(u,test));}
                foreach(string scenario in new[]{"no_target","reach","wall","trigger","several","tie","rapid_tip","viewmodel_far","handedit","no_control","door","document","cancel","disable","move_target","gesture_before","disabled_target","blocked_layers","invisible_tip","gesture_after"})
                {string test=scenario;Case(test+"_"+u,()=>Scenario(u,test));}
                Case("32_masks_"+u,()=>Masks(u));Case("bell_puzzle_"+u,()=>Puzzle(u));
                Case("reentrant_single_activation_"+u,()=>Reentrant(u));
                Case("animated_geometric_fingertip_"+u,()=>Geometry(u));
            }
            foreach(string scenario in new[]{"door_without_documents","door_wall","document","document_wall"})
            {
                string test=scenario;Case("contact_regression_"+test,()=>
                {
                    var result=typeof(Phase1Validation).GetMethod("Scenario",Flags).Invoke(null,new object[]{test,false});
                    Check(Get<string>(result,"outcome")== (test.EndsWith("_wall")?"False":"True"),"Existing contact changed: "+test);
                });
            }
            foreach(string scenario in new[]{"pickup_return","manual","several","duplicates","activation","spawn","reach","wall","initial_state","other_scene","source_refresh","occupied"})
            {string test=scenario;Case("document_auto_"+test,()=>AutomaticDocument(test));}
            Case("document_auto_related_contacts",()=>
            {
                foreach(string test in new[]{"door_without_documents","door_wall","document","document_wall"})
                {
                    var result=typeof(Phase1Validation).GetMethod("Scenario",Flags).Invoke(null,new object[]{test,false});
                    Check(Get<string>(result,"outcome")== (test.EndsWith("_wall")?"False":"True"),"Related contact changed: "+test);
                }
            });
            Case("document_auto_point_compatibility",()=>Bell(true,Hand3DActionInput.Mouse,false));
            foreach(string scenario in new[]{"initial","manual","activated_slow","spawned_fast","registered_fast","source_reach","no_contact","far","wall","dense"})
            {string test=scenario;Case("document_response_"+test,()=>DocumentResponse(test));}
            foreach(string scenario in new[]{"off","throttle","references","not_ready","reach","obstacle","pickup","handedit","projection","display","callback_destroyed","display_transition"})
            {string test=scenario;Case("document_diagnostic_"+test,()=>DocumentDiagnostics(test));}
            if(rows.Count==0)rows.Add(new Row{name="selection",status="FAIL",detail="No Point cases match: "+caseFilter});
            File.WriteAllText("point-results.json",JsonUtility.ToJson(new Report{runId=RunId(),scope="Isolated Unity numeric/state tests; input origin intentions Mouse/Gamepad, not physical devices or Game visual approval.",assertions=checks,maxTipError=maxTipError,cases=rows.ToArray()},true));
            EditorApplication.Exit(rows.Any(r=>r.status=="FAIL")?1:0);
        }
        sealed class Fixture
        {
            public object original;public Hand3DRig rig;public Hand3DPoseActions actions;public Hand3DArmMovement arm;public Hand3DInputController input;
            public Hand3DPointInteractor point;public Hand3DFingerState state;public Camera camera;public Vector2 center;
            public Hand3DInteractionCoordinator coordinator;public Hand3DInteractionPose interaction;public MonoBehaviour door,document;
        }
        static Fixture Create(bool universal)
        {
            var original=typeof(Phase1Validation).GetMethod("Create",Flags).Invoke(null,new object[]{false});
            var rig=Get<Hand3DRig>(original,"rig");var arm=Get<Hand3DArmMovement>(original,"movement");var actions=rig.GetComponent<Hand3DPoseActions>();
            Call(actions,"OnDisable");Call(actions,"OnEnable");if(universal)rig.SelectPoseSystem(Hand3DRig.PoseSystem.Universal);
            var point=rig.GetComponent<Hand3DPointInteractor>();var marker=Get<Transform>(original,"marker");Set(point,"pointFingertip",marker);Set(point,"fingertipOffset",Vector3.zero);
            var camera=Get<Camera>(original,"camera");Get<Hand3DContactDetection>(point,"detection").Configure(camera,marker,Get<UnityEngine.UI.RawImage>(original,"armImage"),null,Vector2.zero,Physics.DefaultRaycastLayers);
            var click=AssetDatabase.LoadAssetAtPath<Hand3DPointClickAction>(Hand3DPointPreparation.Folder+"/TestArmPointClick.asset");
            var world=AssetDatabase.LoadAssetAtPath<Hand3DPointWorldAction>(Hand3DPointPreparation.Folder+"/TestArmPointWorld.asset");
            Set(actions,"bindings",new[]{new Hand3DPoseActions.Binding{gesture=(FlexedFingers)29,actions=new Hand3DGestureAction[]{click,world,world}}});
            var f=new Fixture{original=original,rig=rig,arm=arm,actions=actions,point=point,state=rig.GetComponent<Hand3DFingerState>(),camera=camera,center=Get<Vector2>(original,"center"),input=Get<Hand3DInputController>(original,"input"),coordinator=rig.GetComponent<Hand3DInteractionCoordinator>(),interaction=rig.GetComponent<Hand3DInteractionPose>(),door=Get<MonoBehaviour>(original,"door"),document=Get<MonoBehaviour>(original,"document")};
            Set(point,"targets",Array.Empty<Hand3DPointInteractor.Target>());Mask(f,29);Move(f,f.center);return f;
        }
        static void Mask(Fixture f,int mask){for(int i=0;i<5;i++)f.state.SetFlexed((Finger)i,(mask&(1<<i))!=0);Call(f.rig,"RequestTransition");Call(f.rig,"UpdateRig",1f);}
        static void Move(Fixture f,Vector2 screen)=>Call(f.original,"Move",screen);
        static void Finish(Fixture f){Call(f.actions,"Tick",1f);Call(f.rig,"UpdateRig",1f);}
        static void Bind(Fixture f,float tolerance,params MonoBehaviour[] targets)=>Set(f.point,"targets",targets.Select(t=>new Hand3DPointInteractor.Target{receiver=t,collider=t.GetComponent<Collider>(),maximumDistance=1.8f,tolerancePixels=tolerance}).ToArray());
        static T ObjectAt<T>(Fixture f,Vector2 screen,float depth) where T:Component
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=typeof(T).Name;go.transform.position=f.camera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,depth));go.transform.localScale=Vector3.one*.03f;
            return go.AddComponent<T>();
        }
        static BellInteractable MakeBell(Fixture f,Vector2 screen,float depth=.7f){var bell=ObjectAt<BellInteractable>(f,screen,depth);Call(bell,"Awake");return bell;}
        static void Configured()
        {
            var rig=Load();var adapter=rig.GetComponent<Hand3DPointInteractor>();Check(adapter!=null,"Missing Point adapter");
            string saved=EditorJsonUtility.ToJson(adapter),savedActions=EditorJsonUtility.ToJson(rig.GetComponent<Hand3DPoseActions>());
            Hand3DPointPreparation.Prepare(rig);Check(EditorJsonUtility.ToJson(adapter)==saved && EditorJsonUtility.ToJson(rig.GetComponent<Hand3DPoseActions>())==savedActions,"Preparation overwrote existing calibration/bindings");
            Check(adapter.PointFingertip==rig.Fingers.Single(f=>f.Finger==Finger.Index).Joints.Last().Bone,"Wrong index distal reference");
            Check(adapter.PointFingertip.IsChildOf(rig.GetComponent<Hand3DArmMovement>().Hand),"Tip outside wrist hierarchy");
            Check(Get<Hand3DPointInteractor.Target[]>(adapter,"targets").Length==5,"Expected four bell and one switch collider bindings");
            var bindings=Get<Hand3DPoseActions.Binding[]>(rig.GetComponent<Hand3DPoseActions>(),"bindings");Check(bindings.Single(b=>b.gesture==(FlexedFingers)29).actions.Count(a=>a is Hand3DPointWorldAction)==1,"Duplicate Point world binding");
            var puzzle=Object.FindObjectsByType<BellSequencePuzzle>().Single();var data=new SerializedObject(puzzle);
            Check(data.FindProperty("requiredSequence").arraySize==4,"Missing bell sequence");
            for(int i=0;i<4;i++){var bell=(BellInteractable)data.FindProperty("requiredSequence").GetArrayElementAtIndex(i).objectReferenceValue;Check(bell.name=="BellHinge "+(i+1),"Original sequence changed");Check(Get<BellSequencePuzzle>(bell,"puzzle")==puzzle,"Bell not connected to original puzzle logic");}
            Check(Hand3DPointPreparation.Path(((DoorHandInteractable)data.FindProperty("targetDoor").objectReferenceValue).transform)=="Mansion/Floor 1/Entrance/Wall_Door_ClosedDoor/DoorHinge","Wrong puzzle door");
        }
        static void Bell(bool universal,Hand3DActionInput input,bool registered=true)
        {
            var f=Create(universal);var bell=MakeBell(f,f.center);if(registered)Bind(f,0,bell);
            var sequence=new GameObject("Counter puzzle").AddComponent<BellSequencePuzzle>();Set(sequence,"requiredSequence",new List<BellInteractable>{bell,bell,bell,bell});Set(bell,"puzzle",sequence);
            int originalEvents=0;f.rig.GetComponent<Hand3DPoseRecognizer>().CurrentPose.OnAction.AddListener(()=>originalEvents++);
            Check(f.actions.TryExecute(input),"Point request rejected");Check(f.point.LastAccepted && Get<bool>(bell,"swinging"),"Bell original API not executed");
            Check(sequence.NextExpectedIndex==1 && originalEvents==1,"Duplicate activation/event from duplicated world action definitions");
            Check(!f.actions.TryExecute(input) && sequence.NextExpectedIndex==1,"Same active click retriggered bell");
            Call(f.actions,"Tick",.04f);Check(f.actions.PointClickWeight>0,"PointClick animation missing");
            Check(f.state.Current==(FlexedFingers)29 && !f.coordinator.IsOccupied && !f.interaction.IsGrabbing,"Point changed pose/lease");
            Finish(f);Move(f,f.center);Set(bell,"currentAngle",12f);Set(bell,"previousAngle",20f);Set(bell,"swinging",true);
            Check(f.actions.TryExecute(input) && sequence.NextExpectedIndex==2,"Repeated bell strike blocked");
            Check(Get<float>(bell,"cosineAmplitude")==12f && Get<float>(bell,"sineAmplitude")<0 && Get<float>(bell,"swingTime")==0,"Original repeat-strike swing behavior changed");
        }
        static void Switch(bool universal,Hand3DActionInput input,bool registered=true)
        {
            var f=Create(universal);var item=ObjectAt<LightSwitchInteractable>(f,f.center,.7f);var rocker=new GameObject("Rocker").transform;rocker.SetParent(item.transform,false);
            var light=new GameObject("Controlled light").AddComponent<Light>();Set(item,"switchTransform",rocker);Set(item,"controlledLights",new[]{light});Set(item,"onLocalEulerAngles",new Vector3(180,0,0));Call(item,"Awake");if(registered)Bind(f,0,item);
            int on=0,off=0;item.OnTurnedOn.AddListener(()=>on++);item.OnTurnedOff.AddListener(()=>off++);
            Check(f.actions.TryExecute(input) && item.IsOn && light.enabled && on==1 && off==0,"Switch original toggle/events not invoked exactly once");
            Finish(f);Move(f,f.center);Check(f.actions.TryExecute(input) && !f.point.LastAccepted && on==1 && item.IsOn,"Original moving lock bypassed");
            Finish(f);Call(item,"OnDisable");Move(f,f.center);Check(f.actions.TryExecute(input) && !item.IsOn && !light.enabled && off==1 && on==1,"Second accepted press did not switch off once");
            Call(item,"OnDisable");Check(Quaternion.Angle(rocker.localRotation,Quaternion.identity)<.01f,"Switch final off rotation changed");
        }
        static void Scenario(bool universal,string scenario)
        {
            var f=Create(universal);var bell=MakeBell(f,f.center,scenario=="reach"?2.1f:.7f);Bind(f,0,bell);
            if(scenario=="no_target"){Bind(f,0);Object.DestroyImmediate(bell.GetComponent<Collider>());}
            if(scenario=="wall" || scenario=="trigger")
            {var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=f.camera.ScreenToWorldPoint(new Vector3(f.center.x,f.center.y,.35f));wall.transform.localScale=Vector3.one*.1f;wall.GetComponent<Collider>().isTrigger=scenario=="trigger";}
            if(scenario=="several" || scenario=="tie")
            {
                bell.transform.position=f.camera.ScreenToWorldPoint(new Vector3(f.center.x+20,f.center.y,.7f));bell.transform.localScale=Vector3.one*.008f;
                var other=MakeBell(f,f.center+Vector2.right*(scenario=="tie"?-20:40));other.transform.localScale=Vector3.one*.008f;Bind(f,60,bell,other);
                Check(f.actions.TryExecute() && !f.point.LastAccepted && !Get<bool>(bell,"swinging") && !Get<bool>(other,"swinging"),"Nearby manual targets activated without a fingertip hit");return;
            }
            if(scenario=="rapid_tip")Move(f,f.center+Vector2.right*300);
            if(scenario=="viewmodel_far")
            {
                f.rig.transform.position+=Vector3.right*100;f.arm.ArmCamera.transform.position+=Vector3.right*100;
                Check(Vector3.Distance(f.point.FingertipPosition,bell.transform.position)>50,"Fixture did not separate ViewModel from gameplay space");
                Check(f.actions.TryExecute() && f.point.LastAccepted,"Visual contact incorrectly required ViewModel/object world coincidence");return;
            }
            if(scenario=="disabled_target")bell.enabled=false;
            if(scenario=="blocked_layers")bell.gameObject.layer=2;
            if(scenario=="invisible_tip")Get<Transform>(f.original,"marker").position=f.arm.ArmCamera.transform.position-f.arm.ArmCamera.transform.forward*.1f;
            if(scenario=="handedit"){f.arm.ToggleMode();Call(f.arm,"UpdateMovement",1f);Call(f.rig,"UpdateRig",1f);Move(f,f.center);}
            if(scenario=="no_control")Set(f.input,"<IsArmModeActive>k__BackingField",false);
            if(scenario=="door" || scenario=="document")
            {Check(f.coordinator.TryAcquire(scenario=="door"?f.door:f.document,scenario=="door"?Hand3DInteractionState.Door:Hand3DInteractionState.Document,scenario=="door",Quaternion.identity),"Fixture could not acquire lease");Check(!f.actions.TryExecute() && !Get<bool>(bell,"swinging"),"Occupied hand activated Point");f.coordinator.CancelSession();return;}
            if(scenario=="cancel" || scenario=="disable" || scenario=="move_target" || scenario=="gesture_before")
            {
                var ev=ScriptableObject.CreateInstance<Hand3DEventAction>();var data=new SerializedObject(ev);data.FindProperty("priority").intValue=-10;data.ApplyModifiedPropertiesWithoutUndo();
                var binding=Get<Hand3DPoseActions.Binding[]>(f.actions,"bindings")[0];binding.actions=new[]{(Hand3DGestureAction)ev}.Concat(binding.actions).ToArray();
                if(scenario=="cancel")binding.onEvent.AddListener(f.actions.CancelAction);
                else if(scenario=="disable")binding.onEvent.AddListener(()=>f.point.enabled=false);
                else if(scenario=="gesture_before")binding.onEvent.AddListener(()=>Mask(f,3));
                else binding.onEvent.AddListener(()=>bell.transform.position=f.camera.transform.position+f.camera.transform.forward*3f);
                Check(f.actions.TryExecute() && !Get<bool>(bell,"swinging"),"Cancelled/disabled Point action still executed");Object.DestroyImmediate(ev);return;
            }
            if(scenario=="gesture_after")
            {Check(f.actions.TryExecute() && Get<bool>(bell,"swinging"),"Initial action failed");Mask(f,3);Check(!f.actions.IsRunning && f.state.Current==(FlexedFingers)3,"Changing gesture failed to cancel visual action");return;}
            Check(f.actions.TryExecute(),"Visual PointClick must remain available without world contact");
            Check(!Get<bool>(bell,"swinging"),"Invalid world contact activated bell: "+scenario);
            if(scenario=="rapid_tip")
            {Finish(f);Move(f,f.center);Check(f.actions.TryExecute() && Get<bool>(bell,"swinging"),"Fresh contact not sampled after rapid movement");}
        }
        static void DocumentDiagnostics(string scenario)
        {
            var f=Create(true);Mask(f,3);Move(f,f.center);
            var interactor=(Hand3DDocumentInteractor)f.document;var detection=Get<Hand3DContactDetection>(interactor,"detection");
            var doc=NewDocument(f,f.center,scenario=="reach"?1.2f:.7f);int picked=0;
            Get<UnityEngine.Events.UnityEvent>(doc,"onPickedUp").AddListener(()=>picked++);
            if(scenario=="callback_destroyed")Get<UnityEngine.Events.UnityEvent>(doc,"onPickedUp").AddListener(()=>Object.DestroyImmediate(doc.gameObject));
            if(scenario=="off" || scenario=="throttle" || scenario=="display_transition")Move(f,f.center+Vector2.right*200);
            if(scenario=="references")detection.Configure(null,detection.HandPoint,Get<UnityEngine.UI.RawImage>(f.original,"armImage"),null,Vector2.zero,Physics.DefaultRaycastLayers);
            if(scenario=="not_ready")Get<MeshRenderer>(doc,"worldRenderer").enabled=false;
            if(scenario=="obstacle")
            {var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=f.camera.ScreenToWorldPoint(new Vector3(f.center.x,f.center.y,.35f));wall.transform.localScale=Vector3.one*.1f;}
            if(scenario=="handedit"){f.arm.ToggleMode();Call(f.arm,"UpdateMovement",1f);Call(f.rig,"UpdateRig",1f);}
            if(scenario=="projection")Get<Transform>(f.original,"marker").position=f.arm.ArmCamera.transform.position-f.arm.ArmCamera.transform.forward*.1f;
            if(scenario=="display")Set(f.rig.GetComponent<Hand3DDocumentDisplay>(),"paperGrip",null);
            var logs=new List<string>();Application.LogCallback capture=(message,stack,type)=>{if(message.StartsWith("[Hand3D Documents]"))logs.Add(message);};
            Application.logMessageReceived+=capture;
            try
            {
                Set(interactor,"debugContact",scenario!="off");Call(interactor,"Tick",1f/60);
                if(scenario=="off"){Check(logs.Count==0,"Debug-off emitted diagnostic logs");return;}
                string expected=scenario=="references"?"References":scenario=="not_ready"?"BindingNotReady":scenario=="reach"?"Reach":scenario=="obstacle"?"Obstacle"
                    :scenario=="pickup"?"Acquired":scenario=="handedit"?"ControlMode":scenario=="projection"?"HandProjection":scenario=="display" || scenario=="callback_destroyed"?"PickupRejected":"VisualContact";
                Check(logs.Count==1 && logs[0].Contains("stage="+expected),"Wrong/missing diagnostic: "+string.Join(" | ",logs));
                Check(logs[0].Contains("gesture=") && logs[0].Contains("detection=") && logs[0].Contains("coordinator="),"Missing diagnostic context");
                if(scenario=="references")Check(logs[0].Contains("missing world camera"),"Reference rejection was not explained");
                if(scenario=="not_ready")Check(logs[0].Contains("renderer disabled"),"Binding rejection was not explained");
                if(scenario=="obstacle")Check(logs[0].Contains("blockerDistance=") && logs[0].Contains("occlusionRay="),"Missing obstacle distances");
                if(scenario=="pickup")Check(picked==1 && interactor.IsHolding && logs[0].Contains("onPickedUp dispatched"),"Pickup/event diagnostic changed behavior");
                if(scenario=="display")Check(!interactor.IsHolding && picked==0 && logs[0].Contains("missing visual root/grip"),"Display rejection not diagnosed");
                if(scenario=="callback_destroyed")Check(picked==1 && logs[0].Contains("onPickedUp dispatched"),"Callback destruction broke diagnostic/event ordering");
                if(scenario=="display_transition")
                {
                    Set(f.rig.GetComponent<Hand3DDocumentDisplay>(),"paperGrip",null);Move(f,f.center);Call(interactor,"Tick",1f/60);
                    Check(logs.Count==2 && logs[1].Contains("stage=PickupRejected"),"Transient pickup rejection was hidden by log throttling");
                    for(int i=0;i<60;i++)Call(interactor,"Tick",1f/60);
                    Check(logs.Count==2 && picked==0,"Repeated display rejection flooded logs or invoked pickup");
                }
                if(scenario=="throttle")
                {
                    Check(logs[0].Contains("separation=") && logs[0].Contains("tolerance=") && logs[0].Contains("collider="),"Missing visual metrics");
                    for(int i=0;i<120;i++)Call(interactor,"Tick",1f/60);
                    Check(logs.Count==1,"Repeated frames flooded the console");
                    double now=Time.unscaledTimeAsDouble;
                    for(int i=1;i<=6;i++)Call(interactor,"ReportDiagnostic",now+i*2.1);
                    Check(logs.Count==4 && logs.Count(l=>l.Contains("Summary"))==3,"Waiting summaries were not capped at three");
                    Set(interactor,"debugContact",false);Call(interactor,"Tick",1f/60);Check(logs.Count==4,"Turning diagnostics off did not stop logs");
                }
            }
            finally {Application.logMessageReceived-=capture;}
        }
        static void DocumentResponse(string scenario)
        {
            var f=Create(true);Mask(f,3);
            var interactor=(Hand3DDocumentInteractor)f.document;var display=f.rig.GetComponent<Hand3DDocumentDisplay>();
            var doc=NewDocument(f,f.center,scenario=="far"?1.2f:.7f);int picked=0;Get<UnityEngine.Events.UnityEvent>(doc,"onPickedUp").AddListener(()=>picked++);
            if(scenario=="manual")
            {
                var binding=doc.gameObject.AddComponent<Hand3DDocumentBinding>();Set(binding,"document",doc);
                foreach(string field in new[]{"worldRenderer","worldCollider","heldSprite","localizedText","onPickedUp"})Set(binding,field,doc.GetType().GetField(field,Flags).GetValue(doc));
                Call(binding,"OnEnable");
            }
            if(scenario=="wall")
            {var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=f.camera.ScreenToWorldPoint(new Vector3(f.center.x,f.center.y,.35f));wall.transform.localScale=Vector3.one*.1f;}
            if(scenario=="dense")
                for(int i=0;i<40;i++){var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=f.camera.ScreenToWorldPoint(new Vector3(f.center.x+250,f.center.y,.5f));blocker.transform.localScale=Vector3.one*.002f;}
            bool fast=scenario=="spawned_fast" || scenario=="registered_fast";
            bool delayed=scenario=="activated_slow" || scenario=="spawned_fast";
            if(delayed)doc.gameObject.SetActive(false);
            if(scenario=="source_reach")Set(doc,"maxInteractionDistance",.1f);
            if(delayed || scenario=="registered_fast" || scenario=="source_reach")
            {
                Move(f,f.center-Vector2.right*120);Call(interactor,"Tick",1f/60);
                Check(!interactor.IsHolding,"Setup acquired outside contact");
                if(scenario=="spawned_fast")
                {
                    Object.DestroyImmediate(doc.gameObject);doc=NewDocument(f,f.center,.7f);
                    Get<UnityEngine.Events.UnityEvent>(doc,"onPickedUp").AddListener(()=>picked++);
                }
                else if(delayed)doc.gameObject.SetActive(true);
                if(scenario=="source_reach")Set(doc,"maxInteractionDistance",1.5f);
            }
            Move(f,f.center+Vector2.right*(fast?120:scenario=="activated_slow"?-Get<float>(f.original,"documentRadius")*.8f:scenario=="no_contact"?200:0));
            Call(interactor,"Tick",1f/60);
            bool expected=scenario!="no_contact" && scenario!="far" && scenario!="wall";
            Check(interactor.IsHolding==expected,"First-frame result: "+scenario+"; elapsed="+Get<float>(interactor,"discoveryElapsed")+"; "+Get<string>(interactor,"reason"));
            Check(picked==(expected?1:0),"Pickup event count on first contact: "+scenario);
            if(scenario=="dense")Check(Get<Collider[]>(interactor,"nearbyColliders").Length>32,"Dense local query was not tested beyond its initial buffer");
            if(!expected)return;
            Call(interactor,"Tick",1f/60);Check(picked==1,"Held contact repeated pickup event");
            Set(f.input,"<IsArmModeActive>k__BackingField",false);Call(interactor,"Tick",1f/60);Call(display,"Tick",1f);
            Check(!interactor.IsHolding && Get<MeshRenderer>(doc,"worldRenderer").enabled && Get<Collider>(doc,"worldCollider").enabled,"First-frame pickup did not return correctly");
        }
        static DocumentHandInteractable NewDocument(Fixture f,Vector2 screen,float depth)
        {
            var template=Object.FindObjectsByType<DocumentHandInteractable>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(d=>Get<Sprite>(d,"heldSprite")!=null);
            var paper=GameObject.CreatePrimitive(PrimitiveType.Quad);paper.name="Automatic document";paper.transform.localScale=Vector3.one*.02f;
            paper.transform.position=f.camera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,depth));
            var document=paper.AddComponent<DocumentHandInteractable>();
            Set(document,"worldRenderer",paper.GetComponent<MeshRenderer>());Set(document,"worldCollider",paper.GetComponent<Collider>());
            Set(document,"heldSprite",Get<Sprite>(template,"heldSprite"));Set(document,"localizedText",Get<UnityEngine.Localization.LocalizedString>(template,"localizedText"));
            Set(document,"documentFont",template.DocumentFont);Set(document,"documentFontSize",37f);Set(document,"documentTextColor",Color.blue);
            Set(document,"maxInteractionDistance",1.5f);Set(document,"onPickedUp",new UnityEngine.Events.UnityEvent());return document;
        }
        static void AutomaticDocument(string scenario)
        {
            var f=Create(true);Mask(f,3);Move(f,f.center);
            var interactor=(Hand3DDocumentInteractor)f.document;
            var display=f.rig.GetComponent<Hand3DDocumentDisplay>();
            var doc=NewDocument(f,f.center,scenario=="reach"?1.2f:.7f);
            var renderer=Get<MeshRenderer>(doc,"worldRenderer");var collider=Get<Collider>(doc,"worldCollider");
            var originalEvent=Get<UnityEngine.Events.UnityEvent>(doc,"onPickedUp");int picked=0;originalEvent.AddListener(()=>picked++);
            string serialized=EditorJsonUtility.ToJson(doc);Vector3 position=doc.transform.position,scale=doc.transform.localScale;Quaternion rotation=doc.transform.rotation;
            Hand3DDocumentBinding manual=null;int manualPicked=0;Scene other=default;
            if(scenario=="manual")
            {
                manual=doc.gameObject.AddComponent<Hand3DDocumentBinding>();Set(manual,"document",doc);Set(manual,"worldRenderer",renderer);Set(manual,"worldCollider",collider);
                Set(manual,"heldSprite",Get<Sprite>(doc,"heldSprite"));Set(manual,"localizedText",Get<UnityEngine.Localization.LocalizedString>(doc,"localizedText"));
                Set(manual,"maxInteractionDistance",.2f);var ev=new UnityEngine.Events.UnityEvent();ev.AddListener(()=>manualPicked++);Set(manual,"onPickedUp",ev);Call(manual,"OnEnable");
            }
            if(scenario=="activation")doc.gameObject.SetActive(false);
            if(scenario=="spawn")Object.DestroyImmediate(doc.gameObject);
            if(scenario=="initial_state"){renderer.enabled=false;collider.enabled=false;}
            if(scenario=="other_scene")
            {other=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.MoveGameObjectToScene(doc.gameObject,other);}
            if(scenario=="several")
            {var second=NewDocument(f,f.center+Vector2.right*200,.7f);Set(second,"documentFontSize",61f);Set(second,"documentTextColor",Color.red);}
            if(scenario=="wall")
            {var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=f.camera.ScreenToWorldPoint(new Vector3(f.center.x,f.center.y,.35f));wall.transform.localScale=Vector3.one*.1f;}
            if(scenario=="occupied")Check(f.coordinator.TryAcquire(f.door,Hand3DInteractionState.Door,true,Quaternion.identity),"Could not acquire door for document exclusion");
            try
            {
                Call(interactor,"Tick",.02f);
                if(scenario=="spawn")
                {doc=NewDocument(f,f.center,.7f);originalEvent=Get<UnityEngine.Events.UnityEvent>(doc,"onPickedUp");originalEvent.AddListener(()=>picked++);renderer=Get<MeshRenderer>(doc,"worldRenderer");collider=Get<Collider>(doc,"worldCollider");Call(interactor,"Tick",1.1f);}
                if(scenario=="activation")
                {Check(doc.GetComponent<Hand3DDocumentBinding>()==null,"Inactive document was modified");doc.gameObject.SetActive(true);Call(interactor,"Tick",1.1f);}
                if(scenario=="other_scene")
                {Check(doc.GetComponent<Hand3DDocumentBinding>()==null && !interactor.IsHolding && picked==0,"Cross-scene discovery/pickup");return;}
                var binding=doc.GetComponent<Hand3DDocumentBinding>();Check(binding!=null && binding.Document==doc,"Missing automatically configured binding");
                if(scenario=="manual")
                {
                    Check(binding==manual && binding.MaximumDistance==.2f && !interactor.IsHolding && picked==0,"Manual settings overwritten");
                    Set(binding,"maxInteractionDistance",1.5f);Call(interactor,"Tick",.02f);
                    Check(interactor.IsHolding && manualPicked==1 && picked==0,"Manual event override was replaced");return;
                }
                if(scenario=="reach" || scenario=="wall" || scenario=="initial_state" || scenario=="occupied")
                {
                    Check(!interactor.IsHolding && picked==0,"Invalid document was picked up: "+scenario);
                    if(scenario=="initial_state")Check(!renderer.enabled && !collider.enabled,"Discovery changed initial renderer/collider state");
                    return;
                }
                Check(interactor.ActiveDocument==binding && picked==1 && f.coordinator.Owner==interactor,"Automatic document not held exclusively/exactly once: "+Get<string>(interactor,"reason")+"; ready="+binding.IsReady+"; picked="+picked);
                Check(!renderer.enabled && !collider.enabled,"Held world document not hidden");
                Check(ReferenceEquals(Get<UnityEngine.Localization.LocalizedString>(binding,"localizedText"),Get<UnityEngine.Localization.LocalizedString>(doc,"localizedText")),"LocalizedString was replaced");
                Check(ReferenceEquals(Get<UnityEngine.Events.UnityEvent>(binding,"onPickedUp"),originalEvent),"Pickup listeners were copied instead of reusing the source event");
                var image=Get<UnityEngine.UI.Image>(display,"documentImage");var text=Get<TMPro.TMP_Text>(display,"documentText");
                Check(image.sprite==Get<Sprite>(doc,"heldSprite") && text.font==doc.DocumentFont && text.fontSize==doc.DocumentFontSize && text.color==doc.DocumentTextColor,"Document presentation/style changed");
                Check(ReferenceEquals(Get<UnityEngine.Localization.LocalizedString>(display,"activeText"),Get<UnityEngine.Localization.LocalizedString>(doc,"localizedText")),"Display not subscribed to source localization");
                Call(display,"UpdateText","Focused localized update");Check(text.text=="Focused localized update","Localization callback did not update text");
                var overlay=Get<Hand3DThumbOverlay>(display,"thumbOverlay");Check(overlay.IsConfigured && overlay.HasValidMask && overlay.gameObject.activeSelf,"Thumb overlay routing/configuration lost");
                Mask(f,29);Check(!f.actions.TryExecute(),"Point action accepted while holding a document");Mask(f,3);
                Call(interactor,"Tick",1.1f);Check(picked==1 && doc.GetComponents<Hand3DDocumentBinding>().Length==1,"Refresh duplicated binding/event");
                Check(doc.transform.position==position && doc.transform.localScale==scale && Quaternion.Angle(doc.transform.rotation,rotation)<.001f,"World placement changed");
                Set(f.input,"<IsArmModeActive>k__BackingField",false);Call(interactor,"Tick",.02f);Call(display,"Tick",1f);Call(f.rig,"UpdateRig",1f);
                Check(!interactor.IsHolding && renderer.enabled && collider.enabled && !overlay.gameObject.activeSelf,"Release did not restore document/overlay");
                if(scenario=="source_refresh")
                {
                    Set(doc,"maxInteractionDistance",.1f);Set(f.input,"<IsArmModeActive>k__BackingField",true);Call(interactor,"Tick",.02f);
                    Check(binding.MaximumDistance==.1f && !interactor.IsHolding && picked==1,"Changed source reach was ignored before the next discovery");
                    Set(f.input,"<IsArmModeActive>k__BackingField",false);
                    Set(doc,"maxInteractionDistance",1.5f);Call(interactor,"Tick",1.1f);
                }
                if(scenario=="activation")
                {doc.gameObject.SetActive(false);doc.gameObject.SetActive(true);Call(interactor,"Tick",1.1f);Check(doc.GetComponents<Hand3DDocumentBinding>().Length==1,"Reactivation duplicated binding");}
                if(scenario!="spawn")Check(EditorJsonUtility.ToJson(doc)==serialized,"Original serialized document data changed");
                Set(f.input,"<IsArmModeActive>k__BackingField",true);Move(f,f.center);Call(interactor,"Tick",.02f);
                Check(interactor.IsHolding && picked==2,"Second pickup did not emit exactly one original event");
            }
            finally
            {
                if(f.coordinator.IsOccupied)f.coordinator.CancelSession();Call(display,"Tick",1f);
                if(other.IsValid())EditorSceneManager.CloseScene(other,true);
            }
        }
        static void Automatic(bool universal,string scenario)
        {
            var f=Create(universal);var bell=MakeBell(f,f.center,scenario=="original_reach"?1.9f:.7f);
            bool expected=true;BellInteractable front=null;Scene other=default;
            if(scenario=="empty" || scenario=="destroyed"){Object.DestroyImmediate(bell.gameObject);expected=false;}
            if(scenario=="beside" || scenario=="positive_tolerance")
            {
                bell.transform.position=f.camera.ScreenToWorldPoint(new Vector3(f.center.x+25,f.center.y,.7f));bell.transform.localScale=Vector3.one*.008f;
                if(scenario=="positive_tolerance")Bind(f,100,bell);expected=false;
            }
            if(scenario=="original_reach")expected=false;
            if(scenario=="override_reach")
            {Bind(f,0,bell);Get<Hand3DPointInteractor.Target[]>(f.point,"targets")[0].maximumDistance=.3f;expected=false;}
            BellSequencePuzzle puzzle=null;
            if(scenario=="duplicate_targets")
            {
                Bind(f,0,bell,bell);puzzle=new GameObject("Duplicate target counter").AddComponent<BellSequencePuzzle>();
                Set(puzzle,"requiredSequence",new List<BellInteractable>{bell,bell,bell,bell});Set(bell,"puzzle",puzzle);
            }
            if(scenario=="aligned" || scenario=="disabled_front")
            {
                front=MakeBell(f,f.center,.35f);Bind(f,0,bell);
                if(scenario=="disabled_front"){front.enabled=false;expected=false;}
            }
            if(scenario=="closed_door")
            {
                var door=(DoorHandInteractable)typeof(Phase1Validation).GetMethod("Door",Flags).Invoke(null,new object[]{f.original,"Closed Point blocker",f.center,.35f,false,true});
                var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);panel.transform.position=f.camera.ScreenToWorldPoint(new Vector3(f.center.x,f.center.y,.35f));
                panel.transform.localScale=Vector3.one*.1f;panel.transform.SetParent(door.transform,true);expected=false;
            }
            if(scenario=="parent_receiver")
            {
                bell.GetComponent<Collider>().enabled=false;
                var child=new GameObject("Point child collider");child.transform.SetParent(bell.transform,false);child.AddComponent<SphereCollider>().radius=.5f;
            }
            if(scenario=="trigger_receiver")bell.GetComponent<Collider>().isTrigger=true;
            if(scenario=="player_collider")
            {
                var own=GameObject.CreatePrimitive(PrimitiveType.Cube);own.transform.position=f.camera.ScreenToWorldPoint(new Vector3(f.center.x,f.center.y,.3f));
                own.transform.localScale=Vector3.one*.1f;own.transform.SetParent(f.input.PlayerRoot,true);
            }
            if(scenario=="other_scene")
            {other=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.MoveGameObjectToScene(bell.gameObject,other);expected=false;}
            Hand3DEventAction ev=null;
            if(scenario=="destroyed_before" || scenario=="collider_disabled_before" || scenario=="receiver_disabled_before" || scenario=="late_blocker")
            {
                expected=false;ev=ScriptableObject.CreateInstance<Hand3DEventAction>();var data=new SerializedObject(ev);
                data.FindProperty("priority").intValue=-10;data.ApplyModifiedPropertiesWithoutUndo();
                var binding=Get<Hand3DPoseActions.Binding[]>(f.actions,"bindings")[0];binding.actions=new[]{(Hand3DGestureAction)ev}.Concat(binding.actions).ToArray();
                if(scenario=="destroyed_before")binding.onEvent.AddListener(()=>Object.DestroyImmediate(bell.gameObject));
                else if(scenario=="collider_disabled_before")binding.onEvent.AddListener(()=>bell.GetComponent<Collider>().enabled=false);
                else if(scenario=="receiver_disabled_before")binding.onEvent.AddListener(()=>bell.enabled=false);
                else binding.onEvent.AddListener(()=>
                {
                    var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=f.camera.ScreenToWorldPoint(new Vector3(f.center.x,f.center.y,.35f));wall.transform.localScale=Vector3.one*.1f;
                });
            }
            try
            {
                Check(f.actions.TryExecute(),"Automatic contact blocked visual PointClick: "+scenario);
                Check(f.point.LastAccepted==expected && f.actions.LastActionError==null,"Automatic Point acceptance changed: "+scenario);
                if(expected)Check(f.point.LastTarget==(front!=null?front:bell),"Automatic selection skipped the first receiver");
                if(bell!=null)Check(Get<bool>(bell,"swinging")== (expected && front==null),"Rear/rejected bell activated: "+scenario);
                if(puzzle!=null)Check(puzzle.NextExpectedIndex==1,"Duplicate manual targets/actions emitted multiple activations");
                Call(f.actions,"Tick",.04f);Check(f.actions.PointClickWeight>0,"PointClick missing: "+scenario);
            }
            finally
            {
                if(ev!=null)Object.DestroyImmediate(ev);
                if(other.IsValid())EditorSceneManager.CloseScene(other,true);
            }
        }
        static void Masks(bool universal)
        {
            var f=Create(universal);var bell=MakeBell(f,f.center);Bind(f,0,bell);
            for(int mask=0;mask<32;mask++)
            {Finish(f);Mask(f,mask);Move(f,f.center);Set(bell,"swinging",false);bool executed=f.actions.TryExecute();Check(executed==(mask==29),"Unexpected action for mask "+mask);Check(Get<bool>(bell,"swinging")== (mask==29),"Unexpected world activation mask "+mask);Check((int)f.state.Current==mask,"Action modified logical fingers");}
        }
        static void Reentrant(bool universal)
        {
            var f=Create(universal);var item=ObjectAt<LightSwitchInteractable>(f,f.center,.7f);Set(item,"switchTransform",item.transform);Call(item,"Awake");Bind(f,0,item);
            int on=0,off=0;item.OnTurnedOn.AddListener(()=>
            {on++;f.point.enabled=false;Call(f.point,"OnDisable");f.point.enabled=true;Call(item,"OnDisable");});item.OnTurnedOff.AddListener(()=>off++);
            Check(f.actions.TryExecute() && item.IsOn && on==1 && off==0,"Disable/re-enable callback allowed a duplicate world definition to toggle twice");
        }
        static void Geometry(bool universal)
        {
            var rig=Load();var finger=rig.Fingers.Single(f=>f.Finger==Finger.Index);var distal=finger.Joints.Last().Bone;
            var skin=rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s=>Array.IndexOf(s.bones,distal)>=0);
            int index=Array.IndexOf(skin.bones,distal);var weights=skin.sharedMesh.boneWeights;var mesh=new Mesh();int[] cap;
            try
            {
                skin.BakeMesh(mesh,true);var vertices=mesh.vertices;var axis=(distal.position-finger.Joints[finger.Joints.Length-2].Bone.position).normalized;
                float Weight(BoneWeight w)=>(w.boneIndex0==index?w.weight0:0)+(w.boneIndex1==index?w.weight1:0)+(w.boneIndex2==index?w.weight2:0)+(w.boneIndex3==index?w.weight3:0);
                var candidates=Enumerable.Range(0,vertices.Length).Where(i=>Weight(weights[i])>=.99f).ToArray();
                float Projection(int i)=>Vector3.Dot(skin.transform.TransformPoint(vertices[i])-distal.position,axis);
                float min=candidates.Min(Projection),max=candidates.Max(Projection);cap=candidates.Where(i=>Projection(i)>=max-Mathf.Max(.000001f,(max-min)*.02f)).ToArray();
            }
            finally{Object.DestroyImmediate(mesh);}
            var f=typeof(Phase5Validation).GetMethod("Create",Flags).Invoke(null,new object[]{universal,null});rig=Get<Hand3DRig>(f,"rig");
            var adapter=rig.GetComponent<Hand3DPointInteractor>();skin=rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s=>s.sharedMesh.vertexCount==872);mesh=new Mesh();
            try
            {
                for(int mask=0;mask<32;mask++)
                {
                    Call(f,"Mask",mask);
                    for(int step=0;step<4;step++)
                    {
                        Call(f,"Step",1f/60);skin.BakeMesh(mesh,true);var vertices=mesh.vertices;Vector3 center=Vector3.zero;
                        foreach(int vertex in cap)center+=skin.transform.TransformPoint(vertices[vertex]);center/=cap.Length;
                        float error=Vector3.Distance(center,adapter.FingertipPosition);maxTipError=Mathf.Max(error,maxTipError);Check(error<.0001f,"Animated fingertip diverged from its skinned distal cap: "+error);
                    }
                }
            }
            finally{Object.DestroyImmediate(mesh);}
        }
        static void Puzzle(bool universal)
        {
            var f=Create(universal);var bells=Enumerable.Range(0,4).Select(i=>MakeBell(f,f.center+Vector2.right*(i-1)*65)).ToArray();Bind(f,0,bells);
            var door=(DoorHandInteractable)typeof(Phase1Validation).GetMethod("Door",Flags).Invoke(null,new object[]{f.original,"Point puzzle door",f.center+Vector2.right*400,.7f,true,true});
            var puzzle=new GameObject("Original sequence logic").AddComponent<BellSequencePuzzle>();Set(puzzle,"requiredSequence",bells.ToList());Set(puzzle,"targetDoor",door);foreach(var bell in bells)Set(bell,"puzzle",puzzle);
            int solved=0;puzzle.OnSolved.AddListener(()=>solved++);
            void Strike(int index){Finish(f);Move(f,f.center+Vector2.right*(index-1)*65);Check(f.actions.TryExecute() && f.point.LastTarget==bells[index],"Puzzle bell contact failed");}
            Strike(1);Check(puzzle.NextExpectedIndex==0,"Wrong initial bell advanced puzzle");Strike(0);Check(puzzle.NextExpectedIndex==1,"First bell failed");Strike(2);Check(puzzle.NextExpectedIndex==0,"Wrong order failed to reset");
            for(int i=0;i<4;i++)Strike(i);Check(puzzle.IsSolved && solved==1 && (door.IsAutoOpening || door.IsOpen),"Original puzzle did not unlock/open door and emit event");Strike(0);Check(solved==1,"Solved puzzle event repeated");
        }
    }
}
