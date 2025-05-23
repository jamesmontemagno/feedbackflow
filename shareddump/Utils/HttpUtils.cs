using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace SharedDump.Utils;

/// <summary>
/// Provides utility methods for HTTP requests and responses.
/// </summary>
/// <remarks>
/// This class centralizes HTTP-related functionality to ensure consistent
/// handling of HTTP operations across services and components.
/// </remarks>
public static class HttpUtils
{
    /// <summary>
    /// Creates a JSON StringContent object for HTTP requests.
    /// </summary>
    /// <param name="data">The object to serialize to JSON.</param>
    /// <returns>A StringContent with JSON media type.</returns>
    /// <example>
    /// <code>
    /// var content = HttpUtils.CreateJsonContent(new { name = "value" });
    /// var response = await httpClient.PostAsync(url, content);
    /// </code>
    /// </example>
    public static StringContent CreateJsonContent(object data)
    {
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return content;
    }

    /// <summary>
    /// Creates a standardized error response for HTTP functions.
    /// </summary>
    /// <param name="req">The HTTP request data.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="message">The error message.</param>
    /// <returns>An HTTP response with the provided status and message.</returns>
    /// <example>
    /// <code>
    /// if (string.IsNullOrEmpty(id))
    /// {
    ///     return await HttpUtils.CreateErrorResponse(req, HttpStatusCode.BadRequest, "ID parameter is required");
    /// }
    /// </code>
    /// </example>
    public static async Task<HttpResponseData> CreateErrorResponse(
        HttpRequestData req, 
        HttpStatusCode statusCode, 
        string message)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteStringAsync(message);
        return response;
    }

    /// <summary>
    /// Creates a JSON response with the provided data.
    /// </summary>
    /// <param name="req">The HTTP request data.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="data">The data to serialize to JSON.</param>
    /// <returns>An HTTP response with JSON content.</returns>
    /// <example>
    /// <code>
    /// var result = new { id = "123", name = "Test" };
    /// return await HttpUtils.CreateJsonResponse(req, HttpStatusCode.OK, result);
    /// </code>
    /// </example>
    public static async Task<HttpResponseData> CreateJsonResponse(
        HttpRequestData req, 
        HttpStatusCode statusCode, 
        object data)
    {
        var response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(data));
        return response;
    }

    /// <summary>
    /// Deserializes a request body to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="req">The HTTP request data.</param>
    /// <returns>The deserialized object, or null if deserialization fails.</returns>
    /// <example>
    /// <code>
    /// var data = await HttpUtils.DeserializeRequestBody&lt;MyDataType&gt;(req);
    /// if (data == null)
    /// {
    ///     return await HttpUtils.CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request data");
    /// }
    /// </code>
    /// </example>
    public static async Task<T?> DeserializeRequestBody<T>(HttpRequestData req)
    {
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        try
        {
            return JsonSerializer.Deserialize<T>(requestBody, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return default;
        }
    }
}