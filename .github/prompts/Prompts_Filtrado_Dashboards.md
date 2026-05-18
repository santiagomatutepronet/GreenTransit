# Prompts de Filtrado de Datos por Perfil — Dashboards GreenTransit

> **Propósito**: Cada sección de este documento es un **prompt independiente** para GitHub Copilot.
> Adjuntar siempre `COPILOT_CONTEXT.md`, `README.md` y `Crear_BD_v4_1.sql` al inicio de la sesión de Copilot.
>
> **Reglas transversales** (aplican a TODOS los prompts):
> 1. Filtro multi-tenant: `WHERE OwnerId = @currentUserOwnerId` en TODAS las queries operativas.
> 2. Catálogos compartidos (`LERCodes`, `TreatmentOperations`, tablas geográficas) NO filtran por `OwnerId`.
> 3. El acceso a la pantalla se controla desde `PageDefinitions`/`PagePermissions` (sistema dinámico). Las policies en código son el mínimo de seguridad estático.
> 4. Usar `ICurrentUserService.LinkedEntityId` para obtener la entidad vinculada al usuario.
> 5. Usar `ICurrentUserService.IsInAnyProfile(...)` para detectar el perfil activo.
> 6. Usar `IDataScopeService.ApplyScope()` donde esté disponible.
> 7. Datos geográficos (`ProvinceCode`, `MunicipalityCode`, `AutonomousCommunity`) se resuelven siempre a `Name` (JOIN con tablas de geografía), nunca se muestran como código.
> 8. `ADMIN` y `DISPATCH_OFFICE` ven todos los datos del tenant (solo filtro `OwnerId`), salvo que se indique lo contrario.
> 9. No se crean nuevas entidades de dominio. Todo se implementa con el modelo v4.1.

---

## MÓDULO 1 — DASHBOARDS LOGÍSTICA

---

### PROMPT 1.1 — Optimización SCRAP (`/logistics/optimization`)

**Archivo Query**: `Application/Features/Logistics/Queries/GetLogisticsOptimizationQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Logistics/LogisticsOptimization.razor`
**Policy**: `CanViewLogisticsOptimization`

**Perfiles con acceso**: `SCRAP`, `COORDINATOR`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetLogisticsOptimizationQuery, implementa el siguiente filtrado de datos
según el perfil del usuario autenticado:

REGLA 1 — SCRAP:
  - Filtrar WasteMoves WHERE (IdScrap = @LinkedEntityId OR IdScrap2 = @LinkedEntityId)
  - Todos los widgets (KPIs de ruta, volumen por zona, mapa, cumplimiento DUM,
    heatmap llegadas, incidencias, utilización vehículos) deben partir de este subset
    de traslados.
  - Las Entities del mapa se limitan a las que aparecen como IdSource o IdDestination
    en esos traslados.
  - Las DUMZones se muestran todas (son catálogo compartido), pero el % de cumplimiento
    DUM se calcula solo sobre los traslados del SCRAP.

REGLA 2 — COORDINATOR:
  - Obtener los Agreements WHERE IdCoordinator = @LinkedEntityId.
  - Filtrar WasteMoves WHERE IdScrap IN (SELECT IdScrap FROM Agreements
    WHERE IdCoordinator = @LinkedEntityId).
  - El coordinador ve transversalmente todos los SCRAPs de sus acuerdos.
  - Si hay filtro de UI IdScrap?, aplicarlo como restricción adicional dentro
    del subset del coordinador.

REGLA 3 — ADMIN:
  - Sin restricción adicional. Solo filtro OwnerId (multi-tenant).
  - Los filtros de UI (IdScrap?, ProvinceCode?, etc.) se aplican opcionalmente.

PATRÓN DE IMPLEMENTACIÓN:
  var query = _dbContext.WasteMoves
      .Where(wm => wm.OwnerId == currentUser.OwnerId);

  if (currentUser.IsInProfile("SCRAP"))
      query = query.Where(wm => wm.IdScrap == currentUser.LinkedEntityId
                              || wm.IdScrap2 == currentUser.LinkedEntityId);
  else if (currentUser.IsInProfile("COORDINATOR"))
  {
      var scrapIds = await _dbContext.Agreements
          .Where(a => a.IdCoordinator == currentUser.LinkedEntityId)
          .Select(a => a.IdScrap)
          .Distinct().ToListAsync();
      query = query.Where(wm => scrapIds.Contains(wm.IdScrap));
  }
  // ADMIN: sin filtro adicional

  // Aplicar filtros opcionales de UI
  if (request.IdScrap.HasValue)
      query = query.Where(wm => wm.IdScrap == request.IdScrap.Value);
```

---

### PROMPT 1.2 — Monitorización Pública (`/logistics/public-monitoring`)

**Archivo Query**: `Application/Features/Logistics/Queries/GetPublicMonitoringQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Logistics/PublicMonitoring.razor`
**Policy**: `CanViewPublicMonitoring`

**Perfiles con acceso**: `PUBLIC_ENT`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetPublicMonitoringQuery, implementa el siguiente filtrado:

REGLA 1 — PUBLIC_ENT:
  - Obtener los Agreements WHERE IdPublicEntity = @LinkedEntityId.
  - Filtrar WasteMoves a través de los ServiceOrders vinculados:
    WasteMoves WHERE ServiceOrderId IN (
      SELECT Id FROM ServiceOrders WHERE IdIssuedBy = @LinkedEntityId
    )
  - Liquidaciones: Settlements WHERE IdPublicEntity = @LinkedEntityId.
  - Objetivos municipales: MarketShares filtrados por los SCRAPs que operan
    en el municipio del ayuntamiento (derivado de Agreements.IdScrap
    WHERE Agreements.IdPublicEntity = @LinkedEntityId).
  - Emisiones: SUM(WasteMoveResidues.TransportInfo_TransportCarbonEmissions)
    sobre el subset de traslados filtrado.

  El usuario PUBLIC_ENT NO puede ver traslados de otros municipios ni
  liquidaciones de otros ayuntamientos. El campo IdIssuedBy es el filtro
  principal para ServiceOrders, y IdPublicEntity para Settlements y Agreements.

REGLA 2 — ADMIN:
  - Sin restricción adicional. Solo filtro OwnerId.
  - Puede filtrar por cualquier entidad pública o SCRAP desde la UI.

PATRÓN DE IMPLEMENTACIÓN:
  if (currentUser.IsInProfile("PUBLIC_ENT"))
  {
      var linkedEntityId = currentUser.LinkedEntityId;

      // Traslados: solo los de SOs emitidas por la entidad pública
      var serviceOrderIds = await _dbContext.ServiceOrders
          .Where(so => so.IdIssuedBy == linkedEntityId && so.OwnerId == currentUser.OwnerId)
          .Select(so => so.Id).ToListAsync();

      wasteMoveQuery = wasteMoveQuery.Where(wm => serviceOrderIds.Contains(wm.ServiceOrderId));

      // Liquidaciones
      settlementsQuery = settlementsQuery.Where(s => s.IdPublicEntity == linkedEntityId);

      // Objetivos: SCRAPs del municipio vía agreements
      var scrapIds = await _dbContext.Agreements
          .Where(a => a.IdPublicEntity == linkedEntityId)
          .Select(a => a.IdScrap).Distinct().ToListAsync();
      marketSharesQuery = marketSharesQuery.Where(ms => scrapIds.Contains(ms.IdScrap));
  }
```

---

### PROMPT 1.3 — Panel Operativo (`/logistics/operations`)

**Archivo Query**: `Application/Features/Logistics/Queries/GetOperationalDashboardQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Logistics/OperationalDashboard.razor`
**Policy**: `CanViewOperationalDashboard`

**Perfiles con acceso**: `DISPATCH_OFFICE`, `CAC_OP`, `PLANT_OP`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetOperationalDashboardQuery, implementa un panel multirrol que adapta
tanto los WIDGETS VISIBLES como el FILTRADO DE DATOS al perfil activo:

REGLA 1 — DISPATCH_OFFICE:
  - Ve TODOS los datos del tenant (solo filtro OwnerId).
  - Widgets activos: SO pendientes de planificar, embudo de WasteMoves por estado,
    planificación semanal (próximos 7 días), incidencias abiertas.
  - ServiceOrders: todas del tenant WHERE Status = 'Pending'.
  - WasteMoves: todos del tenant, agrupados por ServiceStatus.

