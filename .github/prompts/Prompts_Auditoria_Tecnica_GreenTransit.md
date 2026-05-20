# 🔍 Prompts de Auditoría Técnica — GreenTransit
> **Propósito**: Secuencia de prompts para GitHub Copilot orientados a detectar errores, problemas de rendimiento, fallos arquitectónicos, riesgos de concurrencia y deuda técnica en la plataforma GreenTransit.
>
> **Stack**: .NET 10 · Blazor Web App · Clean Architecture · EF Core · MediatR · FluentValidation · SQL Server Azure · OpenID Connect · ApexCharts · ClosedXML
>
> **Instrucciones de uso**: Ejecuta cada prompt en una sesión nueva de Copilot Chat. Adjunta siempre `COPILOT_CONTEXT.md`, `README.md` y `Crear_BD_v4_1.sql`. En cada prompt se indica qué archivos adicionales adjuntar.

---

## BLOQUE 1 — ARQUITECTURA Y CLEAN ARCHITECTURE

### Prompt A-1 — Violaciones de dependencias entre capas

```
Contexto del proyecto: GreenTransit (adjunta COPILOT_CONTEXT.md y README.md).
Stack: Clean Architecture con 5 proyectos (Domain, Application, Infrastructure, Web, Tests).

TAREA: Analiza todas las referencias entre proyectos y detecta violaciones de la regla de dependencia de Clean Architecture.

Busca específicamente:
1. Referencias desde Domain hacia Application, Infrastructure o Web.
2. Referencias desde Application hacia Infrastructure o Web.
3. Importaciones directas de EF Core (DbContext, DbSet, IQueryable sobre entidades EF) en la capa Application — solo se permiten interfaces (IApplicationDbContext).
4. Uso de clases concretas de Infrastructure en handlers de MediatR (ej. instanciar repositorios directamente sin interfaz).
5. Componentes Blazor (capa Web) que llamen directamente a repositorios o al DbContext sin pasar por MediatR.
6. Entidades de dominio que tengan atributos de EF Core ([Column], [Table], [Key], etc.) — deben estar en las configuraciones de Infrastructure.

Para cada violación encontrada, indica:
- Archivo y línea exacta.
- Capa origen y capa destino incorrecta.
- Corrección propuesta siguiendo el patrón ya establecido en el proyecto.

Adjunta todos los archivos .csproj de los 5 proyectos y una muestra de handlers existentes.
```

---

### Prompt A-2 — Análisis del patrón CQRS con MediatR

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md y una selección de Query handlers y Command handlers existentes.

TAREA: Audita la implementación del patrón CQRS con MediatR y detecta antipatrones.

Busca:
1. **Queries con efectos secundarios**: handlers de tipo IRequestHandler<Query, T> que modifiquen estado (escrituras en BD, actualizaciones de entidades, envío de eventos).
2. **Commands que devuelven entidades de dominio completas** en lugar de IDs o Result DTOs — viola el principio de separación lectura/escritura.
3. **N+1 en handlers**: consultas que iteren en C# y lancen queries adicionales por cada elemento en lugar de hacer joins en SQL (ej. foreach con await _dbContext.X.FindAsync dentro del bucle).
4. **Handlers que carguen colecciones completas en memoria** antes de filtrar: busca patrones como `.ToListAsync()` seguido de `.Where(...)` en C# en lugar de `.Where(...).ToListAsync()`.
5. **MediatR pipelines (behaviors) ausentes**: verifica si existen behaviors para logging, validación con FluentValidation, manejo de excepciones y transacciones. Si no existen, señala el riesgo.
6. **Commands sin validador FluentValidation asociado**: cada command debe tener su correspondiente AbstractValidator<TCommand>.
7. **Queries sin paginación**: cualquier query que devuelva listas sin parámetros de paginación (Skip/Take) sobre tablas operativas grandes (WasteMoves, ServiceOrders, EntryPlants, etc.).

Para cada problema, proporciona el fragmento de código con el error y la versión corregida.
```

---

### Prompt A-3 — Revisión del modelo de dominio y entidades

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md, Crear_BD_v4_1.sql y los archivos de entidades de Domain/Entities/.

TAREA: Compara las entidades de dominio C# con el esquema SQL v4.1 y detecta desincronizaciones y problemas de diseño.

Busca:
1. **Propiedades faltantes**: campos definidos en SQL que no existen en la entidad C# correspondiente (pueden causar pérdida silenciosa de datos al guardar).
2. **Tipos incorrectos**: mismatches entre tipo SQL y tipo C# (ej. decimal(18,3) en SQL mapeado como double en C# — riesgo de pérdida de precisión; nvarchar(max) en SQL mapeado como string sin restricción de longitud en EF).
3. **Relaciones de navegación incompletas**: FKs definidas en SQL sin navigation property en la entidad C# correspondiente (pueden provocar cartesian explosion en queries con Include).
4. **Entidades sin campo OwnerId** que deberían tenerlo según las reglas multi-tenant del proyecto (tablas operativas: WasteMoves, ServiceOrders, Agreements, Settlements, EntryPlants, EntryCACs, TreatmentPlants, Incidents).
5. **Campos Version y Hash ausentes** en entidades que los requieren por integridad (Agreements, Settlements, ServiceOrders, WasteMoves según el modelo v4.1).
6. **Constructores incorrectos**: entidades con constructor público sin parámetros que permiten crear instancias en estado inválido — evalúa si deben tener factory methods o constructores con parámetros obligatorios.
7. **Enumerados representados como string en SQL** pero no validados en dominio: busca campos como State, EntityRole, ResidueType, FuelType sin constantes o enum equivalente en dominio.

Genera un informe tabulado con: Entidad | Campo | Problema | Severidad (Alta/Media/Baja) | Corrección propuesta.
```

