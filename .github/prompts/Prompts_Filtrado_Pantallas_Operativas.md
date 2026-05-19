# Prompts de Filtrado de Datos por Perfil — Pantallas Operativas GreenTransit

> **Propósito**: Cada sección de este documento es un **prompt independiente** para GitHub Copilot.
> Adjuntar siempre `COPILOT_CONTEXT.md`, `README.md` y `Crear_BD_v4_1.sql` al inicio de la sesión de Copilot.
>
> **Reglas transversales** (aplican a TODOS los prompts):
> 1. Filtro multi-tenant: `WHERE OwnerId = @currentUserOwnerId` en TODAS las queries operativas.
> 2. Catálogos compartidos (`LERCodes`, `TreatmentOperations`, tablas geográficas, `Profiles`) NO filtran por `OwnerId`.
> 3. El acceso a la pantalla se controla desde `PageDefinitions`/`PagePermissions` (sistema dinámico). Las policies en código son el mínimo de seguridad estático.
> 4. Usar `ICurrentUserService.LinkedEntityId` para obtener la entidad vinculada al usuario.
> 5. Usar `ICurrentUserService.IsInAnyProfile(...)` para detectar el perfil activo.
> 6. Usar `IDataScopeService.ApplyScope()` donde esté disponible.
> 7. Datos geográficos (`ProvinceCode`, `MunicipalityCode`, `AutonomousCommunity`) se resuelven siempre a `Name` (JOIN con tablas de geografía), nunca se muestran como código.
> 8. `ADMIN` y `DISPATCH_OFFICE` ven todos los datos del tenant (solo filtro `OwnerId`), salvo que se indique lo contrario.
> 9. No se crean nuevas entidades de dominio. Todo se implementa con el modelo v4.1.
> 10. Los botones de acción (Crear, Editar, Eliminar) se controlan por el nivel de permiso en `PagePermissions` (Lectura vs Escritura vs Ambos). El filtrado de datos es independiente de los permisos de acción.

---

## MÓDULO 1 — DASHBOARD OPERATIVO (HOME)

---

### PROMPT 1.0 — Dashboard Operativo (`/`)

**Archivo Query**: `Application/Features/Dashboard/Queries/GetDashboardSummaryQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Dashboard/DashboardHome.razor`
**Policy**: `[Authorize]` (todos los autenticados)

**Perfiles con acceso**: TODOS (cada perfil ve KPIs adaptados a su rol)

**Instrucciones de filtrado para Copilot**:

```
En el handler GetDashboardSummaryQuery, implementa un dashboard multirrol que adapta
los WIDGETS VISIBLES y el FILTRADO DE DATOS al perfil activo del usuario autenticado:

REGLA 1 — PRODUCER:
  - Embudo de traslados: WasteMoves WHERE ServiceOrderId IN (
      SELECT Id FROM ServiceOrders WHERE IdIssuedBy = @LinkedEntityId
    )
  - Kg recogidos (mes): WasteMoveResidues de los traslados anteriores
  - CO₂ (mes actual y anterior): WasteMoveResidues.TransportInfo_TransportCarbonEmissions
    sobre los traslados filtrados
  - Incidencias abiertas: Incidents WHERE ServiceOrder.IdIssuedBy = @LinkedEntityId
  - Próximas recogidas: ServiceOrders WHERE IdIssuedBy = @LinkedEntityId
    AND Status IN ('Pending', 'Scheduled')
  - Kg recogidos vs tratados (6 meses): sobre el mismo subset de traslados

REGLA 2 — CARRIER:
  - Embudo de traslados: WasteMoves WHERE Id IN (
      SELECT IdWasteMove FROM WasteMoveResidues WHERE IdCarrier = @LinkedEntityId
    )
  - Kg recogidos: WasteMoveResidues WHERE IdCarrier = @LinkedEntityId
  - CO₂: WasteMoveResidues WHERE IdCarrier = @LinkedEntityId
  - Incidencias: Incidents vinculadas a WasteMoves donde es transportista
  - Próximas recogidas: WasteMoves planificados donde es transportista asignado

REGLA 3 — SCRAP:
  - Embudo de traslados: WasteMoves WHERE (IdScrap = @LinkedEntityId
    OR IdScrap2 = @LinkedEntityId)
  - Todos los KPIs operan sobre ese subset
  - Incidencias: de los traslados del SCRAP
  - Kg recogidos vs tratados: EntryPlantResidues y TreatmentPlantResidues
    vinculados a traslados del SCRAP

REGLA 4 — PUBLIC_ENT:
  - Traslados vinculados a SOs emitidas por la entidad:
    WasteMoves WHERE ServiceOrderId IN (
      SELECT Id FROM ServiceOrders WHERE IdIssuedBy = @LinkedEntityId
    )
  - Liquidaciones: Settlements WHERE IdPublicEntity = @LinkedEntityId
  - Cumplimiento: MarketShares de los SCRAPs de sus Agreements

REGLA 5 — CAC_OP:
  - Entradas en CAC de su entidad: EntryCACs vinculadas a traslados cuyo
    destino es su entidad
  - Stock acumulado por residuo
  - Widgets limitados a su CAC

REGLA 6 — PLANT_OP:
  - Entradas en planta de su entidad: EntryPlants vinculadas a traslados
    cuyo IdDestination = @LinkedEntityId
  - Balance de tratamiento: TreatmentPlants/TreatmentPlantResidues de su planta
  - Widgets limitados a su planta

REGLA 7 — COORDINATOR:
  - Agreements WHERE IdCoordinator = @LinkedEntityId → scrapIds
  - Traslados de esos SCRAPs: WasteMoves WHERE IdScrap IN (scrapIds)
  - Vista transversal de cumplimiento de los SCRAPs coordinados

REGLA 8 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción dentro del tenant (solo filtro OwnerId)
  - Todos los widgets activos: SO pendientes, embudo completo,
    planificación semanal, incidencias, KPIs globales

PATRÓN DE IMPLEMENTACIÓN:
  var profile = currentUser.ProfileReference;
  var ownerId = currentUser.OwnerId;
  var linkedEntityId = currentUser.LinkedEntityId;

  // Base query siempre filtrada por tenant
  var wasteMovesQuery = _dbContext.WasteMoves
      .Where(wm => wm.OwnerId == ownerId);

  switch (profile)
  {
      case "PRODUCER":
          var soIds = await _dbContext.ServiceOrders
              .Where(so => so.IdIssuedBy == linkedEntityId && so.OwnerId == ownerId)
              .Select(so => so.Id).ToListAsync();
          wasteMovesQuery = wasteMovesQuery.Where(wm => soIds.Contains(wm.ServiceOrderId));
          break;

      case "CARRIER":
          var carrierWmIds = await _dbContext.WasteMoveResidues
              .Where(wmr => wmr.IdCarrier == linkedEntityId)
              .Select(wmr => wmr.IdWasteMove).Distinct().ToListAsync();
          wasteMovesQuery = wasteMovesQuery.Where(wm => carrierWmIds.Contains(wm.Id));
          break;

      case "SCRAP":
          wasteMovesQuery = wasteMovesQuery.Where(wm =>
              wm.IdScrap == linkedEntityId || wm.IdScrap2 == linkedEntityId);
          break;

      case "PUBLIC_ENT":
          var publicSoIds = await _dbContext.ServiceOrders
              .Where(so => so.IdIssuedBy == linkedEntityId && so.OwnerId == ownerId)
              .Select(so => so.Id).ToListAsync();
          wasteMovesQuery = wasteMovesQuery.Where(wm => publicSoIds.Contains(wm.ServiceOrderId));
          break;

      case "CAC_OP":
          // Filtrar por entradas CAC de su entidad
          break;

      case "PLANT_OP":
          wasteMovesQuery = wasteMovesQuery.Where(wm => wm.IdDestination == linkedEntityId);
          break;

      case "COORDINATOR":
          var scrapIds = await _dbContext.Agreements
              .Where(a => a.IdCoordinator == linkedEntityId && a.OwnerId == ownerId)
              .Select(a => a.IdScrap).Distinct().ToListAsync();
          wasteMovesQuery = wasteMovesQuery.Where(wm => scrapIds.Contains(wm.IdScrap));
          break;

      // DISPATCH_OFFICE, ADMIN: sin filtro adicional
  }
```

