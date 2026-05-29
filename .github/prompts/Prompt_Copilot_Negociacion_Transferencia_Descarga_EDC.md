# Prompt para GitHub Copilot — Negociación de Contrato, Transferencia y Descarga de Datos EDC v3

> **Instrucción**: Copia este prompt completo en GitHub Copilot Chat adjuntando los archivos `COPILOT_CONTEXT.md`, `README.md`, `Crear_BD_v4_1.sql` e `instrucciones_adicionales.md` de tu proyecto. Copilot debe inspeccionar el repo para confirmar estructuras, nombres de clases y patrones reales antes de ejecutar cambios.

---

## 🎯 1. Objetivo

Implementar **end-to-end** el flujo de negociación de contrato, transferencia de datos y descarga dentro de la pantalla `/ecodatanet/consume-data` (vista "Ver oferta"), partiendo del estado `EdcNegotiationSelection` que ya se guarda al pulsar "Iniciar negociación" en el flujo existente de visualización de catálogo DCAT/ODRL.

El flujo completo es:

```
Oferta seleccionada (EdcNegotiationSelection ya existe)
  → Pulsar "Iniciar negociación" (ahora SÍ ejecuta la request real)
    → POST /v3/contractnegotiations/ contra Management API del consumidor
    → Polling de estado de negociación (stepper visual)
      → Al llegar a FINALIZED:
        → Automáticamente POST /v3/transferprocesses
        → Polling de estado de transferencia (stepper visual)
          → Al llegar a COMPLETED/STARTED:
            → Habilitar botón "Descargar datos"
            → Obtener endpoint de descarga (EDR) + token
            → Descargar datos y mostrar/exportar
```

---

## 📐 2. Alcance

### Incluido

- Convertir el botón "Iniciar negociación" de placeholder (que solo guarda estado) en una acción real que ejecuta `POST /v3/contractnegotiations/`.
- Crear un Command MediatR `StartContractNegotiationCommand` que envíe el `ContractRequest` a la Management API del conector consumidor.
- Crear un Query MediatR `GetNegotiationStateQuery` que consulte `GET /v3/contractnegotiations/{id}` para polling.
- Crear un Command MediatR `StartTransferProcessCommand` que envíe el `TransferRequestDto` a la Management API del conector consumidor.
- Crear un Query MediatR `GetTransferStateQuery` que consulte `GET /v3/transferprocesses/{id}` para polling.
- Crear un Query MediatR `GetEndpointDataReferenceQuery` que consulte `GET /v3/edrs/{transferProcessId}/dataaddress` para obtener la dirección de descarga del data plane.
- Crear un Command MediatR `DownloadTransferDataCommand` que descargue los datos desde el data plane (Public API) usando el token EDR.
- Ampliar `IEdcManagementClient` (ya existe) con los métodos nuevos para negociación, transferencia, EDR y descarga.
- Implementar steppers visuales para los estados de negociación y transferencia en el componente Blazor.
- Implementar mecanismo de polling con `PeriodicTimer` y `CancellationTokenSource` en el componente Blazor.
- Mantener logging estructurado (Serilog) en cada paso.
- Respetar multi-tenant: conector del consumidor localizado por `OwnerId`.
- Mantener el proyecto compilando y sin warnings nuevos.

### Fuera de alcance

- Cambios en tablas `UserEDCConnector` o `ProfileEDCConsumer` (no se modifican).
- Nuevas pantallas — todo se implementa dentro de la vista existente de "Ver oferta" en `/ecodatanet/consume-data`.
- Autenticación mutua TLS, certificados de cliente o flujos OAuth entre conectores.
- Persistencia en BD de estados de negociación/transferencia (se manejan en memoria del componente; en futuras iteraciones se podrá persistir).
- Notificaciones push o SignalR para estados (se usa polling simple).
- Retry automático de negociaciones o transferencias fallidas (solo retry manual con botón "Reintentar").

---

## 🔎 3. Suposiciones y cómo verificarlas en el repo

| Suposición | Cómo verificar |
|---|---|
| Existe `IEdcManagementClient` con método `RequestCatalogAsync` | Buscar `interface IEdcManagementClient` en `Application/Common/Interfaces/` o `Application/Interfaces/` |
| Existe `EdcManagementClient` que implementa la interfaz | Buscar `class EdcManagementClient` en `Infrastructure/Services/` |
| Existe `EdcNegotiationSelection` con `SelectedOfferId`, `SelectedDatasetId`, `ProviderParticipantId`, `ProviderProtocolEndpoint` | Buscar `EdcNegotiationSelection` en `Application/Features/EcoDataNet/DTOs/` |
| El componente Blazor de consume-data tiene campo `_negotiationSelection` | Buscar `_negotiationSelection` en el `.razor` con `@page "/ecodatanet/consume-data"` |
| Existen DTOs de catálogo DCAT: `EdcCatalogDto`, `EdcDatasetDto`, `EdcOfferDto` con `OfferId`, permisos, prohibiciones, obligaciones | Buscar en `Application/Features/EcoDataNet/DTOs/` |
| `EdcOfferDto` contiene la offer ODRL completa (Permissions, Prohibitions, Obligations) | Inspeccionar la clase `EdcOfferDto` |
| Existe `EdcProviderParsedCatalogDto` con `ProviderProtocolEndpoint` y `ProviderParticipantId` | Buscar `EdcProviderParsedCatalogDto` en DTOs |
| El `HttpClient` para EDC ya está registrado con `HttpClientFactory` | Buscar `AddHttpClient("EdcManagement"` o similar en `Program.cs` o `DependencyInjection.cs` |
| Existen `EdcOptions` con `ManagementApiKey`, `RequestTimeoutSeconds` | Buscar `EdcOptions` o `EcoDataNet:Edc` en `appsettings.json` |
| `ICurrentUserService` tiene `OwnerId`, `ProfileId`, `UserId` | Buscar `ICurrentUserService` en `Application/` |
| El proyecto usa Serilog | Buscar `Serilog` en `Program.cs` o csproj |
| La oferta ODRL original (JSON) se conserva o es reconstruible desde los DTOs | Verificar si `EdcOfferDto` guarda `RawOfferJson` o si los DTOs tienen suficiente info para reconstruir el payload ODRL |

**INVESTIGACIÓN CRÍTICA**: Copilot debe verificar si `EdcOfferDto` almacena el JSON original de la offer ODRL (`RawOfferJson`). Si NO lo hace, hay dos opciones:
1. **Preferida**: Añadir un campo `string? RawOfferJson` a `EdcOfferDto` y poblarlo en `EdcCatalogParser` con el JSON crudo del nodo `odrl:hasPolicy` del catálogo. Esto garantiza que la offer se envía exactamente como la devolvió el proveedor.
2. **Alternativa**: Reconstruir el JSON ODRL a partir de los DTOs (`OfferId`, `Permissions`, `Prohibitions`, `Obligations`). Menos fiable porque puede perder campos no mapeados.

---

## 🔍 4. Cómo localizar el código existente

