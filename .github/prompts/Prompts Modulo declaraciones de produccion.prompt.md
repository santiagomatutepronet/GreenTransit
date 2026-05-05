# 🤖 Prompts para GitHub Copilot — Implementación del Módulo de Declaraciones de Producción

> Secuencia ordenada de prompts para implementar paso a paso el módulo de Declaraciones de Producción en GreenTransit.
>
> **Stack asumido**: .NET 8+ · Clean Architecture (Domain / Application / Infrastructure / Web) · Blazor Server · Entity Framework Core · SQL Server Azure · MediatR · FluentValidation.
>
> **Instrucciones de uso**: ejecuta cada prompt en orden. Antes de cada prompt, asegúrate de que el paso anterior compila y funciona. Adjunta siempre como contexto los ficheros `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades_GreenTransit.md`, `Mapa_Autorizacion_GreenTransit.md` y los archivos referenciados en cada paso.

---

## FASE 1 — CAPA DE DOMINIO

### Prompt 1.1 — Entidad de dominio `ProductDeclaration`

```
Contexto: estoy implementando el módulo de Declaraciones de Producción en GreenTransit.
Usa el proyecto existente con Clean Architecture (Domain / Application / Infrastructure / Web).

Crea la entidad de dominio `ProductDeclaration` en `Domain/Entities/ProductDeclaration.cs` con estas propiedades mapeadas a la tabla SQL existente `ProductDeclaration`:

- Id (Guid, PK)
- OwnerId (Guid?)
- Period (int?)
- Year (int?)
- Month (int?)
- Currency (string?, max 256)
- State (string?, max 64)
- DateCreate (DateTime?)
- DateEmit (DateTime?)
- Reference (string?, max 256)
- IdProducer (Guid?) — FK → Entities
- Amount (decimal?)
- Type (string?, max 256)
- DateCreateSys (DateTime?)
- DateModifiedSys (DateTime?)
- IdUser (int)

Navegación:
- Producer → Entity (navigation property, tipo Entity)
- Products → ICollection<Product> (one-to-many)

Sigue el patrón de las entidades existentes en el proyecto (p.ej. Agreement, ServiceOrder).
Incluye constantes para los estados: Draft = "Borrador", Issued = "Emitido", Validated = "Validado", Rejected = "Rechazado".
```

### Prompt 1.2 — Entidad de dominio `Product` (línea de declaración)

```
Crea la entidad de dominio `Product` en `Domain/Entities/Product.cs` mapeada a la tabla SQL `Products`:

- Id (Guid, PK)
- IdProductDeclaration (Guid) — FK → ProductDeclaration
- IdResidue (Guid?) — FK → Residues (ResidueType='Product')
- Reference (string?, max 512)
- Source (string?, max 512)
- Quantity (decimal?)
- MeasureUnit (string?, max 64)
- Units (int?)
- Price (decimal?)

Navegación:
- ProductDeclaration → ProductDeclaration (parent)
- Residue → Residue (navigation property, tipo Residue)

Sigue el mismo patrón de estilo que la entidad ProductDeclaration creada en el paso anterior.
```

### Prompt 1.3 — Entidades de diccionarios `dicProductDeclaration*`

```
Crea las entidades de dominio para los 6 diccionarios de declaraciones de producción.
Cada diccionario tiene la misma estructura (Id int, Ref varchar(128), description nvarchar(max)):

1. `DicProductDeclarationCategory` → tabla `dicProductDeclarationCategory`
2. `DicProductDeclarationPeriods` → tabla `dicProductDeclarationPeriods`
3. `DicProductDeclarationProducts` → tabla `dicProductDeclarationProducts` (tiene además CategoryId int? → FK a dicProductDeclarationCategory)
4. `DicProductDeclarationSource` → tabla `dicProductDeclarationSource`
5. `DicProductDeclarationType` → tabla `dicProductDeclarationType`
6. `DicProductDeclarationUse` → tabla `dicProductDeclarationUse`

Ubícalas en `Domain/Entities/Dictionaries/` o donde estén los otros diccionarios del proyecto.
Sigue el patrón de las entidades de diccionario existentes.
```

### Prompt 1.4 — Servicio de dominio `ProductDeclarationStateService`