---

## BLOQUE 2 — RENDIMIENTO Y BASE DE DATOS

### Prompt B-1 — Auditoría de queries EF Core y problemas de rendimiento

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md, Crear_BD_v4_1.sql y los archivos de Query handlers de Application/Features/.

TAREA: Analiza todas las consultas EF Core y detecta problemas de rendimiento.

Busca específicamente:
1. **Cartesian explosion**: uso de múltiples .Include().ThenInclude() sobre colecciones — en EF Core esto genera JOINs que multiplican filas. Identifica queries con más de 2 niveles de Include sobre relaciones 1:N.
2. **Carga de columnas innecesarias**: queries que no usan .Select(new DTO {...}) y cargan entidades completas cuando solo se necesitan 2-3 campos (especialmente sobre tablas con campos nvarchar(max) como CompositionJson, PotentialLERCodesJson).
3. **Missing AsNoTracking()**: queries de solo lectura (en handlers de tipo Query) que no usan .AsNoTracking() — en Blazor Server esto acumula entidades tracked innecesariamente en el ChangeTracker durante la vida del circuito.
4. **Filtros aplicados en memoria en lugar de en SQL**: patrones .ToListAsync() antes del .Where(), o .Where() con lógica C# no traducible a SQL (métodos custom, ToString(), etc.).
5. **Queries sin índices evidentes**: filtros frecuentes sobre columnas sin índice en SQL (OwnerId + IdScrap, IdIssuedBy, State, IdCarrier en WasteMoveResidues). Contrasta con los índices definidos en Crear_BD_v4_1.sql.
6. **Paginación ineficiente**: uso de .Skip(n).Take(m) sin un .OrderBy() previo — SQL Server no garantiza orden sin ORDER BY, además de que genera planes de ejecución ineficientes.
7. **Subconsultas en bucles (N+1)**: cualquier await dentro de un foreach o un Select(async x => ...) no consolidado.
8. **Uso de .Count() donde basta .AnyAsync()**: para verificar existencia, .AnyAsync() es más eficiente que .CountAsync() > 0.

Para cada problema: archivo, método, fragmento de código, impacto estimado y query EF Core corregida.
```

---

### Prompt B-2 — Auditoría del esquema SQL y configuraciones EF Core

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md, Crear_BD_v4_1.sql y los archivos de Infrastructure/Persistence/Configurations/.

TAREA: Analiza las configuraciones EF Core (IEntityTypeConfiguration<T>) y el esquema SQL y detecta problemas.

Busca:
1. **Índices SQL sin equivalent en EF Core**: índices definidos en el SQL (CREATE INDEX) que no están declarados en las configuraciones EF Core con .HasIndex() — si se regenera la BD desde migraciones, los índices críticos se perderán.
2. **Precisión decimal no configurada**: campos decimal en SQL con (18,3) o similar que en EF Core no tienen .HasPrecision(18,3) explícito — EF Core por defecto usa (18,2), lo que puede truncar datos.
3. **Longitudes de string sin restricción**: propiedades string sin .HasMaxLength() que corresponden a nvarchar(N) en SQL — EF Core las genera como nvarchar(max) en migraciones.
4. **Relaciones sin DeleteBehavior configurado**: FKs que por defecto en EF Core pueden ser Cascade cuando deberían ser Restrict o SetNull (ej. eliminar una Entidad no debe eliminar en cascada sus WasteMoves).
5. **Columnas computadas o con defaults SQL no mapeadas**: campos con DEFAULT SYSUTCDATETIME() en SQL que EF Core no conoce (.HasDefaultValueSql()) y podría sobreescribir con null.
6. **Configuraciones duplicadas o en conflicto**: propiedades configuradas tanto con DataAnnotations en la entidad como con Fluent API en la configuración — Fluent API debe ser la única fuente de verdad.
7. **DbSets innecesarios expuestos**: tablas de diccionario o de solo lectura expuestas como DbSet<T> de escritura sin protección — evalúa si deben ser entidades de solo lectura (Keyless entity types).

Genera un informe por tabla con los problemas detectados y las correcciones EF Core correspondientes.
```

---

### Prompt B-3 — Análisis del filtro multi-tenant y riesgos de fuga de datos

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md, AppDbContext.cs, ICurrentUserService.cs y todos los Query handlers.

