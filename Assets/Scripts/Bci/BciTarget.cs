using System;

namespace BciChess.Bci
{
    /// <summary>
    /// One stimulus channel the BCI can tell apart. For the g.tec ERP paradigm this is a flash class id.
    /// Slots are reused: they belong to a target only for the duration of one selection.
    /// </summary>
    public readonly struct StimulusSlot : IEquatable<StimulusSlot>
    {
        /// <summary>Position in the configured slot list (0-based). The simulated BCI maps keys to this.</summary>
        public readonly int Index;

        /// <summary>ERP class id flashed for this slot.</summary>
        public readonly int ClassId;

        public StimulusSlot(int index, int classId)
        {
            Index = index;
            ClassId = classId;
        }

        public override string ToString() => $"slot {Index + 1} (class {ClassId})";

        public bool Equals(StimulusSlot other) => Index == other.Index && ClassId == other.ClassId;
        public override bool Equals(object obj) => obj is StimulusSlot other && Equals(other);
        public override int GetHashCode() => (Index * 397) ^ ClassId;
    }

    /// <summary>Where a target is on screen, in any consistent unit (e.g. board squares). Used to find neighbours.</summary>
    public readonly struct TargetPosition
    {
        public readonly float X;
        public readonly float Y;

        public TargetPosition(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float DistanceTo(TargetPosition other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>
    /// Something the player can pick with the BCI. The BCI layer treats the payload as opaque, so it
    /// never needs to know whether a target is a chess piece, a square or a menu option.
    /// </summary>
    public sealed class BciTarget
    {
        public BciTarget(string id, string label, object payload = null, StimulusSlot? stimulus = null,
            TargetPosition? position = null)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Target id is required.", nameof(id));
            Id = id;
            Label = label ?? id;
            Payload = payload;
            Stimulus = stimulus;
            Position = position;
        }

        /// <summary>Optional on-screen position. Groups take their members' positions instead.</summary>
        public TargetPosition? Position { get; }

        public string Id { get; }
        public string Label { get; }

        /// <summary>Optional application data (e.g. a chess square). Not interpreted by the BCI layer.</summary>
        public object Payload { get; }

        /// <summary>Stimulus assigned for the current selection, or null if not yet assigned.</summary>
        public StimulusSlot? Stimulus { get; }

        public BciTarget WithStimulus(StimulusSlot slot) => new BciTarget(Id, Label, Payload, slot, Position);

        public override string ToString() => Stimulus.HasValue ? $"{Label} [{Stimulus.Value}]" : Label;
    }
}
