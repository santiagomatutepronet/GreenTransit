# 📋 Patrón de Autorización en Páginas Blazor — GreenTransit

> Referencia para implementar la protección de páginas y componentes usando las
> policies del sistema (Paso 8.4) y el componente `ProfileAuthorizeView` (Paso 8.6).

---

## 1. Patrón por tipo de acceso

### A) Página de acceso completo para todos los autenticados

```razor
@attribute [Authorize]
```

Usar cuando: la página es visible para cualquier perfil pero el contenido varía.
Ejemplos: `/incidents`, `/traceability`, `/waste-moves`, `/service-orders`.

---

### B) Página restringida a perfiles concretos

```razor
@attribute [Authorize(Policy = PolicyConstants.CanManageUsers)]
```

Usar cuando: la página entera es inaccesible para perfiles sin el permiso.
Ejemplos: `/users`, `/profiles`, `/ler-codes`, `/emission-factor-sets`.

---

### C) Página con permisos mixtos (todos ven, solo algunos crean/editan)

```razor
@* La página se protege con autenticación básica *@
@attribute [Authorize]

@* Los botones de acción se envuelven en ProfileAuthorizeView *@
<ProfileAuthorizeView Profiles="@(new[]{ ProfileConstants.DispatchOffice, ProfileConstants.Admin })">
    <Authorized>
        <button class="btn btn-primary" @onclick="CrearNuevo">
            <i class="bi bi-plus-circle"></i> Nuevo
        </button>
    </Authorized>
</ProfileAuthorizeView>

@* La query de datos usa IDataScopeService para filtrar según el perfil *@
```

Usar cuando: lectura amplia + escritura restringida.
Ejemplos: `/entities`, `/residues`, `/waste-moves`.

---

## 2. Mapeo completo de páginas

### 2.1. Módulo de Configuración

| Ruta | `@attribute [Authorize...]` | Botones Crear/Editar/Eliminar |
|---|---|---|
| `/entities` | `[Authorize]` | `CanManageEntities` (DISPATCH_OFFICE, ADMIN) + `CanCreateEntitiesRestricted` (SCRAP) |
| `/ler-codes` | `[Authorize(Policy = PolicyConstants.CanManageLER)]` | Ya protegida por la policy de página |
| `/residues` | `[Authorize]` | `CanManageResidues` (DISPATCH_OFFICE, ADMIN) + `CanManageOwnResidues` (PRODUCER) |
| `/treatment-operations` | `[Authorize(Policy = PolicyConstants.CanManageTreatmentOps)]` | Ya protegida |

### 2.2. Módulo de Operaciones

| Ruta | `@attribute [Authorize...]` | Botones Crear/Editar/Eliminar |
|---|---|---|
| `/service-orders` | `[Authorize]` | `CanManageServiceOrders` (DISPATCH, ADMIN) + `CanCreateOwnServiceOrders` (PRODUCER, PUBLIC_ENT) |
| `/service-orders/{id}` | `[Authorize]` | Mismo criterio |
| `/waste-moves` | `[Authorize]` | `CanManageWasteMoves` (DISPATCH, ADMIN) |
| `/waste-moves/{id}` | `[Authorize]` | `CanManageWasteMoves` (DISPATCH, ADMIN) + `CanUpdateAssignedMoves` (CARRIER) |
| `/entry-plants` | `[Authorize]` | `CanManageEntryPlants` (PLANT_OP, ADMIN) |
| `/entry-plants/{id}` | `[Authorize]` | `CanManageEntryPlants` |
| `/entry-cacs` | `[Authorize]` | `CanManageEntryCACs` (CAC_OP, ADMIN) |
| `/treatment-plants` | `[Authorize]` | `CanManageTreatments` (PLANT_OP, ADMIN) |

### 2.3. Módulo de Sostenibilidad

