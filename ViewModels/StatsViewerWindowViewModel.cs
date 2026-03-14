using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace neo_bpsys_wpf._3DViewerIDV.ViewModels;

public partial class StatsViewerWindowViewModel : ViewModelBase
{
    private readonly ISharedDataService _sharedDataService;

    private string _hunterDataJson = "{}";
    public string HunterDataJson
    {
        get => _hunterDataJson;
        set => SetProperty(ref _hunterDataJson, value);
    }

    public StatsViewerWindowViewModel(ISharedDataService sharedDataService)
    {
        _sharedDataService = sharedDataService;

        _sharedDataService.CurrentGameChanged += OnCurrentGameChanged;

        SubscribeToHunterChanges();

        UpdateHtmlContent();
    }

    private void OnCurrentGameChanged(object? sender, EventArgs e)
    {
        SubscribeToHunterChanges();
        UpdateHtmlContent();
    }

    private void SubscribeToHunterChanges()
    {
        var hunPlayer = _sharedDataService.CurrentGame?.HunPlayer;
        if (hunPlayer != null)
        {
            hunPlayer.PropertyChanged -= OnPlayerPropertyChanged;
            hunPlayer.PropertyChanged += OnPlayerPropertyChanged;
        }

        var surPlayers = _sharedDataService.CurrentGame?.SurPlayerList;
        if (surPlayers != null)
        {
            foreach (var player in surPlayers)
            {
                player.PropertyChanged -= OnPlayerPropertyChanged;
                player.PropertyChanged += OnPlayerPropertyChanged;
            }
        }
    }

    private void OnPlayerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Character")
        {
            UpdateHtmlContent();
        }
    }

    private string GetFolderNameFromImageFileName(string imageFileName)
    {
        if (string.IsNullOrEmpty(imageFileName))
        {
            return string.Empty;
        }

        return imageFileName.Replace(".png", "");
    }

    private void UpdateHtmlContent()
    {
        System.Diagnostics.Debug.WriteLine("[ViewModel] UpdateHtmlContent called");
        var hunterCharacter = _sharedDataService.CurrentGame?.HunPlayer?.Character;

        string hunterName = "No Hunter Selected";
        string hunterModelPath = "";
        string hunterModelFile = "";

        if (hunterCharacter != null && !string.IsNullOrEmpty(hunterCharacter.Name))
        {
            hunterName = hunterCharacter.Name;
            var folderName = GetFolderNameFromImageFileName(hunterCharacter.ImageFileName);

            var pluginFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "neo-bpsys-wpf", "Plugins", "3DViewerIDV", "wwwroot", "hunters", folderName
            );

            if (Directory.Exists(pluginFolder))
            {
                var modelFilePath = Path.Combine(pluginFolder, $"{folderName}.gltf");
                if (File.Exists(modelFilePath))
                {
                    hunterModelPath = $"/hunters/{folderName}/";
                    hunterModelFile = $"{folderName}.gltf";
                }
            }
        }

        var survivors = new List<object>();
        var surPlayers = _sharedDataService.CurrentGame?.SurPlayerList;

        if (surPlayers != null)
        {
            for (int i = 0; i < 4; i++)
            {
                if (i < surPlayers.Count && surPlayers[i].Character != null && !string.IsNullOrEmpty(surPlayers[i].Character?.Name))
                {
                    var survivorChar = surPlayers[i].Character!;
                    var folderName = GetFolderNameFromImageFileName(survivorChar.ImageFileName);

                    var pluginFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "neo-bpsys-wpf", "Plugins", "3DViewerIDV", "wwwroot", "survivors", folderName
                    );

                    if (Directory.Exists(pluginFolder))
                    {
                        var modelFilePath = Path.Combine(pluginFolder, $"{folderName}.gltf");
                        if (File.Exists(modelFilePath))
                        {
                            survivors.Add(new
                            {
                                name = survivorChar.Name,
                                modelPath = $"/survivors/{folderName}/",
                                modelFile = $"{folderName}.gltf",
                                hasModel = true
                            });
                            continue;
                        }
                    }
                }

                survivors.Add(new
                {
                    name = "",
                    modelPath = "",
                    modelFile = "",
                    hasModel = false
                });
            }
        }

        var gameData = new
        {
            hunter = new
            {
                name = hunterName,
                modelPath = hunterModelPath,
                modelFile = hunterModelFile,
                hasModel = hunterCharacter != null && !string.IsNullOrEmpty(hunterModelPath)
            },
            survivors = survivors
        };

        HunterDataJson = JsonSerializer.Serialize(gameData, new JsonSerializerOptions
        {
            WriteIndented = false
        });
        System.Diagnostics.Debug.WriteLine($"[ViewModel] HunterDataJson updated: {HunterDataJson}");
    }
}
