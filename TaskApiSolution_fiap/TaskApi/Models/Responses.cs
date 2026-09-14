namespace TaskApi.Models;

/// <summary>
/// Corpo devolvido em respostas 400. Serializa algo como: { "error": "...}" </summary>
/// <param name="Error"></param>
public record ErrorResponse( string Error );

/// <summary>
/// Corpo do Endpoint /health: Serialzia como: {"status": "healthy"}.
/// 
/// </summary>
/// <param name="Status"></param>
public record HealthResponse( string Status);