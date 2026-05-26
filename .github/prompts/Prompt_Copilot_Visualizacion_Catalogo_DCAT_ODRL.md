# Prompt para GitHub Copilot — Visualización del Catálogo EDC (DCAT/ODRL) y Selección de Oferta

> **Instrucción**: Copia este prompt completo en GitHub Copilot Chat adjuntando los archivos `COPILOT_CONTEXT.md`, `README.md`, `Crear_BD_v4_1.sql` e `instrucciones_adicionales.md` de tu proyecto. Copilot debe inspeccionar el repo para confirmar estructuras y patrones reales antes de ejecutar cambios.

---

## 🎯 1. Objetivo

Transformar la pantalla `/ecodatanet/consume-data` para que, cuando el botón "Consumir catálogo" devuelva un JSON de catálogo EDC exitoso (DCAT + ODRL), en lugar de mostrar el JSON bruto en un panel colapsable, **pinte una vista de marketplace** con:

1. Una **lista de datasets** extraídos del catálogo DCAT.
2. La posibilidad de **ver el detalle** de cada dataset, incluyendo su **oferta ODRL** asociada (permisos, prohibiciones, finalidad, participantes).
3. Un botón **"Iniciar negociación"** que capture el `offerId` (`odrl:hasPolicy.@id`), el `datasetId`, el `participantId` del proveedor y el endpoint de protocolo, y los deje preparados en el estado de la aplicación para el paso siguiente (que NO se implementa ahora).

---

## 🚫 2. Qué NO hay que hacer

- **NO implementar** la negociación de contrato (`ContractNegotiationRequest`). Solo dejar el estado preparado.
- **NO inventar endpoints** ni llamadas HTTP nuevas. El JSON del catálogo ya se obtiene mediante el flujo existente (`RequestEdcCatalogCommand` → `IEdcManagementClient`).
- **NO usar mock data**. Todo se alimenta del JSON real devuelto por el conector EDC.
- **NO pintar JSON crudo** en la vista principal. El JSON bruto se puede mantener como panel colapsable secundario para depuración, pero la vista principal debe ser visual y estructurada.
- **NO hacer llamadas HTTP desde componentes Razor**. Todo pasa por MediatR (CQRS).
- **NO asumir negociación automática** al pulsar el botón.

---

## 🔍 3. Cómo localizar el código existente

| Qué buscar | Patrón de búsqueda |
|---|---|
| Componente de consumo de datos | `@page "/ecodatanet/consume-data"` en `Web/Components/Pages/EcoDataNet/` |
| Resultado actual del catálogo | Buscar `RequestEdcCatalogResponse`, `EdcProviderCatalogResult`, `EdcCatalogResult` en `Application/Features/EcoDataNet/` |
| JSON bruto del catálogo | Buscar `RawJson` en los DTOs de `Application/Features/EcoDataNet/DTOs/` |
| Acordeón/panel que muestra JSON por proveedor | Buscar `FormatJson` o `accordion` o `details` en el componente `.razor` de consume-data |
| IEdcManagementClient | Buscar `interface IEdcManagementClient` en `Application/` |
| DTOs existentes del módulo EcoDataNet | Buscar en `Application/Features/EcoDataNet/DTOs/` |
| Servicio EdcManagementClient | Buscar `class EdcManagementClient` en `Infrastructure/Services/` |

---

## 🎨 4. Diseño funcional

### 4.1. Flujo de usuario completo

```
1. Usuario selecciona perfil a consumir → pulsa "Consumir catálogo"
   (ESTO YA EXISTE y devuelve RequestEdcCatalogResponse con RawJson por proveedor)

2. NUEVO: Para cada proveedor con Status == OK y CatalogResult.RawJson no vacío:
   → Parsear el JSON DCAT a DTOs tipados
   → Mostrar en una vista tipo "marketplace" agrupada por proveedor

3. Cada dataset se muestra como card/fila con:
   - Nombre del dataset
   - Versión
   - Tipo de contenido
   - Proveedor (nombre del usuario proveedor)
   - Botón "Ver oferta"

4. Al pulsar "Ver oferta" → se expande/abre un detalle con:
   - Info del dataset
   - Condiciones de la oferta ODRL:
     - ID de la oferta
     - Permisos (odrl:permission): finalidad (edc:purpose), participantes
     - Prohibiciones (odrl:prohibition)
   - Distribuciones (informativo, no seleccionable)
   - Botón "Iniciar negociación" (prominente, solo si la oferta es válida)

5. Al pulsar "Iniciar negociación":
   → Se guarda en el estado del componente:
     - SelectedDatasetId
     - SelectedOfferId  ← CLAVE
     - ProviderParticipantId
     - ProviderProtocolEndpoint
   → Se muestra un Toast: "Oferta seleccionada. La negociación estará disponible próximamente."
   → NO se ejecuta ninguna request HTTP adicional.
```

### 4.2. Estructura del JSON DCAT real del catálogo EDC

El JSON devuelto por el conector EDC tiene esta estructura (Copilot debe usarla como referencia, NO inventar otra):

