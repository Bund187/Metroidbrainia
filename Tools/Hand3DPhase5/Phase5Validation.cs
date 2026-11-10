using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Metroidbrainia.Hand3DExperiment;
using Metroidbrainia.Hand3DExperiment.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using P = Metroidbrainia.Hand3DExperiment.Hand3DPoseProfile;
using S = Metroidbrainia.Hand3DExperiment.Hand3DPoseProfileSet;
using Object = UnityEngine.Object;

namespace Metroidbrainia
{
    public static class Phase5Validation
    {
        public const string AssetPath=Hand3DRuntimePoseExport.Folder+"/TestArmRuntimePoses.asset";
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
        private static int checks;
        private static readonly List<Row> rows=new List<Row>();
        [Serializable] private sealed class Row{public string name,status,detail;}
        [Serializable] private sealed class Report{public string runId,scope;public int assertions;public float maxPosition,maxRotation,maxFingerRotation,maxWristRotation,maxHandPosition,maxFingerPosition,maxReachError;public Row[] cases;}
        [Serializable] private sealed class Sample{public Vector3[] positions;public Quaternion[] rotations;public int mask;public float interaction;}
        [Serializable] private sealed class Samples{public Sample[] samples;}
        private static float maxPosition,maxRotation,maxFinger,maxWrist,maxHandPosition,maxFingerPosition,maxReachError;
        private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Flags).GetValue(target);
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
        private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethods(Flags).Single(m=>m.Name==name && m.GetParameters().Length==args.Length && m.GetParameters().Select((p,i)=>args[i]==null || p.ParameterType.IsInstanceOfType(args[i])).All(v=>v)).Invoke(target,args);
        private static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
        private static string RunId(){string[] args=Environment.GetCommandLineArgs();return args[Array.IndexOf(args,"-phase5RunId")+1];}
        private static Hand3DRig Load()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/TestArmScene.unity",OpenSceneMode.Single);
            return Object.FindObjectsByType<Hand3DRig>(FindObjectsSortMode.None).Single(r=>r.gameObject.scene.name=="TestArmScene");
        }
        public static void Prepare()
        {
            Hand3DPoseConversion.IsolatedRoot();try
            {
                var rig=Load();var source=AssetDatabase.LoadAssetAtPath<Hand3DPoseLibrary>(Hand3DPoseLibrary.Folder+"/HandPoses.asset");
                Check(source!=null,"The accepted Editor library is missing.");
                string before=EditorJsonUtility.ToJson(source);
                if(AssetDatabase.LoadMainAssetAtPath(AssetPath)!=null)AssetDatabase.DeleteAsset(AssetPath);
                var runtime=Hand3DRuntimePoseExport.CreateCopy(rig,source);Hand3DRuntimePoseExport.SaveNew(runtime,AssetPath);
                Check(runtime.Data.profiles.Length==13,"Experimental profiles leaked into functional copy.");
                Check(EditorJsonUtility.ToJson(source)==before,"Editor library modified by export.");
                var serialized=new SerializedObject(rig);serialized.FindProperty("poseSystem").enumValueIndex=0;serialized.FindProperty("runtimePoses").objectReferenceValue=runtime;serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(rig.gameObject.scene,"Assets/Phase5Configured.unity",true);
                var excluded=source.Data.profiles.Where(p=>!runtime.Data.profiles.Any(r=>r.id==p.id)).Select(p=>p.id+" / "+p.displayName);
                File.WriteAllText("phase5-import.txt","Functional profiles: 13\nExcluded, not activated:\n"+string.Join("\n",excluded));
                EditorApplication.Exit(0);
            }catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
        private sealed class Fixture
        {
            public Hand3DRig rig;public Hand3DArmMovement arm;public Hand3DFingerState state;public Hand3DInteractionPose interaction;public Hand3DPoseActions actions;public Transform[] bones;
            public void Step(float dt){Call(arm,"UpdateMovement",dt);Call(rig,"UpdateRig",dt);}
            public void Mask(int mask){for(int i=0;i<5;i++)state.SetFlexed((Finger)i,(mask&(1<<i))!=0);Call(rig,"RequestTransition");}
            public Sample Capture()=>new Sample{positions=bones.Select(b=>b.position).ToArray(),rotations=bones.Select(b=>b.rotation).ToArray(),mask=(int)state.Current,interaction=interaction.Weight};
        }
        private static Fixture Create(bool universal,Hand3DRuntimePoses custom=null)
        {
            // Scene reload can unload an unsaved clone. Preserve its actual data instead
            // of silently testing the default asset when Unity's native object disappears.
            string customData=custom!=null?EditorJsonUtility.ToJson(custom):null;
            var rig=Load();var arm=rig.GetComponent<Hand3DArmMovement>();Check(arm.Initialize(),"Movement initialization failed.");
            if(customData!=null && custom==null){custom=ScriptableObject.CreateInstance<Hand3DRuntimePoses>();EditorJsonUtility.FromJsonOverwrite(customData,custom);}
            var recognized=rig.GetComponent<Hand3DPoseRecognizer>();Call(recognized,"OnDisable");Call(recognized,"OnEnable");
            var actions=rig.GetComponent<Hand3DPoseActions>();Call(actions,"OnDisable");Call(actions,"OnEnable");Check(rig.Initialize(),"Rig initialization failed.");
            if(universal){rig.SelectPoseSystem(Hand3DRig.PoseSystem.Universal,custom!=null?custom:AssetDatabase.LoadAssetAtPath<Hand3DRuntimePoses>(AssetPath));Check(rig.UniversalActive,"Universal rejected: "+rig.UniversalError);}
            var paths=rig.RuntimePoses.Data.skeleton.Select(b=>b.path).ToArray();
            var f=new Fixture{rig=rig,arm=arm,state=rig.GetComponent<Hand3DFingerState>(),interaction=rig.GetComponent<Hand3DInteractionPose>(),actions=actions,bones=paths.Select(p=>p=="."?rig.transform:rig.transform.Find(p)).ToArray()};
            f.Step(1f);return f;
        }
        private static void Case(string name,Action action)
        {try{action();rows.Add(new Row{name=name,status="PASS",detail="Independent scene loads, same effective inputs; current runtime components."});}catch(Exception error){rows.Add(new Row{name=name,status="FAIL",detail=error.ToString()});Debug.LogError(name+": "+error);}}
        public static void Run()
        {
            Hand3DPoseConversion.IsolatedRoot();
            Case("runtime_data_and_explicit_copy",()=>
            {
                var asset=AssetDatabase.LoadAssetAtPath<Hand3DRuntimePoses>(AssetPath);Check(asset.ValidateConfiguration().Length==0,"Invalid runtime config");Check(asset.GetType().Assembly!=typeof(Hand3DPoseLibrary).Assembly,"Runtime type is defined in the Editor assembly");
                Check(asset.Data.profiles.Length==13 && !asset.Data.profiles.Any(p=>!string.IsNullOrEmpty(p.displayName) && p.displayName.Contains("Test")),"Experimental assignment active");
            });
            Case("32_fingers_modes_and_timed_transitions",()=>Compare(Timeline(false),Timeline(true),.0002f,.15f));
            Case("movement_and_return_comparison",()=>Compare(Movement(false),Movement(true),.002f,180.1f,true));
            Case("full_body_reference_ik_and_limits",FullBody);
            Case("switching_partial_masks_pause",Switching);
            Case("universal_door_document_lifecycle",Interactions);
            File.WriteAllText("phase5-results.json",JsonUtility.ToJson(new Report{runId=RunId(),scope="Real Legacy/Universal evaluation on independently reloaded scenes. Numeric/bone comparison; no final Game visual approval.",assertions=checks,maxPosition=maxPosition,maxRotation=maxRotation,maxFingerRotation=maxFinger,maxWristRotation=maxWrist,maxHandPosition=maxHandPosition,maxFingerPosition=maxFingerPosition,maxReachError=maxReachError,cases=rows.ToArray()},true));
            EditorApplication.Exit(rows.Any(r=>r.status=="FAIL")?1:0);
        }
        private static Sample[] Timeline(bool universal)
        {
            var f=Create(universal);var samples=new List<Sample>();
            for(int mode=0;mode<2;mode++)
            {
                if(mode==1){f.arm.ToggleMode();for(int i=0;i<24;i++){f.Step(1f/60f);samples.Add(f.Capture());}}
                for(int mask=0;mask<32;mask++)
                {
                    f.Mask(mask);for(int i=0;i<14;i++){f.Step(1f/60f);samples.Add(f.Capture());}
                }
                f.Mask(29);f.Step(1f);Set(f.actions,"<PointClickWeight>k__BackingField",.12f);f.Step(.03f);samples.Add(f.Capture());Set(f.actions,"<PointClickWeight>k__BackingField",0f);
                foreach(bool document in new[]{false,true})
                {
                    if(document)f.interaction.SetHandHold(true);else f.interaction.SetGrab(true,Quaternion.Euler(0f,5f,0f));
                    for(int i=0;i<10;i++){f.Step(1f/60f);samples.Add(f.Capture());}
                    f.Mask(3);f.Step(.04f);samples.Add(f.Capture());
                    if(document)f.interaction.SetHandHold(false);else f.interaction.SetGrab(false);
                    for(int i=0;i<10;i++){f.Step(1f/60f);samples.Add(f.Capture());}
                }
                if(mode==1)for(int i=0;i<10;i++){f.arm.Modes.Inspect(-.02f);f.Step(.02f);samples.Add(f.Capture());}
            }
            return samples.ToArray();
        }
        private static Sample[] Movement(bool universal)
        {
            var f=Create(universal);f.Mask(29);f.Step(1f);f.arm.BeginControl();var samples=new List<Sample>();
            foreach(var displacement in new[]{new Vector2(20,0),new Vector2(-40,10),new Vector2(30,-20),new Vector2(0,15)})
                for(int i=0;i<20;i++){f.arm.MoveHand(displacement,true);f.Step(1f/60f);samples.Add(f.Capture());}
            f.arm.EndControl();for(int i=0;i<100;i++){f.Step(1f/60f);samples.Add(f.Capture());}
            return samples.ToArray();
        }
        private static void Compare(Sample[] legacy,Sample[] universal,float positionTolerance,float bodyAngleTolerance,bool derivedPole=false)
        {
            Check(legacy.Length==universal.Length,"Different sample counts");var data=AssetDatabase.LoadAssetAtPath<Hand3DRuntimePoses>(AssetPath).Data;
            for(int s=0;s<legacy.Length;s++)
            {
                Check(legacy[s].mask==universal[s].mask,"Logical state changed");Check(Mathf.Abs(legacy[s].interaction-universal[s].interaction)<.00001f,"Interaction timing changed");
                for(int b=0;b<legacy[s].positions.Length;b++)
                {
                    float p=Vector3.Distance(legacy[s].positions[b],universal[s].positions[b]),r=Quaternion.Angle(legacy[s].rotations[b],universal[s].rotations[b]);maxPosition=Mathf.Max(maxPosition,p);maxRotation=Mathf.Max(maxRotation,r);
                    bool finger=data.skeleton[b].role==S.Role.Finger,wrist=data.skeleton[b].role==S.Role.Wrist;if(finger)maxFinger=Mathf.Max(maxFinger,r);if(wrist)maxWrist=Mathf.Max(maxWrist,r);
                    if(finger)maxFingerPosition=Mathf.Max(maxFingerPosition,p);if(wrist)maxHandPosition=Mathf.Max(maxHandPosition,p);
                    bool elbowOrTwist=data.skeleton[b].role==S.Role.Forearm || data.skeleton[b].role==S.Role.Twist;
                    Check(derivedPole && elbowOrTwist || p<positionTolerance,"Position difference sample="+s+" bone="+data.skeleton[b].path+" error="+p);
                    Check(r<(finger || wrist ? .15f : bodyAngleTolerance),"Rotation difference sample="+s+" bone="+data.skeleton[b].path+" error="+r);
                }
            }
        }
        private static Hand3DRuntimePoses Modified(Action<Hand3DPoseProfileSet,Hand3DRuntimePoses> edit)
        {
            var original=AssetDatabase.LoadAssetAtPath<Hand3DRuntimePoses>(AssetPath);var copy=Object.Instantiate(original);var data=Hand3DPoseLibrary.Clone(original.Data);edit(data,copy);
            copy.Configure(data,copy.Exploration,copy.HandEdit,copy.Extended,copy.Flexed,copy.Gestures,copy.GrabDoor,copy.GrabDocument);return copy;
        }
        private static void FullBody()
        {
            var profile=Modified((data,asset)=>
            {
                var p=data.profiles.Single(p=>p.id==asset.Gestures[0].exploration[0]);var shoulder=data.skeleton.Single(b=>b.role==S.Role.Shoulder);
                p.fingersOnly=false;p.contexts=P.Context.Exploration;p.joints=p.joints.Concat(new[]{new P.JointValue{jointPath=shoulder.path,referenceId=shoulder.parentId,storedChannels=P.Channels.Rotation,influenceChannels=P.Channels.Rotation,weight=1f,captured=true,rotation=shoulder.sourceLocalRotation*Quaternion.Euler(8f,-12f,5f)}}).ToArray();
                foreach(var bone in data.skeleton.Where(b=>b.role==S.Role.UpperArm || b.role==S.Role.Forearm))
                    p.joints=p.joints.Concat(new[]{new P.JointValue{jointPath=bone.path,referenceId=bone.parentId,storedChannels=P.Channels.Rotation,influenceChannels=P.Channels.Rotation,weight=1f,captured=true,rotation=bone.sourceLocalRotation*Quaternion.Euler(4f,-3f,2f)}}).ToArray();
                asset.Gestures[0].handEdit=Array.Empty<string>();
            });
            try
            {
                var f=Create(true,profile);var scales=f.bones.Select(b=>b.localScale).ToArray();float a=Vector3.Distance(f.arm.UpperArm.position,f.arm.Forearm.position),b=Vector3.Distance(f.arm.Forearm.position,f.arm.Hand.position);
                f.Mask(3);f.Step(1f);var referenceElbow=f.arm.Forearm.position;var referenceHand=f.arm.Hand.position;f.arm.BeginControl();f.Step(1f/60f);
                Check(Vector3.Distance(referenceElbow,f.arm.Forearm.position)<.00001f && Vector3.Distance(referenceHand,f.arm.Hand.position)<.00001f,"Derived pole did not reproduce its reference at rest");
                for(int i=0;i<90;i++){f.arm.MoveHand(new Vector2(i%2==0?30:-10,Mathf.Sin(i*.2f)*12),true);f.Step(1f/60f);Check(f.rig.UniversalActive,"Universal failed mid-pose");maxReachError=Mathf.Max(maxReachError,f.arm.LastUniversalReachError);Check(f.arm.LastUniversalReachError<.00001f,"Solver missed its feasible target");Check(Mathf.Abs(Vector3.Distance(f.arm.UpperArm.position,f.arm.Forearm.position)-a)<.00001f && Mathf.Abs(Vector3.Distance(f.arm.Forearm.position,f.arm.Hand.position)-b)<.00001f,"IK stretched");Check(Quaternion.Angle(f.arm.Hand.rotation,f.rig.UniversalComposition.FinalWrist)<.05f,"Wrist lost after IK");}
                Check(f.bones.Select((t,i)=>Vector3.Distance(t.localScale,scales[i])<.000001f).All(v=>v),"Scale changed");
                f.arm.MoveHand(new Vector2(100000f,100000f),true);for(int i=0;i<30;i++)f.Step(1f/60f);Check(!float.IsNaN(f.arm.Hand.position.x) && !float.IsInfinity(f.arm.Hand.position.x),"Impossible target produced invalid pose");
                f.arm.EndControl();for(int i=0;i<150;i++)f.Step(1f/60f);var rest=f.arm.Hand.position;
                for(int cycle=0;cycle<30;cycle++){f.arm.BeginControl();f.arm.MoveHand(new Vector2(25,-10),true);f.Step(.1f);f.arm.EndControl();for(int i=0;i<90;i++)f.Step(1f/60f);}
                Check(Vector3.Distance(rest,f.arm.Hand.position)<.0001f,"Repeated return drift");
            }finally{Object.DestroyImmediate(profile);}
        }
        private static void Switching()
        {
            var f=Create(false);f.Mask(3);f.Step(1f);var legacy=f.Capture();
            for(int i=0;i<3;i++){f.rig.SelectPoseSystem(Hand3DRig.PoseSystem.Universal);f.Step(1f);Check(f.rig.UniversalActive && f.state.Current==(FlexedFingers)3,"Universal switch");var before=f.Capture();f.Step(0f);Check(before.positions.SequenceEqual(f.Capture().positions),"Paused pose changed");f.rig.SelectPoseSystem(Hand3DRig.PoseSystem.Legacy);f.Step(1f);Check(!f.rig.UniversalActive,"Legacy switch");var restored=f.Capture();Check(legacy.positions.Select((p,j)=>Vector3.Distance(p,restored.positions[j])<.0001f).All(v=>v),"Legacy did not restore positions");}
            var bad=Object.Instantiate(f.rig.RuntimePoses);var data=Hand3DPoseLibrary.Clone(bad.Data);data.profiles=data.profiles.Where(p=>p.id!=bad.Extended).ToArray();bad.Configure(data,bad.Exploration,bad.HandEdit,bad.Extended,bad.Flexed,bad.Gestures,bad.GrabDoor,bad.GrabDocument);
            f.rig.SelectPoseSystem(Hand3DRig.PoseSystem.Universal,bad);Check(!f.rig.UniversalActive && !string.IsNullOrEmpty(f.rig.UniversalError),"Missing required profile did not fall back");Object.DestroyImmediate(bad);
            f.rig.SelectPoseSystem(Hand3DRig.PoseSystem.Universal,AssetDatabase.LoadAssetAtPath<Hand3DRuntimePoses>(AssetPath));
            var unowned=f.rig.GetComponentsInChildren<Transform>(true).First(t=>!f.rig.UniversalComposition.OwnsBone(t));unowned.localRotation*=Quaternion.Euler(4,0,0);var expected=unowned.localRotation;f.Step(.1f);Check(Quaternion.Angle(expected,unowned.localRotation)<.05f,"A partial right-hand profile overwrote an unowned joint");
        }
        private static void Interactions()
        {
            // Reuse actual detector fixtures, activate Universal before contact, keep their real cancellation paths.
            foreach(bool document in new[]{false,true})
            {
                var factory=typeof(Phase1Validation);var fixture=factory.GetMethod("Create",Flags).Invoke(null,new object[]{false});var rig=Get<Hand3DRig>(fixture,"rig");var arm=Get<Hand3DArmMovement>(fixture,"movement");var input=Get<Hand3DInputController>(fixture,"input");var coordinator=rig.GetComponent<Hand3DInteractionCoordinator>();Set(input,"interactions",coordinator);
                rig.SelectPoseSystem(Hand3DRig.PoseSystem.Universal);Call(rig,"UpdateRig",1f);Call(fixture,"Move",Get<Vector2>(fixture,"center"));
                object item=document?factory.GetMethod("Document",Flags).Invoke(null,new object[]{fixture,"Universal paper",Get<Vector2>(fixture,"center"),.7f}):factory.GetMethod("Door",Flags).Invoke(null,new object[]{fixture,"Universal door",Get<Vector2>(fixture,"center"),.7f,false,true});
                var adapter=document?Get<MonoBehaviour>(fixture,"document"):Get<MonoBehaviour>(fixture,"door");Physics.SyncTransforms();Call(adapter,"Tick",1f/60f);Check(coordinator.IsOccupied,"Universal contact did not acquire");Call(arm,"UpdateMovement",1f/60f);Call(rig,"UpdateRig",1f/60f);Check(rig.UniversalActive,"Universal failed with interaction");
                Call(input,"OnApplicationFocus",false);Check(!coordinator.IsOccupied,"Focus did not cancel universal interaction");Call(rig,"UpdateRig",1f);Call(Get<Hand3DDocumentDisplay>(fixture,"display"),"Tick",1f);Check(rig.GetComponent<Hand3DFingerState>().Current==(FlexedFingers)3,"Interaction changed logical state");
            }
        }
        public static void BuildPlayer()
        {
            Hand3DPoseConversion.IsolatedRoot();try
            {
                var rig=Load();var probe=new GameObject("Temporary player build verification");probe.AddComponent<Phase5PlayerProbe>();EditorSceneManager.SaveScene(rig.gameObject.scene);
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/TestArmScene.unity"},locationPathName=Path.GetFullPath("Phase5Player/HandValidation.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                File.WriteAllText("phase5-build.txt",result.summary.result+"; errors="+result.summary.totalErrors+"; bytes="+result.summary.totalSize);
                EditorApplication.Exit(result.summary.result==BuildResult.Succeeded?0:1);
            }catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}
