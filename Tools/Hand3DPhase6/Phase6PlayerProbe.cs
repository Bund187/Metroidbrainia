using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Metroidbrainia.Hand3DExperiment;
using UnityEngine;

namespace Metroidbrainia
{
    // Staged with the old probe's class/file name only inside the disposable player scene.
    public sealed class Phase5PlayerProbe : MonoBehaviour
    {
        private bool failed;private string error;
        private void Check(bool condition,string text){if(!condition){failed=true;error=text;}}
        private IEnumerator Start()
        {
            Time.captureDeltaTime=.01f;yield return null;yield return null;
            var rig=FindObjectsByType<Hand3DRig>(FindObjectsSortMode.None).Single();var a=rig.GetComponent<Hand3DPoseActions>();var state=rig.GetComponent<Hand3DFingerState>();
            Check(!typeof(Hand3DProfileAction).Assembly.GetReferencedAssemblies().Any(n=>n.Name.Contains("Editor")),"Editor dependency in player actions");
            int configured=0,original=0;
            var click=ScriptableObject.CreateInstance<Hand3DPointClickAction>();var ev=ScriptableObject.CreateInstance<Hand3DEventAction>();
            var binding=new Hand3DPoseActions.Binding{gesture=(FlexedFingers)29,actions=new Hand3DGestureAction[]{click,ev}};binding.onEvent.AddListener(()=>configured++);
            typeof(Hand3DPoseActions).GetField("bindings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(a,new[]{binding});
            for(int finger=0;finger<5;finger++)state.SetFlexed((Finger)finger,(29&(1<<finger))!=0);
            rig.GetComponent<Hand3DPoseRecognizer>().CurrentPose.OnAction.AddListener(()=>original++);
            foreach(bool universal in new[]{true,false})
            {
                rig.SelectPoseSystem(universal?Hand3DRig.PoseSystem.Universal:Hand3DRig.PoseSystem.Legacy);
                for(int i=0;i<50;i++)yield return null;
                Check(!universal || rig.UniversalActive,"Universal unavailable in player");
                foreach(int mode in new[]{0,1})
                {
                    if(mode==1){rig.GetComponent<Hand3DArmMovement>().ToggleMode();for(int i=0;i<50;i++)yield return null;}
                    Check(a.TryExecute(Hand3DActionInput.Mouse),"Point/event request failed in player");
                    for(int i=0;i<4;i++)yield return null;Check(a.PointClickWeight>0,"Point pulse missing in player");
                    for(int i=0;i<30;i++)yield return null;Check(!a.IsRunning && a.PointClickWeight==0,"Player action did not restore");
                }
                rig.GetComponent<Hand3DArmMovement>().ToggleMode();for(int i=0;i<50;i++)yield return null;
            }
            Check(configured==4 && original==4,"Duplicate/missing player events");
            string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-phase5Output");if(index>=0)File.WriteAllText(args[index+1],(failed?"FAIL":"PASS")+"\n"+(failed?error:"Runtime actions, PointClick and events executed in both modes and both backends; original/configured events each fired exactly four times. No Game visual approval."));
            Application.Quit(failed?1:0);
        }
    }
}