```json
{
  "@id": "catalog-id",
  "@type": "dcat:Catalog",
  "dspace:participantId": "provider-participant-id",
  "dcat:dataset": [
    {
      "@id": "asset-id-1",
      "@type": "dcat:Dataset",
      "name": "Nombre del dataset",
      "version": "1.0",
      "contenttype": "application/json",
      "odrl:hasPolicy": {
        "@id": "offer-id-1",
        "@type": "odrl:Offer",
        "odrl:permission": [
          {
            "odrl:action": { "@id": "odrl:use" },
            "odrl:constraint": [
              {
                "odrl:leftOperand": { "@id": "edc:purpose" },
                "odrl:operator": { "@id": "odrl:eq" },
                "odrl:rightOperand": "valor-finalidad"
              },
              {
                "odrl:leftOperand": { "@id": "edc:participant" },
                "odrl:operator": { "@id": "odrl:eq" },
                "odrl:rightOperand": "participante-permitido"
              }
            ]
          }
        ],
        "odrl:prohibition": [],
        "odrl:obligation": []
      },
      "dcat:distribution": [
        {
          "@type": "dcat:Distribution",
          "dct:format": { "@id": "HttpData-PULL" },
          "dcat:accessService": { "@id": "access-service-id" }
        }
      ]
    }
  ],
  "dcat:service": [
    {
      "@id": "access-service-id",
      "@type": "dcat:DataService",
      "dct:terms": "connector",
      "dcat:endpointUrl": "https://proto.server/protocol"
    }
  ]
}
```

**Reglas de parseo**:
- `dcat:dataset` puede ser un array o un objeto único (si hay un solo dataset) → normalizar siempre a array.
- `odrl:hasPolicy` puede ser un objeto único o un array → normalizar a array de ofertas.
- `odrl:permission` puede ser un objeto único o un array → normalizar a array.
- `odrl:constraint` puede ser un objeto único o un array → normalizar a array.
- `odrl:prohibition` puede ser array vacío, objeto único o ausente → normalizar a array.
- Las propiedades JSON-LD pueden usar prefijos compactos (`dcat:dataset`) o IRIs completas (`https://www.w3.org/ns/dcat#dataset`) → Copilot debe soportar ambos formatos en el parsing.

---

## 🏗️ 5. Diseño técnico por capas

### CAPA Application — DTOs y Query de parseo

#### 5.1. DTOs del catálogo DCAT/ODRL

Crear en `Application/Features/EcoDataNet/DTOs/`:

```csharp
/// <summary>
/// Catálogo DCAT parseado de un proveedor EDC.
/// </summary>
public class EdcCatalogDto
{
    public string CatalogId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public List<EdcDatasetDto> Datasets { get; set; } = new();
}

/// <summary>
/// Dataset individual del catálogo DCAT.
/// </summary>
public class EdcDatasetDto
{
    public string DatasetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? ContentType { get; set; }
    public string? Description { get; set; }
    public EdcOfferDto? Offer { get; set; }
    public List<EdcDistributionDto> Distributions { get; set; } = new();
}

/// <summary>
/// Oferta ODRL asociada a un dataset. Contiene el OfferId que se usa para la negociación.
/// </summary>
public class EdcOfferDto
{
    /// <summary>
    /// Identificador de la oferta ODRL. Este es el valor clave para la negociación.
    /// Corresponde a odrl:hasPolicy.@id en el JSON DCAT.
    /// </summary>
    public string OfferId { get; set; } = string.Empty;
    public List<EdcPermissionDto> Permissions { get; set; } = new();
    public List<EdcProhibitionDto> Prohibitions { get; set; } = new();
    public List<EdcObligationDto> Obligations { get; set; } = new();
}

/// <summary>
/// Permiso ODRL con su acción y restricciones.
/// </summary>
public class EdcPermissionDto
{
    public string Action { get; set; } = string.Empty;   // p.ej. "odrl:use"
    public List<EdcConstraintDto> Constraints { get; set; } = new();
}

/// <summary>
/// Restricción ODRL (leftOperand operator rightOperand).
/// </summary>
public class EdcConstraintDto
{
    public string LeftOperand { get; set; } = string.Empty;   // p.ej. "edc:purpose"
    public string Operator { get; set; } = string.Empty;      // p.ej. "odrl:eq"
    public string RightOperand { get; set; } = string.Empty;  // valor concreto
}

/// <summary>
/// Prohibición ODRL.
/// </summary>
public class EdcProhibitionDto
{
    public string Action { get; set; } = string.Empty;
    public List<EdcConstraintDto> Constraints { get; set; } = new();
}

/// <summary>
/// Obligación ODRL.
/// </summary>
public class EdcObligationDto
{
    public string Action { get; set; } = string.Empty;
    public List<EdcConstraintDto> Constraints { get; set; } = new();
}

/// <summary>
/// Distribución DCAT del dataset (informativa).
/// </summary>
public class EdcDistributionDto
{
    public string Format { get; set; } = string.Empty;      // p.ej. "HttpData-PULL"
    public string? AccessServiceId { get; set; }
    public string? EndpointUrl { get; set; }
}
```

#### 5.2. Servicio de parsing del catálogo

