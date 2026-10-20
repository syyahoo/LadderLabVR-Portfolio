using System;
using System.Collections.Generic;
using UnityEngine;

namespace LadderLab.PLC
{
    [Serializable]
    public sealed class LadderRungData
    {
        [SerializeField] private List<LadderElementData> contacts = new();
        [SerializeField] private LadderElementData sealInContact;
        [SerializeField] private LadderElementData coil;
        [SerializeField] private bool useTimerOnDelay;
        [SerializeField, Min(0f)] private float timerPresetSeconds;

        private float timerElapsedSeconds;

        public LadderRungData(IEnumerable<LadderElementData> targetContacts, LadderElementData targetCoil)
            : this(targetContacts, null, targetCoil)
        {
        }

        public LadderRungData(
            IEnumerable<LadderElementData> targetContacts,
            LadderElementData targetSealInContact,
            LadderElementData targetCoil)
        {
            contacts = new List<LadderElementData>(targetContacts ?? Array.Empty<LadderElementData>());
            sealInContact = targetSealInContact;
            coil = targetCoil;
        }

        public IReadOnlyList<LadderElementData> Contacts => contacts;
        public LadderElementData SealInContact => sealInContact;
        public LadderElementData Coil => coil;
        public bool UseTimerOnDelay => useTimerOnDelay;
        public float TimerPresetSeconds => timerPresetSeconds;
        public float TimerElapsedSeconds => timerElapsedSeconds;

        public static LadderRungData TimerOnDelay(
            IEnumerable<LadderElementData> targetContacts,
            LadderElementData targetCoil,
            float targetPresetSeconds)
        {
            return new LadderRungData(targetContacts, null, targetCoil)
            {
                useTimerOnDelay = true,
                timerPresetSeconds = Mathf.Max(0f, targetPresetSeconds)
            };
        }

        public bool Execute(IPlcBackend backend)
        {
            return Execute(backend, 0f);
        }

        public bool Execute(IPlcBackend backend, float deltaTime)
        {
            if (coil == null)
                throw new InvalidOperationException("Ladder rung requires a coil.");

            var energized = EvaluateFirstContact(backend);
            var startIndex = contacts.Count > 0 ? 1 : 0;

            for (var index = startIndex; index < contacts.Count; index++)
            {
                var contact = contacts[index];
                if (contact == null)
                    throw new InvalidOperationException("Ladder rung contains an empty contact.");

                energized &= contact.EvaluateContact(backend);
                if (!energized)
                    break;
            }

            if (useTimerOnDelay)
                energized = EvaluateTimerOnDelay(energized, deltaTime);

            coil.WriteCoil(backend, energized);
            return energized;
        }

        public void ResetRuntimeState()
        {
            timerElapsedSeconds = 0f;
        }

        private bool EvaluateFirstContact(IPlcBackend backend)
        {
            if (contacts.Count == 0)
                return sealInContact == null || sealInContact.EvaluateContact(backend);

            var firstContact = contacts[0];
            if (firstContact == null)
                throw new InvalidOperationException("Ladder rung contains an empty contact.");

            var energized = firstContact.EvaluateContact(backend);
            if (sealInContact != null)
                energized |= sealInContact.EvaluateContact(backend);

            return energized;
        }

        private bool EvaluateTimerOnDelay(bool condition, float deltaTime)
        {
            if (!condition)
            {
                timerElapsedSeconds = 0f;
                return false;
            }

            timerElapsedSeconds += Mathf.Max(0f, deltaTime);
            return timerElapsedSeconds + 0.0001f >= timerPresetSeconds;
        }
    }
}