TAREA: Audita el mecanismo multi-tenant (filtrado por OwnerId) y detecta posibles fugas de datos entre tenants.

Busca:
1. **Queries sin filtro OwnerId**: cualquier query sobre tablas operativas (WasteMoves, ServiceOrders, Agreements, Settlements, EntryPlants, EntryCACs, TreatmentPlants, Incidents, ProductDeclaration) que no aplique el filtro OwnerId — ya sea por QueryFilter global en EF Core o explícitamente en el handler.
2. **IgnoreQueryFilters() usado sin justificación**: llamadas a .IgnoreQueryFilters() fuera de contextos administrativos o de seed — cada uso debe estar documentado y justificado.
3. **ICurrentUserService.OwnerId que pueda devolver null o Guid.Empty**: analiza si hay paths de código donde OwnerId no esté inicializado y si eso se propaga a queries sin el filtro.
4. **Endpoints o rutas Blazor sin [Authorize]**: páginas que puedan ser accedidas sin autenticación y que carguen datos operativos.
5. **DTOs que exponen el OwnerId al cliente**: verifica que los DTOs de respuesta no incluyan el campo OwnerId innecesariamente — es información de infraestructura interna.
6. **Filtrado por perfil (LinkedEntityId) incompleto**: para los perfiles SCRAP, COORDINATOR, PRODUCER, CARRIER, PLANT_OP y PUBLIC_ENT, verifica que los handlers aplican el filtro secundario por LinkedEntityId según las reglas del Mapa de Autorización (además del filtro base por OwnerId).
7. **Datos de catálogos compartidos mal protegidos**: catálogos sin OwnerId (LERCodes, TreatmentOperations) que por error tengan filtros tenant aplicados, o viceversa — tablas operativas sin filtro.

Para cada fuga potencial: riesgo (Crítico/Alto/Medio), datos expuestos, handler afectado y corrección.
```

---

## BLOQUE 3 — CONCURRENCIA Y ESTADO EN BLAZOR SERVER

### Prompt C-1 — Problemas de concurrencia en Blazor Server

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md y los componentes Blazor de Web/Components/Pages/.

TAREA: Analiza los componentes Blazor Server y detecta problemas de concurrencia y gestión del estado.

Blazor Server usa un modelo de circuito por usuario donde los componentes son instancias de larga vida (hasta que el circuito se cierra). Esto crea riesgos específicos:

Busca:
1. **DbContext de larga vida**: inyección de AppDbContext o repositorios como Singleton o compartidos entre requests — en Blazor Server el DbContext debe ser Scoped (un circuito = un scope, pero no compartido entre peticiones concurrentes dentro del mismo circuito).
2. **Llamadas async sin InvokeAsync en eventos UI**: métodos async en event handlers de Blazor que modifiquen estado del componente sin estar envueltos en InvokeAsync cuando son llamados desde callbacks externos (timers, SignalR, eventos de background).
3. **StateHasChanged() llamado fuera del hilo del circuito**: notificaciones de cambio de estado desde threads de background sin pasar por InvokeAsync(StateHasChanged).
4. **Servicios Scoped compartidos entre componentes de forma no segura**: servicios Scoped que mantengan estado mutable accedido simultáneamente por múltiples componentes en el mismo circuito.
5. **Disposición incorrecta de recursos**: componentes que implementen IDisposable/IAsyncDisposable pero no desuscriban event handlers (delegados, eventos de servicios), causando memory leaks en el circuito.
6. **CancellationToken no usado en llamadas async desde componentes**: llamadas MediatR sin pasar el CancellationToken del componente (que se cancela al navegar fuera del componente) — deja queries corriendo innecesariamente.
7. **Renderizado en bucle**: componentes con OnParametersSetAsync o OnAfterRenderAsync que llamen a StateHasChanged() incondicionalmente, causando renderizados infinitos.
8. **Colecciones compartidas sin thread-safety**: listas o diccionarios de instancia en componentes modificados en callbacks async sin sincronización.

Para cada problema: componente, método, fragmento de código, riesgo y corrección.
```

---

### Prompt C-2 — Gestión de ciclo de vida de componentes y memory leaks

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md y los componentes Blazor de mayor complejidad (layouts, dashboards, formularios).

TAREA: Analiza el ciclo de vida de los componentes Blazor y detecta memory leaks y problemas de disposición.

Busca:
1. **Suscripciones a eventos no canceladas en Dispose**: componentes que suscriban a eventos de servicios (NavMenuStateService, IPagePermissionService, etc.) en OnInitializedAsync pero no tengan un IDisposable/IAsyncDisposable que los desuscriba.
2. **Timers sin Dispose**: uso de System.Threading.Timer, PeriodicTimer o Task.Delay en bucle sin cancelación garantizada cuando el componente se destruye.
3. **Referencias a objetos JS (IJSObjectReference) sin DisposeAsync**: invocación de módulos JS (Leaflet, ApexCharts via JS interop) sin liberar la referencia al objeto JS al destruir el componente.
4. **HttpClient o servicios desechables instanciados con new** dentro de componentes en lugar de inyectarlos — no serán gestionados por el contenedor DI.
5. **EventCallback que capture this sin WeakReference**: lambdas capturadas en EventCallback que retengan referencias fuertes al componente padre desde componentes hijos de larga vida.
6. **Componentes que no llamen a base.Dispose()**: clases que hereden de ComponentBase o cualquier clase con Dispose y no llamen al Dispose del padre.
7. **CancellationTokenSource no disposed**: creación de CancellationTokenSource en OnInitializedAsync sin Dispose en el IDisposable del componente.

