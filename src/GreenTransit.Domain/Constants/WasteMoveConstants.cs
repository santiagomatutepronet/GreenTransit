namespace GreenTransit.Domain.Constants;

/// <summary>Estados del ciclo de vida de un Traslado de Residuos.</summary>
public static class WasteMoveStatuses
{
    public const string Solicitado  = "SOLICITADO";
    public const string Planificado = "PLANIFICADO";
    public const string Recogido    = "RECOGIDO";
    public const string EnCAC       = "EN_CAC";
    public const string EnPlanta    = "EN_PLANTA";
    public const string Clasificado = "CLASIFICADO";
    public const string Incidencia  = "INCIDENCIA";
    public const string Cancelado   = "CANCELADO";

    public static readonly IReadOnlyList<string> All =
        [Solicitado, Planificado, Recogido, EnCAC, EnPlanta, Clasificado, Incidencia, Cancelado];

    /// <summary>Orden lineal para el stepper visual (sin INCIDENCIA ni CANCELADO).</summary>
    public static readonly IReadOnlyList<string> StepperOrder =
        [Solicitado, Planificado, Recogido, EnCAC, EnPlanta, Clasificado];

    /// <summary>Estados que permiten edición del traslado.</summary>
    public static readonly IReadOnlyList<string> Editable = [Solicitado];

    public static string Label(string status) => status switch
    {
        Solicitado  => "Solicitado",
        Planificado => "Planificado",
        Recogido    => "Recogido",
        EnCAC       => "En CAC",
        EnPlanta    => "En planta",
        Clasificado => "Clasificado",
        Incidencia  => "Incidencia",
        Cancelado   => "Cancelado",
        _           => status
    };

    public static string BadgeCss(string status) => status switch
    {
        Solicitado  => "badge badge-solicitado",
        Planificado => "badge badge-planificado",
        Recogido    => "badge badge-recogido",
        EnCAC       => "badge badge-en-cac",
        EnPlanta    => "badge badge-en-planta",
        Clasificado => "badge badge-clasificado",
        Incidencia  => "badge badge-incidencia",
        Cancelado   => "badge badge-cancelado",
        _           => "badge badge-neutral"
    };
}

/// <summary>Roles válidos para el campo Origen de un Traslado.</summary>
public static class WasteMoveSourceRoles
{
    public static readonly IReadOnlyList<string> Valid =
        ["Source", EntityRoles.CAC, EntityRoles.PublicEntity, EntityRoles.Producer];
}

/// <summary>Roles válidos para el campo Destino de un Traslado.</summary>
public static class WasteMoveDestinationRoles
{
    public static readonly IReadOnlyList<string> Valid =
        ["Destination", EntityRoles.Plant, EntityRoles.CAC];
}
