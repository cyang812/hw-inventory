namespace HwInventory.Api.Models;

public class Attachment
{
    public int Id { get; set; }

    public int? HardwareId { get; set; }
    public Hardware? Hardware { get; set; }
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public int? ActivityId { get; set; }
    public Activity? Activity { get; set; }

    public AttachmentStorageBackend StorageBackend { get; set; } = AttachmentStorageBackend.Local;
    public string StorageKey { get; set; } = "";

    public string OriginalFilename { get; set; } = "";
    public string MimeType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public string? Label { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
