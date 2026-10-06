using System;
using System.Collections.Generic;

namespace BciChess.Bci
{
    /// <summary>
    /// Tells the visuals which stimulus slots are lit. Either generated locally (<see cref="FlashSequencer"/>)
    /// or driven by the BCI device's own paradigm, which owns flash timing and EEG triggers.
    /// </summary>
    public interface IStimulusSource
    {
        /// <summary>True while stimuli are being presented.</summary>
        bool IsRunning { get; }

        /// <summary>True while the stimulus of <paramref name="slotIndex"/> is lit. Several slots may be lit at once.</summary>
        bool IsLit(int slotIndex);

        /// <summary>Raised when the stimulus of a slot turns on.</summary>
        event Action<int> FlashStarted;
    }

    /// <summary>Optional: a selector that can explain its state to the player (e.g. "headset not connected").</summary>
    public interface IBciStatusProvider
    {
        string StatusText { get; }
    }

    /// <summary>
    /// ERP-style oddball flashing: within each round every active slot flashes exactly once, in random order,
    /// for <c>onTimeMs</c>, followed by <c>offTimeMs</c> of darkness.
    /// Neighbouring slots (targets close together on screen) are kept apart in time: two neighbours flash at least
    /// <c>neighbourSpacing</c> flashes apart, also across round boundaries, so their brain responses overlap less.
    /// A slot never flashes twice in a row. If the constraint cannot be met for a round, the order with the fewest
    /// violations is used. Time is advanced explicitly with <see cref="Tick"/>.
    /// </summary>
    public sealed class FlashSequencer : IStimulusSource
    {
        private const int AttemptsPerRound = 30;

        private readonly Random _random;
        private readonly List<int> _slots = new List<int>();
        private readonly List<int> _round = new List<int>();
        private readonly Dictionary<int, HashSet<int>> _neighbours = new Dictionary<int, HashSet<int>>();
        private readonly List<int> _history = new List<int>();
        private int _spacing = 2;
        private int _position;
        private bool _lit;
        private float _timeInPhase;

        public FlashSequencer(float onTimeMs, float offTimeMs, int seed = 0)
        {
            SetTiming(onTimeMs, offTimeMs);
            _random = seed == 0 ? new Random() : new Random(seed);
        }

        public event Action<int> FlashStarted;

        public float OnTimeSeconds { get; private set; }

        /// <summary>Dark pause after each flash, before the next one starts.</summary>
        public float OffTimeSeconds { get; private set; }

        /// <summary>Changes the flash timing; takes effect from the next phase (e.g. per selection step).</summary>
        public void SetTiming(float onTimeMs, float offTimeMs)
        {
            if (onTimeMs <= 0f)
                throw new ArgumentOutOfRangeException(nameof(onTimeMs), "Flash on-time must be positive.");
            if (offTimeMs < 0f)
                throw new ArgumentOutOfRangeException(nameof(offTimeMs), "Flash off-time cannot be negative.");
            OnTimeSeconds = onTimeMs / 1000f;
            OffTimeSeconds = offTimeMs / 1000f;
        }
        public bool IsRunning { get; private set; }
        public int? LitSlot => IsRunning && _lit ? _round[_position] : (int?)null;

        /// <summary>Completed rounds since <see cref="Start"/> (each slot flashed once per round).</summary>
        public int RoundsCompleted { get; private set; }

        /// <summary>Rounds whose order had to break the neighbour spacing because no valid order was found.</summary>
        public int RoundsWithSpacingViolations { get; private set; }

        public bool IsLit(int slotIndex) => LitSlot == slotIndex;

        /// <summary>Starts flashing the given slots. The first flash begins immediately.</summary>
        /// <param name="neighbourPairs">Slot pairs whose targets are close on screen.</param>
        /// <param name="neighbourSpacing">
        /// Minimum distance, in flashes, between flashes of two neighbours (1 = may follow directly,
        /// 2 = at least one other flash in between, ...).
        /// </param>
        public void Start(IReadOnlyList<int> slots, IEnumerable<(int, int)> neighbourPairs = null, int neighbourSpacing = 2)
        {
            if (slots == null)
                throw new ArgumentNullException(nameof(slots));

            _slots.Clear();
            foreach (int slot in slots)
            {
                if (!_slots.Contains(slot))
                    _slots.Add(slot);
            }

            _neighbours.Clear();
            if (neighbourPairs != null)
            {
                foreach (var (a, b) in neighbourPairs)
                {
                    if (a == b)
                        continue;
                    NeighbourSet(a).Add(b);
                    NeighbourSet(b).Add(a);
                }
            }

            _spacing = Math.Max(1, neighbourSpacing);
            _history.Clear();
            RoundsCompleted = 0;
            RoundsWithSpacingViolations = 0;
            IsRunning = _slots.Count > 0;
            if (!IsRunning)
                return;

            NewRound();
            BeginFlash();
        }

