# 🤖 Prompts GitHub Copilot — GreenTransit
> **Generado**: 24/04/2026 | **Modelo de datos**: v4.1 | **Stack**: .NET 10 · Blazor · EF Core · MediatR · FluentValidation · Serilog · xUnit
>
> ### Instrucciones de uso
> 1. Adjunta siempre `COPILOT_CONTEXT.md` + `Mapa_Funcionalidades_GreenTransit.md` al inicio de cada sesión nueva de Copilot Chat.
> 2. Ejecuta los prompts **en el orden del índice**. El Bloque A es requisito previo para todo lo demás.
> 3. Si Copilot necesita más contexto, adjunta el archivo `.cs` de la entidad de dominio mencionada en el prompt.
> 4. Marca cada prompt como ✅ en la tabla de estado al completarlo.

---

## 📋 ÍNDICE Y ESTADO

| ID | Bloque | Descripción | Prioridad | Estado |
|---|---|---|:-:|:-:|
| A-1 | Infraestructura | Repositorio genérico + Unit of Work | 🔴 | ✅ |
| A-2 | Infraestructura | Filtro multi-tenant global en EF Core | 🔴 | ✅ |
| A-3 | Infraestructura | FluentValidation + MediatR Pipeline Behaviours | 🔴 | ✅ |
| A-4 | Infraestructura | Layout Blazor: Sidebar + Topbar + NavMenu | 🔴 | ✅ |
| B-1 | Maestros | Gestión de Entidades (Ecosistema): CQRS + UI | 🟠 | ✅ |
| B-2 | Maestros | Catálogos: LER + Residuos + Operaciones R/D | 🟠 | ✅ |
| B-3 | Maestros | Catálogos Geográficos: selectores en cascada | 🟠 | ✅ |
| C-1 | Economía | Formalización de Acuerdos (Agreements) | 🟡 | ⬜ |
| C-2 | Economía | Liquidación Económica (Settlements) | 🟡 | ⬜ |
| C-3 | Economía | Objetivos y Cuotas de Mercado (MarketShares) | 🟡 | ⬜ |
| D-1 | Operaciones | Órdenes de Servicio (ServiceOrders): CQRS + UI | 🔴 | ✅ |
| D-2 | Operaciones | Traslados (WasteMoves): creación — estado SOLICITADO | 🔴 | ✅ |
| D-3 | Operaciones | Planificación Logística — estado PLANIFICADO + DUM | 🔴 | ✅ |
| D-4 | Operaciones | Ejecución de Recogida — estado RECOGIDO + emisiones | 🔴 | ✅ |
| D-5 | Operaciones | Entrada en CAC — estado EN CAC | 🟠 | ✅ |
| D-6 | Operaciones | Entrada y Pesaje en Planta — estado EN PLANTA | 🔴 | ✅ |
| D-7 | Operaciones | Clasificación y Tratamiento Final — estado CLASIFICADO | 🔴 | ✅ |
| D-8 | Operaciones | Vista 360º del Traslado | 🟠 | ✅ |
| E-1 | Sostenibilidad | Gestión de Incidencias | 🟠 | ✅ |
| E-2 | Sostenibilidad | Zonas DUM: editor visual y simulador | 🟡 | ✅ |
| E-3 | Sostenibilidad | Energía de Planta + Factores de Emisión | 🟡 | ✅ |
| F-1 | Reporting | Dashboard operativo principal | 🟠 | ✅ |
| F-2 | Reporting | Trazabilidad end-to-end + buscador global | 🟠 | ✅ |
| F-3 | Reporting | KPIs regulatorios y exportación XLSX | 🟡 | ✅ |
| G-1 | Seguridad | Gestión de Usuarios, Perfiles y SharePoint | 🟠 | ⬜ |
| H-1 | Calidad | Tests de integración y cobertura | 🟡 | ⬜ |
| H-2 | Calidad | Notificaciones en tiempo real con SignalR | 🟡 | ⬜ |
| H-3 | Calidad | Seed de datos iniciales + configuración producción | 🟡 | ⬜ |

---

## 🔧 BLOQUE A — Infraestructura transversal

---

### ⬜ A-1 — Repositorio genérico + Unit of Work

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`

**Prompt**:

Implementa el patrón Repositorio genérico y Unit of Work en el proyecto GreenTransit siguiendo Clean Architecture con .NET 10 y EF Core.

Necesito lo siguiente:

**1. Interfaz `IRepository<T>`** en `GreenTransit.Application/Common/Interfaces/`:
- `GetByIdAsync(Guid id, CancellationToken ct)`
- `GetAllAsync(CancellationToken ct)`
- `FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct)`
- `AddAsync(T entity, CancellationToken ct)`
- `Update(T entity)`
- `Remove(T entity)`
- `ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct)`

**2. Interfaz `IUnitOfWork`** en `GreenTransit.Application/Common/Interfaces/`:
- `SaveChangesAsync(CancellationToken ct)`
- Propiedades de repositorios tipados: `IRepository<ServiceOrder>`, `IRepository<WasteMove>`, `IRepository<Agreement>`, `IRepository<Settlement>`, `IRepository<Incident>`

**3. Implementación `EfRepository<T>`** en `GreenTransit.Infrastructure/Persistence/Repositories/`:
- Usa `AppDbContext` inyectado
- Aplica filtro automático por `OwnerId` si la entidad implementa `ITenantEntity` (ya en `GreenTransit.Domain/Interfaces/ITenantEntity.cs`)
- Inyecta `ICurrentUserService` (ya en `GreenTransit.Application/Common/Interfaces/ICurrentUserService.cs`) para obtener el tenant

**4. Implementación `UnitOfWork`** en `GreenTransit.Infrastructure/Persistence/` que envuelva `AppDbContext`

**5.** Registra ambas implementaciones en `Program.cs` con `AddScoped`

Restricciones: no romper los tests existentes en `GreenTransit.Tests`. Usa el namespace `GreenTransit.Infrastructure.Persistence.Repositories`.

---

### ⬜ A-2 — Filtro multi-tenant global en EF Core

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `src/GreenTransit.Infrastructure/Persistence/AppDbContext.cs`, `src/GreenTransit.Domain/Interfaces/ITenantEntity.cs`

**Prompt**:

Implementa el filtrado multi-tenant automático en EF Core para el proyecto GreenTransit.

Necesito lo siguiente:

**1.** En `AppDbContext`, añade `HasQueryFilter` global para todas las entidades que implementen `ITenantEntity`, filtrando por `OwnerId == _currentOwnerId`. El `AppDbContext` debe recibir `ICurrentUserService` vía constructor y usar `ICurrentUserService.OwnerId` como valor del filtro.

**2.** Añade un método `IgnoreTenantFilter()` en el contexto para consultas administrativas que necesiten ver todos los tenants.

**3.** Actualiza el registro del `AppDbContext` en `Program.cs` para que `ICurrentUserService` sea resuelto correctamente desde el contenedor DI.

**4.** Añade un test en `GreenTransit.Tests` que verifique que una query sobre una entidad con `ITenantEntity` solo devuelve registros del tenant del usuario autenticado. Usa `TestDbContextFactory` y `FakeCurrentUserService` ya existentes.

Restricciones: no modifiques las entidades de dominio ya generadas. El `ICurrentUserService` ya expone `OwnerId`, úsalo directamente.

---

### ⬜ A-3 — FluentValidation + MediatR Pipeline Behaviours

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `src/GreenTransit.Application/Features/ServiceOrders/Commands/CreateServiceOrderCommand.cs`, `src/GreenTransit.Web/Program.cs`

**Prompt**:

Configura FluentValidation integrado con MediatR en el proyecto GreenTransit (.NET 10).

Necesito lo siguiente:

**1.** Verifica que están instalados los paquetes NuGet `FluentValidation.DependencyInjectionExtensions` y el soporte de pipeline behavior de MediatR. Añádelos al proyecto si faltan.

**2. Crea `ValidationBehavior<TRequest, TResponse>`** en `GreenTransit.Application/Common/Behaviours/`:
- Implementa `IPipelineBehavior<TRequest, TResponse>`
- Ejecuta todos los `IValidator<TRequest>` registrados en DI
- Si hay errores de validación, lanza `ValidationException` de FluentValidation (no continúa el pipeline)

**3. Crea `LoggingBehavior<TRequest, TResponse>`** en el mismo directorio:
- Implementa `IPipelineBehavior<TRequest, TResponse>`
- Loguea entrada y salida de cada comando/query con Serilog vía `ILogger<>`
- Mide y loguea el tiempo de ejecución con `Stopwatch`

**4.** En `Program.cs`, registra ambos behaviours en el pipeline de MediatR en el orden correcto: primero `LoggingBehavior`, luego `ValidationBehavior`.

**5.** Registra automáticamente todos los validators del assembly `GreenTransit.Application` con `AddValidatorsFromAssembly`.

Restricciones: sigue el estilo de código del archivo `CreateServiceOrderCommand.cs` (records sellados, handlers sellados, pattern MediatR existente).

---

### ⬜ A-4 — Layout Blazor: Sidebar + Topbar + NavMenu

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `src/GreenTransit.Web/Components/Routes.razor`, `src/GreenTransit.Web/Program.cs`, `src/GreenTransit.Web/Services/CurrentUserService.cs`

**Prompt**:

Crea el layout principal de la aplicación Blazor Web App de GreenTransit (.NET 10).

Necesito lo siguiente:

**1. `MainLayout.razor`** en `GreenTransit.Web/Components/Layout/`:
- Sidebar colapsable a la izquierda con grupos de navegación:
  - 🏠 Inicio → `/`
  - 📚 Configuración → Entidades `/entities`, LER `/ler-codes`, Residuos `/residues`, Operaciones R/D `/treatment-operations`
  - 🚛 Operaciones → Órdenes de Servicio `/service-orders`, Traslados `/waste-moves`, Entradas Planta `/entry-plants`, Entradas CAC `/entry-cacs`, Tratamiento `/treatment-plants`
  - 💶 Economía → Acuerdos `/agreements`, Liquidaciones `/settlements`, Cuotas `/market-shares`
  - 🌱 Sostenibilidad → Incidencias `/incidents`, Zonas DUM `/dum-zones`, Emisiones `/emissions`, Energía Planta `/plant-energies`
  - 📈 Reporting → Trazabilidad `/traceability`, KPIs `/kpis`, Documentos `/documents`
  - 👥 Seguridad → Usuarios `/users`, Perfiles `/profiles`
- Topbar con: logo "GreenTransit", nombre del usuario autenticado desde `ICurrentUserService.UserName`, selector de tenant visible solo si el perfil es `ADMIN`, botón de logout, toggle modo oscuro/claro
- Botón hamburguesa para colapsar/expandir el sidebar con transición CSS suave

**2. `NavMenu.razor`** en `GreenTransit.Web/Components/Layout/` que renderice los grupos con `NavLink` de Blazor y Bootstrap Icons.

**3. `layout.css`** con los estilos del sidebar colapsable (no inline styles).

**4.** Usa `AuthorizeView` de Blazor para mostrar/ocultar secciones de navegación según el rol del usuario. Roles del sistema: `ADMIN`, `SCRAP`, `PRODUCER`, `CARRIER`, `PLANT_OP`, `CAC_OP`, `PUBLIC_ENT`, `COORDINATOR`.

**5.** Integra `MainLayout` como layout por defecto en `Routes.razor`.

Restricciones: Blazor Web App .NET 10 con renderizado interactivo Server. Respeta la estructura de carpetas existente en `GreenTransit.Web/Components/`. Usa siempre `ICurrentUserService` para datos del usuario autenticado, nunca accedas a `HttpContext` directamente.

---

## 📚 BLOQUE B — Módulo de Configuración y Maestros

---

### ⬜ B-1 — Gestión de Entidades del Ecosistema

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`, `src/GreenTransit.Domain/Entities/BusinessEntity.cs`, `src/GreenTransit.Domain/Entities/Security.cs`

**Prompt**:

Implementa el módulo completo de Gestión de Entidades del Ecosistema en GreenTransit. La entidad de dominio es `BusinessEntity` (tabla `Entities`), ya creada en el dominio.

**CAPA APPLICATION — `GreenTransit.Application/Features/Entities/`**

Queries:

**1. `GetEntitiesQuery`**: lista paginada. Parámetros: `EntityRole?`, `ProvinceCode?`, `IsActive?`, `SearchTerm?` (busca en `Name`, `NationalId`, `CenterCode`), `PageNumber`, `PageSize`. Filtra siempre por `OwnerId` del usuario autenticado. Devuelve `PaginatedResult<EntityDto>` con campos: `Id`, `Name`, `NationalId`, `CenterCode`, `EntityRole`, `ProvinceCode`, `IsActive`, `LinkedUserLogin`.

**2. `GetEntityByIdQuery`**: devuelve `EntityDetailDto` con todos los campos de la entidad más `LinkedUserId` y `LinkedUserLogin` del usuario vinculado si existe.