Crear interfaz en `Application/Common/Interfaces/` (o donde estén las demás interfaces):

```csharp
public interface IEdcCatalogParser
{
    /// <summary>
    /// Parsea el JSON bruto del catálogo DCAT/ODRL a DTOs tipados.
    /// </summary>
    EdcCatalogDto? ParseCatalog(string rawJson);
}
```

Crear implementación en `Infrastructure/Services/`:

```csharp
public class EdcCatalogParser : IEdcCatalogParser
{
    private readonly ILogger<EdcCatalogParser> _logger;

    public EdcCatalogParser(ILogger<EdcCatalogParser> logger)
    {
        _logger = logger;
    }

    public EdcCatalogDto? ParseCatalog(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            var catalog = new EdcCatalogDto
            {
                CatalogId = GetStringProperty(root, "@id", "id"),
                ParticipantId = GetStringProperty(root, "dspace:participantId",
                    "https://w3id.org/dspace/v0.8/participantId")
            };

            // Parsear datasets
            var datasetsElement = GetProperty(root, "dcat:dataset",
                "https://www.w3.org/ns/dcat#dataset");

            if (datasetsElement.HasValue)
            {
                catalog.Datasets = NormalizeToArray(datasetsElement.Value)
                    .Select(ParseDataset)
                    .Where(d => d != null)
                    .Select(d => d!)
                    .ToList();
            }

            // Parsear servicios (para resolver endpointUrl de distribuciones)
            var services = new Dictionary<string, string>();
            var servicesElement = GetProperty(root, "dcat:service",
                "https://www.w3.org/ns/dcat#service");
            if (servicesElement.HasValue)
            {
                foreach (var svc in NormalizeToArray(servicesElement.Value))
                {
                    var svcId = GetStringProperty(svc, "@id", "id");
                    var endpointUrl = GetStringProperty(svc, "dcat:endpointUrl",
                        "https://www.w3.org/ns/dcat#endpointUrl");
                    if (!string.IsNullOrEmpty(svcId) && !string.IsNullOrEmpty(endpointUrl))
                        services[svcId] = endpointUrl;
                }
            }

            // Resolver endpointUrl en distribuciones
            foreach (var dataset in catalog.Datasets)
            {
                foreach (var dist in dataset.Distributions)
                {
                    if (!string.IsNullOrEmpty(dist.AccessServiceId)
                        && services.TryGetValue(dist.AccessServiceId, out var url))
                    {
                        dist.EndpointUrl = url;
                    }
                }
            }

            _logger.LogInformation(
                "Catálogo EDC parseado: {DatasetCount} datasets, participantId: {ParticipantId}",
                catalog.Datasets.Count, catalog.ParticipantId);

            return catalog;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parseando JSON del catálogo EDC DCAT");
            return null;
        }
    }

    private EdcDatasetDto? ParseDataset(JsonElement element)
    {
        try
        {
            var dataset = new EdcDatasetDto
            {
                DatasetId = GetStringProperty(element, "@id", "id"),
                Name = GetStringProperty(element, "name",
                    "https://w3id.org/edc/v0.0.1/ns/name"),
                Version = GetStringProperty(element, "version",
                    "https://w3id.org/edc/v0.0.1/ns/version"),
                ContentType = GetStringProperty(element, "contenttype",
                    "https://w3id.org/edc/v0.0.1/ns/contenttype"),
                Description = GetStringProperty(element, "description",
                    "https://w3id.org/edc/v0.0.1/ns/description")
            };

            // Parsear oferta(s) ODRL — usar la primera si hay varias
            var policyElement = GetProperty(element, "odrl:hasPolicy",
                "http://www.w3.org/ns/odrl/2/hasPolicy");
            if (policyElement.HasValue)
            {
                var policies = NormalizeToArray(policyElement.Value);
                if (policies.Count > 0)
                {
                    dataset.Offer = ParseOffer(policies[0]);
                }
            }

            // Parsear distribuciones
            var distElement = GetProperty(element, "dcat:distribution",
                "https://www.w3.org/ns/dcat#distribution");
            if (distElement.HasValue)
            {
                dataset.Distributions = NormalizeToArray(distElement.Value)
                    .Select(ParseDistribution)
                    .Where(d => d != null)
                    .Select(d => d!)
                    .ToList();
            }

            return dataset;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parseando dataset EDC individual");
            return null;
        }
    }

    private EdcOfferDto ParseOffer(JsonElement element)
    {
        var offer = new EdcOfferDto
        {
            OfferId = GetStringProperty(element, "@id", "id")
        };

        // Permisos
        var permElement = GetProperty(element, "odrl:permission",
            "http://www.w3.org/ns/odrl/2/permission");
        if (permElement.HasValue)
        {
            offer.Permissions = NormalizeToArray(permElement.Value)
                .Select(ParsePermission)
                .ToList();
        }

        // Prohibiciones
        var prohibElement = GetProperty(element, "odrl:prohibition",
            "http://www.w3.org/ns/odrl/2/prohibition");
        if (prohibElement.HasValue)
        {
            offer.Prohibitions = NormalizeToArray(prohibElement.Value)
                .Select(p => ParsePolicyRule<EdcProhibitionDto>(p))
                .ToList();
        }

        // Obligaciones
        var obligElement = GetProperty(element, "odrl:obligation",
            "http://www.w3.org/ns/odrl/2/obligation");
        if (obligElement.HasValue)
        {
            offer.Obligations = NormalizeToArray(obligElement.Value)
                .Select(o => ParsePolicyRule<EdcObligationDto>(o))
                .ToList();
        }

        return offer;
    }

    private EdcPermissionDto ParsePermission(JsonElement element)
    {
        var permission = new EdcPermissionDto();

        // Acción
        var actionElement = GetProperty(element, "odrl:action",
            "http://www.w3.org/ns/odrl/2/action");
        if (actionElement.HasValue)
        {
            permission.Action = actionElement.Value.ValueKind == JsonValueKind.Object
                ? GetStringProperty(actionElement.Value, "@id", "id")
                : actionElement.Value.GetString() ?? "";
        }

        // Restricciones
        var constraintElement = GetProperty(element, "odrl:constraint",
            "http://www.w3.org/ns/odrl/2/constraint");
        if (constraintElement.HasValue)
        {
            permission.Constraints = NormalizeToArray(constraintElement.Value)
                .Select(ParseConstraint)
                .ToList();
        }

        return permission;
    }

    private EdcConstraintDto ParseConstraint(JsonElement element)
    {
        return new EdcConstraintDto
        {
            LeftOperand = ExtractIdOrValue(GetProperty(element, "odrl:leftOperand",
                "http://www.w3.org/ns/odrl/2/leftOperand")),
            Operator = ExtractIdOrValue(GetProperty(element, "odrl:operator",
                "http://www.w3.org/ns/odrl/2/operator")),
            RightOperand = ExtractIdOrValue(GetProperty(element, "odrl:rightOperand",
                "http://www.w3.org/ns/odrl/2/rightOperand"))
        };
    }

    private T ParsePolicyRule<T>(JsonElement element) where T : new()
    {
        // Genérico para Prohibitions y Obligations con misma estructura que Permission
        var rule = new T();
        // Copilot: usar reflexión o duplicar lógica de ParsePermission
        // para extraer Action y Constraints a las propiedades homónimas de T
        return rule;
    }

    private EdcDistributionDto? ParseDistribution(JsonElement element)
    {
        try
        {
            var dist = new EdcDistributionDto();

            var formatElement = GetProperty(element, "dct:format",
                "http://purl.org/dc/terms/format");
            if (formatElement.HasValue)
            {
                dist.Format = formatElement.Value.ValueKind == JsonValueKind.Object
                    ? GetStringProperty(formatElement.Value, "@id", "id")
                    : formatElement.Value.GetString() ?? "";
            }

            var accessElement = GetProperty(element, "dcat:accessService",
                "https://www.w3.org/ns/dcat#accessService");
            if (accessElement.HasValue)
            {
                dist.AccessServiceId = accessElement.Value.ValueKind == JsonValueKind.Object
                    ? GetStringProperty(accessElement.Value, "@id", "id")
                    : accessElement.Value.GetString();
            }

            return dist;
        }
        catch
        {
            return null;
        }
    }

    // ── Utilidades para manejar JSON-LD con prefijos compactos e IRIs ──

    /// <summary>Busca una propiedad por nombre compacto o IRI completa.</summary>
    private JsonElement? GetProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var prop))
                return prop;
        }
        return null;
    }

    /// <summary>Obtiene un string buscando por nombre compacto o IRI.</summary>
    private string GetStringProperty(JsonElement element, params string[] names)
    {
        var prop = GetProperty(element, names);
        if (!prop.HasValue) return string.Empty;
        return prop.Value.ValueKind == JsonValueKind.String
            ? prop.Value.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>Normaliza un JsonElement a List: si es array devuelve sus elementos, si es objeto lo envuelve en lista.</summary>
    private List<JsonElement> NormalizeToArray(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().ToList();
        if (element.ValueKind == JsonValueKind.Object)
            return new List<JsonElement> { element };
        return new List<JsonElement>();
    }

    /// <summary>Extrae @id de un objeto JSON-LD o el valor string directo.</summary>
    private string ExtractIdOrValue(JsonElement? element)
    {
        if (!element.HasValue) return string.Empty;
        if (element.Value.ValueKind == JsonValueKind.Object)
            return GetStringProperty(element.Value, "@id", "id");
        if (element.Value.ValueKind == JsonValueKind.String)
            return element.Value.GetString() ?? string.Empty;
        return string.Empty;
    }
}
```

