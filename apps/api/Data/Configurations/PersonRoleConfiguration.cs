using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class PersonRoleConfiguration : IEntityTypeConfiguration<PersonRole>
{
    public void Configure(EntityTypeBuilder<PersonRole> b)
    {
        b.ToTable("person_role");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id");
        b.Property(r => r.PersonId).HasColumnName("person_id");
        b.Property(r => r.Role).HasColumnName("role").IsRequired();
        b.Property(r => r.AssignedAt).HasColumnName("assigned_at");

        b.HasIndex(r => new { r.PersonId, r.Role }).IsUnique();

    }
}