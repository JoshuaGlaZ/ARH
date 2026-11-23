
using ARH.Domain.Entities;
using ARH.Domain.Enums;

namespace ARH.DTOs
{

    public class RequestResponseDto
    {
        public Guid Id { get; set; }

        public string ClientRequestId { get; set; } = string.Empty;

        public Guid RequesterId { get; set; }

        public string RequesterName { get; set; } = string.Empty;

        public Guid ApplicationId { get; set; }

        public string ApplicationName { get; set; } = string.Empty;

        public EnvironmentType Environment { get; set; }

        public AccessLevel AccessLevel { get; set; }

        public RiskLevel RiskLevel { get; set; }

        public string Justification { get; set; } = string.Empty;

        public RequestStatus Status { get; set; }

        public Guid? CurrentApproverId { get; set; }

        public string? CurrentApproverName { get; set; }

        public string? RejectReason { get; set; }
            
        public string PolicyVersion { get; set; } = string.Empty;

        public long Version { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime UpdatedAtUtc { get; set; }

        public static RequestResponseDto FromEntity(
            AccessRequest request)
        {
            return new RequestResponseDto
            {
                Id = request.Id,
                ClientRequestId = request.ClientRequestId,

                RequesterId = request.RequesterId,
                RequesterName = request.Requester?.FullName ?? string.Empty,

                ApplicationId = request.ApplicationId,
                ApplicationName = request.Application?.Name ?? string.Empty,

                Environment = request.Environment,
                AccessLevel = request.AccessLevel,
                RiskLevel = request.RiskLevel,

                Justification = request.Justification,

                Status = request.Status,

                CurrentApproverId = request.CurrentApproverId,
                CurrentApproverName = request.CurrentApprover?.FullName,

                RejectReason = request.RejectReason,

                PolicyVersion = request.PolicyVersion,

                Version = request.Version,

                CreatedAtUtc = request.CreatedAtUtc,
                UpdatedAtUtc = request.UpdatedAtUtc
            };
        }
    }
}