REGLA 2 — CAC_OP:
  - Solo ve datos de SU CAC.
  - Filtrar EntryCACs WHERE la entidad del CAC = @LinkedEntityId.
    (La relación es EntryCACs → la entidad CAC se identifica por el punto
    de destino del traslado vinculado, o directamente por el IdCAC si existe
    como campo en el modelo).
  - Widgets activos: entradas en CAC hoy, stock acumulado por residuo,
    tickets de pesaje pendientes.
  - EntryCACResidues.Weight agrupado por Residues.Name / LERCodes.Code.

REGLA 3 — PLANT_OP:
  - Solo ve datos de SU planta.
  - Filtrar EntryPlants WHERE la entidad planta = @LinkedEntityId.
    (EntryPlants vinculadas a traslados cuyo IdDestination = @LinkedEntityId,
    o si EntryPlants tiene campo directo de planta, usar ese).
  - Filtrar TreatmentPlants de la misma forma.
  - Widgets activos: entradas en planta hoy, balance de tratamiento
    (WeightReused + WeightValued vs WeightRemove), impropios detectados
    (SUM TreatmentPlants.ImproperWeight), incidencias de planta.

REGLA 4 — ADMIN:
  - Ve TODOS los widgets de los tres perfiles anteriores.
  - Sin restricción de datos (solo OwnerId).
  - Puede filtrar por entidad específica desde la UI.

PATRÓN DE IMPLEMENTACIÓN:
  var profile = currentUser.ProfileReference;
  var dto = new OperationalDashboardDto { ActiveProfile = profile };

  switch (profile)
  {
      case "DISPATCH_OFFICE":
          dto.PendingServiceOrders = await GetPendingSOs(currentUser.OwnerId);
          dto.WasteMoveFunnel = await GetFunnel(currentUser.OwnerId);
          dto.WeeklyPlan = await GetWeeklyPlan(currentUser.OwnerId);
          dto.OpenIncidents = await GetOpenIncidents(currentUser.OwnerId);
          break;

      case "CAC_OP":
          dto.CacEntriesToday = await GetCacEntries(currentUser.LinkedEntityId, today);
          dto.CacStockByResidue = await GetCacStock(currentUser.LinkedEntityId);
          dto.CacTicketsPending = await GetPendingTickets(currentUser.LinkedEntityId);
          break;

      case "PLANT_OP":
          dto.PlantEntriesToday = await GetPlantEntries(currentUser.LinkedEntityId, today);
          dto.TreatmentBalance = await GetTreatmentBalance(currentUser.LinkedEntityId);
          dto.ImproperWeightKg = await GetImproperWeight(currentUser.LinkedEntityId);
          dto.PlantOpenIncidents = await GetPlantIncidents(currentUser.LinkedEntityId);
          break;

      case "ADMIN":
          // Calcula TODOS los widgets, sin filtro de entidad
          // (salvo filtro opcional de UI request.EntityId)
          break;
  }
```

---

## MÓDULO 2 — MOVILIDAD URBANA

---

### PROMPT 2.1 — Análisis Coordinador (`/mobility/coordinator-analysis`)

**Archivo Query**: `Application/Features/Mobility/Queries/GetMobilityCoordinatorAnalysisQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Mobility/CoordinatorAnalysis.razor`
**Policy**: `CanViewMobilityCoordinatorAnalysis`

**Perfiles con acceso**: `COORDINATOR`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetMobilityCoordinatorAnalysisQuery, implementa:

REGLA 1 — COORDINATOR:
  - Obtener Agreements WHERE IdCoordinator = @LinkedEntityId.
  - Extraer los IdScrap de esos acuerdos.
  - Filtrar WasteMoves WHERE IdScrap IN (scrapIds del coordinador).
  - Todos los widgets (mapa densidad, heatmap 7x24, índice de conflicto,
    recomendaciones, % hora pico, % fuera de DUM) operan sobre este subset.
  - El coordinador ve datos de TODOS los municipios donde operan sus SCRAPs,
    no solo de un municipio específico.
  - Filtros opcionales de UI: IdScrap (dentro de sus SCRAPs), MunicipalityCode,
    ProvinceCode.

REGLA 2 — ADMIN:
  - Sin restricción. Solo OwnerId.
  - Puede filtrar por cualquier coordinador, SCRAP o municipio.

PATRÓN:
  if (currentUser.IsInProfile("COORDINATOR"))
  {
      var scrapIds = await _dbContext.Agreements
          .Where(a => a.IdCoordinator == currentUser.LinkedEntityId)
          .Select(a => a.IdScrap).Distinct().ToListAsync();
      query = query.Where(wm => scrapIds.Contains(wm.IdScrap));
  }
```

---

### PROMPT 2.2 — Monitorización Municipio (`/mobility/municipal-monitoring`)

**Archivo Query**: `Application/Features/Mobility/Queries/GetMobilityMunicipalMonitoringQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Mobility/MunicipalMonitoring.razor`
**Policy**: `CanViewMobilityMunicipalMonitoring`

**Perfiles con acceso**: `PUBLIC_ENT`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetMobilityMunicipalMonitoringQuery, implementa:

REGLA 1 — PUBLIC_ENT:
  - Filtrar traslados cuyo punto de recogida pertenece al municipio
    del ayuntamiento:
    WasteMoves WHERE IdSource IN (
      SELECT Id FROM Entities
      WHERE MunicipalityCode = (
        SELECT MunicipalityCode FROM Entities WHERE Id = @LinkedEntityId
      )
    )
    O BIEN traslados cuya SO fue emitida por la entidad:
    WasteMoves WHERE ServiceOrderId IN (
      SELECT Id FROM ServiceOrders WHERE IdIssuedBy = @LinkedEntityId
    )
  - Los widgets de impacto en movilidad (% hora pico, conflictos DUM,
    distribución temporal) se calculan solo sobre este subset.
  - Incidencias: filtrar por las que afectan a recogidas en su municipio.

REGLA 2 — ADMIN:
  - Sin restricción. Solo OwnerId.

PATRÓN:
  if (currentUser.IsInProfile("PUBLIC_ENT"))
  {
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
  }
```

---

### PROMPT 2.3 — Datos Oficina Asignación (`/mobility/dispatch-data`)

**Archivo Query**: `Application/Features/Mobility/Queries/GetMobilityDispatchDataQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Mobility/DispatchData.razor`
**Policy**: `CanViewMobilityDispatchData`

**Perfiles con acceso**: `DISPATCH_OFFICE`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetMobilityDispatchDataQuery, implementa:

REGLA 1 — DISPATCH_OFFICE:
  - Ve TODOS los datos del tenant (solo filtro OwnerId).
  - Este dashboard provee datasets exportables para análisis externo
    (Living Lab, estudios de movilidad).
  - El panel de datos exportable incluye: fecha recogida, municipio (nombre),
    provincia (nombre), SCRAP, tipo vehículo, peso, distancia, duración,
    emisiones CO2, código zona DUM, cumplimiento DUM (sí/no), en hora pico (sí/no).
  - Resumen operativo por SCRAP: agrupa todos los traslados por IdScrap.
  - Planificación semanal: ServiceOrders de los próximos 7 días con indicadores
    de conflicto de movilidad.

REGLA 2 — ADMIN:
  - Idéntico a DISPATCH_OFFICE en este dashboard. Sin restricción.

NOTA: Ambos perfiles tienen visión completa del tenant. La diferencia
es que ADMIN puede además gestionar usuarios/perfiles (fuera de este dashboard).

PATRÓN:
  // Sin filtro por LinkedEntityId — solo OwnerId
  var query = _dbContext.WasteMoves
      .Where(wm => wm.OwnerId == currentUser.OwnerId);

  // Aplicar filtros opcionales de UI
  if (request.IdScrap.HasValue)
      query = query.Where(wm => wm.IdScrap == request.IdScrap);
  if (!string.IsNullOrEmpty(request.ProvinceCode))
      query = query.Where(wm => wm.Source.ProvinceCode == request.ProvinceCode);
```

---

## MÓDULO 3 — TRATAMIENTO Y RECICLAJE

> **NOTA**: Estos 4 dashboards NO están documentados en el Mapa de Funcionalidades actual.
> Se diseñan aquí basándose en el modelo de datos v4.1, las tablas `TreatmentPlants`,
> `TreatmentPlantResidues`, `EntryPlants`, `EntryPlantResidues`, `TreatmentOperations`,
> y la lógica de perfiles existente.

