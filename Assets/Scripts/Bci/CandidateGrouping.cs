using System;
using System.Collections.Generic;

namespace BciChess.Bci
{
    /// <summary>A set of candidates presented as one BCI target. Used as that target's payload.</summary>
    public sealed class CandidateGroup
    {
        public CandidateGroup(IReadOnlyList<BciTarget> members)
        {
            if (members == null || members.Count == 0)
                throw new ArgumentException("A group needs at least one member.", nameof(members));
            Members = members;
        }

        public IReadOnlyList<BciTarget> Members { get; }

        /// <summary>Short description such as "Pawn a2 .. Knight g1 (4)".</summary>
        public string Describe()
        {
            if (Members.Count == 1)
                return Members[0].Label;
            if (Members.Count == 2)
                return Members[0].Label + ", " + Members[1].Label;
            return $"{Members[0].Label} .. {Members[Members.Count - 1].Label} ({Members.Count})";
        }
    }

    /// <summary>Splits candidates into groups when there are more candidates than stimulus slots.</summary>
    public interface ICandidateGroupingStrategy
    {
        /// <summary>
        /// Returns between 2 and <paramref name="maxGroups"/> groups that together contain every candidate
        /// exactly once. Groups may still be larger than <paramref name="maxGroups"/>; they are split again
        /// when the player enters them.
        /// </summary>
        IReadOnlyList<CandidateGroup> Group(IReadOnlyList<BciTarget> candidates, int maxGroups);
    }

    /// <summary>
    /// Keeps the candidates' order and cuts the list into about sqrt(n) consecutive groups of nearly equal size.
    /// That balances the two selection steps (choose group, then choose member), and because callers order
    /// candidates spatially, each group is a compact area of the board.
    /// </summary>
    public sealed class BalancedGroupingStrategy : ICandidateGroupingStrategy
    {
        public IReadOnlyList<CandidateGroup> Group(IReadOnlyList<BciTarget> candidates, int maxGroups)
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));
            if (maxGroups < 2)
                throw new ArgumentOutOfRangeException(nameof(maxGroups), "Grouping needs at least 2 slots.");
            if (candidates.Count < 2)
                throw new ArgumentException("Grouping needs at least 2 candidates.", nameof(candidates));

            int n = candidates.Count;
            int groupCount = (int)Math.Ceiling(Math.Sqrt(n));
            groupCount = Math.Max(2, Math.Min(groupCount, maxGroups));

            var groups = new List<CandidateGroup>(groupCount);
            int baseSize = n / groupCount;
            int remainder = n % groupCount;
            int start = 0;
            for (int g = 0; g < groupCount; g++)
            {
                int size = baseSize + (g < remainder ? 1 : 0);
                var members = new BciTarget[size];
                for (int i = 0; i < size; i++)
                    members[i] = candidates[start + i];
                groups.Add(new CandidateGroup(members));
                start += size;
            }
            return groups;
        }
    }
}