**Registrar** el servicio en `Program.cs` o `DependencyInjection.cs`:

```csharp
builder.Services.AddSingleton<IEdcCatalogParser, EdcCatalogParser>();
```

#### 5.3. Query para parsear catálogos de un resultado existente

Crear en `Application/Features/EcoDataNet/Queries/`:

```csharp
/// <summary>
/// Parsea los JSON brutos de catálogos ya obtenidos en un RequestEdcCatalogResponse
/// y devuelve DTOs tipados agrupados por proveedor.
/// </summary>
public record ParseEdcCatalogsQuery(RequestEdcCatalogResponse CatalogResponse)
    : IRequest<List<EdcProviderParsedCatalogDto>>;
```

DTO de resultado:

```csharp
/// <summary>
/// Catálogo parseado de un proveedor específico, con datos del usuario proveedor.
/// </summary>
public class EdcProviderParsedCatalogDto
{
    public int ProviderUserId { get; set; }
    public string ProviderUserName { get; set; } = string.Empty;
    public string ProviderUserLogin { get; set; } = string.Empty;
    public string? ProviderServerName { get; set; }
    public string? ProviderConnectorId { get; set; }
    public string ProviderParticipantId { get; set; } = string.Empty;
    public string ProviderProtocolEndpoint { get; set; } = string.Empty;
    public EdcCatalogDto? Catalog { get; set; }
    public bool ParseSuccess { get; set; }
    public string? ParseError { get; set; }
}
```