Commands:

**3. `CreateEntityCommand`**: crea una `BusinessEntity`. Campos requeridos según el mapa sección 1.1: identificación, clasificación, localización, contacto y control. Tras crear la entidad, si su `EntityRole` tiene perfil mapeado (ver tabla del mapa), crea automáticamente un registro en `Users` en la misma transacción usando `IUnitOfWork`. Mapeo de roles a perfiles: `SCRAP→SCRAP`, `Producer→PRODUCER`, `Carrier→CARRIER`, `Plant→PLANT_OP`, `CAC→CAC_OP`, `PublicEntity→PUBLIC_ENT`, `Coordinator→COORDINATOR`, `OperatorTransfer→CARRIER`. Los roles `Source`, `Destination` y `Other` no generan usuario.

**4. `UpdateEntityCommand`**: actualiza todos los campos editables. Si cambia `EntityRole` y ya existe usuario vinculado, lanza `DomainException` con mensaje claro.

**5. `DeactivateEntityCommand`**: pone `IsActive = false`. Devuelve `bool HasLinkedUser` para que la UI pueda preguntar al usuario si también desea desactivar el usuario vinculado.

Validators (FluentValidation):
- `NationalId` único por `OwnerId` + `EntityRole` (consulta a DB)
- Si `EntityRole` es `Plant` o `CAC`: `Latitude` y `Longitude` obligatorios
- Si `EntityRole` es `Carrier`: `InscriptionNumber` obligatorio
- `Email` obligatorio cuando el `EntityRole` tiene perfil mapeado automático

**CAPA WEB — `GreenTransit.Web/Components/Pages/Entities/`**

**6. `EntityList.razor`**: tabla paginada con columnas Nombre, NIF, Rol, Provincia, Activo/Inactivo, Usuario vinculado (con enlace). Barra de filtros superior. Botones de acción: Nuevo, Editar, Desactivar (con confirmación).

**7. `EntityForm.razor`**: formulario de alta y edición con cuatro secciones claramente separadas:
- Datos de identificación (`Name`, `NationalId`, `CenterCode`, `EntityRole`, `EntityType`, `EconomicActivity`)
- Clasificación normativa (`TypeThirdParty`, `InscriptionType`, `InscriptionNumber`)
- Localización con `GeographySelector` en cascada (componente que se creará en B-3)
- Contacto (`PhoneNumber`, `Email`, `ContactPerson`)
- Sección colapsable "Acceso al sistema": visible y activa automáticamente cuando el `EntityRole` tiene perfil mapeado; muestra el `Login` sugerido prefillado con el `Email` y permite personalización

**8. `EntityDetail.razor`**: vista de solo lectura con todos los datos y un botón "Ir al usuario vinculado" que navega a la ficha del usuario.

Restricciones: filtro `OwnerId` obligatorio en todas las queries. Usa `IUnitOfWork` para la transacción conjunta Entidad + Usuario. Componentes Blazor con renderizado interactivo Server.

---

### ⬜ B-2 — Catálogos: LER + Residuos + Operaciones R/D

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`, `src/GreenTransit.Domain/Entities/LerCode.cs`, `src/GreenTransit.Domain/Entities/Residue.cs`, `src/GreenTransit.Domain/Entities/TreatmentOperation.cs`

**Prompt**:

Implementa los tres catálogos normativos de GreenTransit: LER, Residuos/Productos y Operaciones de Tratamiento R/D.

**SECCIÓN A — Catálogo LER (`GreenTransit.Application/Features/LerCodes/`)**

**1. `GetLerCodesQuery`**: filtros `Chapter?`, `SubChapter?`, `IsDangerous?`, `IsRAEE?`, `SearchTerm?`. Sin filtro `OwnerId` (catálogo global compartido). Devuelve estructura jerárquica `List<LerChapterDto>` donde cada capítulo contiene subcapítulos y cada subcapítulo contiene los códigos LER.

**2. `GetLerCodeByIdQuery`**: devuelve `LerCodeDetailDto` con todos los campos.

**3. `CreateLerCodeCommand` / `UpdateLerCodeCommand` / `ToggleLerCodeActiveCommand`**: solo accesibles por perfil `ADMIN`. Validator: `Code` único (6 dígitos), `Chapter` coherente con los dos primeros dígitos del `Code`.

**4. `LerCodeList.razor`**: tabla agrupada jerárquicamente por capítulo y subcapítulo, con opción de expandir/colapsar grupos. Filtros: IsDangerous, IsRAEE, búsqueda por texto. Botón exportar CSV. CRUD visible solo para perfil `ADMIN`.

**SECCIÓN B — Catálogo Residuos y Productos (`GreenTransit.Application/Features/Residues/`)**

**5. `GetResiduesQuery`**: filtros `ResidueType?`, `IdLERCode?`, `IsDangerous?`, `IsRAEE?`, `IdProducer?`, `SearchTerm?`. Filtra por `OwnerId`.

**6. `GetResidueByIdQuery`**: devuelve `ResidueDetailDto` con todos los campos incluyendo nombre del `LerCode` y nombre del `Producer` si aplica.

**7. `CreateResidueCommand` / `UpdateResidueCommand` / `ToggleResidueActiveCommand`**. Validators: si `IsDangerous = true` entonces `DangerousCode` obligatorio; si `ResidueType = ProductSpec` entonces `IdProducer` obligatorio.

**8. `ResidueList.razor`**: tabla con tres tabs "Residuos" / "Productos" / "Fichas técnicas" que filtra por `ResidueType`. Columnas relevantes según el tab activo.

**9. `ResidueForm.razor`**: formulario dinámico. Los campos de ecodiseño (`ReparabilityIndex`, `DisassemblyEase`, `ContainsHazardous`, `RecycledContentPercent`, `CompositionJson`, `PotentialLERCodesJson`, `MaterialsJson`) solo se muestran cuando `ResidueType = ProductSpec`. El campo `IdProducer` solo aparece para `ProductSpec`. El campo `DangerousCode` se hace obligatorio visualmente cuando `IsDangerous = true`.

**SECCIÓN C — Operaciones R/D (`GreenTransit.Application/Features/TreatmentOperations/`)**

**10. `GetTreatmentOperationsQuery`**: filtro `OperationType?` (Recovery/Disposal). Sin filtro `OwnerId`. Devuelve `List<TreatmentOperationDto>` con todos los campos.

**11. Componente Razor reutilizable `TreatmentOperationSelect.razor`** en `GreenTransit.Web/Components/Shared/`: selector con parámetro `@bind-Value` (Guid), agrupado visualmente en dos secciones "R — Valorización (R1–R13)" y "D — Eliminación (D1–D15)". Exponerlo para reutilizarlo en formularios de tratamiento y traslados.

Restricciones: LER y TreatmentOperations son catálogos globales sin filtro `OwnerId`. El componente `TreatmentOperationSelect` debe ser completamente reutilizable. CRUD de LER solo para `ADMIN` (usar `AuthorizeView`).

---

### ⬜ B-3 — Catálogos Geográficos: selectores en cascada

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `src/GreenTransit.Domain/Entities/Geography.cs`

**Prompt**:

Crea el sistema de selectores geográficos en cascada para GreenTransit.

**CAPA APPLICATION — `GreenTransit.Application/Features/Geography/`**

**1.** Crea los siguientes queries, cada uno sin filtro `OwnerId` (catálogos compartidos):
- `GetCountriesQuery` → `IEnumerable<CountryDto>` (Id, Name, IsoCode)
- `GetStatesByCountryQuery(int countryId)` → `IEnumerable<StateDto>`
- `GetProvincesByStateQuery(int stateId)` → `IEnumerable<ProvinceDto>`
- `GetMunicipalitiesByProvinceQuery(int provinceId)` → `IEnumerable<MunicipalityDto>`
- `GetZipCodesByMunicipalityQuery(int municipalityId)` → `IEnumerable<string>`

**2.** Todos los handlers deben implementar caché en memoria con `IMemoryCache` y TTL de 24 horas, ya que estos catálogos son casi estáticos.

**CAPA WEB — `GreenTransit.Web/Components/Shared/`**

**3. Componente `GeographySelector.razor`**:
- Parámetros enlazables: `@bind-CountryId`, `@bind-StateId`, `@bind-ProvinceCode`, `@bind-MunicipalityId`, `@bind-ZipCode`
- Cada nivel carga en cascada cuando cambia el nivel superior usando `EventCallback`
- Muestra spinner de carga mientras espera cada nivel
- Todos los selectores son opcionales; si se proporciona un valor inicial, los niveles superiores se precargan automáticamente
- Completamente reutilizable: se usará en `EntityForm`, `UserForm` y `AgreementForm`

Restricciones: Blazor interactivo Server. La caché debe registrarse con `AddMemoryCache()` en `Program.cs` si no está ya.

---

## 💶 BLOQUE C — Módulo de Contratación y Economía

---

### ⬜ C-1 — Formalización de Acuerdos (Agreements)

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`, `src/GreenTransit.Domain/Entities/Agreement.cs`, `src/GreenTransit.Domain/Entities/AgreementDocument.cs`

**Prompt**:

Implementa el módulo completo de Formalización de Acuerdos en GreenTransit. Las entidades `Agreement` y `AgreementDocument` ya están creadas en el dominio.

**CAPA APPLICATION — `GreenTransit.Application/Features/Agreements/`**

Queries:

**1. `GetAgreementsQuery`**: paginada. Filtros: `Status?`, `IdScrap?`, `IdPublicEntity?`, `Year?`, `SearchTerm?` (busca en `AgreementNumber`). Filtra por `OwnerId`. Devuelve `PaginatedResult<AgreementDto>`.

**2. `GetAgreementByIdQuery`**: devuelve `AgreementDetailDto` con todos los campos del `Agreement` más la lista de `AgreementDocuments`.

**3. `GetExpiringAgreementsQuery(int daysThreshold)`**: acuerdos con `EffectiveTo` entre hoy y hoy+`daysThreshold` días, estado `Active`. Para uso en alertas del dashboard.

Commands:

**4. `CreateAgreementCommand`**: datos agrupados en cuatro bloques lógicos:
- Partes: `IdScrap`, `IdPublicEntity`, `IdCoordinator?`
- Ámbito: `WasteStream`, `SubStream`, `AutonomousCommunity`, `ProvinceCode?`, `MunicipalityCode?`, `CoveredMethodsJson`
- Economía: `TariffModelType`, `TariffRulesJson`, `MinimumsJson`, `ObligationsJson`, `Currency` (default "EUR")
- Vigencia: `EffectiveFrom`, `EffectiveTo`
El handler calcula y persiste el `Hash` (SHA256 del JSON serializado del acuerdo completo). Estado inicial: `Draft`. `Version` inicial: 1.

**5. `UpdateAgreementCommand`**: solo si estado es `Draft`. Incrementa `Version` y recalcula `Hash`.

**6. `ActivateAgreementCommand(Guid id)`**: transición `Draft → Active`. Valida que `EffectiveFrom <= DateTime.UtcNow.Date <= EffectiveTo`.

**7. `CancelAgreementCommand(Guid id, string reason)`**: transición `Active → Cancelled`. `reason` obligatorio.

**8. `AttachDocumentCommand`**: añade un `AgreementDocument`. Campos: `AgreementId`, `DocumentType` (Contrato/Anexo/Acta), `DocumentId`, `DocumentHash` (SHA256), `SignedAt?`, `SignatureProvider?`.

Validators:
- `AgreementNumber` único por `OwnerId`
- `IdScrap` debe pertenecer a una `BusinessEntity` con `EntityRole = SCRAP`
- `IdPublicEntity` debe pertenecer a una `BusinessEntity` con `EntityRole = PublicEntity`
- `EffectiveTo > EffectiveFrom`

**CAPA WEB — `GreenTransit.Web/Components/Pages/Agreements/`**

**9. `AgreementList.razor`**: tabla con badge de estado coloreado (Draft=gris, Active=verde, Expired=naranja, Cancelled=rojo). Columna "Vence en" que muestra días restantes en color naranja si <30 días y rojo si <7 días.

**10. Componente reutilizable `StepperWizard.razor`** en `GreenTransit.Web/Components/Shared/` con parámetros: `Steps` (lista de nombres de paso), `CurrentStep` (índice activo), `OnStepChange` (callback). Muestra los pasos numerados con línea de progreso. Cada paso valida antes de permitir avanzar.

**11. `AgreementWizard.razor`**: formulario de 4 pasos usando el componente `StepperWizard`. Paso 1: Partes. Paso 2: Ámbito geográfico. Paso 3: Modelo económico (tabla editable de reglas tarifarias, no JSON raw: columnas `Category`, `PricePerKg`, `MinWeight`). Paso 4: Revisión y confirmación con resumen de todos los datos.

