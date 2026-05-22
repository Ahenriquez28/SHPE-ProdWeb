public class AuthAccount
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string Provider { get; set; } = "clerk";
    public string ProviderUserId { get; set; } = "";
    public bool EmailVerified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}