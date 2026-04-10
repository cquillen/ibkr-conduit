using System.Globalization;

namespace IbkrConduit.Contracts;

/// <summary>
/// Represents an option/futures expiry month in YYYYMM format (e.g., "202701" for January 2027).
/// Serializes to the IBKR wire format via <see cref="ToString"/>.
/// </summary>
/// <param name="Year">The four-digit year.</param>
/// <param name="Month">The month (1-12).</param>
public readonly record struct ExpiryMonth(int Year, int Month)
{
    /// <summary>Serializes to IBKR wire format: YYYYMM (e.g., "202701").</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Year:D4}{Month:D2}");

    /// <summary>Creates an <see cref="ExpiryMonth"/> from a <see cref="DateOnly"/>. The day is ignored.</summary>
    public static ExpiryMonth FromDate(DateOnly date) => new(date.Year, date.Month);

    /// <summary>Creates an <see cref="ExpiryMonth"/> from a <see cref="DateTime"/>. The day and time are ignored.</summary>
    public static ExpiryMonth FromDateTime(DateTime dateTime) => new(dateTime.Year, dateTime.Month);
}