| Qué buscar | Patrón de búsqueda |
|---|---|
| Componente de consumo de datos | `@page "/ecodatanet/consume-data"` en `Web/Components/Pages/EcoDataNet/` |
| Estado de negociación seleccionada | `_negotiationSelection` en el componente `.razor` |
| Banner de "Oferta seleccionada" | `alert-success` y `EdcNegotiationSelection` en el componente `.razor` |
| IEdcManagementClient | `interface IEdcManagementClient` en `Application/` |
| EdcManagementClient implementación | `class EdcManagementClient` en `Infrastructure/Services/` |
| DTOs EcoDataNet | `Application/Features/EcoDataNet/DTOs/` |
| EdcCatalogParser (parseo DCAT) | `class EdcCatalogParser` en `Infrastructure/Services/` |
| UserEDCConnector entity | `class UserEDCConnector` en `Domain/Entities/` |
| HttpClient factory config | `AddHttpClient` en `Program.cs` o `Infrastructure/DependencyInjection.cs` |
| EdcOptions config | `EcoDataNet:Edc` en `appsettings.json` |
| Patrones de Command/Query existentes | Cualquier `IRequest<>` en `Application/Features/EcoDataNet/` |

---

## 🏗️ 5. Diseño funcional — Flujo de estados

### 5.1. Máquina de estados de negociación (EDC v3)

```
[INITIAL] → [REQUESTING] → [REQUESTED] → [OFFERED] → [ACCEPTING] → [ACCEPTED] → [AGREEING] → [AGREED] → [VERIFYING] → [VERIFIED] → [FINALIZING] → [FINALIZED]
                                                                                                                                              ↘
                                                                                                                                          [TERMINATING] → [TERMINATED]
```

**Estados visibles en el stepper de UI** (simplificados — los estados transitorios `*ING` se agrupan con su padre):

| Paso stepper | Estados EDC que lo activan | Icono |
|---|---|---|
| 1. Solicitada | `INITIAL`, `REQUESTING`, `REQUESTED` | 🔵 spinner |
| 2. Oferta recibida | `OFFERED` | 🔵 check |
| 3. Aceptada | `ACCEPTING`, `ACCEPTED` | 🔵 check |
| 4. Acordada | `AGREEING`, `AGREED` | 🔵 check |
| 5. Verificada | `VERIFYING`, `VERIFIED` | 🔵 check |
| 6. Finalizada | `FINALIZING`, `FINALIZED` | ✅ check verde |
| ❌ Error | `TERMINATED` o cualquier estado inesperado | ❌ rojo |

### 5.2. Máquina de estados de transferencia (EDC v3)

```
[INITIAL] → [PROVISIONING] → [PROVISIONED] → [REQUESTING] → [REQUESTED] → [STARTING] → [STARTED]
                                                                                           ↘
                                                                                      [COMPLETING] → [COMPLETED]
                                                                                           ↘
                                                                                      [TERMINATING] → [TERMINATED]
```

**Estados visibles en el stepper de UI**:

| Paso stepper | Estados EDC que lo activan | Icono |
|---|---|---|
| 1. Iniciada | `INITIAL`, `PROVISIONING`, `PROVISIONED` | 🔵 spinner |
| 2. Solicitada | `REQUESTING`, `REQUESTED` | 🔵 check |
| 3. En curso | `STARTING`, `STARTED` | 🔵 check |
| 4. Completada | `COMPLETING`, `COMPLETED` | ✅ check verde → habilita Download |
| ❌ Error | `TERMINATED` o cualquier estado inesperado | ❌ rojo |

> **Nota**: En EDC v3, el estado `STARTED` (no `READY`) es el que indica que el data plane está listo para transferencia tipo `HttpData-PULL`. Al recibir `STARTED` o `COMPLETED`, se habilita la descarga. Copilot debe verificar esto con la OpenAPI del EDC Management API v3 (buscar en el repo si existe un fichero OpenAPI/Swagger, o consultar la documentación oficial de Eclipse EDC).

### 5.3. Flujo de descarga (EDR — Endpoint Data Reference)

Cuando el transfer process alcanza `STARTED` (para `HttpData-PULL`):

1. Consultar **EDR (Endpoint Data Reference)**: `GET /v3/edrs/{transferProcessId}/dataaddress` contra la Management API del consumidor.
2. La respuesta contiene:
   ```json
   {
     "@type": "DataAddress",
     "type": "https://w3id.org/idsa/v4.1/HTTP",
     "endpoint": "https://public.{providerServer}/public/...",
     "authorization": "Bearer <token-temporal>"
   }
   ```
3. Con el `endpoint` y el `authorization` token, hacer `GET` al endpoint del data plane para descargar los datos.
4. Mostrar el resultado (JSON) al usuario y permitir exportar a fichero.

> **INVESTIGACIÓN OBLIGATORIA**: Copilot debe verificar el endpoint EDR exacto. Puede ser:
> - `GET /v3/edrs/{transferProcessId}/dataaddress` (EDC >= 0.7.x)
> - `GET /v3/transferprocesses/{id}/dataaddress` (versiones anteriores)
> Buscar en el repo si hay OpenAPI o swagger de EDC. Si no existe, usar `/v3/edrs/{transferProcessId}/dataaddress` como valor por defecto y documentar la alternativa.

---

## ⚙️ 6. Diseño técnico por capas

### CAPA 1 — Application (`GreenTransit.Application`)

#### 6.1.1. Ampliar `IEdcManagementClient`

Añadir a la interfaz existente `IEdcManagementClient` estos métodos:

```csharp
/// <summary>
/// Inicia una negociación de contrato con un proveedor EDC.
/// POST /v3/contractnegotiations/ contra la Management API del consumidor.
/// </summary>
/// <param name="consumerManagementBaseUrl">URL base Management API consumidor</param>
/// <param name="contractRequestPayload">Payload JSON-LD completo del ContractRequest</param>
/// <param name="cancellationToken">Token de cancelación</param>
/// <returns>ID de la negociación creada y estado inicial</returns>
Task<EdcNegotiationResponse> StartNegotiationAsync(
    string consumerManagementBaseUrl,
    string contractRequestPayload,
    CancellationToken cancellationToken = default);

/// <summary>
/// Consulta el estado de una negociación de contrato.
/// GET /v3/contractnegotiations/{negotiationId} contra Management API del consumidor.
/// </summary>
Task<EdcNegotiationStateResponse> GetNegotiationStateAsync(
    string consumerManagementBaseUrl,
    string negotiationId,
    CancellationToken cancellationToken = default);

/// <summary>
/// Inicia un proceso de transferencia de datos.
/// POST /v3/transferprocesses contra Management API del consumidor.
/// </summary>
Task<EdcTransferResponse> StartTransferAsync(
    string consumerManagementBaseUrl,
    string transferRequestPayload,
    CancellationToken cancellationToken = default);

/// <summary>
/// Consulta el estado de un proceso de transferencia.
/// GET /v3/transferprocesses/{transferId} contra Management API del consumidor.
/// </summary>
Task<EdcTransferStateResponse> GetTransferStateAsync(
    string consumerManagementBaseUrl,
    string transferId,
    CancellationToken cancellationToken = default);

/// <summary>
/// Obtiene la referencia de datos del endpoint (EDR) para descargar datos.
/// GET /v3/edrs/{transferProcessId}/dataaddress contra Management API del consumidor.
/// </summary>
Task<EdcEndpointDataReferenceResponse> GetEndpointDataReferenceAsync(
    string consumerManagementBaseUrl,
    string transferProcessId,
    CancellationToken cancellationToken = default);

/// <summary>
/// Descarga datos desde el data plane del proveedor usando el endpoint y token del EDR.
/// GET al endpoint indicado con header Authorization: {authType} {authCode}.
/// </summary>
Task<EdcDataDownloadResponse> DownloadDataAsync(
    string dataPlaneEndpoint,
    string authType,
    string authCode,
    CancellationToken cancellationToken = default);
```

#### 6.1.2. DTOs nuevos de negociación y transferencia

Crear en `Application/Features/EcoDataNet/DTOs/`:

```csharp
// ── NEGOCIACIÓN ──

/// <summary>
/// Respuesta al iniciar una negociación de contrato.
/// </summary>
public class EdcNegotiationResponse
{
    public bool Success { get; set; }
    public string? NegotiationId { get; set; }
    public string? State { get; set; }
    public string? ErrorMessage { get; set; }
    public int HttpStatusCode { get; set; }
}

/// <summary>
/// Estado actual de una negociación de contrato.
/// </summary>
public class EdcNegotiationStateResponse
{
    public bool Success { get; set; }
    public string? NegotiationId { get; set; }
    public string? State { get; set; }
    public string? ContractAgreementId { get; set; }
    public string? ErrorDetail { get; set; }
    public string? ErrorMessage { get; set; }
    public int HttpStatusCode { get; set; }
    public string? RawJson { get; set; }
}

// ── TRANSFERENCIA ──

/// <summary>
/// Respuesta al iniciar un proceso de transferencia.
/// </summary>
public class EdcTransferResponse
{
    public bool Success { get; set; }
    public string? TransferProcessId { get; set; }
    public string? State { get; set; }
    public string? ErrorMessage { get; set; }
    public int HttpStatusCode { get; set; }
}

/// <summary>
/// Estado actual de un proceso de transferencia.
/// </summary>
public class EdcTransferStateResponse
{
    public bool Success { get; set; }
    public string? TransferProcessId { get; set; }
    public string? State { get; set; }
    public string? ErrorDetail { get; set; }
    public string? ErrorMessage { get; set; }
    public int HttpStatusCode { get; set; }
    public string? RawJson { get; set; }
}

// ── DESCARGA (EDR) ──

/// <summary>
/// Referencia del endpoint de datos (EDR) para descarga.
/// </summary>
public class EdcEndpointDataReferenceResponse
{
    public bool Success { get; set; }
    public string? Endpoint { get; set; }
    public string? AuthType { get; set; }       // p.ej. "Bearer"
    public string? AuthCode { get; set; }       // token temporal
    public string? ErrorMessage { get; set; }
    public int HttpStatusCode { get; set; }
    public string? RawJson { get; set; }
}

/// <summary>
/// Resultado de la descarga de datos del data plane.
/// </summary>
public class EdcDataDownloadResponse
{
    public bool Success { get; set; }
    public string? ContentType { get; set; }
    public string? Data { get; set; }           // contenido como string (JSON, CSV, etc.)
    public byte[]? RawData { get; set; }        // contenido binario si no es texto
    public string? ErrorMessage { get; set; }
    public int HttpStatusCode { get; set; }
}
```

#### 6.1.3. Ampliar `EdcNegotiationSelection`

Verificar si `EdcNegotiationSelection` ya contiene un campo para guardar la **offer ODRL original como JSON string**. Si NO lo tiene, añadir:

```csharp
// En EdcNegotiationSelection existente, añadir:

/// <summary>
/// JSON crudo de la offer ODRL tal como vino del catálogo.
/// Se usa para construir el payload de ContractRequest sin perder campos.
/// </summary>
public string? RawOfferJson { get; set; }

/// <summary>
/// Objeto offer completa parseada (ya debería existir vía EdcOfferDto).
/// Se mantiene por compatibilidad con la vista de detalle.
/// </summary>
public EdcOfferDto? Offer { get; set; }
```

**Y en el método `OnSelectOfferForNegotiation()` del componente Blazor, asegurar que `RawOfferJson` se rellena.** Si `EdcOfferDto` ya tiene un campo `RawOfferJson`, usarlo. Si no, hay que añadirlo al parser (`EdcCatalogParser`) para que capture el JSON crudo del nodo `odrl:hasPolicy` al parsear cada dataset.

#### 6.1.4. Command `StartContractNegotiationCommand`

Crear en `Application/Features/EcoDataNet/Commands/`:

```csharp
public record StartContractNegotiationCommand : IRequest<EdcNegotiationResponse>
{
    /// <summary>ID del usuario que actúa como consumidor.</summary>
    public int ConsumerUserId { get; init; }

    /// <summary>ID del dataset seleccionado (asset id).</summary>
    public string DatasetId { get; init; } = string.Empty;

    /// <summary>ID de la offer ODRL seleccionada.</summary>
    public string OfferId { get; init; } = string.Empty;

    /// <summary>Participant ID del proveedor (dspace:participantId).</summary>
    public string ProviderParticipantId { get; init; } = string.Empty;

    /// <summary>Protocol endpoint del proveedor (counterPartyAddress).</summary>
    public string ProviderProtocolEndpoint { get; init; } = string.Empty;

    /// <summary>
    /// JSON crudo de la offer ODRL del catálogo (para incluir en el ContractRequest).
    /// Si es null, el handler intentará reconstruirla desde los DTOs.
    /// </summary>
    public string? RawOfferJson { get; init; }

    /// <summary>
    /// Offer parseada (fallback si RawOfferJson no está disponible).
    /// </summary>
    public EdcOfferDto? Offer { get; init; }
}
```

**Handler** — Lógica:

```csharp
public class StartContractNegotiationCommandHandler
    : IRequestHandler<StartContractNegotiationCommand, EdcNegotiationResponse>
{
    // Inyectar: DbContext, ICurrentUserService, IEdcManagementClient, ILogger

    public async Task<EdcNegotiationResponse> Handle(
        StartContractNegotiationCommand request, CancellationToken ct)
    {
        // 1. SEGURIDAD: verificar que ConsumerUserId pertenece al OwnerId del usuario logueado.
        //    Si NO es ADMIN, ConsumerUserId debe coincidir con currentUser.UserId.

        // 2. OBTENER CONECTOR DEL CONSUMIDOR
        var consumerConnector = await _db.UserEDCConnectors
            .FirstOrDefaultAsync(c => c.UserId == request.ConsumerUserId
                                   && c.User.OwnerId == _currentUserService.OwnerId, ct);
        if (consumerConnector == null)
            throw new ValidationException("El consumidor no tiene conector EDC configurado.");

        var consumerMgmtUrl = $"https://mgmt.{consumerConnector.EDCServerName}/management";

        // 3. CONSTRUIR PAYLOAD ContractRequest
        var payload = BuildContractRequestPayload(request);

        _logger.LogInformation(
            "Iniciando negociación: Consumer={ConsumerServer}, Provider={ProviderEndpoint}, " +
            "Asset={AssetId}, Offer={OfferId}",
            consumerConnector.EDCServerName, request.ProviderProtocolEndpoint,
            request.DatasetId, request.OfferId);

        // 4. ENVIAR POST /v3/contractnegotiations/
        return await _edcClient.StartNegotiationAsync(consumerMgmtUrl, payload, ct);
    }

    private string BuildContractRequestPayload(StartContractNegotiationCommand request)
    {
        // OPCIÓN A (preferida): si tenemos RawOfferJson, usarlo directamente
        // OPCIÓN B (fallback): reconstruir desde EdcOfferDto

        // Estructura del ContractRequest JSON-LD:
        // {
        //   "@context": { "@vocab": "https://w3id.org/edc/v0.0.1/ns/" },
        //   "@type": "ContractRequest",
        //   "counterPartyAddress": request.ProviderProtocolEndpoint,
        //   "protocol": "dataspace-protocol-http",
        //   "policy": {
        //     "@context": "http://www.w3.org/ns/odrl.jsonld",
        //     "@id": request.OfferId,
        //     "@type": "odrl:Offer",
        //     "odrl:permission": [...],    ← de la offer original
        //     "odrl:prohibition": [...],   ← de la offer original
        //     "odrl:obligation": [...],    ← de la offer original
        //     "odrl:assigner": request.ProviderParticipantId,
        //     "odrl:target": request.DatasetId
        //   }
        // }

        // Copilot debe implementar esto usando System.Text.Json.
        // Si RawOfferJson está disponible:
        //   - Parsear como JsonDocument
        //   - Añadir/sobrescribir "assigner" con request.ProviderParticipantId
        //   - Añadir/sobrescribir "target" con request.DatasetId
        //   - Envolver en el ContractRequest
        // Si no:
        //   - Reconstruir la offer desde request.Offer (EdcOfferDto)
        //   - Serializar permissions/prohibitions/obligations

        throw new NotImplementedException("Copilot debe implementar BuildContractRequestPayload");
    }
}
```

