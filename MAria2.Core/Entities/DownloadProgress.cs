namespace MAria2.Core.Entities;

public class DownloadProgress
{
    public long TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public double ProgressPercentage => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100 : 0;
    public TimeSpan ElapsedTime { get; set; }
    public double SpeedBps { get; set; }
}