---

## MÓDULO 2 — OPERACIONES

---

### PROMPT 2.1 — Órdenes de Servicio (`/service-orders`)

**Archivo Query**: `Application/Features/ServiceOrders/Queries/GetServiceOrdersQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/ServiceOrders/ServiceOrderList.razor`
**Policy**: `[Authorize]` (acceso diferenciado por perfil)

**Perfiles con acceso y nivel**:
- `PRODUCER`: CRUD-P (solo las suyas)
- `PUBLIC_ENT`: CRUD-P (solo las suyas)
- `CARRIER`: R (lectura)
- `SCRAP`: R (con filtro especial)
- `CAC_OP`: R
- `PLANT_OP`: R
- `COORDINATOR`: R
- `DISPATCH_OFFICE`: CRUD (todas del tenant)
- `ADMIN`: CRUD (todas del tenant)

**Instrucciones de filtrado para Copilot**:

```
En el handler GetServiceOrdersQuery, implementa el siguiente filtrado según el perfil:

REGLA 1 — PRODUCER:
  - Filtrar automáticamente: WHERE IdIssuedBy = @LinkedEntityId
  - El usuario NO puede anular este filtro desde la UI.
  - En ServiceOrderForm.razor: el campo Emisor (IdIssuedBy) se autocompleta
    con LinkedEntityId y es de solo lectura.
  - Puede crear, editar (si Status = Pending o Scheduled) y ver sus propias SOs.

REGLA 2 — PUBLIC_ENT:
  - Filtrar automáticamente: WHERE IdIssuedBy = @LinkedEntityId
  - Mismo comportamiento que PRODUCER en cuanto a filtro obligatorio.
  - Puede crear SOs (solicitar recogida para su municipio).

REGLA 3 — SCRAP:
  - Filtro especial en dos partes:
    a) SOs sin traslado asignado aún (cualquier SCRAP del tenant puede reclamarlas):
       WHERE NOT EXISTS (SELECT 1 FROM WasteMoves wm WHERE wm.ServiceOrderId = so.Id)
    b) SOs cuyo traslado vinculado tiene al SCRAP como responsable:
       WHERE EXISTS (SELECT 1 FROM WasteMoves wm
         WHERE wm.ServiceOrderId = so.Id
         AND (wm.IdScrap = @LinkedEntityId OR wm.IdScrap2 = @LinkedEntityId))
    - Resultado: so WHERE (condición a) OR (condición b)
  - Solo lectura. No puede crear ni editar SOs.

REGLA 4 — CARRIER:
  - Solo lectura de SOs vinculadas a traslados donde es transportista:
    WHERE Id IN (
      SELECT wm.ServiceOrderId FROM WasteMoves wm
      JOIN WasteMoveResidues wmr ON wmr.IdWasteMove = wm.Id
      WHERE wmr.IdCarrier = @LinkedEntityId
    )

REGLA 5 — CAC_OP:
  - Solo lectura de SOs cuyo punto de recogida (IdPickupPoint) corresponde
    a su entidad, O cuyo traslado tiene como destino su CAC:
    WHERE IdPickupPoint = @LinkedEntityId
    OR Id IN (SELECT ServiceOrderId FROM WasteMoves WHERE IdDestination = @LinkedEntityId)

REGLA 6 — PLANT_OP:
  - Solo lectura de SOs cuyo traslado tiene como destino su planta:
    WHERE Id IN (
      SELECT ServiceOrderId FROM WasteMoves WHERE IdDestination = @LinkedEntityId
    )

REGLA 7 — COORDINATOR:
  - Solo lectura de SOs vinculadas a traslados de SCRAPs de sus acuerdos:
    WHERE Id IN (
      SELECT wm.ServiceOrderId FROM WasteMoves wm
      WHERE wm.IdScrap IN (
        SELECT IdScrap FROM Agreements WHERE IdCoordinator = @LinkedEntityId
      )
    )

REGLA 8 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción adicional. Solo filtro OwnerId.
  - CRUD completo. Puede crear SOs para cualquier emisor.
  - Filtros opcionales de UI: IdIssuedBy, Status, WasteStream, DateRange.

PATRÓN DE IMPLEMENTACIÓN:
  var query = _dbContext.ServiceOrders
      .Where(so => so.OwnerId == currentUser.OwnerId);

  if (currentUser.IsInProfile("PRODUCER") || currentUser.IsInProfile("PUBLIC_ENT"))
      query = query.Where(so => so.IdIssuedBy == currentUser.LinkedEntityId);
  else if (currentUser.IsInProfile("SCRAP"))
  {
      var linkedId = currentUser.LinkedEntityId;
      query = query.Where(so =>
          !_dbContext.WasteMoves.Any(wm => wm.ServiceOrderId == so.Id)
          || _dbContext.WasteMoves.Any(wm =>
              wm.ServiceOrderId == so.Id
              && (wm.IdScrap == linkedId || wm.IdScrap2 == linkedId)));
  }
  else if (currentUser.IsInProfile("CARRIER"))
  {
      var wmIds = _dbContext.WasteMoveResidues
          .Where(wmr => wmr.IdCarrier == currentUser.LinkedEntityId)
          .Select(wmr => wmr.IdWasteMove);
      var soIds = _dbContext.WasteMoves
          .Where(wm => wmIds.Contains(wm.Id))
          .Select(wm => wm.ServiceOrderId);
      query = query.Where(so => soIds.Contains(so.Id));
  }
  else if (currentUser.IsInProfile("CAC_OP"))
  {
      query = query.Where(so =>
          so.IdPickupPoint == currentUser.LinkedEntityId
          || _dbContext.WasteMoves.Any(wm =>
              wm.ServiceOrderId == so.Id
              && wm.IdDestination == currentUser.LinkedEntityId));
  }
  else if (currentUser.IsInProfile("PLANT_OP"))
  {
      query = query.Where(so =>
          _dbContext.WasteMoves.Any(wm =>
              wm.ServiceOrderId == so.Id
              && wm.IdDestination == currentUser.LinkedEntityId));
  }
  else if (currentUser.IsInProfile("COORDINATOR"))
  {
      var scrapIds = await _dbContext.Agreements
          .Where(a => a.IdCoordinator == currentUser.LinkedEntityId)
          .Select(a => a.IdScrap).Distinct().ToListAsync();
      query = query.Where(so =>
          _dbContext.WasteMoves.Any(wm =>
              wm.ServiceOrderId == so.Id && scrapIds.Contains(wm.IdScrap)));
  }
  // DISPATCH_OFFICE, ADMIN: sin filtro adicional

  // Aplicar filtros opcionales de UI
  if (request.IdIssuedBy.HasValue)
      query = query.Where(so => so.IdIssuedBy == request.IdIssuedBy.Value);
  if (!string.IsNullOrEmpty(request.Status))
      query = query.Where(so => so.Status == request.Status);
```

---

### PROMPT 2.2 — Traslados (`/waste-moves`)