#### 6.1.5. Query `GetNegotiationStateQuery`

```csharp
public record GetNegotiationStateQuery : IRequest<EdcNegotiationStateResponse>
{
    public int ConsumerUserId { get; init; }
    public string NegotiationId { get; init; } = string.Empty;
}
```

**Handler**: obtener conector del consumidor → construir URL → llamar `GetNegotiationStateAsync`. Logging de cada transición de estado.

**IMPORTANTE**: La respuesta de `GET /v3/contractnegotiations/{id}` incluye el campo `contractAgreementId` cuando el estado es `FINALIZED`. Copilot debe:
- Extraer `contractAgreementId` del JSON de respuesta (buscar el campo exacto: puede ser `contractAgreementId` o `edc:contractAgreementId` o `https://w3id.org/edc/v0.0.1/ns/contractAgreementId`).
- Incluirlo en `EdcNegotiationStateResponse.ContractAgreementId`.

#### 6.1.6. Command `StartTransferProcessCommand`

```csharp
public record StartTransferProcessCommand : IRequest<EdcTransferResponse>
{
    public int ConsumerUserId { get; init; }
    public string ContractAgreementId { get; init; } = string.Empty;
    public string AssetId { get; init; } = string.Empty;
    public string ProviderProtocolEndpoint { get; init; } = string.Empty;
    public string TransferType { get; init; } = "HttpData-PULL";
}
```

**Handler**: construir URL → construir payload → llamar `StartTransferAsync`.

Payload del TransferRequest:
```json
{
  "@context": {
    "@vocab": "https://w3id.org/edc/v0.0.1/ns/"
  },
  "@type": "TransferRequestDto",
  "counterPartyAddress": "{{request.ProviderProtocolEndpoint}}",
  "contractId": "{{request.ContractAgreementId}}",
  "assetId": "{{request.AssetId}}",
  "protocol": "dataspace-protocol-http",
  "transferType": "{{request.TransferType}}"
}
```

#### 6.1.7. Query `GetTransferStateQuery`

```csharp
public record GetTransferStateQuery : IRequest<EdcTransferStateResponse>
{
    public int ConsumerUserId { get; init; }
    public string TransferProcessId { get; init; } = string.Empty;
}
```

**Handler**: análogo a `GetNegotiationStateQuery`.

#### 6.1.8. Query `GetEndpointDataReferenceQuery`

```csharp
public record GetEndpointDataReferenceQuery : IRequest<EdcEndpointDataReferenceResponse>
{
    public int ConsumerUserId { get; init; }
    public string TransferProcessId { get; init; } = string.Empty;
}
```

**Handler**: obtener conector → `GET /v3/edrs/{transferProcessId}/dataaddress` → extraer `endpoint`, `authorization` (o `authType` + `authCode`), `type`.

> **INVESTIGACIÓN**: Copilot debe parsear la respuesta del EDR. Los campos pueden variar según la versión de EDC:
> - Buscar `endpoint` o `https://w3id.org/edc/v0.0.1/ns/endpoint`
> - Buscar `authorization` o `https://w3id.org/edc/v0.0.1/ns/authorization`
> - Buscar `authType` (puede ser `"Bearer"` o `"header"`)
> Si el campo `authorization` contiene un valor como `"Bearer eyJ..."`, separar en authType=`Bearer` y authCode=`eyJ...`.
> Si el campo contiene solo el token sin prefijo, asumir authType=`Bearer`.

#### 6.1.9. Command `DownloadTransferDataCommand`

```csharp
public record DownloadTransferDataCommand : IRequest<EdcDataDownloadResponse>
{
    public string DataPlaneEndpoint { get; init; } = string.Empty;
    public string AuthType { get; init; } = "Bearer";
    public string AuthCode { get; init; } = string.Empty;
}
```

**Handler**: llamar `DownloadDataAsync`. No necesita conector ni seguridad multi-tenant adicional porque el endpoint ya está autorizado por el token EDR temporal.

#### 6.1.10. Validators (FluentValidation)

Crear validators para cada Command/Query siguiendo el patrón existente. Ejemplo:

```csharp
public class StartContractNegotiationCommandValidator
    : AbstractValidator<StartContractNegotiationCommand>
{
    public StartContractNegotiationCommandValidator()
    {
        RuleFor(x => x.ConsumerUserId).GreaterThan(0);
        RuleFor(x => x.DatasetId).NotEmpty();
        RuleFor(x => x.OfferId).NotEmpty();
        RuleFor(x => x.ProviderParticipantId).NotEmpty();
        RuleFor(x => x.ProviderProtocolEndpoint).NotEmpty()
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("ProviderProtocolEndpoint debe ser una URL válida.");
    }
}
```

---

### CAPA 2 — Infrastructure (`GreenTransit.Infrastructure`)

#### 6.2.1. Ampliar `EdcManagementClient`

Buscar la clase `EdcManagementClient` existente y añadir la implementación de los nuevos métodos.

Patrón a seguir (el mismo que `RequestCatalogAsync` ya usa):

```csharp
public async Task<EdcNegotiationResponse> StartNegotiationAsync(
    string consumerManagementBaseUrl,
    string contractRequestPayload,
    CancellationToken ct = default)
{
    var url = $"{consumerManagementBaseUrl}/v3/contractnegotiations";
    _logger.LogInformation("POST {Url} — Iniciando negociación de contrato", url);

    try
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(contractRequestPayload, Encoding.UTF8, "application/json")
        };

        // Añadir API Key si está configurada (copiar patrón de RequestCatalogAsync)
        AddApiKeyHeader(request);

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Negociación fallida: {StatusCode} — {Body}",
                (int)response.StatusCode, body);
            return new EdcNegotiationResponse
            {
                Success = false,
                HttpStatusCode = (int)response.StatusCode,
                ErrorMessage = $"HTTP {(int)response.StatusCode}: {body}"
            };
        }

        // Parsear respuesta: extraer @id y state
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        return new EdcNegotiationResponse
        {
            Success = true,
            NegotiationId = GetJsonLdString(root, "@id"),
            State = GetJsonLdString(root, "state",
                "https://w3id.org/edc/v0.0.1/ns/state"),
            HttpStatusCode = (int)response.StatusCode
        };
    }
    catch (TaskCanceledException)
    {
        return new EdcNegotiationResponse { Success = false, ErrorMessage = "Timeout" };
    }
    catch (HttpRequestException ex)
    {
        _logger.LogError(ex, "Error HTTP al iniciar negociación en {Url}", url);
        return new EdcNegotiationResponse { Success = false, ErrorMessage = ex.Message };
    }
}
```