---

### PROMPT 3.1 — Análisis Revalorización SCRAP (`/reporting/treatment/scrap-revaluation`)

**Archivo Query**: `Application/Features/Reporting/Treatment/Queries/GetScrapRevaluationAnalysisQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/Treatment/ScrapRevaluationAnalysis.razor`
**Policy**: `CanViewScrapRevaluationAnalysis` (nueva — registrar en `PolicyConstants.cs`)
**Perfiles mínimos de la policy**: `SCRAP`, `COORDINATOR`, `ADMIN`

**Perfiles con acceso**: `SCRAP`, `COORDINATOR`, `ADMIN`

**Propósito**: Vista analítica para que cada SCRAP evalúe los resultados de tratamiento
de los residuos recogidos bajo su responsabilidad: tasas de reciclaje, valorización,
reutilización, rechazo e impropios por planta y por tipo de residuo.

**Instrucciones de filtrado para Copilot**:

```
Crea el handler GetScrapRevaluationAnalysisQuery con los siguientes filtros de UI:
Year, Month?, Quarter?, IdPlant? (Entities WHERE EntityRole='Plant'), LERCode?,
WasteStream?, Category?.

FUENTES DE DATOS PRINCIPALES:
  - TreatmentPlants + TreatmentPlantResidues (resultados de tratamiento)
  - TreatmentOperations (catálogo R/D: IsRecycling, IsEnergyRecovery)
  - EntryPlants + EntryPlantResidues (peso de entrada en planta)
  - WasteMoves (para vincular traslados al SCRAP)

REGLA 1 — SCRAP:
  - Filtrar TreatmentPlants a través de sus traslados vinculados:
    TreatmentPlants WHERE IdWasteMove IN (
      SELECT Id FROM WasteMoves
      WHERE (IdScrap = @LinkedEntityId OR IdScrap2 = @LinkedEntityId)
    )
  - Calcular:
    · Tasa reciclaje = SUM(WeightReused WHERE TreatmentOperations.IsRecycling=1
      + WeightValued WHERE TreatmentOperations.IsRecycling=1) / SUM(WeightTotal)
    · Tasa valorización = SUM(WeightValued) / SUM(WeightTotal)
    · Tasa reutilización = SUM(WeightReused WHERE IsPreparationForReuse=1) / SUM(WeightTotal)
    · % impropios = SUM(ImproperWeight) / SUM(entrada neta)
    · % rechazo = SUM(WeightRemove) / SUM(WeightTotal)
  - Desglose por planta: agrupar por IdDestination de WasteMoves → Entities.Name
  - Desglose por tipo residuo: agrupar por LERCodes.Code / Residues.Name
  - Evolución temporal: serie mensual/trimestral de tasas

REGLA 2 — COORDINATOR:
  - Obtener SCRAPs de sus acuerdos: Agreements WHERE IdCoordinator = @LinkedEntityId.
  - Filtrar TreatmentPlants cuyos traslados pertenecen a esos SCRAPs.
  - Vista comparativa entre SCRAPs bajo su coordinación.

REGLA 3 — ADMIN:
  - Sin restricción dentro del tenant.
  - Puede filtrar por cualquier SCRAP, planta o categoría.

PATRÓN:
  var wasteMovesQuery = _dbContext.WasteMoves
      .Where(wm => wm.OwnerId == currentUser.OwnerId);

  if (currentUser.IsInProfile("SCRAP"))
      wasteMovesQuery = wasteMovesQuery.Where(wm =>
          wm.IdScrap == currentUser.LinkedEntityId
          || wm.IdScrap2 == currentUser.LinkedEntityId);
  else if (currentUser.IsInProfile("COORDINATOR"))
  {
      var scrapIds = await _dbContext.Agreements
          .Where(a => a.IdCoordinator == currentUser.LinkedEntityId)
          .Select(a => a.IdScrap).Distinct().ToListAsync();
      wasteMovesQuery = wasteMovesQuery.Where(wm => scrapIds.Contains(wm.IdScrap));
  }

  var treatmentQuery = _dbContext.TreatmentPlants
      .Where(tp => wasteMovesQuery.Select(wm => wm.Id).Contains(tp.IdWasteMove));
```

---

### PROMPT 3.2 — Monitorización Reciclaje Municipal (`/reporting/treatment/municipal-recycling`)

**Archivo Query**: `Application/Features/Reporting/Treatment/Queries/GetMunicipalRecyclingMonitoringQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/Treatment/MunicipalRecyclingMonitoring.razor`
**Policy**: `CanViewMunicipalRecyclingMonitoring` (nueva)
**Perfiles mínimos de la policy**: `PUBLIC_ENT`, `DISPATCH_OFFICE`, `ADMIN`

**Perfiles con acceso**: `PUBLIC_ENT`, `DISPATCH_OFFICE`, `ADMIN`

**Propósito**: Las entidades públicas monitorizan las tasas de reciclaje y tratamiento
de los residuos recogidos en su municipio, independientemente del SCRAP que los gestione.

**Instrucciones de filtrado para Copilot**:

```
Crea el handler GetMunicipalRecyclingMonitoringQuery con filtros de UI:
Year, Month?, IdScrap? (SCRAPs que operan en su municipio), LERCode?, WasteStream?.

REGLA 1 — PUBLIC_ENT:
  - Determinar el MunicipalityCode de la entidad pública:
    SELECT MunicipalityCode FROM Entities WHERE Id = @LinkedEntityId
  - Filtrar los traslados cuyo punto de recogida pertenece a ese municipio:
    WasteMoves WHERE IdSource IN (
      SELECT Id FROM Entities WHERE MunicipalityCode = @municipalityCode
    )
    O cuya SO fue emitida por la entidad:
    WasteMoves WHERE ServiceOrderId IN (
      SELECT Id FROM ServiceOrders WHERE IdIssuedBy = @LinkedEntityId
    )
  - Luego vincular a TreatmentPlants/TreatmentPlantResidues a través de
    esos traslados para obtener resultados de tratamiento.
  - Widgets:
    · Cards KPI: toneladas recogidas, toneladas recicladas, tasa reciclaje,
      tasa valorización, % impropios
    · Desglose por SCRAP: tabla con tasas por cada SCRAP operando en el municipio
    · Evolución mensual: serie de tasas de reciclaje
    · Comparativa objetivos: MarketShares del municipio vs real

REGLA 2 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción dentro del tenant.
  - Puede filtrar por cualquier municipio.

PATRÓN:
  if (currentUser.IsInProfile("PUBLIC_ENT"))
  {
      var municipalityCode = await _dbContext.Entities
          .Where(e => e.Id == currentUser.LinkedEntityId)
          .Select(e => e.MunicipalityCode).FirstAsync();

      var sourceIds = await _dbContext.Entities
          .Where(e => e.MunicipalityCode == municipalityCode
                   && e.OwnerId == currentUser.OwnerId)
          .Select(e => e.Id).ToListAsync();

      var soIds = await _dbContext.ServiceOrders
          .Where(so => so.IdIssuedBy == currentUser.LinkedEntityId)
          .Select(so => so.Id).ToListAsync();

      wasteMoveIds = await _dbContext.WasteMoves
          .Where(wm => wm.OwnerId == currentUser.OwnerId
                     && (sourceIds.Contains(wm.IdSource) || soIds.Contains(wm.ServiceOrderId)))
          .Select(wm => wm.Id).ToListAsync();

      treatmentQuery = treatmentQuery.Where(tp => wasteMoveIds.Contains(tp.IdWasteMove));
  }
```

---

### PROMPT 3.3 — Validación Multi-SCRAP (`/reporting/treatment/multi-scrap-validation`)

**Archivo Query**: `Application/Features/Reporting/Treatment/Queries/GetMultiScrapTreatmentValidationQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/Treatment/MultiScrapTreatmentValidation.razor`
**Policy**: `CanViewMultiScrapTreatmentValidation` (nueva)
**Perfiles mínimos de la policy**: `COORDINATOR`, `DISPATCH_OFFICE`, `ADMIN`

**Perfiles con acceso**: `COORDINATOR`, `DISPATCH_OFFICE`, `ADMIN`

**Propósito**: Vista comparativa multi-SCRAP de resultados de tratamiento para
validación cruzada. Permite al coordinador o a la oficina de asignación verificar
la coherencia de las tasas declaradas por cada SCRAP y detectar anomalías.

