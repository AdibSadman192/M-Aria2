namespace MAria2.Core.Enums;

public enum ProtocolType
{
    // Standard Web Protocols
    HTTP,
    HTTPS,
    
    // File Transfer Protocols
    FTP,
    SFTP,
    
    // Specialized Download Protocols
    Torrent,
    Magnet,
    WebSocket,
    
    // Streaming Protocols
    StreamPlaylist,
    VideoStreaming,
    
    // Specialized Content Types
    Compressed,
    DiskImage,
    
    // Fallback
    Unknown
}