**12. `AgreementDetail.razor`**: vista con tres tabs: "Datos generales" | "Documentos" (lista de `AgreementDocuments` con botón adjuntar) | "Liquidaciones" (lista de `Settlements` asociados).

Restricciones: filtro `OwnerId` en todas las queries. Solo perfiles `ADMIN` y `SCRAP` pueden crear/editar acuerdos. `PUBLIC_ENT` solo puede leer y firmar.

---

### ⬜ C-2 — Liquidación Económica (Settlements)

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`, `src/GreenTransit.Domain/Entities/Settlement.cs`, `src/GreenTransit.Domain/Entities/SettlementLine.cs`

**Prompt**:

Implementa el módulo de Liquidación Económica en GreenTransit. Las entidades `Settlement` y `SettlementLine` ya están creadas en el dominio.

**CAPA APPLICATION — `GreenTransit.Application/Features/Settlements/`**

Queries:

**1. `GetSettlementsQuery`**: paginada. Filtros: `Status?`, `AgreementId?`, `Year?`, `Month?`, `IdScrap?`. Filtra por `OwnerId`.

**2. `GetSettlementByIdQuery`**: devuelve `SettlementDetailDto` con cabecera + lista de `SettlementLineDto` + `EvidenceRefsJson`.

Commands:

**3. `GenerateSettlementCommand(Guid agreementId, int year, int month)`**: lógica del handler en cinco pasos:
- Paso 1: valida que el `Agreement` esté `Active` y que no exista ya un `Settlement` en estado `Pending` o `Approved` para el mismo `agreementId` + `year` + `month`
- Paso 2: recupera todas las `EntryPlants` del periodo (`PlantEntryDate` dentro del año/mes) cuyo `OwnerId` coincide y cuyo `WasteMoveReference` corresponde a un `WasteMove` vinculado al ámbito del acuerdo
- Paso 3: agrupa `EntryPlantResidues` por `IdLERCode` × `ProductCategory` y suma `Weight`
- Paso 4: para cada grupo, busca la regla aplicable en `TariffRulesJson` del `Agreement` y calcula `Amount = WeightKg × PricePerKg`, aplicando el mínimo de `MinimumsJson` si el peso no llega
- Paso 5: calcula cabecera `BaseAmount` (suma de líneas), `AdjustmentsAmount` (eco-modulación si existe, sino 0), `TaxAmount` (IVA 21%), `TotalAmount`. Persiste con estado `Pending` y calcula `Hash`.
El comando acepta un parámetro `bool dryRun = false`. Si `dryRun = true`, devuelve el `SettlementDetailDto` calculado sin persistir (para la previsualización en UI).

**4. `ApproveSettlementCommand(Guid id)`**: transición `Pending → Approved`. Solo perfiles `SCRAP` o `ADMIN`. Registra `ValidatedAt = DateTime.UtcNow` y `Validator = ICurrentUserService.UserName`.

**5. `RejectSettlementCommand(Guid id, string reason)`**: transición `Pending → Rejected`.

**6. `RecalculateSettlementCommand(Guid id)`**: solo si estado es `Pending`. Elimina las líneas actuales y vuelve a ejecutar la lógica de generación. Un `Settlement` en estado `Approved` es inmutable: lanza `DomainException` si se intenta recalcular.

**CAPA WEB — `GreenTransit.Web/Components/Pages/Settlements/`**

**7. `SettlementList.razor`**: tabla con badge de estado coloreado, columnas de importes con formato moneda.

**8. `SettlementGenerate.razor`**: formulario con selector de Acuerdo + Año + Mes. Botón "Previsualizar" que llama a `GenerateSettlementCommand` con `dryRun = true` y muestra la tabla de líneas resultante con totales. Botón "Generar y guardar" que llama con `dryRun = false`.

**9. `SettlementDetail.razor`**: detalle con tabla de líneas, panel de totales, historial de estado y botones "Aprobar" / "Rechazar" / "Recalcular" visibles según el perfil y el estado actual.

Restricciones: un `Settlement` aprobado es completamente inmutable. El `NetWeight` fuente de verdad es siempre `EntryPlants.NetWeight`.

---

### ⬜ C-3 — Objetivos y Cuotas de Mercado (MarketShares)

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `src/GreenTransit.Domain/Entities/MarketShare.cs`

**Prompt**:

Implementa el módulo de Objetivos y Cuotas de Mercado en GreenTransit. La entidad `MarketShare` ya está creada en el dominio.

**CAPA APPLICATION — `GreenTransit.Application/Features/MarketShares/`**

**1. `GetMarketSharesQuery`**: filtros `IdScrap?`, `Category?`, `AutonomousCommunity?`, `Year?`. Filtra por `OwnerId`.

**2. `GetMarketShareComplianceQuery(int year)`**: para cada `MarketShare` del año del `OwnerId` activo, calcula el cumplimiento comparando `Weight` (objetivo en kg) con el peso real acumulado. El peso real se obtiene sumando `EntryPlantResidues.Weight` de las `EntryPlants` del mismo año cuyo `WasteMove` tenga `IdScrap` y cuya categoría de LER coincida con `MarketShare.Category`. Devuelve `List<MarketShareComplianceDto>` con campos: `Category`, `AutonomousCommunity`, `ObjectiveKg`, `AchievedKg`, `CompliancePercent`, `IsAtRisk` (true si `CompliancePercent < 80` a fecha actual teniendo en cuenta el mes en curso vs total del año).

**3. `CreateMarketShareCommand` / `UpdateMarketShareCommand`**: solo perfil `ADMIN`. Validator: no pueden existir dos `MarketShare` con el mismo `IdScrap` + `Category` + `AutonomousCommunity` + `Year` + `Period`.

**CAPA WEB — `GreenTransit.Web/Components/Pages/MarketShares/`**

**4. `MarketShareList.razor`**: tabla con columnas Categoría, CCAA, Año, Objetivo kg, Real kg, % Cumplimiento (renderizado como barra de progreso coloreada: verde si ≥100%, naranja si entre 80–99%, rojo si <80%), `FlowType`. Botones CRUD visibles solo para `ADMIN`.

**5.** Expone el método `GetMarketShareComplianceQuery` también como widget para el Dashboard (se integrará en F-1). El widget muestra una lista compacta de categorías con su barra de progreso.

Restricciones: filtro `OwnerId` obligatorio. Solo `ADMIN` y `SCRAP` acceden a este módulo.

---

## 🚛 BLOQUE D — Flujo Operativo de Residuos

---

### ⬜ D-1 — Órdenes de Servicio (ServiceOrders): CQRS completo + UI

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`, `src/GreenTransit.Domain/Entities/ServiceOrder.cs`, `src/GreenTransit.Application/Features/ServiceOrders/Commands/CreateServiceOrderCommand.cs`, `src/GreenTransit.Application/Features/ServiceOrders/Queries/GetServiceOrdersQuery.cs`, `tests/GreenTransit.Tests/Application/ServiceOrders/GetServiceOrdersQueryHandlerTests.cs`

**Prompt**:

Implementa el módulo completo de Órdenes de Servicio en GreenTransit. El archivo `CreateServiceOrderCommand.cs` actual es un placeholder de ejemplo y debe ser reemplazado por la implementación completa. La entidad `ServiceOrder` ya está creada en el dominio.

**CAPA APPLICATION — `GreenTransit.Application/Features/ServiceOrders/`**

Queries:

**1. `GetServiceOrdersQuery`**: paginada. Filtros: `Status?`, `Priority?`, `IdIssuedBy?`, `IdPickupPoint?`, `IdLERCode?`, `PlannedPickupFrom?` (DateTime), `PlannedPickupTo?` (DateTime), `SearchTerm?` (busca en `ServiceOrderNumber`). Filtra por `OwnerId`. Devuelve `PaginatedResult<ServiceOrderDto>` con campos: `Id`, `ServiceOrderNumber`, `Status`, `Priority`, `IssuedAt`, `PlannedPickupStart`, `IdPickupPoint`, `PickupPointName`, `WasteStream`, `EstimatedWeight`, `MeasureUnit`.

**2. `GetServiceOrderByIdQuery`**: devuelve `ServiceOrderDetailDto` con todos los campos del mapa sección 3.1, incluyendo nombres resueltos de `IdIssuedBy`, `IdPickupPoint`, `IdCarrier`, `IdPlannedPlant` (busca en `BusinessEntity`).

**3. `GetUpcomingServiceOrdersQuery(int days)`**: SOs con `PlannedPickupStart` entre hoy y hoy+`days` días, estado distinto de `Cancelled`. Filtra por `OwnerId`. Para widget del dashboard.

Commands:

**4. `CreateServiceOrderCommand`**: reemplaza el placeholder existente. Campos completos según mapa sección 3.1. Si `ServiceOrderNumber` llega vacío, genera uno automáticamente con formato `SO-{AÑO}-{SECUENCIA:00000}` donde la secuencia es el total de SOs del `OwnerId` + 1. Calcula `Hash` (SHA256 del JSON del command). `Version` = 1. Estado inicial: el que venga en el command.

**5. `UpdateServiceOrderCommand`**: actualiza todos los campos editables. Solo si `Status` es `Pending` o `Scheduled`. Incrementa `Version` y recalcula `Hash`.

**6. `DuplicateServiceOrderCommand(Guid sourceId)`**: clona la SO con nuevas fechas planificadas (nulas, para que el usuario las rellene) y estado `Pending`. El `ServiceOrderNumber` se genera nuevo automáticamente.

**7. `CancelServiceOrderCommand(Guid id, string reason)`**: cambia estado a `Cancelled`. Solo si no tiene un `WasteMove` activo vinculado.

**8. `LinkToWasteMoveCommand(Guid serviceOrderId, Guid wasteMoveId)`**: actualiza `WasteMoveReference` y estado a `InProgress`.

Validators:
- `IdPickupPoint` debe existir y pertenecer a una `BusinessEntity` del mismo `OwnerId`
- `PlannedPickupEnd > PlannedPickupStart` cuando ambos están informados
- `PlannedDeliveryStart >= PlannedPickupEnd` cuando ambos están informados
- `ServiceOrderNumber` único por `OwnerId`
- Si el `LerCode` vinculado tiene `IsDangerous = true`, incluir `Warning` en la respuesta (no bloquear la creación)

**CAPA WEB — `GreenTransit.Web/Components/Pages/ServiceOrders/`**

**9. `ServiceOrderList.razor`**: tabla con filtros en barra superior, paginación, badge de estado coloreado, badge de prioridad. Acciones por fila: Ver detalle, Editar, Duplicar, Cancelar (con confirmación). Botón "Nueva SO" en cabecera.

**10. `ServiceOrderForm.razor`**: formulario con cuatro secciones en acordeón:
- Identificación: número (con botón "Generar automático"), fechas de issuance, estado, prioridad
- Emisor y punto de recogida: selector de `BusinessEntity` filtrado por roles `Source|CAC|PublicEntity|Producer` para `IdIssuedBy`, y `IdPickupPoint` filtrado igual
- Clasificación: `WasteStream`, `SubStream`, `ProductUse`, `ProductCategory`, `IdLERCode` (con indicador visual si es peligroso), `EstimatedWeight`, `MeasureUnit`, `Units`, `ContainersJson` (tabla dinámica con columnas Tipo y Cantidad)
- Asignaciones previstas: `IdCarrier` (filtrado por `EntityRole=Carrier`), `IdPlannedPlant` (filtrado por `EntityRole=Plant`), vinculación opcional a `Agreement`

**11. `ServiceOrderDetail.razor`**: vista de detalle con todos los datos, estado actual con badge, y botón "Crear Traslado" que navega a `WasteMoveForm` con la SO preseleccionada. Muestra aviso si el LER es peligroso.

**TESTS — `GreenTransit.Tests/Application/ServiceOrders/`**

**12.** Actualiza o reemplaza el contenido de `GetServiceOrdersQueryHandlerTests.cs` ya existente con casos: lista filtrada por estado, búsqueda por `SearchTerm`, filtro por fechas de recogida, resultado vacío para tenant sin datos.

**13. Crea `CreateServiceOrderCommandHandlerTests.cs`** con casos: creación exitosa con `ServiceOrderNumber` automático, creación con `ServiceOrderNumber` manual, duplicación de SO existente, error por `IdPickupPoint` de otro tenant, error por fechas incoherentes.

Restricciones: filtro `OwnerId` obligatorio en todas las queries. `ServiceOrderNumber` único por `OwnerId`. Componentes Blazor Server interactivos.

---

