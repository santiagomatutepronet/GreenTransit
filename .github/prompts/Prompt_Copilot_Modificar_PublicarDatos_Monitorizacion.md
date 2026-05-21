# 🤖 Prompt Copilot — Modificar Pantalla "Publicar Datos" (EcoDataNet Dataspace)

> **Objetivo**: Transformar la pantalla "Publicar datos" de cada perfil de una pantalla de acción (con botón "Publicar") a una **pantalla de monitorización y transparencia** que muestra al participante qué datos suyos se publican automáticamente en EcoDataNet, cuándo fue la última sincronización y en qué estado está.
>
> **Stack**: Blazor Web App (.NET 10+) · Radzen Blazor Components · Patrón existente de `EdcPublishDataBase.razor`
>
> **Contexto**: La publicación de datos en la BD intermedia de EcoDataNet se realiza de forma **desatendida** por un servicio central de sincronización. Los participantes no publican manualmente; esta pantalla les permite **verificar** que sus datos se están publicando correctamente.

---

## CAMBIO DE PROPÓSITO

| Antes | Después |
|---|---|
| Pantalla de **acción**: tabla de datasets + botón "Publicar datos" (stub deshabilitado) | Pantalla de **monitorización**: tabla de datasets + estado de sincronización + última fecha + indicador visual |
| Título: "Datasets publicados — {ProfileLabel}" | Título: "Datos publicados en EcoDataNet — {ProfileLabel}" |
| Subtítulo: "Catálogo de datasets que este participante publica..." | Subtítulo: "Estado de la sincronización automática de sus datos con el espacio de datos EcoDataNet" |
| Botón "Publicar datos" (disabled, stub) | **Eliminado**. Sustituido por banner informativo |

---

## MODIFICACIONES EN `EdcPublishDataBase.razor`

### 1. Banner informativo (sustituye al botón)

Añadir un `RadzenAlert` al inicio de la pantalla, antes de la tabla:

```razor
<RadzenAlert AlertStyle="AlertStyle.Info"
             Variant="Variant.Flat"
             ShowIcon="true"
             Icon="sync"
             AllowClose="false"
             Style="margin-bottom: 1rem;">
    La publicación de datos en EcoDataNet se realiza de forma automática y periódica 
    por el servicio central de sincronización. Esta pantalla le permite verificar 
    el estado de sus datos en el espacio de datos.
</RadzenAlert>
```

### 2. Nuevas columnas en el RadzenDataGrid

Modificar el `RadzenDataGrid` existente para añadir dos columnas nuevas después de la columna "Referencia":

| Columna | Ancho | Contenido |
|---------|-------|-----------|
| **UC** (ya existe) | 80px | Badge con caso de uso |
| **Descripción** (ya existe) | flex | Texto descriptivo del dataset |
| **Referencia** (ya existe) | 300px | Nombre técnico (monoespaciada) |
| **Última sincronización** (NUEVA) | 180px | Fecha/hora de última sincronización o texto "Pendiente" |
| **Estado** (NUEVA) | 120px | Badge de estado con color semáforo |

### 3. Modelo de datos de sincronización (mock)

Crear un modelo para el estado de sincronización. En esta fase es mock (datos simulados en el frontend):

```csharp
public record DatasetSyncStatus
{
    public string DatasetRef { get; init; } = string.Empty;
    public DateTime? LastSyncUtc { get; init; }
    public SyncState State { get; init; } = SyncState.Pending;
    public int? RecordCount { get; init; }
}

public enum SyncState
{
    Pending,        // Nunca sincronizado
    Synchronized,   // Última sincronización OK
    InProgress,     // Sincronización en curso
    Error           // Error en última sincronización
}
```

### 4. Generación de datos mock de sincronización

En `EdcPublishDataBase.razor`, al cargar el componente, generar datos mock de sincronización para cada dataset del perfil:

```csharp
private List<DatasetSyncStatus> _syncStatuses = new();

protected override void OnParametersSet()
{
    _profile = EcoDataNetDatasetStore.GetBySlug(ProfileId);
    if (_profile != null)
    {
        _syncStatuses = GenerateMockSyncStatuses(_profile.Publish);
    }
}

private List<DatasetSyncStatus> GenerateMockSyncStatuses(List<DatasetInfo> datasets)
{
    var random = new Random(ProfileId.GetHashCode()); // Seed fijo por perfil para consistencia
    var now = DateTime.UtcNow;

    return datasets.Select(ds => new DatasetSyncStatus
    {
        DatasetRef = ds.Ref,
        LastSyncUtc = random.Next(100) < 85
            ? now.AddMinutes(-random.Next(5, 1440))  // 85% sincronizados (hace 5 min a 24h)
            : null,                                    // 15% pendientes
        State = random.Next(100) switch
        {
            < 70 => SyncState.Synchronized,
            < 85 => SyncState.Pending,
            < 95 => SyncState.InProgress,
            _    => SyncState.Error
        },
        RecordCount = random.Next(100) < 85
            ? random.Next(10, 5000)
            : null
    }).ToList();
}
```