```
Crea un servicio de dominio `ProductDeclarationStateService` en `Domain/Services/ProductDeclarationStateService.cs` que gestione las transiciones de estado de ProductDeclaration.

Transiciones permitidas:
- Borrador → Emitido (requiere: al menos 1 Product asociado, IdProducer informado, Year y Period informados)
- Emitido → Validado (sin precondiciones adicionales de negocio, solo verificar que State == "Emitido")
- Emitido → Rechazado (requiere: motivo de rechazo no vacío)
- Rechazado → Borrador (siempre permitido)

El servicio debe:
- Recibir la ProductDeclaration con sus Products cargados.
- Validar la transición y lanzar DomainException si no es válida.
- Actualizar State, DateEmit (al emitir), DateModifiedSys.
- Devolver la entidad modificada.

Usa las constantes de estado definidas en ProductDeclaration.
```

---

## FASE 2 — CAPA DE INFRAESTRUCTURA (EF Core)

### Prompt 2.1 — Configuración EF Core para `ProductDeclaration` y `Product`

```
Crea las configuraciones de Entity Framework Core para ProductDeclaration y Product:

1. `Infrastructure/Persistence/Configurations/ProductDeclarationConfiguration.cs`
   - Mapea a tabla "ProductDeclaration"
   - Configura PK, propiedades con max lengths según el SQL
   - Configura FK: IdProducer → Entities (con HasOne/WithMany)
   - Configura relación 1:N con Products (HasMany/WithOne)
   - Filtra por OwnerId (query filter multi-tenant si existe este patrón en el proyecto)

2. `Infrastructure/Persistence/Configurations/ProductConfiguration.cs`
   - Mapea a tabla "Products"
   - Configura PK, propiedades
   - Configura FK: IdProductDeclaration → ProductDeclaration
   - Configura FK: IdResidue → Residues

3. Configuraciones para los 6 diccionarios dicProductDeclaration* (pueden ir en un solo archivo si son simples).

4. Registra los DbSet<> correspondientes en el DbContext existente del proyecto.

Sigue el patrón de las configuraciones existentes (p.ej. AgreementConfiguration, ServiceOrderConfiguration).
```

---

## FASE 3 — CAPA DE APLICACIÓN (CQRS con MediatR)

### Prompt 3.1 — DTOs de ProductDeclaration

```
Crea los DTOs para el módulo de Declaraciones de Producción en `Application/Features/ProductDeclarations/DTOs/`:

1. `ProductDeclarationDto` — DTO de lectura para listado:
   - Id, OwnerId, Period, Year, Month, Currency, State, DateCreate, DateEmit, Reference, IdProducer, ProducerName (string, resuelto desde Entities.Name), Amount, Type, DateCreateSys, DateModifiedSys

2. `ProductDeclarationDetailDto` — DTO de lectura para detalle (extiende el anterior):
   - Incluye: Products (List<ProductDto>), ProducerNationalId, ProducerCenterCode

3. `ProductDto` — DTO de lectura para línea de producto:
   - Id, IdProductDeclaration, IdResidue, ResidueName (string, resuelto desde Residues.Name), ResidueReference (desde Residues.Reference), ResidueCategory (desde Residues.ProductCategory), Reference, Source, Quantity, MeasureUnit, Units, Price

4. `CreateProductDeclarationCommand` — DTO de escritura para crear cabecera:
   - IdProducer (Guid), Year (int), Period (int), Month (int?), Type (string), Currency (string?), Reference (string?)

5. `UpdateProductDeclarationCommand` — DTO de escritura para actualizar cabecera:
   - Id (Guid), Year (int?), Period (int?), Month (int?), Type (string?), Currency (string?), Reference (string?)

6. `CreateProductCommand` — DTO para crear línea:
   - IdProductDeclaration (Guid), IdResidue (Guid), Reference (string?), Source (string?), Quantity (decimal), MeasureUnit (string?), Units (int?), Price (decimal?)

7. `UpdateProductCommand` — DTO para actualizar línea:
   - Id (Guid), IdResidue (Guid?), Reference (string?), Source (string?), Quantity (decimal?), MeasureUnit (string?), Units (int?), Price (decimal?)

Sigue el patrón de DTOs existente en el proyecto (p.ej. Features/Agreements/DTOs o Features/ServiceOrders/DTOs).
```

### Prompt 3.2 — Query: Listado de Declaraciones con filtros