### ⬜ D-2 — Traslados (WasteMoves): creación — estado SOLICITADO

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`, `src/GreenTransit.Domain/Entities/WasteMove.cs`, `src/GreenTransit.Domain/Entities/WasteMoveResidue.cs`, `src/GreenTransit.Domain/Entities/ServiceOrder.cs`

**Prompt**:

Implementa la primera parte del módulo de Traslados de Residuos: creación y estado SOLICITADO. Las entidades `WasteMove` y `WasteMoveResidue` ya están creadas en el dominio.

**CAPA APPLICATION — `GreenTransit.Application/Features/WasteMoves/`**

Queries:

**1. `GetWasteMovesQuery`**: paginada. Filtros: `ServiceStatus?`, `IdSource?`, `IdDestination?`, `IdScrap?`, `DateFrom?`, `DateTo?`, `SearchTerm?` (busca en `WasteMoveReference`). Filtra por `OwnerId`. Devuelve `PaginatedResult<WasteMoveDto>` con campos: `Id`, `WasteMoveReference`, `ServiceStatus`, `IdSource`, `SourceName`, `IdDestination`, `DestinationName`, `PlannedPickupStart`, `RequestDate`, `ResidueCount` (nº de líneas).

**2. `GetWasteMoveByIdQuery`**: devuelve `WasteMoveDetailDto` con todos los campos del `WasteMove` + lista de `WasteMoveResidueDto` + nombres de entidades relacionadas + estado actual + `ServiceOrderNumber` de la SO vinculada.

**3. `GetWasteMovesByServiceOrderQuery(Guid serviceOrderId)`**: lista los `WasteMoves` vinculados a una SO concreta.

Commands:

**4. `CreateWasteMoveCommand`**: campos de entrada:
- `ServiceOrderIds` (Guid[], una o varias SOs a agrupar en este traslado)
- `IdSource` (FK BusinessEntity)
- `IdDestination` (FK BusinessEntity)
- `IdScrap` (FK BusinessEntity)
- `IdScrap2?` (FK BusinessEntity, opcional)
- `PlannedPickupStart?`, `PlannedPickupEnd?`, `PlannedDeliveryStart?`, `PlannedDeliveryEnd?`
El handler genera `WasteMoveReference` automáticamente (formato `WM-{AÑO}-{SECUENCIA:00000}`), crea las líneas `WasteMoveResidues` automáticamente heredando `IdResidue`, `Weight`, `MeasureUnit`, `Units` y `IdTreatmentOperationDestiny` de los residuos de la SOs origen, establece `ServiceStatus = "SOLICITADO"` y actualiza `WasteMoves.ServiceOrderId` con la SO del array.

**5. `UpdateWasteMoveResidueCommand`**: permite editar campos de una línea individual: `Weight`, `MeasureUnit`, `Units`, `UnitPriceKg`, `IdTreatmentOperationDestiny`, `DateDelivery`. Solo si el `WasteMove` padre está en estado `SOLICITADO`.

Validators:
- `IdSource.EntityRole` debe estar en `{Source, CAC, PublicEntity, Producer}`
- `IdDestination.EntityRole` debe estar en `{Destination, Plant, CAC}`
- Si algún `Residue` vinculado tiene `IsDangerous = true` o `IsRAEE = true`, `IdTreatmentOperationDestiny` obligatorio en esa línea
- `IdScrap` debe coincidir con el `IdScrap` de la SOs origen

**CAPA WEB — `GreenTransit.Web/Components/Pages/WasteMoves/`**

**6. `WasteMoveList.razor`**: tabla con componente `WasteMoveStatusStepper` embebido en cada fila mostrando visualmente el estado actual del traslado (crea este componente según se describe en D-3). Filtros avanzados en panel lateral colapsable.

**7. `WasteMoveForm.razor`**: formulario de creación con:
- Selector SO (checkboxes, solo muestra SOs en estado `Pending`/`Scheduled` del `OwnerId` activo)
- Al seleccionar una SO, la tabla de líneas de residuos se autocompleta desde los residuos de la SO seleccionada (las líneas son editables)
- Selector de origen filtrado por `EntityRole ∈ {Source, CAC, PublicEntity, Producer}`
- Selector de destino filtrado por `EntityRole ∈ {Destination, Plant, CAC}`
- Campos de fechas planificadas opcionales

Restricciones: la transición de estado debe validarse siempre en el command handler, nunca solo en UI. `WasteMoveReference` único por `OwnerId`. Filtro `OwnerId` en todas las queries.

---

### ⬜ D-3 — Planificación Logística — estado PLANIFICADO + validación DUM

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`, `src/GreenTransit.Domain/Entities/WasteMove.cs`, `src/GreenTransit.Domain/Entities/WasteMoveResidue.cs`, `src/GreenTransit.Domain/Entities/DumZone.cs`, `src/GreenTransit.Domain/Entities/DumRestrictionRule.cs`

**Prompt**:

Implementa la planificación logística de traslados: transición de estado SOLICITADO → PLANIFICADO con validación de Zonas DUM.

**CAPA APPLICATION**

**1. `PlanWasteMoveCommand(Guid wasteMoveId)`** con campos:
- `PlannedPickupStart`, `PlannedPickupEnd`, `PlannedDeliveryStart`, `PlannedDeliveryEnd`
- `IdOperatorTransfer` (FK BusinessEntity con EntityRole=Carrier o OperatorTransfer)
- `Lines`: array de `{ WasteMoveResidueId, IdCarrier, VehicleRegistration, VehicleRegistrationTrailer?, VehicleType, FuelType, EuroClass }`

Lógica del handler en cuatro pasos:
- Paso 1: valida `WasteMove.ServiceStatus == "SOLICITADO"` (lanza `InvalidOperationException` si no)
- Paso 2: valida que `IdCarrier.EntityRole = Carrier` y tiene `InscriptionNumber` no nulo
- Paso 3: llama a `IDumZoneService.CheckPickupPointAsync(Guid pickupPointId, DateTime plannedDate, string vehicleType, string euroClass)` y procesa el resultado: si `ActionType = "Block"` lanza `DomainException` con el motivo; si `ActionType = "Restrict"` o `"Notify"` añade la advertencia a la respuesta pero continúa
- Paso 4: actualiza los campos del `WasteMove` y las líneas `WasteMoveResidues`, cambia `ServiceStatus = "PLANIFICADO"`

**2. Interfaz `IDumZoneService`** en `GreenTransit.Application/Common/Interfaces/`:
Task<DumCheckResult> CheckPickupPointAsync(Guid pickupPointId, DateTime plannedDate, string vehicleType, string euroClass, CancellationToken ct)

donde `DumCheckResult` tiene: `ActionType` (string), `Reason` (string?), `ZoneCodes` (string[]).

**3. Implementación `DumZoneService`** en `GreenTransit.Infrastructure/Services/`:
- Obtiene `Latitude` y `Longitude` de la `BusinessEntity` del `IdPickupPoint` de la SO vinculada al `WasteMove`
- Carga todas las `DumZones` con `Status = "Active"` y sus `DumRestrictionRules` con `ValidFrom <= plannedDate <= ValidTo`
- Para cada zona, comprueba si el punto (lat/lng) está dentro del `GeometryJson` (polígono GeoJSON). Usa una función de `point-in-polygon` implementada manualmente con el algoritmo ray-casting sobre las coordenadas del GeoJSON, sin dependencias externas pesadas
- Evalúa las condiciones de cada regla activa y devuelve el resultado más restrictivo (Block > Restrict > Notify > Allow)
- Si no hay zonas o el punto no cae en ninguna, devuelve `{ ActionType = "Allow" }`

**CAPA WEB — `GreenTransit.Web/Components/Pages/WasteMoves/`**

**4. `WasteMovePlan.razor`**: formulario de planificación con:
- Selector de operador de transferencia (filtrado por `EntityRole ∈ {Carrier, OperatorTransfer}`)
- Tabla de líneas de residuos con selector de transportista y vehículo por línea
- Panel de resultado DUM: aparece solo si `IDumZoneService` devuelve advertencias o bloqueos. Color amarillo para Restrict/Notify, rojo para Block. Muestra el `ZoneCode` y el motivo. El botón "Confirmar planificación" está deshabilitado si hay un `Block`

**5. Componente `WasteMoveStatusStepper.razor`** en `GreenTransit.Web/Components/Shared/`:
- Parámetro `CurrentStatus` (string)
- Muestra los pasos: SOLICITADO → PLANIFICADO → RECOGIDO → EN CAC (punteado, opcional) → EN PLANTA → CLASIFICADO
- El paso actual se resalta. Los pasos completados tienen check verde. Los bloqueados tienen icono de alerta roja
- Incluye tooltip en cada paso con la fecha real si está disponible (parámetro `StatusDates` de tipo `Dictionary<string, DateTime?>`)

**TESTS — `GreenTransit.Tests/Infrastructure/`**

**6. `DumZoneServiceTests.cs`**:
- Caso: sin zonas configuradas → resultado `Allow`
- Caso: punto dentro de zona con regla `Block` activa → resultado `Block` con motivo
- Caso: punto dentro de zona con regla `Notify` → resultado `Notify`
- Caso: punto fuera de todas las zonas → resultado `Allow`
- Caso: zona con regla expirada (`ValidTo < plannedDate`) → resultado `Allow`

Restricciones: la validación DUM no debe bloquear si no hay zonas. El algoritmo ray-casting debe manejar polígonos convexos y cóncavos. Registra en Serilog cada comprobación DUM con nivel `Debug`.

---

### ⬜ D-4 — Ejecución de Recogida — estado RECOGIDO + cálculo de emisiones CO₂

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`, `src/GreenTransit.Domain/Entities/WasteMove.cs`, `src/GreenTransit.Domain/Entities/WasteMoveResidue.cs`, `src/GreenTransit.Domain/Entities/EmissionFactorSet.cs`, `src/GreenTransit.Domain/Entities/EmissionFactor.cs`

**Prompt**:

Implementa la confirmación de recogida (PLANIFICADO → RECOGIDO) y el cálculo automático de huella de carbono.

**CAPA APPLICATION**

**1. `ConfirmPickupCommand(Guid wasteMoveId)`** con campos:
- `ActualPickupStart` (DateTime), `ActualPickupEnd?` (DateTime), `GatheredDate` (DateTime)
- `DocumentId?` (string, referencia al DMS externo), `DocumentHash?` (string, SHA256)
- `SignatureStatus?` (string)
- `Lines`: array de `{ WasteMoveResidueId, NTNumber?, DINumber?, DIPhase? }`

Lógica del handler:
- Valida `WasteMove.ServiceStatus == "PLANIFICADO"`
- Para cada línea: si `Residue.IsDangerous = true` entonces `NTNumber`, `DINumber` y `DIPhase` son obligatorios (lanza `ValidationException` si faltan)
- Actualiza los campos del `WasteMove` y de las líneas `WasteMoveResidues`
- Cambia `ServiceStatus = "RECOGIDO"`
- Después de guardar, dispara `CalculateEmissionsCommand` de forma asíncrona sin bloquear la respuesta (usa `IMediator.Send` con un `CancellationToken` independiente y captura excepciones logueándolas con Serilog `Warning`)

**2. `CalculateEmissionsCommand(Guid wasteMoveId)`** en `GreenTransit.Application/Features/Emissions/`:

Para cada `WasteMoveResidue` del traslado:
- Obtiene `VehicleType`, `FuelType`, `EuroClass` de la línea
- Busca el `EmissionFactorSet` activo: `Status = "Active"` con `ValidFrom` más reciente y `ValidFrom <= DateTime.UtcNow`
- Busca el `EmissionFactor` correspondiente a `FactorSetId` + `VehicleType` + `FuelType` + `EuroClass`
- Calcula: `TransportCarbonEmissions = TransportInfo_TransportDistance × EmissionFactor.Value`
- Actualiza en la línea: `TransportInfo_TransportCarbonEmissions`, `EmissionFactorSetId`, `EmissionFactorVersion`
- Si no existe factor para esa combinación: loguea `Warning` con los parámetros buscados y continúa con la siguiente línea

**3. `RecalculateAllEmissionsCommand`**: solo perfil `ADMIN`. Re-calcula todas las `WasteMoveResidues` de todos los traslados en estado `RECOGIDO` o posterior usando el `EmissionFactorSet` activo actual. Procesa en lotes de 100 para no saturar la DB.

**CAPA WEB — `GreenTransit.Web/Components/Pages/WasteMoves/`**

**4. `WasteMovePickup.razor`**: formulario de confirmación de recogida con:
- Campos de tiempos reales (datepickers)
- Campo de `DocumentId` con botón para calcular el `DocumentHash` del texto introducido (SHA256 en cliente vía JS interop)
- Tabla de líneas de residuos con columnas `NTNumber`, `DINumber`, `DIPhase`. Las celdas de estas columnas se resaltan en rojo y son obligatorias para las líneas cuyo residuo es peligroso
- Panel "Estimación de emisiones CO₂" que muestra una estimación calculada en frontend antes de confirmar (basada en la distancia planificada de la SO y los factores del último set activo)

Restricciones: el cálculo de emisiones nunca bloquea el flujo si falla. Loguea siempre el `EmissionFactorVersion` usado con nivel `Information`. Tests unitarios para `CalculateEmissionsCommand` con casos: factor encontrado y calculado correctamente, factor no encontrado (continúa sin error), set activo no existe (continúa sin error).

---

### ⬜ D-5 — Entrada en CAC — estado EN CAC

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `src/GreenTransit.Domain/Entities/EntryCAC.cs`, `src/GreenTransit.Domain/Entities/EntryCACResidue.cs`

**Prompt**:

Implementa el módulo de Entrada en Centro de Acopio Ciudadano (CAC), paso opcional del flujo operativo. Las entidades `EntryCAC` y `EntryCACResidue` ya están en el dominio.

**CAPA APPLICATION — `GreenTransit.Application/Features/EntryCACs/`**

**1. `GetEntryCACsQuery`**: paginada. Filtros: `WasteMoveReference?`, `CACEntryDateFrom?`, `CACEntryDateTo?`. Filtra por `OwnerId`.

**2. `GetEntryCACByIdQuery`**: devuelve `EntryCACDetailDto` con cabecera + lista de `EntryCACResidueDto`.

**3. `CreateEntryCACCommand(Guid wasteMoveId)`** con campos:
- Cabecera: `CACEntryDate`, `TypeContainer?`, `PriceContainer?`, `CollectionMethod?`
- `Lines`: array de `{ IdResidue, Weight, MeasureUnit, Units, PriceWeight?, PriceUnit? }`

Lógica del handler:
- Valida `WasteMove.ServiceStatus == "RECOGIDO"` (lanza `InvalidOperationException` si no)
- Hereda `OwnerId`, `WasteMoveReference` e `IdUser` del `WasteMove`
- Crea `EntryCAC` + líneas `EntryCACResidues`
- Cambia `WasteMove.ServiceStatus = "EN CAC"`

**4. `UpdateEntryCACCommand(Guid id)`**: actualiza campos de cabecera y líneas. Solo si el `WasteMove` vinculado sigue en estado `EN CAC`.

**CAPA WEB — `GreenTransit.Web/Components/Pages/EntryCACs/`**

**5. `EntryCACForm.razor`**: formulario optimizado para terminal táctil con:
- Selector del traslado en curso (dropdown filtrado por `ServiceStatus = "RECOGIDO"` del `OwnerId` activo)
- Campos de cabecera con botones grandes (diseño mobile-first)
- Tabla editable de líneas con selector de residuo y campos de peso numérico con teclado numérico virtual compatible
- Validación inline visible inmediatamente al cambiar cada campo
- Botón "Guardar y continuar" prominente

**6. `EntryCACList.razor`**: tabla de entradas con filtros por fecha y traslado.

Restricciones: perfil `CAC_OP` es el único que puede crear entradas de CAC. El `OwnerId` del `EntryCAC` se hereda siempre del `WasteMove`. Diseño mobile-first para este formulario específicamente.

---

### ⬜ D-6 — Entrada y Pesaje en Planta — estado EN PLANTA

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`, `src/GreenTransit.Domain/Entities/EntryPlant.cs`, `src/GreenTransit.Domain/Entities/EntryPlantResidue.cs`, `src/GreenTransit.Domain/Entities/Incident.cs`

