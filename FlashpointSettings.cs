using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flashpoint
{
    public class FlashpointSettings : ObservableObject
    {
        private string installDirectory = "C:\\Flashpoint";
        private string collectionName = string.Empty;
        public string collectionFilename = null;

        public string InstallDirectory { get => installDirectory; set => SetValue(ref installDirectory, value); }
        public string CollectionName { get => collectionName; set => SetValue(ref collectionName, value); }
        public string CollectionFilename { get => collectionFilename; set => SetValue(ref collectionFilename, value); }
    }

    public class FlashpointSettingsViewModel : ObservableObject, ISettings
    {
        private readonly Flashpoint plugin;
        private FlashpointSettings editingClone { get; set; }

        private FlashpointSettings settings;
        public FlashpointSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public FlashpointSettingsViewModel(Flashpoint plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<FlashpointSettings>();

            // LoadPluginSettings returns null if no saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new FlashpointSettings();
            }
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to InstallDirectory.
            Settings = editingClone;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to InstallDirectory.
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.


            // Verify that the install directory exists and is valid
            errors = new List<string>();

            if (!System.IO.Directory.Exists(settings.InstallDirectory))
            {
                errors.Add("The specified install directory does not exist.");
            }


            if (editingClone.CollectionName != settings.CollectionName)
            {
                if (settings.CollectionName != null && settings.CollectionName.Length > 0)
                {
                    string filename = null;

                    // Get all the playlist files in the Playlists directory
                    var playlistsDir = System.IO.Path.Combine(settings.InstallDirectory, "Data", "Playlists");
                    var playlistFiles = System.IO.Directory.GetFiles(playlistsDir, "*.json");

                    // For each file, open and check if the name matches the CollectionName
                    foreach (var file in playlistFiles)
                    {
                        var content = System.IO.File.ReadAllText(file);
                        dynamic playlist = Newtonsoft.Json.JsonConvert.DeserializeObject(content);
                        if (playlist.title == settings.CollectionName)
                        {
                            filename = System.IO.Path.GetFileName(file);
                            break;
                        }
                    }

                    if (filename != null)
                    {
                        settings.collectionFilename = filename;
                    }
                    else
                    {
                        errors.Add("The specified collection name does not exist in the Flashpoint installation.");
                    }
                }
                else
                {
                    settings.collectionFilename = null;
                }

            }

            return errors.Count == 0;
        }
    }
}