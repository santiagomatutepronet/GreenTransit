# Prompt para GitHub Copilot — Integración Real del Botón "Consumir Catálogo" con la Management API de EDC

> **Instrucción**: Copia este prompt completo en GitHub Copilot Chat adjuntando los archivos `COPILOT_CONTEXT.md`, `README.md`, `Crear_BD_v4_1.sql` e `instrucciones_adicionales.md` de tu proyecto. Copilot debe inspeccionar el repo para confirmar estructuras, nombres de clases y patrones reales antes de ejecutar cambios.

---

## 🎯 1. Objetivo

Convertir el botón **"Consumir catálogo"** de la pantalla `/ecodatanet/consume-data` de un placeholder inerte en una funcionalidad real que:

1. Identifique a **todos los usuarios del tenant actual** cuyo perfil coincide con el perfil consumido seleccionado.
2. Para cada uno de esos usuarios, lea su registro `UserEDCConnector` y construya la URL de la Management API de EDC.
3. Ejecute una solicitud **POST /v3/catalog/request** contra la Management API del **conector del usuario consumidor** (el usuario logueado), pasando como `counterPartyAddress` la Protocol API de cada proveedor.
4. Muestre en pantalla un resumen agregado por usuario proveedor: OK / ERROR / SIN_CONECTOR, número de datasets, y panel colapsable con el JSON bruto para depuración.

---

## 📐 2. Alcance

### Incluido

- Creación de un servicio `IEdcManagementClient` (interfaz en Application, implementación en Infrastructure) que encapsule las llamadas HTTP a la Management API de EDC.
- Creación de un Command MediatR `RequestEdcCatalogCommand` que orqueste la lógica de negocio.
- Modificación del componente Blazor `ConsumeData.razor` (o como se llame — buscar el componente con `@page "/ecodatanet/consume-data"`) para conectar el botón con el Command.
- Registro del `HttpClient` con `HttpClientFactory`.
- Configuración en `appsettings.json` para timeout y API Key opcional.
- Logs estructurados con Serilog en cada paso (éxito, error, timeout por usuario).

### Fuera de alcance

- Parseo semántico avanzado del catálogo DCAT/JSON-LD (se muestra como JSON bruto colapsable; se cuenta el número de datasets de forma básica).
- Negociación de contrato, transferencia de datos o cualquier paso posterior al descubrimiento de catálogo.
- Cambios en la tabla `UserEDCConnector` o `ProfileEDCConsumer`.
- Autenticación mutua TLS ni certificados de cliente (solo HTTP con API Key opcional).

---

## 🔎 3. Suposiciones y cómo verificarlas en el repo

| Suposición | Cómo verificar |
|---|---|
| Existe un componente Blazor con `@page "/ecodatanet/consume-data"` | Buscar `@page "/ecodatanet/consume-data"` en `Web/Components/Pages/EcoDataNet/` |
| Existe el botón "Consumir catálogo" que actualmente muestra un Toast placeholder | Buscar `Consumir catálogo` en el mismo archivo |
| Existe `ICurrentUserService` con `ProfileId`, `ProfileReference`, `OwnerId`, y un método para obtener el `UserId` | Buscar `ICurrentUserService` en `Application/Interfaces/` o `Application/Common/Interfaces/` |
| Existen las entidades `UserEDCConnector` y `ProfileEDCConsumer` en Domain | Buscar `UserEDCConnector` en `Domain/Entities/` |
| Existe `DbSet<UserEDCConnector>` en el DbContext | Buscar `UserEDCConnector` en el archivo del DbContext |
| El proyecto usa `HttpClientFactory` en algún otro servicio (para seguir el patrón) | Buscar `AddHttpClient` o `IHttpClientFactory` en `Program.cs` o `Infrastructure/` |
| El proyecto usa Serilog para logging | Buscar `Serilog` en `Program.cs` o paquetes NuGet |
| FluentValidation está configurado con MediatR pipeline behavior | Buscar `ValidationBehavior` o `AddFluentValidation` en `Program.cs` |
| Existen políticas de retry/timeout (`Polly`) en el proyecto | Buscar `AddPolicyHandler`, `Polly`, `IAsyncPolicy` en `Infrastructure/` o `Program.cs`. Si NO existe, crear una política básica nueva |

---

## 🔍 4. Cómo localizar el código

| Qué buscar | Patrón de búsqueda |
|---|---|
| Componente de consumo de datos | `@page "/ecodatanet/consume-data"` |
| Botón placeholder actual | `"Consumir catálogo"` o `"Funcionalidad pendiente"` en archivos `.razor` |
| UserEDCConnector entity | `class UserEDCConnector` en `Domain/` |
| ProfileEDCConsumer entity | `class ProfileEDCConsumer` en `Domain/` |
| ICurrentUserService | `interface ICurrentUserService` |
| GetConsumableProfilesQuery | `GetConsumableProfilesQuery` en `Application/Features/EcoDataNet/` |
| DbContext (nombre real) | Buscar `: DbContext` o `: IdentityDbContext` en `Infrastructure/Persistence/` |
| HttpClientFactory registros existentes | `AddHttpClient` en `Program.cs` |
| Configuración appsettings | `appsettings.json` en `Web/` |
| Patrones de Command existentes | Cualquier `IRequest<>` en `Application/Features/` para copiar el patrón |