Para cada problema encontrado: componente afectado, fragmento de código, tipo de leak y corrección con implementación de IAsyncDisposable correcta.
```

---

### Prompt C-3 — Concurrencia en Commands y gestión de transacciones

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md, AppDbContext.cs y los Command handlers de Application/Features/.

TAREA: Analiza los Command handlers y detecta problemas de concurrencia, transacciones y manejo de conflictos optimistas.

Busca:
1. **Commands sin control de concurrencia optimista**: entidades con campo Version (Agreements, Settlements, ServiceOrders, WasteMoves) que no usen .IsConcurrencyToken() en EF Core — al guardar cambios concurrentes, el último en escribir sobrescribe silenciosamente al anterior.
2. **Validación y escritura sin atomicidad**: commands que lean datos para validar (ej. "comprobar que no existe un Agreement duplicado") y luego escriban, con ventana de tiempo entre ambas operaciones donde otro hilo podría insertar el mismo registro (race condition) — sin transacción explícita o manejo de DbUpdateException.
3. **Múltiples SaveChangesAsync en un único handler**: handlers que llamen a SaveChangesAsync más de una vez — si la segunda falla, los cambios de la primera ya están persistidos y no hay rollback.
4. **Falta de manejo de DbUpdateConcurrencyException**: commands sobre entidades con Version que no capturen DbUpdateConcurrencyException y la gestionen (reintentar, devolver conflicto al usuario).
5. **Operaciones de borrado con ExecuteDeleteAsync sin transacción**: uso de ExecuteDeleteAsync o ExecuteUpdateAsync (que bypass el ChangeTracker) en combinación con otros SaveChangesAsync en el mismo handler — inconsistencia si alguno falla.
6. **Commands idempotentes no garantizados**: operaciones que deberían ser idempotentes (como cambios de estado) pero que no comprueban el estado actual antes de aplicar el cambio — aplicar dos veces el mismo command puede corromper datos.
7. **Ausencia de transacciones en operaciones multi-tabla**: commands que modifiquen varias tablas en secuencia sin BeginTransactionAsync — en caso de fallo parcial, la BD queda en estado inconsistente.

Para cada problema: handler afectado, fragmento de código, escenario de fallo y corrección con transacción/concurrencia correcta.
```

---

## BLOQUE 4 — SEGURIDAD Y AUTORIZACIÓN

### Prompt D-1 — Auditoría del sistema de autorización y políticas

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md, PolicyConstants.cs, Program.cs (registro de policies), ICurrentUserService.cs y los archivos de páginas Blazor.

TAREA: Audita el sistema de autorización y detecta vulnerabilidades y bypasses.

Busca:
1. **Páginas Blazor sin atributo [Authorize]**: componentes con @page que carguen o modifiquen datos sin ningún atributo de autorización — un usuario no autenticado podría acceder si navega directamente a la URL.
2. **Autorización solo en UI (no en handler)**: páginas con [Authorize] pero cuyos Command/Query handlers no verifican el perfil del usuario — si alguien llama a la API/MediatR directamente, no hay protección en la capa Application.
3. **Checks de perfil hardcodeados en componentes Blazor**: if (user.IsInRole("ADMIN")) o comparaciones directas de strings de perfil fuera del sistema PageDefinitions/PagePermissions — viola la regla de autorización dinámica del proyecto.
4. **Gestión de LinkedEntityId sin validar**: handlers que usen ICurrentUserService.LinkedEntityId como filtro sin verificar que no sea null — para perfiles que requieren LinkedEntityId (SCRAP, PRODUCER, CARRIER, etc.), un valor null causaría devolver todos los datos del tenant o una excepción.
5. **Claims no validados tras ClaimsTransformation**: verifica que ClaimsTransformation maneje correctamente el caso en que el usuario no exista en la tabla Users (usuario autenticado en OIDC pero no provisionado en la BD) — ¿qué perfil se asigna? ¿Puede acceder a datos?
6. **Exposición de IDs internos en URLs**: rutas como /waste-moves/{id} donde el ID es un Guid — verifica que al cargar el detalle, el handler valide que el registro pertenece al OwnerId del usuario (no solo que el Guid exista).
7. **Falta de validación de pertenencia en Commands de actualización/borrado**: commands como UpdateWasteMoveCommand o DeleteAgreementCommand que reciben un Id pero no verifican que ese Id pertenece al tenant del usuario antes de modificar.