```
Crea la query `GetProductDeclarationsQuery` en `Application/Features/ProductDeclarations/Queries/GetProductDeclarationsQuery.cs` usando MediatR.

Input (filtros):
- Year (int?)
- Period (int?)
- State (string?)
- IdProducer (Guid?) — para ADMIN; para PRODUCER se inyecta automáticamente
- Type (string?)
- DateFrom (DateTime?)
- DateTo (DateTime?)
- Page (int, default 1)
- PageSize (int, default 20)

Output: PaginatedList<ProductDeclarationDto>

Lógica del handler:
1. Obtener el perfil del usuario actual desde ICurrentUserService.
2. Si el perfil es PRODUCER → forzar IdProducer = CurrentUser.LinkedEntityId (ignorar el valor del input).
3. Si el perfil es SCRAP → filtrar IdProducer IN (productores adheridos a acuerdos del SCRAP). Cruzar con Agreements.IdScrap.
4. Aplicar filtros opcionales (Year, Period, State, Type, DateFrom/DateTo).
5. Ordenar por DateCreateSys DESC.
6. Include: Entities (Producer) para resolver ProducerName.
7. Proyectar a ProductDeclarationDto.
8. Paginar.

Sigue el patrón de las queries existentes (p.ej. GetServiceOrdersQuery, GetAgreementsQuery).
```

### Prompt 3.3 — Query: Detalle de Declaración

```
Crea la query `GetProductDeclarationDetailQuery` en `Application/Features/ProductDeclarations/Queries/GetProductDeclarationDetailQuery.cs`.

Input: Id (Guid)

Output: ProductDeclarationDetailDto (incluye Products con datos resueltos de Residues)

Lógica del handler:
1. Buscar ProductDeclaration por Id con Include de:
   - Producer (Entities) para nombre, NIF, centro
   - Products con Include de Residue para nombre, referencia, categoría
2. Verificar visibilidad según perfil:
   - PRODUCER: solo si IdProducer == LinkedEntityId
   - SCRAP: solo si el productor está adherido a sus acuerdos
   - ADMIN: sin restricción
3. Proyectar a ProductDeclarationDetailDto.
4. Si no encontrado o sin permiso → lanzar NotFoundException.

Sigue el patrón existente de queries de detalle.
```

### Prompt 3.4 — Command: Crear Declaración

```
Crea el command `CreateProductDeclarationCommand` y su handler en `Application/Features/ProductDeclarations/Commands/CreateProductDeclarationCommand.cs`.

Input: CreateProductDeclarationCommand (IdProducer, Year, Period, Month?, Type, Currency?, Reference?)

Output: Guid (el Id de la declaración creada)

Lógica del handler:
1. Verificar autorización: solo PRODUCER (su propia entidad) o ADMIN.
2. Si es PRODUCER → forzar IdProducer = LinkedEntityId.
3. Validar que no existe otra declaración activa (no RECHAZADO) con mismo IdProducer + Year + Period + Type.
4. Crear ProductDeclaration con:
   - Id = Guid.NewGuid()
   - OwnerId = CurrentUser.OwnerId
   - State = "Borrador"
   - DateCreate = DateTime.UtcNow
   - DateCreateSys = DateTime.UtcNow
   - DateModifiedSys = DateTime.UtcNow
   - IdUser = CurrentUser.Id
5. Persistir y devolver Id.

Crea también el validator con FluentValidation:
- Year: obligatorio, entre 2020 y 2100
- Period: obligatorio
- Type: obligatorio, no vacío
- IdProducer: obligatorio
```

### Prompt 3.5 — Command: Actualizar Declaración

```
Crea el command `UpdateProductDeclarationCommand` y su handler en `Application/Features/ProductDeclarations/Commands/UpdateProductDeclarationCommand.cs`.

Input: UpdateProductDeclarationCommand (Id, Year?, Period?, Month?, Type?, Currency?, Reference?)

Output: void (o Unit)

Lógica del handler:
1. Buscar la declaración por Id.
2. Verificar que el State es "Borrador" o "Rechazado" (solo se puede editar en estos estados).
3. Verificar autorización: PRODUCER solo si es su declaración; ADMIN siempre.
4. Actualizar solo los campos informados (partial update).
5. Actualizar DateModifiedSys y IdUser.
6. Persistir.

Validator:
- Id: obligatorio
- Year: si se informa, entre 2020 y 2100
```

### Prompt 3.6 — Command: Añadir línea de producto

