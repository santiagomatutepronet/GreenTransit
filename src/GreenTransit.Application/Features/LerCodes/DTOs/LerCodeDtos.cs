namespace GreenTransit.Application.Features.LerCodes.DTOs;

/// <summary>DTO mínimo para el listado plano de códigos LER.</summary>
public sealed record LerCodeDto(
    Guid    Id,
    string  Code,
    string? CodeExtended,
    string  Description,
    string? Chapter,
    string? ChapterDescription,
    string? SubChapter,
    string? SubChapterDescription,
    bool    IsDangerous,
    bool    IsRAEE,
    string? DefaultProductCategory,
    bool    IsActive
);

/// <summary>DTO de detalle con todos los campos.</summary>
public sealed record LerCodeDetailDto(
    Guid    Id,
    string  Code,
    string? CodeExtended,
    string  Description,
    string? Chapter,
    string? ChapterDescription,
    string? SubChapter,
    string? SubChapterDescription,
    bool    IsDangerous,
    bool    IsRAEE,
    string? DefaultProductCategory,
    string? Notes,
    bool    IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>Nodo de subcapítulo dentro de la estructura jerárquica.</summary>
public sealed record LerSubChapterDto(
    string? SubChapter,
    string? SubChapterDescription,
    List<LerCodeDto> Codes
);

/// <summary>Nodo de capítulo en la estructura jerárquica devuelta por GetLerCodesQuery.</summary>
public sealed record LerChapterDto(
    string? Chapter,
    string? ChapterDescription,
    List<LerSubChapterDto> SubChapters
);