**Prompt**:

Implementa la entrada y pesaje en planta (transición a estado EN PLANTA). Las entidades `EntryPlant`, `EntryPlantResidue` e `Incident` ya están en el dominio.

**CAPA APPLICATION — `GreenTransit.Application/Features/EntryPlants/`**

**1. `GetEntryPlantsQuery`**: paginada. Filtros: `WasteMoveReference?`, `PlantEntryDateFrom?`, `PlantEntryDateTo?`, `WeighbridgeId?`. Filtra por `OwnerId`.

**2. `GetEntryPlantByIdQuery`**: devuelve `EntryPlantDetailDto` con cabecera + lista de `EntryPlantResidueDto` + datos básicos del `WasteMove` origen (`WasteMoveReference`, `ServiceStatus`, `SourceName`).

**3. `CreateEntryPlantCommand(Guid wasteMoveId)`** con campos:
- Cabecera: `TicketScale`, `WeighbridgeId?`, `PlantEntryDate`, `TypeContainer?`, `PriceContainer?`
- Pesos: `GrossWeight` (decimal), `TareWeight` (decimal)
- `ServiceOrderId?` (FK a ServiceOrder si aplica)
- `Lines`: array de `{ IdResidue, Weight, MeasureUnit, Units, PriceWeight?, PriceUnit? }`

Lógica del handler:
- Valida `WasteMove.ServiceStatus ∈ {"RECOGIDO", "EN CAC"}`
- Calcula `NetWeight = GrossWeight - TareWeight` en backend (nunca acepta `NetWeight` del cliente)
- Compara `NetWeight` con la suma de `WasteMoveResidues.Weight` del traslado. Si la diferencia relativa supera el 5%, crea automáticamente un `Incident` con `Type = "WeightDiscrepancy"`, `Severity = "Medium"`, `Description` con los valores calculados, vinculado al `WasteMoveReference`. El traslado avanza igualmente a `EN PLANTA`
- Cambia `WasteMove.ServiceStatus = "EN PLANTA"`

**4.** Validator: `GrossWeight > TareWeight` y ambos `> 0`. `TicketScale` no vacío.

**CAPA WEB — `GreenTransit.Web/Components/Pages/EntryPlants/`**

**5. `EntryPlantForm.razor`**:
- Campos `GrossWeight` y `TareWeight` visualmente destacados (tipografía grande, fondo diferenciado)
- Campo `NetWeight` de solo lectura calculado en tiempo real con JavaScript al cambiar `GrossWeight` o `TareWeight`
- Alert amarillo si el `NetWeight` difiere en más del 5% del peso estimado de la SO vinculada (cálculo en frontend como aviso previo; la validación definitiva es backend)
- Selector del traslado filtrado por `ServiceStatus ∈ {"RECOGIDO", "EN CAC"}`
- Tabla de líneas de residuos con peso por fracción

**6. `EntryPlantList.razor`**: tabla con totales de `NetWeight` agrupados por día. Columna `TicketScale` enlazada al detalle.

Restricciones: `NetWeight` calculado exclusivamente en backend. Perfil `PLANT_OP` tiene acceso a creación. El `Incident` por descuadre se crea silenciosamente sin interrumpir el flujo y se muestra como aviso en la respuesta.

---

### ⬜ D-7 — Clasificación y Tratamiento Final — estado CLASIFICADO

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`, `src/GreenTransit.Domain/Entities/TreatmentPlant.cs`, `src/GreenTransit.Domain/Entities/TreatmentPlantResidue.cs`, `src/GreenTransit.Domain/Entities/Incident.cs`, `src/GreenTransit.Domain/Entities/TreatmentOperation.cs`

**Prompt**:

Implementa el paso final del flujo operativo: clasificación y tratamiento (EN PLANTA → CLASIFICADO). Las entidades `TreatmentPlant`, `TreatmentPlantResidue`, `TreatmentOperation` e `Incident` ya están en el dominio.

**CAPA APPLICATION — `GreenTransit.Application/Features/TreatmentPlants/`**

**1. `GetTreatmentPlantsQuery`**: paginada. Filtros: `WasteMoveReference?`, `PlantTreatmentDateFrom?`, `PlantTreatmentDateTo?`, `IdTreatmentOperation?`. Filtra por `OwnerId`.

**2. `GetTreatmentPlantByIdQuery`**: devuelve `TreatmentPlantDetailDto` con cabecera + lista de `TreatmentPlantResidueDto` con las tres fracciones por línea + nombre de la operación R/D + KPIs calculados (tasa reciclaje, valorización, rechazo).

**3. `CreateTreatmentPlantCommand(Guid wasteMoveId)`** con campos:
- Cabecera: `PlantTreatmentDate`, `IdTreatmentOperation`, `TicketScale?`, `ServiceOrderId?`, `ImproperWeight?`, `QualityMetricsJson?`, `TypeContainer?`, `PriceContainer?`
- `Lines`: array de `{ IdResidue, Category, WeightTotal, MeasureUnit, Units, PriceWeight?, PriceUnit?, IdResidueReused?, WeightReused?, MeasureUnitReused?, UnitsReused?, IdResidueValued?, WeightValued?, MeasureUnitValued?, UnitsValued?, IdResidueRemove?, WeightRemove?, MeasureUnitRemove?, UnitsRemove? }`

Lógica del handler — validación crítica de balance de masas:
Para cada línea: suma = (WeightReused ?? 0) + (WeightValued ?? 0) + (WeightRemove ?? 0) + (ImproperWeight ?? 0) diferencia = Math.Abs(WeightTotal - suma) tolerancia = WeightTotal * 0.01  // 1% Si diferencia > tolerancia: → Crea Incident automático: Type="MassBalanceError", Severity="High", Description="Descuadre de {diferencia:F2} kg en línea {IdResidue}", WasteMoveReference del WasteMove padre → NO cambia ServiceStatus → Devuelve error con lista de líneas con descuadre

Si todas las líneas cuadran: cambia `WasteMove.ServiceStatus = "CLASIFICADO"` y vincula `TreatmentPlant.IncidentId = null`.

**CAPA WEB — `GreenTransit.Web/Components/Pages/TreatmentPlants/`**

**4. `TreatmentPlantForm.razor`**:
- Selector de operación R/D usando el componente `TreatmentOperationSelect` creado en B-2
- Tabla de líneas con tres sub-columnas por fracción (Reutilizado / Valorizado / Rechazo), cada una con campos de peso
- Barra de progreso de balance de masas por línea: actualizada en tiempo real con JavaScript. Verde si la suma cuadra dentro del 1%, roja si no
- Campos de calidad: `ImproperWeight` (decimal), `QualityMetricsJson` editable como tabla dinámica de pares clave-valor (no JSON raw)
- El botón "Confirmar tratamiento" está deshabilitado en tiempo real si alguna línea tiene descuadre

**5. `TreatmentPlantDetail.razor`**: vista con KPIs calculados: tasa de reciclaje %, tasa de valorización %, % de rechazo, operación R/D aplicada con descripción oficial. Panel de incidencias asociadas si las hay.

**TESTS — `GreenTransit.Tests/Application/TreatmentPlants/`**

**6. `CreateTreatmentPlantCommandHandlerTests.cs`**:
- Caso: balance correcto → estado cambia a `CLASIFICADO`, no crea `Incident`
- Caso: balance fuera de tolerancia → estado no cambia, crea `Incident` con `Severity=High`
- Caso: línea con `WeightTotal = 0` → lanza `ValidationException`
- Caso: `WasteMove` sin `EntryPlant` previa (estado diferente a `EN PLANTA`) → lanza `InvalidOperationException`

Restricciones: la validación de balance lanza error de dominio (no solo warning) si supera tolerancia. Perfil `PLANT_OP` solo ve y crea registros del `OwnerId` de su entidad.

---

### ⬜ D-8 — Vista 360º del Traslado

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `src/GreenTransit.Domain/Entities/WasteMove.cs`, `src/GreenTransit.Domain/Entities/ServiceOrder.cs`

**Prompt**:

Implementa la vista consolidada 360º de un traslado en GreenTransit.

**CAPA APPLICATION**

**1. `GetWasteMoveTimelineQuery(Guid wasteMoveId)`**: devuelve `WasteMoveTimelineDto` con toda la información del ciclo completa:
- `ServiceOrder`: datos de la SO origen
- `WasteMove` + `WasteMoveResidues`: estado, actores, residuos, documentos, huella CO₂
- `EntryCACs` + `EntryCACResidues`: lista (puede estar vacía)
- `EntryPlants` + `EntryPlantResidues`: lista
- `TreatmentPlants` + `TreatmentPlantResidues`: lista
- `SettlementLines`: líneas de liquidación vinculadas al `WasteMoveReference`
- `Incidents`: lista de incidencias vinculadas
- `TotalCO2Emissions`: suma de `TransportInfo_TransportCarbonEmissions` de todas las líneas de residuos
- `CurrentStatus`: estado actual del `WasteMove`

La visibilidad depende del perfil: `CARRIER` solo ve las líneas donde figura como `IdCarrier`; `PLANT_OP` solo ve entradas y tratamientos de su `OwnerId`; `ADMIN` y `SCRAP` ven todo.

**CAPA WEB — `GreenTransit.Web/Components/Pages/WasteMoves/`**

**2. `WasteMoveTimeline.razor`**:
- Componente `WasteMoveStatusStepper` en la parte superior con el estado actual y fechas reales
- Cards verticales por paso del ciclo: cada card muestra el icono del paso, la fecha, los actores principales y los KPIs del paso (peso, documentos, emisiones). Los pasos sin datos aparecen en gris indicando que no han ocurrido
- Sección "Documentos del expediente": lista de todos los documentos (DI, NT, ticket de báscula, certificado de tratamiento) con sus hashes y estado de firma
- Sección "Incidencias": lista de incidencias vinculadas con badge de severidad
- Sección "Huella de CO₂": total en kgCO₂e con desglose por línea de transporte
- Botón "Exportar expediente PDF": genera un PDF del expediente completo usando `QuestPDF` (instala el paquete). El PDF incluye todos los datos del timeline ordenados cronológicamente
- Mapa con punto de origen y punto de destino usando Leaflet.js embebido vía JavaScript interop

Restricciones: la query respeta siempre la visibilidad por perfil. Accesible desde el buscador global escribiendo el `WasteMoveReference`, `DINumber`, `NTNumber` o `TicketScale`.

---

## 🌱 BLOQUE E — Sostenibilidad, Incidencias y Zonas DUM

---

### ⬜ E-1 — Gestión de Incidencias

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`, `src/GreenTransit.Domain/Entities/Incident.cs`