---

## 🏗️ 5. Diseño técnico — Flujo de datos

### 5.1. Secuencia completa

```
Usuario pulsa "Consumir catálogo"
  → Componente Blazor llama Mediator.Send(RequestEdcCatalogCommand)
    → Handler:
      1. Validar que el usuario tiene permiso para consumir ese perfil
         (verificar ProfileEDCConsumer o que sea ADMIN).
      2. Obtener el UserEDCConnector del USUARIO CONSUMIDOR (el logueado).
         Si no tiene → devolver error "Configure su conector EDC primero".
      3. Listar todos los Users del tenant donde IdProfile == perfilConsumidoId.
      4. Para cada usuario objetivo:
         a. Leer su UserEDCConnector.
            Si no tiene → marcar como SIN_CONECTOR, continuar.
         b. Construir counterPartyAddress = "https://proto.{edcServerName}/protocol"
         c. Construir consumerManagementUrl = "https://mgmt.{consumerEdcServerName}/management"
         d. Llamar a IEdcManagementClient.RequestCatalogAsync(consumerManagementUrl, counterPartyAddress)
         e. Capturar resultado (OK + JSON, o error + mensaje).
      5. Agregar resultados y devolver.
    → Componente Blazor muestra resumen.
```

### 5.2. Construcción de URLs EDC a partir de `EDCServerName`

El campo `UserEDCConnector.EDCServerName` contiene el nombre base del servidor (por ejemplo: `ecoucofiasignacion.ecodatanetconn3.dataspace.wastenode.com`).

Las URLs se construyen anteponiendo subdominios según la API de Traefik:

| API EDC | Subdominio | Path | URL resultante |
|---|---|---|---|
| Management | `mgmt.` | `/management` | `https://mgmt.{EDCServerName}/management` |
| Control | `control.` | `/control` | `https://control.{EDCServerName}/control` |
| Protocol | `proto.` | `/protocol` | `https://proto.{EDCServerName}/protocol` |
| Public | `public.` | `/public` | `https://public.{EDCServerName}/public` |

**En esta implementación solo se usan Management (del consumidor) y Protocol (del proveedor como `counterPartyAddress`).**

### 5.3. Body de la request POST /v3/catalog/request

```json
{
  "@context": {
    "@vocab": "https://w3id.org/edc/v0.0.1/ns/"
  },
  "@type": "CatalogRequest",
  "counterPartyAddress": "https://proto.{providerEdcServerName}/protocol",
  "protocol": "dataspace-protocol-http"
}
```

- El POST se ejecuta contra `https://mgmt.{consumerEdcServerName}/management/v3/catalog/request`.
- El `Content-Type` es `application/json`.
- Si hay API Key configurada, se envía como header `X-Api-Key`.

### 5.4. Paralelización y throttling

- Usar `Task.WhenAll` con un `SemaphoreSlim` para limitar la concurrencia (máximo 5 solicitudes simultáneas por defecto, configurable en appsettings).
- Cada solicitud individual tiene su propio timeout (30 segundos por defecto, configurable).
- Un error en una solicitud NO cancela las demás.

---

## ⚙️ 6. Cambios por capas

### CAPA 1 — Application (`GreenTransit.Application`)

#### 6.1.1. Interfaz `IEdcManagementClient`

Crear en `Application/Common/Interfaces/` (o `Application/Interfaces/`, según dónde estén las demás interfaces del proyecto):

```csharp
public interface IEdcManagementClient
{
    /// <summary>
    /// Solicita el catálogo de un proveedor EDC a través de la Management API del consumidor.
    /// </summary>
    /// <param name="consumerManagementBaseUrl">URL base de la Management API del consumidor (ej: https://mgmt.server/management)</param>
    /// <param name="counterPartyProtocolUrl">URL de la Protocol API del proveedor (ej: https://proto.server/protocol)</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Resultado con el JSON del catálogo o información de error</returns>
    Task<EdcCatalogResult> RequestCatalogAsync(
        string consumerManagementBaseUrl,
        string counterPartyProtocolUrl,
        CancellationToken cancellationToken = default);
}
```

#### 6.1.2. DTOs de resultado

Crear en `Application/Features/EcoDataNet/DTOs/`:

```csharp
// Resultado individual por proveedor
public class EdcCatalogResult
{
    public bool Success { get; set; }
    public string? RawJson { get; set; }
    public int DatasetCount { get; set; }
    public string? ErrorMessage { get; set; }
    public int HttpStatusCode { get; set; }
}

// Resultado por usuario proveedor
public class EdcProviderCatalogResult
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserLogin { get; set; } = string.Empty;
    public string? EDCServerName { get; set; }
    public string? EDCConnectorId { get; set; }
    public EdcProviderStatus Status { get; set; }
    public EdcCatalogResult? CatalogResult { get; set; }
}

public enum EdcProviderStatus
{
    Ok,
    Error,
    NoConnector,
    Timeout
}

// Resultado agregado del command
public class RequestEdcCatalogResponse
{
    public string ConsumerServerName { get; set; } = string.Empty;
    public int TotalProviders { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public int NoConnectorCount { get; set; }
    public List<EdcProviderCatalogResult> Results { get; set; } = new();
}
```

#### 6.1.3. Command `RequestEdcCatalogCommand`

Crear en `Application/Features/EcoDataNet/Commands/`:

```csharp
public record RequestEdcCatalogCommand : IRequest<RequestEdcCatalogResponse>
{
    /// <summary>
    /// ID del perfil cuyos datos se quieren consumir (ConsumedProfileId).
    /// </summary>
    public int ConsumedProfileId { get; init; }

    /// <summary>
    /// ID del usuario que actúa como consumidor.
    /// Si es ADMIN en la pantalla, puede ser un usuario diferente al logueado.
    /// Si NO es ADMIN, debe coincidir con el usuario logueado.
    /// </summary>
    public int ConsumerUserId { get; init; }
}
```

**Handler** — Lógica detallada:

```csharp
public class RequestEdcCatalogCommandHandler
    : IRequestHandler<RequestEdcCatalogCommand, RequestEdcCatalogResponse>
{
    // Inyectar: DbContext, ICurrentUserService, IEdcManagementClient, ILogger, IOptions<EdcOptions>

    public async Task<RequestEdcCatalogResponse> Handle(
        RequestEdcCatalogCommand request, CancellationToken ct)
    {
        // 1. SEGURIDAD: verificar permisos
        var currentUser = _currentUserService;

        // Si NO es ADMIN, validar:
        //   a) request.ConsumerUserId == currentUser.UserId (no puede actuar como otro)
        //   b) Existe un ProfileEDCConsumer donde ProfileId == currentUser.ProfileId
        //      y ConsumedProfileId == request.ConsumedProfileId
        // Si es ADMIN: solo verificar que ConsumerUserId pertenece al mismo tenant (OwnerId)

        // 2. OBTENER CONECTOR DEL CONSUMIDOR
        var consumerConnector = await _db.UserEDCConnectors
            .FirstOrDefaultAsync(c => c.UserId == request.ConsumerUserId, ct);

        if (consumerConnector == null)
            throw new ValidationException(
                "El usuario consumidor no tiene un conector EDC configurado. " +
                "Configure su conector desde 'Configuración conector EDC' antes de consumir catálogos.");

        var consumerMgmtUrl = $"https://mgmt.{consumerConnector.EDCServerName}/management";

        // 3. LISTAR USUARIOS PROVEEDORES del tenant con el perfil consumido
        var providerUsers = await _db.Users
            .Where(u => u.OwnerId == currentUser.OwnerId
                     && u.IdProfile == request.ConsumedProfileId
                     && u.IsActive)
            .Select(u => new {
                u.Id,
                u.CompleteName,
                u.Login,
                Connector = _db.UserEDCConnectors
                    .FirstOrDefault(c => c.UserId == u.Id)
            })
            .ToListAsync(ct);

        // 4. EJECUTAR SOLICITUDES EN PARALELO con throttling
        var semaphore = new SemaphoreSlim(_edcOptions.Value.MaxConcurrentRequests); // default 5
        var tasks = providerUsers.Select(async provider =>
        {
            var result = new EdcProviderCatalogResult
            {
                UserId = provider.Id,
                UserName = provider.CompleteName ?? provider.Login,
                UserLogin = provider.Login
            };

            if (provider.Connector == null)
            {
                result.Status = EdcProviderStatus.NoConnector;
                return result;
            }

            result.EDCServerName = provider.Connector.EDCServerName;
            result.EDCConnectorId = provider.Connector.EDCConnectorId;

            var counterPartyUrl = $"https://proto.{provider.Connector.EDCServerName}/protocol";

            await semaphore.WaitAsync(ct);
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(_edcOptions.Value.RequestTimeoutSeconds));

                var catalogResult = await _edcClient.RequestCatalogAsync(
                    consumerMgmtUrl, counterPartyUrl, timeoutCts.Token);

                result.CatalogResult = catalogResult;
                result.Status = catalogResult.Success
                    ? EdcProviderStatus.Ok
                    : EdcProviderStatus.Error;
            }
            catch (OperationCanceledException)
            {
                result.Status = EdcProviderStatus.Timeout;
                result.CatalogResult = new EdcCatalogResult
                {
                    Success = false,
                    ErrorMessage = $"Timeout tras {_edcOptions.Value.RequestTimeoutSeconds}s"
                };
                _logger.LogWarning(
                    "Timeout solicitando catálogo EDC del proveedor {UserLogin} (server: {Server})",
                    provider.Login, provider.Connector.EDCServerName);
            }
            catch (Exception ex)
            {
                result.Status = EdcProviderStatus.Error;
                result.CatalogResult = new EdcCatalogResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
                _logger.LogError(ex,
                    "Error solicitando catálogo EDC del proveedor {UserLogin} (server: {Server})",
                    provider.Login, provider.Connector.EDCServerName);
            }
            finally
            {
                semaphore.Release();
            }

            return result;
        });

        var results = await Task.WhenAll(tasks);

        // 5. AGREGAR RESULTADOS
        return new RequestEdcCatalogResponse
        {
            ConsumerServerName = consumerConnector.EDCServerName,
            TotalProviders = results.Length,
            SuccessCount = results.Count(r => r.Status == EdcProviderStatus.Ok),
            ErrorCount = results.Count(r => r.Status == EdcProviderStatus.Error),
            NoConnectorCount = results.Count(r => r.Status == EdcProviderStatus.NoConnector),
            Results = results.ToList()
        };
    }
}
```

