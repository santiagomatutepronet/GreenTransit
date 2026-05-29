# 🚀 Prompt Copilot — Publicar datos de GreenTransit a EcoDataNet (Waste API)

> **Generado**: 28/05/2026 | **Stack**: .NET 10 · Blazor Web App · Clean Architecture · EF Core · MediatR  
> **Fuentes**: `API_POST_Endpoints.md` (endpoints y DTOs), `database_greenTransit.sql` (modelo de datos v4.1)  
> **Instrucciones**: Adjunta `COPILOT_CONTEXT.md`, `README.md`, `Crear_BD_v4_1.sql` y este archivo. Ejecuta cada bloque en orden.

---

## 📋 Objetivo

Implementar un proceso **"Publicar a EcoDataNet"** accesible desde la **ventana de generación de datos seed** del módulo **Seguridad** en GreenTransit. Al ejecutar, el sistema:

1. Consulta TODA la información operativa de GreenTransit (16 endpoints).
2. Mapea los datos a los DTOs de la API EcoDataNet Waste.
3. Envía arrays por endpoint en lotes de hasta 100 elementos.
4. Usa `remoteId = Id` de GreenTransit (GUID) para permitir upsert.
5. Asigna `ownerId` específicos de participante EcoDataNet (NO el `OwnerId` multi-tenant de GreenTransit).
6. Gestiona respuesta `207 Multi-Status`: registra ok/error por elemento y muestra resumen en UI.

---

## 📂 Archivos a tocar / crear

### Infrastructure (nuevos)
```
GreenTransit.Infrastructure/
├── ExternalApis/
│   ├── EcoDataNet/
│   │   ├── EcoDataNetOptions.cs
│   │   ├── EcoDataNetHttpClient.cs
│   │   ├── EcoDataNetPublisher.cs
│   │   ├── Models/              ← DTOs por endpoint
│   │   │   ├── WasteMoveItem.cs
│   │   │   ├── ThirdPartyRef.cs
│   │   │   ├── WasteMoveResidueItem.cs
│   │   │   ├── EntryPlantItem.cs
│   │   │   ├── EntryPlantResidueItem.cs
│   │   │   ├── EntryCACItem.cs
│   │   │   ├── EntryCACResidueItem.cs
│   │   │   ├── TreatmentPlantItem.cs
│   │   │   ├── TreatmentPlantResidueItem.cs
│   │   │   ├── ProductDeclarationItem.cs
│   │   │   ├── ProducerRef.cs
│   │   │   ├── ProductItem.cs
│   │   │   ├── ServiceOrderItem.cs
│   │   │   ├── SettlementItem.cs
│   │   │   ├── SettlementLineItem.cs
│   │   │   ├── AgreementItem.cs
│   │   │   ├── AgreementDocumentItem.cs
│   │   │   ├── MarketShareItem.cs
│   │   │   ├── ProductSpecItem.cs
│   │   │   ├── PlantEnergyItem.cs
│   │   │   ├── IncidentItem.cs
│   │   │   ├── EmissionFactorSetItem.cs
│   │   │   ├── EmissionFactorItem.cs
│   │   │   ├── EcoModulationRuleSetItem.cs
│   │   │   ├── EcoModulationRuleItem.cs
│   │   │   ├── DumZoneItem.cs
│   │   │   └── DumRestrictionRuleItem.cs
│   │   └── EndpointResult.cs    ← modelo de resultado por endpoint
```

### Application (nuevos)
```
GreenTransit.Application/
├── Features/
│   └── Security/
│       └── Commands/
│           └── PublishToEcoDataNet/
│               ├── PublishToEcoDataNetCommand.cs
│               └── PublishToEcoDataNetCommandHandler.cs
├── Interfaces/
│   └── IEcoDataNetPublisher.cs
```

### Web (modificar existente)
```
GreenTransit.Web/
├── Components/Pages/Security/
│   └── [BUSCAR ventana seed existente] ← añadir botón "Publicar a EcoDataNet"
```

### Configuración
```
appsettings.json / User Secrets / KeyVault
├── EcoDataNet:BaseUrl
├── EcoDataNet:Username
└── EcoDataNet:Password
```

> **INSTRUCCIÓN PARA COPILOT**: Busca en el proyecto la ventana/página seed existente en el módulo Seguridad. Será un componente Blazor que genera datos de demostración. Conecta ahí el nuevo botón. NO inventes rutas ni crees una página nueva.

---

## 🏗️ Diseño propuesto

### 1. Configuración — `EcoDataNetOptions.cs`

```csharp
public class EcoDataNetOptions
{
    public const string SectionName = "EcoDataNet";
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 100;
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxRetries { get; set; } = 3;
}
```

Registro en `Program.cs` o en la extensión de servicios de Infrastructure:
```csharp
builder.Services.Configure<EcoDataNetOptions>(
    builder.Configuration.GetSection(EcoDataNetOptions.SectionName));
```

### 2. HttpClient — `EcoDataNetHttpClient.cs`

Registrar con `HttpClientFactory` y `Polly` para retries.

```csharp
public class EcoDataNetHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EcoDataNetHttpClient> _logger;

    public EcoDataNetHttpClient(HttpClient httpClient, ILogger<EcoDataNetHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Envía un batch de items a un endpoint POST.
    /// Gestiona 200 OK y 207 Multi-Status.
    /// </summary>
    public async Task<EndpointResult> PostBatchAsync<T>(
        string endpoint, ICollection<T> items, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(items, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(endpoint, content, ct);

        var result = new EndpointResult { Endpoint = endpoint, TotalSent = items.Count };

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            result.SuccessCount = items.Count;
        }
        else if ((int)response.StatusCode == 207) // Multi-Status
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            // Parsear array de resultados individuales
            result.ParseMultiStatus(body);
        }
        else
        {
            result.ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
            var body = await response.Content.ReadAsStringAsync(ct);
            result.ErrorDetail = body;
        }
        return result;
    }
}
```

Registro en DI con Basic Auth:
```csharp
builder.Services.AddHttpClient<EcoDataNetHttpClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<EcoDataNetOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

    // HTTP Basic Auth
    var credentials = Convert.ToBase64String(
        Encoding.ASCII.GetBytes($"{options.Username}:{options.Password}"));
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Basic", credentials);
})
.AddStandardResilienceHandler(); // Polly retries
```

### 3. Interfaz — `IEcoDataNetPublisher.cs`

```csharp
public interface IEcoDataNetPublisher
{
    /// <summary>
    /// Publica todos los datos de GreenTransit a EcoDataNet.
    /// Emite progreso vía el callback.
    /// </summary>
    Task<PublishSummary> PublishAllAsync(
        Action<string, int, int>? onProgress = null,
        CancellationToken ct = default);
}

public class PublishSummary
{
    public List<EndpointResult> Results { get; set; } = new();
    public int TotalEndpoints => Results.Count;
    public int TotalItemsSent => Results.Sum(r => r.TotalSent);
    public int TotalSuccess => Results.Sum(r => r.SuccessCount);
    public int TotalErrors => Results.Sum(r => r.ErrorCount);
    public TimeSpan Duration { get; set; }
}
```

### 4. Orquestador — `EcoDataNetPublisher.cs`

