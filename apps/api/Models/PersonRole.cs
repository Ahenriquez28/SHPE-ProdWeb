public class PersonRole
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string Role { get; set; } = "";
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}