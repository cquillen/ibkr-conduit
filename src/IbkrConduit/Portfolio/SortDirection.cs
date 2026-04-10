using System.Runtime.Serialization;

namespace IbkrConduit.Portfolio;

/// <summary>
/// Sort direction for ordered results.
/// </summary>
public enum SortDirection
{
    /// <summary>Ascending order.</summary>
    [EnumMember(Value = "a")]
    Ascending,

    /// <summary>Descending order.</summary>
    [EnumMember(Value = "d")]
    Descending,
}