```csharp
public class EcoDataNetPublisher : IEcoDataNetPublisher
{
    private readonly IApplicationDbContext _db;
    private readonly EcoDataNetHttpClient _httpClient;
    private readonly IOptions<EcoDataNetOptions> _options;
    private readonly ILogger<EcoDataNetPublisher> _logger;

    // Inyección por constructor

    public async Task<PublishSummary> PublishAllAsync(
        Action<string, int, int>? onProgress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var summary = new PublishSummary();
        var batchSize = _options.Value.BatchSize;

        // Orden de ejecución (16 endpoints):
        // 1. WasteMoves
        // 2. EntryPlants
        // 3. EntryCACs
        // 4. TreatmentPlants
        // 5. ProductDeclarations
        // 6. ServiceOrders
        // 7. Settlements (requiere Agreements ya publicados)
        // 8. Agreements
        // 9. AgreementDocuments
        // 10. MarketShares
        // 11. ProductSpecs
        // 12. PlantEnergies
        // 13. Incidents
        // 14. EmissionFactorSets
        // 15. EcoModulationRuleSets
        // 16. DUMZones

        var endpoints = new (string Name, Func<Task<EndpointResult>> Action)[]
        {
            ("WasteMoves",            () => PublishWasteMovesAsync(batchSize, ct)),
            ("EntryPlants",           () => PublishEntryPlantsAsync(batchSize, ct)),
            ("EntryCACs",             () => PublishEntryCACs(batchSize, ct)),
            ("TreatmentPlants",       () => PublishTreatmentPlantsAsync(batchSize, ct)),
            ("ProductDeclarations",   () => PublishProductDeclarationsAsync(batchSize, ct)),
            ("ServiceOrders",         () => PublishServiceOrdersAsync(batchSize, ct)),
            ("Agreements",            () => PublishAgreementsAsync(batchSize, ct)),
            ("Settlements",           () => PublishSettlementsAsync(batchSize, ct)),
            ("AgreementDocuments",     () => PublishAgreementDocumentsAsync(batchSize, ct)),
            ("MarketShares",          () => PublishMarketSharesAsync(batchSize, ct)),
            ("ProductSpecs",          () => PublishProductSpecsAsync(batchSize, ct)),
            ("PlantEnergies",         () => PublishPlantEnergiesAsync(batchSize, ct)),
            ("Incidents",             () => PublishIncidentsAsync(batchSize, ct)),
            ("EmissionFactorSets",    () => PublishEmissionFactorSetsAsync(batchSize, ct)),
            ("EcoModulationRuleSets", () => PublishEcoModulationRuleSetsAsync(batchSize, ct)),
            ("DUMZones",              () => PublishDUMZonesAsync(batchSize, ct)),
        };

        for (int i = 0; i < endpoints.Length; i++)
        {
            onProgress?.Invoke(endpoints[i].Name, i + 1, endpoints.Length);
            try
            {
                var result = await endpoints[i].Action();
                summary.Results.Add(result);
                _logger.LogInformation(
                    "EcoDataNet [{Endpoint}]: enviados {Sent}, ok {Ok}, error {Err}",
                    result.Endpoint, result.TotalSent, result.SuccessCount, result.ErrorCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publicando {Endpoint}", endpoints[i].Name);
                summary.Results.Add(new EndpointResult
                {
                    Endpoint = endpoints[i].Name,
                    ErrorMessage = ex.Message
                });
            }
        }

        sw.Stop();
        summary.Duration = sw.Elapsed;
        return summary;
    }
}
```

### 5. Command CQRS — `PublishToEcoDataNetCommand.cs`

```csharp
public record PublishToEcoDataNetCommand : IRequest<PublishSummary>;

public class PublishToEcoDataNetCommandHandler
    : IRequestHandler<PublishToEcoDataNetCommand, PublishSummary>
{
    private readonly IEcoDataNetPublisher _publisher;

    public PublishToEcoDataNetCommandHandler(IEcoDataNetPublisher publisher)
        => _publisher = publisher;

    public Task<PublishSummary> Handle(
        PublishToEcoDataNetCommand request, CancellationToken ct)
        => _publisher.PublishAllAsync(ct: ct);
}
```

### 6. UI Blazor — Botón en ventana seed

Busca la ventana seed existente en el módulo Seguridad y añade:

```razor
@* ──── Sección: Publicar a EcoDataNet ──── *@
<div class="seed-section">
    <h4>Publicar datos a EcoDataNet</h4>
    <p>Envía TODA la información operativa de GreenTransit a la API EcoDataNet Waste.</p>

    <button class="btn btn-primary" @onclick="PublishToEcoDataNet"
            disabled="@_isPublishing">
        @if (_isPublishing)
        {
            <span class="spinner-border spinner-border-sm me-1"></span>
            <span>Publicando @_currentEndpoint (@_currentStep/@_totalSteps)...</span>
        }
        else
        {
            <span>🚀 Publicar a EcoDataNet</span>
        }
    </button>

    @if (_publishSummary is not null)
    {
        <div class="publish-summary mt-3">
            <h5>Resumen de publicación</h5>
            <p>Duración: @_publishSummary.Duration.ToString(@"mm\:ss")</p>
            <p>Total enviados: @_publishSummary.TotalItemsSent |
               ✅ Ok: @_publishSummary.TotalSuccess |
               ❌ Errores: @_publishSummary.TotalErrors</p>

            <table class="table table-sm">
                <thead>
                    <tr>
                        <th>Endpoint</th><th>Enviados</th><th>Ok</th><th>Errores</th><th>Detalle</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var r in _publishSummary.Results)
                    {
                        <tr class="@(r.ErrorCount > 0 ? "table-danger" : "")">
                            <td>@r.Endpoint</td>
                            <td>@r.TotalSent</td>
                            <td>@r.SuccessCount</td>
                            <td>@r.ErrorCount</td>
                            <td>@(r.ErrorMessage ?? "—")</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }
</div>

@code {
    private bool _isPublishing;
    private string _currentEndpoint = "";
    private int _currentStep, _totalSteps;
    private PublishSummary? _publishSummary;

    [Inject] private IMediator Mediator { get; set; } = default!;

    private async Task PublishToEcoDataNet()
    {
        _isPublishing = true;
        _publishSummary = null;
        StateHasChanged();

        try
        {
            _publishSummary = await Mediator.Send(new PublishToEcoDataNetCommand());
        }
        catch (Exception ex)
        {
            // Mostrar error global
        }
        finally
        {
            _isPublishing = false;
            StateHasChanged();
        }
    }
}
```

---

## 🗺️ Mapeos por endpoint

### OwnerId de participante EcoDataNet

**IMPORTANTE**: No usar el `OwnerId` multi-tenant de GreenTransit. Cada endpoint usa un GUID fijo de participante EcoDataNet.

| Endpoint | OwnerId EcoDataNet | Nota |
|---|---|---|
| WasteMoves | Cíclico: `F49F3B63-120B-49B2-9B27-F42D2E80153C`, `4E3C335B-A84A-4E3E-B960-71F7086C6489`, `7E1AA26A-D006-42DF-BCA2-8FF1F56FCD00` | Alternar round-robin |
| EntryPlants | `B5BF81E1-92B9-4D5D-A873-B23F187D8088` | Fijo |
| EntryCACs | `ACB7BFE6-AAE2-4A9B-BC23-A1D7EAA7DEEF` | Fijo |
| TreatmentPlants | `B5BF81E1-92B9-4D5D-A873-B23F187D8088` | Fijo |
| ProductDeclarations | `D5FD04C2-752B-4277-6EBB-08DE64D3ACCE` | Fijo |
| ServiceOrders | `0168F029-6DD2-4EBF-9CF1-FC8DCC0819AB` | Fijo |
| Settlements | `0168F029-6DD2-4EBF-9CF1-FC8DCC0819AB` | Fijo |
| Agreements | `0168F029-6DD2-4EBF-9CF1-FC8DCC0819AB` | Fijo |
| AgreementDocuments | *(heredado del Agreement padre)* | Sin ownerId propio |
| MarketShares | Cíclico: `4E3C335B-A84A-4E3E-B960-71F7086C6489`, `7E1AA26A-D006-42DF-BCA2-8FF1F56FCD00` | Alternar round-robin |
| ProductSpecs | `5ACB6BA6-8EF7-42EC-8DA7-B47DEBC7D160` | Fijo |
| PlantEnergies | `B5BF81E1-92B9-4D5D-A873-B23F187D8088` | Fijo |
| Incidents | `0168F029-6DD2-4EBF-9CF1-FC8DCC0819AB` | Fijo |
| EmissionFactorSets | `0168F029-6DD2-4EBF-9CF1-FC8DCC0819AB` | Fijo |
| EcoModulationRuleSets | Cíclico: `4E3C335B-A84A-4E3E-B960-71F7086C6489`, `7E1AA26A-D006-42DF-BCA2-8FF1F56FCD00` | Alternar round-robin |
| DUMZones | `64ED5419-D01C-4009-AFE7-173F1857C84F` | Fijo |
| DUMRestrictionRules *(dentro de DUMZones)* | `64ED5419-D01C-4009-AFE7-173F1857C84F` | Fijo |

Helper para asignación cíclica:
```csharp
private static readonly Guid[] WasteMoveOwnerIds = new[]
{
    Guid.Parse("F49F3B63-120B-49B2-9B27-F42D2E80153C"),
    Guid.Parse("4E3C335B-A84A-4E3E-B960-71F7086C6489"),
    Guid.Parse("7E1AA26A-D006-42DF-BCA2-8FF1F56FCD00"),
};

// Uso: WasteMoveOwnerIds[index % WasteMoveOwnerIds.Length]
```

