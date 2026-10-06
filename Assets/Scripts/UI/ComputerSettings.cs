using System;
using System.IO;
using UnityEngine;

namespace BciChess.UI
{
    /// <summary>One selectable computer strength.</summary>
    [Serializable]
    public sealed class DifficultyLevel
    {
        public string name;

        [Tooltip("Stockfish skill level: 0 = weakest, 20 = full strength.")]
        [Range(0, 20)] public int skillLevel;

        [Tooltip("Thinking time per move in milliseconds (used when search depth is 0).")]
        [Min(50)] public int moveTimeMs = 500;

        [Tooltip("Fixed search depth; 0 = use the thinking time instead.")]
        [Min(0)] public int searchDepth;

        public DifficultyLevel()
        {
        }

        public DifficultyLevel(string name, int skillLevel, int moveTimeMs, int searchDepth = 0)
        {
            this.name = name;
            this.skillLevel = skillLevel;
            this.moveTimeMs = moveTimeMs;
            this.searchDepth = searchDepth;
        }

        public string Summary => searchDepth > 0
            ? $"Stockfish skill {skillLevel}, depth {searchDepth}"
            : $"Stockfish skill {skillLevel}, {moveTimeMs / 1000f:0.#} s per move";
    }

    /// <summary>Computer opponent configuration, tunable in the inspector. Mode, side and level are chosen in the lobby.</summary>
    [Serializable]
    public sealed class ComputerSettings
    {
        [Tooltip("Stockfish executable. Relative paths are resolved against Assets/StreamingAssets.")]
        public string stockfishPath = "Stockfish/stockfish.exe";

        [Tooltip("Difficulty levels offered in the lobby, weakest first.")]
        public DifficultyLevel[] difficultyLevels =
        {
            new DifficultyLevel("Beginner", 0, 100, 1),
            new DifficultyLevel("Casual", 3, 200),
            new DifficultyLevel("Intermediate", 6, 400),
            new DifficultyLevel("Advanced", 10, 600),
            new DifficultyLevel("Expert", 15, 900),
            new DifficultyLevel("Master", 20, 1500),
        };

        [Tooltip("Show 'Computer thinking...' for at least this long, so moves don't appear instantly.")]
        [Min(0f)] public float minimumThinkSeconds = 0.8f;

        [Tooltip("If Stockfish is missing or crashes, continue with a simple built-in opponent.")]
        public bool useFallbackOpponent = true;

        public DifficultyLevel GetLevel(int index)
        {
            if (difficultyLevels == null || difficultyLevels.Length == 0)
                return new DifficultyLevel("Default", 5, 500);
            return difficultyLevels[Mathf.Clamp(index, 0, difficultyLevels.Length - 1)];
        }

        public string ResolveStockfishPath()
        {
            if (string.IsNullOrWhiteSpace(stockfishPath))
                return string.Empty;
            return Path.IsPathRooted(stockfishPath)
                ? stockfishPath
                : Path.Combine(Application.streamingAssetsPath, stockfishPath);
        }
    }
}
