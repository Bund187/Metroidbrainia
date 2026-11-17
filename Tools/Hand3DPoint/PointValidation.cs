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
                {var source=origin;Case("bell_"+u+"_"+origin,()=>Bell(u,source));Case("switch_"+u+"_"+origin,()=>Switch(u,source));}
                foreach(string scenario in new[]{"no_target","reach","wall","trigger","several","tie","rapid_tip","viewmodel_far","handedit","no_control","door","document","cancel","disable","move_target","gesture_before","disabled_target","blocked_layers","invisible_tip","gesture_after"})
                {string test=scenario;Case(test+"_"+u,()=>Scenario(u,test));}
                Case("32_masks_"+u,()=>Masks(u));Case("bell_puzzle_"+u,()=>Puzzle(u));
                Case("reentrant_single_activation_"+u,()=>Reentrant(u));
                Case("animated_geometric_fingertip_"+u,()=>Geometry(u));
            }
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
        static void Bell(bool universal,Hand3DActionInput input)
        {
            var f=Create(universal);var bell=MakeBell(f,f.center);Bind(f,0,bell);
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
        static void Switch(bool universal,Hand3DActionInput input)
        {
            var f=Create(universal);var item=ObjectAt<LightSwitchInteractable>(f,f.center,.7f);var rocker=new GameObject("Rocker").transform;rocker.SetParent(item.transform,false);
            var light=new GameObject("Controlled light").AddComponent<Light>();Set(item,"switchTransform",rocker);Set(item,"controlledLights",new[]{light});Set(item,"onLocalEulerAngles",new Vector3(180,0,0));Call(item,"Awake");Bind(f,0,item);
            int on=0,off=0;item.OnTurnedOn.AddListener(()=>on++);item.OnTurnedOff.AddListener(()=>off++);
            Check(f.actions.TryExecute(input) && item.IsOn && light.enabled && on==1 && off==0,"Switch original toggle/events not invoked exactly once");
            Finish(f);Move(f,f.center);Check(f.actions.TryExecute(input) && !f.point.LastAccepted && on==1 && item.IsOn,"Original moving lock bypassed");
            Finish(f);Call(item,"OnDisable");Move(f,f.center);Check(f.actions.TryExecute(input) && !item.IsOn && !light.enabled && off==1 && on==1,"Second accepted press did not switch off once");
            Call(item,"OnDisable");Check(Quaternion.Angle(rocker.localRotation,Quaternion.identity)<.01f,"Switch final off rotation changed");
        }
        static void Scenario(bool universal,string scenario)
        {
            var f=Create(universal);var bell=MakeBell(f,f.center,scenario=="reach"?2.1f:.7f);Bind(f,0,bell);
            if(scenario=="no_target")Bind(f,0);
            if(scenario=="wall" || scenario=="trigger")
            {var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=f.camera.ScreenToWorldPoint(new Vector3(f.center.x,f.center.y,.35f));wall.transform.localScale=Vector3.one*.1f;wall.GetComponent<Collider>().isTrigger=scenario=="trigger";}
            if(scenario=="several" || scenario=="tie")
            {
                bell.transform.position=f.camera.ScreenToWorldPoint(new Vector3(f.center.x+20,f.center.y,.7f));bell.transform.localScale=Vector3.one*.008f;
                var other=MakeBell(f,f.center+Vector2.right*(scenario=="tie"?-20:40));other.transform.localScale=Vector3.one*.008f;Bind(f,60,bell,other);
                Check(f.actions.TryExecute() && f.point.LastTarget==bell && Get<bool>(bell,"swinging") && !Get<bool>(other,"swinging"),"Closest candidate/stable serialized tie-break failed");return;
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
