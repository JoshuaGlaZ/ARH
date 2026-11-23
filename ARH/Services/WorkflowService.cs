using ARH.Domain.Entities;
using ARH.Domain.Enums;

namespace ARH.Services
{

    public class WorkflowService
    {
        // Calculate request risk
        public RiskLevel CalculateRisk(
            EnvironmentType environment,
            AccessLevel accessLevel)
        {
            return environment == EnvironmentType.Production
                || accessLevel == AccessLevel.Admin
                ? RiskLevel.High
                : RiskLevel.NonHigh;
        }

        // Manager approval transition
        public RequestStatus GetAfterManagerApproval(
            AccessRequest request)
        {
            if (request.Status !=
                RequestStatus.PendingManagerApproval)
            {
                throw new InvalidOperationException(
                    "Request is not waiting for Manager approval.");
            }

            return request.RiskLevel == RiskLevel.High
                ? RequestStatus.PendingSystemOwnerApproval
                : RequestStatus.Approved;
        }

        // System Owner approval transition
        public RequestStatus GetAfterSystemOwnerApproval(
            AccessRequest request)
        {
            if (request.Status !=
                RequestStatus.PendingSystemOwnerApproval)
            {
                throw new InvalidOperationException(
                    "Request is not waiting for System Owner approval.");
            }

            return RequestStatus.Approved;
        }

        // Rejection transition
        public RequestStatus GetAfterRejection(
            AccessRequest request)
        {
            if (request.Status != RequestStatus.PendingManagerApproval
                && request.Status != RequestStatus.PendingSystemOwnerApproval)
            {
                throw new InvalidOperationException(
                    "Request cannot be rejected in its current status.");
            }

            return RequestStatus.Rejected;
        }
    }
}