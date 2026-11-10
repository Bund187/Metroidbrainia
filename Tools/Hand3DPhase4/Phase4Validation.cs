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
using P = Metroidbrainia.Hand3DExperiment.Hand3DPoseProfile;
using S = Metroidbrainia.Hand3DExperiment.Hand3DPoseProfileSet;
using L = Metroidbrainia.Hand3DExperiment.Editor.Hand3DPoseLibrary;
using V = Metroidbrainia.Hand3DExperiment.Editor.Hand3DPosePreview;

namespace Metroidbrainia
{
    public static class Phase4Validation
    {
        [Serializable] private sealed class Row { public string name,status,detail; }
        [Serializable] private sealed class Report { public string runId,scope; public int assertions; public Row[] cases; }
        private static int checks;
        private static readonly List<Row> rows=new List<Row>();
        private static Hand3DRig rig;
        private static L library;
        private static V preview;
        private static Hand3DPoseEditor window;
        private static string original;
        private static string[] sources;
        private static bool dirty;
        private static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
        private static void Q(Quaternion a,Quaternion b,string message)=>Check(Quaternion.Angle(a,b)<.05f,message);
        private static void Fail(Action action,string message){bool rejected=false;try{action();}catch(Exception){rejected=true;}Check(rejected,message);}
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        private static P Profile(string id)=>library.Data.profiles.Single(p=>p.id==id);
        private static void Setup()
        {
            StageUtility.GoToMainStage();var scene=EditorSceneManager.OpenScene("Assets/Scenes/TestArmScene.unity",OpenSceneMode.Single);
            rig=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Hand3DRig>(true)).Single();
            library=L.Import(rig,File.ReadAllText("phase3-profile-export.json"));
            original=EditorJsonUtility.ToJson(rig);sources=rig.GetComponents<MonoBehaviour>().Select(EditorJsonUtility.ToJson).ToArray();dirty=scene.isDirty;
        }
        private static void Unchanged()
        {
            Check(EditorJsonUtility.ToJson(rig)==original,"Legacy rig changed");
            Check(rig.GetComponents<MonoBehaviour>().Select(EditorJsonUtility.ToJson).SequenceEqual(sources),"Legacy component serialization changed");
            Check(rig.gameObject.scene.isDirty==dirty,"Source scene dirty state changed");
            foreach(var joint in library.Data.skeleton)
            {
                var bone=joint.path=="."?rig.transform:rig.transform.Find(joint.path);
                Check(Vector3.Distance(bone.localPosition,joint.sourceLocalPosition)<.000001f,"Source position changed");Q(bone.localRotation,joint.sourceLocalRotation,"Source rotation changed");Check(bone.localScale==joint.sourceLocalScale,"Source scale changed");
            }
        }
        private static void Case(string name,Action test)
        {
            try{Setup();test();if(preview!=null)preview.Restore();preview=null;if(window!=null){window.DiscardChanges();window.Close();window=null;}Unchanged();rows.Add(new Row{name=name,status="PASS",detail="Real Editor window/stage, isolated project; no final visual judgement."});}
            catch(Exception error){rows.Add(new Row{name=name,status="FAIL",detail=error.ToString()});Debug.LogError(name+": "+error);}
            finally{if(window!=null){window.DiscardChanges();window.Close();window=null;}if(preview!=null){preview.Restore();preview=null;}StageUtility.GoToMainStage();if(library!=null && !EditorUtility.IsPersistent(library))UnityEngine.Object.DestroyImmediate(library);Undo.ClearAll();}
        }
        public static void Run()
        {
            Hand3DPoseConversion.IsolatedRoot();
            Case("open_close_without_edits",()=>
            {
                var paths=AssetDatabase.GetAllAssetPaths();window=EditorWindow.GetWindow<Hand3DPoseEditor>();window.Show();window.Close();window=null;
                Check(paths.SequenceEqual(AssetDatabase.GetAllAssetPaths()),"Opening a window created assets");
            });
            Case("masks_and_generic_profiles",()=>
            {
                Check(V.SelectPaths(library.Data,V.SelectionMask.FullBody,Array.Empty<string>()).Length==22,"Full mask");
                Check(V.SelectPaths(library.Data,V.SelectionMask.ArmOnly,Array.Empty<string>()).Length==6,"Arm mask");
                Check(V.SelectPaths(library.Data,V.SelectionMask.WristOnly,Array.Empty<string>()).Length==1,"Wrist mask");
                Check(V.SelectPaths(library.Data,V.SelectionMask.FingersOnly,Array.Empty<string>()).Length==15,"Finger mask");
                library.AddProfile("new.bell","Bell",P.Context.Action,false);library.AddProfile("new.gesture","New gesture",P.Context.HandEdit,true);
                Check(library.Data.profiles.Length==15 && Profile("new.bell").joints.Length==0,"Generic creation failed");
                library.DuplicateProfile("Gesture.0.OK.Wrist","new.offset","New offset");Check(Profile("new.offset").joints[0].space==P.Space.ExplorationWristOffset,"Duplicate lost special operation");
                Fail(()=>library.AddProfile("new.bell","Duplicate",P.Context.Action,false),"Duplicate IDs accepted");
            });
            Case("local_preview_and_weights",()=>
            {
                preview=V.Open(rig,library,"Fingers.flexed");foreach(var value in Profile("Fingers.flexed").joints)Q(preview.Joint(value.jointPath).localRotation,value.rotation,"Finger target incorrect");
                Check(preview.PreviewRoot.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"Preview has runtime scripts");
                preview.Restore();preview=null;var data=L.Clone(library.Data);data.profiles.Single(p=>p.id=="Fingers.flexed").joints[0].weight=.5f;library.Replace(data,"Weight test");
                var first=Profile("Fingers.flexed").joints[0];var baseline=library.Data.skeleton.Single(b=>b.path==first.jointPath).sourceLocalRotation;
                preview=V.Open(rig,library,"Fingers.flexed");Q(preview.Joint(first.jointPath).localRotation,Quaternion.Slerp(baseline,first.rotation,.5f),"Weight ignored");
            });
            Case("camera_and_special_wrist_preview",()=>
            {
                preview=V.Open(rig,library,"Mode.handEdit");var camera=rig.GetComponent<Hand3DArmMovement>().ArmCamera.transform;
                foreach(var value in Profile("Mode.handEdit").joints)if(value.space==P.Space.CameraRelative){Q(preview.Joint(value.jointPath).rotation,camera.rotation*value.rotation,"Camera rotation incorrect");if((value.influenceChannels&P.Channels.Position)!=0)Check(Vector3.Distance(preview.Joint(value.jointPath).position,camera.TransformPoint(value.position))<.00001f,"Camera position incorrect");}
                preview.Restore();preview=null;
                Fail(()=>V.Open(rig,library,"Gesture.0.OK.Wrist"),"Missing reference produced misleading preview");
                Fail(()=>V.Open(rig,library,"Mode.exploration","Mode.exploration"),"Self-reference accepted");
                Fail(()=>V.Open(rig,library,"Gesture.0.OK.Overrides","Mode.handEdit",new[]{"Gesture.0.OK.Wrist"},context:P.Context.HandEdit),"Exploration wrist layer leaked into HandEdit");
                preview=V.Open(rig,library,"Gesture.0.OK.Wrist","Mode.exploration",new[]{"Gesture.0.OK.Overrides"});
                var basis=camera.rotation*Profile("Mode.exploration").joints.Single(j=>j.jointPath==library.Data.wristPath).rotation;
                Q(preview.Joint(library.Data.wristPath).rotation,basis*Profile("Gesture.0.OK.Wrist").joints[0].rotation,"Exploration offset incorrect");
                preview.Restore();preview=null;preview=V.Open(rig,library,"GrabDoor.Additional","Mode.exploration");var wrist=Profile("GrabDoor.Additional").joints.Single(j=>j.jointPath==library.Data.wristPath);
                Q(preview.Joint(wrist.jointPath).rotation,basis*Quaternion.Inverse(wrist.neutralRotation)*wrist.rotation,"Neutral wrist delta incorrect");
            });
            Case("full_capture_only_selected_profile",()=>
            {
                string before=JsonUtility.ToJson(library.Data);library.AddProfile("capture.full","Complete",P.Context.Exploration,false);
                var originals=library.Data.profiles.Where(p=>p.id!="capture.full").Select(JsonUtility.ToJson).ToArray();preview=V.Open(rig,library,"capture.full",apply:false);
                var shoulder=library.Data.skeleton.Single(b=>b.role==S.Role.Shoulder);preview.Joint(shoulder.path).localRotation*=Quaternion.Euler(2f,0f,0f);
                preview.Capture(V.SelectPaths(library.Data,V.SelectionMask.FullBody,Array.Empty<string>()),P.Channels.Rotation,false);
                Check(Profile("capture.full").joints.Length==22,"Full capture incomplete");Check(originals.SequenceEqual(library.Data.profiles.Where(p=>p.id!="capture.full").Select(JsonUtility.ToJson)),"Other profiles overwritten");
                foreach(var entry in Profile("capture.full").joints)
                {Check(entry.captured && entry.influenceChannels==P.Channels.Rotation,"Capture mask/flag");if(entry.space==P.Space.ParentLocal)Q(entry.rotation,preview.Joint(entry.jointPath).localRotation,"Local capture");else Q(entry.rotation,Quaternion.Inverse(rig.GetComponent<Hand3DArmMovement>().ArmCamera.transform.rotation)*preview.Joint(entry.jointPath).rotation,"Camera capture");}
            });
            Case("partial_capture_repeat_and_channels",()=>
            {
                library.AddProfile("capture.partial","Partial",P.Context.HandEdit,true);preview=V.Open(rig,library,"capture.partial",apply:false);
                string[] fingers=V.SelectPaths(library.Data,V.SelectionMask.FingersOnly,Array.Empty<string>());
                preview.Capture(fingers,P.Channels.Rotation,false);Check(Profile("capture.partial").joints.Length==15,"Partial finger capture");
                string retained=JsonUtility.ToJson(Profile("capture.partial").joints[1]);preview.Joint(fingers[0]).localRotation*=Quaternion.Euler(3f,4f,5f);preview.Joint(fingers[0]).localPosition+=Vector3.right*.00001f;
                preview.Capture(new[]{fingers[0]},P.Channels.Position|P.Channels.Rotation,false);Check(Profile("capture.partial").joints.Length==16,"Disjoint channels duplicated");Check(JsonUtility.ToJson(Profile("capture.partial").joints[1])==retained,"Nonselected contribution changed");
                preview.Capture(new[]{fingers[0]},P.Channels.Position|P.Channels.Rotation,false);Check(Profile("capture.partial").joints.Length==16,"Repeated capture accumulated values");
                Fail(()=>preview.Capture(new[]{library.Data.wristPath},P.Channels.Rotation,false),"HandEdit finger mask changed wrist");
            });
            Case("geometry_permission_and_rejection",()=>
            {
                library.AddProfile("geometry","Geometry",P.Context.Exploration,false);preview=V.Open(rig,library,"geometry",apply:false);
                var forearm=library.Data.skeleton.Single(b=>b.role==S.Role.Forearm);string[] all=V.SelectPaths(library.Data,V.SelectionMask.FullBody,Array.Empty<string>());
                preview.Capture(all,P.Channels.Position|P.Channels.Rotation,false);
                Check(Profile("geometry").joints.Where(j=>library.Data.skeleton.Single(b=>b.path==j.jointPath).role==S.Role.Forearm).All(j=>(j.influenceChannels&P.Channels.Position)==0),"Default changed IK positions");
                preview.Capture(new[]{forearm.path},P.Channels.Position,true);Check(Profile("geometry").joints.Any(j=>j.jointPath==forearm.path && j.allowSegmentPosition),"Explicit valid position not accepted");
                string before=JsonUtility.ToJson(library.Data);preview.Joint(forearm.path).localPosition*=1.2f;Fail(()=>preview.Capture(new[]{forearm.path},P.Channels.Position,true),"Stretch captured");Check(before==JsonUtility.ToJson(library.Data),"Failed capture wrote data");
            });
            Case("relative_capture_roundtrip",()=>
            {
                var value=Profile("Gesture.0.OK.Wrist").joints[0];var expected=value.rotation;preview=V.Open(rig,library,"Gesture.0.OK.Wrist","Mode.exploration");preview.Capture(new[]{value.jointPath},P.Channels.Rotation,false);Q(Profile("Gesture.0.OK.Wrist").joints[0].rotation,expected,"Offset capture changed operation");preview.Restore();preview=null;
                expected=Profile("GrabDoor.Additional").joints.Single(j=>j.jointPath==library.Data.wristPath).rotation;preview=V.Open(rig,library,"GrabDoor.Additional","Mode.exploration");preview.Capture(new[]{library.Data.wristPath},P.Channels.Rotation,false);Q(Profile("GrabDoor.Additional").joints.Single(j=>j.jointPath==library.Data.wristPath).rotation,expected,"Neutral delta capture changed operation");
            });
            Case("undo_redo_data_and_preview_transforms",()=>
            {
                string before=JsonUtility.ToJson(library.Data);library.AddProfile("undo","Undo",P.Context.Action,false);Undo.FlushUndoRecordObjects();Undo.PerformUndo();Check(before==JsonUtility.ToJson(library.Data),"Data undo failed");Undo.PerformRedo();Check(library.Data.profiles.Any(p=>p.id=="undo"),"Data redo failed");
                preview=V.Open(rig,library,"undo",apply:false);var bone=preview.Joint(library.Data.wristPath);var old=bone.localRotation;Undo.IncrementCurrentGroup();Undo.RecordObject(bone,"Pose joint edit");bone.localRotation*=Quaternion.Euler(10f,0,0);var edited=bone.localRotation;Undo.FlushUndoRecordObjects();Undo.PerformUndo();Q(bone.localRotation,old,"Preview undo failed");Undo.PerformRedo();Q(bone.localRotation,edited,"Preview redo failed");
            });
            Case("save_reload_and_storage_protection",()=>
            {
                library.AddProfile("persist.capture","Saved finger",P.Context.Action,false);preview=V.Open(rig,library,"persist.capture",apply:false);var pathOfJoint=library.Data.skeleton.First(b=>b.role==S.Role.Finger).path;preview.Joint(pathOfJoint).localRotation*=Quaternion.Euler(7f,4f,0f);preview.Capture(new[]{pathOfJoint},P.Channels.Rotation,false);preview.Restore();preview=null;
                string path=L.Folder+"/Test-"+Guid.NewGuid().ToString("N")+".asset";library.Save(path);string saved=JsonUtility.ToJson(library.Data);Undo.ClearUndo(library);Resources.UnloadAsset(library);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);var loaded=AssetDatabase.LoadAssetAtPath<L>(path);Check(loaded!=null && JsonUtility.ToJson(loaded.Data)==saved,"Save/reload failed");library=loaded;Check(Profile("persist.capture").displayName=="Saved finger" && Profile("persist.capture").joints.Length==1,"Captured pose/name did not persist");
                var other=L.Import(rig,File.ReadAllText("phase3-profile-export.json"));try{Fail(()=>other.Save(path),"Existing library overwritten");Fail(()=>other.Save("Assets/Scenes/Prototype.unity"),"Protected destination accepted");}finally{UnityEngine.Object.DestroyImmediate(other);}
            });
            Case("window_switch_cancel_close_and_play_exit",()=>
            {
                window=EditorWindow.GetWindow<Hand3DPoseEditor>();window.Bind(rig,library);window.SelectProfile("Fingers.flexed");window.BeginPreview();Check(window.Preview!=null,"Window preview missing");window.SelectProfile("Fingers.extended");Check(window.Preview==null && !(StageUtility.GetCurrentStage() is V),"Profile switch leaked preview");
                window.BeginPreview();window.Restore();Check(!(StageUtility.GetCurrentStage() is V),"Cancel leaked stage");window.BeginPreview();typeof(Hand3DPoseEditor).GetMethod("OnPlayMode",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,new object[]{PlayModeStateChange.ExitingEditMode});Check(window.Preview==null,"Entering Play kept preview");
                window.BeginPreview();window.DiscardChanges();window.Close();window=null;Check(!(StageUtility.GetCurrentStage() is V),"Close leaked preview stage");
                // The window owns and destroys its transient library; keep a fresh read-only copy for source comparisons.
                library=L.Import(rig,File.ReadAllText("phase3-profile-export.json"));
            });
            Case("invalid_references_and_catalog_extension",()=>
            {
                var data=L.Clone(library.Data);data.skeleton[1].path="not-present";Fail(()=>L.ValidateBinding(rig,data),"Missing joint accepted");
                data=L.Clone(library.Data);data.profiles[0].contexts=0;Fail(()=>library.Replace(data,"Invalid context"),"Unselectable context persisted");
                data=L.Clone(library.Data);data.skeleton=data.skeleton.Concat(new[]{data.skeleton[1]}).ToArray();Fail(()=>L.ValidateBinding(rig,data),"Duplicate reference accepted");
                var extra=rig.GetComponentsInChildren<Transform>(true).First(t=>t!=rig.transform && !library.Data.skeleton.Any(b=>b.path==AnimationUtility.CalculateTransformPath(t,rig.transform)));
                library.AddJoint(rig,extra,S.Role.Twist,-1);Check(library.Data.skeleton.Length==23,"Existing joint could not be registered");Fail(()=>library.AddJoint(rig,extra,S.Role.Twist,-1),"Duplicate catalog joint accepted");
                var foreign=new GameObject("Foreign rig").AddComponent<Hand3DRig>();try{Fail(()=>L.Guard(foreign),"Unconfigured foreign rig accepted");}finally{UnityEngine.Object.DestroyImmediate(foreign.gameObject);}
            });
            string[] args=Environment.GetCommandLineArgs();string id=args[Array.IndexOf(args,"-phase4RunId")+1];File.WriteAllText("phase4-results.json",JsonUtility.ToJson(new Report{runId=id,scope="Real Unity Editor stage/window and asset APIs in the isolated project. Programmatic UI lifecycle; no manual Scene View visual validation.",assertions=checks,cases=rows.ToArray()},true));EditorApplication.Exit(rows.Any(r=>r.status=="FAIL")?1:0);
        }
    }
}
