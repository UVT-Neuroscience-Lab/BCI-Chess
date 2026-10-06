using System;
using Gtec.Chain.Common.Templates.Utilities;
using Gtec.UnityInterface;

namespace BciChess.Unicorn
{
    /// <summary>
    /// An ERP tag with no visuals of its own. The g.tec paradigm switches it on and off exactly when it flashes
    /// (and timestamps the EEG trigger); the tag just reports that, so the game's own UI can draw the flash.
    /// </summary>
    public sealed class StimulusProxyTag : ERPTag
    {
        /// <summary>(class id, on) whenever the paradigm turns this tag's stimulus on or off.</summary>
        public event Action<int, bool> StimulusChanged;

        public override void SetStimulusOn() => StimulusChanged?.Invoke(ClassId, true);

        public override void SetStimulusOff() => StimulusChanged?.Invoke(ClassId, false);

        // Selections are consumed centrally by UnicornBciSelector via ERPPipeline.OnClassSelection.
        public override void OnSelected(ERPPipeline sender, ClassSelection classSelection)
        {
        }
    }
}
