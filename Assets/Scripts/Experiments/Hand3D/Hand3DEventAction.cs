namespace Metroidbrainia.Hand3DExperiment
{
    [UnityEngine.CreateAssetMenu(menuName="Metroidbrainia/Hand 3D/Actions/Configured Event")]
    public sealed class Hand3DEventAction : Hand3DGestureAction
    {
        public override Run Begin(Hand3DActionContext context){context.InvokeConfiguredEvent();return null;}
    }
}
