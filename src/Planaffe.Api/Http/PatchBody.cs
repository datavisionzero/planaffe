using System.Text.Json;
using Planaffe.Domain;

namespace Planaffe.Api.Http;

/// <summary>
/// The raw document of a <c>PATCH</c>, where present, present as <c>null</c>
/// and absent are three different things and only the document tells them
/// apart — the one reader for the endpoints that parse it by hand and the
/// converters that parse it for a bound record.
/// </summary>
/// <remarks>
/// A body that is not JSON, or not an object, is <c>validation</c> on
/// <c>body</c> and never the <c>internal</c> of an exception nobody caught; so
/// is a value of the wrong kind where the kind decides what it means.
/// </remarks>
public static class PatchBody
{
    /// <summary>The body as an object, detached from the document it was parsed from.</summary>
    public static async Task<JsonElement> ReadObjectAsync(HttpRequest request, string what, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            throw Refusal.Validation("body", $"{what} is a JSON object, and the body is not JSON.");
        }

        using (document)
        {
            return Object(document.RootElement.Clone(), what);
        }
    }

    /// <summary><paramref name="body"/>, if it is an object.</summary>
    public static JsonElement Object(JsonElement body, string what) =>
        body.ValueKind is JsonValueKind.Object ? body : throw Refusal.Validation("body", $"{what} is an object.");

    /// <summary>
    /// A bound change, which an empty body leaves null: a <c>PATCH</c> that
    /// says nothing is refused rather than answered as if it changed nothing.
    /// </summary>
    public static T Required<T>(T? request, string what) where T : class =>
        request ?? throw Refusal.Validation("body", $"{what} is a JSON object, and the body is empty.");

    /// <summary>Whether the field is there at all, <c>null</c> included.</summary>
    public static bool Given(JsonElement body, string property) => body.TryGetProperty(property, out _);

    /// <summary>The field's text, or nothing when it is absent, <c>null</c> or not a string.</summary>
    public static string? Text(JsonElement body, string property) =>
        body.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.String ? value.GetString() : null;

    /// <summary>The field's switch, or nothing when it is absent or not a boolean.</summary>
    public static bool? Flag(JsonElement body, string property) =>
        body.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    /// <summary>The field's list of strings, or nothing when it is absent or not a list.</summary>
    public static string[]? Texts(JsonElement body, string property) =>
        body.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.Array
            ? [.. value.EnumerateArray().Select(item => item.ValueKind is JsonValueKind.String ? item.GetString()! : string.Empty)]
            : null;

    /// <summary>
    /// The field's whole number, or nothing when it is absent or not a number;
    /// a number that is not a whole one — <c>1.5</c> — is <c>validation</c>.
    /// </summary>
    public static int? Integer(JsonElement body, string property)
    {
        if (!body.TryGetProperty(property, out var value) || value.ValueKind is not JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetInt32(out var number)
            ? number
            : throw Refusal.Validation(property, $"{property} is a whole number.");
    }
}
