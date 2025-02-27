using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Helpers;
using MAria2.Core.Interfaces;
using MAria2.Core.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace MAria2.Application.Services;

public class MediaInformationService : IMediaInformationService
{
    private readonly ILoggingService _loggingService;
    private readonly ConfigurationManager _configurationManager;
    private readonly string _thumbnailCacheDirectory;

    private static readonly HashSet<string> SupportedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm"
    };

    private static readonly HashSet<string> SupportedAudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a"
    };

    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
    };

    public MediaInformationService(
        ILoggingService loggingService, 
        ConfigurationManager configurationManager)
    {
        _loggingService = loggingService;
        _configurationManager = configurationManager;
        
        // Configure thumbnail cache directory
        _thumbnailCacheDirectory = Path.Combine(
            configurationManager.GetApplicationDataPath(), 
            "ThumbnailCache"
        );
        Directory.CreateDirectory(_thumbnailCacheDirectory);
    }

    public bool IsSupportedMediaType(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        var extension = Path.GetExtension(filePath);
        return SupportedVideoExtensions.Contains(extension) ||
               SupportedAudioExtensions.Contains(extension) ||
               SupportedImageExtensions.Contains(extension);
    }

    public async Task<MediaMetadata> ExtractMetadataAsync(string filePath)
    {
        try 
        {
            var mediaType = DetermineMediaType(filePath);
            var metadata = new MediaMetadata 
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                FileSize = new FileInfo(filePath).Length,
                Type = mediaType,
                CreationDate = File.GetCreationTime(filePath)
            };

            switch (mediaType)
            {
                case MediaType.Video:
                case MediaType.Audio:
                    await ExtractMediaFileMetadata(filePath, metadata);
                    break;
                case MediaType.Image:
                    await ExtractImageMetadata(filePath, metadata);
                    break;
            }

            return metadata;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Metadata extraction failed: {ex.Message}");
            return new MediaMetadata { FilePath = filePath };
        }
    }

    private MediaType DetermineMediaType(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        
        if (SupportedVideoExtensions.Contains(extension))
            return MediaType.Video;
        
        if (SupportedAudioExtensions.Contains(extension))
            return MediaType.Audio;
        
        if (SupportedImageExtensions.Contains(extension))
            return MediaType.Image;
        
        return MediaType.Unknown;
    }

    private async Task ExtractMediaFileMetadata(string filePath, MediaMetadata metadata)
    {
        var mediaInfo = await FFProbe.AnalyseAsync(filePath);
        
        metadata.Duration = mediaInfo.Duration;
        metadata.Title = mediaInfo.PrimaryVideoStream?.CodecName ?? 
                         mediaInfo.PrimaryAudioStream?.CodecName ?? 
                         "Unknown";
        
        // Extract additional tags if available
        var format = mediaInfo.Format;
        if (format?.Tags != null)
        {
            foreach (var tag in format.Tags)
            {
                metadata.Tags[tag.Key] = tag.Value;
            }
        }

        // Generate thumbnail
        metadata.ThumbnailPath = await GenerateMediaThumbnail(filePath);
    }

    private async Task ExtractImageMetadata(string filePath, MediaMetadata metadata)
    {
        using var image = await Image.LoadAsync(filePath);
        
        metadata.Title = Path.GetFileNameWithoutExtension(filePath);
        metadata.Tags["Width"] = image.Width.ToString();
        metadata.Tags["Height"] = image.Height.ToString();
        metadata.ThumbnailPath = await GenerateImageThumbnail(filePath);
    }

    public async Task<List<string>> GeneratePreviewThumbnailsAsync(
        string filePath, 
        int thumbnailCount = 5)
    {
        if (!IsSupportedMediaType(filePath))
            return new List<string>();

        var mediaType = DetermineMediaType(filePath);
        
        return mediaType switch
        {
            MediaType.Video => await GenerateVideoPreviewThumbnails(filePath, thumbnailCount),
            MediaType.Audio => await GenerateAudioPreviewThumbnails(filePath),
            MediaType.Image => new List<string> { await GenerateImageThumbnail(filePath) },
            _ => new List<string>()
        };
    }

    private async Task<List<string>> GenerateVideoPreviewThumbnails(
        string filePath, 
        int thumbnailCount)
    {
        var thumbnails = new List<string>();
        var mediaInfo = await FFProbe.AnalyseAsync(filePath);
        var duration = mediaInfo.Duration;

        for (int i = 1; i <= thumbnailCount; i++)
        {
            var timestamp = TimeSpan.FromSeconds(
                duration.TotalSeconds * (i / (double)(thumbnailCount + 1))
            );

            var thumbnailPath = Path.Combine(
                _thumbnailCacheDirectory, 
                $"{Path.GetFileNameWithoutExtension(filePath)}_thumb_{i}.jpg"
            );

            await FFMpegArguments
                .FromFileInput(filePath, false, options => options
                    .Seek(timestamp))
                .OutputToFile(thumbnailPath, false, options => options
                    .WithVideoCodec("mjpeg")
                    .Resize(320, 180)
                    .WithFrameOutputCount(1))
                .ProcessAsynchronously();

            thumbnails.Add(thumbnailPath);
        }

        return thumbnails;
    }

    private async Task<List<string>> GenerateAudioPreviewThumbnails(string filePath)
    {
        // Generate a waveform image for audio files
        var thumbnailPath = Path.Combine(
            _thumbnailCacheDirectory, 
            $"{Path.GetFileNameWithoutExtension(filePath)}_waveform.png"
        );

        await FFMpegArguments
            .FromFileInput(filePath)
            .OutputToFile(thumbnailPath, false, options => options
                .WithVideoFilters(
                    videoFilters => videoFilters
                        .DrawWaveform()
                        .Resize(640, 240)
                )
                .WithFrameOutputCount(1))
            .ProcessAsynchronously();

        return new List<string> { thumbnailPath };
    }

    private async Task<string> GenerateMediaThumbnail(string filePath)
    {
        var thumbnailPath = Path.Combine(
            _thumbnailCacheDirectory, 
            $"{Path.GetFileNameWithoutExtension(filePath)}_thumbnail.jpg"
        );

        await FFMpegArguments
            .FromFileInput(filePath, false, options => options
                .Seek(TimeSpan.FromSeconds(1)))
            .OutputToFile(thumbnailPath, false, options => options
                .WithVideoCodec("mjpeg")
                .Resize(320, 180)
                .WithFrameOutputCount(1))
            .ProcessAsynchronously();

        return thumbnailPath;
    }

    private async Task<string> GenerateImageThumbnail(string filePath)
    {
        var thumbnailPath = Path.Combine(
            _thumbnailCacheDirectory, 
            $"{Path.GetFileNameWithoutExtension(filePath)}_thumbnail.jpg"
        );

        using var image = await Image.LoadAsync(filePath);
        image.Mutate(x => x.Resize(new ResizeOptions 
        { 
            Size = new Size(320, 180), 
            Mode = ResizeMode.Max 
        }));
        await image.SaveAsync(thumbnailPath);

        return thumbnailPath;
    }

    public async Task<MediaStreamInfo> AnalyzeStreamInfoAsync(string filePath)
    {
        try 
        {
            var mediaInfo = await FFProbe.AnalyseAsync(filePath);
            
            return new MediaStreamInfo
            {
                VideoCodec = mediaInfo.PrimaryVideoStream?.CodecName,
                AudioCodec = mediaInfo.PrimaryAudioStream?.CodecName,
                VideoWidth = mediaInfo.PrimaryVideoStream?.Width ?? 0,
                VideoHeight = mediaInfo.PrimaryVideoStream?.Height ?? 0,
                VideoFrameRate = mediaInfo.PrimaryVideoStream?.FrameRate ?? 0,
                AudioChannels = mediaInfo.PrimaryAudioStream?.Channels ?? 0,
                AudioSampleRate = mediaInfo.PrimaryAudioStream?.SampleRate ?? 0,
                SubtitleLanguages = mediaInfo.SubtitleStreams
                    ?.Select(s => s.Language)
                    .ToList() ?? new List<string>()
            };
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Stream analysis failed: {ex.Message}");
            return new MediaStreamInfo();
        }
    }
}
