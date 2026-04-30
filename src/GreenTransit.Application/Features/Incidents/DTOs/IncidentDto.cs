namespace GreenTransit.Application.Features.Incidents.DTOs;

/// <summary>DTO para el listado paginado de incidencias.</summary>
public sealed record IncidentDto(
    Guid      Id,
    string    Type,
    string    Severity,
    bool      IsOpen,
    DateTime  OpenedAt,
    DateTime? ClosedAt,
    Guid?     ServiceOrderId,
    string?   WasteMoveReference,
    string?   TicketScale,
    string?   ReportedByName,
    string?   Description
);

/// <summary>DTO de resolución estructurada (mapeado desde ResolutionJson).</summary>
public sealed record IncidentResolutionDto(
    string?   PreviousStatus,
    string?   ResolutionType,
    string?   ResolutionDescription,
    string?   ResolvedByName,
    DateTime? ResolvedAt
);

/// <summary>DTO de detalle completo de una incidencia.</summary>
public sealed record IncidentDetailDto(
    Guid                   Id,
    Guid?                  OwnerId,
    string                 Type,
    string                 Severity,
    bool                   IsOpen,
    DateTime               OpenedAt,
    DateTime?              ClosedAt,
    Guid?                  ServiceOrderId,
    string?                WasteMoveReference,
    string?                WasteMoveServiceStatus,
    string?                TicketScale,
    string?                ReportedByName,
    string?                ReportedByNationalId,
    string?                ReportedByCenterCode,
    string?                Description,
    IncidentResolutionDto? Resolution,
    int                    Version,
    string?                Hash,
    DateTime               CreatedAt,
    DateTime               UpdatedAt,
    int                    IdUser
);
