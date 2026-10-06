using System;
using System.Collections.Generic;

namespace BciChess.Bci
{
    /// <summary>
    /// Which targets are close to each other on screen. Close targets are the ones a BCI confuses most, so they
    /// get stimuli that are as different as possible (see <see cref="StimulusManager"/> and <see cref="FlashSequencer"/>).
    /// A target's points are its own position, or for a group the positions of all its members; two targets are
    /// neighbours when any of their points are within the neighbour distance.
    /// </summary>
    public sealed class NeighbourGraph
    {
        private readonly List<int>[] _neighbours;

        private NeighbourGraph(int count)
        {
            _neighbours = new List<int>[count];
            for (int i = 0; i < count; i++)
                _neighbours[i] = new List<int>();
        }

        public int Count => _neighbours.Length;

        /// <summary>A graph in which no two targets are neighbours.</summary>
        public static NeighbourGraph Empty(int count) => new NeighbourGraph(count);

        /// <param name="neighbourDistance">Maximum distance between points of two neighbouring targets (&lt;= 0: none are neighbours).</param>
        public static NeighbourGraph Build(IReadOnlyList<BciTarget> targets, float neighbourDistance)
        {
            if (targets == null)
                throw new ArgumentNullException(nameof(targets));

            var graph = new NeighbourGraph(targets.Count);
            if (neighbourDistance <= 0f)
                return graph;

            var points = new List<TargetPosition>[targets.Count];
            for (int i = 0; i < targets.Count; i++)
                points[i] = PointsOf(targets[i]);

            for (int i = 0; i < targets.Count; i++)
            {
                for (int j = i + 1; j < targets.Count; j++)
                {
                    if (AnyWithin(points[i], points[j], neighbourDistance))
                    {
                        graph._neighbours[i].Add(j);
                        graph._neighbours[j].Add(i);
                    }
                }
            }
            return graph;
        }

        public IReadOnlyList<int> NeighboursOf(int index) => _neighbours[index];

        public bool AreNeighbours(int a, int b) => _neighbours[a].Contains(b);

        /// <summary>Every neighbouring pair once, as (lower index, higher index).</summary>
        public IEnumerable<(int, int)> Pairs()
        {
            for (int i = 0; i < _neighbours.Length; i++)
            {
                foreach (int j in _neighbours[i])
                {
                    if (j > i)
                        yield return (i, j);
                }
            }
        }

        private static List<TargetPosition> PointsOf(BciTarget target)
        {
            var points = new List<TargetPosition>();
            if (target.Payload is CandidateGroup group)
            {
                foreach (var member in group.Members)
                    points.AddRange(PointsOf(member));
            }
            else if (target.Position.HasValue)
            {
                points.Add(target.Position.Value);
            }
            return points;
        }

        private static bool AnyWithin(List<TargetPosition> a, List<TargetPosition> b, float distance)
        {
            foreach (var p in a)
            {
                foreach (var q in b)
                {
                    if (p.DistanceTo(q) <= distance)
                        return true;
                }
            }
            return false;
        }
    }
}
