public class Person
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string? GsuEmail { get; set; }
    public string? PhoneNumber { get; set; }
    public string? GradYear { get; set; }
    public string? LinkedinUrl {get; set; }
    public bool SmsOptIn { get; set; }
    public bool ShareContact { get; set; }
    public bool PhotoOptOut { get; set; }
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }



}