---

### Endpoint 1: WasteMoves — `POST /api/WasteMoves/Register`

**Tablas origen GreenTransit**: `WasteMoves` + `WasteMoveResidues` + `Entities` (Scrap, Scrap2, Source, Destination, OperatorTransfer, Carrier) + `Residues` + `LERCodes`

**Consulta EF Core**:
```csharp
var moves = await _db.WasteMoves
    .AsNoTracking()
    .Include(wm => wm.Scrap)       // Entity vía IdScrap
    .Include(wm => wm.Scrap2)      // Entity vía IdScrap2
    .Include(wm => wm.Source)      // Entity vía IdSource
    .Include(wm => wm.Destination) // Entity vía IdDestination
    .Include(wm => wm.OperatorTransfer) // Entity vía IdOperatorTransfer
    .Include(wm => wm.WasteMoveResidues)
        .ThenInclude(r => r.Residue)     // Residue vía IdResidue
    .Include(wm => wm.WasteMoveResidues)
        .ThenInclude(r => r.LerCode)     // LERCode vía LerCodeId
    .Include(wm => wm.WasteMoveResidues)
        .ThenInclude(r => r.Carrier)     // Entity vía IdCarrier
    .ToListAsync(ct);
```

**Mapeo campo→campo**:

| DTO (`WasteMoveItem`) | Origen (BD GreenTransit) | Notas |
|---|---|---|
| `remoteId` | `WasteMoves.Id` | GUID para upsert |
| `ownerId` | *Asignación cíclica* (ver tabla arriba) | NO usar WasteMoves.OwnerId |
| `gatheredDate` | `WasteMoves.GatheredDate` | |
| `requestDate` | `WasteMoves.RequestDate` | |
| `plantEntryDate` | `WasteMoves.PlantEntryDate` | |
| `wasteMoveReference` | `WasteMoves.WasteMoveReference` | |
| `lot` | `WasteMoves.Lot` | |
| `serviceOrderId` | `WasteMoves.ServiceOrderId` | |
| `serviceStatus` | `WasteMoves.ServiceStatus` | |
| `plannedPickupStart` | `WasteMoves.PlannedPickupStart` | |
| `plannedPickupEnd` | `WasteMoves.PlannedPickupEnd` | |
| `plannedDeliveryStart` | `WasteMoves.PlannedDeliveryStart` | |
| `plannedDeliveryEnd` | `WasteMoves.PlannedDeliveryEnd` | |
| `actualPickupStart` | `WasteMoves.ActualPickupStart` | |
| `actualPickupEnd` | `WasteMoves.ActualPickupEnd` | |
| `actualDeliveryStart` | `WasteMoves.ActualDeliveryStart` | |
| `actualDeliveryEnd` | `WasteMoves.ActualDeliveryEnd` | |
| `documentId` | `WasteMoves.DocumentId` | |
| `documentHash` | `WasteMoves.DocumentHash` | |
| `signatureStatus` | `WasteMoves.SignatureStatus` | |
| `sourceSystem` | `WasteMoves.SourceSystem` ?? `"GreenTransit"` | Valor por defecto |
| `version` | `WasteMoves.Version` | |
| `scrap.typeThirdParty` | `Entities(IdScrap).TypeThirdParty` | Convertir a `TypeThirdPartyEnum` (int) |
| `scrap.name` | `Entities(IdScrap).Name` | |
| `scrap.nationalId` | `Entities(IdScrap).NationalId` | |
| `scrap.centerCode` | `Entities(IdScrap).CenterCode` | |
| `scrap2.*` | `Entities(IdScrap2).*` | Mismo patrón que scrap |
| `source.typeThirdParty` | `Entities(IdSource).TypeThirdParty` | |
| `source.name` | `Entities(IdSource).Name` | |
| `source.nationalId` | `Entities(IdSource).NationalId` | |
| `source.centerCode` | `Entities(IdSource).CenterCode` | |
| `source.entityType` | `Entities(IdSource).EntityType` | |
| `source.inscriptionType` | `Entities(IdSource).InscriptionType` | |
| `source.inscriptionNumber` | `Entities(IdSource).InscriptionNumber` | |
| `source.countryCode` | `Entities(IdSource).CountryCode` | |
| `source.stateCode` | `Entities(IdSource).StateCode` | |
| `source.zipCode` | `Entities(IdSource).ZipCode` | |
| `destination.*` | `Entities(IdDestination).*` | Mismo esquema que source |
| `operatorTransfer.*` | `Entities(IdOperatorTransfer).*` | Mismo esquema que source |
| **residues[].** | | |
| `residues[].idWasteMove` | `WasteMoveResidues.IdWasteMove` | |
| `residues[].lerCode` | `LERCodes.Code` (vía `WasteMoveResidues.LerCodeId`) | JOIN LERCodes |
| `residues[].lerCodeExtended` | `LERCodes.CodeExtended` | |
| `residues[].dangerous` | `Residues.IsDangerous` (vía `WasteMoveResidues.IdResidue`) | JOIN Residues |
| `residues[].raee` | `Residues.IsRAEE` | |
| `residues[].productUse` | `Residues.ProductUse` | Convertir string→int (UseProductEnum) |
| `residues[].productCategory` | `Residues.ProductCategory` | Convertir string→int (CategoryProductEnum) |
| `residues[].description` | `Residues.Description` | |
| `residues[].residueName` | `Residues.Name` | |
| `residues[].weight` | `WasteMoveResidues.Weight` | |
| `residues[].measureUnit` | `WasteMoveResidues.MeasureUnit` | Convertir string→int (MeasureUnitEnum) |
| `residues[].units` | `WasteMoveResidues.Units` | |
| `residues[].unitPriceKg` | `WasteMoveResidues.unitPriceKg` | |
| `residues[].ntNumber` | `WasteMoveResidues.NTNumber` | |
| `residues[].diNumber` | `WasteMoveResidues.DINumber` | |
| `residues[].diPhase` | `WasteMoveResidues.DIPhase` | |
| `residues[].dangerousCode` | `Residues.DangerousCode` | |

**DTO helper reutilizable — `ThirdPartyRef.cs`**:
```csharp
public class ThirdPartyRef
{
    public int? TypeThirdParty { get; set; }
    public string? Name { get; set; }
    public string? NationalId { get; set; }
    public string? CenterCode { get; set; }
    public string? EntityType { get; set; }
    public string? InscriptionType { get; set; }
    public string? InscriptionNumber { get; set; }
    public string? CountryCode { get; set; }
    public string? StateCode { get; set; }
    public string? ZipCode { get; set; }
}
```

Método helper para mapear Entity→ThirdPartyRef:
```csharp
private static ThirdPartyRef? MapEntity(Entity? entity)
{
    if (entity is null) return null;
    return new ThirdPartyRef
    {
        TypeThirdParty = ParseTypeThirdParty(entity.TypeThirdParty),
        Name = entity.Name,
        NationalId = entity.NationalId,
        CenterCode = entity.CenterCode,
        EntityType = entity.EntityType,
        InscriptionType = entity.InscriptionType,
        InscriptionNumber = entity.InscriptionNumber,
        CountryCode = entity.CountryCode,
        StateCode = entity.StateCode,
        ZipCode = entity.ZipCode,
    };
}

// TypeThirdPartyEnum: 1=PuntoDeRecogida, 2=Gestor, 3=SCRAP, 4=OperadorTraslado
private static int? ParseTypeThirdParty(string? value) => value switch
{
    "PuntoDeRecogida" => 1,
    "Gestor" => 2,
    "SCRAP" => 3,
    "OperadorTraslado" => 4,
    _ => null
};
```

---

### Endpoint 2: EntryPlants — `POST /api/EntryPlants/Register`

**Tablas origen**: `EntryPlants` + `EntryPlantResidues` + `Residues` + `LERCodes` + `WasteMoves` (para WasteMoveReference)

**Consulta EF Core**:
```csharp
var entries = await _db.EntryPlants
    .AsNoTracking()
    .Include(ep => ep.EntryPlantResidues)
        .ThenInclude(r => r.Residue)
            .ThenInclude(res => res.LERCode) // LERCode vía Residue.IdLERCode
    .Include(ep => ep.WasteMove) // Para WasteMoveReference si no está en EntryPlants
    .ToListAsync(ct);
```