Handler:

```csharp
public class ParseEdcCatalogsQueryHandler
    : IRequestHandler<ParseEdcCatalogsQuery, List<EdcProviderParsedCatalogDto>>
{
    private readonly IEdcCatalogParser _parser;

    public ParseEdcCatalogsQueryHandler(IEdcCatalogParser parser)
    {
        _parser = parser;
    }

    public Task<List<EdcProviderParsedCatalogDto>> Handle(
        ParseEdcCatalogsQuery request, CancellationToken ct)
    {
        var results = new List<EdcProviderParsedCatalogDto>();

        foreach (var provider in request.CatalogResponse.Results
            .Where(r => r.Status == EdcProviderStatus.Ok
                     && r.CatalogResult?.Success == true
                     && !string.IsNullOrEmpty(r.CatalogResult.RawJson)))
        {
            var dto = new EdcProviderParsedCatalogDto
            {
                ProviderUserId = provider.UserId,
                ProviderUserName = provider.UserName,
                ProviderUserLogin = provider.UserLogin,
                ProviderServerName = provider.EDCServerName,
                ProviderConnectorId = provider.EDCConnectorId,
                ProviderProtocolEndpoint = !string.IsNullOrEmpty(provider.EDCServerName)
                    ? $"https://proto.{provider.EDCServerName}/protocol"
                    : string.Empty
            };

            try
            {
                dto.Catalog = _parser.ParseCatalog(provider.CatalogResult!.RawJson!);
                dto.ParseSuccess = dto.Catalog != null;
                dto.ProviderParticipantId = dto.Catalog?.ParticipantId ?? string.Empty;
            }
            catch (Exception ex)
            {
                dto.ParseSuccess = false;
                dto.ParseError = ex.Message;
            }

            results.Add(dto);
        }

        return Task.FromResult(results);
    }
}
```

#### 5.4. DTO de estado de negociación (preparación)

Crear en `Application/Features/EcoDataNet/DTOs/`:

```csharp
/// <summary>
/// Estado de selección de oferta listo para iniciar negociación.
/// Se guarda en el componente Blazor y se usará en el futuro paso de negociación.
/// </summary>
public class EdcNegotiationSelection
{
    public string SelectedDatasetId { get; set; } = string.Empty;
    public string SelectedOfferId { get; set; } = string.Empty;
    public string ProviderParticipantId { get; set; } = string.Empty;
    public string ProviderProtocolEndpoint { get; set; } = string.Empty;
    public string DatasetName { get; set; } = string.Empty;
    public string ProviderUserName { get; set; } = string.Empty;
}
```

---

### CAPA Web — Cambios en el componente Blazor

#### 6.1. Estado adicional en el componente

Añadir al `@code` del componente `ConsumeData.razor` (o como se llame):

```csharp
// NUEVO: catálogos parseados
private List<EdcProviderParsedCatalogDto>? _parsedCatalogs = null;

// NUEVO: estado de selección para negociación
private EdcNegotiationSelection? _negotiationSelection = null;

// NUEVO: dataset seleccionado para ver detalle
private EdcDatasetDto? _selectedDataset = null;
private EdcProviderParsedCatalogDto? _selectedProvider = null;
private bool _showDatasetDetail = false;
```

#### 6.2. Modificar el flujo post-consumo

Después de que `OnConsumeCatalogAsync` obtenga `_catalogResponse` exitosamente, añadir el parseo automático:

```csharp
// Dentro de OnConsumeCatalogAsync, después de _catalogResponse = response:
if (_catalogResponse != null && _catalogResponse.SuccessCount > 0)
{
    _parsedCatalogs = await Mediator.Send(
        new ParseEdcCatalogsQuery(_catalogResponse));
}
```

#### 6.3. Vista marketplace de datasets

Reemplazar (o añadir debajo de) la sección de resultados con badges por proveedor. La vista principal del catálogo debe ser:

```razor
@* ── CATÁLOGO DE DATASETS ── *@
@if (_parsedCatalogs != null && _parsedCatalogs.Any(c => c.ParseSuccess))
{
    <div class="mt-4">
        <h4><i class="bi bi-database me-2"></i>Catálogo de datos disponibles</h4>

        @foreach (var provider in _parsedCatalogs.Where(c => c.ParseSuccess && c.Catalog != null))
        {
            <div class="card mb-3">
                <div class="card-header d-flex justify-content-between align-items-center">
                    <div>
                        <strong>@provider.ProviderUserName</strong>
                        <span class="text-muted ms-2">(@provider.ProviderUserLogin)</span>
                    </div>
                    <span class="badge bg-info">
                        @provider.Catalog!.Datasets.Count dataset(s)
                    </span>
                </div>
                <div class="card-body p-0">
                    <table class="table table-hover mb-0">
                        <thead>
                            <tr>
                                <th>Nombre</th>
                                <th>Versión</th>
                                <th>Tipo</th>
                                <th>Oferta</th>
                                <th></th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var dataset in provider.Catalog.Datasets)
                            {
                                <tr>
                                    <td>
                                        <strong>@(string.IsNullOrEmpty(dataset.Name) ? dataset.DatasetId : dataset.Name)</strong>
                                        @if (!string.IsNullOrEmpty(dataset.Description))
                                        {
                                            <br /><small class="text-muted">@dataset.Description</small>
                                        }
                                    </td>
                                    <td>@(dataset.Version ?? "—")</td>
                                    <td><span class="badge bg-secondary">@(dataset.ContentType ?? "—")</span></td>
                                    <td>
                                        @if (dataset.Offer != null)
                                        {
                                            <span class="badge bg-success">
                                                <i class="bi bi-check-circle me-1"></i>Disponible
                                            </span>
                                        }
                                        else
                                        {
                                            <span class="badge bg-warning text-dark">Sin oferta</span>
                                        }
                                    </td>
                                    <td>
                                        @if (dataset.Offer != null)
                                        {
                                            <button class="btn btn-sm btn-outline-primary"
                                                    @onclick="() => ShowDatasetDetail(provider, dataset)">
                                                <i class="bi bi-eye me-1"></i>Ver oferta
                                            </button>
                                        }
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            </div>
        }
    </div>
}
```

> **Copilot**: si el proyecto usa Radzen, adaptar la tabla a `RadzenDataGrid`, los badges a `RadzenBadge`, y las cards a `RadzenCard`. Inspeccionar los componentes usados en el resto del proyecto.

#### 6.4. Vista detalle del dataset con oferta ODRL

```razor
@* ── DETALLE DE DATASET Y OFERTA ── *@
@if (_showDatasetDetail && _selectedDataset != null && _selectedProvider != null)
{
    <div class="modal fade show d-block" tabindex="-1" style="background-color: rgba(0,0,0,0.5);">
        <div class="modal-dialog modal-lg modal-dialog-scrollable">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title">
                        <i class="bi bi-file-earmark-text me-2"></i>
                        @(string.IsNullOrEmpty(_selectedDataset.Name) ? _selectedDataset.DatasetId : _selectedDataset.Name)
                    </h5>
                    <button type="button" class="btn-close" @onclick="CloseDatasetDetail"></button>
                </div>
                <div class="modal-body">

                    @* Info del dataset *@
                    <div class="mb-3">
                        <h6>Información del dataset</h6>
                        <dl class="row mb-0">
                            <dt class="col-sm-4">ID del asset</dt>
                            <dd class="col-sm-8"><code>@_selectedDataset.DatasetId</code></dd>
                            <dt class="col-sm-4">Versión</dt>
                            <dd class="col-sm-8">@(_selectedDataset.Version ?? "—")</dd>
                            <dt class="col-sm-4">Tipo de contenido</dt>
                            <dd class="col-sm-8">@(_selectedDataset.ContentType ?? "—")</dd>
                            <dt class="col-sm-4">Proveedor</dt>
                            <dd class="col-sm-8">@_selectedProvider.ProviderUserName (@_selectedProvider.ProviderParticipantId)</dd>
                        </dl>
                    </div>

                    <hr />

                    @* Oferta ODRL *@
                    @if (_selectedDataset.Offer != null)
                    {
                        <div class="mb-3">
                            <h6><i class="bi bi-shield-check me-2"></i>Condiciones de la oferta</h6>
                            <p class="text-muted small">
                                ID de oferta: <code>@_selectedDataset.Offer.OfferId</code>
                            </p>

                            @* Permisos *@
                            @if (_selectedDataset.Offer.Permissions.Any())
                            {
                                <div class="mb-2">
                                    <strong class="text-success"><i class="bi bi-check-lg me-1"></i>Permisos</strong>
                                    @foreach (var perm in _selectedDataset.Offer.Permissions)
                                    {
                                        <div class="ms-3 mt-1">
                                            <span>Acción: <code>@HumanizeOdrl(perm.Action)</code></span>
                                            @if (perm.Constraints.Any())
                                            {
                                                <ul class="mb-0 small">
                                                    @foreach (var c in perm.Constraints)
                                                    {
                                                        <li>
                                                            <strong>@HumanizeOdrl(c.LeftOperand)</strong>
                                                            @HumanizeOdrl(c.Operator)
                                                            <em>@c.RightOperand</em>
                                                        </li>
                                                    }
                                                </ul>
                                            }
                                        </div>
                                    }
                                </div>
                            }

                            @* Prohibiciones *@
                            @if (_selectedDataset.Offer.Prohibitions.Any())
                            {
                                <div class="mb-2">
                                    <strong class="text-danger"><i class="bi bi-x-lg me-1"></i>Prohibiciones</strong>
                                    @foreach (var p in _selectedDataset.Offer.Prohibitions)
                                    {
                                        <div class="ms-3 mt-1">
                                            <span>Acción: <code>@HumanizeOdrl(p.Action)</code></span>
                                        </div>
                                    }
                                </div>
                            }
                            else
                            {
                                <p class="text-muted small">Sin prohibiciones explícitas.</p>
                            }

                            @* Obligaciones *@
                            @if (_selectedDataset.Offer.Obligations.Any())
                            {
                                <div class="mb-2">
                                    <strong class="text-warning"><i class="bi bi-exclamation-triangle me-1"></i>Obligaciones</strong>
                                    @foreach (var o in _selectedDataset.Offer.Obligations)
                                    {
                                        <div class="ms-3 mt-1">
                                            <span>Acción: <code>@HumanizeOdrl(o.Action)</code></span>
                                        </div>
                                    }
                                </div>
                            }
                        </div>

                        <hr />
                    }

                    @* Distribuciones *@
                    @if (_selectedDataset.Distributions.Any())
                    {
                        <div class="mb-3">
                            <h6><i class="bi bi-arrow-down-circle me-2"></i>Distribuciones (informativo)</h6>
                            <ul class="list-unstyled">
                                @foreach (var dist in _selectedDataset.Distributions)
                                {
                                    <li>
                                        <span class="badge bg-light text-dark">@dist.Format</span>
                                        @if (!string.IsNullOrEmpty(dist.EndpointUrl))
                                        {
                                            <span class="text-muted small ms-2">@dist.EndpointUrl</span>
                                        }
                                    </li>
                                }
                            </ul>
                        </div>
                    }
                </div>

                <div class="modal-footer">
                    <button class="btn btn-secondary" @onclick="CloseDatasetDetail">Cerrar</button>
                    @if (_selectedDataset.Offer != null)
                    {
                        <button class="btn btn-primary" @onclick="OnSelectOfferForNegotiation">
                            <i class="bi bi-handshake me-2"></i>Iniciar negociación
                        </button>
                    }
                </div>
            </div>
        </div>
    </div>
}
```

