using System;
using System.Globalization;
using System.Text;

namespace BciChess.Engine
{
    /// <summary>Builds and parses the UCI text commands used to talk to Stockfish.</summary>
    public static class UciProtocol
    {
        public const int MinSkillLevel = 0;
        public const int MaxSkillLevel = 20;

        /// <summary>"position fen &lt;start&gt; moves e2e4 e7e5 ..."</summary>
        public static string PositionCommand(EnginePosition position)
        {
            var sb = new StringBuilder("position fen ");
            sb.Append(position.Start.ToFen());
            if (position.Moves.Count > 0)
            {
                sb.Append(" moves");
                foreach (var move in position.Moves)
                    sb.Append(' ').Append(move.ToUci());
            }
            return sb.ToString();
        }

        /// <summary>Search by depth when <paramref name="depth"/> &gt; 0, otherwise by fixed move time.</summary>
        public static string GoCommand(int moveTimeMs, int depth)
        {
            if (depth > 0)
                return "go depth " + depth.ToString(CultureInfo.InvariantCulture);
            return "go movetime " + Math.Max(1, moveTimeMs).ToString(CultureInfo.InvariantCulture);
        }

        public static string SetOption(string name, int value) =>
            $"setoption name {name} value {value.ToString(CultureInfo.InvariantCulture)}";

        public static bool IsBestMoveLine(string line) =>
            line != null && (line == "bestmove" || line.StartsWith("bestmove ", StringComparison.Ordinal));

        /// <summary>
        /// Parses "bestmove e2e4 [ponder e7e5]". <paramref name="move"/> is null when the engine reports no
        /// move ("(none)" or "0000", i.e. mate or stalemate).
        /// </summary>
        public static bool TryParseBestMove(string line, out string move)
        {
            move = null;
            if (!IsBestMoveLine(line))
                return false;

            string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return false;

            string candidate = parts[1];
            if (candidate == "(none)" || candidate == "0000")
                return true;
            if (candidate.Length != 4 && candidate.Length != 5)
                return false;

            move = candidate;
            return true;
        }
    }
}