**Instrucciones de filtrado para Copilot**:

```
Crea el handler GetMultiScrapTreatmentValidationQuery con filtros de UI:
Year, Quarter?, AutonomousCommunity?, Category?, FlowType?.

REGLA 1 — COORDINATOR:
  - Obtener SCRAPs de sus acuerdos: Agreements WHERE IdCoordinator = @LinkedEntityId.
  - Para cada SCRAP, agregar:
    · Toneladas entrada (EntryPlantResidues.Weight vía WasteMoves del SCRAP)
    · Toneladas recicladas (TreatmentPlantResidues con operaciones IsRecycling=1)
    · Toneladas valorizadas
    · Toneladas rechazo
    · Tasa reciclaje, valorización, reutilización
    · % impropios
  - Presentar como tabla comparativa SCRAP vs SCRAP.
  - Destacar desviaciones: si la tasa de un SCRAP difiere >X% de la media,
    marcar con semáforo.
  - El coordinador NO puede ver SCRAPs fuera de sus acuerdos.

REGLA 2 — DISPATCH_OFFICE / ADMIN:
  - Ve TODOS los SCRAPs del tenant.
  - Sin restricción. Visión completa para auditoría.

PATRÓN:
  List<Guid> scrapIds;
  if (currentUser.IsInProfile("COORDINATOR"))
  {
      scrapIds = await _dbContext.Agreements
          .Where(a => a.IdCoordinator == currentUser.LinkedEntityId
                   && a.OwnerId == currentUser.OwnerId)
          .Select(a => a.IdScrap).Distinct().ToListAsync();
  }
  else // DISPATCH_OFFICE, ADMIN
  {
      scrapIds = await _dbContext.Entities
          .Where(e => e.EntityRole == "SCRAP" && e.OwnerId == currentUser.OwnerId)
          .Select(e => e.Id).ToListAsync();
  }

  // Para cada SCRAP, agregar datos de tratamiento
  foreach (var scrapId in scrapIds)
  {
      var wasteMovesForScrap = _dbContext.WasteMoves
          .Where(wm => wm.IdScrap == scrapId && wm.OwnerId == currentUser.OwnerId);
      // Agregar TreatmentPlantResidues...
  }
```

---

### PROMPT 3.4 — Datos Operativos TR (`/reporting/treatment/operational-data`)

**Archivo Query**: `Application/Features/Reporting/Treatment/Queries/GetTreatmentOperationalDataQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/Treatment/TreatmentOperationalData.razor`
**Policy**: `CanViewTreatmentOperationalData` (nueva)
**Perfiles mínimos de la policy**: `PLANT_OP`, `SCRAP`, `DISPATCH_OFFICE`, `ADMIN`

**Perfiles con acceso**: `PLANT_OP`, `SCRAP`, `DISPATCH_OFFICE`, `ADMIN`

**Propósito**: Panel operativo centrado en los datos de tratamiento de planta:
entradas diarias, balance de masas, operaciones R/D aplicadas, incidencias de
balance, y eficiencia por tipo de residuo.

**Instrucciones de filtrado para Copilot**:

```
Crea el handler GetTreatmentOperationalDataQuery con filtros de UI:
Year, Month?, IdPlant? (solo visible para ADMIN/DISPATCH_OFFICE),
LERCode?, TreatmentOperationCode?.

REGLA 1 — PLANT_OP:
  - Solo ve datos de SU planta.
  - Filtrar EntryPlants y TreatmentPlants vinculados a traslados cuyo
    IdDestination = @LinkedEntityId.
  - O si el modelo vincula directamente por planta: filtrar por la entidad planta.
  - Widgets:
    · Entradas hoy: EntryPlants WHERE PlantEntryDate = hoy
    · Balance de masas: TreatmentPlantResidues agrupados por operación R/D
      (TreatmentOperations.Code, .Description)
    · Eficiencia por residuo: tasa reciclaje por LERCode/Residue
    · Incidencias de balance abiertas: Incidents vinculadas a tratamientos
      donde el balance de masas falló
    · Histórico semanal/mensual: evolución de entradas y tratamiento

REGLA 2 — SCRAP:
  - Ve datos de tratamiento de los traslados donde es SCRAP responsable:
    TreatmentPlants WHERE IdWasteMove IN (
      SELECT Id FROM WasteMoves
      WHERE IdScrap = @LinkedEntityId OR IdScrap2 = @LinkedEntityId
    )
  - Ve datos de TODAS las plantas que procesan sus residuos (vista multi-planta).
  - No puede ver tratamientos de otros SCRAPs.

REGLA 3 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción dentro del tenant.
  - Puede seleccionar planta específica desde la UI.

PATRÓN:
  if (currentUser.IsInProfile("PLANT_OP"))
  {
      // Solo su planta
      var plantEntityId = currentUser.LinkedEntityId;
      var wasteMovesAtPlant = _dbContext.WasteMoves
          .Where(wm => wm.IdDestination == plantEntityId
                    && wm.OwnerId == currentUser.OwnerId);
      treatmentQuery = treatmentQuery
          .Where(tp => wasteMovesAtPlant.Select(wm => wm.Id).Contains(tp.IdWasteMove));
  }
  else if (currentUser.IsInProfile("SCRAP"))
  {
      var scrapMoves = _dbContext.WasteMoves
          .Where(wm => (wm.IdScrap == currentUser.LinkedEntityId
                     || wm.IdScrap2 == currentUser.LinkedEntityId)
                    && wm.OwnerId == currentUser.OwnerId);
      treatmentQuery = treatmentQuery
          .Where(tp => scrapMoves.Select(wm => wm.Id).Contains(tp.IdWasteMove));
  }
  // DISPATCH_OFFICE / ADMIN: sin filtro adicional, opcionalmente por IdPlant de UI
```

---

## MÓDULO 4 — ECOMODULACIÓN

> **NOTA**: Estos 3 dashboards NO están documentados en el Mapa de Funcionalidades actual.
> Se diseñan basándose en las tablas `EcoModulationRuleSets`, `EcoModulationRules`,
> `Settlements` (campo AdjustmentsAmount), `ProductDeclaration`, `Products`,
> `Residues` (ResidueType=Product/ProductSpec), y las entidades existentes.

---

### PROMPT 4.1 — Panel SCRAP de Ecomodulación (`/reporting/ecomodulation/scrap-panel`)

**Archivo Query**: `Application/Features/Reporting/EcoModulation/Queries/GetScrapEcoModulationPanelQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/EcoModulation/ScrapEcoModulationPanel.razor`
**Policy**: `CanViewScrapEcoModulationPanel` (nueva — registrar en `PolicyConstants.cs`)
**Perfiles mínimos de la policy**: `SCRAP`, `DISPATCH_OFFICE`, `ADMIN`

**Perfiles con acceso**: `SCRAP`, `DISPATCH_OFFICE`, `ADMIN`

**Propósito**: Cada SCRAP analiza el impacto económico de la ecomodulación en sus
liquidaciones: qué reglas se están aplicando, qué productores se benefician o
penalizan, y cómo afecta al AdjustmentsAmount de los Settlements.

**Instrucciones de filtrado para Copilot**:

```
Crea el handler GetScrapEcoModulationPanelQuery con filtros de UI:
Year, Quarter?, Month?, Category?, ProducerId?.

FUENTES DE DATOS:
  - EcoModulationRuleSets + EcoModulationRules (reglas vigentes y sus criterios)
  - Settlements (AdjustmentsAmount = impacto eco-modulación)
  - SettlementLines (desglose por categoría/LER)
  - ProductDeclaration + Products (declaraciones de productores adheridos)
  - Residues WHERE ResidueType IN ('Product', 'ProductSpec') (fichas técnicas
    con datos de ecodiseño: reciclabilidad, composición, peligrosidad)

REGLA 1 — SCRAP:
  - Filtrar Settlements WHERE IdScrap = @LinkedEntityId.
  - Mostrar AdjustmentsAmount como total y desglose por Settlement/periodo.
  - Vincular con ProductDeclarations de productores adheridos al SCRAP:
    ProductDeclaration WHERE IdEntity IN (
      SELECT DISTINCT so.IdIssuedBy FROM ServiceOrders so
      JOIN WasteMoves wm ON wm.ServiceOrderId = so.Id
      WHERE wm.IdScrap = @LinkedEntityId
    )
  - Listar reglas de EcoModulationRules vigentes (catálogo compartido)
    y calcular qué productores cumplen/incumplen cada criterio.
  - Widgets:
    · Impacto económico total (suma AdjustmentsAmount)
    · Desglose por periodo (bar chart)
    · Top productores con mayor bonificación/penalización
    · Reglas activas y % de cumplimiento

REGLA 2 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción dentro del tenant.
  - Puede ver todos los SCRAPs y sus liquidaciones.
  - Filtro opcional IdScrap en UI.

PATRÓN:
  if (currentUser.IsInProfile("SCRAP"))
  {
      settlementsQuery = settlementsQuery
          .Where(s => s.IdScrap == currentUser.LinkedEntityId);
      // Productores adheridos: derivar de traslados del SCRAP
  }
```