> **Copilot**: si el proyecto usa `RadzenDialog` o `DialogService`, adaptar el modal a ese patrón en lugar de Bootstrap modal manual.

#### 6.5. Métodos del componente

```csharp
private void ShowDatasetDetail(EdcProviderParsedCatalogDto provider, EdcDatasetDto dataset)
{
    _selectedProvider = provider;
    _selectedDataset = dataset;
    _showDatasetDetail = true;
    StateHasChanged();
}

private void CloseDatasetDetail()
{
    _showDatasetDetail = false;
    _selectedDataset = null;
    _selectedProvider = null;
    StateHasChanged();
}

private void OnSelectOfferForNegotiation()
{
    if (_selectedDataset?.Offer == null || _selectedProvider == null)
        return;

    _negotiationSelection = new EdcNegotiationSelection
    {
        SelectedDatasetId = _selectedDataset.DatasetId,
        SelectedOfferId = _selectedDataset.Offer.OfferId,
        ProviderParticipantId = _selectedProvider.ProviderParticipantId,
        ProviderProtocolEndpoint = _selectedProvider.ProviderProtocolEndpoint,
        DatasetName = _selectedDataset.Name,
        ProviderUserName = _selectedProvider.ProviderUserName
    };

    CloseDatasetDetail();

    // Mostrar confirmación (Toast o alerta)
    // Copilot: usar el mecanismo de Toast/notificación existente en el proyecto
    // Ejemplo con un flag:
    _showNegotiationConfirmation = true;
    StateHasChanged();
}

/// <summary>
/// Humaniza términos ODRL/EDC para mostrar al usuario.
/// Elimina prefijos de namespace y capitaliza.
/// </summary>
private string HumanizeOdrl(string term)
{
    if (string.IsNullOrEmpty(term)) return "—";

    // Eliminar prefijos comunes
    var prefixes = new[] { "odrl:", "edc:", "http://www.w3.org/ns/odrl/2/",
        "https://w3id.org/edc/v0.0.1/ns/" };
    foreach (var prefix in prefixes)
    {
        if (term.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            term = term[prefix.Length..];
            break;
        }
    }

    // Capitalizar primera letra
    return char.ToUpper(term[0]) + term[1..];
}
```

#### 6.6. Banner de oferta seleccionada

Añadir después de la sección de catálogo, visible cuando `_negotiationSelection != null`:

```razor
@* ── OFERTA SELECCIONADA (preparada para negociación) ── *@
@if (_negotiationSelection != null)
{
    <div class="alert alert-success mt-3 d-flex align-items-center">
        <div class="flex-grow-1">
            <h6 class="alert-heading mb-1">
                <i class="bi bi-check-circle-fill me-2"></i>Oferta seleccionada
            </h6>
            <p class="mb-0 small">
                <strong>Dataset:</strong> @_negotiationSelection.DatasetName<br />
                <strong>Proveedor:</strong> @_negotiationSelection.ProviderUserName<br />
                <strong>Offer ID:</strong> <code>@_negotiationSelection.SelectedOfferId</code>
            </p>
        </div>
        <div>
            <button class="btn btn-outline-danger btn-sm" @onclick="ClearNegotiationSelection">
                <i class="bi bi-x"></i> Cancelar
            </button>
        </div>
    </div>
    <p class="text-muted small mt-1">
        <i class="bi bi-info-circle me-1"></i>
        La negociación de contrato estará disponible en una próxima versión.
    </p>
}
```

```csharp
private bool _showNegotiationConfirmation = false;

private void ClearNegotiationSelection()
{
    _negotiationSelection = null;
    _showNegotiationConfirmation = false;
    StateHasChanged();
}
```

---

## 📦 6. DTOs esperados (resumen)

| DTO | Ubicación | Propósito |
|---|---|---|
| `EdcCatalogDto` | `Application/Features/EcoDataNet/DTOs/` | Catálogo DCAT parseado |
| `EdcDatasetDto` | ídem | Dataset individual con nombre, versión, tipo, oferta |
| `EdcOfferDto` | ídem | Oferta ODRL con OfferId, permisos, prohibiciones, obligaciones |
| `EdcPermissionDto` | ídem | Permiso ODRL con acción y restricciones |
| `EdcConstraintDto` | ídem | Restricción ODRL (leftOperand, operator, rightOperand) |
| `EdcProhibitionDto` | ídem | Prohibición ODRL |
| `EdcObligationDto` | ídem | Obligación ODRL |
| `EdcDistributionDto` | ídem | Distribución DCAT (formato, endpoint) |
| `EdcProviderParsedCatalogDto` | ídem | Catálogo parseado + datos del proveedor |
| `EdcNegotiationSelection` | ídem | Estado de selección para negociación futura |

---

## 7. Cambios en UI (resumen)

| Cambio | Descripción |
|---|---|
| **Vista marketplace** | Tabla/cards agrupadas por proveedor con datasets, versión, tipo y badge de oferta |
| **Botón "Ver oferta"** | Abre modal con detalle del dataset y condiciones ODRL humanizadas |
| **Botón "Iniciar negociación"** | Captura `offerId` + `datasetId` + `participantId` + `protocolEndpoint` en estado |
| **Banner de selección** | Alert persistente mostrando la oferta seleccionada con opción de cancelar |
| **JSON bruto** | Se mantiene como panel colapsable secundario debajo de la vista marketplace para depuración |

---

## ✅ 8. Checklist de aceptación

- [ ] **CA-1**: Se muestran TODOS los datasets del catálogo de cada proveedor exitoso, no solo el JSON bruto.
- [ ] **CA-2**: Cada dataset muestra nombre, versión, tipo de contenido y badge indicando si tiene oferta.
- [ ] **CA-3**: El botón "Ver oferta" abre un detalle legible con permisos, prohibiciones y obligaciones ODRL.
- [ ] **CA-4**: Los términos ODRL se muestran humanizados (sin prefijos `odrl:` / `edc:` / IRIs).
- [ ] **CA-5**: El botón "Iniciar negociación" solo aparece si el dataset tiene una `Offer` válida con `OfferId` no vacío.
- [ ] **CA-6**: Al pulsar "Iniciar negociación", el `OfferId` guardado coincide exactamente con `odrl:hasPolicy.@id` del JSON original.
- [ ] **CA-7**: El estado `EdcNegotiationSelection` contiene: `SelectedDatasetId`, `SelectedOfferId`, `ProviderParticipantId`, `ProviderProtocolEndpoint`.
- [ ] **CA-8**: El parsing soporta JSON-LD con prefijos compactos (`dcat:dataset`) y con IRIs completas (`https://www.w3.org/ns/dcat#dataset`).
- [ ] **CA-9**: `dcat:dataset` se normaliza tanto si viene como array como si viene como objeto único.
- [ ] **CA-10**: `odrl:hasPolicy` se normaliza tanto si viene como array como si viene como objeto único.
- [ ] **CA-11**: Errores de parseo en un proveedor no afectan a los demás — se muestran con mensaje de error.
- [ ] **CA-12**: NO hay llamadas HTTP nuevas — todo se basa en el JSON ya obtenido por `RequestEdcCatalogCommand`.
- [ ] **CA-13**: NO se pinta JSON crudo como vista principal — solo como panel colapsable secundario.
- [ ] **CA-14**: Todo el parsing vive en `Infrastructure/Services/EdcCatalogParser.cs`, no en el componente Razor.
- [ ] **CA-15**: La negociación NO se implementa; el botón solo guarda estado y muestra mensaje informativo.
- [ ] **CA-16**: El proyecto compila sin errores y sin warnings nuevos.
- [ ] **CA-17**: El código respeta Clean Architecture: DTOs e interfaces en Application, parsing en Infrastructure, UI en Web.