**Mapeo campo→campo**:

| DTO (`EntryPlantItem`) | Origen | Notas |
|---|---|---|
| `remoteId` | `EntryPlants.Id` | |
| `ownerId` | `B5BF81E1-92B9-4D5D-A873-B23F187D8088` | Fijo |
| `wasteMoveReference` | `EntryPlants.WasteMoveReference` | |
| `ticketScale` | `EntryPlants.TicketScale` | |
| `plantEntryDate` | `EntryPlants.PlantEntryDate` | |
| `typeContainer` | `EntryPlants.TypeContainer` | Convertir string→int (TypeContainerEnum) |
| `typeContainerRef` | `EntryPlants.TypeContainer` | Se puede omitir (API lo resuelve) |
| `priceContainer` | `EntryPlants.PriceContainer` | |
| **residues[].** | | |
| `residues[].idEntryPlant` | `EntryPlantResidues.IdEntryPlant` | |
| `residues[].residueName` | `Residues.Name` | JOIN vía IdResidue |
| `residues[].lerCode` | `LERCodes.Code` | JOIN vía Residue.IdLERCode |
| `residues[].lerCodeExtended` | `LERCodes.CodeExtended` | |
| `residues[].weight` | `EntryPlantResidues.Weight` | |
| `residues[].measureUnit` | `EntryPlantResidues.MeasureUnit` | Convertir string→int |
| `residues[].units` | `EntryPlantResidues.Units` | |
| `residues[].priceWeight` | `EntryPlantResidues.PriceWeight` | |
| `residues[].priceUnit` | `EntryPlantResidues.PriceUnit` | |
| `residues[].dangerous` | `Residues.IsDangerous` | |
| `residues[].raee` | `Residues.IsRAEE` | |
| `residues[].productCategory` | `Residues.ProductCategory` | Convertir string→int |

---

### Endpoint 3: EntryCACs — `POST /api/EntryCACs/Register`

**Tablas origen**: `EntryCACs` + `EntryCACResidues` + `Residues` + `LERCodes`

**Consulta EF Core**:
```csharp
var entries = await _db.EntryCACs
    .AsNoTracking()
    .Include(ec => ec.EntryCACResidues)
        .ThenInclude(r => r.Residue)
            .ThenInclude(res => res.LERCode)
    .ToListAsync(ct);
```

**Mapeo campo→campo**:

| DTO (`EntryCACItem`) | Origen | Notas |
|---|---|---|
| `remoteId` | `EntryCACs.Id` | |
| `ownerId` | `ACB7BFE6-AAE2-4A9B-BC23-A1D7EAA7DEEF` | Fijo |
| `wasteMoveReference` | `EntryCACs.WasteMoveReference` | |
| `cacEntryDate` | `EntryCACs.CACEntryDate` | |
| `typeContainer` | `EntryCACs.TypeContainer` | Convertir string→int |
| `priceContainer` | `EntryCACs.PriceContainer` | |
| `collectionMethod` | `EntryCACs.CollectionMethod` | |
| **residues[].** | | Mismo patrón que EntryPlants |
| `residues[].idEntryCAC` | `EntryCACResidues.IdEntryCAC` | |
| `residues[].residueName` | `Residues.Name` | |
| `residues[].lerCode` | `LERCodes.Code` | |
| `residues[].lerCodeExtended` | `LERCodes.CodeExtended` | |
| `residues[].weight` | `EntryCACResidues.Weight` | |
| `residues[].measureUnit` | `EntryCACResidues.MeasureUnit` | Convertir string→int |
| `residues[].units` | `EntryCACResidues.Units` | |
| `residues[].priceWeight` | `EntryCACResidues.PriceWeight` | |
| `residues[].priceUnit` | `EntryCACResidues.PriceUnit` | |
| `residues[].dangerous` | `Residues.IsDangerous` | |
| `residues[].raee` | `Residues.IsRAEE` | |
| `residues[].productCategory` | `Residues.ProductCategory` | Convertir string→int |

---

### Endpoint 4: TreatmentPlants — `POST /api/TreatmentPlants/Register`

**Tablas origen**: `TreatmentPlants` + `TreatmentPlantResidues` + `Residues` + `LERCodes`

**Consulta EF Core**:
```csharp
var treatments = await _db.TreatmentPlants
    .AsNoTracking()
    .Include(tp => tp.TreatmentPlantResidues)
        .ThenInclude(r => r.Residue)
            .ThenInclude(res => res.LERCode)
    .ToListAsync(ct);
```

**Mapeo campo→campo**:

| DTO (`TreatmentPlantItem`) | Origen | Notas |
|---|---|---|
| `remoteId` | `TreatmentPlants.Id` | |
| `ownerId` | `B5BF81E1-92B9-4D5D-A873-B23F187D8088` | Fijo |
| `wasteMoveReference` | `TreatmentPlants.WasteMoveReference` | |
| `ticketScale` | `TreatmentPlants.TicketScale` | |
| `plantTreatmentDate` | `TreatmentPlants.PlantTreatmentDate` | |
| `typeContainer` | `TreatmentPlants.TypeContainer` | Convertir string→int |
| `priceContainer` | `TreatmentPlants.PriceContainer` | |
| **residues[].** | | |
| `residues[].idTreatmentPlant` | `TreatmentPlantResidues.IdTreatmentPlant` | |
| `residues[].residueName` | `Residues.Name` | |
| `residues[].lerCode` | `LERCodes.Code` | |
| `residues[].lerCodeExtended` | `LERCodes.CodeExtended` | |
| `residues[].category` | `TreatmentPlantResidues.Category` | Convertir string→int |
| `residues[].weightTotal` | `TreatmentPlantResidues.WeightTotal` | |
| `residues[].measureUnit` | `TreatmentPlantResidues.MeasureUnit` | Convertir string→int |
| `residues[].units` | `TreatmentPlantResidues.Units` | |
| `residues[].priceWeight` | `TreatmentPlantResidues.PriceWeight` | |
| `residues[].priceUnit` | `TreatmentPlantResidues.PriceUnit` | |
| `residues[].weightReused` | `TreatmentPlantResidues.WeightReused` | |
| `residues[].measureUnitReused` | `TreatmentPlantResidues.MeasureUnitReused` | Convertir string→int |
| `residues[].unitsReused` | `TreatmentPlantResidues.UnitsReused` | |

---

### Endpoint 5: ProductDeclarations — `POST /api/ProductDeclarations/Register`

**Tablas origen**: `ProductDeclaration` + `Products` + `Entities` (IdProducer) + `Residues` (vía Products.IdResidue)

**Consulta EF Core**:
```csharp
var declarations = await _db.ProductDeclarations
    .AsNoTracking()
    .Include(pd => pd.Producer) // Entity vía IdProducer
    .Include(pd => pd.Products)
        .ThenInclude(p => p.Residue) // Residue vía Products.IdResidue (para datos extra)
    .ToListAsync(ct);
```

**Mapeo campo→campo**:

| DTO (`ProductDeclarationItem`) | Origen | Notas |
|---|---|---|
| `remoteId` | `ProductDeclaration.Id` | |
| `ownerId` | `D5FD04C2-752B-4277-6EBB-08DE64D3ACCE` | Fijo |
| `period` | `ProductDeclaration.Period` | |
| `year` | `ProductDeclaration.Year` | |
| `month` | `ProductDeclaration.Month` | |
| `reference` | `ProductDeclaration.Reference` | |
| `currency` | `ProductDeclaration.Currency` | |
| `state` | `ProductDeclaration.State` | |
| `dateCreate` | `ProductDeclaration.DateCreate` | |
| `dateEmit` | `ProductDeclaration.DateEmit` | |
| `amount` | `ProductDeclaration.Amount` | |
| `type` | `ProductDeclaration.Type` | |
| `producer.name` | `Entities(IdProducer).Name` | |
| `producer.nationalId` | `Entities(IdProducer).NationalId` | |
| `producer.countryCode` | `Entities(IdProducer).CountryCode` | |
| `producer.stateCode` | `Entities(IdProducer).StateCode` | |
| `producer.zipCode` | `Entities(IdProducer).ZipCode` | |
| `producer.municipalityCode` | `Entities(IdProducer).MunicipalityCode` | |
| `producer.address` | `Entities(IdProducer).Address` | |
| **products[].** | | |
| `products[].id` | `Products.Id` | |
| `products[].idProductDeclaration` | `Products.IdProductDeclaration` | |
| `products[].description` | `Residues.Description` (vía Products.IdResidue) | O Products.ProductName |
| `products[].reference` | `Products.Reference` | |
| `products[].source` | `Products.Source` | |
| `products[].quantity` | `Products.Quantity` | |
| `products[].price` | `Products.Price` | |
| `products[].measureUnit` | `Products.MeasureUnit` | |
| `products[].units` | `Products.Units` | |
| `products[].productUse` | `Products.ProductUse` | |
| `products[].productCategory` | `Products.ProductCategory` | |
| `products[].weightPerUnitKg` | `Residues.WeightPerUnitKg` | JOIN vía Products.IdResidue |
| `products[].reparabilityIndex` | `Residues.ReparabilityIndex` | |
| `products[].recycledContentPercent` | `Residues.RecycledContentPercent` | |
| `products[].materialsJson` | `Residues.MaterialsJson` | |