**Archivo Query**: `Application/Features/WasteMoves/Queries/GetWasteMovesQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/WasteMoves/WasteMoveList.razor`
**Policy**: `[Authorize]`

**Perfiles con acceso y nivel**:
- `PRODUCER`: R-P (solo traslados de sus SOs)
- `CARRIER`: U-P (solo traslados donde está asignado — puede actualizar datos de ejecución)
- `SCRAP`: R (sus traslados)
- `PUBLIC_ENT`: R (traslados de sus SOs)
- `CAC_OP`: R (traslados con destino su CAC)
- `PLANT_OP`: R (traslados con destino su planta)
- `COORDINATOR`: R (traslados de SCRAPs de sus acuerdos)
- `DISPATCH_OFFICE`: CRUD
- `ADMIN`: CRUD

**Instrucciones de filtrado para Copilot**:

```
En el handler GetWasteMovesQuery, implementa:

REGLA 1 — PRODUCER:
  - Solo lectura de traslados originados de sus SOs:
    WHERE ServiceOrderId IN (
      SELECT Id FROM ServiceOrders WHERE IdIssuedBy = @LinkedEntityId
    )
  - No puede crear, editar ni eliminar traslados.

REGLA 2 — CARRIER:
  - Solo traslados donde es transportista asignado:
    WHERE Id IN (
      SELECT IdWasteMove FROM WasteMoveResidues WHERE IdCarrier = @LinkedEntityId
    )
  - Puede actualizar (U-P): ActualPickupStart/End, GatheredDate, DocumentId,
    DocumentHash, SignatureStatus, y datos de transporte en WasteMoveResidues
    (NTNumber, DINumber, DIPhase).
  - NO puede modificar: IdSource, IdDestination, IdScrap, ni crear nuevos traslados.

REGLA 3 — SCRAP:
  - WHERE (IdScrap = @LinkedEntityId OR IdScrap2 = @LinkedEntityId)
  - Solo lectura.

REGLA 4 — PUBLIC_ENT:
  - WHERE ServiceOrderId IN (
      SELECT Id FROM ServiceOrders WHERE IdIssuedBy = @LinkedEntityId
    )
  - Solo lectura.

REGLA 5 — CAC_OP:
  - WHERE IdDestination = @LinkedEntityId
    (traslados cuyo destino es su CAC)
  - O WHERE IdSource = @LinkedEntityId
    (traslados cuyo origen es su CAC — si reexpide)
  - Solo lectura.

REGLA 6 — PLANT_OP:
  - WHERE IdDestination = @LinkedEntityId
  - Solo lectura.

REGLA 7 — COORDINATOR:
  - Agreements WHERE IdCoordinator = @LinkedEntityId → scrapIds
  - WHERE IdScrap IN (scrapIds)
  - Solo lectura.

REGLA 8 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción. Solo OwnerId. CRUD completo.

PATRÓN DE IMPLEMENTACIÓN:
  var query = _dbContext.WasteMoves
      .Where(wm => wm.OwnerId == currentUser.OwnerId);

  if (currentUser.IsInProfile("PRODUCER"))
  {
      var soIds = await _dbContext.ServiceOrders
          .Where(so => so.IdIssuedBy == currentUser.LinkedEntityId)
          .Select(so => so.Id).ToListAsync();
      query = query.Where(wm => soIds.Contains(wm.ServiceOrderId));
  }
  else if (currentUser.IsInProfile("CARRIER"))
  {
      var wmIds = await _dbContext.WasteMoveResidues
          .Where(wmr => wmr.IdCarrier == currentUser.LinkedEntityId)
          .Select(wmr => wmr.IdWasteMove).Distinct().ToListAsync();
      query = query.Where(wm => wmIds.Contains(wm.Id));
  }
  else if (currentUser.IsInProfile("SCRAP"))
      query = query.Where(wm =>
          wm.IdScrap == currentUser.LinkedEntityId
          || wm.IdScrap2 == currentUser.LinkedEntityId);
  else if (currentUser.IsInProfile("PUBLIC_ENT"))
  {
      var soIds = await _dbContext.ServiceOrders
          .Where(so => so.IdIssuedBy == currentUser.LinkedEntityId)
          .Select(so => so.Id).ToListAsync();
      query = query.Where(wm => soIds.Contains(wm.ServiceOrderId));
  }
  else if (currentUser.IsInProfile("CAC_OP"))
      query = query.Where(wm =>
          wm.IdDestination == currentUser.LinkedEntityId
          || wm.IdSource == currentUser.LinkedEntityId);
  else if (currentUser.IsInProfile("PLANT_OP"))
      query = query.Where(wm => wm.IdDestination == currentUser.LinkedEntityId);
  else if (currentUser.IsInProfile("COORDINATOR"))
  {
      var scrapIds = await _dbContext.Agreements
          .Where(a => a.IdCoordinator == currentUser.LinkedEntityId)
          .Select(a => a.IdScrap).Distinct().ToListAsync();
      query = query.Where(wm => scrapIds.Contains(wm.IdScrap));
  }
  // DISPATCH_OFFICE, ADMIN: sin filtro adicional
```

---

### PROMPT 2.3 — Entradas Planta (`/entry-plants`)

**Archivo Query**: `Application/Features/EntryPlants/Queries/GetEntryPlantsQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/EntryPlants/EntryPlantList.razor`
**Policy**: `[Authorize]`

**Perfiles con acceso y nivel**:
- `PRODUCER`: sin acceso (—)
- `CARRIER`: sin acceso (—)
- `SCRAP`: R (entradas vinculadas a traslados del SCRAP)
- `PUBLIC_ENT`: R (entradas vinculadas a traslados de SOs de la entidad)
- `CAC_OP`: sin acceso (—)
- `PLANT_OP`: CRUD-P (solo su planta)
- `COORDINATOR`: R (entradas de plantas que procesan residuos de SCRAPs coordinados)
- `DISPATCH_OFFICE`: R (todo el tenant)
- `ADMIN`: CRUD (todo el tenant)

**Instrucciones de filtrado para Copilot**:

```
En el handler GetEntryPlantsQuery, implementa:

REGLA 1 — PLANT_OP:
  - Solo entradas de SU planta:
    WHERE IdWasteMove IN (
      SELECT Id FROM WasteMoves WHERE IdDestination = @LinkedEntityId
    )
  - O si EntryPlants tiene un campo directo de planta, filtrar por ese campo.
  - CRUD completo sobre sus propias entradas.

REGLA 2 — SCRAP:
  - Entradas vinculadas a traslados del SCRAP:
    WHERE IdWasteMove IN (
      SELECT Id FROM WasteMoves
      WHERE (IdScrap = @LinkedEntityId OR IdScrap2 = @LinkedEntityId)
    )
  - Solo lectura.

REGLA 3 — PUBLIC_ENT:
  - Entradas vinculadas a traslados de SOs emitidas por la entidad:
    WHERE IdWasteMove IN (
      SELECT wm.Id FROM WasteMoves wm
      JOIN ServiceOrders so ON so.Id = wm.ServiceOrderId
      WHERE so.IdIssuedBy = @LinkedEntityId
    )
  - Solo lectura.

REGLA 4 — COORDINATOR:
  - Agreements WHERE IdCoordinator = @LinkedEntityId → scrapIds
  - Entradas vinculadas a traslados de esos SCRAPs:
    WHERE IdWasteMove IN (
      SELECT Id FROM WasteMoves WHERE IdScrap IN (scrapIds)
    )
  - Solo lectura.

REGLA 5 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción dentro del tenant.

PATRÓN DE IMPLEMENTACIÓN:
  var query = _dbContext.EntryPlants
      .Where(ep => ep.OwnerId == currentUser.OwnerId);

  if (currentUser.IsInProfile("PLANT_OP"))
  {
      var plantWmIds = _dbContext.WasteMoves
          .Where(wm => wm.IdDestination == currentUser.LinkedEntityId
                    && wm.OwnerId == currentUser.OwnerId)
          .Select(wm => wm.Id);
      query = query.Where(ep => plantWmIds.Contains(ep.IdWasteMove));
  }
  else if (currentUser.IsInProfile("SCRAP"))
  {
      var scrapWmIds = _dbContext.WasteMoves
          .Where(wm => (wm.IdScrap == currentUser.LinkedEntityId
                     || wm.IdScrap2 == currentUser.LinkedEntityId)
                    && wm.OwnerId == currentUser.OwnerId)
          .Select(wm => wm.Id);
      query = query.Where(ep => scrapWmIds.Contains(ep.IdWasteMove));
  }
  else if (currentUser.IsInProfile("PUBLIC_ENT"))
  {
      var soIds = await _dbContext.ServiceOrders
          .Where(so => so.IdIssuedBy == currentUser.LinkedEntityId)
          .Select(so => so.Id).ToListAsync();
      var wmIds = await _dbContext.WasteMoves
          .Where(wm => soIds.Contains(wm.ServiceOrderId))
          .Select(wm => wm.Id).ToListAsync();
      query = query.Where(ep => wmIds.Contains(ep.IdWasteMove));
  }
  else if (currentUser.IsInProfile("COORDINATOR"))
  {
      var scrapIds = await _dbContext.Agreements
          .Where(a => a.IdCoordinator == currentUser.LinkedEntityId)
          .Select(a => a.IdScrap).Distinct().ToListAsync();
      var wmIds = _dbContext.WasteMoves
          .Where(wm => scrapIds.Contains(wm.IdScrap) && wm.OwnerId == currentUser.OwnerId)
          .Select(wm => wm.Id);
      query = query.Where(ep => wmIds.Contains(ep.IdWasteMove));
  }
  // DISPATCH_OFFICE, ADMIN: sin filtro adicional
```

---

### PROMPT 2.4 — Entradas CAC (`/entry-cacs`)

**Archivo Query**: `Application/Features/EntryCACs/Queries/GetEntryCACsQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/EntryCACs/EntryCACList.razor`
**Policy**: `[Authorize]`

**Perfiles con acceso y nivel**:
- `SCRAP`: R (entradas en CAC cuyo traslado vinculado es del SCRAP)
- `PUBLIC_ENT`: R (entradas vinculadas a SOs de la entidad)
- `CAC_OP`: CRUD-P (solo su CAC)
- `COORDINATOR`: R
- `DISPATCH_OFFICE`: R
- `ADMIN`: CRUD

**Instrucciones de filtrado para Copilot**:

```
En el handler GetEntryCACsQuery, implementa:

REGLA 1 — CAC_OP:
  - Solo entradas de SU CAC.
  - Filtrar EntryCACs cuyo traslado vinculado (IdWasteMove → WasteMoves)
    tiene IdDestination = @LinkedEntityId O IdSource = @LinkedEntityId.
  - CRUD completo sobre sus propias entradas.

REGLA 2 — SCRAP:
  - Solo entradas en CAC cuyo traslado vinculado tiene
    IdScrap = @LinkedEntityId OR IdScrap2 = @LinkedEntityId:
    WHERE IdWasteMove IN (
      SELECT Id FROM WasteMoves
      WHERE (IdScrap = @LinkedEntityId OR IdScrap2 = @LinkedEntityId)
    )
  - Solo lectura.

REGLA 3 — PUBLIC_ENT:
  - Entradas vinculadas a traslados de SOs emitidas por la entidad:
    WHERE IdWasteMove IN (
      SELECT wm.Id FROM WasteMoves wm
      JOIN ServiceOrders so ON so.Id = wm.ServiceOrderId
      WHERE so.IdIssuedBy = @LinkedEntityId
    )
  - Solo lectura.

REGLA 4 — COORDINATOR:
  - Entradas de traslados de SCRAPs de sus acuerdos.
  - Solo lectura.

REGLA 5 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción dentro del tenant.

PATRÓN DE IMPLEMENTACIÓN:
  var query = _dbContext.EntryCACs
      .Where(ec => ec.OwnerId == currentUser.OwnerId);

  if (currentUser.IsInProfile("CAC_OP"))
  {
      var cacWmIds = _dbContext.WasteMoves
          .Where(wm => (wm.IdDestination == currentUser.LinkedEntityId
                     || wm.IdSource == currentUser.LinkedEntityId)
                    && wm.OwnerId == currentUser.OwnerId)
          .Select(wm => wm.Id);
      query = query.Where(ec => cacWmIds.Contains(ec.IdWasteMove));
  }
  else if (currentUser.IsInProfile("SCRAP"))
  {
      var scrapWmIds = _dbContext.WasteMoves
          .Where(wm => (wm.IdScrap == currentUser.LinkedEntityId
                     || wm.IdScrap2 == currentUser.LinkedEntityId)
                    && wm.OwnerId == currentUser.OwnerId)
          .Select(wm => wm.Id);
      query = query.Where(ec => scrapWmIds.Contains(ec.IdWasteMove));
  }
  else if (currentUser.IsInProfile("PUBLIC_ENT"))
  {
      var soIds = await _dbContext.ServiceOrders
          .Where(so => so.IdIssuedBy == currentUser.LinkedEntityId)
          .Select(so => so.Id).ToListAsync();
      var wmIds = await _dbContext.WasteMoves
          .Where(wm => soIds.Contains(wm.ServiceOrderId))
          .Select(wm => wm.Id).ToListAsync();
      query = query.Where(ec => wmIds.Contains(ec.IdWasteMove));
  }
  else if (currentUser.IsInProfile("COORDINATOR"))
  {
      var scrapIds = await _dbContext.Agreements
          .Where(a => a.IdCoordinator == currentUser.LinkedEntityId)
          .Select(a => a.IdScrap).Distinct().ToListAsync();
      var wmIds = _dbContext.WasteMoves
          .Where(wm => scrapIds.Contains(wm.IdScrap) && wm.OwnerId == currentUser.OwnerId)
          .Select(wm => wm.Id);
      query = query.Where(ec => wmIds.Contains(ec.IdWasteMove));
  }
  // DISPATCH_OFFICE, ADMIN: sin filtro adicional
```

---

### PROMPT 2.5 — Tratamiento (`/treatment-plants`)

**Archivo Query**: `Application/Features/TreatmentPlants/Queries/GetTreatmentPlantsQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/TreatmentPlants/TreatmentPlantList.razor`
**Policy**: `[Authorize]`

**Perfiles con acceso y nivel**:
- `SCRAP`: R (tratamientos de traslados del SCRAP)
- `PUBLIC_ENT`: R (tratamientos vinculados a SOs de la entidad)
- `PLANT_OP`: CRUD-P (solo su planta)
- `COORDINATOR`: R
- `DISPATCH_OFFICE`: R
- `ADMIN`: CRUD

**Instrucciones de filtrado para Copilot**:

```
En el handler GetTreatmentPlantsQuery, implementa:

REGLA 1 — PLANT_OP:
  - Solo tratamientos de SU planta:
    WHERE IdWasteMove IN (
      SELECT Id FROM WasteMoves WHERE IdDestination = @LinkedEntityId
    )
  - CRUD completo.

REGLA 2 — SCRAP:
  - WHERE IdWasteMove IN (
      SELECT Id FROM WasteMoves
      WHERE (IdScrap = @LinkedEntityId OR IdScrap2 = @LinkedEntityId)
    )
  - Solo lectura. Ve tratamientos de TODAS las plantas que procesan
    sus residuos (vista multi-planta).

REGLA 3 — PUBLIC_ENT:
  - Tratamientos vinculados a traslados de SOs emitidas por la entidad.
  - Solo lectura.

REGLA 4 — COORDINATOR:
  - Tratamientos de traslados de SCRAPs de sus acuerdos.
  - Solo lectura.

REGLA 5 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción dentro del tenant.

PATRÓN: idéntico al de Entradas Planta (PROMPT 2.3), sustituyendo
_dbContext.EntryPlants por _dbContext.TreatmentPlants.
```

---

## MÓDULO 3 — ECONOMÍA

---

### PROMPT 3.1 — Acuerdos (`/agreements`)

**Archivo Query**: `Application/Features/Agreements/Queries/GetAgreementsQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Agreements/AgreementList.razor`
**Policy**: `[Authorize]`

**Perfiles con acceso y nivel**:
- `SCRAP`: C+R (alta/edición de los suyos)
- `PUBLIC_ENT`: R (lectura + firma de los suyos)
- `COORDINATOR`: R (acuerdos donde es coordinador)
- `DISPATCH_OFFICE`: CRUD
- `ADMIN`: CRUD

**Instrucciones de filtrado para Copilot**:

```
En el handler GetAgreementsQuery, implementa:

REGLA 1 — SCRAP:
  - WHERE IdScrap = @LinkedEntityId
  - Puede crear acuerdos donde sea el SCRAP asignado.
  - Puede editar acuerdos propios en estado Draft.

REGLA 2 — PUBLIC_ENT:
  - WHERE IdPublicEntity = @LinkedEntityId
  - Solo lectura. Puede participar en flujo de firma.

REGLA 3 — COORDINATOR:
  - WHERE IdCoordinator = @LinkedEntityId
  - Solo lectura transversal de los acuerdos donde coordina.

REGLA 4 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción dentro del tenant.
  - CRUD completo.

PATRÓN DE IMPLEMENTACIÓN:
  var query = _dbContext.Agreements
      .Where(a => a.OwnerId == currentUser.OwnerId);

  if (currentUser.IsInProfile("SCRAP"))
      query = query.Where(a => a.IdScrap == currentUser.LinkedEntityId);
  else if (currentUser.IsInProfile("PUBLIC_ENT"))
      query = query.Where(a => a.IdPublicEntity == currentUser.LinkedEntityId);
  else if (currentUser.IsInProfile("COORDINATOR"))
      query = query.Where(a => a.IdCoordinator == currentUser.LinkedEntityId);
  // DISPATCH_OFFICE, ADMIN: sin filtro adicional

  // Filtros opcionales de UI
  if (!string.IsNullOrEmpty(request.Status))
      query = query.Where(a => a.Status == request.Status);
  if (request.IdScrap.HasValue)
      query = query.Where(a => a.IdScrap == request.IdScrap.Value);
```

---

### PROMPT 3.2 — Liquidaciones (`/settlements`)

**Archivo Query**: `Application/Features/Settlements/Queries/GetSettlementsQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Settlements/SettlementList.razor`
**Policy**: `[Authorize]`

**Perfiles con acceso y nivel**:
- `SCRAP`: V (validador — aprueba/rechaza liquidaciones donde es SCRAP)
- `PUBLIC_ENT`: R (solo sus liquidaciones)
- `COORDINATOR`: R (liquidaciones de SCRAPs de sus acuerdos)
- `DISPATCH_OFFICE`: R (todo el tenant)
- `ADMIN`: CRUD (cálculo, generación)

**Instrucciones de filtrado para Copilot**:

```
En el handler GetSettlementsQuery, implementa:

REGLA 1 — SCRAP:
  - WHERE IdScrap = @LinkedEntityId
  - Puede validar (aprobar/rechazar) las liquidaciones de sus acuerdos.
  - No puede crear ni calcular liquidaciones (eso es del ADMIN).

REGLA 2 — PUBLIC_ENT:
  - WHERE IdPublicEntity = @LinkedEntityId
  - Solo lectura. Revisa las compensaciones que recibe.

REGLA 3 — COORDINATOR:
  - Agreements WHERE IdCoordinator = @LinkedEntityId → scrapIds
  - WHERE IdScrap IN (scrapIds)
  - Solo lectura transversal.

REGLA 4 — DISPATCH_OFFICE:
  - Sin restricción dentro del tenant. Solo lectura.

REGLA 5 — ADMIN:
  - Sin restricción. CRUD completo (calcula, genera, edita).

PATRÓN DE IMPLEMENTACIÓN:
  var query = _dbContext.Settlements
      .Where(s => s.OwnerId == currentUser.OwnerId);

  if (currentUser.IsInProfile("SCRAP"))
      query = query.Where(s => s.IdScrap == currentUser.LinkedEntityId);
  else if (currentUser.IsInProfile("PUBLIC_ENT"))
      query = query.Where(s => s.IdPublicEntity == currentUser.LinkedEntityId);
  else if (currentUser.IsInProfile("COORDINATOR"))
  {
      var scrapIds = await _dbContext.Agreements
          .Where(a => a.IdCoordinator == currentUser.LinkedEntityId)
          .Select(a => a.IdScrap).Distinct().ToListAsync();
      query = query.Where(s => scrapIds.Contains(s.IdScrap));
  }
  // DISPATCH_OFFICE, ADMIN: sin filtro adicional
```

---

### PROMPT 3.3 — Cuotas de Mercado (`/market-shares`)

**Archivo Query**: `Application/Features/MarketShares/Queries/GetMarketSharesQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/MarketShares/MarketShareList.razor`
**Policy**: `CanViewMarketShares` / `CanManageMarketShares`

**Perfiles con acceso y nivel**:
- `SCRAP`: R (solo sus cuotas)
- `ADMIN`: CRUD

**Instrucciones de filtrado para Copilot**:

```
En el handler GetMarketSharesQuery, implementa:

REGLA 1 — SCRAP:
  - WHERE IdScrap = @LinkedEntityId
  - Filtro automático y obligatorio. Solo lectura.

REGLA 2 — ADMIN:
  - Sin restricción. CRUD completo.
  - Filtros opcionales de UI: IdScrap, Category, AutonomousCommunity, Year.

NOTA: esta pantalla ya está implementada con este patrón. Verificar que
el filtrado está en servidor y que el SCRAP no puede anularlo.

PATRÓN DE IMPLEMENTACIÓN:
  var query = _dbContext.MarketShares.AsQueryable();

  if (currentUser.IsInProfile("SCRAP"))
      query = query.Where(ms => ms.IdScrap == currentUser.LinkedEntityId);

  // Filtros opcionales de UI
  if (request.IdScrap.HasValue)
      query = query.Where(ms => ms.IdScrap == request.IdScrap.Value);
  if (request.Year.HasValue)
      query = query.Where(ms => ms.Year == request.Year.Value);
```

---

## MÓDULO 4 — DECLARACIONES

---

### PROMPT 4.1 — Listado de Declaraciones (`/product-declarations`)

**Archivo Query**: `Application/Features/ProductDeclarations/Queries/GetProductDeclarationsQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/ProductDeclarations/ProductDeclarationList.razor`
**Policy**: `CanViewProductDeclarations`

**Perfiles con acceso y nivel**:
- `PRODUCER`: CRUD-P (solo las suyas)
- `SCRAP`: R-P (solo declaraciones de productores adheridos)
- `COORDINATOR`: R
- `DISPATCH_OFFICE`: R
- `ADMIN`: CRUD

