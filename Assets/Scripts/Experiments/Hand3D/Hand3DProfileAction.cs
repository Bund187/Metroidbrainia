using System;
using System.Linq;
using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment
{
    [CreateAssetMenu(menuName="Metroidbrainia/Hand 3D/Actions/Profile Pulse")]
    public sealed class Hand3DProfileAction : Hand3DGestureAction
    {
        [SerializeField] private string profileId;
        [SerializeField,Min(.01f)] private float duration=.3f;
        [SerializeField,Range(0,1)] private float amplitude=1f;
        public override bool CanBegin(Hand3DActionContext context,out string reason)
        {
            reason=null;
            var profile=context.Profiles!=null?context.Profiles.Find(profileId):null;
            if(!context.UniversalActive)reason="Profile actions require the Universal backend; PointClick and events also support Legacy.";
            else if(profile==null)reason="Missing action profile: "+profileId;
            else if((profile.contexts&(Hand3DPoseProfile.Context.Action|(context.Mode==Hand3DMode.HandEdit?Hand3DPoseProfile.Context.HandEdit:Hand3DPoseProfile.Context.Exploration)))==0)reason="Profile context does not allow this mode: "+profileId;
            else if(profile.joints.Any(j=>j.enabled && j.weight>0 && j.influenceChannels!=0 && j.space==Hand3DPoseProfile.Space.ExplorationWristOffset))reason="Action profiles must use explicit local/camera rotations, not the migrated Exploration wrist-offset adapter: "+profileId;
            return reason==null;
        }
        public override Run Begin(Hand3DActionContext context)=>new Pulse(profileId,Mathf.Max(.01f,duration),Mathf.Clamp01(amplitude));
        private sealed class Pulse : Run
        {
            private readonly string id;private readonly float duration,amplitude;private float elapsed,weight;
            public Pulse(string id,float duration,float amplitude){this.id=id;this.duration=duration;this.amplitude=amplitude;}
            public override string ProfileId=>id;
            public override float ProfileWeight=>weight;
            public override bool Tick(float dt){elapsed+=dt;float t=Mathf.Clamp01(elapsed/duration);weight=Mathf.Sin(t*Mathf.PI)*amplitude;return t>=1f;}
            public override void End(bool cancelled){weight=0;}
        }
    }
}