---

### Endpoint 6: ServiceOrders — `POST /api/ServiceOrders/Register`

**Tablas origen**: `ServiceOrders` + `Entities` (IdIssuedBy, IdCarrier, IdPlannedPlant, IdPickupPoint) + `LERCodes`

**Consulta EF Core**:
```csharp
var orders = await _db.ServiceOrders
    .AsNoTracking()
    .Include(so => so.IssuedBy)     // Entity vía IdIssuedBy
    .Include(so => so.Carrier)      // Entity vía IdCarrier
    .Include(so => so.PlannedPlant) // Entity vía IdPlannedPlant
    .Include(so => so.PickupPoint)  // Entity vía IdPickupPoint
    .Include(so => so.LERCode)      // LERCode vía IdLERCode
    .ToListAsync(ct);
```

**Mapeo campo→campo**:

| DTO (`ServiceOrderItem`) | Origen | Notas |
|---|---|---|
| `remoteId` | `ServiceOrders.Id` | |
| `ownerId` | `0168F029-6DD2-4EBF-9CF1-FC8DCC0819AB` | Fijo |
| `serviceOrderNumber` | `ServiceOrders.ServiceOrderNumber` | |
| `issuedAt` | `ServiceOrders.IssuedAt` | |
| `issuedByName` | `ServiceOrders.IssuedByName` ó `Entities(IdIssuedBy).Name` | Preferir campo directo |
| `issuedByNationalId` | `ServiceOrders.IssuedByNationalId` ó `Entities(IdIssuedBy).NationalId` | |
| `issuedByCenterCode` | `ServiceOrders.IssuedByCenterCode` ó `Entities(IdIssuedBy).CenterCode` | |
| `status` | `ServiceOrders.Status` | |
| `priority` | `ServiceOrders.Priority` | |
| `wasteStream` | `ServiceOrders.WasteStream` | |
| `subStream` | `ServiceOrders.SubStream` | |
| `productUse` | `ServiceOrders.ProductUse` | Ya es int |
| `productCategory` | `ServiceOrders.ProductCategory` | Ya es int |
| `lerCode` | `LERCodes.Code` (vía IdLERCode) | |
| `lerCodeExtended` | `LERCodes.CodeExtended` | |
| `pointName` | `Entities(IdPickupPoint).Name` | |
| `pointType` | `Entities(IdPickupPoint).EntityType` | |
| `pointAddress` | `Entities(IdPickupPoint).Address` | |
| `municipalityCode` | `Entities(IdPickupPoint).MunicipalityCode` | |
| `latitude` | `Entities(IdPickupPoint).Latitude` | |
| `longitude` | `Entities(IdPickupPoint).Longitude` | |
| `plannedPickupStart/End` | `ServiceOrders.PlannedPickupStart/End` | |
| `plannedDeliveryStart/End` | `ServiceOrders.PlannedDeliveryStart/End` | |
| `estimatedWeight` | `ServiceOrders.EstimatedWeight` | |
| `measureUnit` | `ServiceOrders.MeasureUnit` | Ya es int |
| `units` | `ServiceOrders.Units` | |
| `containersJson` | `ServiceOrders.ContainersJson` | String JSON directo |
| `assignedCarrierName` | `Entities(IdCarrier).Name` | |
| `assignedCarrierNationalId` | `Entities(IdCarrier).NationalId` | |
| `assignedCarrierCenterCode` | `Entities(IdCarrier).CenterCode` | |
| `assignedCarrierInscriptionType` | `Entities(IdCarrier).InscriptionType` | |
| `assignedCarrierInscriptionNumber` | `Entities(IdCarrier).InscriptionNumber` | |
| `plannedPlantName` | `Entities(IdPlannedPlant).Name` | |
| `plannedPlantCenterCode` | `Entities(IdPlannedPlant).CenterCode` | |
| `wasteMoveReference` | `ServiceOrders.WasteMoveReference` | |
| `ticketScalePlanned` | `ServiceOrders.TicketScalePlanned` | |
| `actualPickupStart/End` | `ServiceOrders.ActualPickupStart/End` | |
| `actualDeliveryStart/End` | `ServiceOrders.ActualDeliveryStart/End` | |
| `transportDistanceKm` | `ServiceOrders.TransportDistanceKm` | |
| `transportDurationMin` | `ServiceOrders.TransportDurationMin` | |
| `vehicleRegistration` | `ServiceOrders.VehicleRegistration` | |
| `vehicleType` | `ServiceOrders.VehicleType` | |
| `fuelType` | `ServiceOrders.FuelType` | |
| `euroClass` | `ServiceOrders.EuroClass` | |
| `sourceSystem` | `ServiceOrders.SourceSystem` ?? `"GreenTransit"` | |
| `version` | `ServiceOrders.Version` | |
| `hash` | `ServiceOrders.Hash` | |
| `createdAt` | `ServiceOrders.CreatedAt` | |
| `updatedAt` | `ServiceOrders.UpdatedAt` | |

---

### Endpoint 7: Settlements — `POST /api/Settlements/Register`

**Tablas origen**: `Settlements` + `SettlementLines` + `Entities` (IdScrap, IdPublicEntity) + `LERCodes`

**Consulta EF Core**:
```csharp
var settlements = await _db.Settlements
    .AsNoTracking()
    .Include(s => s.Scrap)        // Entity vía IdScrap
    .Include(s => s.PublicEntity)  // Entity vía IdPublicEntity
    .Include(s => s.Lines)
        .ThenInclude(l => l.LERCode) // LERCode vía IdLERCode
    .ToListAsync(ct);
```

**Mapeo campo→campo**:

| DTO (`SettlementItem`) | Origen | Notas |
|---|---|---|
| `remoteId` | `Settlements.Id` | |
| `ownerId` | `0168F029-6DD2-4EBF-9CF1-FC8DCC0819AB` | Fijo |
| `settlementNumber` | `Settlements.SettlementNumber` | |
| `status` | `Settlements.Status` | |
| `agreementId` | `Settlements.AgreementId` | |
| `year` | `Settlements.Year` | |
| `month` | `Settlements.Month` | |
| `scrapName` | `Entities(IdScrap).Name` | |
| `scrapNationalId` | `Entities(IdScrap).NationalId` | |
| `publicEntityName` | `Entities(IdPublicEntity).Name` | |
| `publicEntityNationalId` | `Entities(IdPublicEntity).NationalId` | |
| `currency` | `Settlements.Currency` | |
| `baseAmount` | `Settlements.BaseAmount` | |
| `adjustmentsAmount` | `Settlements.AdjustmentsAmount` | |
| `taxAmount` | `Settlements.TaxAmount` | |
| `totalAmount` | `Settlements.TotalAmount` | |
| `evidenceRefsJson` | `Settlements.EvidenceRefsJson` | |
| `validator` | `Settlements.Validator` | |
| `validationStatus` | `Settlements.ValidationStatus` | |
| `validatedAt` | `Settlements.ValidatedAt` | |
| `validationRef` | `Settlements.ValidationRef` | |
| `sourceSystem` | `Settlements.SourceSystem` ?? `"GreenTransit"` | |
| `version` | `Settlements.Version` | |
| `hash` | `Settlements.Hash` | |
| `createdAt` | `Settlements.CreatedAt` | |
| `updatedAt` | `Settlements.UpdatedAt` | |
| **lines[].** | | |
| `lines[].remoteId` | `SettlementLines.Id` | |
| `lines[].settlementId` | `SettlementLines.SettlementId` | |
| `lines[].productCategory` | `SettlementLines.ProductCategory` | Ya es int |
| `lines[].lerCode` | `LERCodes.Code` (vía SettlementLines.IdLERCode) | |
| `lines[].weightKg` | `SettlementLines.WeightKg` | |
| `lines[].pricePerKg` | `SettlementLines.PricePerKg` | |
| `lines[].amount` | `SettlementLines.Amount` | |
| `lines[].evidenceType` | `SettlementLines.EvidenceType` | |
| `lines[].sourceIdsJson` | `SettlementLines.SourceIdsJson` | |