```
Crea el command `AddProductToDeclarationCommand` y su handler en `Application/Features/ProductDeclarations/Commands/AddProductToDeclarationCommand.cs`.

Input: IdProductDeclaration (Guid), IdResidue (Guid), Reference (string?), Source (string?), Quantity (decimal), MeasureUnit (string?), Units (int?), Price (decimal?)

Output: Guid (el Id del Product creado)

Lógica:
1. Buscar la ProductDeclaration por IdProductDeclaration.
2. Verificar que State es "Borrador" o "Rechazado".
3. Verificar autorización (PRODUCER solo su declaración, ADMIN).
4. Validar que IdResidue existe en Residues con ResidueType = "Product".
5. Crear Product con Id = Guid.NewGuid().
6. Recalcular Amount de la cabecera = Σ(Quantity × Price) de todas las líneas con precio.
7. Persistir.

Validator:
- IdProductDeclaration: obligatorio
- IdResidue: obligatorio
- Quantity: obligatorio, > 0
```

### Prompt 3.7 — Command: Eliminar línea de producto

```
Crea el command `RemoveProductFromDeclarationCommand` en `Application/Features/ProductDeclarations/Commands/RemoveProductFromDeclarationCommand.cs`.

Input: Id (Guid del Product a eliminar)

Output: void

Lógica:
1. Buscar el Product por Id, incluir su ProductDeclaration.
2. Verificar que la declaración está en "Borrador" o "Rechazado".
3. Verificar autorización.
4. Eliminar el Product.
5. Recalcular Amount de la cabecera.
6. Persistir.
```

### Prompt 3.8 — Commands: Transiciones de estado

```
Crea tres commands para las transiciones de estado de ProductDeclaration en `Application/Features/ProductDeclarations/Commands/`:

1. `EmitProductDeclarationCommand` (Borrador → Emitido)
   - Input: Id (Guid)
   - Usa ProductDeclarationStateService para validar y ejecutar la transición
   - Requiere perfil PRODUCER (propia) o ADMIN
   - Al emitir, genera una notificación para ADMIN (si existe un servicio de notificaciones en el proyecto, úsalo; si no, deja un TODO)

2. `ValidateProductDeclarationCommand` (Emitido → Validado)
   - Input: Id (Guid)
   - Solo ADMIN
   - Genera notificación para el PRODUCER

3. `RejectProductDeclarationCommand` (Emitido → Rechazado)
   - Input: Id (Guid), Reason (string)
   - Solo ADMIN
   - Reason obligatorio (mínimo 10 caracteres)
   - Genera notificación para el PRODUCER con el motivo

Todos los handlers deben: cargar la declaración con Products, llamar a ProductDeclarationStateService, persistir, y (opcionalmente) notificar.
```

### Prompt 3.9 — Query: Dashboard KPIs de declaraciones

```
Crea la query `GetProductDeclarationDashboardQuery` en `Application/Features/ProductDeclarations/Queries/GetProductDeclarationDashboardQuery.cs`.

Input: Year (int?), Period (int?), IdProducer (Guid?)

Output: ProductDeclarationDashboardDto con:
- DeclarationsByState: Dictionary<string, int> (State → count)
- TotalDeclaredAmount: decimal
- TotalDeclaredQuantity: decimal (Σ Products.Quantity)
- TopProducts: List<TopProductDto> (top 10 por Σ Quantity, con ResidueName)
- ProducersWithoutDeclaration: int (nº de Entities con EntityRole=Producer sin declaración en el periodo)

Lógica:
1. Aplicar filtros de perfil (igual que en GetProductDeclarationsQuery).
2. Agrupar por State para DeclarationsByState.
3. Sumar Amount y Quantity.
4. Agrupar Products por IdResidue, sumar Quantity, ordenar desc, top 10.
5. Contar productores sin declaración: Entities con EntityRole=Producer del tenant MINUS los que tienen ProductDeclaration en el periodo.
```

---

## FASE 4 — CAPA WEB (Blazor)

### Prompt 4.1 — Página de listado de declaraciones

