using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Metroidbrainia.Hand3DExperiment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace Metroidbrainia
{
    public static class Phase2Validation
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        private static int checks;
        [Serializable] private sealed class Row { public string name, status, detail; }
        [Serializable] private sealed class Report { public string runId, scope; public int assertions; public Row[] cases; }
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(Flags)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length && m.GetParameters().Select((p,i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(v=>v)).Invoke(target,args);
        private static object FixtureCall(string name, params object[] args) => typeof(Phase1Validation).GetMethod(name, Flags).Invoke(null,args);
        private static void Check(bool value, string message) { if(!value) throw new Exception(message); checks++; }
        private const float Step = 1f/60f;

        public static void Run()
        {
            if(!File.Exists("phase0-isolated-copy.marker")) throw new Exception("Isolated project required.");
            var rows = new List<Row>();
            foreach(bool document in new[]{false,true})
            foreach(string test in new[]{"release", "repeat", "focus", "application_pause", "time_pause", "mode", "r2", "object_disable", "object_destroy", "adapter_disable", "adapter_destroy", "input_disable", "coordinator_disable", "movement_disable", "pose_disable", "rig_disable", "action", "gesture_change", "reentrant_cancel"})
            {
                string name = (document ? "document_" : "door_")+test;
                try { Scenario(document,test); rows.Add(new Row{name=name,status="PASS",detail="Isolated Editor fixture; real session/adapters, synthetic effective input and contact."}); }
                catch(Exception error) { rows.Add(new Row{name=name,status="FAIL",detail=error.ToString()}); Debug.LogError(name+": "+error); }
            }
            string[] args=Environment.GetCommandLineArgs(); string id=args[Array.IndexOf(args,"-phase2RunId")+1];
            File.WriteAllText("phase2-results.json", JsonUtility.ToJson(new Report{runId=id,scope="Session lifecycle Editor stepping. No physical device or final Game visual validation.",assertions=checks,cases=rows.ToArray()},true));
            EditorApplication.Exit(rows.Any(r=>r.status=="FAIL")?1:0);
        }

        private static void Scenario(bool document, string test)
        {
            var fixture=FixtureCall("Create",false);
            var door=Get<MonoBehaviour>(fixture,"door") as Hand3DDoorInteractor;
            var docs=Get<MonoBehaviour>(fixture,"document") as Hand3DDocumentInteractor;
            var rig=Get<Hand3DRig>(fixture,"rig"); var input=Get<Hand3DInputController>(fixture,"input");
            var movement=Get<Hand3DArmMovement>(fixture,"movement"); var display=Get<Hand3DDocumentDisplay>(fixture,"display");
            var coordinator=rig.GetComponent<Hand3DInteractionCoordinator>(); var pose=rig.GetComponent<Hand3DInteractionPose>();
            var actions=rig.GetComponent<Hand3DPoseActions>();
            Set(input,"interactions",coordinator); Call(actions,"OnDisable"); Call(actions,"OnEnable");
            MonoBehaviour adapter=document?(MonoBehaviour)docs:door;
            var center=Get<Vector2>(fixture,"center");
            Component item=document ? (Component)FixtureCall("Document",fixture,"Session paper",center,.7f)
                : (Component)FixtureCall("Door",fixture,"Session door",center,.7f,false,true);
            var state=rig.GetComponent<Hand3DFingerState>();
            if(test=="action")
            {
                for(int i=0;i<5;i++) state.SetFlexed((Finger)i,(29 & (1<<i))!=0);
                Call(rig,"UpdateRig",1f); Call(fixture,"Move",center);
                Check(actions.TryExecute(),"PointClick did not begin");
                Set(actions,"<PointClickWeight>k__BackingField",.15f);
                Check(Get<bool>(actions,"clicking"),"Click was not active");
            }
            if(test=="reentrant_cancel" && document)
            {
                var picked=new UnityEvent(); picked.AddListener(coordinator.CancelSession); Set(item,"onPickedUp",picked);
            }
            Physics.SyncTransforms(); Call(adapter,"Tick",Step);
            if(test=="reentrant_cancel" && document)
            {
                Check(!coordinator.IsOccupied && !docs.IsHolding && !pose.IsHandHolding,"Reentrant event left a lease");
                Call(display,"Tick",1f); Check(item.GetComponent<Renderer>().enabled,"Reentrant return failed"); return;
            }
            Check(coordinator.Owns(adapter) && coordinator.Owner==adapter && coordinator.IsOccupied,"Acquisition/owner mismatch");
            Check(coordinator.State==(document?Hand3DInteractionState.Document:Hand3DInteractionState.Door),"Wrong state");
            Check(coordinator.IsMovementRestricted==!document,"Movement policy changed");
            Check(!coordinator.CanExecuteGestureAction && !actions.TryExecute(),"Gesture allowed during interaction");
            Check(!coordinator.TryAcquire(document?(MonoBehaviour)door:docs),"Exclusive ownership failed");
            coordinator.Release(document?(MonoBehaviour)door:docs);
            Check(coordinator.Owns(adapter),"A foreign release stole the session");
            Check(pose.Weight<=.001f,"Acquisition unexpectedly recomposed the skeleton in the same frame");
            Call(rig,"UpdateRig",1f);
            Check(pose.Weight>.99f,"Interaction pose failed to blend");
            if(test=="gesture_change")
            {
                for(int i=0;i<5;i++) state.SetFlexed((Finger)i,(17 & (1<<i))!=0);
                Call(rig,"UpdateRig",1f); Check(state.Current==(FlexedFingers)17,"Held gesture change failed");
            }
            var mask=state.Current;
            if(document) Check(!coordinator.ProcessMovement(Vector2.zero,true),"Document consumed free movement");
            else Check(coordinator.ProcessMovement(Vector2.zero,true),"Door did not consume movement");

            switch(test)
            {
                case "focus": Call(input,"OnApplicationFocus",false); Check(Get<bool>(input,"waitForRelease"),"Focus did not require release"); break;
                case "application_pause": Call(input,"OnApplicationPause",true); Check(Get<bool>(input,"waitForRelease"),"Application pause did not require release"); break;
                case "time_pause":
                    Set(input,"initialized",true);
                    float saved=Time.timeScale; Time.timeScale=0; try { Call(input,"Update"); } finally {Time.timeScale=saved;} break;
                case "mode": Call(input,"EndArmMode"); movement.ToggleMode(); Check(movement.Modes.IsEditing,"Mode did not change"); break;
                case "r2": Call(input,"EndArmMode"); break;
                case "object_disable":
                    if(document) { var binding=(Hand3DDocumentBinding)item; binding.Document.enabled=false; }
                    else item.GetComponent<DoorHandInteractable>().enabled=false;
                    Call(adapter,"Tick",Step); break;
                case "object_destroy": Object.DestroyImmediate(item.gameObject); Call(adapter,"Tick",Step); break;
                case "adapter_disable": adapter.enabled=false; Call(adapter,"OnDisable"); break;
                case "adapter_destroy": Object.DestroyImmediate(adapter); Call(coordinator,"LateUpdate"); break;
                case "input_disable": input.enabled=false; Call(input,"OnDisable"); break;
                case "coordinator_disable": coordinator.enabled=false; Call(coordinator,"OnDisable"); break;
                case "movement_disable": movement.enabled=false; Call(coordinator,"LateUpdate"); Check(!coordinator.CanAcquire(adapter),"Disabled movement allowed acquisition"); break;
                case "pose_disable": pose.enabled=false; Call(pose,"OnDisable"); Call(coordinator,"LateUpdate"); Check(!coordinator.CanAcquire(adapter),"Disabled pose allowed acquisition"); break;
                case "rig_disable": rig.enabled=false; Call(rig,"OnDisable"); Call(coordinator,"LateUpdate"); Check(!coordinator.CanAcquire(adapter),"Disabled rig allowed acquisition"); break;
                case "action":
                    Check(!Get<bool>(actions,"clicking") && actions.PointClickWeight==0f,"Acquisition left PointClick running");
                    coordinator.CancelSession(); break;
                default: coordinator.CancelSession(); break;
            }
            Check(!coordinator.IsOccupied && coordinator.Owner==null,"Session did not release");
            Check(!pose.IsGrabbing && !pose.IsHandHolding,"Visual request left active");
            Check(!movement.HasContact,"Contact left active");
            Check(state.Current==mask,"Logical fingers changed");
            if(pose.isActiveAndEnabled && rig.isActiveAndEnabled)
            {
                Check(coordinator.State==Hand3DInteractionState.Restoring,"Restoring state missing");
                Check(!coordinator.CanExecuteGestureAction && !actions.TryExecute(),"Gesture allowed during restoration");
                Call(rig,"UpdateRig",1f);
                Check(coordinator.State==Hand3DInteractionState.Free,"Pose restoration did not finish");
            }
            if(document) Call(display,"Tick",1f);
            if(item!=null && document) Check(item.GetComponent<Renderer>().enabled && item.GetComponent<Collider>().enabled,"Document did not return");
            coordinator.CancelSession(); if(adapter!=null) Call(adapter,"Cancel");
            Check(!coordinator.IsOccupied && state.Current==mask,"Repeated cancellation changed state");
            if(test=="repeat")
            {
                for(int i=0;i<3;i++)
                {
                    coordinator.EndArmSession(); Call(rig,"UpdateRig",1f); Call(fixture,"Move",center);
                    if(!document) { item.GetComponent<DoorHandInteractable>().RestoreOpenState(false); }
                    Call(adapter,"Tick",Step); Check(coordinator.Owns(adapter),"Repeated acquisition failed");
                    Call(rig,"UpdateRig",1f); coordinator.CancelSession(); Call(display,"Tick",1f); Call(rig,"UpdateRig",1f);
                    Check(!coordinator.IsOccupied && state.Current==mask,"Repeated release failed");
                }
            }
        }
    }
}