### 5. Modelo combinado para la grid

Crear un modelo que combine la info del dataset con el estado de sincronización:

```csharp
private record DatasetRow(
    string Uc,
    string Desc,
    string Ref,
    DateTime? LastSyncUtc,
    SyncState State,
    int? RecordCount
);

private List<DatasetRow> _rows = new();

// En OnParametersSet, después de generar _syncStatuses:
_rows = _profile.Publish.Select(ds =>
{
    var sync = _syncStatuses.FirstOrDefault(s => s.DatasetRef == ds.Ref);
    return new DatasetRow(
        ds.Uc,
        ds.Desc,
        ds.Ref,
        sync?.LastSyncUtc,
        sync?.State ?? SyncState.Pending,
        sync?.RecordCount
    );
}).ToList();
```

### 6. Markup del RadzenDataGrid actualizado

```razor
<RadzenDataGrid TItem="DatasetRow"
                Data="@_rows"
                AllowSorting="true"
                AllowFiltering="false"
                Density="Density.Compact"
                Style="margin-bottom: 1rem;">
    <Columns>
        @* ── Columna UC (badge) ── *@
        <RadzenDataGridColumn TItem="DatasetRow"
                              Title="UC"
                              Width="80px"
                              Property="Uc"
                              Sortable="true">
            <Template Context="row">
                <RadzenBadge BadgeStyle="BadgeStyle.Info"
                             IsPill="true"
                             Text="@row.Uc" />
            </Template>
        </RadzenDataGridColumn>

        @* ── Columna Descripción ── *@
        <RadzenDataGridColumn TItem="DatasetRow"
                              Title="Descripción"
                              Property="Desc"
                              Sortable="false" />

        @* ── Columna Referencia (monoespaciada) ── *@
        <RadzenDataGridColumn TItem="DatasetRow"
                              Title="Referencia"
                              Width="300px"
                              Property="Ref"
                              Sortable="true">
            <Template Context="row">
                <code>@row.Ref</code>
            </Template>
        </RadzenDataGridColumn>

        @* ── Columna Última sincronización (NUEVA) ── *@
        <RadzenDataGridColumn TItem="DatasetRow"
                              Title="Última sincronización"
                              Width="180px"
                              Sortable="true"
                              Property="LastSyncUtc">
            <Template Context="row">
                @if (row.LastSyncUtc.HasValue)
                {
                    <span>@row.LastSyncUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")</span>
                    @if (row.RecordCount.HasValue)
                    {
                        <br />
                        <small class="rz-text-secondary-color">@row.RecordCount.Value.ToString("N0") registros</small>
                    }
                }
                else
                {
                    <span class="rz-text-secondary-color"><em>Pendiente</em></span>
                }
            </Template>
        </RadzenDataGridColumn>

        @* ── Columna Estado (NUEVA, badge semáforo) ── *@
        <RadzenDataGridColumn TItem="DatasetRow"
                              Title="Estado"
                              Width="140px"
                              Sortable="true"
                              Property="State">
            <Template Context="row">
                @switch (row.State)
                {
                    case SyncState.Synchronized:
                        <RadzenBadge BadgeStyle="BadgeStyle.Success"
                                     IsPill="true"
                                     Text="Sincronizado" />
                        break;
                    case SyncState.InProgress:
                        <RadzenBadge BadgeStyle="BadgeStyle.Warning"
                                     IsPill="true"
                                     Text="En curso" />
                        break;
                    case SyncState.Error:
                        <RadzenBadge BadgeStyle="BadgeStyle.Danger"
                                     IsPill="true"
                                     Text="Error" />
                        break;
                    case SyncState.Pending:
                    default:
                        <RadzenBadge BadgeStyle="BadgeStyle.Light"
                                     IsPill="true"
                                     Text="Pendiente" />
                        break;
                }
            </Template>
        </RadzenDataGridColumn>
    </Columns>
</RadzenDataGrid>
```