---

### PROMPT 4.2 — Vista Regulatoria de Ecomodulación (`/reporting/ecomodulation/regulatory-view`)

**Archivo Query**: `Application/Features/Reporting/EcoModulation/Queries/GetEcoModulationRegulatoryViewQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/EcoModulation/EcoModulationRegulatoryView.razor`
**Policy**: `CanViewEcoModulationRegulatoryView` (nueva)
**Perfiles mínimos de la policy**: `COORDINATOR`, `PUBLIC_ENT`, `DISPATCH_OFFICE`, `ADMIN`

**Perfiles con acceso**: `COORDINATOR`, `PUBLIC_ENT`, `DISPATCH_OFFICE`, `ADMIN`

**Propósito**: Vista orientada a supervisión regulatoria: cómo las reglas de
ecomodulación están afectando al ecosistema, qué SCRAPs aplican más/menos
ajustes, y la coherencia entre las declaraciones de producto y los criterios
de ecomodulación vigentes.

**Instrucciones de filtrado para Copilot**:

```
Crea el handler GetEcoModulationRegulatoryViewQuery con filtros de UI:
Year, IdScrap?, AutonomousCommunity?, Category?.

FUENTES DE DATOS:
  - EcoModulationRuleSets + EcoModulationRules (catálogo compartido, sin OwnerId)
  - Settlements + SettlementLines (impacto económico)
  - Agreements (vínculo SCRAP-coordinador-entidad pública)

REGLA 1 — COORDINATOR:
  - Ve SCRAPs de sus acuerdos: Agreements WHERE IdCoordinator = @LinkedEntityId.
  - Filtrar Settlements WHERE IdScrap IN (scrapIds del coordinador).
  - Vista comparativa de impacto eco-modulación por SCRAP.

REGLA 2 — PUBLIC_ENT:
  - Ve liquidaciones de sus acuerdos:
    Settlements WHERE IdPublicEntity = @LinkedEntityId.
  - Analiza cómo la eco-modulación afecta las compensaciones que recibe.

REGLA 3 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción dentro del tenant.
  - Vista consolidada de todo el ecosistema.

PATRÓN:
  if (currentUser.IsInProfile("COORDINATOR"))
  {
      var scrapIds = await GetScrapIdsForCoordinator(currentUser.LinkedEntityId);
      settlementsQuery = settlementsQuery.Where(s => scrapIds.Contains(s.IdScrap));
  }
  else if (currentUser.IsInProfile("PUBLIC_ENT"))
  {
      settlementsQuery = settlementsQuery
          .Where(s => s.IdPublicEntity == currentUser.LinkedEntityId);
  }
  // EcoModulationRuleSets: catálogo compartido, se muestra siempre completo.
```

---

### PROMPT 4.3 — Preparación DPP (`/reporting/ecomodulation/dpp-preparation`)

**Archivo Query**: `Application/Features/Reporting/EcoModulation/Queries/GetDPPPreparationQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/EcoModulation/DPPPreparation.razor`
**Policy**: `CanViewDPPPreparation` (nueva)
**Perfiles mínimos de la policy**: `PRODUCER`, `SCRAP`, `ADMIN`

**Perfiles con acceso**: `PRODUCER`, `SCRAP`, `ADMIN`

**Propósito**: Dashboard orientado a la preparación del Pasaporte Digital de Producto
(DPP, regulación EU). Muestra el estado de completitud de las fichas técnicas
de producto (ProductSpecs), los criterios de ecomodulación que aplican a cada
producto, y las carencias de información que impiden generar un DPP completo.

**Instrucciones de filtrado para Copilot**:

```
Crea el handler GetDPPPreparationQuery con filtros de UI:
Year?, Category?, ProductReference?.

FUENTES DE DATOS:
  - Residues WHERE ResidueType IN ('Product', 'ProductSpec')
  - ProductDeclaration + Products (declaraciones del productor)
  - EcoModulationRules (criterios que evalúan campos de la ficha técnica)

REGLA 1 — PRODUCER:
  - Solo ve SUS productos y declaraciones:
    Residues WHERE IdProducer = @LinkedEntityId
    ProductDeclaration WHERE IdEntity = @LinkedEntityId
  - Para cada producto/ficha técnica, evaluar:
    · % completitud de campos requeridos para DPP (composición, reciclabilidad,
      peligrosidad, peso, materiales)
    · Qué reglas de ecomodulación se aplican según los atributos del producto
    · Campos faltantes que impiden cumplir criterios DPP
  - Widgets:
    · Resumen: nº productos declarados, % con ficha completa, % DPP-ready
    · Tabla de productos con semáforo de completitud
    · Detalle por producto: campos faltantes, criterios eco-modulación aplicables
    · Evolución de completitud por periodo de declaración

REGLA 2 — SCRAP:
  - Ve productos de los productores adheridos a su sistema:
    Residues WHERE IdProducer IN (
      SELECT DISTINCT so.IdIssuedBy FROM ServiceOrders so
      JOIN WasteMoves wm ON wm.ServiceOrderId = so.Id
      WHERE wm.IdScrap = @LinkedEntityId
    )
  - Vista agregada: qué % de productores adheridos tienen fichas DPP-ready.
  - No puede editar fichas (solo lectura analítica).

REGLA 3 — ADMIN:
  - Sin restricción dentro del tenant.
  - Ve todos los productos y declaraciones.

PATRÓN:
  if (currentUser.IsInProfile("PRODUCER"))
  {
      residuesQuery = residuesQuery
          .Where(r => r.IdProducer == currentUser.LinkedEntityId);
      declarationsQuery = declarationsQuery
          .Where(pd => pd.IdEntity == currentUser.LinkedEntityId);
  }
  else if (currentUser.IsInProfile("SCRAP"))
  {
      // Productores adheridos derivados de traslados del SCRAP
      var producerIds = await _dbContext.ServiceOrders
          .Join(_dbContext.WasteMoves,
              so => so.Id, wm => wm.ServiceOrderId,
              (so, wm) => new { so.IdIssuedBy, wm.IdScrap, wm.IdScrap2 })
          .Where(x => x.IdScrap == currentUser.LinkedEntityId
                   || x.IdScrap2 == currentUser.LinkedEntityId)
          .Select(x => x.IdIssuedBy)
          .Distinct().ToListAsync();

      residuesQuery = residuesQuery.Where(r => producerIds.Contains(r.IdProducer));
      declarationsQuery = declarationsQuery.Where(pd => producerIds.Contains(pd.IdEntity));
  }
```

---

## MÓDULO 5 — MAPAS DE CALOR

---

### PROMPT 5.1 — Densidad de Residuos (`/reporting/heat-maps/waste-density`)

**Archivo Query**: `Application/Features/Reporting/HeatMaps/Queries/GetWasteDensityHeatMapQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/HeatMaps/WasteDensityHeatMap.razor`
**Policy**: `CanViewHeatMapWasteDensity`

**Perfiles con acceso**: `SCRAP`, `DISPATCH_OFFICE`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetWasteDensityHeatMapQuery, implementa:

REGLA 1 — SCRAP:
  - Filtrar WasteMoves WHERE (IdScrap = @LinkedEntityId OR IdScrap2 = @LinkedEntityId).
  - Los puntos del heatmap se derivan de WasteMoves.IdSource → Entities.Latitude/Longitude.
  - Solo se muestran puntos de recogida que han tenido traslados del SCRAP.
  - Peso por punto: SUM(WasteMoveResidues.Weight) para los traslados filtrados.
  - Frecuencia: COUNT(WasteMoves) por punto de recogida.
  - Desglose por LER/WasteStream sobre el subset filtrado.