**Prompt**:

Implementa el módulo completo de gestión de incidencias en GreenTransit. La entidad `Incident` ya está en el dominio.

**CAPA APPLICATION — `GreenTransit.Application/Features/Incidents/`**

**1. `GetIncidentsQuery`**: paginada. Filtros: `Severity?`, `IsOpen?` (bool: `ClosedAt IS NULL`), `Type?`, `ServiceOrderId?`, `WasteMoveReference?`, `DateFrom?`, `DateTo?`. Filtra por `OwnerId`.

**2. `GetIncidentByIdQuery`**: devuelve `IncidentDetailDto` con todos los campos incluyendo `ResolutionJson` parseado como objeto estructurado.

**3. `OpenIncidentCommand`**: campos: `Type`, `Severity` (`Low|Medium|High|Critical`), `ServiceOrderId?`, `WasteMoveReference?`, `TicketScale?`, `ReportedByName`, `ReportedByNationalId?`, `ReportedByCenterCode?`, `Description`.
Lógica del handler: establece `OpenedAt = DateTime.UtcNow`. Si `Severity ∈ {"High", "Critical"}` y `WasteMoveReference` está informado, busca el `WasteMove` correspondiente, guarda su `ServiceStatus` actual en `ResolutionJson` (campo `previousStatus`) y cambia `WasteMove.ServiceStatus = "BLOQUEADO"`. Calcula y persiste `Hash`.

**4. `ResolveIncidentCommand(Guid id)`**: campos: `ResolutionType`, `ResolutionDescription`, `ResolvedByName`. Lógica: establece `ClosedAt = DateTime.UtcNow`. Si el `WasteMove` vinculado estaba `BLOQUEADO`, lo restaura al estado guardado en `ResolutionJson.previousStatus`. Actualiza `Hash`.

**CAPA WEB — `GreenTransit.Web/Components/Pages/Incidents/`**

**5. `IncidentList.razor`**: tabla con dos tabs "Abiertas" / "Cerradas". Badge de severidad con colores: Critical=rojo, High=naranja, Medium=amarillo, Low=azul gris. Filtros por severidad, tipo y fecha. Botón "Nueva incidencia" accesible para cualquier perfil autenticado.

**6. `IncidentForm.razor`**: formulario rápido de apertura optimizado para móvil. Selector de severidad como botones grandes con color. Campo de descripción de texto libre grande. Selector de traslado vinculado (opcional). Diseño mobile-first.

**7. `IncidentDetail.razor`**: ficha con todos los datos de apertura, estado del traslado vinculado (con enlace), y formulario de resolución (solo visible si `ClosedAt IS NULL` y el perfil tiene permiso). El formulario de resolución tiene campos `ResolutionType`, `ResolutionDescription` y un botón "Resolver y restaurar estado del traslado".

**8.** Widget `IncidentsSummaryWidget.razor` en `GreenTransit.Web/Components/Shared/`: muestra 4 cards con el contador de incidencias abiertas por severidad (Critical, High, Medium, Low). Para uso en el Dashboard.

Restricciones: cualquier perfil autenticado puede abrir una incidencia. Solo `ADMIN` o el responsable del tipo puede cerrarla. El campo `ResolutionJson` se edita como formulario estructurado, nunca como JSON raw en la UI.

---

### ⬜ E-2 — Zonas DUM: editor visual y simulador

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `src/GreenTransit.Domain/Entities/DumZone.cs`, `src/GreenTransit.Domain/Entities/DumRestrictionRule.cs`

**Prompt**:

Implementa el módulo de gestión de Zonas DUM con editor visual y simulador de restricciones. Las entidades `DumZone` y `DumRestrictionRule` ya están en el dominio.

**CAPA APPLICATION — `GreenTransit.Application/Features/DumZones/`**

**1. `GetDumZonesQuery`**: filtros `Status?`, `ZoneCode?`. Sin filtro `OwnerId` (zonas son globales por municipio). Devuelve `List<DumZoneDto>` con campo `RulesCount` (nº de reglas activas).

**2. `GetDumZoneByIdQuery`**: devuelve `DumZoneDetailDto` con `GeometryJson` + lista de `DumRestrictionRuleDto`.

**3. `CreateDumZoneCommand` / `UpdateDumZoneCommand`**: campos: `ZoneCode` (único), `GeometryJson` (GeoJSON), `Status`. Validator: `GeometryJson` debe ser un GeoJSON `Polygon` o `MultiPolygon` válido (valida estructura básica en el handler). Solo perfil `ADMIN`.

**4. `AddRestrictionRuleCommand(Guid zoneId)`**: crea una `DumRestrictionRule`. Campos: `RuleCode`, `ValidFrom`, `ValidTo`, `ConditionsJson`, `ActionType` (`Block|Restrict|Allow|Notify`), `ActionReason`. Solo `ADMIN`.

**5. `SimulateDumCheckQuery(decimal latitude, decimal longitude, DateTime date, string vehicleType, string euroClass)`**: usa la implementación `IDumZoneService` (creada en D-3) para evaluar si un punto con esos parámetros está restringido. Devuelve `DumSimulationResultDto` con `ActionType`, `Reason`, `ZoneCodes[]` y `ActiveRulesApplied[]`.

**CAPA WEB — `GreenTransit.Web/Components/Pages/DumZones/`**

**6. `DumZoneMap.razor`**: página con mapa Leaflet embebido vía JavaScript interop:
- Carga y renderiza todos los polígonos de `DumZones` coloreados por `ActionType` del la regla más restrictiva activa: Block=rojo semi-transparente, Restrict=naranja, Notify=azul, sin reglas=gris
- Panel lateral con lista de zonas. Al hacer clic en una zona del panel, el mapa hace zoom a ese polígono
- Botón "Nueva zona" (solo `ADMIN`): activa modo dibujo de polígono en Leaflet. Al completar el polígono, abre el formulario de creación con el `GeometryJson` ya relleno
- Al hacer clic en un polígono del mapa, muestra popup con `ZoneCode`, estado y lista de reglas activas con enlace al detalle

**7. `DumZoneForm.razor`**: formulario de alta/edición con campo `GeometryJson` editable como textarea (para carga manual de GeoJSON). Botón "Validar GeoJSON" que llama al validator antes de guardar.

**8. `DumSimulator.razor`**: formulario del simulador con campos: latitud, longitud (o selector de entidad del maestro para autocompletar coords), fecha y hora, tipo vehículo (select), clase Euro (select). Botón "Comprobar restricciones". Resultado mostrado en un panel con color según `ActionType`.

Restricciones: solo perfil `ADMIN` puede crear/editar zonas y reglas. El `GeometryJson` debe ser GeoJSON RFC 7946 válido.

---

### ⬜ E-3 — Energía de Planta + Factores de Emisión

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `src/GreenTransit.Domain/Entities/PlantEnergy.cs`, `src/GreenTransit.Domain/Entities/EmissionFactorSet.cs`, `src/GreenTransit.Domain/Entities/EmissionFactor.cs`

**Prompt**:

Implementa los módulos de consumo energético de plantas y gestión de factores de emisión en GreenTransit.

**SECCIÓN A — Energía de Planta (`GreenTransit.Application/Features/PlantEnergies/`)**

**1. `GetPlantEnergiesQuery`**: filtros `PlantCenterCode?`, `Year?`, `Month?`. Filtra por `OwnerId`.

**2. `CreatePlantEnergyCommand` / `UpdatePlantEnergyCommand`**: campos: `PlantName`, `PlantCenterCode`, `Year`, `Month`, `KwhTotal`, `Source?`, `GridMixRef?`, `AllocationMethod?`, `Notes?`. Validator: no puede existir ya un registro para el mismo `PlantCenterCode` + `Year` + `Month` + `OwnerId`.

**3. `GetPlantEnergySummaryQuery(string plantCenterCode, int year)`**: devuelve `PlantEnergySummaryDto` con consumo mensual (array de 12 valores), total anual `KwhTotal`, y `TotalCO2e` calculado multiplicando `KwhTotal` por el factor de red eléctrica configurable en `appsettings["PlantEnergy:GridEmissionFactor"]` (kgCO₂e/kWh, default 0.27 para España).

**4. `PlantEnergyList.razor`**: tabla mensual con 12 columnas de meses + columna total anual. Cada celda es editable inline (si el usuario tiene permiso). Selector de año y planta en la cabecera.

**SECCIÓN B — Factores de Emisión (`GreenTransit.Application/Features/EmissionFactors/`)**

**5. `GetEmissionFactorSetsQuery`**: lista todos los sets con `Status`, `ValidFrom`, `ValidTo`, `FactorSetName`, `Version`.

**6. `CreateEmissionFactorSetCommand`**: crea un nuevo set con todas sus líneas en una sola transacción. Campos del set: `FactorSetName`, `Version`, `ValidFrom`, `ValidTo`. Campos de cada línea: `VehicleType`, `FuelType`, `EuroClass`, `Unit`, `Value`.

**7. `ActivateEmissionFactorSetCommand(Guid setId)`**: marca el set seleccionado como `Status = "Active"` y pone todos los demás sets en `Status = "Inactive"`. Solo uno puede estar activo a la vez.

**8. `GetActiveEmissionFactorsQuery`**: devuelve el set activo con todas sus líneas. Para uso en el cálculo de emisiones.

**9. `EmissionFactorSetList.razor`**: tabla de sets con badge de estado. Botón "Activar" (con confirmación "¿Desactivar el set actual?"). Al hacer clic en un set, muestra panel lateral con la tabla de factores para previsualización antes de activar. Solo `ADMIN` puede crear/activar sets.

Restricciones: solo `ADMIN` gestiona factores de emisión. Perfil `PLANT_OP` registra energías de su propia planta (filtrado por `OwnerId`). El factor de red eléctrica debe ser configurable en `appsettings.json`.

---

## 📈 BLOQUE F — Reporting y Dashboard

---

