using ChangeOrder.Domain.ValueObjects;
using ChangeOrder.Domain.Enums;

namespace ChangeOrder.Domain.Entities;

/// <summary>
/// 
/// </summary>
public class ChangeOrderEntity
{
    /// <summary>
    /// 
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// 
    /// </summary>
    public required OrderNumber Number { get; init; } 

    /// <summary>
    /// 
    /// </summary>
    public required string ProgramName { get; init; }

    /// <summary>
    /// 
    /// </summary>
    public required string ProductionVersion { get; init; }

    /// <summary>
    /// 
    /// </summary>
    public string? VersionScreenshotPath { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public required DateTime RequestDate  { get; init; }

    /// <summary>
    /// 
    /// </summary>
    public required string WorkDescription { get; init; }

    /// <summary>
    /// 
    /// </summary>
    public required string RequestDetails { get; init; }

    /// <summary>
    /// 
    /// </summary>
    public required string Justification { get; init; }

    /// <summary>
    /// 
    /// </summary>
    public required string RequiredAction { get; init; }

    /// <summary>
    /// 
    /// </summary>
    public required RequesterInfo Requester { get; init; }

    /// <summary>
    /// 
    /// </summary>
    public required ApprovalChain Approval { get; init; }

    /// <summary>
    /// 
    /// </summary>
    public required OrderStatus Status { get; init; }

    /// <summary>
    /// 
    /// </summary>
    public  DateTime? DeliveryDate { get; set;  }

    /// <summary>
    /// 
    /// </summary>
    public  DateTime? InitialEvaluationDate { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public  DateTime? ProductionDeployDate { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? PostDeployScreenshotPath { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public required DateTime CreatedAt { get; init;  }

    /// <summary>
    /// 
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public required bool IsDeleted { get; init;  }

    /// <summary>
    /// 
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}
