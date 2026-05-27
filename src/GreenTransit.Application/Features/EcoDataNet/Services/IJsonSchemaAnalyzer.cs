using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

namespace GreenTransit.Application.Features.EcoDataNet.Services;

/// <summary>
/// Analiza la estructura de un JSON arbitrario y produce un esquema tipado.
/// </summary>
public interface IJsonSchemaAnalyzer
{
    /// <summary>
    /// Analiza el JSON proporcionado y devuelve un esquema descriptivo.
    /// </summary>
    /// <param name="jsonContent">String con el contenido JSON.</param>
    /// <returns>Esquema detectado o null si el JSON es inválido.</returns>
    JsonDataSchema? Analyze(string jsonContent);

    /// <summary>
    /// Genera un hash MD5 de la estructura del esquema (nombres de propiedades + tipos).
    /// Permite detectar cambios en la estructura del JSON entre descargas del mismo asset.
    /// </summary>
    /// <param name="schema">Esquema analizado.</param>
    /// <returns>String hexadecimal del hash MD5 en minúsculas.</returns>
    string ComputeSchemaHash(JsonDataSchema schema);
}
