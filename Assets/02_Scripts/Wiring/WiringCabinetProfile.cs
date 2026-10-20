using System;
using System.Collections.Generic;
using UnityEngine;

namespace LadderLab.Wiring
{
    [CreateAssetMenu(menuName = "LadderLab/Wiring Cabinet Profile", fileName = "WiringCabinetProfile")]
    public sealed class WiringCabinetProfile : ScriptableObject
    {
        [SerializeField] private List<WiringEndpointAssignment> assignments = new();

        public IReadOnlyList<WiringEndpointAssignment> Assignments => assignments;

        public bool TryGetAssignment(string endpointId, out WiringEndpointAssignment assignment)
        {
            foreach (var candidate in assignments)
            {
                if (candidate != null && candidate.Matches(endpointId))
                {
                    assignment = candidate;
                    return true;
                }
            }

            assignment = null;
            return false;
        }

        public bool ContainsDuplicateEndpointIds()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assignment in assignments)
            {
                if (assignment == null || string.IsNullOrWhiteSpace(assignment.EndpointId))
                    continue;

                if (!seen.Add(assignment.EndpointId))
                    return true;
            }

            return false;
        }
    }
}
