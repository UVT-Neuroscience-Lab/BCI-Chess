using System;
using Gtec.Chain.Common.Nodes.FilterNodes;
using Gtec.UnityInterface;
using UnityEngine;

namespace BciChess.Unicorn
{
    /// <summary>Unicorn Hybrid Black / g.tec ERP configuration, tunable in the inspector.</summary>
    [Serializable]
    public sealed class UnicornSettings
    {
        [Tooltip("The g.tec 'BCI Visual ERP 2D' prefab (Assets/g.tec/Unity Interface/Prefabs/BCI). " +
                 "Menu: BCI Chess > Assign Unicorn BCI Prefab.")]
        public GameObject erpPrefab;

        [Tooltip("Unicorn for the real headset; UnicornSimulator to test the pipeline without hardware.")]
        public Device.DeviceType deviceType = Device.DeviceType.Unicorn;

        [Header("ERP paradigm (applied to the g.tec ERPParadigm when the game starts)")]
        [Tooltip("How long each flash stays on, in milliseconds. g.tec prefab default: 100.")]
        [Min(1f)] public float flashOnTimeMs = 50f;

        [Tooltip("Dark time after each flash, in milliseconds. g.tec prefab default: 900. " +
                 "Shorter on/off times flash faster and make selections quicker.")]
        [Min(0f)] public float flashOffTimeMs = 450f;

        [Tooltip("Flashes of the target collected during training. Fewer = shorter calibration, " +
                 "possibly less accurate. g.tec requires at least 30; prefab default: 60.")]
        [Min(30)] public int numberOfTrainingTrials = 45;

        [Header("Selection")]
        [Tooltip("How confident the g.tec classifier must be before it reports a selection.")]
        public ERPScoreStatistics.SelectionThreshold selectionThreshold = ERPScoreStatistics.SelectionThreshold.Confidence95;

        [Tooltip("If the Unicorn setup fails (e.g. prefab missing), continue with the simulated BCI.")]
        public bool fallBackToSimulated = true;
    }
}