REGLA 2 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción. Ve todos los puntos de recogida del tenant.

PATRÓN:
  var query = _dbContext.WasteMoves.Where(wm => wm.OwnerId == currentUser.OwnerId);
  if (currentUser.IsInProfile("SCRAP"))
      query = query.Where(wm => wm.IdScrap == currentUser.LinkedEntityId
                              || wm.IdScrap2 == currentUser.LinkedEntityId);
```

---

### PROMPT 5.2 — Patrones y Estacionalidad (`/reporting/heat-maps/pattern-analysis`)

**Archivo Query**: `Application/Features/Reporting/HeatMaps/Queries/GetWastePatternAnalysisQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/HeatMaps/WastePatternAnalysis.razor`
**Policy**: `CanViewHeatMapPatternAnalysis`

**Perfiles con acceso**: `SCRAP`, `DISPATCH_OFFICE`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetWastePatternAnalysisQuery, implementa:

MISMAS REGLAS DE FILTRADO QUE PROMPT 5.1 (Densidad de Residuos).

El filtrado de datos es idéntico: SCRAP ve solo sus traslados, DISPATCH_OFFICE
y ADMIN ven todo el tenant. La diferencia es en los WIDGETS:

  - Análisis temporal: distribución de recogidas por mes/trimestre/año,
    identificación de patrones estacionales.
  - Heatmap temporal: día de semana × mes, coloreado por volumen.
  - Tendencia por tipología: evolución de la composición (% por LERCode/WasteStream)
    a lo largo del tiempo.
  - Detección de anomalías: puntos de recogida con variaciones >X% respecto
    a su media histórica.

Reutilizar el patrón de filtrado del PROMPT 5.1 exactamente.
```

---

### PROMPT 5.3 — Vista Entidad Pública (`/reporting/heat-maps/public-view`)

**Archivo Query**: `Application/Features/Reporting/HeatMaps/Queries/GetPublicEntityHeatMapQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/HeatMaps/PublicEntityHeatMapView.razor`
**Policy**: `CanViewHeatMapPublicView`

**Perfiles con acceso**: `PUBLIC_ENT`, `DISPATCH_OFFICE`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetPublicEntityHeatMapQuery, implementa:

REGLA 1 — PUBLIC_ENT:
  - Obtener MunicipalityCode de la entidad pública:
    SELECT MunicipalityCode FROM Entities WHERE Id = @LinkedEntityId
  - Filtrar recogidas cuyo punto de recogida (ServiceOrders.IdPickupPoint → Entities)
    pertenece al mismo municipio:
    WasteMoves WHERE IdSource IN (
      SELECT Id FROM Entities WHERE MunicipalityCode = @municipalityCode
    )
    O cuya SO fue emitida por la entidad:
    WasteMoves WHERE ServiceOrderId IN (
      SELECT Id FROM ServiceOrders WHERE IdIssuedBy = @LinkedEntityId
    )
  - El mapa de calor solo muestra puntos dentro del ámbito territorial
    del ayuntamiento.
  - Alertas: semáforo si un punto supera umbral de acumulación en zona sensible.

REGLA 2 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción. Ve todos los municipios del tenant.

PATRÓN:
  if (currentUser.IsInProfile("PUBLIC_ENT"))
  {
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
  }
```

---

## MÓDULO 6 — HUELLA DE CARBONO

---

### PROMPT 6.1 — Visión Consolidada (`/reporting/carbon-footprint/overview`)

**Archivo Query**: `Application/Features/Reporting/CarbonFootprint/Queries/GetCarbonFootprintOverviewQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/CarbonFootprint/CarbonFootprintOverview.razor`
**Policy**: `CanViewCarbonFootprintOverview`

**Perfiles con acceso**: `SCRAP`, `COORDINATOR`, `DISPATCH_OFFICE`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetCarbonFootprintOverviewQuery, implementa:

REGLA 1 — SCRAP:
  - Scope 1 (transporte): WasteMoveResidues donde el WasteMove tiene
    IdScrap = @LinkedEntityId OR IdScrap2 = @LinkedEntityId.
  - Scope 2 (energía plantas): PlantEnergies de las plantas que procesan
    residuos del SCRAP (derivar de WasteMoves.IdDestination).
  - Consolidación: suma Scope 1 + Scope 2 filtrados.

REGLA 2 — COORDINATOR:
  - Agreements WHERE IdCoordinator = @LinkedEntityId → extraer scrapIds.
  - Scope 1: WasteMoves WHERE IdScrap IN scrapIds.
  - Scope 2: PlantEnergies de las plantas destino de esos traslados.
  - Vista comparativa por SCRAP.

REGLA 3 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción dentro del tenant.

PATRÓN:
  if (currentUser.IsInProfile("SCRAP"))
  {
      wasteMovesQuery = wasteMovesQuery.Where(wm =>
          wm.IdScrap == currentUser.LinkedEntityId
          || wm.IdScrap2 == currentUser.LinkedEntityId);
      // PlantEnergies: filtrar por plantas destino de esos traslados
      var plantIds = await wasteMovesQuery
          .Select(wm => wm.IdDestination).Distinct().ToListAsync();
      plantEnergiesQuery = plantEnergiesQuery
          .Where(pe => plantIds.Any(pid =>
              pe.PlantCenterCode == _dbContext.Entities
                  .Where(e => e.Id == pid).Select(e => e.CenterCode).FirstOrDefault()));
  }
  else if (currentUser.IsInProfile("COORDINATOR"))
  {
      var scrapIds = await GetScrapIdsForCoordinator(currentUser.LinkedEntityId);
      wasteMovesQuery = wasteMovesQuery.Where(wm => scrapIds.Contains(wm.IdScrap));
  }
```

---

### PROMPT 6.2 — Emisiones Transporte (`/reporting/carbon-footprint/transport-emissions`)

**Archivo Query**: `Application/Features/Reporting/CarbonFootprint/Queries/GetTransportEmissionsAnalysisQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/CarbonFootprint/TransportEmissionsAnalysis.razor`
**Policy**: `CanViewTransportEmissionsAnalysis`

**Perfiles con acceso**: `SCRAP`, `CARRIER`, `COORDINATOR`, `DISPATCH_OFFICE`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetTransportEmissionsAnalysisQuery, implementa:

REGLA 1 — SCRAP:
  - WasteMoveResidues a través de WasteMoves WHERE
    IdScrap = @LinkedEntityId OR IdScrap2 = @LinkedEntityId.
  - Todos los campos de emisión: TransportInfo_TransportCarbonEmissions,
    TransportInfo_TransportDistance, VehicleType, FuelType, EuroClass.

REGLA 2 — CARRIER:
  - WasteMoveResidues WHERE IdCarrier = @LinkedEntityId.
  - El transportista solo ve las emisiones de los traslados donde fue asignado.
  - Vista centrada en SUS vehículos y combustibles.

REGLA 3 — COORDINATOR:
  - SCRAPs de sus acuerdos → WasteMoves de esos SCRAPs.
  - Vista comparativa de emisiones por SCRAP y por transportista.

REGLA 4 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción dentro del tenant.

PATRÓN:
  if (currentUser.IsInProfile("SCRAP"))
      wasteMovesQuery = wasteMovesQuery.Where(wm =>
          wm.IdScrap == currentUser.LinkedEntityId
          || wm.IdScrap2 == currentUser.LinkedEntityId);
  else if (currentUser.IsInProfile("CARRIER"))
      wasteMovesQuery = wasteMovesQuery.Where(wm =>
          wm.WasteMoveResidues.Any(wmr =>
              wmr.IdCarrier == currentUser.LinkedEntityId));
  else if (currentUser.IsInProfile("COORDINATOR"))
  {
      var scrapIds = await GetScrapIdsForCoordinator(currentUser.LinkedEntityId);
      wasteMovesQuery = wasteMovesQuery.Where(wm => scrapIds.Contains(wm.IdScrap));
  }
