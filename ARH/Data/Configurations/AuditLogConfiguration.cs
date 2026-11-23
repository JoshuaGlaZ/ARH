using ARH.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ARH.Data.Configurations
{

    public class AuditLogConfiguration
        : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("AuditLogs", table =>
            {
                table.HasCheckConstraint(
                    "CK_AuditLogs_Version",
                    "`NewVersion` = `PreviousVersion` + 1");
            });

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .HasColumnType("char(36)");

            builder.Property(x => x.AccessRequestId)
                .HasColumnType("char(36)");

            builder.Property(x => x.ActorId)
                .HasColumnType("char(36)");

            builder.Property(x => x.ActorEmailSnapshot)
                .HasMaxLength(150)
                .IsRequired();

            builder.Property(x => x.Action)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(x => x.FromStatus)
                .HasMaxLength(40);

            builder.Property(x => x.ToStatus)
                .HasMaxLength(40)
                .IsRequired();

            builder.Property(x => x.PolicyVersion)
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.Reason)
                .HasMaxLength(1000);

            builder.Property(x => x.TimestampUtc)
                .HasColumnType("datetime(6)")
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            builder.HasIndex(x => new
            {
                x.AccessRequestId,
                x.NewVersion
            })
            .IsUnique()
            .HasDatabaseName("UQ_AuditLogs_RequestVersion");

            builder.HasIndex(x => new
            {
                x.AccessRequestId,
                x.TimestampUtc
            });

            builder.HasIndex(x => new
            {
                x.ActorId,
                x.TimestampUtc
            });

            builder.HasOne(x => x.AccessRequest)
                .WithMany(x => x.AuditLogs)
                .HasForeignKey(x => x.AccessRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Actor)
                .WithMany()
                .HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}