Para cada vulnerabilidad: página/handler afectado, vector de ataque, severidad (Crítica/Alta/Media) y corrección.
```

---

### Prompt D-2 — Seguridad en manejo de datos externos y exportaciones

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md y los handlers de exportación (XLSX, PDF) y cualquier integración externa.

TAREA: Analiza el manejo de datos externos, exportaciones y posibles vectores de inyección.

Busca:
1. **Inyección en queries EF Core**: concatenación de strings de usuario directamente en FromSqlRaw() o ExecuteSqlRaw() sin parametrización — busca cualquier uso de FromSqlRaw o ExecuteSqlRaw y verifica que todos los valores variables sean parámetros.
2. **Formula injection en exportaciones XLSX (CSV Injection)**: campos de texto libre (Nombre de entidad, Referencia, Descripción) exportados a Excel mediante ClosedXML sin sanear prefijos peligrosos (=, +, -, @) que Excel interpreta como fórmulas.
3. **Exposición de información sensible en logs**: Serilog configurado para loguear objetos completos (commands, queries) que contengan datos personales (NIF/CIF, emails, coordenadas GPS) sin anonimización — revisar configuración de Destructuring.
4. **Path traversal en subida/descarga de documentos**: si el sistema gestiona documentos adjuntos, verifica que los nombres de archivo no se usen directamente en rutas del sistema de ficheros sin sanitización.
5. **Validación insuficiente en FluentValidation**: commands que reciban campos JSON (ContainersJson, CompositionJson, PotentialLERCodesJson, MaterialsJson) sin validar que el contenido sea JSON válido antes de persistir — puede causar errores en runtime al deserializar.
6. **Tamaño de payload no limitado**: commands que acepten campos nvarchar(max) sin límite de longitud en el validador — un cliente malicioso podría enviar megabytes de datos en un solo campo.
7. **Anti-forgery tokens en formularios Blazor**: verifica que las operaciones de escritura (commands) no puedan ser ejecutadas mediante CSRF — en Blazor Server esto está mitigado por el circuito SignalR, pero si hay endpoints API REST adicionales, deben tener protección.

Para cada riesgo: vector, componente afectado, severidad y mitigación propuesta.
```

---

## BLOQUE 5 — VALIDACIÓN Y MANEJO DE ERRORES

### Prompt E-1 — Auditoría de validaciones con FluentValidation

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md y todos los archivos AbstractValidator<T> de Application/Features/.

TAREA: Analiza los validadores FluentValidation y detecta validaciones incompletas o incorrectas.

Busca:
1. **Commands sin validador asociado**: para cada Command (implementa IRequest), verifica que existe un AbstractValidator<TCommand> registrado. Si el ValidationBehavior de MediatR está configurado, commands sin validador pasarán sin validación alguna.
2. **Validaciones de Guid.Empty no realizadas**: campos de tipo Guid que sean FKs obligatorias (IdScrap, IdProducer, IdCarrier) sin la regla .NotEqual(Guid.Empty) — Guid.Empty es un valor "no vacío" técnicamente, pero semánticamente inválido.
3. **Validaciones cruzadas ausentes**: commands que modifiquen relaciones entre entidades sin validar que la entidad referenciada existe y pertenece al mismo OwnerId (ej. al crear un WasteMove, verificar que el IdScrap pertenece al tenant).
4. **Reglas de negocio en handlers en lugar de validadores**: lógica del tipo "si State == X, el campo Y es obligatorio" implementada en el handler con if/throw en lugar de en el validador con RuleFor.When() — dificulta el testing y la retroalimentación al usuario.
5. **Validadores sin mensaje de error en español**: mensajes de error en inglés (por defecto de FluentValidation) que llegarán al usuario final — la aplicación es para el mercado español.
6. **Validaciones de longitud de string que no coinciden con HasMaxLength de EF Core**: campo con .HasMaxLength(256) en EF Core pero el validador permite .MaximumLength(512) o no tiene límite — los datos pasarán validación pero fallarán al persistir.
7. **Reglas async innecesariamente pesadas**: MustAsync que lancen queries a BD por cada campo validado en lugar de agrupar la validación en una sola query.

