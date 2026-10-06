using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BciChess.Core;

namespace BciChess.Engine
{
    /// <summary>
    /// Plays one colour of a <see cref="ChessGame"/> with an <see cref="IChessEngine"/>. Requests are
    /// asynchronous; a result that arrives after the position changed (undo, reset) is discarded.
    /// If the engine fails, the optional fallback engine takes over for the rest of the session.
    /// </summary>
    public sealed class ComputerPlayer : IDisposable
    {
        private readonly ChessGame _game;
        private readonly IChessEngine _fallback;
        private readonly TimeSpan _minimumThinkTime;
        private IChessEngine _engine;
        private CancellationTokenSource _thinking;
        private bool _enabled;

        /// <param name="minimumThinkSeconds">
        /// Shortest visible "thinking" time, so the player can follow what happens. 0 = play as soon as the engine answers.
        /// </param>
        public ComputerPlayer(ChessGame game, IChessEngine engine, PieceColor color, IChessEngine fallback = null,
            float minimumThinkSeconds = 0f)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _fallback = fallback;
            _minimumThinkTime = TimeSpan.FromSeconds(Math.Max(0f, minimumThinkSeconds));
            Color = color;
            _game.PositionChanged += OnPositionChanged;
        }

        /// <summary>Raised when thinking starts or stops, or the message changes.</summary>
        public event Action StateChanged;

        public PieceColor Color { get; }
        public bool IsThinking => _thinking != null;

        /// <summary>Name of the engine currently in use (changes when falling back).</summary>
        public string EngineName => _engine.Name;

        /// <summary>Last problem worth telling the player about; empty if none.</summary>
        public string Message { get; private set; } = string.Empty;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                    return;
                _enabled = value;
                OnPositionChanged();
            }
        }

        public void Dispose()
        {
            _game.PositionChanged -= OnPositionChanged;
            CancelThinking();
        }

        private void OnPositionChanged()
        {
            CancelThinking();
            if (_enabled && !_game.IsGameOver && _game.SideToMove == Color)
                _ = ThinkAsync();
        }

        private void CancelThinking()
        {
            if (_thinking == null)
                return;
            _thinking.Cancel();
            _thinking.Dispose();
            _thinking = null;
            StateChanged?.Invoke();
        }

        private async Task ThinkAsync()
        {
            var thinking = new CancellationTokenSource();
            _thinking = thinking;
            var token = thinking.Token;
            StateChanged?.Invoke();

            var position = EnginePosition.From(_game);
            var clock = Stopwatch.StartNew();
            string uci;
            try
            {
                uci = await AskEngineAsync(position, token);

                var remaining = _minimumThinkTime - clock.Elapsed;
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                // Never let an engine failure escape this fire-and-forget task; report it instead.
                if (_thinking == thinking)
                    Stop($"{_engine.Name} failed: {e.Message}");
                return;
            }

            // Discard answers to a position that is no longer on the board.
            if (_thinking != thinking || token.IsCancellationRequested)
                return;

            if (uci == null || !_game.TryGetUciMove(uci, out var move))
            {
                Stop($"{_engine.Name} returned no legal move ({uci ?? "none"}).");
                return;
            }

            _thinking = null;
            thinking.Dispose();
            StateChanged?.Invoke();
            _game.TryMakeMove(move);
        }

        private async Task<string> AskEngineAsync(EnginePosition position, CancellationToken token)
        {
            try
            {
                return await _engine.GetBestMoveAsync(position, token);
            }
            catch (Exception e) when (!(e is OperationCanceledException) && _fallback != null && _engine != _fallback)
            {
                Message = $"{_engine.Name} unavailable - using {_fallback.Name} opponent. ({e.Message})";
                _engine = _fallback;
                StateChanged?.Invoke();
                return await _engine.GetBestMoveAsync(position, token);
            }
        }

        private void Stop(string message)
        {
            Message = message;
            _thinking?.Dispose();
            _thinking = null;
            StateChanged?.Invoke();
        }
    }
}