| Ruta | `@attribute [Authorize...]` | Botones Crear/Editar/Eliminar |
|---|---|---|
| `/incidents` | `[Authorize(Policy = PolicyConstants.CanCreateIncidents)]` | Apertura: todos. Resolución: `CanResolveIncidents` |
| `/dum-zones` | `[Authorize(Policy = PolicyConstants.CanManageDUMZones)]` | Ya protegida |
| `/dum-zones/simulator` | `[Authorize]` | Solo lectura (simulador) |
| `/emissions` | `[Authorize]` | `CanManageEmissionFactors` (solo re-cálculo) |
| `/plant-energies` | `[Authorize]` | `CanManagePlantEnergy` (PLANT_OP, ADMIN) |
| `/emission-factor-sets` | `[Authorize(Policy = PolicyConstants.CanManageEmissionFactors)]` | Ya protegida |

### 2.4. Módulo de Reporting

| Ruta | `@attribute [Authorize(Policy = PolicyConstants.CanViewReporting)]` | Notas |
|---|---|---|
| `/traceability` | ✅ | Solo lectura para todos |
| `/waste-moves/{id}/timeline` | ✅ | Solo lectura |
| `/kpis` | `[Authorize(Policy = PolicyConstants.CanViewKPIs)]` | Solo perfiles supervisores |
| `/documents` | `[Authorize(Policy = PolicyConstants.CanViewReporting)]` | CRUD solo ADMIN |

### 2.5. Módulo de Seguridad

| Ruta | `@attribute [Authorize...]` | Notas |
|---|---|---|
| `/users` | `[Authorize(Policy = PolicyConstants.CanManageUsers)]` | Solo ADMIN |
| `/profiles` | `[Authorize(Policy = PolicyConstants.CanManageProfiles)]` | Solo ADMIN |

---

## 3. Ejemplo de implementación completa — `/service-orders`

```razor
@page "/service-orders"
@attribute [Authorize]
@using GreenTransit.Domain.Authorization
@inject ICurrentUserService CurrentUser
@inject IDataScopeService DataScope

<h2>Órdenes de Servicio</h2>

@* Botón de creación: visible solo para quien puede crear *@
<ProfileAuthorizeView Profiles="@(new[]{
    ProfileConstants.DispatchOffice,
    ProfileConstants.Admin,
    ProfileConstants.Producer,
    ProfileConstants.PublicEnt })">
    <Authorized>
        <a href="/service-orders/new" class="btn btn-primary">
            <i class="bi bi-plus-circle"></i> Nueva orden
        </a>
    </Authorized>
</ProfileAuthorizeView>

@* Lista de órdenes — el handler ya aplica DataScopeService *@
@foreach (var so in _serviceOrders)
{
    <div class="card">
        <span>@so.ServiceOrderNumber</span>

        @* Botón de editar: solo quien puede gestionar *@
        <ProfileAuthorizeView Profiles="@(new[]{
            ProfileConstants.DispatchOffice, ProfileConstants.Admin })">
            <Authorized>
                <a href="/service-orders/@so.Id/edit" class="btn btn-sm btn-outline-primary">Editar</a>
            </Authorized>
        </ProfileAuthorizeView>
    </div>
}
```

---

## 4. Patrón en Query Handlers MediatR

```csharp
// Ejemplo: GetServiceOrdersQueryHandler.cs
public async Task<List<ServiceOrderDto>> Handle(GetServiceOrdersQuery request, CancellationToken ct)
{
    var query = _db.ServiceOrders
        .Where(so => so.OwnerId == _currentUser.OwnerId)  // 1. Filtro multi-tenant
        .AsQueryable();

    query = _dataScope.ApplyScope(query);                  // 2. Filtro por perfil

    // 3. Filtros adicionales del request (buscador, fechas, estado...)
    if (!string.IsNullOrEmpty(request.SearchTerm))
        query = query.Where(so => so.ServiceOrderNumber.Contains(request.SearchTerm));

    return await query
        .OrderByDescending(so => so.CreatedAt)
        .Select(so => new ServiceOrderDto { ... })
        .ToListAsync(ct);
}
```

---

*Documento generado en el Paso 8.6 del sistema de autorización GreenTransit.*