        public void Stop()
        {
            IsRunning = false;
            _lit = false;
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsRunning || deltaSeconds <= 0f)
                return;

            _timeInPhase += deltaSeconds;
            while (IsRunning)
            {
                float phaseLength = _lit ? OnTimeSeconds : OffTimeSeconds;
                if (_timeInPhase < phaseLength)
                    break;
                _timeInPhase -= phaseLength;

                if (_lit)
                {
                    _lit = false;
                }
                else
                {
                    _position++;
                    if (_position >= _round.Count)
                    {
                        RoundsCompleted++;
                        NewRound();
                    }
                    BeginFlash();
                }
            }
        }

        private HashSet<int> NeighbourSet(int slot)
        {
            if (!_neighbours.TryGetValue(slot, out var set))
                _neighbours[slot] = set = new HashSet<int>();
            return set;
        }

        private void BeginFlash()
        {
            _lit = true;
            int slot = _round[_position];
            _history.Add(slot);
            if (_history.Count > 64)
                _history.RemoveRange(0, _history.Count - 64);
            FlashStarted?.Invoke(slot);
        }

        /// <summary>Builds the next round: random order, as close to the spacing constraint as possible.</summary>
        private void NewRound()
        {
            List<int> best = null;
            int bestViolations = int.MaxValue;

            for (int attempt = 0; attempt < AttemptsPerRound && bestViolations > 0; attempt++)
            {
                var candidate = BuildRoundAttempt(out int violations);
                if (violations < bestViolations)
                {
                    best = candidate;
                    bestViolations = violations;
                }
            }

            if (bestViolations > 0)
                RoundsWithSpacingViolations++;
            _round.Clear();
            _round.AddRange(best);
            _position = 0;
        }

        /// <summary>
        /// Greedy random construction: each step picks randomly among the remaining slots that keep the spacing
        /// to recent flashes; if none does, the one whose conflict lies furthest back.
        /// </summary>
        private List<int> BuildRoundAttempt(out int violations)
        {
            var remaining = new List<int>(_slots);
            var recent = new List<int>(_history);
            var order = new List<int>(remaining.Count);
            violations = 0;

            while (remaining.Count > 0)
            {
                var valid = new List<int>();
                int fallback = -1;
                int fallbackGap = -1;
                foreach (int slot in remaining)
                {
                    int gap = GapToLastConflict(slot, recent);
                    if (gap >= _spacing)
                    {
                        valid.Add(slot);
                    }
                    else if (gap > fallbackGap)
                    {
                        fallbackGap = gap;
                        fallback = slot;
                    }
                }

                int chosen;
                if (valid.Count > 0)
                {
                    chosen = valid[_random.Next(valid.Count)];
                }
                else
                {
                    chosen = fallback;
                    violations++;
                }

                order.Add(chosen);
                recent.Add(chosen);
                remaining.Remove(chosen);
            }
            return order;
        }

        /// <summary>
        /// How many flashes ago a neighbour of <paramref name="slot"/> last flashed within the spacing window
        /// (1 = the previous flash), or int.MaxValue if none did. Repeating the previous flash's own slot scores 0,
        /// worse than any neighbour conflict, so a slot never flashes twice in a row unless it is the only one.
        /// </summary>
        private int GapToLastConflict(int slot, List<int> recent)
        {
            if (recent.Count > 0 && recent[recent.Count - 1] == slot && _slots.Count > 1)
                return 0;

            _neighbours.TryGetValue(slot, out var neighbours);
            if (neighbours == null)
                return int.MaxValue;

            for (int back = 1; back < _spacing && back <= recent.Count; back++)
            {
                if (neighbours.Contains(recent[recent.Count - back]))
                    return back;
            }
            return int.MaxValue;
        }
    }
}
