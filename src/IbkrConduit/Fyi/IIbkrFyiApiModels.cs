using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Fyi;

/// <summary>
/// Response from GET /fyi/unreadnumber.
/// </summary>
/// <param name="BN">The number of unread bulletins.</param>
[ExcludeFromCodeCoverage]
public record UnreadBulletinCountResponse(
    [property: JsonPropertyName("BN")] int BN)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// A single FYI notification setting/subscription item from GET /fyi/settings.
/// </summary>
/// <param name="FC">FYI code for the notification type.</param>
/// <param name="FN">Human-readable title for the notification.</param>
/// <param name="FD">Detailed description of the topic.</param>
/// <param name="H">Disclaimer read status (0=unread, 1=read).</param>
/// <param name="A">Whether the subscription can be modified (1=yes).</param>
[ExcludeFromCodeCoverage]
public record FyiSettingItem(
    [property: JsonPropertyName("FC")] string FC,
    [property: JsonPropertyName("FN")] string FN,
    [property: JsonPropertyName("FD")] string FD,
    [property: JsonPropertyName("H")] int H,
    [property: JsonPropertyName("A")] int A)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Request body for POST /fyi/settings/{typecode}.
/// </summary>
/// <param name="Enabled">Whether to enable or disable the subscription.</param>
[ExcludeFromCodeCoverage]
public record FyiSettingUpdateRequest(
    [property: JsonPropertyName("enabled")] bool Enabled);

/// <summary>
/// Generic acknowledgement response from FYI mutation endpoints.
/// </summary>
/// <param name="V">Acknowledgement indicator (1 = acknowledged).</param>
/// <param name="T">Time in milliseconds to complete the operation.</param>
[ExcludeFromCodeCoverage]
public record FyiAcknowledgementResponse(
    [property: JsonPropertyName("V")] int V,
    [property: JsonPropertyName("T")] int T)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from GET /fyi/disclaimer/{typecode}.
/// </summary>
/// <param name="FC">The typecode for the disclaimer.</param>
/// <param name="DT">The disclaimer message text.</param>
[ExcludeFromCodeCoverage]
public record FyiDisclaimerResponse(
    [property: JsonPropertyName("FC")] string FC,
    [property: JsonPropertyName("DT")] string DT)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// A device entry in the delivery options response.
/// </summary>
/// <param name="NM">Human-readable device name.</param>
/// <param name="I">Device identifier.</param>
/// <param name="UI">Unique device ID.</param>
/// <param name="A">Whether the device is enabled (0=disabled, 1=enabled).</param>
[ExcludeFromCodeCoverage]
public record FyiDeviceInfo(
    [property: JsonPropertyName("NM")] string NM,
    [property: JsonPropertyName("I")] string I,
    [property: JsonPropertyName("UI")] string UI,
    [property: JsonPropertyName("A")] int A)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from GET /fyi/deliveryoptions.
/// </summary>
/// <param name="M">Email option status (0=disabled, 1=enabled).</param>
/// <param name="E">Array of registered devices.</param>
[ExcludeFromCodeCoverage]
public record FyiDeliveryOptionsResponse(
    [property: JsonPropertyName("M")] int M,
    [property: JsonPropertyName("E")] List<FyiDeviceInfo> E)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Request body for POST /fyi/deliveryoptions/device.
/// </summary>
/// <param name="DeviceName">Human-readable name of the device.</param>
/// <param name="DeviceId">ID code for the specific device.</param>
/// <param name="UiName">Title used for the interface system.</param>
/// <param name="Enabled">Whether the device should be enabled.</param>
[ExcludeFromCodeCoverage]
public record FyiDeviceRequest(
    [property: JsonPropertyName("deviceName")] string DeviceName,
    [property: JsonPropertyName("deviceId")] string DeviceId,
    [property: JsonPropertyName("uiName")] string UiName,
    [property: JsonPropertyName("enabled")] bool Enabled);

/// <summary>
/// A single FYI notification from GET /fyi/notifications.
/// </summary>
/// <param name="R">Read status (0=unread, 1=read).</param>
/// <param name="D">Notification date as epoch string.</param>
/// <param name="MS">Title of the notification.</param>
/// <param name="MD">Content of the notification.</param>
/// <param name="ID">Unique notification identifier.</param>
/// <param name="HT">Notification HT flag.</param>
/// <param name="FC">FYI typecode.</param>
[ExcludeFromCodeCoverage]
public record FyiNotification(
    [property: JsonPropertyName("R")] int R,
    [property: JsonPropertyName("D")] string D,
    [property: JsonPropertyName("MS")] string MS,
    [property: JsonPropertyName("MD")] string MD,
    [property: JsonPropertyName("ID")] string ID,
    [property: JsonPropertyName("HT")] int HT,
    [property: JsonPropertyName("FC")] string FC)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Notification read status detail returned within <see cref="FyiNotificationReadResponse"/>.
/// </summary>
/// <param name="R">Read status (0=unread, 1=read).</param>
/// <param name="ID">Notification identifier.</param>
[ExcludeFromCodeCoverage]
public record FyiNotificationReadDetail(
    [property: JsonPropertyName("R")] int R,
    [property: JsonPropertyName("ID")] string ID)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from PUT /fyi/notifications/{notificationId}.
/// </summary>
/// <param name="V">Acknowledgement indicator (1 = acknowledged).</param>
/// <param name="T">Time in milliseconds to complete the operation.</param>
/// <param name="P">Details about the notification read status.</param>
[ExcludeFromCodeCoverage]
public record FyiNotificationReadResponse(
    [property: JsonPropertyName("V")] int V,
    [property: JsonPropertyName("T")] int T,
    [property: JsonPropertyName("P")] FyiNotificationReadDetail? P)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