---

### Endpoint 8: Agreements — `POST /api/Agreements/Register`

**Tablas origen**: `Agreements` + `AgreementDocuments` + `Entities` (IdScrap, IdPublicEntity, IdCoordinator)

**Consulta EF Core**:
```csharp
var agreements = await _db.Agreements
    .AsNoTracking()
    .Include(a => a.Scrap)
    .Include(a => a.PublicEntity)
    .Include(a => a.Coordinator)
    .Include(a => a.Documents) // AgreementDocuments
    .ToListAsync(ct);
```

**Mapeo campo→campo**:

| DTO (`AgreementItem`) | Origen | Notas |
|---|---|---|
| `remoteId` | `Agreements.Id` | |
| `ownerId` | `0168F029-6DD2-4EBF-9CF1-FC8DCC0819AB` | Fijo |
| `agreementNumber` | `Agreements.AgreementNumber` | |
| `status` | `Agreements.Status` | |
| `effectiveFrom` | `Agreements.EffectiveFrom` | |
| `effectiveTo` | `Agreements.EffectiveTo` | |
| `scrapName` | `Entities(IdScrap).Name` | |
| `scrapNationalId` | `Entities(IdScrap).NationalId` | |
| `scrapCenterCode` | `Entities(IdScrap).CenterCode` | |
| `publicEntityName` | `Entities(IdPublicEntity).Name` | |
| `publicEntityNationalId` | `Entities(IdPublicEntity).NationalId` | |
| `publicEntityCenterCode` | `Entities(IdPublicEntity).CenterCode` | |
| `coordinatorName` | `Entities(IdCoordinator).Name` | |
| `coordinatorNationalId` | `Entities(IdCoordinator).NationalId` | |
| `coordinatorCenterCode` | `Entities(IdCoordinator).CenterCode` | |
| `wasteStream` | `Agreements.WasteStream` | |
| `subStream` | `Agreements.SubStream` | |
| `autonomousCommunity` | `Agreements.AutonomousCommunity` | |
| `provinceCode` | `Agreements.ProvinceCode` | |
| `municipalityCode` | `Agreements.MunicipalityCode` | |
| `coveredMethodsJson` | `Agreements.CoveredMethodsJson` | |
| `tariffModelType` | `Agreements.TariffModelType` | |
| `currency` | `Agreements.Currency` | |
| `tariffRulesJson` | `Agreements.TariffRulesJson` | |
| `minimumsJson` | `Agreements.MinimumsJson` | |
| `obligationsJson` | `Agreements.ObligationsJson` | |
| `sourceSystem` | `Agreements.SourceSystem` ?? `"GreenTransit"` | |
| `version` | `Agreements.Version` | |
| `hash` | `Agreements.Hash` | |
| `createdAt` | `Agreements.CreatedAt` | |
| `updatedAt` | `Agreements.UpdatedAt` | |
| **documents[].** | | |
| `documents[].remoteId` | `AgreementDocuments.Id` | |
| `documents[].agreementId` | `AgreementDocuments.AgreementId` | |
| `documents[].documentType` | `AgreementDocuments.DocumentType` | |
| `documents[].documentId` | `AgreementDocuments.DocumentId` | |
| `documents[].documentHash` | `AgreementDocuments.DocumentHash` | |
| `documents[].signedAt` | `AgreementDocuments.SignedAt` | |
| `documents[].signatureProvider` | `AgreementDocuments.SignatureProvider` | |

---

### Endpoint 9: AgreementDocuments — `POST /api/AgreementDocuments/Register`

**Nota**: Este endpoint envía documentos sueltos. Se envía SOLO si hay documentos huérfanos o como refuerzo post-Agreements.

| DTO (`AgreementDocumentItem`) | Origen | Notas |
|---|---|---|
| Mismo mapeo que `documents[]` en Agreements | `AgreementDocuments.*` | Mapeo directo 1:1 |

---

### Endpoint 10: MarketShares — `POST /api/MarketShares/Register`

**Tablas origen**: `MarketShares` + `Entities` (IdScrap)

**Consulta EF Core**:
```csharp
var shares = await _db.MarketShares
    .AsNoTracking()
    .Include(ms => ms.Scrap) // Entity vía IdScrap
    .ToListAsync(ct);
```

| DTO (`MarketShareItem`) | Origen | Notas |
|---|---|---|
| `remoteId` | `MarketShares.Id` | |
| `ownerId` | *Cíclico*: `4E3C335B-...`, `7E1AA26A-...` | Alternar round-robin |
| `scrap` | `Entities(IdScrap).Name` | |
| `category` | `MarketShares.Category` | Convertir string→int (CategoryProductEnum) |
| `autonomousCommunity` | `MarketShares.AutonomousCommunity` | |
| `year` | `MarketShares.Year` | |
| `weight` | `MarketShares.Weight` | |

---

### Endpoint 11: ProductSpecs — `POST /api/ProductSpecs/Register`

**Tablas origen**: `ProductSpecs` + `Entities` (IdProducer) + `Residues` (IdResidue para composición y datos extra)

**Consulta EF Core**:
```csharp
var specs = await _db.ProductSpecs
    .AsNoTracking()
    .Include(ps => ps.Producer) // Entity vía IdProducer
    .Include(ps => ps.Residue) // Residue vía IdResidue
    .ToListAsync(ct);
```

| DTO (`ProductSpecItem`) | Origen | Notas |
|---|---|---|
| `remoteId` | `ProductSpecs.Id` | |
| `ownerId` | `5ACB6BA6-8EF7-42EC-8DA7-B47DEBC7D160` | Fijo |
| `productRef` | `ProductSpecs.ProductRef` | |
| `productUse` | `ProductSpecs.ProductUse` | Ya es int |
| `productCategory` | `ProductSpecs.ProductCategory` | Ya es int |
| `categoryRef` | `ProductSpecs.CategoryRef` | |
| `producerName` | `Entities(IdProducer).Name` | |
| `producerNationalId` | `Entities(IdProducer).NationalId` | |
| `producerRef` | `ProductSpecs.ProducerRef` | |
| `compositionJson` | `Residues.CompositionJson` (vía IdResidue) | |
| `weightPerUnitKg` | `Residues.WeightPerUnitKg` | |
| `reparabilityIndex` | `Residues.ReparabilityIndex` | |
| `disassemblyEase` | `Residues.DisassemblyEase` | |
| `containsHazardous` | `Residues.ContainsHazardous` | |
| `potentialLERCodesJson` | `Residues.PotentialLERCodesJson` | |
| `notes` | `ProductSpecs.Notes` | |
| `sourceSystem` | `ProductSpecs.SourceSystem` ?? `"GreenTransit"` | |
| `version` | `ProductSpecs.Version` | |
| `hash` | `ProductSpecs.Hash` | |
| `createdAt` | `ProductSpecs.CreatedAt` | |
| `updatedAt` | `ProductSpecs.UpdatedAt` | |

---

### Endpoint 12: PlantEnergies — `POST /api/PlantEnergies/Register`

**Tablas origen**: `PlantEnergies` (mapeo directo, sin joins)

