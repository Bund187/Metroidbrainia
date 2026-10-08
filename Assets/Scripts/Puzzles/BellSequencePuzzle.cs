using System.Collections.Generic;
using UnityEngine;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class BellSequencePuzzle : MonoBehaviour
    {
        [SerializeField] private List<BellInteractable> requiredSequence = new List<BellInteractable>();
        [SerializeField] private DoorHandInteractable targetDoor;

        private int nextExpectedIndex;

        public int NextExpectedIndex => nextExpectedIndex;
        public bool IsSolved { get; private set; }

        public void NotifyBellRung(BellInteractable bell)
        {
            if (!isActiveAndEnabled || IsSolved || bell == null || requiredSequence.Count == 0)
                return;

            if (bell == requiredSequence[nextExpectedIndex])
                nextExpectedIndex++;
            else
                nextExpectedIndex = bell == requiredSequence[0] ? 1 : 0;

            if (nextExpectedIndex != requiredSequence.Count)
                return;

            IsSolved = true;
            if (targetDoor != null)
            {
                targetDoor.Unlock();
                targetDoor.OpenAutomatically();
            }
        }
    }
}