```
Crea la página Blazor `Web/Components/Pages/ProductDeclarations/ProductDeclarationList.razor` (y su .razor.cs) siguiendo el patrón de las páginas de listado existentes (p.ej. ServiceOrderList.razor o AgreementList.razor).

Ruta: `/product-declarations`
Título: "Declaraciones de Producción"

Funcionalidades:
1. Tabla paginada con las columnas: Referencia, Productor, Año, Periodo, Tipo, Estado (badge con color), Importe, Fecha Creación, Fecha Emisión, Acciones.
2. Barra de filtros colapsable:
   - Estado (multi-select con los 4 valores)
   - Año (numérico)
   - Periodo (combo cargado desde dicProductDeclarationPeriods vía query)
   - Productor (selector de entidades, oculto si el perfil es PRODUCER)
   - Tipo (combo cargado desde dicProductDeclarationType)
   - Rango de fechas
3. Botón "Nueva declaración" (visible solo para ADMIN y PRODUCER).
4. Botón "Exportar" (CSV/XLSX).
5. Acciones por fila según estado y perfil:
   - Ver (siempre)
   - Editar (si BORRADOR o RECHAZADO y es el productor o ADMIN)
   - Emitir (si BORRADOR y es el productor o ADMIN)
   - Validar (si EMITIDO y ADMIN)
   - Rechazar (si EMITIDO y ADMIN)

Colores de badge de estado:
- BORRADOR = gris (secondary)
- EMITIDO = azul (primary)
- VALIDADO = verde (success)
- RECHAZADO = rojo (danger)

Usa MediatR (ISender) para invocar GetProductDeclarationsQuery. Usa ICurrentUserService para determinar el perfil y ajustar la visibilidad.
```

### Prompt 4.2 — Formulario de declaración (cabecera + líneas)

```
Crea la página Blazor `Web/Components/Pages/ProductDeclarations/ProductDeclarationForm.razor` (y su .razor.cs) para alta y edición de declaraciones.

Ruta: `/product-declarations/new` (alta) · `/product-declarations/{Id:guid}/edit` (edición)

Estructura del formulario (2 secciones):

**Sección 1 — Cabecera:**
- Productor: selector de Entities con EntityRole=Producer. Para PRODUCER → solo lectura, autocompletado con LinkedEntityId. Para ADMIN → selector libre con búsqueda.
- Año: input numérico, default año actual.
- Periodo: combo cargado desde dicProductDeclarationPeriods.
- Mes: combo 1-12, solo visible si el periodo requiere mes.
- Tipo: combo cargado desde dicProductDeclarationType.
- Moneda: combo (EUR, USD), default EUR.
- Referencia: texto libre, opcional.
- Estado: badge readonly.

**Sección 2 — Líneas de producto (tabla editable):**
- Tabla con columnas: Producto (selector → Residues con ResidueType=Product, búsqueda por nombre/referencia), Referencia, Fuente (combo → dicProductDeclarationSource), Cantidad, Unidad, Unidades, Precio, Acciones (eliminar).
- Botón "Añadir línea" que inserta una fila vacía editable.
- Al seleccionar un producto del catálogo Residues, autocompletar MeasureUnit desde Residues.DefaultMeasureUnit.
- Fila de totales: Σ Cantidad, Σ (Cantidad × Precio).

**Botones de acción:**
- "Guardar borrador" → crea/actualiza la declaración con State=Borrador.
- "Emitir" → guarda + transiciona a Emitido (solo si hay al menos 1 línea).
- "Cancelar" → vuelve al listado.

Al editar: cargar datos con GetProductDeclarationDetailQuery. Solo editable si State es Borrador o Rechazado.

Sigue el patrón de formularios existentes (p.ej. ServiceOrderForm.razor, AgreementForm.razor).
```

### Prompt 4.3 — Página de detalle / vista 360°

```
Crea la página Blazor `Web/Components/Pages/ProductDeclarations/ProductDeclarationDetail.razor` para visualización de una declaración.

Ruta: `/product-declarations/{Id:guid}`

Secciones:
1. **Cabecera**: card con datos del productor (nombre, NIF, centro), periodo, tipo, estado con badge de color, importe total formateado, fechas de creación y emisión.

2. **Líneas de producto**: tabla readonly con columnas: Producto (nombre del catálogo Residues), Categoría, Referencia, Fuente, Cantidad, Unidad, Unidades, Precio, Subtotal. Fila de totales al pie.

3. **Timeline de estados**: componente stepper horizontal (reutilizar el patrón del stepper de traslados si existe) mostrando: Borrador → Emitido → Validado (o Rechazado en rojo). Cada paso muestra fecha y usuario.

4. **Botones de acción contextual** según estado y perfil:
   - "Editar" → navega a /edit (si Borrador/Rechazado y tiene permiso)
   - "Emitir" → transiciona a Emitido
   - "Validar" → transiciona a Validado (solo ADMIN)
   - "Rechazar" → abre modal con campo de motivo obligatorio (solo ADMIN)
   - "Exportar PDF" → descarga resumen (TODO: conectar con QuestPDF o similar)
   - "Exportar XLSX" → descarga líneas

Cargar datos con GetProductDeclarationDetailQuery.
```