Genera tabla: Command | Validador presente | Problemas encontrados | Corrección propuesta.
```

---

### Prompt E-2 — Manejo global de errores y resiliencia

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md, Program.cs y los MediatR Pipeline Behaviors existentes.

TAREA: Analiza el manejo de errores y la resiliencia de la aplicación.

Busca:
1. **Ausencia de ExceptionHandlingBehavior en MediatR pipeline**: si no existe un behavior que capture excepciones no controladas de handlers y las convierta en Result/ProblemDetails, las excepciones de BD o de dominio llegarán como excepciones no controladas a Blazor, mostrando pantallas de error genéricas.
2. **Try-catch que silencian excepciones**: bloques catch {} vacíos o que solo loguean pero devuelven datos incorrectos o null sin informar al usuario — busca patrones catch (Exception ex) { _logger.LogError(ex, ...); return null; } en handlers.
3. **DbUpdateException no manejada**: operaciones de escritura sin capturar DbUpdateException — errores de violación de constraint (FK, UNIQUE) llegarán como excepciones genéricas sin mensaje útil al usuario.
4. **Falta de circuit breaker para Azure SQL**: en un entorno Azure SQL Basic, los throttling y timeouts transitorios son frecuentes — verifica si EF Core está configurado con EnableRetryOnFailure() para SQL Azure.
5. **Timeouts de query no configurados**: CommandTimeout por defecto de EF Core (30 segundos en SQL Server) puede ser insuficiente para queries de reporting sobre tablas grandes — y excesivo para queries de UI interactiva. Verifica si hay configuración diferenciada.
6. **Errores de OIDC no manejados**: flujo de autenticación (login, logout, token refresh) sin manejo de errores cuando el proveedor OIDC externo no está disponible.
7. **Páginas Blazor sin ErrorBoundary**: componentes de UI complejos (dashboards, mapas) sin <ErrorBoundary> que aísle los fallos — un error en un widget de ApexCharts puede tumbar toda la página.

Para cada problema: componente/behavior afectado, escenario de fallo, impacto en usuario y corrección.
```

---

## BLOQUE 6 — RENDIMIENTO EN BLAZOR Y FRONTEND

### Prompt F-1 — Rendimiento de componentes Blazor y renderizado

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md y los componentes Blazor de dashboards y listas (RadzenDataGrid, tablas, filtros).

TAREA: Analiza el rendimiento de los componentes Blazor y detecta problemas de renderizado excesivo y carga de datos.

Busca:
1. **Grids que cargan todos los datos en memoria**: RadzenDataGrid o tablas que llamen a MediatR y carguen la lista completa antes de paginar en cliente — para WasteMoves, ServiceOrders o EntryPlants con miles de registros, esto es inaceptable. Las grids deben usar server-side paging pasando Skip/Take al handler.
2. **Componentes sin ShouldRender() override**: componentes que reciban parámetros por @Parameter y re-rendericen en cada ciclo aunque los parámetros no hayan cambiado — considera implementar ShouldRender() o comparación de parámetros.
3. **Cascading parameters innecesarios**: uso de [CascadingParameter] para valores que no cambian frecuentemente (OwnerId, perfil de usuario) en lugar de inyectarlos directamente vía DI — los cascading parameters disparan re-renderizado en todos los descendientes cuando cambian.
4. **Llamadas a MediatR en OnParametersSetAsync sin debounce**: filtros de búsqueda que disparan una query al servidor en cada pulsación de tecla sin debounce — para un campo de texto libre, cada letra lanza una query a SQL Azure.
5. **Componentes de gráfico (ApexCharts) sin virtualización de datos**: gráficos que renderizan series de tiempo con cientos de puntos sin agregación previa — considera agregar en servidor (por día/semana/mes) antes de enviar al componente.
6. **JS Interop síncrono bloqueante**: llamadas a IJSRuntime.InvokeVoidAsync() en el hilo de renderizado sin await correcto, o llamadas síncronas (JSRuntime.InvokeVoid) que bloquean el circuito.
7. **IMemoryCache no usado para datos de catálogo**: catálogos de referencia (LERCodes, TreatmentOperations, geografía) que se consultan en cada renderizado sin caché — estos datos cambian raramente y deben cachearse.

Para cada problema: componente, impacto en UX y corrección con código.
```

---

### Prompt F-2 — Análisis de la configuración de DI y lifetime de servicios

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md y Program.cs completo.

TAREA: Analiza el registro de servicios en el contenedor DI y detecta problemas de lifetime.

Busca:
1. **Captive dependency (Singleton → Scoped)**: servicios registrados como Singleton que inyecten servicios Scoped (ej. ICurrentUserService, DbContext) — el Singleton captura la instancia Scoped del primer request y la reutiliza para todos, causando datos de tenant incorrectos.
2. **Scoped services en contextos Singleton**: verifica que no haya IHostedService o BackgroundService que inyecten directamente servicios Scoped — deben usar IServiceScopeFactory.CreateScope().
3. **DbContext registrado como Singleton o Transient**: AppDbContext debe ser Scoped. Singleton causaría compartición de estado entre usuarios; Transient causaría múltiples contextos por operación con pérdida de Unit of Work.
4. **ICurrentUserService sin acceso correcto a HttpContext**: en Blazor Server, IHttpContextAccessor puede ser null después del primer render (el contexto HTTP solo existe durante el handshake inicial). Verifica que ICurrentUserService use el mecanismo correcto para Blazor Server (AuthenticationStateProvider o CascadingAuthenticationState, no HttpContext).
5. **MediatR handlers registrados como Singleton**: si los handlers están registrados manualmente como Singleton (no con AddMediatR automático), pueden capturar scopes incorrectos.
6. **IMemoryCache sin límite de tamaño**: caché registrada sin SizeLimit — en un servicio Azure de memoria limitada, puede causar presión de memoria y GC frecuente.
7. **Servicios duplicados registrados**: verifica que no haya llamadas a AddScoped<IFoo, Foo>() duplicadas que registren la misma interfaz varias veces — MediatR y algunas librerías pueden registrar handlers múltiples veces si se llaman varias veces los métodos de extensión de registro.

Para cada problema: servicio afectado, lifetime actual, lifetime correcto y el riesgo concreto en producción.
```

