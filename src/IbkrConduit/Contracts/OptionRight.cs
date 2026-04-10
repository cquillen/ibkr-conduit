using System.Runtime.Serialization;

namespace IbkrConduit.Contracts;

/// <summary>
/// Option right (call or put).
/// </summary>
public enum OptionRight
{
    /// <summary>Call option.</summary>
    [EnumMember(Value = "C")]
    Call,

    /// <summary>Put option.</summary>
    [EnumMember(Value = "P")]
    Put,
}
