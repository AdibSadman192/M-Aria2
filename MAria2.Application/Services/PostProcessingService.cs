using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FFMpegCore;
using MAria2.Core.Entities;
using MAria2.Core.Interfaces;
using MAria2.Core.Configuration;
using TagLib;

namespace MAria2.Application.Services;

public class PostProcessingService : IPostProcessingService
{
    private readonly ILoggingService _loggingService;
    private readonly ConfigurationManager _configurationManager;
    private readonly ConcurrentDictionary<Guid, PostProcessingTask> _processingTasks = 
        new ConcurrentDictionary<Guid, PostProcessingTask>();

    private static readonly Dictionary<string, List<PostProcessingType>> 
        SupportedProcessingTypes = new Dictionary<string, List<PostProcessingType>>
    {
        { ".mp4", new List<PostProcessingType> 
            { 
                PostProcessingType.FormatConversion, 
                PostProcessingType.VideoTrimming, 
                PostProcessingType.Thumbnail 
            }
        },
        { ".mkv", new List<PostProcessingType> 
            { 
                PostProcessingType.FormatConversion, 
                PostProcessingType.VideoTrimming, 
                PostProcessingType.Subtitles 
            }
        },
        { ".mp3", new List<PostProcessingType> 
            { 
                PostProcessingType.FormatConversion, 
                PostProcessingType.AudioExtraction, 
                PostProcessingType.Normalization 
            }
        },
        { ".wav", new List<PostProcessingType> 
            { 
                PostProcessingType.FormatConversion, 
                PostProcessingType.Normalization 
            }
        }
    };

    public PostProcessingService(
        ILoggingService loggingService,
        ConfigurationManager configurationManager)
    {
        _loggingService = loggingService;
        _configurationManager = configurationManager;
    }

    public async Task<PostProcessingTask> CreateTaskAsync(
        Download sourceDownload, 
        PostProcessingType type, 
        PostProcessingConfig config = null)
    {
        var task = new PostProcessingTask
        {
            SourceDownload = sourceDownload,
            Type = type,
            Configuration = config ?? new PostProcessingConfig()
        };

        _processingTasks[task.Id] = task;
        return task;
    }

    public async Task<PostProcessingTask> ExecuteTaskAsync(Guid taskId)
    {
        if (!_processingTasks.TryGetValue(taskId, out var task))
            throw new KeyNotFoundException($"Task {taskId} not found");

        try 
        {
            task.Status = PostProcessingStatus.Processing;

            task.ResultFilePath = await ProcessFileAsync(
                task.SourceDownload.DestinationPath, 
                task.Type, 
                task.Configuration
            );

            task.Status = PostProcessingStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Post-processing failed: {ex.Message}");
            task.Status = PostProcessingStatus.Failed;
            task.ErrorMessage = ex.Message;
        }

        return task;
    }

    public async Task<List<PostProcessingTask>> ExecuteTasksAsync(List<Guid> taskIds)
    {
        var tasks = taskIds
            .Select(async id => await ExecuteTaskAsync(id))
            .ToList();

        return await Task.WhenAll(tasks);
    }

    public async Task<List<PostProcessingTask>> GetPendingTasksAsync()
    {
        return _processingTasks.Values
            .Where(t => t.Status == PostProcessingStatus.Pending)
            .ToList();
    }

    public async Task CancelTaskAsync(Guid taskId)
    {
        if (_processingTasks.TryGetValue(taskId, out var task))
        {
            task.Status = PostProcessingStatus.Cancelled;
        }
    }

    public async Task<MetadataExtractionTask> ExtractMetadataAsync(string filePath)
    {
        var task = new MetadataExtractionTask 
        { 
            SourceFilePath = filePath,
            Status = MetadataExtractionStatus.Extracting
        };

        try 
        {
            using var file = TagLib.File.Create(filePath);
            
            task.ExtractedMetadata = new Dictionary<string, string>
            {
                ["Title"] = file.Tag.Title ?? "Unknown",
                ["Artist"] = file.Tag.FirstPerformer ?? "Unknown",
                ["Album"] = file.Tag.Album ?? "Unknown",
                ["Year"] = file.Tag.Year.ToString(),
                ["Genre"] = file.Tag.FirstGenre ?? "Unknown",
                ["Duration"] = file.Properties.Duration.ToString(@"hh\:mm\:ss")
            };

            task.Status = MetadataExtractionStatus.Completed;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Metadata extraction failed: {ex.Message}");
            task.Status = MetadataExtractionStatus.Failed;
        }

        return task;
    }

