using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flashpoint
{
    public class ClifpPlayController : PlayController
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly string clifpPath;
        private readonly string clifpDirectory;
        private readonly string clifpFileName;
        private CancellationTokenSource watcherToken;

        public ClifpPlayController(Game game, string clifpPath) : base(game)
        {
            this.clifpPath = clifpPath;
            // Pre-calculate to avoid string manipulation during the game loop
            this.clifpDirectory = Path.GetDirectoryName(clifpPath);
            this.clifpFileName = Path.GetFileNameWithoutExtension(clifpPath);
        }

        public override void Play(PlayActionArgs args)
        {
            logger.Info($"Starting Flashpoint ID: {Game.GameId}");

            var startInfo = new ProcessStartInfo
            {
                FileName = clifpPath,
                Arguments = $"play -i {Game.GameId.Replace(Flashpoint.GAMEID_PREFIX, "")}",
                WorkingDirectory = clifpDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                var proc = Process.Start(startInfo);
                InvokeOnStarted(new GameStartedEventArgs());

                // Start tracking
                watcherToken = new CancellationTokenSource();
                Task.Run(() => WatchGameSession(proc, watcherToken.Token));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to start CLIFp process.");
                InvokeOnStopped(new GameStoppedEventArgs());
            }
        }
        private async Task WatchGameSession(Process launcherProc, CancellationToken token)
        {
            try
            {
                // 1. Wait for the launcher to exit or spawn the game
                // We use a small loop to catch the game process while the launcher is active
                Process gameProc = null;

                while (gameProc == null && !token.IsCancellationRequested)
                {
                    // Try to find the actual game process
                    gameProc = Process.GetProcessesByName(clifpFileName).FirstOrDefault();

                    if (gameProc == null)
                    {
                        // If the launcher closed and we still haven't found a game, it likely failed
                        if (launcherProc.HasExited)
                        {
                            logger.Info("Launcher closed without a detectable game process starting.");
                            break;
                        }
                        await Task.Delay(500, token); // Check every half second during startup
                    }
                }

                if (gameProc != null)
                {
                    using (gameProc) // Ensure handle is closed when we're done
                    {
                        logger.Info($"Pinned tracking to Process ID: {gameProc.Id}");

                        // 2. THE EFFICIENT BIT: 
                        // We no longer call GetProcessesByName. We just check this specific handle.
                        while (!gameProc.HasExited)
                        {
                            if (token.IsCancellationRequested) break;
                            await Task.Delay(1000, token); // Low-frequency heartbeat
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error during session tracking.");
            }
            finally
            {
                launcherProc?.Dispose();
                InvokeOnStopped(new GameStoppedEventArgs());
            }
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }
    }
}