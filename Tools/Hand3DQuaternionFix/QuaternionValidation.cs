using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Metroidbrainia.Hand3DExperiment;
using UnityEditor;
using UnityEngine;

namespace Metroidbrainia
{
    public static class QuaternionValidation
    {
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        [Serializable] class Report { public string runId,firstViolation,firstStack,sourceMaximum,scope; public int assertions,quaternionErrors,universalErrors,legacyErrors,trsCalls; public double maxTrsDeviation,maxWorldDeviation,maxSerializedDeviation; public bool reproduced; }
        static Report report;
        static int errors;
        static void Check(bool ok,string detail){if(!ok)throw new Exception(detail);report.assertions++;}
        static object Call(object instance,string method,params object[] args)=>instance.GetType().GetMethods(Flags).Single(m=>m.Name==method && m.GetParameters().Length==args.Length).Invoke(instance,args);
        static T Get<T>(object instance,string field)=>(T)instance.GetType().GetField(field,Flags).GetValue(instance);
        static double Norm(Quaternion q)=>(double)q.x*q.x+(double)q.y*q.y+(double)q.z*q.z+(double)q.w*q.w;
        static void Audit(string source,Quaternion q)
        {
            double deviation=Math.Abs(Norm(q)-1);
            if(source.StartsWith("TRS/")){report.trsCalls++;report.maxTrsDeviation=Math.Max(report.maxTrsDeviation,deviation);}
            else report.maxWorldDeviation=Math.Max(report.maxWorldDeviation,deviation);
            if(deviation>1e-6 && report.firstViolation==null){report.firstViolation=source+" q="+q.ToString("F9")+" squared norm="+Norm(q).ToString("R");report.firstStack=Environment.StackTrace;}
            if(!report.reproduced)Check(!double.IsNaN(deviation) && !double.IsInfinity(deviation) && deviation<1e-6,"Non-unit quaternion at "+source+": "+deviation);
        }
        static void Log(string text,string stack,LogType type){if(text.Contains("Quaternion To Matrix"))errors++;}
        public static void Run()
        {
            bool before=!typeof(Hand3DPoseComposition).GetMethods(Flags).Any(m=>m.Name=="NormalizeRotation");
            string[] args=Environment.GetCommandLineArgs();report=new Report{runId=args[Array.IndexOf(args,"-phase5RunId")+1],reproduced=before,scope="Numerical stress in isolated Unity Editor; no Game visual approval."};
            try
            {
                Application.SetStackTraceLogType(LogType.Assert,StackTraceLogType.None);
                Application.logMessageReceived+=Log;
                typeof(Hand3DPoseComposition).GetField("RotationAudit",Flags).SetValue(null,(Action<string,Quaternion>)Audit);
                foreach(bool universal in new[]{false,true})
                {
                    int start=errors;
                    var f=typeof(Phase5Validation).GetMethod("Create",Flags).Invoke(null,new object[]{universal,null});
                    var rig=Get<Hand3DRig>(f,"rig");var arm=Get<Hand3DArmMovement>(f,"arm");var interaction=Get<Hand3DInteractionPose>(f,"interaction");var bones=Get<Transform[]>(f,"bones");
                    foreach(var profile in rig.RuntimePoses.Data.profiles)foreach(var j in profile.joints)
                    {
                        double d=Math.Abs(Norm(j.rotation)-1);if(d>report.maxSerializedDeviation){report.maxSerializedDeviation=d;report.sourceMaximum=profile.id+" / "+j.jointPath;}
                    }
                    var scales=bones.Select(b=>b.localScale).ToArray();float upper=Vector3.Distance(arm.UpperArm.position,arm.Forearm.position),lower=Vector3.Distance(arm.Forearm.position,arm.Hand.position);
                    Call(f,"Mask",29);Call(f,"Step",1f);arm.BeginControl();
                    for(int i=0;i<6000;i++)
                    {
                        if(i%71==0)Call(f,"Mask",(i/71)%32);
                        if(i%600==400)arm.ToggleMode();if(i%600==500)arm.ToggleMode();
                        if(i%300==100)interaction.SetGrab(true,Quaternion.Euler(0,25,0));if(i%300==130)interaction.SetGrab(false);
                        if(i%300==180)interaction.SetHandHold(true);if(i%300==220)interaction.SetHandHold(false);
                        float speed=Mathf.Pow(10,-(i/100)%4);
                        Vector2 delta=i<300?new Vector2(Mathf.Sin(i*.07f)*2,0):i<600?new Vector2(0,Mathf.Sin(i*.07f)*2):new Vector2(Mathf.Sin(i*.09f)*2,Mathf.Cos(i*.11f)*2)*speed;
                        if(i%333==0)delta*=10000;
                        arm.MoveHand(delta,true);Call(f,"Step",1f/60f);
                        Check(!universal || rig.UniversalActive,"Universal fell back: "+rig.UniversalError);
                        Check(Mathf.Abs(Vector3.Distance(arm.UpperArm.position,arm.Forearm.position)-upper)<.00001f && Mathf.Abs(Vector3.Distance(arm.Forearm.position,arm.Hand.position)-lower)<.00001f,"Changed segment length");
                        foreach(var bone in bones){Check(!float.IsNaN(bone.position.sqrMagnitude) && !float.IsInfinity(bone.position.sqrMagnitude),"Invalid position");Check(Math.Abs(Norm(bone.rotation)-1)<1e-5,"Invalid committed orientation");}
                        if(universal)Check(Quaternion.Angle(arm.Hand.rotation,rig.UniversalComposition.FinalWrist)<.05f,"Wrist orientation lost");
                    }
                    interaction.SetGrab(false);interaction.SetHandHold(false);arm.EndControl();Call(f,"Mask",29);
                    for(int i=0;i<180;i++)Call(f,"Step",1f/60f);var rest=arm.Hand.position;
                    for(int cycle=0;cycle<20;cycle++){arm.BeginControl();arm.MoveHand(new Vector2(45,-25),true);Call(f,"Step",.1f);arm.EndControl();for(int i=0;i<100;i++)Call(f,"Step",1f/60f);}
                    Check(Vector3.Distance(rest,arm.Hand.position)<.0001f,"Return drift");
                    Check(bones.Select((b,i)=>b.localScale==scales[i]).All(v=>v),"Scale changed");
                    if(universal)report.universalErrors=errors-start;else report.legacyErrors=errors-start;
                }
                // Replay the rounded quaternion from the actual Game report at the IK buffer
                // boundary. This is an isolated numerical fault replay, not a modified capture.
                {
                    var f=typeof(Phase5Validation).GetMethod("Create",Flags).Invoke(null,new object[]{true,null});var rig=Get<Hand3DRig>(f,"rig");var arm=Get<Hand3DArmMovement>(f,"arm");
                    var buffer=Get<Hand3DPoseComposition.Frame>(rig.UniversalComposition,"result");
                    var recorded=new Quaternion(-.008593f,-.204440f,.078868f,.975667f);
                    for(int i=0;i<8;i++)
                    {
                        buffer.SetWorldRotation(buffer.Index(arm.UpperArm),recorded);
                        arm.SolveUniversal(buffer,arm.Hand.position+new Vector3(.005f,-.003f,.001f),rig.UniversalComposition.ReferenceElbow);
                        buffer.Commit();
                        arm.MoveHand(new Vector2(.1f,-.2f),true);Call(f,"Step",1f/60f);
                    }
                }
                report.quaternionErrors=errors;
                if(before)Check(errors>0,"Original bug was not reproduced");else Check(errors==0,"Quaternion To Matrix errors remain");
                if(!before)InvalidInputs();
                File.WriteAllText("quaternion-results.json",JsonUtility.ToJson(report,true));EditorApplication.Exit(0);
            }
            catch(Exception e){report.quaternionErrors=errors;File.WriteAllText("quaternion-results.json",JsonUtility.ToJson(report,true));Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void InvalidInputs()
        {
            var f=typeof(Phase5Validation).GetMethod("Create",Flags).Invoke(null,new object[]{true,null});var rig=Get<Hand3DRig>(f,"rig");var pose=rig.UniversalComposition;
            var method=typeof(Hand3DPoseComposition).GetMethod("NormalizeRotation",Flags);
            foreach(var bad in new[]{new Quaternion(float.NaN,0,0,1),new Quaternion(float.PositiveInfinity,0,0,1),new Quaternion(0,0,0,0),new Quaternion(1e-15f,0,0,0)})
            {
                bool rejected=false;try{method.Invoke(pose,new object[]{bad,0,"test phase","injected input"});}catch(TargetInvocationException e){rejected=e.InnerException.Message.Contains("test phase") && e.InnerException.Message.Contains("injected input") && e.InnerException.Message.Contains("armsFBX");}Check(rejected,"Unrecoverable input not diagnosed");
            }
            var repaired=(Quaternion)method.Invoke(pose,new object[]{new Quaternion(.1f,.2f,.3f,.9f),0,"test phase","scaled input"});Check(Math.Abs(Norm(repaired)-1)<1e-6,"Recoverable quaternion not normalized");
            repaired=(Quaternion)method.Invoke(pose,new object[]{new Quaternion(float.MaxValue,float.MaxValue,0,0),0,"test phase","large finite input"});Check(Math.Abs(Norm(repaired)-1)<1e-6,"Finite quaternion normalization overflowed");
            var copy=UnityEngine.Object.Instantiate(rig.RuntimePoses);
            try
            {
                var broken=typeof(Phase5Validation).GetMethod("Create",Flags).Invoke(null,new object[]{true,copy});var brokenRig=Get<Hand3DRig>(broken,"rig");
                copy=brokenRig.RuntimePoses;
                Call(broken,"Mask",0);
                var entry=copy.Find(copy.Extended).joints.First(j=>(j.influenceChannels&Hand3DPoseProfile.Channels.Rotation)!=0);
                entry.rotation=new Quaternion(float.NaN,0,0,1);
                int diagnostics=0;Application.LogCallback count=(text,stack,type)=>{if(text.Contains("[Hand3D Universal] Evaluation rejected"))diagnostics++;};
                Application.logMessageReceived+=count;
                try{Call(broken,"Step",.02f);Call(broken,"Step",.02f);}finally{Application.logMessageReceived-=count;}
                Check(!brokenRig.UniversalActive && diagnostics==1,"Bad runtime input must fall back once, without frame spam. active="+brokenRig.UniversalActive+", diagnostics="+diagnostics+", mask="+Get<Hand3DFingerState>(broken,"state").Current+", same asset="+(brokenRig.RuntimePoses==copy)+", error="+brokenRig.UniversalError);
                Check(brokenRig.UniversalError.Contains(copy.Extended) && brokenRig.UniversalError.Contains("phase"),"Fallback lacks joint/phase/profile diagnosis");
            }finally{UnityEngine.Object.DestroyImmediate(copy);}
        }
        public static void Quick()
        {
            report=new Report();try{InvalidInputs();Debug.Log("QUICK PASS");EditorApplication.Exit(0);}catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}