    public async Task<List<PostProcessingType>> GetSupportedProcessingTypesAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return SupportedProcessingTypes.ContainsKey(extension)
            ? SupportedProcessingTypes[extension]
            : new List<PostProcessingType>();
    }

    public async Task<PostProcessingTask> AutoProcessAsync(Download sourceDownload)
    {
        var supportedTypes = await GetSupportedProcessingTypesAsync(
            sourceDownload.DestinationPath
        );

        // Intelligent processing selection
        var processingType = supportedTypes.FirstOrDefault();
        
        if (processingType == PostProcessingType.None)
            return null;

        var config = DetermineOptimalConfig(sourceDownload, processingType);

        return await CreateTaskAsync(sourceDownload, processingType, config);
    }

    private async Task<string> ProcessFileAsync(
        string sourcePath, 
        PostProcessingType type, 
        PostProcessingConfig config)
    {
        var extension = Path.GetExtension(sourcePath);
        var outputPath = GenerateOutputPath(sourcePath, config);

        switch (type)
        {
            case PostProcessingType.FormatConversion:
                return await ConvertFileAsync(sourcePath, outputPath, config);
            
            case PostProcessingType.VideoTrimming:
                return await TrimVideoAsync(sourcePath, outputPath, config);
            
            case PostProcessingType.AudioExtraction:
                return await ExtractAudioAsync(sourcePath, outputPath, config);
            
            case PostProcessingType.Thumbnail:
                return await GenerateThumbnailAsync(sourcePath, outputPath, config);
            
            case PostProcessingType.Normalization:
                return await NormalizeAudioAsync(sourcePath, outputPath, config);
            
            default:
                return sourcePath;
        }
    }

    private async Task<string> ConvertFileAsync(
        string sourcePath, 
        string outputPath, 
        PostProcessingConfig config)
    {
        var targetFormat = config.TargetFormat ?? 
            Path.GetExtension(outputPath).TrimStart('.');

        await FFMpegArguments
            .FromFileInput(sourcePath)
            .OutputToFile(outputPath, false, options => 
            {
                if (!string.IsNullOrEmpty(config.Codec))
                    options.WithVideoCodec(config.Codec);
                
                if (!string.IsNullOrEmpty(config.Resolution))
                    options.Resize(config.Resolution);
                
                if (!string.IsNullOrEmpty(config.Bitrate))
                    options.WithVideoBitrate(config.Bitrate);
            })
            .ProcessAsynchronously();

        return outputPath;
    }

    private async Task<string> TrimVideoAsync(
        string sourcePath, 
        string outputPath, 
        PostProcessingConfig config)
    {
        var startTime = config.AdditionalOptions.TryGetValue("StartTime", out var start) 
            ? TimeSpan.Parse(start) 
            : TimeSpan.Zero;

        var duration = config.AdditionalOptions.TryGetValue("Duration", out var dur)
            ? TimeSpan.Parse(dur)
            : null;

        await FFMpegArguments
            .FromFileInput(sourcePath, false, options => options
                .Seek(startTime))
            .OutputToFile(outputPath, false, options => 
            {
                if (duration.HasValue)
                    options.WithDuration(duration.Value);
            })
            .ProcessAsynchronously();

        return outputPath;
    }

    private async Task<string> ExtractAudioAsync(
        string sourcePath, 
        string outputPath, 
        PostProcessingConfig config)
    {
        await FFMpegArguments
            .FromFileInput(sourcePath)
            .OutputToFile(outputPath, false, options => 
            {
                options.ExtractAudio();
                
                if (config.AudioSampleRate.HasValue)
                    options.WithAudioSampleRate(config.AudioSampleRate.Value);
            })
            .ProcessAsynchronously();

        return outputPath;
    }

    private async Task<string> GenerateThumbnailAsync(
        string sourcePath, 
        string outputPath, 
        PostProcessingConfig config)
    {
        await FFMpegArguments
            .FromFileInput(sourcePath, false, options => options
                .Seek(TimeSpan.FromSeconds(5)))
            .OutputToFile(outputPath, false, options => options
                .Resize(320, 180)
                .WithFrameOutputCount(1))
            .ProcessAsynchronously();

        return outputPath;
    }

    private async Task<string> NormalizeAudioAsync(
        string sourcePath, 
        string outputPath, 
        PostProcessingConfig config)
    {
        await FFMpegArguments
            .FromFileInput(sourcePath)
            .OutputToFile(outputPath, false, options => 
            {
                options.WithAudioFilters(audioFilters => 
                    audioFilters.Loudnorm());
            })
            .ProcessAsynchronously();

        return outputPath;
    }

    private string GenerateOutputPath(
        string sourcePath, 
        PostProcessingConfig config)
    {
        var directory = Path.GetDirectoryName(sourcePath);
        var fileName = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = config.TargetFormat ?? Path.GetExtension(sourcePath);

        return Path.Combine(
            directory, 
            $"{fileName}_processed{extension}"
        );
    }

    private PostProcessingConfig DetermineOptimalConfig(
        Download download, 
        PostProcessingType processingType)
    {
        // Intelligent configuration based on file type and processing needs
        return new PostProcessingConfig
        {
            Quality = "medium",
            TargetFormat = processingType switch
            {
                PostProcessingType.FormatConversion => "mp4",
                PostProcessingType.AudioExtraction => "mp3",
                _ => null
            }
        };
    }
}