---

## BLOQUE 7 — TESTING Y CALIDAD DE CÓDIGO

### Prompt G-1 — Auditoría de cobertura de tests y calidad

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md y los archivos de GreenTransit.Tests/.

TAREA: Analiza la suite de tests y detecta gaps críticos de cobertura y antipatrones de testing.

Busca:
1. **Ausencia de tests para lógica de filtrado multi-tenant**: verifica que existan tests que comprueben que un handler con OwnerId_A nunca devuelve datos de OwnerId_B.
2. **Ausencia de tests para filtrado por perfil**: tests que verifiquen que un usuario con perfil SCRAP solo ve sus datos (IdScrap = LinkedEntityId), y que un perfil ADMIN ve todo el tenant.
3. **Tests que usan base de datos real en lugar de InMemory o SQLite**: tests de integración conectados a SQL Server real — frágiles, lentos y con dependencias externas.
4. **Mocks excesivos**: tests que mockean todo (incluido el DbContext completo) para probar lógica que solo depende de transformaciones de datos — si todo está mockeado, el test no verifica nada real.
5. **Ausencia de tests para transiciones de estado**: máquinas de estado (WasteMove states, ProductDeclaration states, ServiceOrder states) sin tests que verifiquen transiciones válidas e inválidas.
6. **Tests sin Assert (solo comprueban que no lanza excepción)**: tests con solo Act sin Assert explícito — no verifican el resultado concreto.
7. **Tests de validadores FluentValidation incompletos**: validadores sin tests para casos límite (Guid.Empty, string vacío, longitud máxima+1, valores negativos).
8. **Ausencia de tests de concurrencia**: ningún test que simule dos handlers ejecutándose concurrentemente sobre la misma entidad (versión optimista).

Genera un plan de tests prioritario: área | tipo de test | caso de prueba | prioridad (Alta/Media/Baja).
```

---

### Prompt G-2 — Revisión de código y deuda técnica general

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md y una selección representativa de handlers, componentes y configuraciones.

TAREA: Revisión general de calidad de código y detección de deuda técnica acumulada.

Busca:
1. **Magic strings y magic numbers sin constantes**: strings de estado ("Borrador", "Emitido", "SCRAP", "CARRIER", etc.) repetidos en múltiples archivos en lugar de usar las constantes definidas en Domain (ProfileConstants, ServiceOrderStatuses, etc.).
2. **Código comentado (dead code)**: bloques de código comentados que deberían eliminarse o extraerse — indican código no terminado o lógica alternativa abandonada.
3. **TODO/HACK/FIXME sin ticket**: comentarios TODO sin referencia a issue — deuda técnica invisible que no se gestionará nunca.
4. **Métodos con más de 30 líneas en handlers**: handlers que mezclan lógica de consulta, transformación y autorización en un solo método largo — candidatos a refactorizar con métodos privados o servicios de dominio.
5. **Duplicación de lógica de filtrado entre handlers**: el mismo bloque switch/case para filtrado por perfil (SCRAP → IdScrap, COORDINATOR → Agreements, etc.) copiado en múltiples handlers — debe extraerse a IDataScopeService para no divergir.
6. **Inconsistencia de nomenclatura**: mezcla de español e inglés en nombres de variables, parámetros o DTOs en el mismo archivo o módulo.
7. **Ausencia de cancellation token propagation**: métodos async que reciben CancellationToken pero no lo pasan a todas las llamadas async internas (EF Core queries, llamadas a servicios).
8. **Configuración hardcodeada en código**: URLs, timeouts, límites o parámetros de negocio embebidos en handlers o componentes en lugar de leerse de IConfiguration/appsettings.json.

Genera informe con: problema | archivos afectados | impacto en mantenibilidad | refactor propuesto.
```

---

## BLOQUE 8 — INFRAESTRUCTURA Y CONFIGURACIÓN

### Prompt H-1 — Configuración de Azure SQL Basic y estrategia de conexión

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md, Program.cs y la configuración de EF Core.

TAREA: Analiza la configuración de la conexión a Azure SQL Basic y detecta problemas de resiliencia y configuración.

Azure SQL Basic es el tier más limitado (5 DTU, 2GB). Tiene throttling frecuente, conexiones limitadas y timeouts bajo carga. Busca:

1. **EnableRetryOnFailure no configurado**: EF Core con SQL Server debe tener .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null) para manejar errores transitorios de Azure SQL.
2. **Connection pool no dimensionado**: sin configuración explícita de Min/Max Pool Size en la connection string — Azure SQL Basic puede rechazar conexiones si el pool se agota.
3. **CommandTimeout demasiado alto o bajo**: queries de reporting sobre tablas grandes pueden necesitar más de 30 segundos; queries de UI deberían fallar rápido. Verifica si hay configuración de timeout diferenciada.
4. **Migrations aplicadas en startup**: si el código tiene context.Database.MigrateAsync() en el startup de producción, una migración larga puede causar timeout del health check y reinicio del pod.
5. **Connection string con credenciales en appsettings.json**: verifica que la connection string de producción use Managed Identity o Azure Key Vault, no usuario/contraseña embebidos en el archivo de configuración.
6. **Ausencia de health checks**: verifica si existe un health check endpoint (/health) que compruebe la conexión a SQL Azure — sin él, el balanceador de carga no puede detectar instancias no saludables.
7. **Logging de queries EF Core en producción**: verifica que el LogLevel para Microsoft.EntityFrameworkCore.Database.Command esté en Warning o superior en producción — logear todas las queries en producción en Azure SQL Basic puede generar miles de líneas por minuto y agotar el storage de logs.

Para cada problema: riesgo en producción, configuración actual y configuración correcta con código.
```

---

### Prompt H-2 — Configuración de autenticación OIDC y seguridad de tokens

```
Contexto: GreenTransit — adjunta COPILOT_CONTEXT.md, Program.cs (configuración OIDC) y AccountController.cs.

TAREA: Audita la configuración de autenticación OpenID Connect y detecta problemas de seguridad y configuración.

Busca:
1. **Validación de token no configurada correctamente**: verifica que TokenValidationParameters incluya ValidateIssuer: true, ValidateAudience: true, ValidateLifetime: true — sin estas validaciones, tokens expirados o de otros emisores podrían ser aceptados.
2. **RefreshToken no gestionado**: si el Access Token expira (típicamente en 1 hora), la aplicación debe usar el Refresh Token para renovarlo silenciosamente. Verifica si existe lógica de refresh o si el usuario se desautentica al expirar el token.
3. **Datos de claims sensibles cacheados sin expiración**: si ICurrentUserService cachea claims en IMemoryCache sin TTL, un usuario cuyo perfil cambia en BD seguirá viendo el perfil antiguo indefinidamente.
4. **Logout sin revocación de sesión OIDC**: el logout local (borrar cookie) sin hacer end_session_endpoint en el proveedor OIDC — el usuario sigue autenticado en el Identity Provider y puede reloguear sin credenciales.
5. **Cookie de autenticación sin configuración de seguridad**: verifica que la cookie tenga HttpOnly: true, Secure: true, SameSite: Strict o Lax.
6. **Ausencia de validación de state parameter en OIDC**: el parámetro state previene CSRF en el flujo de autorización — verifica que la librería OIDC de .NET lo gestiona correctamente (normalmente sí, pero puede estar desactivado).
7. **Secrets OIDC (ClientSecret) en appsettings.json**: el ClientSecret del flujo OIDC no debe estar en el archivo de configuración — debe venir de Azure Key Vault o variables de entorno de la infraestructura.

Para cada problema: riesgo de seguridad, configuración actual y configuración corregida.
```

---

## ÍNDICE DE USO RÁPIDO

| Bloque | Prompt | Área | Archivos a adjuntar |
|--------|--------|------|---------------------|
| A | A-1 | Violaciones capas Clean Architecture | `.csproj` de los 5 proyectos + handlers |
| A | A-2 | Antipatrones CQRS/MediatR | Handlers de Application/Features/ |
| A | A-3 | Modelo de dominio vs SQL | Domain/Entities/ + Crear_BD_v4_1.sql |
| B | B-1 | Rendimiento queries EF Core | Query handlers de Application/ |
| B | B-2 | Esquema SQL vs configuraciones EF | Infrastructure/Persistence/Configurations/ |
| B | B-3 | Fugas multi-tenant | AppDbContext.cs + ICurrentUserService.cs + handlers |
| C | C-1 | Concurrencia Blazor Server | Web/Components/Pages/ |
| C | C-2 | Memory leaks componentes | Componentes de dashboards y formularios |
| C | C-3 | Concurrencia en Commands | Command handlers + AppDbContext.cs |
| D | D-1 | Vulnerabilidades de autorización | PolicyConstants.cs + páginas Blazor |
| D | D-2 | Seguridad datos externos | Handlers de exportación + integraciones |
| E | E-1 | Validaciones FluentValidation | Todos los AbstractValidator<T> |
| E | E-2 | Manejo de errores y resiliencia | Program.cs + Pipeline Behaviors |
| F | F-1 | Rendimiento componentes Blazor | Grids, dashboards, filtros |
| F | F-2 | Lifetime de servicios DI | Program.cs completo |
| G | G-1 | Cobertura de tests | GreenTransit.Tests/ |
| G | G-2 | Deuda técnica general | Muestra representativa de todo el código |
| H | H-1 | Azure SQL Basic y conexión | Program.cs + configuración EF |
| H | H-2 | Seguridad OIDC y tokens | Program.cs + AccountController.cs |

---

> **Nota**: Ejecuta los prompts del Bloque B (rendimiento) y Bloque D (seguridad) con prioridad máxima — son los que mayor impacto tienen en producción para una aplicación multi-tenant en Azure SQL Basic.
