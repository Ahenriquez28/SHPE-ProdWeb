public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorPersonId { get; set; }
    public string Action { get; set; } = "";
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}