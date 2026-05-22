using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_log");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("id");
        b.Property(a => a.ActorPersonId).HasColumnName("actor_person_id");
        b.Property(a => a.Action).HasColumnName("action").IsRequired();
        b.Property(a => a.TargetType).HasColumnName("target_type");
        b.Property(a => a.TargetId).HasColumnName("target_id");
        b.Property(a => a.Details).HasColumnName("details");
        b.Property(a => a.IpAddress).HasColumnName("ip_address");
        b.Property(a => a.UserAgent).HasColumnName("user_agent");
        b.Property(a => a.At).HasColumnName("at");
    }
}