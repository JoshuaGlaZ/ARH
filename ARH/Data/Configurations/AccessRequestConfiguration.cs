using ARH.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ARH.Data.Configurations
{

    public class AccessRequestConfiguration
        : IEntityTypeConfiguration<AccessRequest>
    {
        public void Configure(EntityTypeBuilder<AccessRequest> builder)
        {
            builder.ToTable("AccessRequests", table =>
            {
                table.HasCheckConstraint(
                    "CK_AccessRequests_Version",
                    "`Version` >= 1");

                table.HasCheckConstraint(
                    "CK_AccessRequests_Approver",
                    """
                (
                    `Status` IN (
                        'PendingManagerApproval',
                        'PendingSystemOwnerApproval'
                    )
                    AND `CurrentApproverId` IS NOT NULL
                )
                OR
                (
                    `Status` IN ('Approved', 'Rejected')
                    AND `CurrentApproverId` IS NULL
                )
                """);

                table.HasCheckConstraint(
                    "CK_AccessRequests_RejectReason",
                    """
                `Status` <> 'Rejected'
                OR (
                    `RejectReason` IS NOT NULL
                    AND CHAR_LENGTH(TRIM(`RejectReason`)) > 0
                )
                """);

                table.HasCheckConstraint(
                    "CK_AccessRequests_RiskConsistency",
                    """
                (
                    `Environment` = 'Production'
                    OR `AccessLevel` = 'Admin'
                )
                AND `RiskLevel` = 'High'
                OR (
                    `Environment` = 'NonProduction'
                    AND `AccessLevel` = 'Read'
                    AND `RiskLevel` = 'NonHigh'
                )
                """);

                table.HasCheckConstraint(
                    "CK_AccessRequests_NoSelfApprover",
                    """
                `CurrentApproverId` IS NULL
                OR `CurrentApproverId` <> `RequesterId`
                """);
            });

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .HasColumnType("char(36)");

            builder.Property(x => x.ClientRequestId)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.RequestPayloadHash)
                .HasColumnType("char(64)")
                .IsRequired();

            builder.HasIndex(x => new
            {
                x.RequesterId,
                x.ClientRequestId
            })
            .IsUnique()
            .HasDatabaseName("UQ_AccessRequests_Idempotency");

            builder.Property(x => x.RequesterId)
                .HasColumnType("char(36)");

            builder.Property(x => x.ApplicationId)
                .HasColumnType("char(36)");

            builder.Property(x => x.CurrentApproverId)
                .HasColumnType("char(36)");

            builder.Property(x => x.Environment)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(x => x.AccessLevel)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(x => x.RiskLevel)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(40)
                .IsRequired();

            builder.Property(x => x.Justification)
                .HasMaxLength(1000)
                .IsRequired();

            builder.Property(x => x.RejectReason)
                .HasMaxLength(1000);

            builder.Property(x => x.PolicyVersion)
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("v1");

            builder.Property(x => x.Version)
                .HasColumnType("bigint")
                .HasDefaultValue(1L)
                .IsRequired();

            builder.Property(x => x.CreatedAtUtc)
                .HasColumnType("datetime(6)")
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            builder.Property(x => x.UpdatedAtUtc)
                .HasColumnType("datetime(6)")
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            // Requester relationship
            builder.HasOne(x => x.Requester)
                .WithMany()
                .HasForeignKey(x => x.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            // Application relationship
            builder.HasOne(x => x.Application)
                .WithMany(x => x.AccessRequests)
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Current approver relationship
            builder.HasOne(x => x.CurrentApprover)
                .WithMany()
                .HasForeignKey(x => x.CurrentApproverId)
                .OnDelete(DeleteBehavior.Restrict);

            // Inbox index
            builder.HasIndex(x => new
            {
                x.CurrentApproverId,
                x.Status,
                x.CreatedAtUtc
            });

            // Application request index
            builder.HasIndex(x => new
            {
                x.ApplicationId,
                x.Status,
                x.CreatedAtUtc
            });

            // Requester history index
            builder.HasIndex(x => new
            {
                x.RequesterId,
                x.CreatedAtUtc
            });
        }
    }
}