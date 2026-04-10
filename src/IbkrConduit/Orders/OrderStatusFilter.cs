using System.Runtime.Serialization;

namespace IbkrConduit.Orders;

/// <summary>
/// Filter values for the live orders endpoint. Pass an array of these to
/// <see cref="Client.IOrderOperations.GetLiveOrdersAsync"/> to filter by order status.
/// Include <see cref="SortByTime"/> to sort results chronologically.
/// </summary>
public enum OrderStatusFilter
{
    /// <summary>Orders that are inactive.</summary>
    [EnumMember(Value = "inactive")]
    Inactive,

    /// <summary>Orders pending submission to the exchange.</summary>
    [EnumMember(Value = "pending_submit")]
    PendingSubmit,

    /// <summary>Orders that have been pre-submitted.</summary>
    [EnumMember(Value = "pre_submitted")]
    PreSubmitted,

    /// <summary>Orders that have been submitted to the exchange.</summary>
    [EnumMember(Value = "submitted")]
    Submitted,

    /// <summary>Orders that have been fully filled.</summary>
    [EnumMember(Value = "filled")]
    Filled,

    /// <summary>Orders with a pending cancellation request.</summary>
    [EnumMember(Value = "pending_cancel")]
    PendingCancel,

    /// <summary>Orders that have been cancelled.</summary>
    [EnumMember(Value = "cancelled")]
    Cancelled,

    /// <summary>Orders in a warning state.</summary>
    [EnumMember(Value = "warn_state")]
    WarnState,

    /// <summary>Sort results by time. Include alongside status filters to get chronological ordering.</summary>
    [EnumMember(Value = "sort_by_time")]
    SortByTime,
}