### Prompt 4.4 — Widgets de dashboard

```
Crea el componente Blazor `Web/Components/Pages/ProductDeclarations/ProductDeclarationDashboard.razor` con los widgets de KPIs del módulo.

Ruta: `/product-declarations/dashboard`

Widgets (usar ApexCharts o Chart.js según el patrón del proyecto):
1. **Donut chart de estados**: Borrador/Emitido/Validado/Rechazado con colores consistentes (gris/azul/verde/rojo).
2. **Bar chart de volumen por periodo**: Σ Products.Quantity agrupado por Year+Period.
3. **Card de importe total**: Σ ProductDeclaration.Amount formateado con Currency.
4. **Top 10 productos**: tabla pequeña con ranking por Σ Quantity, nombre del producto.
5. **Alerta de productores sin declaración**: card con nº de productores sin declaración en el periodo actual, con enlace al listado filtrado.

Filtros superiores: Año, Periodo, Productor (solo ADMIN).

Usa GetProductDeclarationDashboardQuery via MediatR.
```

### Prompt 4.5 — Navegación y menú lateral

```
Actualiza el menú de navegación lateral (MainLayout.razor o NavMenu.razor, según el patrón del proyecto) para incluir las nuevas páginas del módulo de Declaraciones de Producción.

Añade una sección "📦 Declaraciones" (o similar, usando el icono consistente con el diseño) con los siguientes enlaces:
1. "Listado" → /product-declarations (visible para ADMIN, PRODUCER, SCRAP, COORDINATOR)
2. "Dashboard" → /product-declarations/dashboard (visible para ADMIN, PRODUCER, SCRAP)
3. "Nueva declaración" → /product-declarations/new (visible para ADMIN, PRODUCER)

Para los diccionarios, añade dentro de la sección de Administración existente:
4. "Diccionarios Declaración" → /admin/dictionaries/product-declarations (visible solo para ADMIN)

Usa ICurrentUserService para verificar el perfil y mostrar/ocultar los enlaces.
```

---

## FASE 5 — ADMINISTRACIÓN DE DICCIONARIOS

### Prompt 5.1 — CRUD de diccionarios

```
Crea las páginas de administración para los 6 diccionarios de declaraciones de producción en `Web/Components/Pages/Admin/Dictionaries/ProductDeclarations/`.

Ruta base: `/admin/dictionaries/product-declarations`
Solo accesible para perfil ADMIN (policy CanManageDeclarationDicts).

Crea un layout con tabs (o sub-navegación) para los 6 diccionarios:
1. Categorías (dicProductDeclarationCategory)
2. Periodos (dicProductDeclarationPeriods)
3. Productos declarables (dicProductDeclarationProducts) — este tiene además un combo CategoryId → dicProductDeclarationCategory
4. Fuentes (dicProductDeclarationSource)
5. Tipos (dicProductDeclarationType)
6. Usos (dicProductDeclarationUse)

Cada tab muestra:
- Tabla con columnas: Id, Ref, Descripción, Acciones (Editar, Desactivar)
- Botón "Nuevo" que abre un modal/drawer con formulario (Ref + Descripción + CategoryId si aplica)
- Búsqueda por Ref y Descripción
- No permitir eliminación física, solo desactivación lógica (si el esquema no tiene IsActive, mostrar un aviso y solo permitir edición)

Crea las queries y commands necesarios (GetDicProductDeclarationCategoriesQuery, CreateDicProductDeclarationCategoryCommand, etc.) o un handler genérico si prefieres para reducir boilerplate.
```

---

## FASE 6 — IMPORTACIÓN MASIVA

### Prompt 6.1 — Importación CSV/XLSX de líneas de producto

