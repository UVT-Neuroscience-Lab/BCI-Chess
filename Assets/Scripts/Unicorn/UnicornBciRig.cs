using System;
using System.Collections.Generic;
using System.Linq;
using BciChess.Bci;
using Gtec.Chain.Common.Templates.Utilities;
using Gtec.UnityInterface;
using UnityEngine;

namespace BciChess.Unicorn
{
    /// <summary>
    /// Instantiates and configures the g.tec ERP prefab for the game: device type, number of classes, flash timing,
    /// training trials, selection threshold, and one invisible <see cref="StimulusProxyTag"/> per stimulus class (plus the training object)
    /// in place of the prefab's demo sprites. Everything Unicorn-specific is created here.
    /// </summary>
    public sealed class UnicornBciRig : MonoBehaviour
    {
        private readonly List<StimulusProxyTag> _tags = new List<StimulusProxyTag>();
        private float _refreshRate = 60f;
        private int _frames;
        private float _frameSeconds;

        public UnicornBciSelector Selector { get; private set; }
        public ERPParadigm Paradigm { get; private set; }
        public ERPPipeline Pipeline { get; private set; }
        public Device Device { get; private set; }

        /// <summary>Class ids in use, one per stimulus slot.</summary>
        public IReadOnlyList<int> ClassIds { get; private set; }

        /// <summary>Class the player attends to during calibration.</summary>
        public int TrainingClassId { get; private set; }

        public bool IsTraining => Selector != null && Selector.IsTraining;

        /// <summary>Last calibration summary, e.g. "Good (8 trials)"; empty before the first calibration.</summary>
        public string CalibrationSummary { get; private set; } = string.Empty;

        /// <summary>(class id, on) for every flash, including during training.</summary>
        public event Action<int, bool> StimulusChanged;

        /// <summary>Raised when training/application starts or stops, or a calibration result arrives.</summary>
        public event Action StateChanged;

        /// <summary>
        /// Creates the rig. The prefab is instantiated under an inactive holder so it can be configured before
        /// any g.tec component runs Awake/Start. Throws if the prefab or its components are missing.
        /// </summary>
        /// <param name="uiSortingOrder">Sorting order for the g.tec UI canvases (should be above the game UI).</param>
        /// <param name="minimumFlashes">Flashes a target needs since it appeared before a selection of it counts.</param>
        public static UnicornBciRig Create(UnicornSettings settings, IReadOnlyList<StimulusSlot> slots, int uiSortingOrder,
            int minimumFlashes)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (settings.erpPrefab == null)
                throw new InvalidOperationException(
                    "No g.tec ERP prefab assigned (BCI > Unicorn > Erp Prefab). Use the menu BCI Chess > Assign Unicorn BCI Prefab.");
            if (slots == null || slots.Count < 2)
                throw new ArgumentException("The g.tec ERP paradigm needs at least 2 stimulus classes.", nameof(slots));

            var holder = new GameObject("Unicorn BCI");
            holder.SetActive(false);
            try
            {
                var instance = Instantiate(settings.erpPrefab, holder.transform);
                instance.name = settings.erpPrefab.name;
                var rig = holder.AddComponent<UnicornBciRig>();
                rig.Configure(settings, slots, uiSortingOrder, minimumFlashes);
                holder.SetActive(true);
                return rig;
            }
            catch
            {
                Destroy(holder);
                throw;
            }
        }

        private void Configure(UnicornSettings settings, IReadOnlyList<StimulusSlot> slots, int uiSortingOrder,
            int minimumFlashes)
        {
            Paradigm = GetComponentInChildren<ERPParadigm>(true);
            Pipeline = GetComponentInChildren<ERPPipeline>(true);
            Device = GetComponentInChildren<Device>(true);
            if (Paradigm == null || Pipeline == null || Device == null)
                throw new InvalidOperationException(
                    $"Prefab '{settings.erpPrefab.name}' lacks an ERPParadigm, ERPPipeline or Device component.");

            var classIds = slots.Select(s => s.ClassId).Distinct().ToList();
            if (classIds.Any(id => id < 1))
                throw new ArgumentException("ERP class ids must be 1 or greater.", nameof(slots));
            ClassIds = classIds;
            TrainingClassId = classIds[0];

            Device.Product = settings.deviceType;
            // Class ids are 1-based; the paradigm must know about the highest one in use.
            Paradigm.NumberOfClasses = (uint)Mathf.Max(2, classIds.Max());
            Paradigm.SelectionThreshold = settings.selectionThreshold;
            Paradigm.OnTimeMs = settings.flashOnTimeMs;
            Paradigm.OffTimeMs = settings.flashOffTimeMs;
            // The g.tec paradigm refuses fewer than 30 training trials.
            Paradigm.NumberOfTrainingTrials = (uint)Mathf.Max(30, settings.numberOfTrainingTrials);
            LockFrameRateToDisplay();

            ReplaceDemoTags(classIds);

            BringUiToFront(uiSortingOrder);

            Selector = new UnicornBciSelector(Paradigm, Pipeline, Device, slots, minimumFlashes);
            Selector.Diagnostic += message => Debug.Log("[Unicorn] " + message, this);

            Paradigm.OnParadigmStarted.AddListener(RaiseStateChanged);
            Paradigm.OnParadigmStopped.AddListener(RaiseStateChanged);
            Pipeline.OnCalibrationResult.AddListener(OnCalibrationResult);
            Pipeline.OnRuntimeExceptionOccured.AddListener(OnPipelineException);
            Device.OnRuntimeExceptionOccured.AddListener(e => Debug.LogException(e, this));
        }