#### 6.1.4. Validator

```csharp
public class RequestEdcCatalogCommandValidator : AbstractValidator<RequestEdcCatalogCommand>
{
    public RequestEdcCatalogCommandValidator()
    {
        RuleFor(x => x.ConsumedProfileId).GreaterThan(0)
            .WithMessage("Debe seleccionar un perfil a consumir.");
        RuleFor(x => x.ConsumerUserId).GreaterThan(0)
            .WithMessage("Se requiere un usuario consumidor.");
    }
}
```

#### 6.1.5. Opciones de configuración

Crear en `Application/Common/Options/` (o donde el proyecto ubique las clases de opciones):

```csharp
public class EdcOptions
{
    public const string SectionName = "EcoDataNet:Edc";

    /// <summary>Máximo de solicitudes de catálogo simultáneas.</summary>
    public int MaxConcurrentRequests { get; set; } = 5;

    /// <summary>Timeout por solicitud individual, en segundos.</summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>API Key para la Management API (header X-Api-Key). Vacío = no enviar header.</summary>
    public string ManagementApiKey { get; set; } = string.Empty;
}
```

---

### CAPA 2 — Infrastructure (`GreenTransit.Infrastructure`)

#### 6.2.1. Implementación de `IEdcManagementClient`

Crear en `Infrastructure/Services/` (o `Infrastructure/ExternalServices/`, según convención del repo):