```
Crea la funcionalidad de importación masiva de líneas de producto en una declaración existente.

1. Command: `ImportProductsFromFileCommand` en `Application/Features/ProductDeclarations/Commands/ImportProductsFromFileCommand.cs`
   - Input: IdProductDeclaration (Guid), FileContent (byte[]), FileName (string)
   - Output: ImportResultDto con: TotalRows, SuccessRows, ErrorRows, Errors (List<ImportRowErrorDto> con LineNumber, Column, ErrorMessage)
   - Lógica:
     a. Verificar que la declaración existe y está en Borrador/Rechazado.
     b. Parsear el fichero (CSV con separador ';' o XLSX primera hoja) usando la librería existente en el proyecto (ClosedXML para XLSX, o similar).
     c. Columnas esperadas: ProductReference, Source, Quantity, MeasureUnit, Units, Price.
     d. Por cada fila: buscar Residues por Reference + ResidueType=Product. Si no existe → error. Validar Quantity > 0.
     e. Insertar las filas válidas como Products.
     f. Recalcular Amount de la cabecera.
     g. Devolver ImportResultDto.

2. Componente Blazor: `ImportProductsDialog.razor`
   - Modal con: botón de subir fichero, link para descargar plantilla, preview de resultados con semáforo (verde/rojo por fila), botón "Confirmar importación".
   - Se integra en la página de detalle/formulario de la declaración.

3. Plantilla de importación: genera un fichero CSV de ejemplo con cabeceras y 2 filas de ejemplo. Ubícalo en wwwroot/templates/product-import-template.csv.
```

---

## FASE 7 — EXPORTACIÓN

### Prompt 7.1 — Exportación PDF de la declaración

```
Crea la funcionalidad de exportación PDF de una declaración de producción.

Query: `ExportProductDeclarationToPdfQuery` en `Application/Features/ProductDeclarations/Queries/ExportProductDeclarationToPdfQuery.cs`

Input: Id (Guid)
Output: byte[] (contenido PDF) + string FileName

Lógica:
1. Cargar la declaración con detalle (Producer, Products, Residues).
2. Generar PDF usando QuestPDF (o la librería de PDF existente en el proyecto, revisa cómo se genera el PDF de trazabilidad WasteMoveTimelinePdfGenerator como referencia).
3. Contenido del PDF:
   - Encabezado: logo GreenTransit, título "Declaración de Producción", referencia, fecha.
   - Datos del productor: nombre, NIF, centro.
   - Datos de la declaración: periodo, año, tipo, estado, moneda.
   - Tabla de líneas: Producto, Categoría, Referencia, Fuente, Cantidad, Unidad, Unidades, Precio, Subtotal.
   - Pie: total general, fecha de emisión, firma (espacio para firma).
4. Devolver bytes del PDF.

Componente Blazor: botón "Exportar PDF" en la página de detalle que invoca esta query y descarga el fichero via JS interop (downloadBase64File o similar, reutilizando el patrón existente).
```

### Prompt 7.2 — Exportación XLSX del listado

```
Crea la funcionalidad de exportación XLSX del listado de declaraciones y de las líneas de una declaración individual.

1. `ExportProductDeclarationsToExcelQuery` — exporta el listado filtrado:
   - Input: mismos filtros que GetProductDeclarationsQuery (sin paginación)
   - Output: byte[] + FileName
   - Genera XLSX con ClosedXML (1 hoja: Referencia, Productor, NIF, Año, Periodo, Tipo, Estado, Importe, Fecha Creación, Fecha Emisión)

2. `ExportProductDeclarationDetailToExcelQuery` — exporta las líneas de una declaración:
   - Input: Id (Guid)
   - Output: byte[] + FileName
   - Genera XLSX con 2 hojas:
     - "Cabecera": datos de la declaración
     - "Líneas": Producto, Categoría, Referencia, Fuente, Cantidad, Unidad, Unidades, Precio, Subtotal

Sigue el patrón de ExportKpisToExcelQuery existente.
```

---

## FASE 8 — TESTS

### Prompt 8.1 — Tests unitarios del servicio de dominio

```
Crea tests unitarios para ProductDeclarationStateService en `Tests/Domain/Services/ProductDeclarationStateServiceTests.cs` (o la ubicación de tests del proyecto).

Casos a cubrir:
1. Transición Borrador → Emitido: éxito con Products y datos obligatorios.
2. Transición Borrador → Emitido: fallo si no hay Products → DomainException.
3. Transición Borrador → Emitido: fallo si IdProducer es null → DomainException.
4. Transición Emitido → Validado: éxito.
5. Transición Emitido → Rechazado: éxito con motivo.
6. Transición Emitido → Rechazado: fallo sin motivo → DomainException.
7. Transición Rechazado → Borrador: éxito.
8. Transición inválida (p.ej. Borrador → Validado): fallo → DomainException.
9. Transición inválida (p.ej. Validado → Borrador): fallo → DomainException.

Usa xUnit + FluentAssertions (o el framework de test existente en el proyecto).
```

