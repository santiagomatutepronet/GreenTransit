namespace GreenTransit.Domain.Constants;

/// <summary>Estados válidos de una Orden de Servicio.</summary>
public static class ServiceOrderStatuses
{
    public const string Pending    = "Pending";
    public const string Scheduled  = "Scheduled";
    public const string InProgress = "InProgress";
    public const string Completed  = "Completed";
    public const string Cancelled  = "Cancelled";

    public static readonly IReadOnlyList<string> All =
        [Pending, Scheduled, InProgress, Completed, Cancelled];

    /// <summary>Estados que permiten edición.</summary>
    public static readonly IReadOnlyList<string> Editable = [Pending, Scheduled];

    public static string BadgeCss(string status) => status switch
    {
        Pending    => "badge badge-pendiente",
        Scheduled  => "badge badge-planificado",
        InProgress => "badge badge-recogido",
        Completed  => "badge badge-success",
        Cancelled  => "badge badge-cancelado",
        _          => "badge badge-neutral"
    };

    public static string Label(string status) => status switch
    {
        Pending    => "Pendiente",
        Scheduled  => "Planificada",
        InProgress => "En curso",
        Completed  => "Completada",
        Cancelled  => "Cancelada",
        _          => status
    };
}

/// <summary>Prioridades válidas de una Orden de Servicio.</summary>
public static class ServiceOrderPriorities
{
    public const string Low      = "Low";
    public const string Normal   = "Normal";
    public const string High     = "High";
    public const string Critical = "Critical";

    public static readonly IReadOnlyList<string> All = [Low, Normal, High, Critical];

    public static string BadgeCss(string priority) => priority switch
    {
        Critical => "badge badge-severity-critical",
        High     => "badge badge-severity-high",
        Normal   => "badge badge-info",
        Low      => "badge badge-neutral",
        _        => "badge badge-neutral"
    };

    public static string Label(string priority) => priority switch
    {
        Low      => "Baja",
        Normal   => "Normal",
        High     => "Alta",
        Critical => "Crítica",
        _        => priority
    };
}
