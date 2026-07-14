using System.Text.Json;

namespace Clawmeter.Shared;

/// <summary>
/// GATT service + characteristic UUIDs shared with the firmware. These match
/// firmware/src/ble.cpp exactly — do not edit one side without the other.
/// </summary>
public static class BleProtocol
{
    public const string ServiceUuid = "4c41555a-4465-7669-6365-000000000001";
    public const string RxCharUuid   = "4c41555a-4465-7669-6365-000000000002";  // daemon → firmware (JSON)
    public const string TxCharUuid   = "4c41555a-4465-7669-6365-000000000003";  // firmware → daemon (ack/nack)
    public const string ReqCharUuid  = "4c41555a-4465-7669-6365-000000000004";  // firmware → daemon (refresh request)
}

/// <summary>
/// JSON serializer with the same compact format the Python daemon uses
/// (separators=(",", ":")). The byte order matches the firmware parser.
/// </summary>
public static class PayloadSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
    };

    public static byte[] Serialize(UsagePayload payload)
    {
        var json = JsonSerializer.Serialize(payload, Options);
        return System.Text.Encoding.UTF8.GetBytes(json);
    }
}