        /// <summary>
        /// Draws the g.tec UI above the game. Its main canvas (BCI_UI) is "Screen Space - Camera", which always
        /// renders behind a "Screen Space - Overlay" canvas such as the game's, whatever the sorting order; so every
        /// root canvas is switched to overlay mode with a higher sorting order. Canvas.isRootCanvas cannot be used
        /// here because it reports false while the hierarchy is still inactive.
        /// </summary>
        private void BringUiToFront(int sortingOrder)
        {
            foreach (var canvas in GetComponentsInChildren<Canvas>(true))
            {
                var parent = canvas.transform.parent;
                bool isRoot = parent == null || parent.GetComponentInParent<Canvas>(true) == null;
                if (!isRoot)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = sortingOrder;
            }
        }

        /// <summary>The paradigm collects ERPTag children when it starts; give it ours instead of the demo sprites.</summary>
        private void ReplaceDemoTags(IReadOnlyList<int> classIds)
        {
            foreach (var demoTag in Paradigm.GetComponentsInChildren<ERPTag>(true))
            {
                demoTag.gameObject.SetActive(false);
                Destroy(demoTag.gameObject);
            }

            var container = new GameObject("BciChessStimulusTags").transform;
            container.SetParent(Paradigm.transform, false);

            foreach (int classId in classIds)
                CreateTag(container, classId, isTrainingObject: false);
            CreateTag(container, TrainingClassId, isTrainingObject: true);
        }

        private void CreateTag(Transform parent, int classId, bool isTrainingObject)
        {
            var go = new GameObject(isTrainingObject ? $"TrainingTag (class {classId})" : $"StimulusTag (class {classId})");
            go.transform.SetParent(parent, false);
            var tag = go.AddComponent<StimulusProxyTag>();
            tag.ClassId = classId;
            tag.IsTrainingObject = isTrainingObject;
            tag.StimulusChanged += OnTagStimulusChanged;
            _tags.Add(tag);
        }

        private void OnTagStimulusChanged(int classId, bool on)
        {
            Selector?.OnStimulusChanged(classId, on);
            StimulusChanged?.Invoke(classId, on);
        }

        private void OnCalibrationResult(ERPParadigm paradigm, CalibrationResult result)
        {
            if (result == null)
                return;
            CalibrationSummary = $"{result.CalibrationQuality} ({result.TrialsSelected} trials)";
            Debug.Log("[Unicorn] Calibration: " + CalibrationSummary, this);
            RaiseStateChanged();
        }

        /// <summary>
        /// The g.tec ERP paradigm advances its flash sequence once per rendered frame and sizes that sequence for the
        /// display refresh rate. If the game renders faster (the Editor's Game view runs uncapped unless its VSync
        /// toggle is on), flashes come far too quickly, their EEG triggers collide and get lost, and training ends
        /// with too few trials ("More data required"). So render at exactly the refresh rate.
        /// </summary>
        private void LockFrameRateToDisplay()
        {
            double refresh = Screen.currentResolution.refreshRateRatio.value;
            _refreshRate = refresh > 1.0 ? (float)refresh : 60f;
#if UNITY_EDITOR
            // The Game view ignores QualitySettings.vSyncCount, but honours a target frame rate when VSync is off.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Mathf.RoundToInt(_refreshRate);
#else
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
#endif
        }

        /// <summary>Warns while flashing if the frame rate drifts from the refresh rate the paradigm assumes.</summary>
        private void Update()
        {
            if (Paradigm == null || !Paradigm.IsRunning)
            {
                _frames = 0;
                _frameSeconds = 0f;
                return;
            }

            _frames++;
            _frameSeconds += Time.unscaledDeltaTime;
            if (_frameSeconds < 5f)
                return;
            float fps = _frames / _frameSeconds;
            if (Mathf.Abs(fps - _refreshRate) > _refreshRate * 0.15f)
            {
                Debug.LogWarning($"[Unicorn] Rendering at {fps:0} fps but the display runs at {_refreshRate:0} Hz. The g.tec " +
                                 "paradigm times flashes in frames, so flashes run at the wrong speed and EEG triggers can " +
                                 "be lost. In the Editor, turn on VSync in the Game view toolbar or use a build.", this);
            }
            _frames = 0;
            _frameSeconds = 0f;
        }

        private void OnPipelineException(Exception exception)
        {
            // Thrown by the g.tec classifier training (10-fold cross-validation) when fewer than 10 trials arrived.
            if (exception != null && exception.Message.Contains("More data required"))
            {
                CalibrationSummary = "failed - too few training trials reached the EEG pipeline. Train again.";
                Debug.LogWarning("[Unicorn] Calibration failed: the classifier needs at least 10 complete training " +
                                 "trials, but most EEG triggers were lost. Usually the frame rate did not match the display " +
                                 "refresh rate (see warnings above) or training was stopped early. Start training again.", this);
                RaiseStateChanged();
                return;
            }
            Debug.LogException(exception, this);
        }

        private void RaiseStateChanged() => StateChanged?.Invoke();

        private void OnDestroy()
        {
            foreach (var tag in _tags)
            {
                if (tag)
                    tag.StimulusChanged -= OnTagStimulusChanged;
            }
            Selector?.Dispose();
        }
    }
}
