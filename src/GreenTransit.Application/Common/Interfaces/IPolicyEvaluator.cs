namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Abstracción de la evaluación de policies de ASP.NET Core desde la capa Application.
/// Implementada en GreenTransit.Web para evitar que Application dependa de
/// Microsoft.AspNetCore.Authorization directamente.
/// </summary>
public interface IPolicyEvaluator
{
    /// <summary>
    /// Evalúa si el usuario autenticado actual cumple la policy indicada.
    /// </summary>
    /// <param name="policyName">Nombre de la policy (valor de PolicyConstants).</param>
    /// <returns>True si el usuario cumple la policy; false en caso contrario.</returns>
    Task<bool> AuthorizeAsync(string policyName);
}