**Instrucciones de filtrado para Copilot**:

```
En el handler GetProductDeclarationsQuery, implementa:

REGLA 1 — PRODUCER:
  - WHERE IdProducer = @LinkedEntityId
  - Filtro automático y obligatorio. El filtro IdProducer en la UI está oculto.
  - Puede crear y editar declaraciones en estado BORRADOR o RECHAZADO.
  - En el formulario, IdProducer se asigna automáticamente con LinkedEntityId.

REGLA 2 — SCRAP:
  - Solo ve declaraciones de productores adheridos a sus acuerdos:
    WHERE IdProducer IN (
      SELECT DISTINCT so.IdIssuedBy FROM ServiceOrders so
      JOIN WasteMoves wm ON wm.ServiceOrderId = so.Id
      WHERE (wm.IdScrap = @LinkedEntityId OR wm.IdScrap2 = @LinkedEntityId)
    )
  - ALTERNATIVA más precisa: derivar productores de los Agreements del SCRAP.
  - Solo lectura.

REGLA 3 — COORDINATOR:
  - Agreements WHERE IdCoordinator = @LinkedEntityId → scrapIds
  - Declaraciones de productores adheridos a esos SCRAPs.
  - Solo lectura.

REGLA 4 — DISPATCH_OFFICE:
  - Sin restricción. Solo lectura dentro del tenant.

REGLA 5 — ADMIN:
  - Sin restricción. CRUD completo.
  - Puede validar y rechazar declaraciones.

PATRÓN DE IMPLEMENTACIÓN:
  var query = _dbContext.ProductDeclarations
      .Where(pd => pd.OwnerId == currentUser.OwnerId);

  if (currentUser.IsInProfile("PRODUCER"))
      query = query.Where(pd => pd.IdProducer == currentUser.LinkedEntityId);
  else if (currentUser.IsInProfile("SCRAP"))
  {
      var producerIds = await _dbContext.WasteMoves
          .Where(wm => (wm.IdScrap == currentUser.LinkedEntityId
                     || wm.IdScrap2 == currentUser.LinkedEntityId)
                    && wm.OwnerId == currentUser.OwnerId)
          .Join(_dbContext.ServiceOrders,
              wm => wm.ServiceOrderId, so => so.Id,
              (wm, so) => so.IdIssuedBy)
          .Distinct().ToListAsync();
      query = query.Where(pd => producerIds.Contains(pd.IdProducer));
  }
  else if (currentUser.IsInProfile("COORDINATOR"))
  {
      var scrapIds = await _dbContext.Agreements
          .Where(a => a.IdCoordinator == currentUser.LinkedEntityId)
          .Select(a => a.IdScrap).Distinct().ToListAsync();
      var producerIds = await _dbContext.WasteMoves
          .Where(wm => scrapIds.Contains(wm.IdScrap) && wm.OwnerId == currentUser.OwnerId)
          .Join(_dbContext.ServiceOrders,
              wm => wm.ServiceOrderId, so => so.Id,
              (wm, so) => so.IdIssuedBy)
          .Distinct().ToListAsync();
      query = query.Where(pd => producerIds.Contains(pd.IdProducer));
  }
  // DISPATCH_OFFICE, ADMIN: sin filtro adicional
```

---

### PROMPT 4.2 — Dashboard de Declaraciones (`/product-declarations/dashboard`)

**Archivo Query**: `Application/Features/ProductDeclarations/Queries/GetDeclarationDashboardQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/ProductDeclarations/DeclarationDashboard.razor`
**Policy**: `CanViewProductDeclarations`

**Perfiles con acceso**: `PRODUCER`, `SCRAP`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
MISMAS REGLAS DE FILTRADO QUE PROMPT 4.1.

Los KPIs se calculan sobre el mismo subset filtrado por perfil:
  - Declaraciones por estado: COUNT agrupado por State
  - Volumen declarado por periodo: SUM(Products.Quantity)
  - Top 10 productos: ranking por Quantity
  - Productores sin declaración: solo para ADMIN y SCRAP
  - Importe total declarado: SUM(Amount)
```

---

### PROMPT 4.3 — Nueva Declaración (`/product-declarations/new` y `/product-declarations/{id}`)

**Archivo Command**: `Application/Features/ProductDeclarations/Commands/CreateProductDeclarationCommand.cs`
**Archivo Blazor**: `Web/Components/Pages/ProductDeclarations/ProductDeclarationForm.razor`
**Policy**: `CanManageProductDeclarations`

**Perfiles con acceso para creación**: `PRODUCER`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el formulario de declaración:

REGLA 1 — PRODUCER:
  - Campo IdProducer: solo lectura, autocompletado con LinkedEntityId.
  - Solo puede crear/editar declaraciones en estado BORRADOR o RECHAZADO.
  - Solo sus propias declaraciones (WHERE IdProducer = LinkedEntityId).

REGLA 2 — ADMIN:
  - Campo IdProducer: selector libre de Entities con EntityRole=Producer.
  - CRUD completo sobre cualquier declaración del tenant.
  - Puede validar y rechazar (transición de estado).

VALIDACIÓN EN SERVIDOR:
  En CreateProductDeclarationCommand y UpdateProductDeclarationCommand:
  - Si el perfil es PRODUCER, forzar IdProducer = LinkedEntityId
    (ignorar cualquier valor enviado desde UI).
  - Validar que el usuario tiene permiso sobre la declaración antes de editar.
```

---

## MÓDULO 5 — SOSTENIBILIDAD

---

### PROMPT 5.1 — Incidencias (`/incidents`)

**Archivo Query**: `Application/Features/Incidents/Queries/GetIncidentsQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Incidents/IncidentList.razor`
**Policy**: `[Authorize]`

**Perfiles con acceso y nivel**:
- `PRODUCER`: C+R (puede abrir incidencias de sus SOs)
- `CARRIER`: C+R (puede abrir incidencias de sus traslados)
- `SCRAP`: C+R (incidencias de sus traslados)
- `PUBLIC_ENT`: C+R (incidencias de sus SOs)
- `CAC_OP`: C+R (incidencias de su CAC)
- `PLANT_OP`: C+R (incidencias de su planta)
- `COORDINATOR`: C+R (incidencias de SCRAPs coordinados)
- `DISPATCH_OFFICE`: CRUD (todo — puede resolver/cerrar)
- `ADMIN`: CRUD (todo)

**Instrucciones de filtrado para Copilot**:

