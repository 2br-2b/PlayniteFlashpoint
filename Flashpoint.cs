using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using static System.Net.Mime.MediaTypeNames;

namespace Flashpoint
{
    public class Flashpoint : LibraryPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        public static string GAMEID_PREFIX = "flashpoint-";

        private FlashpointSettingsViewModel settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("05c6d99c-c8ad-49c5-939a-9010f0d585fb");

        // Change to something more appropriate
        public override string Name => "Flashpoint";

        // Implementing Client adds ability to open it via special menu in playnite.
        public override LibraryClient Client { get; } = new FlashpointClient();

        public Flashpoint(IPlayniteAPI api) : base(api)
        {
            settings = new FlashpointSettingsViewModel(this);
            Properties = new LibraryPluginProperties
            {
                HasSettings = true
            };
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            // Get the list of games from "C:\Flashpoint\Data\flashpoint.sqlite"

            var dbPath = System.IO.Path.Combine(settings.Settings.InstallDirectory, "Data", "flashpoint.sqlite");

            var games = new List<GameMetadata>();

            if(settings.Settings.InstallDirectory == null)
            {
                // Send a playnite notification that the install directory is not set
                logger.Error("Flashpoint install directory is not set.");
                var a = new NotificationMessage("flashpoint-error", "The Flashpoint install directory is not set! Please set it in the Flashpoint plugin settings.", NotificationType.Error);
                PlayniteApi.Notifications.Add(a);

                return new List<GameMetadata>();
            }
            if(settings.Settings.CollectionFilename == null)
            {
                // Send a playnite notification that the collection name is not set
                logger.Error("Flashpoint collection name is not set.");
                var a = new NotificationMessage("flashpoint-error", "The Flashpoint collection name is not set! Please set it in the Flashpoint plugin settings.", NotificationType.Error);
                PlayniteApi.Notifications.Add(a);
                return new List<GameMetadata>();
            }

            string jsonPath = System.IO.Path.Combine(settings.Settings.InstallDirectory, "Data", "Playlists", settings.Settings.CollectionFilename);

            // Verify that the file exists
            if (!System.IO.File.Exists(jsonPath))
            {
                // Send a playnite notification that the collection file does not exist
                logger.Error("Flashpoint collection file does not exist: " + jsonPath);
                var a = new NotificationMessage("flashpoint-error", "The Flashpoint collection specified does not exist! Please check your Flashpoint plugin settings.", NotificationType.Error);
                PlayniteApi.Notifications.Add(a);
                return new List<GameMetadata>();
            }

            try
            {
                string jsonContent = System.IO.File.ReadAllText(jsonPath);
                dynamic playlist = JsonConvert.DeserializeObject(jsonContent);

                // 2. Get the IDs of games in this collection, along with descriptions
                Dictionary<string, string> gameIdsAndNotes = new Dictionary<string, string>();
                foreach (var gameEntry in playlist.games)
                {
                    gameIdsAndNotes.Add((string)gameEntry.gameId, (string)gameEntry.notes);
                    
                    // Only show one game:
                    //break;
                }

                var database = SQLite.OpenDatabase(dbPath, SqliteOpenFlags.ReadOnly);


                // 3. Use SQL to get details for these IDs

                foreach (var gameId in gameIdsAndNotes.Keys)
                {
                    string query = "SELECT id,title,version,originalDescription,lastPlayed,playtime,playCounter,logoPath,screenshotPath FROM game WHERE id = ?";
                         

                    var result = database.Query<FlashpointGame>(query, gameId).Single();
                    //logger.Info("ASUID");
                    //logger.Info(gameId);
                    //logger.Info(result.id);
                    //logger.Info(result.title);

                    //var retrievedGameId = reader.GetString(0);
                    var logoPath = System.IO.Path.Combine(
                        settings.Settings.InstallDirectory,
                        "Data",
                        "Images",
                        result.logoPath
                        );

                    var screenshotPath = System.IO.Path.Combine(
                        settings.Settings.InstallDirectory,
                        "Data",
                        "Images",
                        result.screenshotPath
                        );

                    //logger.Info(imagePath);

                    // Last played date - format is 2026-01-22T12:04:10.702Z
                    DateTime? lastPlayed = result.lastPlayed ==  null? (DateTime?)null : DateTime.Parse(result.lastPlayed);


                    var game = new GameMetadata
                    {
                        Name = result.title,
                        GameId = GAMEID_PREFIX + gameId,
                        Description = gameIdsAndNotes[gameId],
                        IsInstalled = true,
                        BackgroundImage = new MetadataFile(screenshotPath),
                        Icon = new MetadataFile(logoPath),
                        Version = result.version,
                        Playtime = ulong.Parse(result.playtime),
                        PlayCount = ulong.Parse(result.playCounter),
                        LastActivity = lastPlayed
                    };

                    games.Add(game);

                    if(args.CancelToken.IsCancellationRequested)
                    {
                        return games;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to load games from Flashpoint: {ex.Message}");
                var notification = new NotificationMessage("flashpoint-error", $"Failed to load games: {ex.Message}", NotificationType.Error);
                PlayniteApi.Notifications.Add(notification);
            }

            return games;
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new FlashpointSettingsView();
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            logger.Info($"Starting {args.Game.Name} via Flashpoint");


            // Extract the Game UUID. The Guid.Parse will ensure that the format is right (to prevent hacky behavior)
            // Should that be a concern for this project? Heck if I know! But why the heck not, am I right?
            var game_id = Guid.Parse(args.Game.GameId.Substring(GAMEID_PREFIX.Length));

            logger.Info("GameID: " + game_id);


            var flashpoint_directory = settings.Settings.InstallDirectory;
            var CLIFp_path = System.IO.Path.Combine(flashpoint_directory, "CLIFp", "bin", "clifp-c.exe");

            // Verify that CLIFp exists
            if (!System.IO.File.Exists(CLIFp_path))
            {
                logger.Error("CLIFp executable not found at: " + CLIFp_path);
                PlayniteApi.Dialogs.ShowErrorMessage($"Couldn't find the CLIFp executable at: `{CLIFp_path}`. Please make sure you install it!", "Couldn't launch game");
                yield break;
            }

            yield return new ClifpPlayController(args.Game, CLIFp_path);
        }

    }
}