| DTO (`PlantEnergyItem`) | Origen | Notas |
|---|---|---|
| `remoteId` | `PlantEnergies.Id` | |
| `ownerId` | `B5BF81E1-92B9-4D5D-A873-B23F187D8088` | Fijo |
| `plantName` | `PlantEnergies.PlantName` | |
| `plantCenterCode` | `PlantEnergies.PlantCenterCode` | |
| `year` | `PlantEnergies.Year` | |
| `month` | `PlantEnergies.Month` | |
| `kwhTotal` | `PlantEnergies.KwhTotal` | |
| `source` | `PlantEnergies.Source` | |
| `gridMixRef` | `PlantEnergies.GridMixRef` | |
| `allocationMethod` | `PlantEnergies.AllocationMethod` | |
| `notes` | `PlantEnergies.Notes` | |
| `sourceSystem` | `PlantEnergies.SourceSystem` ?? `"GreenTransit"` | |
| `version` | `PlantEnergies.Version` | |
| `hash` | `PlantEnergies.Hash` | |
| `createdAt` | `PlantEnergies.CreatedAt` | |
| `updatedAt` | `PlantEnergies.UpdatedAt` | |

---

### Endpoint 13: Incidents — `POST /api/Incidents/Register`

**Tablas origen**: `Incidents` + `Entities` (para ReportedBy si existe FK)

| DTO (`IncidentItem`) | Origen | Notas |
|---|---|---|
| `remoteId` | `Incidents.Id` | |
| `ownerId` | `0168F029-6DD2-4EBF-9CF1-FC8DCC0819AB` | Fijo |
| `type` | `Incidents.Type` | |
| `severity` | `Incidents.Severity` | |
| `openedAt` | `Incidents.OpenedAt` | |
| `closedAt` | `Incidents.ClosedAt` | |
| `serviceOrderId` | `Incidents.ServiceOrderId` | |
| `wasteMoveReference` | `Incidents.WasteMoveReference` | |
| `ticketScale` | `Incidents.TicketScale` | |
| `reportedByName` | `Incidents.ReportedByName` | Campo directo en tabla |
| `reportedByNationalId` | `Incidents.ReportedByNationalId` | |
| `reportedByCenterCode` | `Incidents.ReportedByCenterCode` | |
| `description` | `Incidents.Description` | |
| `resolutionJson` | `Incidents.ResolutionJson` | |
| `sourceSystem` | `Incidents.SourceSystem` ?? `"GreenTransit"` | |
| `version` | `Incidents.Version` | |
| `hash` | `Incidents.Hash` | |
| `createdAt` | `Incidents.CreatedAt` | |
| `updatedAt` | `Incidents.UpdatedAt` | |

---

### Endpoint 14: EmissionFactorSets — `POST /api/EmissionFactorSets/Register`

**Tablas origen**: `EmissionFactorSets` + `EmissionFactors`

**Consulta EF Core**:
```csharp
var sets = await _db.EmissionFactorSets
    .AsNoTracking()
    .Include(s => s.Factors) // EmissionFactors vía FactorSetId
    .ToListAsync(ct);
```

| DTO (`EmissionFactorSetItem`) | Origen | Notas |
|---|---|---|
| `remoteId` | `EmissionFactorSets.Id` | |
| `ownerId` | `0168F029-6DD2-4EBF-9CF1-FC8DCC0819AB` | Fijo |
| `factorSetName` | `EmissionFactorSets.FactorSetName` | |
| `version` | `EmissionFactorSets.Version` | |
| `status` | `EmissionFactorSets.Status` | |
| `validFrom` | `EmissionFactorSets.ValidFrom` | |
| `validTo` | `EmissionFactorSets.ValidTo` | |
| `publisher` | `EmissionFactorSets.Publisher` | |
| `reference` | `EmissionFactorSets.Reference` | |
| `methodology` | `EmissionFactorSets.Methodology` | |
| `sourceSystem` | `EmissionFactorSets.SourceSystem` ?? `"GreenTransit"` | |
| `hash` | `EmissionFactorSets.Hash` | |
| `createdAt` | `EmissionFactorSets.CreatedAt` | |
| `updatedAt` | `EmissionFactorSets.UpdatedAt` | |
| **factors[].** | | |
| `factors[].remoteId` | `EmissionFactors.Id` | |
| `factors[].factorSetId` | `EmissionFactors.FactorSetId` | |
| `factors[].vehicleType` | `EmissionFactors.VehicleType` | |
| `factors[].fuelType` | `EmissionFactors.FuelType` | |
| `factors[].euroClass` | `EmissionFactors.EuroClass` | |
| `factors[].unit` | `EmissionFactors.Unit` | |
| `factors[].value` | `EmissionFactors.Value` | |
| `factors[].createdAt` | `EmissionFactors.CreatedAt` | |

---

### Endpoint 15: EcoModulationRuleSets — `POST /api/EcoModulationRuleSets/Register`

**Tablas origen**: `EcoModulationRuleSets` + `EcoModulationRules`

**Consulta EF Core**:
```csharp
var sets = await _db.EcoModulationRuleSets
    .AsNoTracking()
    .Include(s => s.Rules) // EcoModulationRules vía RuleSetId
    .ToListAsync(ct);
```

| DTO (`EcoModulationRuleSetItem`) | Origen | Notas |
|---|---|---|
| `remoteId` | `EcoModulationRuleSets.Id` | |
| `ownerId` | *Cíclico*: `4E3C335B-...`, `7E1AA26A-...` | Alternar round-robin |
| `ruleSetName` | `EcoModulationRuleSets.RuleSetName` | |
| `version` | `EcoModulationRuleSets.Version` | |
| `status` | `EcoModulationRuleSets.Status` | |
| `validFrom` | `EcoModulationRuleSets.ValidFrom` | |
| `validTo` | `EcoModulationRuleSets.ValidTo` | |
| `publisherName` | `EcoModulationRuleSets.PublisherName` | |
| `publisherNationalId` | `EcoModulationRuleSets.PublisherNationalId` | |
| `publisherCenterCode` | `EcoModulationRuleSets.PublisherCenterCode` | |
| `sourceSystem` | `EcoModulationRuleSets.SourceSystem` ?? `"GreenTransit"` | |
| `hash` | `EcoModulationRuleSets.Hash` | |
| `createdAt` | `EcoModulationRuleSets.CreatedAt` | |
| `updatedAt` | `EcoModulationRuleSets.UpdatedAt` | |
| **rules[].** | | |
| `rules[].remoteId` | `EcoModulationRules.Id` | |
| `rules[].ruleSetId` | `EcoModulationRules.RuleSetId` | |
| `rules[].ruleCode` | `EcoModulationRules.RuleCode` | |
| `rules[].productCategory` | `EcoModulationRules.ProductCategory` | Ya es int |
| `rules[].criteriaJson` | `EcoModulationRules.CriteriaJson` | |
| `rules[].feeImpactType` | `EcoModulationRules.FeeImpactType` | |
| `rules[].feeImpactValue` | `EcoModulationRules.FeeImpactValue` | |
| `rules[].createdAt` | `EcoModulationRules.CreatedAt` | |

---

### Endpoint 16: DUMZones — `POST /api/DUMZones/Register`

**Tablas origen**: `DUMZones` + `DUMRestrictionRules`

**Consulta EF Core**:
```csharp
var zones = await _db.DUMZones
    .AsNoTracking()
    .Include(z => z.RestrictionRules) // DUMRestrictionRules vía ZoneId
    .ToListAsync(ct);
```

| DTO (`DumZoneItem`) | Origen | Notas |
|---|---|---|
| `remoteId` | `DUMZones.Id` | |
| `ownerId` | `64ED5419-D01C-4009-AFE7-173F1857C84F` | Fijo |
| `zoneCode` | `DUMZones.ZoneCode` | |
| `name` | `DUMZones.Name` | |
| `description` | `DUMZones.Description` | |
| `status` | `DUMZones.Status` | |
| `geometryJson` | `DUMZones.GeometryJson` | |
| `sourceSystem` | `DUMZones.SourceSystem` ?? `"GreenTransit"` | |
| `version` | `DUMZones.Version` | |
| `hash` | `DUMZones.Hash` | |
| `createdAt` | `DUMZones.CreatedAt` | |
| `updatedAt` | `DUMZones.UpdatedAt` | |
| **rules[].** | | |
| `rules[].remoteId` | `DUMRestrictionRules.Id` | |
| `rules[].ownerId` | `64ED5419-D01C-4009-AFE7-173F1857C84F` | Fijo |
| `rules[].ruleCode` | `DUMRestrictionRules.RuleCode` | |
| `rules[].status` | `DUMRestrictionRules.Status` | |
| `rules[].zoneId` | `DUMRestrictionRules.ZoneId` | |
| `rules[].validFrom` | `DUMRestrictionRules.ValidFrom` | |
| `rules[].validTo` | `DUMRestrictionRules.ValidTo` | |
| `rules[].conditionsJson` | `DUMRestrictionRules.ConditionsJson` | |
| `rules[].actionType` | `DUMRestrictionRules.ActionType` | |
| `rules[].actionReason` | `DUMRestrictionRules.ActionReason` | |
| `rules[].sourceSystem` | `DUMRestrictionRules.SourceSystem` | |
| `rules[].version` | `DUMRestrictionRules.Version` | |
| `rules[].hash` | `DUMRestrictionRules.Hash` | |
| `rules[].createdAt` | `DUMRestrictionRules.CreatedAt` | |
| `rules[].updatedAt` | `DUMRestrictionRules.UpdatedAt` | |

