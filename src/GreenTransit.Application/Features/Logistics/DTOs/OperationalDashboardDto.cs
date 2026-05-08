namespace GreenTransit.Application.Features.Logistics.DTOs;

// ── Dashboard 3 — Panel Operativo de Gestores / CACs / Plantas ───────────────

/// <summary>DTO raíz del Dashboard 3. Agrupa las secciones según el perfil activo.</summary>
public sealed record OperationalDashboardDto(
    // ── DISPATCH_OFFICE ───────────────────────────────────────────────────────
    /// <summary>W1 — SO pendientes de planificar.</summary>
    List<PendingServiceOrderDto>    PendingServiceOrders,
    /// <summary>W2 — Embudo de traslados por estado.</summary>
    List<WasteMoveFunnelItemDto>    WasteMovesFunnel,
    /// <summary>W3 — Planificación semanal (próximos 7 días).</summary>
    List<WeeklyPlanItemDto>         WeeklyPlan,
    /// <summary>W4 — Incidencias abiertas del ámbito.</summary>
    List<OpenIncidentRowDto>        OpenIncidents,
    // ── CAC_OP ────────────────────────────────────────────────────────────────
    /// <summary>W5 — Entradas en CAC hoy.</summary>
    List<CacEntryTodayDto>          CacEntriesToday,
    /// <summary>W6 — Stock acumulado por tipología de residuo.</summary>
    List<CacStockByResidueDto>      CacStockByResidue,
    /// <summary>W7 — Tickets de pesaje pendientes (sin peso registrado).</summary>
    int                             CacTicketsPending,
    // ── PLANT_OP ──────────────────────────────────────────────────────────────
    /// <summary>W8 — Entradas en planta hoy.</summary>
    List<PlantEntryTodayDto>        PlantEntriesToday,
    /// <summary>W9 — Balance de tratamiento del periodo.</summary>
    TreatmentBalanceDto             TreatmentBalance,
    /// <summary>W10 — Impropios detectados en el periodo.</summary>
    decimal                         ImproperWeightKg,
    /// <summary>W11 — Incidencias de planta abiertas.</summary>
    List<OpenIncidentRowDto>        PlantOpenIncidents,
    // ── Metadatos ─────────────────────────────────────────────────────────────
    string  ActiveProfile,
    int     Year,
    int?    Month
);

// ── Widget 1 — SO pendientes ─────────────────────────────────────────────────

public sealed record PendingServiceOrderDto(
    Guid      Id,
    string    ServiceOrderNumber,
    string    Priority,
    string?   WasteStream,
    DateTime? PlannedPickupStart,
    string?   PickupPointName
);

// ── Widget 2 — Embudo de traslados ────────────────────────────────────────────

public sealed record WasteMoveFunnelItemDto(
    string ServiceStatus,
    int    Count
);

// ── Widget 3 — Planificación semanal ─────────────────────────────────────────

public sealed record WeeklyPlanItemDto(
    DateTime Date,
    int      Count,
    string?  PriorityTop
);

// ── Widget 4 / 11 — Incidencia abierta ───────────────────────────────────────

public sealed record OpenIncidentRowDto(
    Guid      Id,
    string?   WasteMoveReference,
    string    Type,
    string    Severity,
    DateTime  OpenedAt,
    int       DaysOpen,
    string?   Description
);

// ── Widget 5 — Entrada CAC hoy ────────────────────────────────────────────────

public sealed record CacEntryTodayDto(
    Guid      Id,
    string?   WasteMoveReference,
    decimal   TotalKg,
    string?   CollectionMethod
);

// ── Widget 6 — Stock CAC por residuo ──────────────────────────────────────────

public sealed record CacStockByResidueDto(
    string? ResidueName,
    decimal TotalKg,
    int     Entries
);

// ── Widget 8 — Entrada planta hoy ────────────────────────────────────────────

public sealed record PlantEntryTodayDto(
    Guid     Id,
    string?  WasteMoveReference,
    string?  TicketScale,
    decimal? NetWeight,
    decimal? GrossWeight,
    decimal? TareWeight
);

// ── Widget 9 — Balance de tratamiento ─────────────────────────────────────────

public sealed record TreatmentBalanceDto(
    decimal TotalIn,
    decimal Reused,
    decimal Valued,
    decimal Removed,
    double  RecyclingRate
);