```

---

### PROMPT 6.3 — Huella Energética Plantas (`/reporting/carbon-footprint/plant-energy`)

**Archivo Query**: `Application/Features/Reporting/CarbonFootprint/Queries/GetPlantEnergyFootprintQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/CarbonFootprint/PlantEnergyFootprint.razor`
**Policy**: `CanViewPlantEnergyFootprint`

**Perfiles con acceso**: `PLANT_OP`, `SCRAP`, `DISPATCH_OFFICE`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetPlantEnergyFootprintQuery, implementa:

REGLA 1 — PLANT_OP:
  - Solo ve datos de SU planta.
  - PlantEnergies WHERE PlantCenterCode = (
      SELECT CenterCode FROM Entities WHERE Id = @LinkedEntityId
    )
  - EntryPlants vinculadas a traslados cuyo IdDestination = @LinkedEntityId.
  - TreatmentPlants vinculadas a esos mismos traslados.
  - Scope 2: KwhTotal × factor de conversión (configurable en appsettings).

REGLA 2 — SCRAP:
  - Ve PlantEnergies de TODAS las plantas que procesan sus residuos.
  - Derivar plantas desde WasteMoves WHERE IdScrap = @LinkedEntityId
    → IdDestination → Entities → CenterCode → PlantEnergies.
  - Vista comparativa entre plantas.

REGLA 3 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción. Ve todas las plantas del tenant.

PATRÓN:
  if (currentUser.IsInProfile("PLANT_OP"))
  {
      var centerCode = await _dbContext.Entities
          .Where(e => e.Id == currentUser.LinkedEntityId)
          .Select(e => e.CenterCode).FirstAsync();
      plantEnergiesQuery = plantEnergiesQuery
          .Where(pe => pe.PlantCenterCode == centerCode);
  }
  else if (currentUser.IsInProfile("SCRAP"))
  {
      var plantCenterCodes = await _dbContext.WasteMoves
          .Where(wm => (wm.IdScrap == currentUser.LinkedEntityId
                     || wm.IdScrap2 == currentUser.LinkedEntityId)
                    && wm.OwnerId == currentUser.OwnerId)
          .Join(_dbContext.Entities, wm => wm.IdDestination, e => e.Id,
              (wm, e) => e.CenterCode)
          .Distinct().ToListAsync();
      plantEnergiesQuery = plantEnergiesQuery
          .Where(pe => plantCenterCodes.Contains(pe.PlantCenterCode));
  }
```

---

### PROMPT 6.4 — Reporte Productor (`/reporting/carbon-footprint/producer-report`)

**Archivo Query**: `Application/Features/Reporting/CarbonFootprint/Queries/GetProducerCarbonReportQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/CarbonFootprint/ProducerCarbonReport.razor`
**Policy**: `CanViewProducerCarbonReport`

**Perfiles con acceso**: `PRODUCER`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetProducerCarbonReportQuery, implementa:

REGLA 1 — PRODUCER:
  - Solo ve la huella de carbono de SUS residuos.
  - Filtrar ServiceOrders WHERE IdIssuedBy = @LinkedEntityId.
  - Vincular a WasteMoves → WasteMoveResidues para obtener
    TransportInfo_TransportCarbonEmissions.
  - Widgets:
    · Emisiones totales generadas por el transporte de sus residuos
    · Intensidad: kgCO2e por tonelada de residuo generado
    · Desglose por tipo de residuo (LERCode)
    · Evolución mensual
    · Comparativa con la media del tenant (opcional, si la policy lo permite)

REGLA 2 — ADMIN:
  - Sin restricción. Puede filtrar por cualquier productor.

PATRÓN:
  if (currentUser.IsInProfile("PRODUCER"))
  {
      var soIds = await _dbContext.ServiceOrders
          .Where(so => so.IdIssuedBy == currentUser.LinkedEntityId
                    && so.OwnerId == currentUser.OwnerId)
          .Select(so => so.Id).ToListAsync();
      wasteMovesQuery = wasteMovesQuery
          .Where(wm => soIds.Contains(wm.ServiceOrderId));
  }
```

---

### PROMPT 6.5 — Vista Entidad Pública (`/reporting/carbon-footprint/public-view`)

**Archivo Query**: `Application/Features/Reporting/CarbonFootprint/Queries/GetPublicEntityCarbonViewQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/CarbonFootprint/PublicEntityCarbonView.razor`
**Policy**: `CanViewPublicEntityCarbonView`

**Perfiles con acceso**: `PUBLIC_ENT`, `DISPATCH_OFFICE`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetPublicEntityCarbonViewQuery, implementa:

REGLA 1 — PUBLIC_ENT:
  - Filtrar traslados cuyo punto de recogida pertenece a su municipio
    O cuya SO fue emitida por su entidad:
    (MISMO PATRÓN QUE PROMPT 5.3 — Vista Entidad Pública de Mapas de Calor)
  - Emisiones: SUM(WasteMoveResidues.TransportInfo_TransportCarbonEmissions)
    sobre el subset filtrado.
  - Desglose por SCRAP: qué SCRAP genera más emisiones en su municipio.

REGLA 2 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción dentro del tenant.

PATRÓN: idéntico al PROMPT 5.3 para la parte de filtrado de traslados.
```

---

## MÓDULO 7 — ANÁLISIS CUMPLIMIENTO

---

### PROMPT 7.1 — Panel SCRAP CN-A (`/reporting/regulatory-compliance/scrap-overview`)

**Archivo Query**: `Application/Features/Reporting/RegulatoryCompliance/Queries/GetScrapComplianceOverviewQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/RegulatoryCompliance/ScrapComplianceOverview.razor`
**Policy**: `CanViewScrapComplianceOverview`

**Perfiles con acceso**: `SCRAP`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetScrapComplianceOverviewQuery, implementa:

REGLA 1 — SCRAP:
  - TODO el dashboard se centra en el SCRAP logueado.
  - Filtros principales:
    · WasteMoves WHERE IdScrap = @LinkedEntityId OR IdScrap2 = @LinkedEntityId
    · MarketShares WHERE IdScrap = @LinkedEntityId
    · Agreements WHERE IdScrap = @LinkedEntityId
    · Settlements WHERE IdScrap = @LinkedEntityId
  - TreatmentPlantResidues: a través de WasteMoves del SCRAP.
  - Tasas de reciclaje/valorización/reutilización calculadas sobre
    TreatmentPlantResidues + TreatmentOperations filtrados.
  - Semáforo vs RegulatoryTargets (catálogo compartido o por OwnerId).
  - Alertas: generadas por ComplianceMonitoringService en backend.

REGLA 2 — ADMIN:
  - Sin restricción. Puede seleccionar cualquier SCRAP desde filtro UI IdScrap.

PATRÓN:
  if (currentUser.IsInProfile("SCRAP"))
  {
      var linkedEntityId = currentUser.LinkedEntityId;
      wasteMovesQuery = wasteMovesQuery.Where(wm =>
          wm.IdScrap == linkedEntityId || wm.IdScrap2 == linkedEntityId);
      marketSharesQuery = marketSharesQuery.Where(ms => ms.IdScrap == linkedEntityId);
      agreementsQuery = agreementsQuery.Where(a => a.IdScrap == linkedEntityId);
      settlementsQuery = settlementsQuery.Where(s => s.IdScrap == linkedEntityId);
  }
```

---

### PROMPT 7.2 — Auditoría Cuotas CN-B (`/reporting/regulatory-compliance/market-share-audit`)

**Archivo Query**: `Application/Features/Reporting/RegulatoryCompliance/Queries/GetMarketShareAuditQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/RegulatoryCompliance/MarketShareAudit.razor`
**Policy**: `CanViewMarketShareAudit`

**Perfiles con acceso**: `COORDINATOR`, `DISPATCH_OFFICE`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetMarketShareAuditQuery, implementa:

REGLA 1 — COORDINATOR:
  - Agreements WHERE IdCoordinator = @LinkedEntityId → scrapIds.
  - MarketShares WHERE IdScrap IN scrapIds.
  - EntryPlantResidues vinculados a WasteMoves de esos SCRAPs
    (para calcular el real vs objetivo).
  - Vista: tabla de proporcionalidad (cuota declarada vs real) por SCRAP,
    categoría y comunidad autónoma.
  - Verificación del principio de proporcionalidad entre SCRAPs.

REGLA 2 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción dentro del tenant. Ve todos los SCRAPs.

PATRÓN:
  if (currentUser.IsInProfile("COORDINATOR"))
  {
      var scrapIds = await GetScrapIdsForCoordinator(currentUser.LinkedEntityId);
      marketSharesQuery = marketSharesQuery.Where(ms => scrapIds.Contains(ms.IdScrap));
  }
```