```
En el handler GetIncidentsQuery, implementa:

REGLA 1 — PRODUCER:
  - Incidencias cuya SO fue emitida por su entidad:
    WHERE ServiceOrderId IS NOT NULL
    AND ServiceOrder.IdIssuedBy = @LinkedEntityId
  - Puede crear incidencias vinculando a traslados de sus SOs.

REGLA 2 — CARRIER:
  - Incidencias vinculadas a traslados donde es transportista:
    WHERE WasteMoveId IN (
      SELECT IdWasteMove FROM WasteMoveResidues
      WHERE IdCarrier = @LinkedEntityId
    )
  - O incidencias que el mismo carrier creó:
    WHERE CreatedByUserId = @CurrentUserId

REGLA 3 — SCRAP:
  - Incidencias vinculadas a traslados del SCRAP:
    WHERE WasteMoveId IN (
      SELECT Id FROM WasteMoves
      WHERE (IdScrap = @LinkedEntityId OR IdScrap2 = @LinkedEntityId)
    )

REGLA 4 — PUBLIC_ENT:
  - Incidencias de SOs emitidas por la entidad:
    WHERE ServiceOrderId IN (
      SELECT Id FROM ServiceOrders WHERE IdIssuedBy = @LinkedEntityId
    )
  - O incidencias cuyo traslado recoge en su municipio:
    WHERE WasteMoveId IN (
      SELECT Id FROM WasteMoves WHERE IdSource IN (
        SELECT Id FROM Entities WHERE MunicipalityCode = @municipalityCode
      )
    )

REGLA 5 — CAC_OP:
  - Incidencias vinculadas a entradas/traslados de su CAC.

REGLA 6 — PLANT_OP:
  - Incidencias vinculadas a entradas/tratamientos de su planta:
    WHERE WasteMoveId IN (
      SELECT Id FROM WasteMoves WHERE IdDestination = @LinkedEntityId
    )

REGLA 7 — COORDINATOR:
  - Incidencias de traslados de SCRAPs coordinados.

REGLA 8 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción. CRUD completo.
  - Pueden resolver y cerrar incidencias.

NOTA SOBRE CREACIÓN: cualquier perfil puede abrir una incidencia (C),
pero el campo WasteMoveId en el formulario de apertura se carga como <select>
filtrado por el mismo criterio de visibilidad del perfil. Es decir, un PRODUCER
solo puede vincular incidencias a traslados de sus SOs.
```

---

### PROMPT 5.2 — Zonas DUM (`/dum-zones`)

**Archivo Query**: `Application/Features/DUMZones/Queries/GetDUMZonesQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/DUMZones/DUMZoneList.razor`
**Policy**: `[Authorize]`

**Perfiles con acceso y nivel**:
- `PRODUCER`: R
- `CARRIER`: R
- `SCRAP`: R
- `PUBLIC_ENT`: R
- `COORDINATOR`: R
- `DISPATCH_OFFICE`: R
- `ADMIN`: CRUD

**Instrucciones de filtrado para Copilot**:

```
En el handler GetDUMZonesQuery:

Las zonas DUM y sus reglas de restricción son CATÁLOGO COMPARTIDO.
NO se filtran por perfil ni por OwnerId (son de ámbito global).

Todos los perfiles con acceso ven el mismo listado completo de zonas.
Solo ADMIN puede crear, editar y eliminar zonas y reglas.

Sin filtrado diferenciado por perfil. El control de acceso (quién ve
la pantalla) se gestiona desde PagePermissions.
```

---

### PROMPT 5.3 — Simulador DUM (`/dum-zones/simulator`)

**Instrucciones de filtrado para Copilot**:

```
El simulador DUM es de solo lectura para todos los perfiles.
Opera sobre el catálogo compartido de DUMZones + DUMRestrictionRules.
NO requiere filtrado diferenciado por perfil.

La consulta recibe parámetros del usuario (punto de recogida, tipo vehículo,
horario planificado) y evalúa contra las reglas vigentes.
Devuelve resultado de cumplimiento (sí/no/aviso) y recomendaciones.
```

---

### PROMPT 5.4 — Emisiones (`/emissions`)

**Archivo Query**: `Application/Features/Emissions/Queries/GetEmissionsQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Emissions/EmissionsList.razor`
**Policy**: `[Authorize]`

**Perfiles con acceso y nivel**:
- `PRODUCER`: R-P (emisiones de SUS traslados)
- `CARRIER`: R-P (emisiones de traslados donde es transportista)
- `SCRAP`: R (emisiones de sus traslados)
- `PUBLIC_ENT`: R (emisiones en su territorio)
- `PLANT_OP`: R (emisiones de traslados a su planta)
- `COORDINATOR`: R (emisiones de SCRAPs coordinados)
- `DISPATCH_OFFICE`: R (todo)
- `ADMIN`: CRUD (todo — puede forzar re-cálculo)

**Instrucciones de filtrado para Copilot**:

```
Las emisiones se almacenan en WasteMoveResidues
(campos TransportInfo_TransportCarbonEmissions, TransportInfo_TransportDistance).
El filtrado se aplica sobre WasteMoves y WasteMoveResidues.

REGLA 1 — PRODUCER:
  - WasteMoveResidues de traslados de SOs del productor:
    WHERE IdWasteMove IN (
      SELECT Id FROM WasteMoves WHERE ServiceOrderId IN (
        SELECT Id FROM ServiceOrders WHERE IdIssuedBy = @LinkedEntityId
      )
    )

REGLA 2 — CARRIER:
  - WasteMoveResidues WHERE IdCarrier = @LinkedEntityId

REGLA 3 — SCRAP:
  - WasteMoveResidues de WasteMoves WHERE
    (IdScrap = @LinkedEntityId OR IdScrap2 = @LinkedEntityId)

REGLA 4 — PUBLIC_ENT:
  - Emisiones de traslados en su territorio (mismo patrón que PUBLIC_ENT
    en traslados: filtro por municipio del punto de recogida o SO emitida).

REGLA 5 — PLANT_OP:
  - Emisiones de traslados cuyo destino es su planta:
    WasteMoveResidues de WasteMoves WHERE IdDestination = @LinkedEntityId

REGLA 6 — COORDINATOR:
  - Emisiones de traslados de SCRAPs de sus acuerdos.

REGLA 7 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción. Todo el tenant.

Reutilizar el patrón de filtrado de WasteMoves del PROMPT 2.2 (Traslados)
y aplicarlo a WasteMoveResidues.
```

---

### PROMPT 5.5 — Energía Planta (`/plant-energies`)

**Archivo Query**: `Application/Features/PlantEnergies/Queries/GetPlantEnergiesQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/PlantEnergies/PlantEnergyList.razor`
**Policy**: `[Authorize]`

**Perfiles con acceso y nivel**:
- `SCRAP`: R (energía de plantas que procesan sus residuos)
- `PUBLIC_ENT`: R
- `PLANT_OP`: CRUD-P (solo su planta)
- `COORDINATOR`: R
- `DISPATCH_OFFICE`: R
- `ADMIN`: CRUD

**Instrucciones de filtrado para Copilot**:

```
En el handler GetPlantEnergiesQuery, implementa:

REGLA 1 — PLANT_OP:
  - Solo datos de SU planta:
    WHERE PlantCenterCode = (
      SELECT CenterCode FROM Entities WHERE Id = @LinkedEntityId
    )
  - CRUD completo (declara consumo eléctrico de su planta — Scope 2).

REGLA 2 — SCRAP:
  - Datos de TODAS las plantas que procesan sus residuos:
    WHERE PlantCenterCode IN (
      SELECT e.CenterCode FROM WasteMoves wm
      JOIN Entities e ON e.Id = wm.IdDestination
      WHERE (wm.IdScrap = @LinkedEntityId OR wm.IdScrap2 = @LinkedEntityId)
      AND e.EntityRole = 'Plant'
    )
  - Solo lectura.

REGLA 3 — PUBLIC_ENT:
  - Lectura de energía de plantas en su territorio o vinculadas a sus servicios.

REGLA 4 — COORDINATOR:
  - Energía de plantas destino de traslados de SCRAPs coordinados.

REGLA 5 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción. Todo el tenant.

PATRÓN DE IMPLEMENTACIÓN:
  var query = _dbContext.PlantEnergies.AsQueryable();

  if (currentUser.IsInProfile("PLANT_OP"))
  {
      var centerCode = await _dbContext.Entities
          .Where(e => e.Id == currentUser.LinkedEntityId)
          .Select(e => e.CenterCode).FirstAsync();
      query = query.Where(pe => pe.PlantCenterCode == centerCode);
  }
  else if (currentUser.IsInProfile("SCRAP"))
  {
      var plantCenterCodes = await _dbContext.WasteMoves
          .Where(wm => (wm.IdScrap == currentUser.LinkedEntityId
                     || wm.IdScrap2 == currentUser.LinkedEntityId)
                    && wm.OwnerId == currentUser.OwnerId)
          .Join(_dbContext.Entities, wm => wm.IdDestination, e => e.Id,
              (wm, e) => e.CenterCode)
          .Where(cc => cc != null)
          .Distinct().ToListAsync();
      query = query.Where(pe => plantCenterCodes.Contains(pe.PlantCenterCode));
  }
  // Etc.
```

