using System;
using System.Collections.Generic;

namespace BciChess.Bci
{
    /// <summary>
    /// The single place where stimuli are assigned to targets. Configured with the available ERP class ids;
    /// nothing else in the project hard-codes stimulus values.
    /// </summary>
    public sealed class StimulusManager
    {
        private readonly StimulusSlot[] _slots;

        /// <param name="classIds">Available ERP class ids, in assignment order. Duplicates are ignored.</param>
        /// <param name="maxSimultaneousTargets">Upper bound on targets shown at once (&lt;= 0 means use all slots).</param>
        public StimulusManager(IEnumerable<int> classIds, int maxSimultaneousTargets)
        {
            if (classIds == null)
                throw new ArgumentNullException(nameof(classIds));

            var distinct = new List<int>();
            foreach (int id in classIds)
            {
                if (!distinct.Contains(id))
                    distinct.Add(id);
            }

            int count = maxSimultaneousTargets > 0 ? Math.Min(maxSimultaneousTargets, distinct.Count) : distinct.Count;
            if (count < 1)
                throw new ArgumentException("At least one stimulus class id is required.", nameof(classIds));

            _slots = new StimulusSlot[count];
            for (int i = 0; i < count; i++)
                _slots[i] = new StimulusSlot(i, distinct[i]);
        }

        /// <summary>How many targets can be presented in one selection.</summary>
        public int Capacity => _slots.Length;

        public IReadOnlyList<StimulusSlot> Slots => _slots;

        /// <summary>
        /// Gives every candidate its own stimulus, deterministically in list order. Fails when there are
        /// more candidates than slots; the caller must then reduce or group the candidates.
        /// </summary>
        public bool TryAssign(IReadOnlyList<BciTarget> candidates, out IReadOnlyList<BciTarget> assigned)
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));

            if (candidates.Count > _slots.Length)
            {
                assigned = Array.Empty<BciTarget>();
                return false;
            }

            var result = new BciTarget[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
                result[i] = candidates[i].WithStimulus(_slots[i]);
            assigned = result;
            return true;
        }

        /// <summary>
        /// Like <see cref="TryAssign(IReadOnlyList{BciTarget}, out IReadOnlyList{BciTarget})"/>, but neighbouring
        /// targets get slots that are as different as possible, measured by <paramref name="slotDistance"/>
        /// (default: distance of the slot indices around the slot ring). Greedy and deterministic: the most connected
        /// targets choose first, each taking the free slot farthest from its already-assigned neighbours, then
        /// farthest from all assigned slots. Output keeps the order of <paramref name="candidates"/>.
        /// </summary>
        public bool TryAssign(IReadOnlyList<BciTarget> candidates, NeighbourGraph neighbours,
            out IReadOnlyList<BciTarget> assigned, Func<int, int, double> slotDistance = null)
        {
            if (neighbours == null)
                return TryAssign(candidates, out assigned);
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));
            if (neighbours.Count != candidates.Count)
                throw new ArgumentException("Neighbour graph does not match the candidates.", nameof(neighbours));

            if (candidates.Count > _slots.Length)
            {
                assigned = Array.Empty<BciTarget>();
                return false;
            }

            var distance = slotDistance ?? RingDistance;
            var slotOf = new int[candidates.Count];
            for (int i = 0; i < slotOf.Length; i++)
                slotOf[i] = -1;
            var used = new bool[_slots.Length];

            var order = new List<int>();
            for (int i = 0; i < candidates.Count; i++)
                order.Add(i);
            order.Sort((a, b) =>
            {
                int byDegree = neighbours.NeighboursOf(b).Count.CompareTo(neighbours.NeighboursOf(a).Count);
                return byDegree != 0 ? byDegree : a.CompareTo(b);
            });

            foreach (int target in order)
            {
                int bestSlot = -1;
                double bestToNeighbours = double.MinValue;
                double bestToAll = double.MinValue;

                for (int slot = 0; slot < _slots.Length; slot++)
                {
                    if (used[slot])
                        continue;

                    double toNeighbours = double.MaxValue;
                    foreach (int neighbour in neighbours.NeighboursOf(target))
                    {
                        if (slotOf[neighbour] >= 0)
                            toNeighbours = Math.Min(toNeighbours, distance(slot, slotOf[neighbour]));
                    }

                    double toAll = double.MaxValue;
                    for (int other = 0; other < slotOf.Length; other++)
                    {
                        if (slotOf[other] >= 0)
                            toAll = Math.Min(toAll, distance(slot, slotOf[other]));
                    }

                    if (toNeighbours > bestToNeighbours ||
                        (toNeighbours == bestToNeighbours && toAll > bestToAll))
                    {
                        bestSlot = slot;
                        bestToNeighbours = toNeighbours;
                        bestToAll = toAll;
                    }
                }

                slotOf[target] = bestSlot;
                used[bestSlot] = true;
            }

            var result = new BciTarget[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
                result[i] = candidates[i].WithStimulus(_slots[slotOf[i]]);
            assigned = result;
            return true;
        }

        private double RingDistance(int a, int b)
        {
            int d = Math.Abs(a - b);
            return Math.Min(d, _slots.Length - d);
        }
    }
}
