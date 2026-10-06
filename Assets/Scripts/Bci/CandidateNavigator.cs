using System;
using System.Collections.Generic;

namespace BciChess.Bci
{
    /// <summary>
    /// Walks a candidate list hierarchically: if the candidates fit the available slots they are offered
    /// directly; otherwise they are offered as groups, and choosing a group descends into it.
    /// </summary>
    public sealed class CandidateNavigator
    {
        private readonly ICandidateGroupingStrategy _strategy;
        private readonly Stack<IReadOnlyList<BciTarget>> _parents = new Stack<IReadOnlyList<BciTarget>>();
        private IReadOnlyList<BciTarget> _current = Array.Empty<BciTarget>();

        public CandidateNavigator(ICandidateGroupingStrategy strategy)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        }

        /// <summary>0 at the top level; increases each time a group is entered.</summary>
        public int Depth => _parents.Count;

        /// <summary>All leaf candidates reachable at the current level.</summary>
        public IReadOnlyList<BciTarget> CurrentCandidates => _current;

        public void Reset(IReadOnlyList<BciTarget> candidates)
        {
            _parents.Clear();
            _current = candidates ?? throw new ArgumentNullException(nameof(candidates));
        }

        /// <summary>
        /// The options to present at the current level using at most <paramref name="slots"/> targets:
        /// the candidates themselves, or group targets whose payload is a <see cref="CandidateGroup"/>.
        /// Returns false when the candidates do not fit and cannot be grouped (fewer than 2 slots).
        /// </summary>
        public bool TryGetOptions(int slots, out IReadOnlyList<BciTarget> options)
        {
            if (_current.Count <= slots)
            {
                options = _current;
                return true;
            }
            if (slots < 2)
            {
                options = Array.Empty<BciTarget>();
                return false;
            }

            var groups = _strategy.Group(_current, slots);
            var result = new List<BciTarget>(groups.Count);
            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                // A group of one is pointless to enter: offer its member directly.
                result.Add(group.Members.Count == 1
                    ? group.Members[0]
                    : new BciTarget($"group:{Depth}:{i}", group.Describe(), group));
            }
            options = result;
            return true;
        }

        public void Enter(CandidateGroup group)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));
            _parents.Push(_current);
            _current = group.Members;
        }

        /// <summary>Returns to the parent level. False at the top level.</summary>
        public bool Back()
        {
            if (_parents.Count == 0)
                return false;
            _current = _parents.Pop();
            return true;
        }
    }

    /// <summary>Payload of navigation targets that are not candidates themselves.</summary>
    public enum NavigationCommand
    {
        /// <summary>Leave the current group and return to the previous level.</summary>
        Back
    }
}
