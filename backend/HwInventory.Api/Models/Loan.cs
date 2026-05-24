namespace HwInventory.Api.Models;

public class Loan
{
    public int Id { get; set; }

    public int HardwareId { get; set; }
    public Hardware Hardware { get; set; } = null!;

    public string LoanedTo { get; set; } = "";
    public DateTimeOffset LoanedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? ReturnedAt { get; set; }
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
