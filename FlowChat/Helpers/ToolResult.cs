using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowChat.Helpers;

public static class ToolResult
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Returns a JSON-formatted tool result with <c>status: "success"</c>.
    /// </summary>
    /// <param name="result">
    /// The result payload. Strings are serialized as a JSON string value;
    /// objects are serialized as a nested JSON object.
    /// </param>
    /// <returns>
    /// A JSON string of the form <c>{"status":"success","result":...}</c>.
    /// </returns>
    public static string Success(object result) =>
        Serialize("success", result);

    /// <summary>
    /// Returns a JSON-formatted tool result with <c>status: "failure"</c>.
    /// </summary>
    /// <param name="result">
    /// A description of what went wrong. Strings are serialized as a JSON string value;
    /// objects are serialized as a nested JSON object.
    /// </param>
    /// <returns>
    /// A JSON string of the form <c>{"status":"failure","result":...}</c>.
    /// </returns>
    public static string Failure(object result) =>
        Serialize("failure", result);

    private static string Serialize(string status, object result) =>
        JsonSerializer.Serialize(new { status, result }, JsonOptions);
}
