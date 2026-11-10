using Metroidbrainia.Hand3DExperiment;
using UnityEngine;
using UnityEngine.UI;

namespace Metroidbrainia
{
    // Staged outside Editor only in the disposable project so snapshot MonoBehaviours can be attached.
    public static class Phase1Counters
    {
        public static int raycasts, handProjections;
        public static RaycastHit[] BeforeRaycastAll(Vector3 origin, Vector3 direction, float distance, int layers, QueryTriggerInteraction triggers)
        { raycasts++; return Physics.RaycastAll(origin, direction, distance, layers, triggers); }
        public static bool BeforeHandProjection(Camera camera, RawImage image, Vector3 world, out Vector2 screen, out string reason)
        { handProjections++; return Hand3DScreenProjection.TryWorldToScreen(camera, image, world, out screen, out reason); }
    }
}