---

## 🔧 Conversores de Enums reutilizables

Crear una clase estática `EcoDataNetEnumMapper`:

```csharp
public static class EcoDataNetEnumMapper
{
    // MeasureUnitEnum: 1=Gr, 2=Kg, 3=Tm, 4=Ud
    public static int? ToMeasureUnit(string? value) => value?.ToUpperInvariant() switch
    {
        "GR" or "GRAMOS" => 1,
        "KG" or "KILOGRAMOS" => 2,
        "TM" or "TONELADAS" => 3,
        "UD" or "UNIDADES" => 4,
        _ => int.TryParse(value, out var v) ? v : null
    };

    // TypeContainerEnum: 1..17
    public static int? ToTypeContainer(string? value) => value switch
    {
        "Auto_Compactador" => 1,
        "Balas" => 2,
        "Barca" => 3,
        "Barca_5_m3" => 4,
        "Barca_9_m3" => 5,
        "Barca_con_tapa" => 6,
        "Caja_Compactador_Estático" => 7,
        "Contenedor" => 8,
        "Contenedor_1100L" => 9,
        "Contenedor_con_Tapa" => 10,
        "Jaula" => 11,
        "Jaula_Doble" => 12,
        "Prensa" => 13,
        "Semiremolque" => 14,
        "Volteador" => 15,
        "Contenedor_C30" => 16,
        "Contenedor_C20" => 17,
        _ => int.TryParse(value, out var v) ? v : null
    };

    // UseProductEnum: 1=Domestico, 2=Profesional
    public static int? ToUseProduct(string? value) => value?.ToUpperInvariant() switch
    {
        "DOMESTICO" or "1" => 1,
        "PROFESIONAL" or "2" => 2,
        _ => int.TryParse(value, out var v) ? v : null
    };

    // CategoryProductEnum: 1=AEE, 2=A1, 3=A2, etc.
    public static int? ToCategoryProduct(string? value) => value switch
    {
        "AEE" => 1,
        "A1" => 2,
        "A2" => 3,
        _ => int.TryParse(value, out var v) ? v : null
    };

    // TypeThirdPartyEnum: 1=PuntoDeRecogida, 2=Gestor, 3=SCRAP, 4=OperadorTraslado
    public static int? ToTypeThirdParty(string? value) => value switch
    {
        "PuntoDeRecogida" => 1,
        "Gestor" => 2,
        "SCRAP" => 3,
        "OperadorTraslado" => 4,
        _ => int.TryParse(value, out var v) ? v : null
    };
}
```

---

## 📝 Pseudocódigo del flujo completo

```
1. UI: Usuario pulsa "Publicar a EcoDataNet"
2. UI: Envía PublishToEcoDataNetCommand vía MediatR
3. Handler: invoca IEcoDataNetPublisher.PublishAllAsync()
4. Para cada endpoint (1..16):
   a. Ejecutar consulta EF Core con .AsNoTracking() + Include/ThenInclude necesarios
   b. Mapear entidades GreenTransit → DTOs EcoDataNet (usando helpers y enum mappers)
   c. Asignar ownerId EcoDataNet (fijo o cíclico según endpoint)
   d. Asignar remoteId = Id de GreenTransit (GUID)
   e. Partir en lotes de BatchSize (default 100)
   f. Para cada lote:
      - POST al endpoint correspondiente vía EcoDataNetHttpClient
      - Si 200 OK: registrar success
      - Si 207 Multi-Status: parsear respuesta, registrar ok/error por elemento
      - Si 400/401/500: registrar error global del lote
   g. Agregar resultados al EndpointResult
5. Devolver PublishSummary a la UI
6. UI: Mostrar tabla resumen con totales ok/error por endpoint
```

---

## ✅ Criterios de aceptación

1. **Conectividad**: El proceso se conecta a la API EcoDataNet usando Basic Auth con las credenciales configuradas.
2. **Completitud**: Los 16 endpoints reciben datos (si existen registros en GreenTransit).
3. **Upsert**: Todo `remoteId` se envía como el `Id` (GUID) de GreenTransit para permitir re-ejecución idempotente.
4. **OwnerId correcto**: Cada endpoint usa el GUID de participante EcoDataNet especificado (no el OwnerId multi-tenant de GreenTransit).
5. **Asignación cíclica**: WasteMoves, MarketShares y EcoModulationRuleSets alternan ownerId round-robin.
6. **Batching**: Los envíos se hacen en lotes de máximo 100 elementos.
7. **Multi-Status 207**: Se parsea la respuesta y se registra ok/error por elemento individual.
8. **Errores HTTP**: 400, 401, 500 se capturan, loguean con Serilog y se muestran en la UI.
9. **Progreso en UI**: La UI muestra qué endpoint se está procesando y el número de paso (ej. "3/16 EntryCACs").
10. **Resumen en UI**: Al finalizar, se muestra tabla con: endpoint, enviados, ok, errores, detalle.
11. **Seguridad**: Las credenciales (`Username`, `Password`) no están hardcodeadas en el código; se leen de configuración (User Secrets en desarrollo, Azure KeyVault en producción).
12. **Sin regresión**: El proceso se integra en la ventana seed existente sin romper funcionalidad previa.
13. **Logging**: Cada endpoint se loguea con nivel Information (ok) o Error (fallo).
14. **Rendimiento**: Las consultas EF Core usan `.AsNoTracking()` y los Include necesarios (no más, no menos).
15. **Resiliencia**: El HttpClient tiene configuración de retries (Polly) y timeout.

---

## 🧪 Pruebas

### Tests unitarios (xUnit)

1. **EcoDataNetEnumMapper**: Verificar conversión de strings a enums int para cada tipo (MeasureUnit, TypeContainer, UseProduct, CategoryProduct, TypeThirdParty).
2. **Asignación cíclica de OwnerId**: Verificar que `ownerIds[index % ownerIds.Length]` produce la rotación correcta.
3. **MapEntity→ThirdPartyRef**: Verificar que null Entity devuelve null, y que los campos se mapean correctamente.

### Tests de integración (con InMemory DB)

4. **PublishWasteMoves**: Crear WasteMoves con Entities y Residues en memoria, verificar que el DTO generado tiene todos los campos esperados.
5. **Batching**: Crear 250 registros, verificar que se envían en 3 lotes (100+100+50).
6. **Multi-Status parsing**: Simular respuesta 207 con algunos errores, verificar que el EndpointResult refleja los conteos correctos.

### Verificación manual

7. Ejecutar el proceso contra un entorno de prueba de EcoDataNet y verificar que los datos aparecen en la API destino.
8. Re-ejecutar y verificar que los registros se actualizan (upsert vía remoteId) sin duplicar.

---

## ⚠️ Notas adicionales

- **Campos `*Ref` opcionales**: Los campos como `typeContainerRef`, `categoryRef`, `measureUnitRef` se pueden omitir al enviar; la API EcoDataNet los resuelve desde el valor numérico del enum.
- **Campos `*Json`**: Deben contener JSON válido como string. Enviar tal cual desde GreenTransit (ya se almacenan como JSON en nvarchar(max)).
- **`idUser`**: No se envía; la API EcoDataNet lo rellena a partir del token de autenticación.
- **No duplicar maestros**: No enviar Entities, Residues ni LERCodes como endpoints independientes. Se usan solo como JOINs para alimentar los DTOs operativos.
- **Enums de GreenTransit**: En la BD, algunos campos de enum se almacenan como string (ej. `MeasureUnit = "Kg"`). El mapper debe convertirlos a int para la API. En otros casos ya son int (ej. `ServiceOrders.ProductUse`). Verificar caso por caso.
