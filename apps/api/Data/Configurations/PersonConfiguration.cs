using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> b)
    {
        b.ToTable("person");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("id");
        b.Property(p => p.FullName).HasColumnName("full_name").IsRequired();
        b.Property(p => p.Email).HasColumnName("email");
        b.HasIndex(p => p.Email).IsUnique();
        b.Property(p => p.GsuEmail).HasColumnName("gsu_email");
        b.HasIndex(p => p.GsuEmail).IsUnique();
        b.Property(p => p.PhoneNumber).HasColumnName("phone");
        b.Property(p => p.GradYear).HasColumnName("grad_year");
        b.Property(p => p.LinkedinUrl).HasColumnName("linkedin_url");
        b.Property(p => p.SmsOptIn).HasColumnName("sms_opt_in");
        b.Property(p => p.ShareContact).HasColumnName("share_contact");
        b.Property(p => p.PhotoOptOut).HasColumnName("photo_opt_out");
        b.Property(p => p.Source).HasColumnName("source");
        b.Property(p => p.CreatedAt).HasColumnName("created_at");
        b.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        b.Property(p => p.DeletedAt).HasColumnName("deleted_at");

        b.HasQueryFilter(p => p.DeletedAt == null);
    }
}