using System;
using System.Collections.Generic;

namespace BciChess.Bci
{
    /// <summary>Optional: a selector that reports how much evidence it has gathered for the current selection.</summary>
    public interface ISelectionProgress
    {
        /// <summary>Fewest flashes any current target has had since the selection started.</summary>
        int FlashesCollected { get; }

        /// <summary>Flashes a target needs before it can be selected (0 = no minimum).</summary>
        int FlashesRequired { get; }
    }

    /// <summary>
    /// Counts flashes per stimulus slot since the last <see cref="Reset"/>. Selectors use it to enforce a minimum
    /// number of flashes before a selection is accepted.
    /// </summary>
    public sealed class FlashCounter : IDisposable
    {
        private readonly IStimulusSource _source;
        private readonly Dictionary<int, int> _counts = new Dictionary<int, int>();

        public FlashCounter(IStimulusSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _source.FlashStarted += OnFlash;
        }

        public void Reset() => _counts.Clear();

        public int CountOf(int slotIndex) => _counts.TryGetValue(slotIndex, out int count) ? count : 0;

        /// <summary>The smallest count among <paramref name="slotIndices"/> (0 if empty).</summary>
        public int MinimumOver(IEnumerable<int> slotIndices)
        {
            int minimum = int.MaxValue;
            foreach (int slot in slotIndices)
                minimum = Math.Min(minimum, CountOf(slot));
            return minimum == int.MaxValue ? 0 : minimum;
        }

        public void Dispose() => _source.FlashStarted -= OnFlash;

        private void OnFlash(int slotIndex) => _counts[slotIndex] = CountOf(slotIndex) + 1;
    }
}