```csharp
public class EdcManagementClient : IEdcManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<EdcOptions> _options;
    private readonly ILogger<EdcManagementClient> _logger;

    public EdcManagementClient(
        HttpClient httpClient,
        IOptions<EdcOptions> options,
        ILogger<EdcManagementClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<EdcCatalogResult> RequestCatalogAsync(
        string consumerManagementBaseUrl,
        string counterPartyProtocolUrl,
        CancellationToken cancellationToken = default)
    {
        var requestUrl = $"{consumerManagementBaseUrl.TrimEnd('/')}/v3/catalog/request";

        var body = new
        {
            @context = new Dictionary<string, string>
            {
                ["@vocab"] = "https://w3id.org/edc/v0.0.1/ns/"
            },
            @type = "CatalogRequest",
            counterPartyAddress = counterPartyProtocolUrl,
            protocol = "dataspace-protocol-http"
        };

        // Serializar con System.Text.Json preservando "@context" y "@type"
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null, // mantener nombres exactos
            WriteIndented = false
        };
        var jsonBody = JsonSerializer.Serialize(body, jsonOptions);

        // NOTA: System.Text.Json escapa '@' en las claves por defecto.
        // Para generar JSON-LD correcto con '@context' y '@type', usar
        // serialización manual o un Dictionary<string, object>:
        var requestBody = new Dictionary<string, object>
        {
            ["@context"] = new Dictionary<string, string>
            {
                ["@vocab"] = "https://w3id.org/edc/v0.0.1/ns/"
            },
            ["@type"] = "CatalogRequest",
            ["counterPartyAddress"] = counterPartyProtocolUrl,
            ["protocol"] = "dataspace-protocol-http"
        };

        var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // API Key opcional
        var apiKey = _options.Value.ManagementApiKey;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Add("X-Api-Key", apiKey);
        }

        _logger.LogInformation(
            "Solicitando catálogo EDC: POST {Url} | counterParty: {CounterParty}",
            requestUrl, counterPartyProtocolUrl);

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Respuesta no exitosa de EDC Management API: {StatusCode} | URL: {Url} | Body: {Body}",
                    (int)response.StatusCode, requestUrl, responseBody.Truncate(500));

                return new EdcCatalogResult
                {
                    Success = false,
                    HttpStatusCode = (int)response.StatusCode,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}: {responseBody.Truncate(200)}",
                    RawJson = responseBody
                };
            }

            // Contar datasets de forma básica:
            // El catálogo DCAT suele tener un array "dcat:dataset" o "@graph"
            var datasetCount = CountDatasetsFromJson(responseBody);

            _logger.LogInformation(
                "Catálogo EDC recibido con éxito: {DatasetCount} datasets | URL: {Url}",
                datasetCount, requestUrl);

            return new EdcCatalogResult
            {
                Success = true,
                HttpStatusCode = (int)response.StatusCode,
                RawJson = responseBody,
                DatasetCount = datasetCount
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de conexión con EDC Management API: {Url}", requestUrl);
            return new EdcCatalogResult
            {
                Success = false,
                ErrorMessage = $"Error de conexión: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Cuenta datasets del JSON de respuesta del catálogo de forma heurística.
    /// El catálogo DCAT/JSON-LD puede venir en distintos formatos.
    /// Copilot: inspeccionar la respuesta real del entorno de pruebas y ajustar.
    /// </summary>
    private int CountDatasetsFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Intentar "dcat:dataset" como array
            if (root.TryGetProperty("dcat:dataset", out var datasetProp))
            {
                return datasetProp.ValueKind == JsonValueKind.Array
                    ? datasetProp.GetArrayLength()
                    : 1;
            }

            // Intentar "https://www.w3.org/ns/dcat#dataset"
            if (root.TryGetProperty("https://www.w3.org/ns/dcat#dataset", out var datasetProp2))
            {
                return datasetProp2.ValueKind == JsonValueKind.Array
                    ? datasetProp2.GetArrayLength()
                    : 1;
            }

            // Intentar "@graph" (JSON-LD expandido)
            if (root.TryGetProperty("@graph", out var graphProp)
                && graphProp.ValueKind == JsonValueKind.Array)
            {
                // Contar elementos con @type que contenga "Dataset"
                return graphProp.EnumerateArray()
                    .Count(e => e.TryGetProperty("@type", out var t)
                             && t.GetString()?.Contains("Dataset", StringComparison.OrdinalIgnoreCase) == true);
            }

            _logger.LogDebug("No se pudo determinar el número de datasets del catálogo EDC");
            return 0;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Error parseando JSON del catálogo EDC para contar datasets");
            return 0;
        }
    }
}
```

> **NOTA sobre `Truncate`**: si no existe un extension method `string.Truncate(int)` en el proyecto, Copilot debe crear uno sencillo o sustituir por `responseBody[..Math.Min(responseBody.Length, 500)]`.

#### 6.2.2. Registro de servicios

En `Program.cs` (o en la clase de extensión `DependencyInjection.cs` de Infrastructure):

```csharp
// Configuración EDC
builder.Services.Configure<EdcOptions>(
    builder.Configuration.GetSection(EdcOptions.SectionName));

// HttpClient para EDC Management API
builder.Services.AddHttpClient<IEdcManagementClient, EdcManagementClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60); // timeout global del HttpClient
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});
```

> **Copilot**: verificar si el proyecto ya tiene un método `AddInfrastructure()` o similar donde centralizar estos registros. Si existe, añadir ahí en lugar de directamente en `Program.cs`.

> Si el proyecto ya usa `Polly` para políticas de retry en otros `HttpClient`, reutilizar el patrón. Si no, NO añadir `Polly` — el manejo de errores del handler ya es suficiente.

---

### CAPA 3 — Web (`GreenTransit.Web`)

#### 6.3.1. Modificación del componente `ConsumeData.razor`

Buscar el componente con `@page "/ecodatanet/consume-data"` y modificarlo:

**Estado adicional en el componente:**

```csharp
@code {
    // ... variables existentes del componente (perfil seleccionado, etc.) ...

    // NUEVO: estado del consumo de catálogo
    private bool _isLoadingCatalog = false;
    private RequestEdcCatalogResponse? _catalogResponse = null;
    private string? _catalogError = null;
}
```

**Reemplazar el botón placeholder actual** por:

```razor
@* Botón "Consumir catálogo" — ahora funcional *@
<button class="btn btn-primary"
        disabled="@(_isLoadingCatalog || _selectedConsumedProfileId == 0)"
        @onclick="OnConsumeCatalogAsync">
    @if (_isLoadingCatalog)
    {
        <span class="spinner-border spinner-border-sm me-2" role="status"></span>
        <span>Consultando conectores EDC...</span>
    }
    else
    {
        <i class="bi bi-cloud-download me-2"></i>
        <span>Consumir catálogo</span>
    }
</button>
```

> **Copilot**: adaptar las clases CSS y el icono al patrón visual existente del proyecto (puede que use Radzen en lugar de Bootstrap puro — verificar el repo).

**Método del botón:**