### ⬜ F-1 — Dashboard operativo principal

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`, `src/GreenTransit.Web/Components/Pages/Dashboard.razor`

**Prompt**:

Implementa el Dashboard operativo principal de GreenTransit. El archivo `Dashboard.razor` ya existe, amplíalo o reemplázalo con la implementación completa.

**CAPA APPLICATION — `GreenTransit.Application/Features/Dashboard/`**

**1. `GetDashboardSummaryQuery`**: devuelve `DashboardSummaryDto` calculando todos los KPIs en paralelo con `Task.WhenAll`. Estructura del DTO:
- `WasteMovesByStatus`: `Dictionary<string, int>` — conteo de `WasteMoves` por `ServiceStatus` del `OwnerId` activo
- `KgCollectedThisMonth`: suma de `WasteMoveResidues.Weight` de traslados en estado `RECOGIDO` o posterior del mes en curso
- `KgTreatedThisMonth`: suma de `TreatmentPlantResidues.WeightTotal` del mes en curso
- `RecyclingRatePercent`: `Σ WeightValued (TreatmentOperation.IsRecycling=true)` / `Σ WeightTotal` × 100
- `EnergyRecoveryPercent`: `Σ WeightValued (TreatmentOperation.IsEnergyRecovery=true)` / `Σ WeightTotal` × 100
- `ReusePercent`: `Σ WeightReused (TreatmentOperation.IsPreparationForReuse=true)` / `Σ WeightTotal` × 100
- `TotalCO2ThisMonth`: suma de `WasteMoveResidues.TransportInfo_TransportCarbonEmissions` del mes en curso
- `CO2PreviousMonth`: mismo cálculo del mes anterior (para mostrar tendencia)
- `OpenIncidentsBySeverity`: `Dictionary<string, int>` — conteo de incidencias abiertas por severidad
- `MarketShareCompliance`: resultado de `GetMarketShareComplianceQuery` del año en curso (del widget C-3)
- `UpcomingPickups`: resultado de `GetUpcomingServiceOrdersQuery(7)` (próximos 7 días)
Filtra todo por `OwnerId`. La query debe completarse en menos de 2 segundos.

**2.** La query debe adaptar los datos según `ICurrentUserService.UserProfile`:
- `CARRIER`: `WasteMovesByStatus` solo cuenta traslados donde figura como `IdCarrier`; `UpcomingPickups` solo SOs asignadas a él
- `PLANT_OP`: `KgTreatedThisMonth` y tasas de reciclaje solo de su entidad (filtrado por `OwnerId`)
- `ADMIN` / `SCRAP`: todos los datos del `OwnerId`

**CAPA WEB — `src/GreenTransit.Web/Components/Pages/Dashboard.razor`**

**3.** Reemplaza el `Dashboard.razor` existente con la implementación completa. Instala el paquete NuGet `Blazor-ApexCharts` si no está ya. Usa `OnInitializedAsync` para la carga inicial y skeleton loaders mientras esperan los datos:

- **Embudo de traslados**: gráfico de barras horizontales con `WasteMovesByStatus`. Colores por estado: SOLICITADO=gris, PLANIFICADO=azul, RECOGIDO=amarillo, EN CAC=naranja claro, EN PLANTA=naranja, CLASIFICADO=verde, BLOQUEADO=rojo
- **Kg recogidos vs tratados**: gráfico de barras agrupadas de los últimos 6 meses
- **Tasas de tratamiento**: gráfico de dona con reciclaje/valorización/reutilización/rechazo
- **Card huella CO₂**: número grande con unidad kgCO₂e + flecha de tendencia (↑ rojo si aumentó, ↓ verde si bajó vs mes anterior)
- **Cards de incidencias**: 4 cards con contador por severidad usando `IncidentsSummaryWidget` (creado en E-1)
- **Cumplimiento MarketShares**: widget de barras de progreso por categoría usando el componente del prompt C-3
- **Próximas recogidas**: tabla de las 5 SOs más próximas con columnas Número, Punto de recogida, Fecha planificada, Prioridad
- **Mapa interactivo**: Leaflet.js embebido vía JavaScript interop mostrando puntos de las entidades activas del `OwnerId` (lat/lng de `BusinessEntity`) y polígonos de zonas DUM activas

Restricciones: skeleton loaders (divs con clase CSS `skeleton`) mientras cargan los datos. Gráficos ApexCharts con lazy loading. El mapa carga de forma diferida solo cuando el componente está visible (IntersectionObserver vía JS interop).

---

### ⬜ F-2 — Trazabilidad end-to-end + buscador global

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`

**Prompt**:

Implementa la trazabilidad end-to-end del residuo y el buscador global en GreenTransit.

**CAPA APPLICATION**

**1. `GlobalSearchQuery(string searchTerm)`** en `GreenTransit.Application/Features/Search/`:
Busca en paralelo con `Task.WhenAll` en los siguientes campos, todos filtrados por `OwnerId`:
- `ServiceOrders.ServiceOrderNumber`
- `WasteMoves.WasteMoveReference`
- `EntryPlants.TicketScale`
- `WasteMoveResidues.DINumber` y `WasteMoveResidues.NTNumber`
- `Agreements.AgreementNumber`
- `BusinessEntity.Name`, `BusinessEntity.NationalId`, `BusinessEntity.CenterCode`

Devuelve `GlobalSearchResultDto` con una lista por tipo de resultado (máximo 5 por tipo). Cada resultado tiene: `Id`, `DisplayText`, `SecondaryText`, `Type` (para el ícono), `NavigationUrl` (ruta a la ficha correspondiente). Si `searchTerm` tiene menos de 3 caracteres, devuelve vacío sin consultar DB.

**2. `GetResidueTraceabilityQuery`** en `GreenTransit.Application/Features/Reporting/`:
Parámetros: `string searchTerm` (busca por `DINumber`, `NTNumber`, `TicketScale` o `WasteMoveReference`).
Devuelve `ResidueTraceabilityDto` con:
- Datos de la `ServiceOrder` origen
- Datos del `WasteMove` + líneas de residuos
- Datos de `EntryCAC` si existe
- Datos de `EntryPlant` + líneas
- Datos de `TreatmentPlant` + líneas con fracciones
- `SettlementLines` vinculadas
- `Incidents` vinculados
- `TotalCO2Emissions`
Siempre filtra por `OwnerId`.

**CAPA WEB**

**3. Componente `GlobalSearchBar.razor`** en `GreenTransit.Web/Components/Shared/`:
- Input de texto con placeholder "Buscar traslado, DI, NT, entidad..."
- Debounce de 300ms antes de ejecutar la query
- Dropdown de resultados bajo el input, agrupados por tipo con icono diferenciador (🚛 Traslados, 📋 Órdenes, 🏭 Entidades, 💶 Acuerdos, 🎫 Tickets)
- Al hacer clic en un resultado, navega a `NavigationUrl` usando `NavigationManager`
- Tecla Escape cierra el dropdown. Flechas arriba/abajo para navegar resultados
- Integrar este componente en el Topbar del `MainLayout`

**4. `ResidueTraceability.razor`** en `GreenTransit.Web/Components/Pages/Reporting/`:
- Input de búsqueda por `DINumber`, `NTNumber`, `TicketScale` o `WasteMoveReference`
- Al buscar, muestra el timeline del expediente reutilizando el componente `WasteMoveTimeline` (creado en D-8)
- Botón "Exportar expediente PDF" usando `QuestPDF`
- Botón "Exportar XML" que serializa el `ResidueTraceabilityDto` a XML estándar

Restricciones: todos los resultados del buscador global pertenecen siempre al `OwnerId` del usuario autenticado. El buscador está en el topbar y es accesible desde cualquier página.

---

### ⬜ F-3 — KPIs regulatorios y exportación XLSX

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`

**Prompt**:

Implementa el módulo de KPIs regulatorios y reporting en GreenTransit.

**CAPA APPLICATION — `GreenTransit.Application/Features/Reporting/`**

**1. `GetRegulatoryKpisQuery`**: parámetros: `IdScrap?`, `AutonomousCommunity?`, `Year` (obligatorio), `Quarter?` (1-4), `Category?`. Filtra por `OwnerId`. Calcula y devuelve `RegulatoryKpisDto`:
- `RecyclingRatePercent`: `Σ WeightValued` (donde `TreatmentOperation.IsRecycling=true`) / `Σ WeightTotal` × 100
- `ReusePreparationPercent`: `Σ WeightReused` (donde `IsPreparationForReuse=true`) / `Σ WeightTotal` × 100
- `MarketShareComplianceList`: resultado de `GetMarketShareComplianceQuery` filtrado
- `CO2IntensityKgPerTon`: `Σ TransportCarbonEmissions` / (`Σ WeightTotal` / 1000)
- `TotalWeightKg`: `Σ WeightTotal` del periodo
- `TotalTransportsCount`: nº de `WasteMoves` en estado `CLASIFICADO` del periodo
- `ByQuarter`: array de los 4 trimestres con los KPIs anteriores (para gráficos históricos)

**2. `ExportKpisToExcelQuery`**: misma lógica que `GetRegulatoryKpisQuery`. Devuelve `byte[]` con un fichero XLSX generado con `ClosedXML`. El Excel tiene tres hojas: "Resumen", "Por Categoría", "Histórico Trimestral". Registra el tipo de paquete en el `ContentType`: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.

**CAPA WEB — `GreenTransit.Web/Components/Pages/Reporting/`**

**3. `RegulatoryKpis.razor`**:
- Filtros: selector de SCRAP (si es `ADMIN`), CCAA, año, trimestre, categoría
- Cards de KPI: Tasa Reciclaje, Tasa Reutilización, Intensidad CO₂, Total kg tratados. Cada card muestra el valor actual + objetivo normativo configurable (ver punto 5)
- Gráficos históricos por trimestre con ApexCharts: líneas para tasas de reciclaje y valorización
- Tabla de cumplimiento de `MarketShares` reutilizando el componente del prompt C-3
- Botón "Exportar XLSX" que llama a `ExportKpisToExcelQuery` y descarga el fichero con `IJSRuntime`

**4. `DocumentRepository.razor`**:
- Lista todos los documentos del `OwnerId` activo: `AgreementDocuments`, documentos de `WasteMoves` (`DocumentId`/`DocumentHash`) y evidencias de `Settlements` (`EvidenceRefsJson`)
- Columnas: Tipo, Referencia, Fecha, Hash de integridad, Estado firma
- Botón "Verificar hash" por fila: muestra si el hash almacenado coincide con un hash recalculado o si ha podido ser manipulado
- Filtros: tipo de documento, fecha, referencia

**5.** Los objetivos normativos (% mínimo de reciclaje, % mínimo de valorización) deben ser configurables. Crea una tabla `RegulatoryTargets` en DB (si no existe) con campos: `OwnerId`, `Category`, `Year`, `MinRecyclingPercent`, `MinReusePercent`. Si no hay configuración para el `OwnerId`, usa los valores por defecto de `appsettings["RegulatoryTargets:DefaultMinRecyclingPercent"]` y `appsettings["RegulatoryTargets:DefaultMinReusePercent"]`.

Restricciones: acceso restringido a perfiles `ADMIN`, `SCRAP` y `PUBLIC_ENT`. El XLSX nunca se almacena en servidor, se genera en memoria y se devuelve directamente como stream.

---

## 👥 BLOQUE G — Seguridad y Usuarios

---

### ⬜ G-1 — Gestión de Usuarios, Perfiles y Credenciales SharePoint

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`, `src/GreenTransit.Domain/Entities/Security.cs`, `src/GreenTransit.Application/Common/Interfaces/ICurrentUserService.cs`

**Prompt**:

Implementa el módulo de gestión de usuarios, perfiles y credenciales SharePoint en GreenTransit. Las entidades `User`, `Profile` y `UserSharePointCredential` ya están en el dominio (en `Security.cs`).

**CAPA APPLICATION — `GreenTransit.Application/Features/Security/`**

Perfiles:

**1. `GetProfilesQuery`**: lista todos los perfiles con `ID`, `Reference`, `Description`. Sin filtro `OwnerId` (los perfiles son del sistema). Solo lectura, sin CRUD desde UI.

Usuarios:

**2. `GetUsersQuery`**: paginada. Filtros: `IdProfile?`, `IsActive?` (campo a añadir si no existe en la entidad), `SearchTerm?` (busca en `Login` y `Email`). Filtra por `OwnerId`. Solo perfil `ADMIN` ejecuta esta query.

**3. `GetUserByIdQuery`**: devuelve `UserDetailDto` con todos los campos más `ProfileReference`, nombres de País/CCAA/Municipio resueltos y `LinkedEntityName` (nombre de la `BusinessEntity` donde `IdUser` apunta a este usuario, si existe).

**4. `CreateUserCommand`**: campos: `Login`, `Email`, `IdProfile`, `OwnerId` (heredado del admin autenticado), `NationalId?` (FK Country.id), `GeographicalId?` (FK TerritoryState.id), `MunicipalityId?` (FK Municipality.Id), `PortalEDCProvider?`, `PortalEDCConsumer?`. Validator: `Login` único por `OwnerId`.

**5. `UpdateUserCommand`**: actualiza todos los campos excepto `OwnerId`. Si cambia `IdProfile`, loguea el cambio con `Serilog` a nivel `Warning` incluyendo el perfil anterior y el nuevo.

**6. `DeactivateUserCommand(Guid userId)`**: bloquea el acceso. Implementa esto añadiendo un campo `IsActive` a la entidad `User` si no existe (con migración EF Core).

**7. `LinkUserToEntityCommand(Guid userId, Guid entityId)`**: vínculo lógico. Actualiza `BusinessEntity.IdUser = userId` y loguea la operación.

Credenciales SharePoint:

**8. `UpsertSharePointCredentialCommand(Guid userId)`**: crea o actualiza la credencial activa del usuario. Campos: `TenantId`, `ClientId`, `ClientSecret`. El `ClientSecret` debe almacenarse cifrado usando `IDataProtector` de ASP.NET Core Data Protection. Desactiva (`IsActive = false`) cualquier credencial anterior del mismo usuario antes de crear/activar la nueva.

**9. `TestSharePointConnectionCommand(Guid userId)`**: obtiene la credencial activa del usuario, descifra el `ClientSecret`, intenta obtener un token de autenticación de SharePoint Online con `Microsoft.Identity.Client` (MSAL). Devuelve `SharePointTestResultDto { Success, ErrorMessage? }`. Solo accesible por el propio usuario o un `ADMIN`.

**CAPA WEB — `GreenTransit.Web/Components/Pages/Security/`**

**10. `UserList.razor`**: tabla con filtros, columna "Perfil" con badge coloreado por referencia, columna "Entidad vinculada" con enlace a la ficha de `BusinessEntity`. Acciones: Ver, Editar, Desactivar. Solo accesible para `ADMIN`.

