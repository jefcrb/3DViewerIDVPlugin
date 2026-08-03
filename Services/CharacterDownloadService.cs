using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace neo_bpsys_wpf._3DViewerIDV.Services;

public class CharacterDownloadService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _wwwrootPath;
    private readonly string _catalogPath;
    private CancellationTokenSource? _cancellationTokenSource;

    public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;
    public event EventHandler<DownloadCompletedEventArgs>? DownloadCompleted;

    public CharacterDownloadService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        _wwwrootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "neo-bpsys-wpf", "Plugins", "3DViewerIDV", "wwwroot"
        );

        // Look for catalog in the assembly directory
        var assemblyDir = Path.GetDirectoryName(typeof(CharacterDownloadService).Assembly.Location) ?? string.Empty;
        _catalogPath = Path.Combine(assemblyDir, "characters_catalog.json");
    }

    public async Task<(int Hunters, int Survivors)> GetCatalogTotalsAsync()
    {
        if (!File.Exists(_catalogPath)) return (0, 0);

        var catalogJson = await File.ReadAllTextAsync(_catalogPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var catalog = JsonSerializer.Deserialize<CharacterCatalog>(catalogJson, options);
        return catalog is null
            ? (0, 0)
            : (catalog.Hunters.Count, catalog.Survivors.Count);
    }

    public async Task<(int Hunters, int Survivors)> GetDownloadedCountAsync()
    {
        return await Task.Run(() =>
        {
            var huntersPath = Path.Combine(_wwwrootPath, "hunters");
            var survivorsPath = Path.Combine(_wwwrootPath, "survivors");

            int huntersCount = 0;
            int survivorsCount = 0;

            if (Directory.Exists(huntersPath))
            {
                huntersCount = Directory.GetDirectories(huntersPath)
                    .Count(dir => File.Exists(Path.Combine(dir, Path.GetFileName(dir) + ".gltf")));
            }

            if (Directory.Exists(survivorsPath))
            {
                survivorsCount = Directory.GetDirectories(survivorsPath)
                    .Count(dir => File.Exists(Path.Combine(dir, Path.GetFileName(dir) + ".gltf")));
            }

            return (huntersCount, survivorsCount);
        });
    }

    public async Task DownloadAllCharactersAsync()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            if (!File.Exists(_catalogPath))
            {
                throw new FileNotFoundException("Character catalog not found", _catalogPath);
            }

            var catalogJson = await File.ReadAllTextAsync(_catalogPath, token);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var catalog = JsonSerializer.Deserialize<CharacterCatalog>(catalogJson, options);

            if (catalog == null)
            {
                throw new InvalidOperationException("Failed to parse character catalog");
            }

            var totalCharacters = catalog.Hunters.Count + catalog.Survivors.Count;
            var downloadedCount = 0;
            var countLock = new object();

            // Create a semaphore to limit concurrent downloads (10 at a time)
            var semaphore = new SemaphoreSlim(10, 10);
            var tasks = new List<Task>();

            // Download hunters in parallel
            foreach (var hunter in catalog.Hunters)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(token);
                    try
                    {
                        if (token.IsCancellationRequested) return;

                        await DownloadCharacterAsync(hunter.Zy, hunter.ModelUrl, "hunters", token);

                        int currentCount;
                        lock (countLock)
                        {
                            currentCount = ++downloadedCount;
                        }

                        ProgressChanged?.Invoke(this, new DownloadProgressEventArgs
                        {
                            CharacterName = hunter.Zy,
                            TotalCount = totalCharacters,
                            DownloadedCount = currentCount,
                            Type = "Hunter"
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to download hunter {hunter.Zy}: {ex.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, token));
            }

            // Download survivors in parallel
            foreach (var survivor in catalog.Survivors)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(token);
                    try
                    {
                        if (token.IsCancellationRequested) return;

                        await DownloadCharacterAsync(survivor.Zy, survivor.ModelUrl, "survivors", token);

                        int currentCount;
                        lock (countLock)
                        {
                            currentCount = ++downloadedCount;
                        }

                        ProgressChanged?.Invoke(this, new DownloadProgressEventArgs
                        {
                            CharacterName = survivor.Zy,
                            TotalCount = totalCharacters,
                            DownloadedCount = currentCount,
                            Type = "Survivor"
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to download survivor {survivor.Zy}: {ex.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, token));
            }

            // Wait for all downloads to complete
            await Task.WhenAll(tasks);

            DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs
            {
                Success = !token.IsCancellationRequested,
                TotalDownloaded = downloadedCount,
                TotalCharacters = totalCharacters
            });
        }
        catch (Exception ex)
        {
            DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs
            {
                Success = false,
                ErrorMessage = ex.Message
            });
            throw;
        }
    }

    private async Task DownloadCharacterAsync(string characterName, string gltfUrl, string type, CancellationToken token)
    {
        // Strip wrapping quotes from the zy field before using it as a folder name.
        // The catalog uses fullwidth curly quotes (U+201C / U+201D) for a handful of
        // entries; previously only the ASCII chars were trimmed, so folders ended up
        // named "“骑士”" instead of "骑士" and the JS loader 404'd on selection.
        var cleanName = characterName.Trim(
            '"', '\'',
            '“', '”', // “ ”
            '‘', '’'  // ‘ ’
        );
        var characterDir = Path.Combine(_wwwrootPath, type, cleanName);
        Directory.CreateDirectory(characterDir);

        // Download as bytes — some catalog entries (e.g. 逃脱大师) serve a binary GLB
        // at a .gltf URL. GetStringAsync + WriteAllText would mangle the binary into
        // UTF-8 replacement chars and break the file.
        var gltfPath = Path.Combine(characterDir, $"{cleanName}.gltf");
        var rawBytes = await _httpClient.GetByteArrayAsync(gltfUrl, token);

        // GLB magic: bytes 0..3 spell "glTF" (0x67 0x6C 0x54 0x46). Self-contained,
        // no external buffers or images — write raw and we're done. (THREE's
        // GLTFLoader detects the format from the magic bytes, not the extension.)
        if (rawBytes.Length >= 4 && rawBytes[0] == 0x67 && rawBytes[1] == 0x6C
            && rawBytes[2] == 0x54 && rawBytes[3] == 0x46)
        {
            await File.WriteAllBytesAsync(gltfPath, rawBytes, token);
            return;
        }

        // Otherwise: JSON glTF. Decode as UTF-8 and continue to fetch external bin + textures.
        var gltfContent = System.Text.Encoding.UTF8.GetString(rawBytes);
        await File.WriteAllTextAsync(gltfPath, gltfContent, token);

        // Parse GLTF to find resources
        var gltfDoc = JsonDocument.Parse(gltfContent);
        var baseUrl = gltfUrl.Substring(0, gltfUrl.LastIndexOf('/') + 1);

        var downloadTasks = new List<Task>();

        // Download buffers (.bin files)
        if (gltfDoc.RootElement.TryGetProperty("buffers", out var buffers))
        {
            foreach (var buffer in buffers.EnumerateArray())
            {
                if (buffer.TryGetProperty("uri", out var uri))
                {
                    var uriStr = uri.GetString();
                    if (!string.IsNullOrEmpty(uriStr) && !uriStr.StartsWith("data:"))
                    {
                        var resourceUrl = baseUrl + uriStr;
                        var resourcePath = Path.Combine(characterDir, uriStr);
                        downloadTasks.Add(DownloadFileAsync(resourceUrl, resourcePath, token));
                    }
                }
            }
        }

        // Download images (.jpg files)
        if (gltfDoc.RootElement.TryGetProperty("images", out var images))
        {
            foreach (var image in images.EnumerateArray())
            {
                if (image.TryGetProperty("uri", out var uri))
                {
                    var uriStr = uri.GetString();
                    if (!string.IsNullOrEmpty(uriStr) && !uriStr.StartsWith("data:"))
                    {
                        var resourceUrl = baseUrl + uriStr;
                        var resourcePath = Path.Combine(characterDir, uriStr);
                        downloadTasks.Add(DownloadFileAsync(resourceUrl, resourcePath, token));
                    }
                }
            }
        }

        // Wait for all resources to download
        await Task.WhenAll(downloadTasks);
    }

    private async Task DownloadFileAsync(string url, string destinationPath, CancellationToken token)
    {
        var response = await _httpClient.GetAsync(url, token);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsByteArrayAsync(token);
        await File.WriteAllBytesAsync(destinationPath, content, token);
    }

    public void CancelDownload()
    {
        _cancellationTokenSource?.Cancel();
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _httpClient.Dispose();
    }
}

public class CharacterCatalog
{
    public List<CharacterInfo> Hunters { get; set; } = new();
    public List<CharacterInfo> Survivors { get; set; } = new();
}

public class CharacterInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Zy { get; set; } = string.Empty;
    public string ModelUrl { get; set; } = string.Empty;
}

public class DownloadProgressEventArgs : EventArgs
{
    public string CharacterName { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int DownloadedCount { get; set; }
    public string Type { get; set; } = string.Empty;
}

public class DownloadCompletedEventArgs : EventArgs
{
    public bool Success { get; set; }
    public int TotalDownloaded { get; set; }
    public int TotalCharacters { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
