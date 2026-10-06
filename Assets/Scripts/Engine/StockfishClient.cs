using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BciChess.Engine
{
    public sealed class StockfishOptions
    {
        /// <summary>Full path to the Stockfish executable.</summary>
        public string ExecutablePath { get; set; }

        /// <summary>Stockfish "Skill Level", 0 (weakest) to 20 (full strength).</summary>
        public int SkillLevel { get; set; } = 10;

        /// <summary>Thinking time per move, used when <see cref="Depth"/> is 0.</summary>
        public int MoveTimeMs { get; set; } = 500;

        /// <summary>Fixed search depth; 0 = search by <see cref="MoveTimeMs"/> instead.</summary>
        public int Depth { get; set; }

        public int Threads { get; set; } = 1;
        public int HashMb { get; set; } = 16;

        /// <summary>How long to wait for the engine to start and answer "uci"/"isready".</summary>
        public int StartupTimeoutMs { get; set; } = 5000;

        /// <summary>Grace period on top of the expected search time before a move request times out.</summary>
        public int ExtraMoveTimeoutMs { get; set; } = 3000;

        /// <summary>Timeout used for depth-limited searches, whose duration is unknown.</summary>
        public int DepthSearchTimeoutMs { get; set; } = 30000;
    }

    /// <summary>
    /// Runs Stockfish as a child process and talks UCI over stdin/stdout. Process handling stays inside this
    /// class: callers just ask <see cref="GetBestMoveAsync"/>. Requests are serialized; output is read on a
    /// background thread, so nothing here blocks the caller's thread.
    /// </summary>
    public sealed class StockfishClient : IChessEngine
    {
        private readonly StockfishOptions _options;
        private readonly SemaphoreSlim _requestGate = new SemaphoreSlim(1, 1);
        private readonly object _lock = new object();

        private Process _process;
        private StreamWriter _stdin;
        private volatile bool _running;
        private Func<string, bool> _waitMatch;
        private TaskCompletionSource<string> _waitResult;
        private int _staleBestMoves;
        private bool _disposed;

        public StockfishClient(StockfishOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public string Name => "Stockfish";

        /// <summary>True while the engine process is running.</summary>
        public bool IsRunning => _running;

        /// <summary>Raised (on a background thread) for diagnostics such as process exit.</summary>
        public event Action<string> Diagnostic;

        /// <summary>Starts the process and performs the UCI handshake. Called automatically by the first request.</summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _requestGate.Release();
            }
        }

        public async Task<string> GetBestMoveAsync(EnginePosition position, CancellationToken cancellationToken)
        {
            if (position == null)
                throw new ArgumentNullException(nameof(position));

            await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);

                Send(UciProtocol.PositionCommand(position));
                int timeout = (_options.Depth > 0 ? _options.DepthSearchTimeoutMs : _options.MoveTimeMs)
                              + _options.ExtraMoveTimeoutMs;

                string line;
                try
                {
                    line = await SendAndWaitAsync(UciProtocol.GoCommand(_options.MoveTimeMs, _options.Depth),
                        UciProtocol.IsBestMoveLine, timeout, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    AbandonSearch();
                    throw;
                }
                catch (TimeoutException)
                {
                    AbandonSearch();
                    throw new EngineException($"Stockfish did not answer within {timeout} ms.");
                }

                if (!UciProtocol.TryParseBestMove(line, out string move))
                    throw new EngineException($"Unexpected reply from Stockfish: '{line}'.");
                return move;
            }
            finally
            {
                _requestGate.Release();
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }

            var process = _process;
            _process = null;
            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        TryWrite(_stdin, "quit");
                        if (!process.WaitForExit(500))
                            process.Kill();
                    }
                }
                catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
                {
                    // Already gone.
                }
                process.Dispose();
            }

            _running = false;
            FailWaiter(new EngineException("Stockfish client was shut down."));
            _requestGate.Dispose();
        }

        private async Task EnsureStartedAsync(CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StockfishClient));
            if (_running)
                return;

            string path = _options.ExecutablePath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new EngineException($"Stockfish executable not found at '{path}'.");

            _process?.Dispose();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo(path)
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(path) ?? string.Empty
                },
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    OnLine(e.Data.Trim().TrimStart('﻿'));
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Diagnostic?.Invoke("Stockfish stderr: " + e.Data);
            };
            process.Exited += (_, __) => OnExited();

            try
            {
                if (!process.Start())
                    throw new EngineException("Stockfish process did not start.");
            }
            catch (Win32Exception e)
            {
                process.Dispose();
                throw new EngineException($"Could not start Stockfish: {e.Message}", e);
            }

            // When the console uses UTF-8, the runtime's stdin writer emits a byte order mark as soon as the
            // process starts, which would turn the first command into "﻿uci" (unknown to the engine).
            // Write through our own BOM-free writer and start with an empty line that absorbs any BOM already sent.
            _stdin = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false))
            {
                AutoFlush = true,
                NewLine = "\n"
            };
            _process = process;
            _running = true;
            TryWrite(_stdin, string.Empty);
            _staleBestMoves = 0;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await SendAndWaitAsync("uci", l => l == "uciok", _options.StartupTimeoutMs, cancellationToken)
                    .ConfigureAwait(false);
                Send(UciProtocol.SetOption("Threads", Math.Max(1, _options.Threads)));
                Send(UciProtocol.SetOption("Hash", Math.Max(1, _options.HashMb)));
                int skill = Math.Max(UciProtocol.MinSkillLevel, Math.Min(UciProtocol.MaxSkillLevel, _options.SkillLevel));
                Send(UciProtocol.SetOption("Skill Level", skill));
                await SendAndWaitAsync("isready", l => l == "readyok", _options.StartupTimeoutMs, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                KillProcess();
                throw new EngineException("Stockfish did not complete the UCI handshake. Is the path a UCI engine?");
            }
            catch
            {
                KillProcess();
                throw;
            }
        }

        /// <summary>Sends <paramref name="command"/> and waits for the first output line matching <paramref name="match"/>.</summary>
        private async Task<string> SendAndWaitAsync(string command, Func<string, bool> match, int timeoutMs,
            CancellationToken cancellationToken)
        {
            var result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_lock)
            {
                _waitMatch = match;
                _waitResult = result;
            }

            Send(command);

            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var delay = Task.Delay(timeoutMs, timeoutCts.Token);
                var finished = await Task.WhenAny(result.Task, delay).ConfigureAwait(false);
                timeoutCts.Cancel();

                if (finished == result.Task)
                    return await result.Task.ConfigureAwait(false);

                lock (_lock)
                {
                    if (_waitResult == result)
                    {
                        _waitResult = null;
                        _waitMatch = null;
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException();
            }
        }

        /// <summary>Stops a search whose answer we no longer want; its late "bestmove" line will be ignored.</summary>
        private void AbandonSearch()
        {
            lock (_lock)
                _staleBestMoves++;
            try
            {
                Send("stop");
            }
            catch (EngineException)
            {
                // The process is gone; nothing to stop.
            }
        }

        private void OnLine(string line)
        {
            TaskCompletionSource<string> completed = null;
            lock (_lock)
            {
                if (_staleBestMoves > 0 && UciProtocol.IsBestMoveLine(line))
                {
                    _staleBestMoves--;
                    return;
                }
                if (_waitResult != null && _waitMatch(line))
                {
                    completed = _waitResult;
                    _waitResult = null;
                    _waitMatch = null;
                }
            }
            completed?.TrySetResult(line);
        }

        private void OnExited()
        {
            _running = false;
            if (_disposed)
                return;
            Diagnostic?.Invoke("Stockfish process exited.");
            FailWaiter(new EngineException("Stockfish process exited unexpectedly."));
        }

        private void FailWaiter(Exception error)
        {
            TaskCompletionSource<string> waiter;
            lock (_lock)
            {
                waiter = _waitResult;
                _waitResult = null;
                _waitMatch = null;
            }
            waiter?.TrySetException(error);
        }

        private void Send(string command)
        {
            var stdin = _stdin;
            if (!_running || stdin == null)
                throw new EngineException("Stockfish is not running.");
            if (!TryWrite(stdin, command))
            {
                _running = false;
                throw new EngineException("Could not write to Stockfish; the process may have exited.");
            }
        }

        private static bool TryWrite(StreamWriter stdin, string command)
        {
            if (stdin == null)
                return false;
            try
            {
                lock (stdin)
                    stdin.WriteLine(command);
                return true;
            }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is ObjectDisposedException)
            {
                return false;
            }
        }

        private void KillProcess()
        {
            _running = false;
            try
            {
                if (_process != null && !_process.HasExited)
                    _process.Kill();
            }
            catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
            {
                // Already gone.
            }
        }
    }
}