**11. `UserForm.razor`**: formulario con:
- Datos básicos: Login, Email, Perfil (selector de `Profiles`)
- Geografía: componente `GeographySelector` reutilizable (del prompt B-3)
- Integración EDC: campos `PortalEDCProvider` y `PortalEDCConsumer` en sección colapsable "Interoperabilidad"
- Sección "Integración SharePoint" colapsable: campos `TenantId`, `ClientId`, `ClientSecret` (input de tipo `password`, muestra `••••••••` si ya tiene credencial guardada, con botón "Cambiar credencial" para habilitar edición) y botón "Probar conexión" que llama a `TestSharePointConnectionCommand`

**12. `UserDetail.razor`**: ficha de detalle con botón "Ir a Entidad vinculada" (visible si `LinkedEntityName` no es nulo) y sección de credenciales SharePoint con botón "Probar conexión" y resultado del último test.

Restricciones: solo perfil `ADMIN` accede al módulo de usuarios. `ClientSecret` NUNCA se devuelve en las queries (solo escritura). En queries y DTOs, el campo `ClientSecret` no existe. `OwnerId` del usuario creado siempre es el `OwnerId` del `ADMIN` autenticado.

---

## 🏁 BLOQUE H — Calidad y Finalización

---

### ⬜ H-1 — Tests de integración y cobertura

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `tests/GreenTransit.Tests/Helpers/TestDbContextFactory.cs`, `tests/GreenTransit.Tests/Helpers/FakeCurrentUserService.cs`

**Prompt**:

Amplía la suite de tests en `GreenTransit.Tests` con tests de integración para los flujos críticos del sistema.

**1. `WasteFlowIntegrationTests.cs`** en `GreenTransit.Tests/Integration/`:
Test end-to-end que recorre el flujo completo:
- Crea una `ServiceOrder` (verifica que se genera `ServiceOrderNumber`)
- Crea un `WasteMove` desde esa SO (verifica `ServiceStatus = "SOLICITADO"`)
- Planifica el traslado (verifica `ServiceStatus = "PLANIFICADO"`, que `DumZoneService` devuelve `Allow` con datos de prueba sin zonas)
- Confirma recogida (verifica `ServiceStatus = "RECOGIDO"`, verifica que se disparó el cálculo de emisiones)
- Crea `EntryPlant` (verifica `ServiceStatus = "EN PLANTA"`, verifica `NetWeight` calculado)
- Crea `TreatmentPlant` con balance correcto (verifica `ServiceStatus = "CLASIFICADO"`)
- Intenta transición inválida (p.ej. volver a `SOLICITADO`) y verifica que lanza excepción

**2. `SettlementCalculationTests.cs`** en `GreenTransit.Tests/Application/Settlements/`:
Setup: crea un `Agreement` activo con `TariffRulesJson` simple (una regla por categoría), crea `EntryPlants` del periodo con `EntryPlantResidues`. Ejecuta `GenerateSettlementCommand`. Verifica: `SettlementLines` correctas, `BaseAmount` calculado correctamente, `TotalAmount = BaseAmount × 1.21` (IVA).

**3. `MultiTenantIsolationTests.cs`** en `GreenTransit.Tests/Infrastructure/`:
Crea registros de `ServiceOrders` para dos `OwnerId` distintos en la misma DB InMemory. Ejecuta `GetServiceOrdersQuery` con `FakeCurrentUserService` configurado para el tenant A. Verifica que el resultado solo contiene registros del tenant A y ninguno del tenant B.

**4. `DumZoneServiceTests.cs`**: ya descrito en el prompt D-3. Añadir aquí si no se creó antes.

**5. `MassBalanceValidationTests.cs`** en `GreenTransit.Tests/Application/TreatmentPlants/`:
Tests del balance de masas: dentro de tolerancia, fuera de tolerancia (1.5%), exactamente en el límite (1.0%).

**6.** Configuración de cobertura de código:
- Añade el paquete `coverlet.collector` al proyecto `GreenTransit.Tests` si no está
- Crea `Directory.Build.props` en la raíz del repositorio con umbral mínimo de cobertura del 70% en líneas y ramas
- Añade un script `run-tests-with-coverage.ps1` en la raíz: ejecuta `dotnet test --collect:"XPlat Code Coverage"` y luego genera el reporte HTML con `reportgenerator`

Restricciones: no usar base de datos real, solo InMemory EF Core. Los tests deben pasar con `dotnet test` sin configuración adicional del entorno.

---

### ⬜ H-2 — Notificaciones en tiempo real con SignalR

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `src/GreenTransit.Web/Program.cs`

**Prompt**:

Implementa el sistema de notificaciones en tiempo real para GreenTransit usando SignalR.

**CAPA DOMAIN/APPLICATION**

**1.** Crea la entidad `Notification` en `GreenTransit.Domain/Entities/` con campos: `Id` (Guid PK), `OwnerId` (Guid), `UserId?` (Guid, null = broadcast al tenant), `Type` (string), `Title` (string), `Message` (string), `Severity` (string: Info/Warning/Error), `RelatedEntityId?` (Guid), `RelatedEntityType?` (string), `NavigationUrl?` (string), `IsRead` (bool, default false), `CreatedAt` (DateTime UTC). Añade el `DbSet` en `AppDbContext` y genera la migración.

**2. Interfaz `INotificationService`** en `GreenTransit.Application/Common/Interfaces/`:
- `NotifyUserAsync(Guid userId, NotificationDto notification, CancellationToken ct)` — notifica a un usuario específico
- `NotifyTenantAsync(Guid ownerId, NotificationDto notification, CancellationToken ct)` — notifica a todos los usuarios del tenant

**CAPA INFRASTRUCTURE**

**3. Hub `NotificationHub`** en `GreenTransit.Web/Hubs/` (o en Infrastructure): hereda de `Hub`. Los clientes se unen al grupo de su `OwnerId` al conectar (`Groups.AddToGroupAsync`).

**4. Implementación `SignalRNotificationService`** en `GreenTransit.Infrastructure/Services/`: usa `IHubContext<NotificationHub>` para enviar al grupo correspondiente y persiste la notificación en la tabla `Notifications`.

**5.** Registra `AddSignalR()` en `Program.cs` y mapea el hub en `/hubs/notifications`.

**6.** Los siguientes command handlers deben disparar notificaciones (añade la llamada a `INotificationService` al final del handler, después del `SaveChangesAsync`):
- `ConfirmPickupCommandHandler` → `NotifyTenantAsync` al `OwnerId` del `IdDestination`: "Traslado {WasteMoveReference} en camino. Llegada estimada: {PlannedDeliveryStart}"
- `CreateEntryPlantCommandHandler` → `NotifyTenantAsync` al `OwnerId` del `IdScrap` del `WasteMove`: "Nueva entrada pesada: {NetWeight:N0} kg en {TicketScale}"
- `OpenIncidentCommandHandler` (si `Severity ∈ {"High","Critical"}`) → `NotifyTenantAsync`: "⚠️ Incidencia {Severity} abierta: {Description truncada a 80 chars}"
- `GetExpiringAgreementsQuery` → se llama desde un `IHostedService` diario que dispara `NotifyTenantAsync` para acuerdos que vencen en 30, 7 y 1 días

**CAPA WEB**

**7. Componente `NotificationBell.razor`** en `GreenTransit.Web/Components/Shared/`:
- Badge con contador de notificaciones no leídas del usuario autenticado
- Dropdown al hacer clic mostrando las últimas 10 notificaciones, agrupadas por fecha
- Al hacer clic en una notificación: la marca como leída y navega a `NavigationUrl` si existe
- Botón "Marcar todas como leídas"
- Se suscribe al hub SignalR para recibir notificaciones en tiempo real sin recargar

**8. Servicio `ToastService`** en `GreenTransit.Web/Services/` con métodos `ShowSuccess`, `ShowError`, `ShowWarning`, `ShowInfo`. Componente `ToastContainer.razor` en `GreenTransit.Web/Components/Shared/` que renderiza los toasts en la esquina inferior derecha con auto-dismiss a los 5 segundos.

Restricciones: las notificaciones solo se envían a usuarios del mismo `OwnerId`. `NotificationBell` en el topbar del `MainLayout`. El `IHostedService` del paso 6 se registra con `AddHostedService`.

---

### ⬜ H-3 — Seed de datos iniciales + configuración de producción

**Archivos a adjuntar**: `COPILOT_CONTEXT.md`, `src/GreenTransit.Web/Program.cs`, `src/GreenTransit.Infrastructure/Persistence/AppDbContext.cs`, `src/GreenTransit.Web/appsettings.json`

**Prompt**:

Implementa el seed de datos iniciales y la configuración de despliegue en producción para GreenTransit.

**CAPA INFRASTRUCTURE**

**1. `DbInitializer`** en `GreenTransit.Infrastructure/Persistence/`:
Método estático `InitializeAsync(IServiceProvider serviceProvider)`. Solo ejecuta si `appsettings["Seed:RunOnStartup"] = true`. Usa `IServiceScopeFactory` para resolver el `AppDbContext` en un scope propio. Para cada conjunto de datos, comprueba si ya existen antes de insertar (idempotente):

- **Perfiles** (`Profiles`): inserta los 8 perfiles estándar si la tabla está vacía:
  `ADMIN`, `SCRAP`, `PRODUCER`, `CARRIER`, `PLANT_OP`, `CAC_OP`, `PUBLIC_ENT`, `COORDINATOR` con sus descripciones

- **Estados de documento** (`DocStates`): Borrador, Emitido, Firmado, Validado, Rechazado

- **Operaciones R/D** (`TreatmentOperations`): inserta R1–R13 y D1–D15 si la tabla está vacía. Incluye las descripciones oficiales resumidas de la Directiva 2008/98/CE. Establece los flags `IsRecycling`, `IsEnergyRecovery`, `IsPreparationForReuse` correctamente: R2/R3/R4/R5/R6/R7/R8/R9=IsRecycling, R1=IsEnergyRecovery, R2=IsPreparationForReuse

- **Usuario ADMIN inicial**: crea un registro en `Users` si no existe ningún usuario con `IdProfile = ADMIN`:
  `Login` = valor de `appsettings["Seed:AdminLogin"]` (default "admin"),
  `Email` = valor de `appsettings["Seed:AdminEmail"]`,
  `OwnerId` = valor de `appsettings["Seed:DefaultOwnerId"]` (Guid)

- **EmissionFactorSet por defecto**: si no existe ningún set activo, crea uno con nombre "EU Standard 2024" con factores medios estándar para las combinaciones más comunes (Diesel+EuroVI+Rigid, Diesel+EuroV+Rigid, CNG+EuroVI+Rigid, Electric+NA+Van). Valores aproximados en kgCO₂e/km según HBEFA 4.1.

**2.** En `Program.cs`, llama a `DbInitializer.InitializeAsync(app.Services)` dentro del bloque de startup, justo después de las migraciones automáticas.

**3.** Actualiza `appsettings.json` añadiendo la sección:
"Seed": { "RunOnStartup": false, "AdminLogin": "admin", "AdminEmail": "", "DefaultOwnerId": "" }

En `appsettings.Development.json` sobreescribe `"Seed:RunOnStartup": true`.

**4.** Crea el script PowerShell `run-seed.ps1` en la raíz del repositorio:
El script acepta parámetros `-AdminEmail`, `-DefaultOwnerId`. Sobreescribe temporalmente `appsettings.Development.json` con `Seed:RunOnStartup=true` y los valores proporcionados, ejecuta `dotnet run --project src/GreenTransit.Web --launch-profile Development`, y restaura el archivo original.

Restricciones: el seed es completamente idempotente (ejecutar dos veces no duplica datos). No sobreescribe modificaciones manuales del ADMIN en catálogos. En producción `RunOnStartup` siempre es `false` por defecto.

---


## 📝 NOTAS DE USO

### Convención de estados en prompts

Actualiza la columna Estado del índice conforme avances:
- `⬜` Pendiente
- `🔄` En progreso
- `✅` Completado
- `⚠️` Completado con ajustes manuales necesarios
- `❌` Fallido — relanzar con más contexto

### Cómo relanzar un prompt fallido

Si Copilot genera código incorrecto o incompleto:
1. Adjunta el archivo `.cs` de la entidad de dominio relevante
2. Adjunta el archivo de configuración EF Core del contexto si hay errores de mapeo
3. Añade al inicio del prompt: "El intento anterior falló porque: [describe el problema]. Corrígelo teniendo en cuenta: [especifica lo que falta]"

### Orden recomendado de ejecución por sprint

- **Sprint 1**: A-1, A-2, A-3, A-4
- **Sprint 2**: B-1, B-2, B-3
- **Sprint 3**: D-1, D-2, D-3
- **Sprint 4**: D-4, D-5, D-6, D-7
- **Sprint 5**: C-1, C-2, E-1
- **Sprint 6**: D-8, F-1, F-2
- **Sprint 7**: C-3, E-2, E-3, F-3
- **Sprint 8**: G-1, H-1, H-2, H-3