Implementar de forma análoga:
- `GetNegotiationStateAsync` → `GET {baseUrl}/v3/contractnegotiations/{id}` → parsear `state`, `contractAgreementId`, `errorDetail`
- `StartTransferAsync` → `POST {baseUrl}/v3/transferprocesses` → parsear `@id`, `state`
- `GetTransferStateAsync` → `GET {baseUrl}/v3/transferprocesses/{id}` → parsear `state`, `errorDetail`
- `GetEndpointDataReferenceAsync` → `GET {baseUrl}/v3/edrs/{id}/dataaddress` → parsear `endpoint`, `authorization`
- `DownloadDataAsync` → `GET {endpoint}` con `Authorization: {authType} {authCode}` → devolver body como string/bytes

**Método helper** `GetJsonLdString(JsonElement element, params string[] propertyNames)`:
- Copilot debe verificar si ya existe un helper similar en `EdcManagementClient` o en `EdcCatalogParser`.
- Debe buscar la propiedad por cualquiera de los nombres (con y sin prefijo namespace), análogo al patrón de `GetStringProperty` del parser.

---

### CAPA 3 — Web (`GreenTransit.Web`)

#### 6.3.1. Componente Stepper reutilizable

Crear un componente `EdcProcessStepper.razor` en `Web/Components/Pages/EcoDataNet/` (o subcarpeta `Components/`):

```razor
@* Stepper visual para procesos EDC (negociación / transferencia) *@

<div class="edc-stepper d-flex align-items-center gap-2 my-3">
    @for (int i = 0; i < Steps.Count; i++)
    {
        var step = Steps[i];
        var isActive = i == CurrentStepIndex;
        var isCompleted = i < CurrentStepIndex;
        var isError = IsError && i == CurrentStepIndex;

        <div class="edc-step text-center @(isActive ? "active" : "") @(isCompleted ? "completed" : "") @(isError ? "error" : "")">
            <div class="step-circle @(isCompleted ? "bg-success text-white" : isError ? "bg-danger text-white" : isActive ? "bg-primary text-white" : "bg-light text-muted")">
                @if (isCompleted)
                {
                    <i class="bi bi-check"></i>
                }
                else if (isError)
                {
                    <i class="bi bi-x"></i>
                }
                else if (isActive)
                {
                    <span class="spinner-border spinner-border-sm"></span>
                }
                else
                {
                    <span>@(i + 1)</span>
                }
            </div>
            <div class="step-label small mt-1 @(isActive ? "fw-bold" : "text-muted")">
                @step
            </div>
        </div>

        @if (i < Steps.Count - 1)
        {
            <div class="step-connector flex-grow-1" style="height: 2px; background: @(i < CurrentStepIndex ? "var(--bs-success)" : "var(--bs-gray-300)")"></div>
        }
    }
</div>

@code {
    [Parameter] public List<string> Steps { get; set; } = new();
    [Parameter] public int CurrentStepIndex { get; set; }
    [Parameter] public bool IsError { get; set; }
}
```

Con CSS scoped:

```css
.edc-stepper .step-circle {
    width: 32px;
    height: 32px;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 0.8rem;
}
```

#### 6.3.2. Modificar el componente ConsumeData.razor

Buscar el componente con `@page "/ecodatanet/consume-data"` y aplicar estos cambios:

**A) Variables de estado nuevas:**

```csharp
// ── Estado del proceso de negociación + transferencia ──
private bool _isNegotiating = false;
private bool _isTransferring = false;
private bool _isDownloading = false;

private string? _negotiationId;
private string? _negotiationState;
private int _negotiationStepIndex = 0;
private bool _negotiationError = false;
private string? _negotiationErrorMessage;

private string? _contractAgreementId;

private string? _transferProcessId;
private string? _transferState;
private int _transferStepIndex = 0;
private bool _transferError = false;
private string? _transferErrorMessage;

private EdcEndpointDataReferenceResponse? _edrResponse;
private EdcDataDownloadResponse? _downloadResponse;

private CancellationTokenSource? _pollingCts;

// Steps para los steppers
private readonly List<string> _negotiationSteps = new()
{
    "Solicitada", "Oferta recibida", "Aceptada", "Acordada", "Verificada", "Finalizada"
};

private readonly List<string> _transferSteps = new()
{
    "Iniciada", "Solicitada", "En curso", "Completada"
};
```

**B) Reemplazar el placeholder del botón "Iniciar negociación":**

Donde actualmente el método `OnSelectOfferForNegotiation()` solo guarda estado y muestra Toast, reemplazar por un botón que EJECUTE la negociación real:

```razor
@* ── PROCESO DE NEGOCIACIÓN Y TRANSFERENCIA ── *@
@if (_negotiationSelection != null)
{
    <div class="card mt-3 border-primary">
        <div class="card-header bg-primary text-white d-flex justify-content-between align-items-center">
            <span><i class="bi bi-handshake me-2"></i>Negociación y Transferencia</span>
            @if (!_isNegotiating && !_isTransferring && _downloadResponse == null)
            {
                <button class="btn btn-sm btn-outline-light" @onclick="ClearNegotiationSelection">
                    <i class="bi bi-x"></i> Cancelar
                </button>
            }
        </div>
        <div class="card-body">
            @* Info de la oferta seleccionada *@
            <div class="mb-3">
                <strong>Dataset:</strong> @_negotiationSelection.DatasetName<br />
                <strong>Proveedor:</strong> @_negotiationSelection.ProviderUserName<br />
                <strong>Offer ID:</strong> <code>@_negotiationSelection.SelectedOfferId</code>
            </div>

            @* Botón iniciar negociación (solo si no ha empezado) *@
            @if (!_isNegotiating && !_isTransferring && _negotiationId == null && _downloadResponse == null)
            {
                <button class="btn btn-primary" @onclick="StartNegotiation">
                    <i class="bi bi-play-fill me-2"></i>Iniciar negociación
                </button>
            }

            @* Stepper de negociación *@
            @if (_negotiationId != null)
            {
                <h6 class="mt-3">Negociación de contrato</h6>
                <EdcProcessStepper Steps="_negotiationSteps"
                                   CurrentStepIndex="_negotiationStepIndex"
                                   IsError="_negotiationError" />
                @if (_negotiationError && !string.IsNullOrEmpty(_negotiationErrorMessage))
                {
                    <div class="alert alert-danger mt-2">
                        <i class="bi bi-exclamation-triangle me-2"></i>@_negotiationErrorMessage
                        <button class="btn btn-outline-danger btn-sm ms-3" @onclick="RetryNegotiation">
                            <i class="bi bi-arrow-clockwise me-1"></i>Reintentar
                        </button>
                    </div>
                }
            }

            @* Stepper de transferencia *@
            @if (_transferProcessId != null)
            {
                <h6 class="mt-3">Transferencia de datos</h6>
                <EdcProcessStepper Steps="_transferSteps"
                                   CurrentStepIndex="_transferStepIndex"
                                   IsError="_transferError" />
                @if (_transferError && !string.IsNullOrEmpty(_transferErrorMessage))
                {
                    <div class="alert alert-danger mt-2">
                        <i class="bi bi-exclamation-triangle me-2"></i>@_transferErrorMessage
                        <button class="btn btn-outline-danger btn-sm ms-3" @onclick="RetryTransfer">
                            <i class="bi bi-arrow-clockwise me-1"></i>Reintentar
                        </button>
                    </div>
                }
            }

            @* Botón Download *@
            @if (_edrResponse != null && !_isDownloading && _downloadResponse == null)
            {
                <div class="mt-3">
                    <button class="btn btn-success btn-lg" @onclick="DownloadData">
                        <i class="bi bi-download me-2"></i>Descargar datos
                    </button>
                </div>
            }

            @if (_isDownloading)
            {
                <div class="mt-3">
                    <span class="spinner-border spinner-border-sm me-2"></span>Descargando datos...
                </div>
            }

            @* Resultado de descarga *@
            @if (_downloadResponse != null)
            {
                <div class="mt-3">
                    @if (_downloadResponse.Success)
                    {
                        <div class="alert alert-success">
                            <i class="bi bi-check-circle-fill me-2"></i>Datos descargados correctamente.
                            <span class="badge bg-info ms-2">@_downloadResponse.ContentType</span>
                        </div>
                        <div class="mb-2">
                            <button class="btn btn-outline-primary btn-sm" @onclick="ExportDownloadToFile">
                                <i class="bi bi-file-earmark-arrow-down me-1"></i>Exportar a fichero
                            </button>
                        </div>
                        <details>
                            <summary class="text-muted small">Ver datos descargados</summary>
                            <pre class="bg-light p-3 mt-2 border rounded" style="max-height: 400px; overflow: auto;">@_downloadResponse.Data</pre>
                        </details>
                    }
                    else
                    {
                        <div class="alert alert-danger">
                            <i class="bi bi-exclamation-triangle me-2"></i>@_downloadResponse.ErrorMessage
                        </div>
                    }
                }
            </div>
        }
    </div>
}
```

