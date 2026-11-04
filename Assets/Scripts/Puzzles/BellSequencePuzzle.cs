using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Metroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class BellSequencePuzzle : MonoBehaviour
    {
        [SerializeField] private List<BellInteractable> requiredSequence = new List<BellInteractable>();
        [SerializeField] private DoorHandInteractable targetDoor;
        [SerializeField] private UnityEvent onSolved = new UnityEvent();

        private int nextExpectedIndex;

        public int NextExpectedIndex => nextExpectedIndex;
        public bool IsSolved { get; private set; }
        public UnityEvent OnSolved => onSolved;

        public void RestoreSolvedState(bool solved)
        {
            IsSolved = solved;
            nextExpectedIndex = solved ? requiredSequence.Count : 0;
            if (targetDoor != null)
                targetDoor.RestoreOpenState(solved);
        }

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
            onSolved?.Invoke();
        }
    }
}
