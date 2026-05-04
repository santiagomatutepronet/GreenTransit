
# 🛰️ Proyecto Eco‑Waste‑Management: Waste Operations Coordinator

[![Platform: .NET 10](https://img.shields.io/badge/.NET-10.0-blueviolet)](https://dotnet.microsoft.com/)
[![Framework: Blazor](https://img.shields.io/badge/UI-Blazor-blue)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![AI: GitHub Copilot Pro](https://img.shields.io/badge/AI-Copilot_Pro-brightgreen)](https://github.com/features/copilot)


---

## 🎯 Propósito del Proyecto

Aplicación web para la **gestión operativa, trazabilidad y control de residuos** en un entorno multi-actor:

- Productores
- Transportistas
- Gestores
- Centros de Acopio (CAC)
- Plantas de tratamiento
- Administraciones

El sistema permite coordinar operaciones reales, asegurar cumplimiento normativo y mantener trazabilidad completa desde origen hasta tratamiento.


---

## 🧭 Alcance funcional

El sistema es un **portal transaccional multiempresa (multi-tenant)** orientado a:

- Registro de operaciones de residuos
- Control logístico y operativo
- Validación normativa
- Trazabilidad auditable
- Base para cálculo económico y ambiental

No es un sistema BI ni de optimización avanzada en esta fase.

---

## 🛠️ Stack tecnológico

| Componente | Tecnología |
|---|---|
| Runtime | .NET 10 |
| UI | Blazor Web App |
| ORM | EF Core |
| DB | SQL Server |
| Auth | OpenID Connect |
| Mediación | MediatR |
| Validación | FluentValidation |
| Logging | Serilog |
| Testing | xUnit |

---

## 🔐 Autenticación

Proveedor externo OpenID Connect:

- Authority: https://pronet-identity-wst-app.azurewebsites.net/
- Flujo: Authorization Code

### Comportamiento

- No se almacenan credenciales
- Se recibe ID Token + Access Token

### Claims necesarios

- `sub` → identificador usuario
- `email` o `preferred_username`
- claim organizativo → `OwnerId`

### Uso interno

- `IdUser` ← mapeo de `sub`
- `OwnerId` ← claim organizativo

---

## 🔐 Seguridad y multi-tenant

- Aislamiento por `OwnerId`
- Sin acceso entre tenants
- Autenticación: OpenID Connect (proveedor externo)
- Autorización: sistema basado en perfiles (ver sección siguiente)

---

## 🛡️ Autorización por perfiles

Sistema de autorización en dos capas implementado en el Paso 8:

### 9 Perfiles del sistema

| Reference | Descripción |
|---|---|
| `ADMIN` | Administrador del sistema |
| `SCRAP` | Sistema Colectivo de Responsabilidad Ampliada |
| `PRODUCER` | Productor / Generador de residuos |
| `CARRIER` | Transportista |
| `PLANT_OP` | Operador de Planta de Tratamiento |
| `CAC_OP` | Operador de Centro de Acopio |
| `PUBLIC_ENT` | Entidad Pública / Ayuntamiento |
| `COORDINATOR` | Coordinador del acuerdo |
| `DISPATCH_OFFICE` | Oficina de Asignación — Gestor logístico |

### Componentes de autorización

| Componente | Capa | Función |
|---|---|---|
| `ProfileConstants`, `PolicyConstants` | Domain | Constantes sin strings mágicos |
| `ProfileRequirement`, `OwnDataRequirement` | Infrastructure | Requisitos de ASP.NET Core |
| `ProfileAuthorizationHandler` | Infrastructure | Evalúa perfil del usuario |
| `OwnDataAuthorizationHandler` | Infrastructure | Evalúa datos propios + entidad vinculada |
| `IDataScopeService` / `DataScopeService` | Application / Infrastructure | Filtrado de IQueryable por perfil |
| `ProfileAuthorizeView` | Web (Blazor) | Muestra/oculta elementos de UI |
| `AuthorizationBehavior` | Application (MediatR) | Valida permisos en el pipeline |
| `AuthorizeAttribute` | Application | Decora commands/queries con permisos |

### 24 Policies registradas

Agrupadas en: Maestros · Operaciones · Sostenibilidad · Contratación · Reporting · Seguridad.

Ver [`Mapa_Autorizacion_GreenTransit.md`](./Mapa_Autorizacion_GreenTransit.md) para la matriz completa.  
Ver [`PATRON_AUTORIZACION_PAGINAS.md`](./PATRON_AUTORIZACION_PAGINAS.md) para el patrón de implementación en páginas Blazor.

### Uso en un command/query

```csharp
// Proteger un command con perfil específico
[Authorize(Profiles = "DISPATCH_OFFICE,ADMIN")]
public record CreateWasteMoveCommand(...) : IRequest<Guid>;

// Filtrar datos según el perfil en un query handler
var query = _db.ServiceOrders
    .Where(so => so.OwnerId == _currentUser.OwnerId)  // 1. multi-tenant
    .AsQueryable();
query = _dataScopeService.ApplyScope(query);           // 2. filtro por perfil
```

### Uso en una página Blazor

```razor
@attribute [Authorize]  @* o @attribute [Authorize(Policy = PolicyConstants.CanManageWasteMoves)] *@

<ProfileAuthorizeView Profiles="@(new[]{ ProfileConstants.DispatchOffice, ProfileConstants.Admin })">
    <Authorized>
        <button class="btn btn-primary">Nuevo Traslado</button>
    </Authorized>
</ProfileAuthorizeView>
```

---

## ⚙️ Reglas transversales

### Multi-tenant
- Todas las queries filtran por `OwnerId`

### Auditoría
- `CreatedAt`, `UpdatedAt`
- `IdUser`

### Integridad
- Relaciones lógicas validadas en aplicación
- Uso de `Hash` cuando aplica

---

## 🚀 Ejecución

```bash
dotnet restore
dotnet run