### Prompt 8.2 — Tests de integración de queries y commands

```
Crea tests de integración para los handlers principales del módulo de Declaraciones de Producción.

Casos:
1. CreateProductDeclarationCommand: crea correctamente con State=Borrador.
2. CreateProductDeclarationCommand: falla si ya existe declaración activa con mismo IdProducer+Year+Period+Type.
3. AddProductToDeclarationCommand: añade línea y recalcula Amount.
4. EmitProductDeclarationCommand: transiciona y asigna DateEmit.
5. EmitProductDeclarationCommand: falla si no hay líneas.
6. GetProductDeclarationsQuery con filtro PRODUCER: solo devuelve sus declaraciones.
7. GetProductDeclarationsQuery con filtro SCRAP: solo devuelve declaraciones de productores adheridos.
8. ValidateProductDeclarationCommand: transiciona a Validado.
9. RejectProductDeclarationCommand: transiciona a Rechazado con motivo.

Usa el patrón de tests de integración existente en el proyecto (base de datos en memoria o TestContainers, según lo que ya se use).
```

---

## FASE 9 — INTEGRACIÓN FINAL

### Prompt 9.1 — Integración con Dashboard principal

```
Actualiza el dashboard principal (§0.1 del mapa de funcionalidades, probablemente en Web/Components/Pages/Dashboard/ o Home/) para incluir un widget de declaraciones de producción.

Añade al dashboard:
1. Card "Declaraciones pendientes de validación" (solo para ADMIN): nº de ProductDeclaration en estado EMITIDO. Al hacer clic navega a /product-declarations?state=Emitido.
2. Card "Mis declaraciones" (solo para PRODUCER): nº total y desglose por estado. Al hacer clic navega a /product-declarations.
3. Card "Volumen declarado (periodo actual)": Σ Products.Quantity del Year+Period actual.

Integra estas cards en la query/componente de dashboard existente, o crea una sub-query que el dashboard invoque.
```

### Prompt 9.2 — Notificaciones

```
Si el proyecto tiene un sistema de notificaciones (revisa si existe un servicio como INotificationService, SignalR hub, o similar), integra las siguientes notificaciones del módulo de declaraciones:

1. Al emitir (Borrador → Emitido):
   - Notificar a todos los usuarios con perfil ADMIN del tenant.
   - Mensaje: "Nueva declaración emitida por {ProducerName} para {Year}/{Period}".

2. Al validar (Emitido → Validado):
   - Notificar al usuario PRODUCER vinculado al IdProducer de la declaración.
   - Mensaje: "Tu declaración {Reference} ha sido validada".

3. Al rechazar (Emitido → Rechazado):
   - Notificar al usuario PRODUCER vinculado al IdProducer.
   - Mensaje: "Tu declaración {Reference} ha sido rechazada. Motivo: {Reason}".

Si no existe un sistema de notificaciones, crea la interfaz IProductDeclarationNotificationService con los 3 métodos y una implementación stub (log a consola) que se pueda reemplazar después por SignalR o email.
```

---

## Resumen de archivos a generar

| Fase | Capa | Archivos principales |
|---|---|---|
| 1 | Domain | `ProductDeclaration.cs`, `Product.cs`, 6× `DicProductDeclaration*.cs`, `ProductDeclarationStateService.cs` |
| 2 | Infrastructure | `ProductDeclarationConfiguration.cs`, `ProductConfiguration.cs`, configs de diccionarios, DbContext update |
| 3 | Application | DTOs, 8+ Commands, 3+ Queries, Validators |
| 4 | Web | `ProductDeclarationList.razor`, `ProductDeclarationForm.razor`, `ProductDeclarationDetail.razor`, `ProductDeclarationDashboard.razor`, NavMenu update |
| 5 | Web/Admin | `DictionariesProductDeclarations.razor` + queries/commands |
| 6 | Application+Web | `ImportProductsFromFileCommand`, `ImportProductsDialog.razor`, template CSV |
| 7 | Application+Web | `ExportProductDeclarationToPdfQuery`, `ExportProductDeclarations*ToExcelQuery` |
| 8 | Tests | `ProductDeclarationStateServiceTests.cs`, tests de integración |
| 9 | Web | Dashboard widgets, notificaciones |