```csharp
private async Task OnConsumeCatalogAsync()
{
    _isLoadingCatalog = true;
    _catalogResponse = null;
    _catalogError = null;
    StateHasChanged();

    try
    {
        // Determinar el ConsumerUserId:
        // - Si NO es ADMIN: usar el userId del usuario logueado
        // - Si es ADMIN: usar el userId del usuario que se está "representando"
        //   (depende de la UI actual — si hay un selector de usuario en modo ADMIN, usarlo;
        //    si no, usar el usuario logueado como consumidor)
        var consumerUserId = /* obtener del ICurrentUserService o del estado del componente */;

        var response = await Mediator.Send(new RequestEdcCatalogCommand
        {
            ConsumedProfileId = _selectedConsumedProfileId,
            ConsumerUserId = consumerUserId
        });

        _catalogResponse = response;
    }
    catch (ValidationException ex)
    {
        _catalogError = ex.Message;
    }
    catch (Exception ex)
    {
        _catalogError = $"Error inesperado: {ex.Message}";
        Logger.LogError(ex, "Error al consumir catálogo EDC");
    }
    finally
    {
        _isLoadingCatalog = false;
        StateHasChanged();
    }
}
```

**Panel de resultados** (añadir debajo del botón):

```razor
@* Mensaje de error general *@
@if (!string.IsNullOrEmpty(_catalogError))
{
    <div class="alert alert-danger mt-3">
        <i class="bi bi-exclamation-triangle me-2"></i>@_catalogError
    </div>
}

@* Resultados del catálogo *@
@if (_catalogResponse != null)
{
    <div class="mt-4">
        <h5>Resultados del descubrimiento de catálogo</h5>

        @* Resumen *@
        <div class="d-flex gap-3 mb-3">
            <span class="badge bg-info">@_catalogResponse.TotalProviders proveedores</span>
            <span class="badge bg-success">@_catalogResponse.SuccessCount OK</span>
            @if (_catalogResponse.ErrorCount > 0)
            {
                <span class="badge bg-danger">@_catalogResponse.ErrorCount errores</span>
            }
            @if (_catalogResponse.NoConnectorCount > 0)
            {
                <span class="badge bg-warning text-dark">@_catalogResponse.NoConnectorCount sin conector</span>
            }
        </div>

        <p class="text-muted small">
            Conector consumidor: @_catalogResponse.ConsumerServerName
        </p>

        @* Detalle por proveedor *@
        <div class="accordion" id="catalogResults">
            @foreach (var (result, index) in _catalogResponse.Results.Select((r, i) => (r, i)))
            {
                <div class="accordion-item">
                    <h2 class="accordion-header">
                        <button class="accordion-button collapsed" type="button"
                                data-bs-toggle="collapse"
                                data-bs-target="#collapse-@index">
                            @switch (result.Status)
                            {
                                case EdcProviderStatus.Ok:
                                    <span class="badge bg-success me-2">OK</span>
                                    break;
                                case EdcProviderStatus.Error:
                                case EdcProviderStatus.Timeout:
                                    <span class="badge bg-danger me-2">@result.Status</span>
                                    break;
                                case EdcProviderStatus.NoConnector:
                                    <span class="badge bg-warning text-dark me-2">SIN CONECTOR</span>
                                    break;
                            }
                            <strong>@result.UserName</strong>
                            <span class="text-muted ms-2">(@result.UserLogin)</span>
                            @if (result.Status == EdcProviderStatus.Ok && result.CatalogResult != null)
                            {
                                <span class="ms-2">— @result.CatalogResult.DatasetCount datasets</span>
                            }
                        </button>
                    </h2>
                    <div id="collapse-@index" class="accordion-collapse collapse">
                        <div class="accordion-body">
                            @if (result.EDCServerName != null)
                            {
                                <p><strong>Servidor:</strong> @result.EDCServerName</p>
                                <p><strong>Conector ID:</strong> @result.EDCConnectorId</p>
                            }
                            @if (result.CatalogResult != null)
                            {
                                @if (!string.IsNullOrEmpty(result.CatalogResult.ErrorMessage))
                                {
                                    <div class="alert alert-warning">@result.CatalogResult.ErrorMessage</div>
                                }
                                @if (!string.IsNullOrEmpty(result.CatalogResult.RawJson))
                                {
                                    <details>
                                        <summary>Ver JSON completo del catálogo</summary>
                                        <pre class="bg-dark text-light p-3 rounded mt-2"
                                             style="max-height: 400px; overflow-y: auto; font-size: 12px;">
@FormatJson(result.CatalogResult.RawJson)
                                        </pre>
                                    </details>
                                }
                            }
                            @if (result.Status == EdcProviderStatus.NoConnector)
                            {
                                <p class="text-muted">
                                    Este usuario no tiene un conector EDC configurado.
                                    Se puede configurar desde "Configuración conector EDC".
                                </p>
                            }
                        </div>
                    </div>
                </div>
            }
        </div>
    </div>
}
```

**Método auxiliar para formatear JSON:**

