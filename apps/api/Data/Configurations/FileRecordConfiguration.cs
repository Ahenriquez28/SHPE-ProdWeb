using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// FileRecordConfiguration — maps FileRecord to the file_record table.
//
// All column names are snake_case (Postgres convention per CLAUDE.md rule 2).
// One config file per entity (CLAUDE.md rule 3).
// Soft-delete query filter so deleted files are invisible to all queries (rule 4).
public class FileRecordConfiguration : IEntityTypeConfiguration<FileRecord>
{
    public void Configure(EntityTypeBuilder<FileRecord> b)
    {
        b.ToTable("file_record");
        b.HasKey(f => f.Id);

        b.Property(f => f.Id).HasColumnName("id");
        b.Property(f => f.SpacesKey).HasColumnName("spaces_key").IsRequired();
        b.Property(f => f.CdnUrl).HasColumnName("cdn_url").IsRequired();
        b.Property(f => f.MimeType).HasColumnName("mime_type").IsRequired();
        b.Property(f => f.SizeBytes).HasColumnName("size_bytes");
        b.Property(f => f.UploadedBy).HasColumnName("uploaded_by");
        b.Property(f => f.UploadedAt).HasColumnName("uploaded_at");
        b.Property(f => f.DeletedAt).HasColumnName("deleted_at");

        // Soft-delete filter: db.Files never returns deleted records.
        // To see deleted files (admin audit), use db.Files.IgnoreQueryFilters().
        b.HasQueryFilter(f => f.DeletedAt == null);
    }
}
