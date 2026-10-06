using System;
using UnityEngine;

namespace BciChess.UI
{
    public enum GameMode
    {
        /// <summary>Human against Stockfish.</summary>
        VsComputer,

        /// <summary>Two humans share this machine and play both colours.</summary>
        LocalTwoPlayer
    }

    public enum SideChoice
    {
        White,
        Random,
        Black
    }

    /// <summary>Everything the player chooses in the lobby. Saved between sessions in PlayerPrefs.</summary>
    [Serializable]
    public sealed class GamePreferences
    {
        private const string PrefsKey = "BciChess.Preferences";

        [Header("Game")]
        public GameMode mode = GameMode.VsComputer;
        public SideChoice playAs = SideChoice.White;
        public int difficulty = 2;
        [Tooltip("Local 2-player: turn the board after every move so the side to move is at the bottom.")]
        public bool autoFlip;

        [Header("Appearance")]
        public string boardScheme = "green";
        public string pieceSet = "cburnett";
        public bool showCoordinates = true;
        public bool showLegalMoves = true;
        public bool highlightLastMove = true;
        public bool animateMoves = true;

        [Header("Sound")]
        [Range(0f, 1f)] public float masterVolume = 0.8f;
        public bool soundEnabled = true;
        public bool gameSounds = true;
        public bool interfaceSounds = true;
        public bool bciSounds = true;

        public BoardColorScheme BoardScheme => BoardColorScheme.Find(boardScheme);
        public PieceSet PieceSet => PieceSet.Find(pieceSet);

        public static GamePreferences Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
                if (!string.IsNullOrEmpty(json))
                    return JsonUtility.FromJson<GamePreferences>(json) ?? new GamePreferences();
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning($"Ignoring unreadable saved preferences: {e.Message}");
            }
            return new GamePreferences();
        }

        public void Save()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
    }
}
