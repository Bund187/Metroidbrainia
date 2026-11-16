using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment
{
    [CreateAssetMenu(menuName="Metroidbrainia/Hand 3D/Actions/Point Click")]
    public sealed class Hand3DPointClickAction : Hand3DGestureAction
    {
        [SerializeField,Min(.01f)] private float duration=.18f;
        [SerializeField,Range(0,1)] private float amplitude=.18f;
        internal void ConfigureCompatibility(float seconds,float weight){duration=seconds;amplitude=weight;}
        public override Run Begin(Hand3DActionContext context)=>new Pulse(duration,amplitude);
        private sealed class Pulse : Run
        {
            private readonly float duration,amplitude;
            private float elapsed,weight;
            public Pulse(float duration,float amplitude){this.duration=Mathf.Max(.01f,duration);this.amplitude=Mathf.Clamp01(amplitude);}
            public override float IndexFlexWeight=>weight;
            public override bool Tick(float dt){elapsed+=dt;float t=Mathf.Clamp01(elapsed/duration);weight=Mathf.Sin(t*Mathf.PI)*amplitude;return t>=1f;}
            public override void End(bool cancelled){weight=0;}
        }
    }
}