**C) Métodos de lógica:**

```csharp
private async Task StartNegotiation()
{
    if (_negotiationSelection == null) return;

    _isNegotiating = true;
    _negotiationError = false;
    _negotiationErrorMessage = null;
    _negotiationStepIndex = 0;
    StateHasChanged();

    try
    {
        // 1. Iniciar negociación
        var response = await Mediator.Send(new StartContractNegotiationCommand
        {
            ConsumerUserId = /* obtener del flujo actual: si es ADMIN con selector, el userId seleccionado;
                               si no, el userId logueado. Copilot debe buscar cómo se obtiene
                               ConsumerUserId en RequestEdcCatalogCommand y replicar el patrón */,
            DatasetId = _negotiationSelection.SelectedDatasetId,
            OfferId = _negotiationSelection.SelectedOfferId,
            ProviderParticipantId = _negotiationSelection.ProviderParticipantId,
            ProviderProtocolEndpoint = _negotiationSelection.ProviderProtocolEndpoint,
            RawOfferJson = _negotiationSelection.RawOfferJson,
            Offer = _negotiationSelection.Offer
        });

        if (!response.Success)
        {
            _negotiationError = true;
            _negotiationErrorMessage = response.ErrorMessage;
            _isNegotiating = false;
            StateHasChanged();
            return;
        }

        _negotiationId = response.NegotiationId;
        StateHasChanged();

        // 2. Polling de estado
        await PollNegotiationState();
    }
    catch (Exception ex)
    {
        _negotiationError = true;
        _negotiationErrorMessage = ex.Message;
        _isNegotiating = false;
        StateHasChanged();
    }
}

private async Task PollNegotiationState()
{
    _pollingCts?.Cancel();
    _pollingCts = new CancellationTokenSource();
    var ct = _pollingCts.Token;

    var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
    var maxAttempts = 120; // 6 minutos máximo
    var attempt = 0;

    try
    {
        while (await timer.WaitForNextTickAsync(ct) && attempt++ < maxAttempts)
        {
            var state = await Mediator.Send(new GetNegotiationStateQuery
            {
                ConsumerUserId = /* mismo patrón */,
                NegotiationId = _negotiationId!
            }, ct);

            if (!state.Success)
            {
                _negotiationError = true;
                _negotiationErrorMessage = state.ErrorMessage;
                _isNegotiating = false;
                StateHasChanged();
                return;
            }

            _negotiationState = state.State;
            _negotiationStepIndex = MapNegotiationStateToStep(state.State);
            StateHasChanged();

            // ¿Finalizada?
            if (state.State is "FINALIZED")
            {
                _contractAgreementId = state.ContractAgreementId;
                _isNegotiating = false;
                StateHasChanged();

                // Iniciar transferencia automáticamente
                await StartTransferProcess();
                return;
            }

            // ¿Error / terminada?
            if (state.State is "TERMINATED" or "TERMINATING")
            {
                _negotiationError = true;
                _negotiationErrorMessage = state.ErrorDetail ?? $"Negociación terminada: {state.State}";
                _isNegotiating = false;
                StateHasChanged();
                return;
            }
        }

        // Timeout
        if (attempt >= maxAttempts)
        {
            _negotiationError = true;
            _negotiationErrorMessage = "Timeout: la negociación no se completó en el tiempo esperado.";
            _isNegotiating = false;
            StateHasChanged();
        }
    }
    catch (OperationCanceledException)
    {
        // Cancelado por el usuario o al abandonar la página
    }
}

private async Task StartTransferProcess()
{
    if (_contractAgreementId == null || _negotiationSelection == null) return;

    _isTransferring = true;
    _transferError = false;
    _transferErrorMessage = null;
    _transferStepIndex = 0;
    StateHasChanged();

    try
    {
        var response = await Mediator.Send(new StartTransferProcessCommand
        {
            ConsumerUserId = /* mismo patrón */,
            ContractAgreementId = _contractAgreementId,
            AssetId = _negotiationSelection.SelectedDatasetId,
            ProviderProtocolEndpoint = _negotiationSelection.ProviderProtocolEndpoint,
            TransferType = "HttpData-PULL"
        });

        if (!response.Success)
        {
            _transferError = true;
            _transferErrorMessage = response.ErrorMessage;
            _isTransferring = false;
            StateHasChanged();
            return;
        }

        _transferProcessId = response.TransferProcessId;
        StateHasChanged();

        await PollTransferState();
    }
    catch (Exception ex)
    {
        _transferError = true;
        _transferErrorMessage = ex.Message;
        _isTransferring = false;
        StateHasChanged();
    }
}

private async Task PollTransferState()
{
    _pollingCts?.Cancel();
    _pollingCts = new CancellationTokenSource();
    var ct = _pollingCts.Token;

    var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
    var maxAttempts = 60; // 3 minutos máximo
    var attempt = 0;

    try
    {
        while (await timer.WaitForNextTickAsync(ct) && attempt++ < maxAttempts)
        {
            var state = await Mediator.Send(new GetTransferStateQuery
            {
                ConsumerUserId = /* mismo patrón */,
                TransferProcessId = _transferProcessId!
            }, ct);

            if (!state.Success)
            {
                _transferError = true;
                _transferErrorMessage = state.ErrorMessage;
                _isTransferring = false;
                StateHasChanged();
                return;
            }

            _transferState = state.State;
            _transferStepIndex = MapTransferStateToStep(state.State);
            StateHasChanged();

            // ¿Lista para descarga? (STARTED para HttpData-PULL, o COMPLETED)
            if (state.State is "STARTED" or "COMPLETED")
            {
                _isTransferring = false;
                StateHasChanged();

                // Obtener EDR
                await ObtainEndpointDataReference();
                return;
            }

            // ¿Error?
            if (state.State is "TERMINATED" or "TERMINATING")
            {
                _transferError = true;
                _transferErrorMessage = state.ErrorDetail ?? $"Transferencia terminada: {state.State}";
                _isTransferring = false;
                StateHasChanged();
                return;
            }
        }

        if (attempt >= maxAttempts)
        {
            _transferError = true;
            _transferErrorMessage = "Timeout: la transferencia no se completó en el tiempo esperado.";
            _isTransferring = false;
            StateHasChanged();
        }
    }
    catch (OperationCanceledException) { }
}

private async Task ObtainEndpointDataReference()
{
    try
    {
        _edrResponse = await Mediator.Send(new GetEndpointDataReferenceQuery
        {
            ConsumerUserId = /* mismo patrón */,
            TransferProcessId = _transferProcessId!
        });

        if (!_edrResponse.Success)
        {
            _transferError = true;
            _transferErrorMessage = $"No se pudo obtener referencia de descarga: {_edrResponse.ErrorMessage}";
        }

        StateHasChanged();
    }
    catch (Exception ex)
    {
        _transferError = true;
        _transferErrorMessage = $"Error obteniendo referencia de descarga: {ex.Message}";
        StateHasChanged();
    }
}

private async Task DownloadData()
{
    if (_edrResponse == null) return;

    _isDownloading = true;
    StateHasChanged();

    try
    {
        _downloadResponse = await Mediator.Send(new DownloadTransferDataCommand
        {
            DataPlaneEndpoint = _edrResponse.Endpoint!,
            AuthType = _edrResponse.AuthType ?? "Bearer",
            AuthCode = _edrResponse.AuthCode!
        });
    }
    catch (Exception ex)
    {
        _downloadResponse = new EdcDataDownloadResponse
        {
            Success = false,
            ErrorMessage = ex.Message
        };
    }
    finally
    {
        _isDownloading = false;
        StateHasChanged();
    }
}

private async Task ExportDownloadToFile()
{
    if (_downloadResponse?.Data == null) return;

    // Copilot: usar JSRuntime para descargar fichero al navegador.
    // Buscar si el proyecto ya tiene un helper de descarga JS (saveAs, FileSaver, etc.).
    // Si no, implementar un helper mínimo:
    // await JSRuntime.InvokeVoidAsync("saveAsFile",
    //     $"ecodatanet-{_negotiationSelection?.DatasetName ?? "data"}.json",
    //     _downloadResponse.Data);
    //
    // Con función JS:
    // window.saveAsFile = (filename, content) => {
    //     const blob = new Blob([content], { type: "application/json" });
    //     const url = URL.createObjectURL(blob);
    //     const a = document.createElement("a"); a.href = url; a.download = filename; a.click();
    //     URL.revokeObjectURL(url);
    // };
}

private void RetryNegotiation()
{
    _negotiationId = null;
    _negotiationError = false;
    _negotiationErrorMessage = null;
    _negotiationStepIndex = 0;
    _contractAgreementId = null;
    _transferProcessId = null;
    _transferError = false;
    _edrResponse = null;
    _downloadResponse = null;
    StateHasChanged();
    _ = StartNegotiation();
}

private void RetryTransfer()
{
    _transferProcessId = null;
    _transferError = false;
    _transferErrorMessage = null;
    _transferStepIndex = 0;
    _edrResponse = null;
    _downloadResponse = null;
    StateHasChanged();
    _ = StartTransferProcess();
}

/// <summary>
/// Mapea estado EDC de negociación al índice del stepper.
/// </summary>
private int MapNegotiationStateToStep(string? state) => state switch
{
    "INITIAL" or "REQUESTING" or "REQUESTED" => 0,
    "OFFERED" => 1,
    "ACCEPTING" or "ACCEPTED" => 2,
    "AGREEING" or "AGREED" => 3,
    "VERIFYING" or "VERIFIED" => 4,
    "FINALIZING" or "FINALIZED" => 5,
    _ => 0
};

/// <summary>
/// Mapea estado EDC de transferencia al índice del stepper.
/// </summary>
private int MapTransferStateToStep(string? state) => state switch
{
    "INITIAL" or "PROVISIONING" or "PROVISIONED" => 0,
    "REQUESTING" or "REQUESTED" => 1,
    "STARTING" or "STARTED" => 2,
    "COMPLETING" or "COMPLETED" => 3,
    _ => 0
};
```