```csharp
private string FormatJson(string json)
{
    try
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }
    catch
    {
        return json;
    }
}
```

> **Copilot**: si el proyecto usa Radzen, adaptar los badges, alerts y accordion a componentes Radzen equivalentes (`RadzenBadge`, `RadzenAlert`, `RadzenAccordion`, `RadzenAccordionItem`). Inspeccionar los componentes usados en el resto del proyecto para mantener consistencia.

---

### CAPA 4 — Configuración (`appsettings.json`)

Añadir la sección en `appsettings.json`:

```json
{
  "EcoDataNet": {
    "Edc": {
      "MaxConcurrentRequests": 5,
      "RequestTimeoutSeconds": 30,
      "ManagementApiKey": ""
    }
  }
}
```

Si existe `appsettings.Development.json`, añadir ahí valores para desarrollo/test:

```json
{
  "EcoDataNet": {
    "Edc": {
      "MaxConcurrentRequests": 3,
      "RequestTimeoutSeconds": 15,
      "ManagementApiKey": ""
    }
  }
}
```

---

## ✅ 7. Criterios de aceptación

- [ ] **CA-1**: El botón "Consumir catálogo" ejecuta una solicitud real POST a la Management API del conector del usuario consumidor.
- [ ] **CA-2**: El body de la request es JSON-LD exacto con `@context`, `@type: CatalogRequest`, `counterPartyAddress` y `protocol: dataspace-protocol-http`.
- [ ] **CA-3**: El `counterPartyAddress` se construye como `https://proto.{providerEDCServerName}/protocol` para cada proveedor.
- [ ] **CA-4**: La Management API se invoca en `https://mgmt.{consumerEDCServerName}/management/v3/catalog/request`.
- [ ] **CA-5**: Se consultan TODOS los usuarios del tenant con el perfil consumido seleccionado, y se lanza una request por cada uno que tenga conector configurado.
- [ ] **CA-6**: Los usuarios sin conector se marcan como `SIN_CONECTOR` en el resumen sin bloquear las demás solicitudes.
- [ ] **CA-7**: Los errores HTTP (4xx/5xx), timeouts y excepciones de red se capturan individualmente y no cancelan el resto de solicitudes.
- [ ] **CA-8**: El componente Blazor muestra estado "cargando" mientras se ejecutan las solicitudes y deshabilita el botón.
- [ ] **CA-9**: El resumen muestra badges con contadores (OK / Error / Sin conector) y un detalle por proveedor con accordion/colapsable.
- [ ] **CA-10**: El JSON bruto del catálogo se muestra en un panel colapsable formateado para depuración.
- [ ] **CA-11**: Un usuario NO ADMIN no puede consumir catálogos de perfiles que no estén en su `ProfileEDCConsumer`.
- [ ] **CA-12**: Toda la lógica HTTP vive en Infrastructure (`EdcManagementClient`), NO en el componente Blazor ni directamente en el handler.
- [ ] **CA-13**: La API Key se envía como header `X-Api-Key` solo si está configurada; si está vacía, no se envía ningún header de autenticación.
- [ ] **CA-14**: El filtrado de usuarios proveedores respeta `OwnerId` (multi-tenant) y `IsActive`.
- [ ] **CA-15**: Se registra log (Serilog) para cada solicitud (inicio, éxito, error, timeout).
- [ ] **CA-16**: El proyecto compila sin errores y sin warnings nuevos.

---

## 🧪 8. Plan de pruebas manual

### Test 1 — Consumo exitoso con un proveedor real

1. Configurar en BD: un usuario con perfil PLANT_OP y su `UserEDCConnector` apuntando a un servidor EDC real de pruebas.
2. Configurar en BD: `ProfileEDCConsumer` para que el perfil del usuario logueado pueda consumir PLANT_OP.
3. Configurar el `UserEDCConnector` del usuario logueado (consumidor) con su servidor EDC.
4. Login como usuario NO ADMIN.
5. Navegar a "Consumir datos". Seleccionar el perfil PLANT_OP del desplegable.
6. Pulsar "Consumir catálogo".
7. **Verificar**: spinner visible, botón deshabilitado, resultado con badge OK, nº de datasets, JSON colapsable.

### Test 2 — Proveedor sin conector configurado

1. Crear un usuario con perfil PLANT_OP pero SIN registro en `UserEDCConnector`.
2. Repetir flujo del Test 1.
3. **Verificar**: el proveedor aparece con badge "SIN CONECTOR" y mensaje informativo. Las demás solicitudes no se ven afectadas.

### Test 3 — Consumidor sin conector configurado

1. Login como usuario que NO tiene `UserEDCConnector`.
2. Pulsar "Consumir catálogo".
3. **Verificar**: mensaje de error claro indicando que debe configurar su conector EDC primero. No se dispara ninguna solicitud HTTP.

### Test 4 — Proveedor con servidor caído / timeout

