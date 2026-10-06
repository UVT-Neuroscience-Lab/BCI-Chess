using System.Collections.Generic;
using BciChess.Unicorn;
using UnityEngine;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>
    /// Full-screen calibration display shown while the g.tec paradigm is training: one tile per stimulus class,
    /// flashed by the paradigm itself, with the training target clearly marked. Training is started and stopped
    /// from the g.tec BCI bar, which is drawn above this view.
    /// </summary>
    public sealed class BciCalibrationView : MonoBehaviour
    {
        private const float TileSize = 150f;
        private const float Spacing = 28f;
        private const int TilesPerRow = 6;

        private readonly Dictionary<int, BciTargetVisual> _tiles = new Dictionary<int, BciTargetVisual>();
        private UnicornBciRig _rig;
        private GameObject _overlay;
        private Text _calibrationText;

        public void Build(RectTransform canvasRoot, BoardTheme theme, StimulusVisualSettings visuals, UnicornBciRig rig)
        {
            _rig = rig;

            var overlay = UiFactory.CreateImage("BciCalibration", canvasRoot, new Color(0.04f, 0.05f, 0.07f, 0.97f),
                raycastTarget: true);
            UiFactory.Stretch(overlay.rectTransform);
            _overlay = overlay.gameObject;

            var title = UiFactory.CreateText("Title", overlay.transform, "BCI calibration", 52, theme.text,
                TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            UiFactory.Place(title.rectTransform, new Vector2(0f, 330f), new Vector2(1400f, 80f));

            var instructions = UiFactory.CreateText("Instructions", overlay.transform,
                "Look at the tile marked TARGET and silently count every time it flashes.\n" +
                "Ignore the other tiles. Sit still and keep your face relaxed.",
                28, theme.mutedText, TextAnchor.MiddleCenter);
            UiFactory.Place(instructions.rectTransform, new Vector2(0f, 240f), new Vector2(1400f, 90f));

            var classIds = rig.ClassIds;
            int rows = (classIds.Count + TilesPerRow - 1) / TilesPerRow;
            for (int i = 0; i < classIds.Count; i++)
            {
                int row = i / TilesPerRow;
                int inRow = Mathf.Min(TilesPerRow, classIds.Count - row * TilesPerRow);
                int column = i % TilesPerRow;
                float x = (column - (inRow - 1) / 2f) * (TileSize + Spacing);
                float y = 20f - (row - (rows - 1) / 2f) * (TileSize + Spacing);
                _tiles[classIds[i]] = CreateTile(overlay.transform, theme, visuals, classIds[i], i, new Vector2(x, y),
                    classIds[i] == rig.TrainingClassId);
            }

            _calibrationText = UiFactory.CreateText("Calibration", overlay.transform, "", 24, theme.accent,
                TextAnchor.MiddleCenter);
            UiFactory.Place(_calibrationText.rectTransform, new Vector2(0f, -330f), new Vector2(1400f, 40f));

            rig.StimulusChanged += OnStimulusChanged;
            rig.StateChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_rig == null)
                return;
            _rig.StimulusChanged -= OnStimulusChanged;
            _rig.StateChanged -= Refresh;
        }

        private BciTargetVisual CreateTile(Transform parent, BoardTheme theme, StimulusVisualSettings visuals,
            int classId, int slotIndex, Vector2 position, bool isTrainingTarget)
        {
            var tile = UiFactory.CreateImage($"Class{classId}", parent, theme.button);
            UiFactory.Place(tile.rectTransform, position, new Vector2(TileSize, TileSize));

            var label = UiFactory.CreateText("Label", tile.transform, isTrainingTarget ? "TARGET" : classId.ToString(),
                isTrainingTarget ? 30 : 40, isTrainingTarget ? theme.warning : theme.buttonText, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            UiFactory.Stretch(label.rectTransform);

            if (isTrainingTarget)
            {
                var ring = UiFactory.CreateImage("TargetMarker", tile.transform, theme.warning, UiFactory.Frame);
                ring.type = Image.Type.Sliced;
                UiFactory.Stretch(ring.rectTransform, -10f);
            }

            var visual = BciTargetVisual.Attach(tile.rectTransform, visuals, withBadge: false);
            visual.SetTarget(true, slotIndex);
            return visual;
        }

        private void OnStimulusChanged(int classId, bool on)
        {
            if (_overlay.activeSelf && _tiles.TryGetValue(classId, out var tile))
                tile.SetLit(on);
        }

        private void Refresh()
        {
            bool training = _rig.IsTraining;
            _overlay.SetActive(training);
            if (!training)
            {
                foreach (var tile in _tiles.Values)
                    tile.SetLit(false);
            }
            _calibrationText.text = string.IsNullOrEmpty(_rig.CalibrationSummary)
                ? string.Empty
                : "Previous calibration: " + _rig.CalibrationSummary;
        }
    }
}
