using System.Text.Json;

namespace GreenTransit.Application.Common.Models;

/// <summary>
/// Resultado de la publicación de un endpoint a EcoDataNet.
/// </summary>
public class EndpointResult
{
    public string  Endpoint     { get; set; } = string.Empty;
    public int     TotalSent    { get; set; }
    public int     SuccessCount { get; set; }
    public int     ErrorCount   { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorDetail  { get; set; }

    /// <summary>
    /// Parsea la respuesta 207 Multi-Status de EcoDataNet para contabilizar ok/error por elemento.
    /// </summary>
    public void ParseMultiStatus(string responseBody)
    {
        try
        {
            using var doc  = JsonDocument.Parse(responseBody);
            var       root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                string? firstError = null;
                foreach (var item in root.EnumerateArray())
                {
                    bool isCorrect = false;

                    // Formato EcoDataNet: { isCorrect: bool, errorMessage: string, ... }
                    if (item.TryGetProperty("isCorrect", out var ic))
                        isCorrect = ic.GetBoolean();
                    else if (item.TryGetProperty("statusCode", out var sc))
                        isCorrect = sc.GetInt32() >= 200 && sc.GetInt32() < 300;
                    else if (item.TryGetProperty("status", out var s))
                        isCorrect = s.GetInt32() >= 200 && s.GetInt32() < 300;

                    if (isCorrect)
                        SuccessCount++;
                    else
                    {
                        ErrorCount++;
                        if (firstError is null && item.TryGetProperty("errorMessage", out var em))
                            firstError = em.GetString();
                    }
                }
                if (firstError is not null)
                    ErrorMessage = firstError.Length > 300 ? firstError[..300] : firstError;
            }
            else
            {
                ErrorMessage = "Respuesta 207 con formato inesperado";
                ErrorDetail  = responseBody.Length > 500 ? responseBody[..500] : responseBody;
            }
        }
        catch (JsonException ex)
        {
            ErrorMessage = $"Error al parsear respuesta 207: {ex.Message}";
            ErrorDetail  = responseBody.Length > 500 ? responseBody[..500] : responseBody;
        }
    }
}