---

### PROMPT 7.3 — Convenios CN-C (`/reporting/regulatory-compliance/agreement-monitoring`)

**Archivo Query**: `Application/Features/Reporting/RegulatoryCompliance/Queries/GetAgreementComplianceMonitoringQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/RegulatoryCompliance/AgreementComplianceMonitoring.razor`
**Policy**: `CanViewAgreementComplianceMonitoring`

**Perfiles con acceso**: `COORDINATOR`, `DISPATCH_OFFICE`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetAgreementComplianceMonitoringQuery, implementa:

REGLA 1 — COORDINATOR:
  - Agreements WHERE IdCoordinator = @LinkedEntityId.
  - Para cada acuerdo: estado, vigencia, SCRAPs vinculados, entidades públicas,
    servicios prestados (WasteMoves vinculados), liquidaciones (Settlements).
  - Alertas: acuerdos próximos a expirar, acuerdos sin actividad reciente,
    acuerdos con liquidaciones pendientes.

REGLA 2 — DISPATCH_OFFICE / ADMIN:
  - Sin restricción. Ve todos los acuerdos del tenant.

PATRÓN:
  if (currentUser.IsInProfile("COORDINATOR"))
  {
      agreementsQuery = agreementsQuery
          .Where(a => a.IdCoordinator == currentUser.LinkedEntityId);
  }
```

---

### PROMPT 7.4 — Entidad Pública CN-D (`/reporting/regulatory-compliance/public-view`)

**Archivo Query**: `Application/Features/Reporting/RegulatoryCompliance/Queries/GetPublicEntityComplianceViewQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/RegulatoryCompliance/PublicEntityComplianceView.razor`
**Policy**: `CanViewPublicEntityComplianceView`

**Perfiles con acceso**: `PUBLIC_ENT`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetPublicEntityComplianceViewQuery, implementa:

REGLA 1 — PUBLIC_ENT:
  - Agreements WHERE IdPublicEntity = @LinkedEntityId.
  - Settlements WHERE IdPublicEntity = @LinkedEntityId.
  - MarketShares filtrados por SCRAPs que operan en su territorio
    (derivado de Agreements.IdScrap donde IdPublicEntity = @LinkedEntityId).
  - EntryPlantResidues para calcular cumplimiento real vs objetivo.
  - Incidencias: Incidents vinculadas a WasteMoves de SOs emitidas
    por la entidad o en su municipio.
  - Widgets:
    · Servicios recibidos por SCRAP (toneladas, nº servicios)
    · Cumplimiento por SCRAP en territorio (tabla + semáforo)
    · Liquidaciones de compensación
    · Incidencias y reclamaciones

REGLA 2 — ADMIN:
  - Sin restricción. Puede filtrar por cualquier entidad pública.

PATRÓN:
  if (currentUser.IsInProfile("PUBLIC_ENT"))
  {
      agreementsQuery = agreementsQuery
          .Where(a => a.IdPublicEntity == currentUser.LinkedEntityId);
      settlementsQuery = settlementsQuery
          .Where(s => s.IdPublicEntity == currentUser.LinkedEntityId);
      var scrapIds = await agreementsQuery.Select(a => a.IdScrap).Distinct().ToListAsync();
      marketSharesQuery = marketSharesQuery.Where(ms => scrapIds.Contains(ms.IdScrap));
  }
```

---

### PROMPT 7.5 — Oficina Asignación CN-E (`/reporting/regulatory-compliance/dispatch-data`)

**Archivo Query**: `Application/Features/Reporting/RegulatoryCompliance/Queries/GetDispatchOfficeComplianceDataQuery.cs`
**Archivo Blazor**: `Web/Components/Pages/Reporting/RegulatoryCompliance/DispatchOfficeComplianceData.razor`
**Policy**: `CanViewDispatchOfficeComplianceData`

**Perfiles con acceso**: `DISPATCH_OFFICE`, `ADMIN`

**Instrucciones de filtrado para Copilot**:

```
En el handler GetDispatchOfficeComplianceDataQuery, implementa:

REGLA 1 — DISPATCH_OFFICE:
  - Ve TODOS los datos del tenant (solo filtro OwnerId).
  - Este dashboard consolida el cumplimiento normativo del ecosistema completo.
  - Widgets:
    · Dashboard ejecutivo: tasas globales, nº SCRAPs, nº convenios, importe liquidado
    · Ranking SCRAPs por cumplimiento
    · Tabla exportable para auditorías externas (AENOR Confía)
    · Evolución interanual
    · Mapa calor geográfico de cumplimiento (CCAA × SCRAP)
    · Resumen cambios normativos (RegulatoryTargets, EmissionFactorSets,
      EcoModulationRuleSets)

REGLA 2 — ADMIN:
  - Idéntico a DISPATCH_OFFICE en este dashboard.

PATRÓN:
  // Sin filtro por LinkedEntityId — solo OwnerId
  var query = _dbContext.WasteMoves
      .Where(wm => wm.OwnerId == currentUser.OwnerId);
  // Todos los widgets agregan sobre el dataset completo del tenant
  // Filtros opcionales de UI: IdScrap?, AutonomousCommunity?, Category?
```

---

## RESUMEN — Matriz Rápida de Acceso

| # | Dashboard | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH | ADMIN |
|---|-----------|:--------:|:-------:|:-----:|:----------:|:------:|:--------:|:-----------:|:--------:|:-----:|
| 1.1 | Optimización SCRAP | — | — | ✅ | — | — | — | ✅ | — | ✅ |
| 1.2 | Monitorización Pública | — | — | — | ✅ | — | — | — | — | ✅ |
| 1.3 | Panel Operativo | — | — | — | — | ✅ | ✅ | — | ✅ | ✅ |
| 2.1 | Análisis Coordinador | — | — | — | — | — | — | ✅ | — | ✅ |
| 2.2 | Monitorización Municipio | — | — | — | ✅ | — | — | — | — | ✅ |
| 2.3 | Datos Oficina Asignación | — | — | — | — | — | — | — | ✅ | ✅ |
| 3.1 | Revalorización SCRAP | — | — | ✅ | — | — | — | ✅ | — | ✅ |
| 3.2 | Reciclaje Municipal | — | — | — | ✅ | — | — | — | ✅ | ✅ |
| 3.3 | Validación Multi-SCRAP | — | — | — | — | — | — | ✅ | ✅ | ✅ |
| 3.4 | Datos Operativos TR | — | — | ✅ | — | — | ✅ | — | ✅ | ✅ |
| 4.1 | Ecomod. Panel SCRAP | — | — | ✅ | — | — | — | — | ✅ | ✅ |
| 4.2 | Ecomod. Vista Regulatoria | — | — | — | ✅ | — | — | ✅ | ✅ | ✅ |
| 4.3 | Ecomod. Preparación DPP | ✅ | — | ✅ | — | — | — | — | — | ✅ |
| 5.1 | MC Densidad | — | — | ✅ | — | — | — | — | ✅ | ✅ |
| 5.2 | MC Patrones | — | — | ✅ | — | — | — | — | ✅ | ✅ |
| 5.3 | MC Vista Ent. Pública | — | — | — | ✅ | — | — | — | ✅ | ✅ |
| 6.1 | HC Visión Consolidada | — | — | ✅ | — | — | — | ✅ | ✅ | ✅ |
| 6.2 | HC Emisiones Transporte | — | ✅ | ✅ | — | — | — | ✅ | ✅ | ✅ |
| 6.3 | HC Huella Plantas | — | — | ✅ | — | — | ✅ | — | ✅ | ✅ |
| 6.4 | HC Reporte Productor | ✅ | — | — | — | — | — | — | — | ✅ |
| 6.5 | HC Vista Ent. Pública | — | — | — | ✅ | — | — | — | ✅ | ✅ |
| 7.1 | CN-A Panel SCRAP | — | — | ✅ | — | — | — | — | — | ✅ |
| 7.2 | CN-B Auditoría Cuotas | — | — | — | — | — | — | ✅ | ✅ | ✅ |
| 7.3 | CN-C Convenios | — | — | — | — | — | — | ✅ | ✅ | ✅ |
| 7.4 | CN-D Ent. Pública | — | — | — | ✅ | — | — | — | — | ✅ |
| 7.5 | CN-E Oficina Asignación | — | — | — | — | — | — | — | ✅ | ✅ |

