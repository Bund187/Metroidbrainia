using UnityEngine;

namespace Metroidbrainia.Hand3DExperiment
{
    [CreateAssetMenu(menuName = "Metroidbrainia/Hand 3D/Actions/Point World Interaction")]
    public sealed class Hand3DPointWorldAction : Hand3DGestureAction
    {
        public override bool CanBegin(Hand3DActionContext context, out string reason)
        {
            reason = null;
            var adapter = context.Interactions != null ? context.Interactions.GetComponent<Hand3DPointInteractor>() : null;
            return adapter != null && adapter.CanPerform(context);
        }
        public override Run Begin(Hand3DActionContext context)
        {
            var adapter = context.Interactions != null ? context.Interactions.GetComponent<Hand3DPointInteractor>() : null;
            if (adapter != null) adapter.TryPerform(context);
            return null;
        }
    }
}