---

### PROMPT 5.6 — Factores de Emisión (`/emission-factors`)

**Archivo Query**: `Application/Features/EmissionFactors/Queries/GetEmissionFactorSetsQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/EmissionFactors/EmissionFactorList.razor`
**Policy**: `[Authorize]`

**Perfiles con acceso y nivel**:
- `SCRAP`: R
- `PUBLIC_ENT`: R
- `PLANT_OP`: R
- `COORDINATOR`: R
- `DISPATCH_OFFICE`: R
- `ADMIN`: CRUD

**Instrucciones de filtrado para Copilot**:

```
Los factores de emisión (EmissionFactorSets / EmissionFactors) son
CATÁLOGO COMPARTIDO o versionado por tenant.

NO requieren filtrado diferenciado por perfil.
Todos los perfiles con acceso ven el mismo catálogo.
Solo ADMIN puede crear nuevas versiones, activar sets y editar factores.

Sin filtrado diferenciado. Control de acceso vía PagePermissions.
```

---

## RESUMEN — Matriz Rápida de Acceso y Filtrado

| # | Pantalla | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH | ADMIN |
|---|----------|:--------:|:-------:|:-----:|:----------:|:------:|:--------:|:-----------:|:--------:|:-----:|
| 1.0 | Dashboard Home | ✅ SO propias | ✅ Traslados asignados | ✅ IdScrap | ✅ SO propias | ✅ Su CAC | ✅ Su planta | ✅ SCRAPs coordinados | ✅ TODO | ✅ TODO |
| 2.1 | Órdenes Servicio | CRUD-P (IdIssuedBy) | R (asignadas) | R (especial) | CRUD-P (IdIssuedBy) | R (su CAC) | R (destino) | R (SCRAPs) | CRUD | CRUD |
| 2.2 | Traslados | R-P (SO propias) | U-P (IdCarrier) | R (IdScrap) | R (SO propias) | R (destino) | R (destino) | R (SCRAPs) | CRUD | CRUD |
| 2.3 | Entradas Planta | — | — | R (IdScrap) | R (SO propias) | — | CRUD-P (destino) | R (SCRAPs) | R | CRUD |
| 2.4 | Entradas CAC | — | — | R (IdScrap) | R (SO propias) | CRUD-P (su CAC) | — | R (SCRAPs) | R | CRUD |
| 2.5 | Tratamiento | — | — | R (IdScrap) | R (SO propias) | — | CRUD-P (su planta) | R (SCRAPs) | R | CRUD |
| 3.1 | Acuerdos | — | — | C+R (IdScrap) | R (IdPublicEntity) | — | — | R (IdCoordinator) | CRUD | CRUD |
| 3.2 | Liquidaciones | — | — | V (IdScrap) | R (IdPublicEntity) | — | — | R (SCRAPs) | R | CRUD |
| 3.3 | Cuotas | — | — | R (IdScrap) | — | — | — | — | — | CRUD |
| 4.1 | Declaraciones (Lista) | CRUD-P (IdProducer) | — | R-P (adheridos) | — | — | — | R (SCRAPs) | R | CRUD |
| 4.2 | Declaraciones (Dash) | ✅ Propios | — | ✅ Adheridos | — | — | — | ✅ SCRAPs | — | ✅ TODO |
| 4.3 | Nueva Declaración | CRUD-P | — | — | — | — | — | — | — | CRUD |
| 5.1 | Incidencias | C+R (SO propias) | C+R (asignadas) | C+R (IdScrap) | C+R (SO/municipio) | C+R (su CAC) | C+R (su planta) | C+R (SCRAPs) | CRUD | CRUD |
| 5.2 | Zonas DUM | R | R | R | R | — | — | R | R | CRUD |
| 5.3 | Simulador DUM | R | R | R | R | — | — | R | R | CRUD |
| 5.4 | Emisiones | R-P (SO propias) | R-P (IdCarrier) | R (IdScrap) | R (municipio) | — | R (destino) | R (SCRAPs) | R | CRUD |
| 5.5 | Energía Planta | — | — | R (plantas usadas) | R | — | CRUD-P (su planta) | R (SCRAPs) | R | CRUD |
| 5.6 | Factores Emisión | — | — | R | R | — | R | R | R | CRUD |

---

## APÉNDICE — Patrones Reutilizables

### Patrón A: Filtrado SCRAP (WasteMoves)
```csharp
// Reutilizable en cualquier query que filtre WasteMoves por SCRAP
query = query.Where(wm =>
    wm.IdScrap == currentUser.LinkedEntityId
    || wm.IdScrap2 == currentUser.LinkedEntityId);
```

### Patrón B: Filtrado COORDINATOR (vía Agreements)
```csharp
// Reutilizable en cualquier query que necesite los SCRAPs del coordinador
var scrapIds = await _dbContext.Agreements
    .Where(a => a.IdCoordinator == currentUser.LinkedEntityId
             && a.OwnerId == currentUser.OwnerId)
    .Select(a => a.IdScrap)
    .Distinct().ToListAsync();
query = query.Where(wm => scrapIds.Contains(wm.IdScrap));
```

### Patrón C: Filtrado PUBLIC_ENT (por municipio)
```csharp
// Reutilizable cuando se necesita filtrar por territorio de la entidad pública
var municipalityCode = await _dbContext.Entities
    .Where(e => e.Id == currentUser.LinkedEntityId)
    .Select(e => e.MunicipalityCode).FirstAsync();
var entityIdsInMunicipality = await _dbContext.Entities
    .Where(e => e.MunicipalityCode == municipalityCode
             && e.OwnerId == currentUser.OwnerId)
    .Select(e => e.Id).ToListAsync();
var soIds = await _dbContext.ServiceOrders
    .Where(so => so.IdIssuedBy == currentUser.LinkedEntityId)
    .Select(so => so.Id).ToListAsync();
query = query.Where(wm =>
    entityIdsInMunicipality.Contains(wm.IdSource)
    || soIds.Contains(wm.ServiceOrderId));
```

### Patrón D: Filtrado PRODUCER / PUBLIC_ENT (por SOs emitidas)
```csharp
// Reutilizable para cualquier tabla derivada de ServiceOrders
var soIds = await _dbContext.ServiceOrders
    .Where(so => so.IdIssuedBy == currentUser.LinkedEntityId
              && so.OwnerId == currentUser.OwnerId)
    .Select(so => so.Id).ToListAsync();
```

### Patrón E: Filtrado CARRIER (por WasteMoveResidues)
```csharp
// Reutilizable para traslados y derivados del transportista
var wmIds = await _dbContext.WasteMoveResidues
    .Where(wmr => wmr.IdCarrier == currentUser.LinkedEntityId)
    .Select(wmr => wmr.IdWasteMove)
    .Distinct().ToListAsync();
query = query.Where(wm => wmIds.Contains(wm.Id));
```

### Patrón F: Filtrado PLANT_OP (por destino)
```csharp
// Reutilizable para entradas y tratamiento de planta
query = query.Where(wm => wm.IdDestination == currentUser.LinkedEntityId);
```
