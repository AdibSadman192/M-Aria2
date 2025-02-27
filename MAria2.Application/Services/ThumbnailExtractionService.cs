using System;
using System.IO;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using FFMpegCore;
using MAria2.Core.Interfaces;
using MAria2.Core.Entities;

namespace MAria2.Application.Services;

public class ThumbnailExtractionService
{
    private readonly ILoggingService _loggingService;
    private readonly string _thumbnailCacheDirectory;

    public ThumbnailExtractionService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
        _thumbnailCacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "MAria2", 
            "Thumbnails"
        );

        // Ensure thumbnail cache directory exists
        Directory.CreateDirectory(_thumbnailCacheDirectory);
    }

    public async Task<string> ExtractThumbnailAsync(Download download, int? width = null, int? height = null)
    {
        try 
        {
            // Validate download
            if (string.IsNullOrEmpty(download.DestinationPath) || !File.Exists(download.DestinationPath))
            {
                throw new FileNotFoundException("Download file not found");
            }

            // Determine media type
            var mediaType = Path.GetExtension(download.DestinationPath).ToLowerInvariant();
            
            // Generate unique thumbnail filename
            var thumbnailFilename = $"{Path.GetFileNameWithoutExtension(download.DestinationPath)}_thumb.jpg";
            var thumbnailPath = Path.Combine(_thumbnailCacheDirectory, thumbnailFilename);

            // Check if thumbnail already exists
            if (File.Exists(thumbnailPath))
            {
                return thumbnailPath;
            }

            // Extract thumbnail based on media type
            switch (mediaType)
            {
                case ".mp4":
                case ".avi":
                case ".mkv":
                case ".mov":
                    return await ExtractVideoThumbnailAsync(download.DestinationPath, thumbnailPath, width, height);

                case ".mp3":
                case ".wav":
                case ".flac":
                    return await ExtractAudioThumbnailAsync(download.DestinationPath, thumbnailPath);

                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                    return await ExtractImageThumbnailAsync(download.DestinationPath, thumbnailPath, width, height);

                default:
                    throw new NotSupportedException($"Thumbnail extraction not supported for {mediaType}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Thumbnail extraction failed: {ex.Message}");
            return null;
        }
    }

    private async Task<string> ExtractVideoThumbnailAsync(
        string videoPath, 
        string thumbnailPath, 
        int? width = null, 
        int? height = null)
    {
        try 
        {
            // Use FFMpegCore to extract video thumbnail
            var mediaInfo = FFProbe.Analyse(videoPath);
            var duration = mediaInfo.Duration;
            var extractTime = duration.TotalSeconds / 2; // Middle of the video

            await FFMpegArguments
                .FromFileInput(videoPath, false, options => options
                    .Seek(TimeSpan.FromSeconds(extractTime)))
                .OutputToFile(thumbnailPath, false, options => options
                    .WithVideoCodec("mjpeg")
                    .WithFrameOutputCount(1)
                    .Scale(width ?? 320, height ?? 240))
                .ProcessAsynchronously();

            return thumbnailPath;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Video thumbnail extraction failed: {ex.Message}");
            return null;
        }
    }

    private async Task<string> ExtractAudioThumbnailAsync(string audioPath, string thumbnailPath)
    {
        try 
        {
            // Create a default audio thumbnail
            using var bitmap = new Bitmap(320, 240);
            using var graphics = Graphics.FromImage(bitmap);
            
            // Fill background
            graphics.Clear(Color.DarkSlateBlue);
            
            // Draw audio wave-like pattern
            using var pen = new Pen(Color.White, 2);
            for (int i = 0; i < bitmap.Width; i += 10)
            {
                int height = new Random().Next(bitmap.Height / 4, bitmap.Height * 3 / 4);
                graphics.DrawLine(pen, i, bitmap.Height / 2, i, height);
            }

            // Save thumbnail
            bitmap.Save(thumbnailPath, ImageFormat.Jpeg);
            return thumbnailPath;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Audio thumbnail generation failed: {ex.Message}");
            return null;
        }
    }

    private async Task<string> ExtractImageThumbnailAsync(
        string imagePath, 
        string thumbnailPath, 
        int? width = null, 
        int? height = null)
    {
        try 
        {
            using var originalImage = Image.FromFile(imagePath);
            
            // Calculate resize dimensions
            int targetWidth = width ?? 320;
            int targetHeight = height ?? 240;
            
            using var resizedImage = new Bitmap(targetWidth, targetHeight);
            using var graphics = Graphics.FromImage(resizedImage);
            
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(originalImage, 0, 0, targetWidth, targetHeight);
            
            resizedImage.Save(thumbnailPath, ImageFormat.Jpeg);
            
            return thumbnailPath;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Image thumbnail extraction failed: {ex.Message}");
            return null;
        }
    }

    public async Task<string> GetCachedThumbnailAsync(string originalFilePath)
    {
        var thumbnailFilename = $"{Path.GetFileNameWithoutExtension(originalFilePath)}_thumb.jpg";
        var thumbnailPath = Path.Combine(_thumbnailCacheDirectory, thumbnailFilename);

        return File.Exists(thumbnailPath) ? thumbnailPath : null;
    }

    public void ClearThumbnailCache(TimeSpan? olderThan = null)
    {
        try 
        {
            olderThan ??= TimeSpan.FromDays(30);
            var cutoffTime = DateTime.UtcNow.Subtract(olderThan.Value);

            var thumbnailFiles = Directory.GetFiles(_thumbnailCacheDirectory, "*.jpg");
            foreach (var file in thumbnailFiles)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastAccessTime < cutoffTime)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Thumbnail cache cleanup failed: {ex.Message}");
        }
    }
}
