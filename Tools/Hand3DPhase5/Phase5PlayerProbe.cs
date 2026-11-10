using System;
using System.Collections;
using System.IO;
using System.Linq;
using Metroidbrainia.Hand3DExperiment;
using UnityEngine;

namespace Metroidbrainia
{
    // This component is staged only in the disposable build scene, never in production Assets.
    public sealed class Phase5PlayerProbe : MonoBehaviour
    {
        private IEnumerator Start()
        {
            Application.targetFrameRate=60;yield return null;yield return null;
            bool success=true;string detail="";
            try
            {
                var rig=FindObjectsByType<Hand3DRig>(FindObjectsSortMode.None).Single();
                if(rig.RuntimePoses==null || rig.RuntimePoses.Data.profiles.Length!=13)throw new Exception("Runtime asset missing from player");
                if(typeof(Hand3DRuntimePoses).Assembly.GetReferencedAssemblies().Any(a=>a.Name.Contains("Editor")))throw new Exception("Editor dependency in player");
                rig.SelectPoseSystem(Hand3DRig.PoseSystem.Universal);
                rig.GetComponent<Hand3DFingerState>().SetFlexed(Finger.Thumb,true);rig.GetComponent<Hand3DFingerState>().SetFlexed(Finger.Index,true);
            }catch(Exception error){success=false;detail=error.ToString();}
            for(int i=0;i<40 && success;i++)yield return null;
            if(success)
            {
                var rig=FindObjectsByType<Hand3DRig>(FindObjectsSortMode.None).Single();success=rig.UniversalActive && rig.GetComponent<Hand3DFingerState>().Current==(FlexedFingers)3;
                detail=success?"Runtime asset loaded and Universal evaluated in a standalone player without Editor assemblies.":rig.UniversalError;
                rig.SelectPoseSystem(Hand3DRig.PoseSystem.Legacy);success&=!rig.UniversalActive;
            }
            string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-phase5Output");if(index>=0)File.WriteAllText(args[index+1],(success?"PASS":"FAIL")+"\n"+detail);
            Application.Quit(success?0:1);
        }
    }
}