### 7. Pie de página con resumen y endpoint

Debajo de la tabla, sustituir el botón eliminado por un resumen de estado y la referencia al endpoint:

```razor
@* ── Resumen de sincronización ── *@
<RadzenStack Orientation="Orientation.Horizontal"
             AlignItems="AlignItems.Center"
             Gap="1.5rem"
             Style="margin-top: 0.5rem; margin-bottom: 1rem;">
    <RadzenText TextStyle="TextStyle.Caption" class="rz-text-secondary-color">
        <RadzenIcon Icon="check_circle" Style="color: var(--rz-success);" />
        @_rows.Count(r => r.State == SyncState.Synchronized) sincronizados
    </RadzenText>
    <RadzenText TextStyle="TextStyle.Caption" class="rz-text-secondary-color">
        <RadzenIcon Icon="hourglass_top" Style="color: var(--rz-warning);" />
        @_rows.Count(r => r.State == SyncState.InProgress) en curso
    </RadzenText>
    <RadzenText TextStyle="TextStyle.Caption" class="rz-text-secondary-color">
        <RadzenIcon Icon="schedule" />
        @_rows.Count(r => r.State == SyncState.Pending) pendientes
    </RadzenText>
    @if (_rows.Any(r => r.State == SyncState.Error))
    {
        <RadzenText TextStyle="TextStyle.Caption" class="rz-text-secondary-color">
            <RadzenIcon Icon="error_outline" Style="color: var(--rz-danger);" />
            @_rows.Count(r => r.State == SyncState.Error) con error
        </RadzenText>
    }
</RadzenStack>

@* ── Endpoint base (informativo) ── *@
<RadzenText TextStyle="TextStyle.Caption" class="rz-text-secondary-color">
    Endpoint base: <code>@EcoDataNetDatasetStore.BaseDatasetEndpointHint</code>
</RadzenText>
```

### 8. Eliminar el botón "Publicar datos"

**Eliminar completamente** del componente:
- El `RadzenButton` con texto "Publicar datos" (stub deshabilitado)
- Cualquier tooltip asociado ("Funcionalidad pendiente de integración EDC")
- Cualquier variable `_isPublishing` o lógica de stub asociada

### 9. Caso especial: Coordinador (sin datasets)

Mantener el `RadzenAlert` existente para el Coordinador (que no publica nada), pero actualizar el texto:

```razor
@if (_profile?.Publish?.Count == 0)
{
    <RadzenAlert AlertStyle="AlertStyle.Light"
                 Variant="Variant.Flat"
                 ShowIcon="true"
                 Icon="info"
                 AllowClose="false">
        Este perfil no publica datos en el espacio de datos EcoDataNet. 
        Solo consume datos de otros participantes.
    </RadzenAlert>
}
```

---

## CRITERIOS DE ACEPTACIÓN

- [ ] El botón "Publicar datos" ha sido **eliminado** de todas las pantallas de publicación (8 perfiles).
- [ ] Se muestra un `RadzenAlert` informativo explicando que la publicación es automática.
- [ ] La tabla incluye las columnas nuevas "Última sincronización" y "Estado".
- [ ] Los badges de estado usan los colores correctos: verde (Sincronizado), amarillo (En curso), rojo (Error), gris (Pendiente).
- [ ] La columna "Última sincronización" muestra fecha formateada `dd/MM/yyyy HH:mm` y número de registros, o "Pendiente" si no hay dato.
- [ ] El pie de tabla muestra el resumen de contadores por estado.
- [ ] El perfil Coordinador sigue mostrando la alerta de "no publica datos".
- [ ] Los datos de sincronización son **mock** (generados en frontend con seed fijo por perfil para consistencia visual).
- [ ] El proyecto compila sin errores (`dotnet build`).
- [ ] **NO** se ha creado ninguna tabla nueva ni servicio backend. Todo es UI mock.

---

## NOTAS PARA FUTURAS FASES

```
// TODO: En la fase de integración real:
// 1. Sustituir GenerateMockSyncStatuses() por una Query CQRS real
//    que consulte una tabla SyncLog o el estado del servicio de sincronización.
// 2. Los campos LastSyncUtc, State y RecordCount vendrán de BD.
// 3. Añadir auto-refresh con Timer para actualizar el estado en tiempo real.
// 4. Añadir botón "Forzar sincronización" (solo para ADMIN) que dispare
//    el servicio de sincronización bajo demanda.
```