**D) Limpieza al salir (IDisposable):**

El componente debe implementar `IDisposable` (si no lo hace ya) para cancelar el polling:

```csharp
public void Dispose()
{
    _pollingCts?.Cancel();
    _pollingCts?.Dispose();
}
```

---

### CAPA 4 — Configuración (`appsettings.json`)

Verificar que la sección `EcoDataNet:Edc` ya existe (del prompt anterior). Si no, añadir. Añadir campo nuevo para polling:

```json
{
  "EcoDataNet": {
    "Edc": {
      "MaxConcurrentRequests": 5,
      "RequestTimeoutSeconds": 30,
      "ManagementApiKey": "",
      "NegotiationPollingIntervalSeconds": 3,
      "TransferPollingIntervalSeconds": 3,
      "NegotiationPollingMaxAttempts": 120,
      "TransferPollingMaxAttempts": 60
    }
  }
}
```

Ampliar `EdcOptions` (si existe) con los nuevos campos. Si no existe como clase tipada, crearla.

---

## 🌐 7. Endpoints EDC v3 y payloads (resumen)

| Operación | Método | URL (relativa a Management API consumidor) | Payload |
|---|---|---|---|
| Iniciar negociación | `POST` | `/v3/contractnegotiations` | `ContractRequest` (ver §6.1.4) |
| Estado de negociación | `GET` | `/v3/contractnegotiations/{negotiationId}` | — |
| Iniciar transferencia | `POST` | `/v3/transferprocesses` | `TransferRequestDto` (ver §6.1.6) |
| Estado de transferencia | `GET` | `/v3/transferprocesses/{transferProcessId}` | — |
| Obtener EDR | `GET` | `/v3/edrs/{transferProcessId}/dataaddress` | — |
| Descargar datos | `GET` | `{endpoint del EDR}` | — (con header `Authorization`) |

### Construcción de URLs

Todas las llamadas a Management API se construyen como:
```
https://mgmt.{consumerEDCServerName}/management/v3/...
```

Donde `consumerEDCServerName` proviene de `UserEDCConnector.EDCServerName` del usuario consumidor.

El `counterPartyAddress` en los payloads apunta al protocol del proveedor:
```
https://proto.{providerEDCServerName}/protocol
```

Donde `providerEDCServerName` se obtiene del `ProviderProtocolEndpoint` ya almacenado en `EdcNegotiationSelection`.

### Payload ContractRequest

```json
{
  "@context": {
    "@vocab": "https://w3id.org/edc/v0.0.1/ns/"
  },
  "@type": "ContractRequest",
  "counterPartyAddress": "https://proto.{providerServer}/protocol",
  "protocol": "dataspace-protocol-http",
  "policy": {
    "@context": "http://www.w3.org/ns/odrl.jsonld",
    "@id": "{{offerId}}",
    "@type": "odrl:Offer",
    "odrl:permission": [ ... ],
    "odrl:prohibition": [ ... ],
    "odrl:obligation": [ ... ],
    "odrl:assigner": "{{providerParticipantId}}",
    "odrl:target": "{{datasetId/assetId}}"
  }
}
```

### Payload TransferRequestDto

```json
{
  "@context": {
    "@vocab": "https://w3id.org/edc/v0.0.1/ns/"
  },
  "@type": "TransferRequestDto",
  "counterPartyAddress": "https://proto.{providerServer}/protocol",
  "contractId": "{{contractAgreementId}}",
  "assetId": "{{datasetId}}",
  "protocol": "dataspace-protocol-http",
  "transferType": "HttpData-PULL"
}
```

---

## 🧪 8. Plan de pruebas manual

### Test 1 — Negociación exitosa end-to-end

1. Configurar en BD: usuario consumidor con `UserEDCConnector` apuntando a conector EDC real.
2. Ejecutar "Consumir catálogo" → seleccionar un dataset con oferta → pulsar "Iniciar negociación".
3. **Verificar**: stepper avanza por los estados. Al llegar a "Finalizada", automáticamente inicia la transferencia.

