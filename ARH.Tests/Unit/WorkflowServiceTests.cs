using ARH.Domain.Entities;
using ARH.Domain.Enums;
using ARH.Services;
using System;

namespace ARH.Tests;

public class WorkflowServiceTests
{
    private readonly WorkflowService _workflow = new();

    [Theory]
    [InlineData(
        EnvironmentType.NonProduction,
        AccessLevel.Read,
        RiskLevel.NonHigh)]
    [InlineData(
        EnvironmentType.NonProduction,
        AccessLevel.Admin,
        RiskLevel.High)]
    [InlineData(
        EnvironmentType.Production,
        AccessLevel.Read,
        RiskLevel.High)]
    [InlineData(
        EnvironmentType.Production,
        AccessLevel.Admin,
        RiskLevel.High)]
    public void CalculateRisk_ReturnsExpectedRisk(
        EnvironmentType environment,
        AccessLevel accessLevel,
        RiskLevel expected)
    {
        var result = _workflow.CalculateRisk(
            environment,
            accessLevel);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ManagerApproval_NonHighRisk_ReturnsApproved()
    {
        var request = new AccessRequest
        {
            Status = RequestStatus.PendingManagerApproval,
            RiskLevel = RiskLevel.NonHigh
        };

        var result = _workflow.GetAfterManagerApproval(request);

        Assert.Equal(RequestStatus.Approved, result);
    }

    [Fact]
    public void ManagerApproval_HighRisk_ReturnsPendingSystemOwner()
    {
        var request = new AccessRequest
        {
            Status = RequestStatus.PendingManagerApproval,
            RiskLevel = RiskLevel.High
        };

        var result = _workflow.GetAfterManagerApproval(request);

        Assert.Equal(
            RequestStatus.PendingSystemOwnerApproval,
            result);
    }

    [Fact]
    public void SystemOwnerApproval_ReturnsApproved()
    {
        var request = new AccessRequest
        {
            Status = RequestStatus.PendingSystemOwnerApproval,
            RiskLevel = RiskLevel.High
        };

        var result =
            _workflow.GetAfterSystemOwnerApproval(request);

        Assert.Equal(RequestStatus.Approved, result);
    }

    [Theory]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected)]
    public void TerminalRequest_CannotBeApproved(
        RequestStatus status)
    {
        var request = new AccessRequest
        {
            Status = status,
            RiskLevel = RiskLevel.NonHigh
        };

        Assert.Throws<InvalidOperationException>(() =>
            _workflow.GetAfterManagerApproval(request));
    }

    [Fact]
    public void Rejection_ReturnsRejected()
    {
        var request = new AccessRequest
        {
            Status = RequestStatus.PendingManagerApproval
        };

        var result = _workflow.GetAfterRejection(request);

        Assert.Equal(RequestStatus.Rejected, result);
    }
}