1. Configurar un `UserEDCConnector` con un servidor EDC inexistente (ej: `servidor-inexistente.test.local`).
2. Pulsar "Consumir catálogo".
3. **Verificar**: el proveedor aparece con badge ERROR o TIMEOUT. El mensaje de error incluye información útil. Los demás proveedores (si los hay) se procesan correctamente.

### Test 5 — Seguridad: NO ADMIN intenta consumir perfil no autorizado

1. Login como SCRAP.
2. Verificar que el desplegable de perfiles solo muestra los perfiles que `ProfileEDCConsumer` autoriza para SCRAP.
3. Si se intenta manipular la request (via consola del navegador) para pasar un `ConsumedProfileId` no autorizado, el handler debe rechazarlo.

### Test 6 — ADMIN: consumo con selección de perfil

1. Login como ADMIN.
2. Navegar a "Consumir datos".
3. Seleccionar un perfil de la lista de perfiles.
4. Seleccionar un perfil consumible del desplegable.
5. Pulsar "Consumir catálogo".
6. **Verificar**: se ejecuta correctamente usando el conector del ADMIN como consumidor.

### Test 7 — Entorno de pruebas OFIASIGNACIÓN

1. Configurar un `UserEDCConnector` con:
   - `EDCServerName`: `ecoucofiasignacion.ecodatanetconn3.dataspace.wastenode.com`
   - `EDCConnectorId`: (el identificador real del conector en ese entorno)
2. El Management API resultante será: `mgmt.ecoucofiasignacion.ecodatanetconn3.dataspace.wastenode.com/management`
3. El Protocol API (counterPartyAddress) será: `proto.ecoucofiasignacion.ecodatanetconn3.dataspace.wastenode.com/protocol`
4. Ejecutar "Consumir catálogo" contra ese proveedor.
5. **Verificar**: la respuesta llega, se muestra el JSON del catálogo DCAT, y se cuentan los datasets correctamente.

### Test 8 — Multi-tenant: aislamiento

1. Crear dos tenants (OwnerId A y B) con usuarios PLANT_OP en ambos.
2. Login como usuario del Tenant A.
3. Consumir catálogo del perfil PLANT_OP.
4. **Verificar**: solo aparecen proveedores del Tenant A. Ningún usuario del Tenant B es visible.

---

## 🧪 9. Tests automatizados

Buscar el proyecto `Tests` y el framework de test utilizado (xUnit, NUnit, etc.). Si existen tests unitarios, sugerir la creación de:

### 9.1. `EdcManagementClientTests`

- `RequestCatalogAsync_ReturnsSuccess_WhenApiReturns200`: mock de `HttpMessageHandler` que devuelve 200 con JSON de catálogo. Verificar que `Success == true` y `DatasetCount > 0`.
- `RequestCatalogAsync_ReturnsError_WhenApiReturns4xx`: mock que devuelve 400/401/403. Verificar `Success == false` y `ErrorMessage` contiene el status code.
- `RequestCatalogAsync_ReturnsError_WhenConnectionFails`: mock que lanza `HttpRequestException`. Verificar `Success == false`.
- `RequestCatalogAsync_SendsApiKeyHeader_WhenConfigured`: verificar que el header `X-Api-Key` se envía cuando `ManagementApiKey` no está vacío.
- `RequestCatalogAsync_DoesNotSendApiKeyHeader_WhenEmpty`: verificar que NO se envía header cuando está vacío.
- `RequestCatalogAsync_BuildsCorrectUrl`: verificar que la URL del POST es `{baseUrl}/v3/catalog/request`.
- `RequestCatalogAsync_SendsCorrectJsonLdBody`: capturar el body enviado y verificar que contiene `@context`, `@type`, `counterPartyAddress`, `protocol`.

### 9.2. `RequestEdcCatalogCommandHandlerTests`

- `Handle_ThrowsValidation_WhenConsumerHasNoConnector`: usuario sin `UserEDCConnector` → `ValidationException`.
- `Handle_ReturnsNoConnector_ForProvidersWithoutConnector`: proveedores sin conector → status `NoConnector`.
- `Handle_ThrowsUnauthorized_WhenNonAdminConsumesUnauthorizedProfile`: verificar que el handler rechaza perfiles no autorizados por `ProfileEDCConsumer`.
- `Handle_FiltersProvidersByOwnerId`: verificar que solo se devuelven usuarios del mismo tenant.
- `Handle_AggregatesResults_WhenMultipleProviders`: 3 proveedores (1 OK, 1 ERROR, 1 SIN_CONECTOR) → verificar contadores.

### 9.3. `RequestEdcCatalogCommandValidatorTests`

- `Validate_Fails_WhenConsumedProfileIdIsZero`.
- `Validate_Fails_WhenConsumerUserIdIsZero`.
- `Validate_Passes_WhenAllFieldsValid`.

No inventar framework de test ni librería de mocking — usar los mismos que ya existan en el proyecto.