### Test 2 — Transferencia completa y descarga

1. Continuación del Test 1: tras negociación exitosa, verificar que el stepper de transferencia avanza.
2. Al llegar a "Completada" / "En curso" (STARTED), verificar que aparece el botón "Descargar datos".
3. Pulsar "Descargar datos".
4. **Verificar**: se muestra el contenido descargado y el botón "Exportar a fichero" funciona.

### Test 3 — Consumidor sin conector EDC

1. Login como usuario que NO tiene `UserEDCConnector`.
2. Intentar pulsar "Iniciar negociación".
3. **Verificar**: error claro indicando que debe configurar su conector. No se envía ninguna request HTTP.

### Test 4 — Negociación rechazada/terminada

1. Configurar un escenario donde la negociación sea rechazada por el proveedor (policy que no coincide, o proveedor que no acepta).
2. **Verificar**: el stepper muestra estado ERROR con mensaje descriptivo. Aparece botón "Reintentar".

### Test 5 — Transferencia fallida

1. Forzar un error de transferencia (servidor proveedor caído, o contractId inválido).
2. **Verificar**: stepper de transferencia muestra ERROR con detalle. Botón "Reintentar" disponible.

### Test 6 — Timeout de negociación

1. Configurar un conector proveedor que responda lentamente o que no avance de estado.
2. Esperar al timeout configurado.
3. **Verificar**: tras agotar intentos de polling, se muestra "Timeout" con opción de reintentar.

### Test 7 — Multi-tenant: aislamiento de conectores

1. Crear dos tenants (OwnerId A y B) con usuarios y conectores.
2. Login como usuario del Tenant A.
3. **Verificar**: solo se puede negociar usando el conector del Tenant A. El handler rechaza si se intenta usar un ConsumerUserId de otro tenant.

### Test 8 — Cancelar proceso y volver atrás

1. Iniciar una negociación.
2. Mientras el stepper está en polling, pulsar "Cancelar" (si está visible) o navegar fuera de la página.
3. **Verificar**: el polling se detiene limpiamente (CancellationToken), no hay excepciones en consola, no hay llamadas HTTP huérfanas.

### Test 9 — Reintentar negociación después de error

1. Provocar un error en la negociación (ej: proveedor caído).
2. Pulsar "Reintentar".
3. **Verificar**: se resetean los estados y se reinicia la negociación desde cero.

### Test 10 — EDR con token expirado

1. Completar la negociación y transferencia.
2. Esperar a que el token EDR expire (si es posible en el entorno de pruebas).
3. Pulsar "Descargar datos".
4. **Verificar**: error claro indicando que el token ha expirado. Sugerencia de re-solicitar la transferencia.

---

## ✅ 9. Checklist de aceptación

- [ ] **CA-1**: En `/ecodatanet/consume-data`, con un asset y oferta seleccionados del catálogo, el usuario puede pulsar "Iniciar negociación" y se ejecuta `POST /v3/contractnegotiations/` contra la Management API del conector consumidor.
- [ ] **CA-2**: El payload `ContractRequest` incluye: `counterPartyAddress` (protocol API del proveedor), `protocol: dataspace-protocol-http`, y `policy` con la offer ODRL original (`offerId`, `permissions`, `prohibitions`, `obligations`, `assigner`, `target`).
- [ ] **CA-3**: El `counterPartyAddress` se construye como `https://proto.{providerEDCServerName}/protocol`.
- [ ] **CA-4**: La Management API se invoca en `https://mgmt.{consumerEDCServerName}/management/v3/contractnegotiations`.
- [ ] **CA-5**: La UI muestra un stepper de negociación que refleja los estados EDC v3 (Initial → ... → Finalized) y se actualiza en tiempo real por polling.
- [ ] **CA-6**: Al alcanzar `FINALIZED`, se extrae el `contractAgreementId` de la respuesta y se lanza automáticamente `POST /v3/transferprocesses`.
- [ ] **CA-7**: El payload `TransferRequestDto` incluye `contractId` (del acuerdo), `assetId`, `counterPartyAddress`, `protocol` y `transferType: HttpData-PULL`.
- [ ] **CA-8**: La UI muestra un stepper de transferencia que refleja los estados EDC v3 y se actualiza por polling.
- [ ] **CA-9**: Al alcanzar `STARTED` o `COMPLETED`, se consulta el EDR (`GET /v3/edrs/{id}/dataaddress`) y se habilita el botón "Descargar datos".
- [ ] **CA-10**: Al pulsar "Descargar datos", se descarga el contenido desde el endpoint del data plane usando el token del EDR y se muestra al usuario.
- [ ] **CA-11**: Errores en negociación, transferencia o descarga se muestran con mensaje descriptivo y permiten reintentar.
- [ ] **CA-12**: El polling se cancela limpiamente al salir de la página (`IDisposable` + `CancellationTokenSource`).
- [ ] **CA-13**: Multi-tenant: solo se usan conectores del tenant del usuario autenticado (filtro por `OwnerId`).
- [ ] **CA-14**: Toda la lógica HTTP vive en `Infrastructure/Services/EdcManagementClient.cs`, no en componentes Razor.
- [ ] **CA-15**: Toda la orquestación pasa por MediatR (Commands/Queries), no por llamadas directas desde Razor.
- [ ] **CA-16**: Se registra log (Serilog) para cada inicio de negociación, cambio de estado, inicio de transferencia, obtención de EDR y descarga.
- [ ] **CA-17**: La API Key se envía como header `X-Api-Key` solo si está configurada (patrón existente).
- [ ] **CA-18**: El proyecto compila sin errores y sin warnings nuevos.
- [ ] **CA-19**: Si `EdcOfferDto` no tenía `RawOfferJson`, se ha añadido el campo y se puebla en `EdcCatalogParser`.
- [ ] **CA-20**: Los validators FluentValidation están creados para todos los Commands/Queries nuevos.

---

## 📝 10. Notas para Copilot

1. **Antes de escribir código**, ejecutar las búsquedas indicadas en §3 y §4 para confirmar nombres de clases, interfaces, namespaces y patrones reales del repo.
2. **Seguir el mismo patrón** de `RequestCatalogAsync` en `EdcManagementClient` para los métodos nuevos (headers, error handling, logging, API key).
3. **No modificar** los flujos existentes de "Consumir catálogo" ni de "Ver oferta" — solo extenderlos.
4. **La offer ODRL debe enviarse tal como vino del catálogo**. No inventar campos ni valores. Si `RawOfferJson` no existe, la primera tarea es añadirlo al parser.
5. **Los estados EDC v3 incluyen estados transitorios** (`REQUESTING`, `ACCEPTING`, `AGREEING`, etc.) que son momentáneos. El stepper los agrupa con su padre para no confundir al usuario.
6. **El endpoint EDR puede variar** según la versión de EDC. Implementar con `/v3/edrs/{id}/dataaddress` como primario y documentar la alternativa `/v3/transferprocesses/{id}/dataaddress` en un comentario.
7. **No persistir secretos** (tokens EDR, API keys) en BD. Los tokens EDR son temporales y se usan en memoria.
8. **Registrar las páginas nuevas** (si se crea algún `.razor` con `@page`) siguiendo `instrucciones_adicionales.md` (PageDefinitions, InferModuleName, etc.).
9. **Mantener el código compilando** en cada paso. Si un paso intermedio rompe la compilación, añadir `throw new NotImplementedException()` como placeholder temporal y continuar.
