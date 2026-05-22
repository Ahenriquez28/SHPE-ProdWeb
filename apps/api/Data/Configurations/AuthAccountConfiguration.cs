using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AuthAccountConfiguration : IEntityTypeConfiguration<AuthAccount>
{
    public void Configure(EntityTypeBuilder<AuthAccount> b)
    {
        b.ToTable("auth_account");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("id");
        b.Property(a => a.PersonId).HasColumnName("person_id");
        b.Property(a => a.Provider).HasColumnName("provider").IsRequired();
        b.Property(a => a.ProviderUserId).HasColumnName("provider_user_id").IsRequired();
        b.Property(a => a.EmailVerified).HasColumnName("email_verified");
        b.Property(a => a.CreatedAt).HasColumnName("created_at");

        b.HasIndex(a => new { a.Provider, a.ProviderUserId }).IsUnique();
    }
}