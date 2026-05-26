# Prompt para GitHub Copilot — Refactorización del Módulo EcoDataNet

> **Instrucción**: Copia este prompt completo en GitHub Copilot Chat adjuntando los archivos `COPILOT_CONTEXT.md`, `README.md`, `Crear_BD_v4_1.sql` e `instrucciones_adicionales.md` de tu proyecto. Copilot debe inspeccionar el repo para confirmar estructuras y rutas reales antes de ejecutar cambios.

---

## 🎯 Objetivo

Refactorizar completamente el módulo **EcoDataNet** de GreenTransit:

1. **Eliminar** todas las pantallas mock actuales del menú EcoDataNet (publicar datos, consumir datos, configuración por perfil) y todo el código asociado (componentes Blazor, CQRS, DTOs, rutas, menús, entradas en `PageDefinitions`/`PagePermissions`).
2. **Crear** dos nuevas entidades de dominio: `UserEDCConnector` y `ProfileEDCConsumer`.
3. **Crear** dos nuevas pantallas funcionales bajo el menú EcoDataNet:
   - **Configuración conector EDC** (`/ecodatanet/connector-config`)
   - **Consumir datos** (`/ecodatanet/consume-data`)
4. Ambas pantallas implementan comportamiento diferenciado ADMIN vs NO ADMIN.
5. Actualizar el sistema de autorización dinámico (`PageDefinitions`/`PagePermissions`) para reflejar los cambios.

---

## 📐 Alcance

### Incluido

- Eliminación completa del código EcoDataNet actual (mock frontend).
- Creación de entidades `UserEDCConnector` y `ProfileEDCConsumer` en Domain + EF Core.
- CQRS completo (Queries + Commands + Validators) para ambas pantallas.
- Componentes Blazor para ambas pantallas con lógica ADMIN / NO ADMIN.
- Actualización de `NavMenu.razor` (sección EcoDataNet).
- Actualización de `PageDiscoveryService.InferModuleName()` y `HumanizeName()` si es necesario.
- Limpieza de `PageDefinitions`/`PagePermissions` obsoletas.
- Migración EF Core para las nuevas tablas.

### Fuera de alcance

- El botón "Consumir catálogo" en la pantalla de consumo **NO hace nada** (placeholder para integración futura con API EDC real). Mostrar un `Toast`/notificación informativa: "Funcionalidad pendiente de integración con EcoDataNet".
- No se implementa integración real con conectores EDC ni API externa.
- No se modifica la tabla `Users` existente (los campos `PortalEDCProvider` y `PortalEDCConsumer` quedan sin usar pero no se eliminan en esta fase).

---

## 🔍 Cómo localizar el código a eliminar

Buscar en el repositorio los siguientes patrones para identificar todo el código mock actual:

| Patrón de búsqueda | Qué buscar |
|---|---|
| Rutas `/ecodatanet/` | Componentes `.razor` con `@page "/ecodatanet/..."` |
| Carpeta `EcoDataNet` | `src/GreenTransit.Web/Components/Pages/EcoDataNet/` — todos los archivos `.razor` y `.razor.css` |
| Menú lateral | En `NavMenu.razor`, buscar la sección colapsable **EcoDataNet** (icono `bi-broadcast`) y todos sus ítems hijos |
| CQRS | En `Application/Features/`, buscar carpeta `EcoDataNet` o clases con prefijo `EcoDataNet`, `PublishData`, `ConsumeData` relacionadas con el módulo |
| DTOs | Buscar DTOs relacionados con `EcoDataNet` en `Application/Features/` |
| `PageDefinitions` | En BD o en seed/migración, buscar rutas que empiecen por `/ecodatanet/` |
| `PagePermissions` | Entradas en BD vinculadas a las `PageDefinitions` de rutas `/ecodatanet/` |
| Imágenes | `wwwroot/images/ecodatanet/` — verificar si hay imágenes estáticas referenciadas solo por las pantallas a eliminar |
| `_groupRoutes` | En `NavMenu.razor`, buscar entradas del diccionario `_groupRoutes` con rutas `/ecodatanet/` |
| Tests | Buscar en el proyecto `Tests` cualquier test referenciando componentes/queries/commands de EcoDataNet |

**Confirmación**: el fichero `PublishData.razor` (y `.css`) en `src/GreenTransit.Web/Components/Pages/EcoDataNet/` es la pantalla mock actual conocida. Puede haber más componentes. Copilot debe escanear toda la carpeta `EcoDataNet` y listar todos los archivos antes de eliminar.

---

## 🗄️ Modelo de datos — Tablas nuevas

### Tabla `UserEDCConnector`

Almacena la configuración del conector EDC asociado a cada usuario.

```sql
CREATE TABLE dbo.UserEDCConnector (
    ID              INT             NOT NULL IDENTITY(1,1),
    UserId          INT             NOT NULL,
    EDCServerName   NVARCHAR(255)   NOT NULL,
    EDCConnectorId  NVARCHAR(255)   NOT NULL,
    CONSTRAINT PK_UserEDCConnector PRIMARY KEY (ID),
    CONSTRAINT FK_UserEDCConnector_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users (ID)
        ON DELETE CASCADE
);
```

- Relación 1:1 con `Users` (un usuario tiene como máximo un registro de conector EDC).
- `EDCServerName`: nombre/URL del servidor EDC donde está desplegado el conector del usuario.
- `EDCConnectorId`: identificador único del conector dentro del servidor EDC.

### Tabla `ProfileEDCConsumer`

Define qué perfiles puede consumir cada perfil en el espacio de datos.

```sql
CREATE TABLE dbo.ProfileEDCConsumer (
    ID                  INT NOT NULL IDENTITY(1,1),
    ProfileId           INT NOT NULL,
    ConsumedProfileId   INT NOT NULL,
    CONSTRAINT PK_ProfileEDCConsumer PRIMARY KEY (ID),
    CONSTRAINT FK_ProfileEDCConsumer_Profile FOREIGN KEY (ProfileId)
        REFERENCES dbo.Profiles (ID)
        ON DELETE CASCADE,
    CONSTRAINT FK_ProfileEDCConsumer_ConsumedProfile FOREIGN KEY (ConsumedProfileId)
        REFERENCES dbo.Profiles (ID)
        -- SIN ON DELETE CASCADE para evitar ciclo
);
```

- Relación N:M entre perfiles: `ProfileId` = "perfil que consume", `ConsumedProfileId` = "perfil cuyos datos se consumen".
- Ejemplo: si el perfil SCRAP (ID=2) puede consumir datos del perfil PLANT_OP (ID=5), existirá un registro `(ProfileId=2, ConsumedProfileId=5)`.
- La tabla `Profiles` es compartida entre tenants (sin `OwnerId`), así que `ProfileEDCConsumer` tampoco tiene `OwnerId`.

---

## ⚙️ Cambios por capas

### CAPA 1 — Domain (`GreenTransit.Domain`)

#### 1.1. Entidad `UserEDCConnector`

Crear en `Entities/` (o la carpeta donde estén las entidades del dominio):

```csharp
public class UserEDCConnector
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string EDCServerName { get; set; } = string.Empty;
    public string EDCConnectorId { get; set; } = string.Empty;

    // Navegación
    public User User { get; set; } = null!;
}
```

> **Nota**: la entidad `User` del dominio puede llamarse `User` o `Users` — Copilot debe buscar el nombre real en el proyecto (buscar la clase que mapea a la tabla `Users`).

#### 1.2. Entidad `ProfileEDCConsumer`

```csharp
public class ProfileEDCConsumer
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public int ConsumedProfileId { get; set; }

    // Navegación
    public Profile Profile { get; set; } = null!;
    public Profile ConsumedProfile { get; set; } = null!;
}
```

#### 1.3. Navegación inversa

Añadir en la entidad `User` (buscar la clase real):

```csharp
public UserEDCConnector? EDCConnector { get; set; }
```

Añadir en la entidad `Profile` (buscar la clase real):

```csharp
public ICollection<ProfileEDCConsumer> EDCConsumerPermissions { get; set; } = new List<ProfileEDCConsumer>();
public ICollection<ProfileEDCConsumer> EDCConsumedByPermissions { get; set; } = new List<ProfileEDCConsumer>();
```

---

### CAPA 2 — Infrastructure (`GreenTransit.Infrastructure`)

#### 2.1. Configuración EF Core

Crear `Persistence/Configurations/UserEDCConnectorConfiguration.cs`:

```csharp
public class UserEDCConnectorConfiguration : IEntityTypeConfiguration<UserEDCConnector>
{
    public void Configure(EntityTypeBuilder<UserEDCConnector> builder)
    {
        builder.ToTable("UserEDCConnector");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("ID");
        builder.Property(x => x.EDCServerName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.EDCConnectorId).HasMaxLength(255).IsRequired();

        builder.HasOne(x => x.User)
            .WithOne(u => u.EDCConnector)
            .HasForeignKey<UserEDCConnector>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índice único: un usuario = un conector
        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
```

Crear `Persistence/Configurations/ProfileEDCConsumerConfiguration.cs`:

```csharp
public class ProfileEDCConsumerConfiguration : IEntityTypeConfiguration<ProfileEDCConsumer>
{
    public void Configure(EntityTypeBuilder<ProfileEDCConsumer> builder)
    {
        builder.ToTable("ProfileEDCConsumer");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("ID");

        builder.HasOne(x => x.Profile)
            .WithMany(p => p.EDCConsumerPermissions)
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ConsumedProfile)
            .WithMany(p => p.EDCConsumedByPermissions)
            .HasForeignKey(x => x.ConsumedProfileId)
            .OnDelete(DeleteBehavior.Restrict); // evitar ciclo cascada

        // Índice único: un perfil no puede consumir el mismo perfil dos veces
        builder.HasIndex(x => new { x.ProfileId, x.ConsumedProfileId }).IsUnique();
    }
}
```

#### 2.2. DbContext

Añadir los `DbSet<>` en el `ApplicationDbContext` (buscar el nombre real del DbContext):

```csharp
public DbSet<UserEDCConnector> UserEDCConnectors => Set<UserEDCConnector>();
public DbSet<ProfileEDCConsumer> ProfileEDCConsumers => Set<ProfileEDCConsumer>();
```

#### 2.3. Migración EF Core

Generar una migración:

```bash
dotnet ef migrations add AddEDCConnectorTables --project src/GreenTransit.Infrastructure --startup-project src/GreenTransit.Web
```

---

### CAPA 3 — Application (`GreenTransit.Application`)

Crear carpeta `Features/EcoDataNet/` con subcarpetas `DTOs/`, `Queries/`, `Commands/`, `Validators/`.

#### 3.1. DTOs

```csharp
// DTOs/UserEDCConnectorDto.cs
public class UserEDCConnectorDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;    // Users.CompleteName
    public string UserLogin { get; set; } = string.Empty;   // Users.Login
    public string EDCServerName { get; set; } = string.Empty;
    public string EDCConnectorId { get; set; } = string.Empty;
}

// DTOs/UserForEDCListDto.cs  (para el listado de usuarios en modo ADMIN)
public class UserForEDCListDto
{
    public int Id { get; set; }
    public string CompleteName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string ProfileReference { get; set; } = string.Empty;
    public bool HasEDCConnector { get; set; }
}

// DTOs/ProfileEDCConsumerDto.cs
public class ProfileEDCConsumerDto
{
    public int ProfileId { get; set; }
    public string ProfileReference { get; set; } = string.Empty;
    public string ProfileDescription { get; set; } = string.Empty;
}
```

#### 3.2. Queries

**GetUserEDCConnectorQuery** — Obtiene la configuración EDC de un usuario concreto:

```csharp
public record GetUserEDCConnectorQuery(int UserId) : IRequest<UserEDCConnectorDto?>;
```

Handler:
- Filtrar por `OwnerId` del usuario actual (multi-tenant).
- Si el perfil NO es ADMIN: validar que `UserId == ICurrentUserService.GetUserId()` (no puede ver el conector de otro usuario).
- Si es ADMIN: puede consultar cualquier usuario del mismo tenant.
- LEFT JOIN con `UserEDCConnector` — si no tiene registro, devolver un DTO con campos vacíos y `Id = 0` para indicar que es nuevo.

**GetUsersForEDCListQuery** — Lista de usuarios del tenant (para modo ADMIN):

```csharp
public record GetUsersForEDCListQuery(string? SearchTerm, int Page, int PageSize) : IRequest<PaginatedList<UserForEDCListDto>>;
```

Handler:
- Solo ADMIN puede ejecutar esta query.
- Filtrar `Users` por `OwnerId`.
- Filtros opcionales: `SearchTerm` (busca en `CompleteName`, `Login`).
- Incluir indicador `HasEDCConnector` = `UserEDCConnectors.Any(c => c.UserId == u.Id)`.
- Paginación estándar del proyecto.

**GetConsumableProfilesQuery** — Perfiles que el perfil actual puede consumir:

```csharp
public record GetConsumableProfilesQuery(int ProfileId) : IRequest<List<ProfileEDCConsumerDto>>;
```

Handler:
- Consultar `ProfileEDCConsumer` donde `ProfileId` = parámetro.
- JOIN con `Profiles` para traer `Reference` y `Description` del `ConsumedProfileId`.
- Si el perfil NO es ADMIN: `ProfileId` debe coincidir con `ICurrentUserService.GetUserProfileId()`.
- Si es ADMIN: puede consultar cualquier perfil.

**GetProfilesForConsumptionListQuery** — Lista de perfiles del sistema (para modo ADMIN):

```csharp
public record GetProfilesForConsumptionListQuery() : IRequest<List<ProfileEDCConsumerDto>>;
```

Handler:
- Solo ADMIN.
- Listar todos los perfiles de la tabla `Profiles` con `Id`, `Reference`, `Description`.

#### 3.3. Commands

**UpsertUserEDCConnectorCommand** — Crea o actualiza la configuración EDC de un usuario:

```csharp
public record UpsertUserEDCConnectorCommand : IRequest<int>
{
    public int UserId { get; init; }
    public string EDCServerName { get; init; } = string.Empty;
    public string EDCConnectorId { get; init; } = string.Empty;
}
```

Handler:
- Multi-tenant: verificar que el `UserId` pertenece al mismo `OwnerId` que el usuario autenticado.
- Si el perfil NO es ADMIN: validar que `UserId == ICurrentUserService.GetUserId()`.
- Buscar `UserEDCConnector` existente por `UserId`.
  - Si existe → actualizar `EDCServerName` y `EDCConnectorId`.
  - Si no existe → crear nuevo registro.
- Devolver el `ID` del registro.

#### 3.4. Validators (FluentValidation)

```csharp
public class UpsertUserEDCConnectorValidator : AbstractValidator<UpsertUserEDCConnectorCommand>
{
    public UpsertUserEDCConnectorValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.EDCServerName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.EDCConnectorId).NotEmpty().MaximumLength(255);
    }
}
```

---

### CAPA 4 — Web (`GreenTransit.Web`)

#### 4.1. Policies nuevas

Añadir en `PolicyConstants.cs` (o donde se definan las constantes de policy):

```csharp
public const string CanAccessEDCConnectorConfig = "CanAccessEDCConnectorConfig";
public const string CanAccessEDCConsumeData = "CanAccessEDCConsumeData";
```

Registrar en `Program.cs` (o `AuthorizationConfiguration`):

```csharp
// Acceso a configuración de conector EDC — todos los perfiles autenticados
options.AddPolicy(PolicyConstants.CanAccessEDCConnectorConfig, policy =>
    policy.RequireAuthenticatedUser());

// Acceso a consumo de datos EDC — todos los perfiles autenticados
options.AddPolicy(PolicyConstants.CanAccessEDCConsumeData, policy =>
    policy.RequireAuthenticatedUser());
```

> **IMPORTANTE**: estas policies son el **suelo mínimo**. El control fino (qué perfiles ven la pantalla) se gestiona dinámicamente desde `/security/page-permissions` por el administrador. NO hardcodear checks de perfil específico en las policies.

#### 4.2. Pantalla "Configuración conector EDC"

Crear `Web/Components/Pages/EcoDataNet/EDCConnectorConfig.razor`

- **Ruta**: `@page "/ecodatanet/connector-config"`
- **Policy**: `@attribute [Authorize(Policy = PolicyConstants.CanAccessEDCConnectorConfig)]`

**Comportamiento según perfil del usuario logueado**:

**A) Si es ADMIN** (`ICurrentUserService.IsInAnyProfile("ADMIN")`):

1. Mostrar primero una **tabla paginada de usuarios** del tenant actual:
   - Columnas: Nombre completo, Login, Perfil, Tiene conector (Sí/No badge).
   - Filtros: búsqueda por nombre/login.
   - Usa `GetUsersForEDCListQuery`.
2. Al hacer clic en un usuario (fila seleccionable o botón "Configurar"), **navegar o mostrar** el formulario de configuración del conector EDC cargando los datos de ese usuario.

**B) Si NO es ADMIN**:

1. Mostrar directamente el formulario de configuración del conector EDC con los datos del usuario logueado.
   - Usa `GetUserEDCConnectorQuery(currentUserId)`.

**Formulario de configuración del conector EDC** (compartido por ambos modos):

| Campo | Tipo | Comportamiento |
|---|---|---|
| Nombre de usuario | `InputText` | **Solo lectura**. Muestra `Users.CompleteName`. |
| Nombre del servidor EDC | `InputText` | Editable. Mapea a `UserEDCConnector.EDCServerName`. |
| Identificador del conector | `InputText` | Editable. Mapea a `UserEDCConnector.EDCConnectorId`. |

- Botón **"Guardar"**: ejecuta `UpsertUserEDCConnectorCommand`.
- Si el usuario no tiene registro en `UserEDCConnector`, el formulario aparece vacío (listo para crear).
- Tras guardar: `Toast` de éxito y, si es ADMIN, volver a la lista de usuarios.
- Botón **"Volver"** (solo en modo ADMIN): regresa a la lista de usuarios.

#### 4.3. Pantalla "Consumir datos"

Crear `Web/Components/Pages/EcoDataNet/ConsumeData.razor`

- **Ruta**: `@page "/ecodatanet/consume-data"`
- **Policy**: `@attribute [Authorize(Policy = PolicyConstants.CanAccessEDCConsumeData)]`

**Comportamiento según perfil del usuario logueado**:

**A) Si es ADMIN**:

1. Mostrar primero una **tabla paginada de perfiles del sistema**:
   - Columnas: Referencia del perfil, Descripción.
   - Usa `GetProfilesForConsumptionListQuery`.
2. Al hacer clic en un perfil, **navegar o mostrar** la ventana de consumo de datos de ese perfil.

**B) Si NO es ADMIN**:

1. Abrir directamente la ventana de consumo de datos del perfil del usuario logueado.
   - Obtener `profileId` del usuario logueado vía `ICurrentUserService`.

**Ventana de consumo por perfil** (compartida por ambos modos):

1. **Título**: "Consumo de datos — Perfil: {ProfileReference}".
2. **Desplegable de perfiles consumibles**: alimentado por `GetConsumableProfilesQuery(profileId)`.
   - Si no hay perfiles configurados → mostrar mensaje: "No hay perfiles de consumo configurados para este perfil. Contacte al administrador."
3. Al seleccionar un perfil del desplegable, **mostrar un botón "Consumir catálogo"**.
4. **Botón "Consumir catálogo"**: de momento **NO hace nada**. Al hacer clic, mostrar `Toast`/notificación: "Funcionalidad pendiente de integración con la plataforma EcoDataNet."
5. Botón **"Volver"** (solo en modo ADMIN): regresa a la lista de perfiles.

#### 4.4. Menú lateral (`NavMenu.razor`)

Reemplazar completamente la sección EcoDataNet existente. La nueva sección debe tener:

```
🌐 EcoDataNet (grupo colapsable, icono bi-broadcast)
   ├── Configuración conector EDC  →  /ecodatanet/connector-config  (icono bi-gear)
   └── Consumir datos              →  /ecodatanet/consume-data       (icono bi-cloud-download)
```

- Cada enlace debe verificar `IPagePermissionService.CanAccessRouteAsync` antes de renderizarse (patrón existente del proyecto).
- El grupo padre solo es visible si al menos un hijo tiene permisos (patrón `HasAnyVisibleChild`).
- Actualizar el diccionario `_groupRoutes` con las nuevas rutas:

```csharp
["EcoDataNet"] = new[] { "/ecodatanet/connector-config", "/ecodatanet/consume-data" },
```

- **Eliminar** cualquier ruta anterior de EcoDataNet del diccionario.

#### 4.5. `PageDiscoveryService` — Actualización

Verificar que `InferModuleName()` ya mapea `EcoDataNet` / `/ecodatanet/` al módulo "EcoDataNet". Según la documentación, este mapeo ya existe. Si no, añadirlo.

Actualizar `HumanizeName()` para las nuevas pantallas:

```csharp
"EDCConnectorConfig" => "Configuración conector EDC",
"ConsumeData" => "Consumir datos",
```

---

### CAPA 5 — Seguridad y PagePermissions

#### 5.1. Limpieza de PageDefinitions/PagePermissions obsoletas

Tras eliminar los componentes `.razor` antiguos, `PageDiscoveryService` dejará de detectarlos automáticamente en el siguiente arranque. Sin embargo, las filas en `PageDefinitions` y `PagePermissions` asociadas a las rutas eliminadas **quedarán huérfanas**.

Crear una migración de datos (o script SQL en el seed) que:

```sql
-- Eliminar permisos huérfanos de pantallas EcoDataNet antiguas
DELETE pp FROM PagePermissions pp
INNER JOIN PageDefinitions pd ON pp.PageDefinitionId = pd.ID
WHERE pd.Route LIKE '/ecodatanet/%'
  AND pd.Route NOT IN ('/ecodatanet/connector-config', '/ecodatanet/consume-data');

-- Eliminar definiciones de pantallas EcoDataNet antiguas
DELETE FROM PageDefinitions
WHERE Route LIKE '/ecodatanet/%'
  AND Route NOT IN ('/ecodatanet/connector-config', '/ecodatanet/consume-data');
```

> **Alternativa**: si el `PageDiscoveryService` tiene un mecanismo de marcar como `IsActive = false` las pantallas que ya no existen como componente, puede ser suficiente. Copilot debe verificar el comportamiento actual del servicio de sincronización.

#### 5.2. Configuración recomendada por defecto

Tras despliegue, las nuevas pantallas aparecerán en amarillo en `/security/page-permissions`. La configuración recomendada inicial:

| Pantalla | ADMIN | SCRAP | PRODUCER | CARRIER | PLANT_OP | CAC_OP | PUBLIC_ENT | COORDINATOR | DISPATCH_OFFICE |
|---|---|---|---|---|---|---|---|---|---|
| Configuración conector EDC | Ambos | Ambos | Ambos | Ambos | Ambos | Ambos | Ambos | Ambos | Ambos |
| Consumir datos | Ambos | Lectura | Lectura | Sin acceso | Lectura | Sin acceso | Lectura | Lectura | Lectura |

> Esta tabla es referencia para el administrador. NO se hardcodea en código.

---

## 🚫 Eliminación de código muerto — Checklist

Copilot debe verificar y eliminar TODOS los siguientes elementos si existen:

- [ ] Componentes `.razor` en `Web/Components/Pages/EcoDataNet/` — eliminar TODOS los que no sean los nuevos `EDCConnectorConfig.razor` y `ConsumeData.razor`.
- [ ] Archivos `.razor.css` asociados a los componentes eliminados.
- [ ] Queries/Commands/Handlers en `Application/Features/EcoDataNet/` relacionados con las pantallas eliminadas (buscar clases con `Publish`, `PublishData`, mock de consumo previo).
- [ ] DTOs obsoletos relacionados.
- [ ] Imágenes en `wwwroot/images/ecodatanet/` que solo eran usadas por las pantallas mock — verificar que no se referencian en ningún otro lugar antes de eliminar.
- [ ] Referencias en `NavMenu.razor` a rutas `/ecodatanet/publish` u otras rutas EcoDataNet antiguas.
- [ ] Tests (si existen) que referencien componentes/queries/commands eliminados.
- [ ] Entradas en `_groupRoutes` del `NavMenu.razor` con rutas antiguas.

**Regla**: tras la eliminación, buscar en todo el repo `ecodatanet` (case-insensitive) y `PublishData` para confirmar que no quedan referencias rotas. Las únicas coincidencias válidas deben ser:
- Los nuevos componentes (`EDCConnectorConfig.razor`, `ConsumeData.razor`).
- Las nuevas queries/commands/DTOs.
- Documentación (Markdown/docx) — no requiere cambios.
- Este prompt.

---

## 🔒 Reglas arquitectónicas obligatorias

1. **Autorización dinámica**: el acceso a las pantallas se controla desde `PageDefinitions`/`PagePermissions` vía `/security/page-permissions`. Las policies en `[Authorize(Policy = ...)]` son el suelo mínimo estático. NO hardcodear checks de perfil específico para controlar visibilidad de pantalla.

2. **Multi-tenant**: toda query sobre `Users` o `UserEDCConnector` DEBE filtrar por `OwnerId` del usuario autenticado. La tabla `ProfileEDCConsumer` NO tiene `OwnerId` (los perfiles son globales).

3. **Distinción ADMIN vs NO ADMIN en handlers**: la lógica de "si es ADMIN muestra listado, si no muestra directo" reside en el **componente Blazor** (capa Web), no en el handler MediatR. Los handlers simplemente procesan la query con los parámetros recibidos y aplican validaciones de seguridad (verificar OwnerId, verificar que el userId solicitado es el propio si no es ADMIN).

4. **Patrón CQRS**: mantener el patrón existente del proyecto con MediatR. Cada query/command en su propio archivo con handler interno (`IRequestHandler<>`).

5. **FluentValidation**: cada command debe tener su validator.

6. **Navegación inversa en entidades**: añadir propiedades de navegación en `User` y `Profile` para las nuevas relaciones.

---

## ✅ Criterios de aceptación

- [ ] **CA-1**: No existe ningún componente `.razor` ni código C# referenciando las pantallas EcoDataNet antiguas (PublishData, configuración mock, consumo mock).
- [ ] **CA-2**: Las tablas `UserEDCConnector` y `ProfileEDCConsumer` se crean correctamente vía migración EF Core.
- [ ] **CA-3**: Un usuario ADMIN accede a `/ecodatanet/connector-config`, ve la lista de usuarios paginada, selecciona uno, ve/edita su configuración EDC, y puede guardar.
- [ ] **CA-4**: Un usuario NO ADMIN accede a `/ecodatanet/connector-config`, ve directamente su formulario de configuración EDC, edita y guarda.
- [ ] **CA-5**: Un usuario NO ADMIN no puede consultar ni modificar la configuración EDC de otro usuario (validación en handler).
- [ ] **CA-6**: Un usuario ADMIN accede a `/ecodatanet/consume-data`, ve la lista de perfiles, selecciona uno, ve el desplegable de perfiles consumibles.
- [ ] **CA-7**: Un usuario NO ADMIN accede a `/ecodatanet/consume-data`, ve directamente el desplegable de perfiles consumibles de su perfil.
- [ ] **CA-8**: El botón "Consumir catálogo" NO ejecuta ninguna lógica real, solo muestra un Toast informativo.
- [ ] **CA-9**: El menú lateral muestra la sección EcoDataNet con los dos nuevos ítems, filtrados por `IPagePermissionService`.
- [ ] **CA-10**: Las pantallas nuevas aparecen en `/security/page-permissions` en amarillo (sin configurar) tras el primer arranque post-despliegue.
- [ ] **CA-11**: El `PageDiscoveryService` clasifica las nuevas pantallas en el módulo "EcoDataNet".
- [ ] **CA-12**: No hay fugas de datos multi-tenant: un usuario de un tenant NO puede ver usuarios ni configuraciones de otro tenant.
- [ ] **CA-13**: Buscar `ecodatanet` y `PublishData` en todo el repo no devuelve referencias rotas.

---

## 🧪 Plan de pruebas manual

### Test 1 — ADMIN: Configuración conector EDC
1. Login como ADMIN.
2. Navegar a EcoDataNet → Configuración conector EDC.
3. Verificar que aparece la lista de usuarios del tenant con filtro de búsqueda.
4. Seleccionar un usuario sin conector configurado.
5. Rellenar "Nombre del servidor EDC" y "Identificador del conector". Guardar.
6. Verificar Toast de éxito. Volver a la lista. Verificar que el badge "Tiene conector" cambia a "Sí".
7. Seleccionar el mismo usuario. Verificar que los datos guardados se cargan correctamente. Modificar y guardar.

### Test 2 — NO ADMIN: Configuración conector EDC
1. Login como usuario con perfil SCRAP (o cualquier perfil no-ADMIN).
2. Navegar a EcoDataNet → Configuración conector EDC.
3. Verificar que NO aparece la lista de usuarios; se carga directamente el formulario con el nombre del usuario logueado.
4. Rellenar y guardar. Verificar Toast de éxito.
5. Refrescar la página. Verificar que los datos persisten.

### Test 3 — ADMIN: Consumir datos
1. Login como ADMIN.
2. Navegar a EcoDataNet → Consumir datos.
3. Verificar que aparece la lista de perfiles del sistema.
4. Seleccionar un perfil que tenga `ProfileEDCConsumer` configurados en BD.
5. Verificar que el desplegable muestra los perfiles consumibles.
6. Seleccionar un perfil del desplegable. Verificar que aparece el botón "Consumir catálogo".
7. Pulsar el botón. Verificar que aparece el Toast "Funcionalidad pendiente de integración con la plataforma EcoDataNet."

### Test 4 — NO ADMIN: Consumir datos
1. Login como usuario con perfil que tiene `ProfileEDCConsumer` configurados.
2. Navegar a EcoDataNet → Consumir datos.
3. Verificar que no aparece la lista de perfiles; se carga directamente la vista de consumo del perfil logueado.
4. Verificar desplegable y botón "Consumir catálogo" (placeholder).

### Test 5 — Seguridad: aislamiento multi-tenant
1. Login como ADMIN del Tenant A.
2. Verificar que la lista de usuarios en Configuración conector EDC solo muestra usuarios del Tenant A.
3. Intentar acceder vía URL directa pasando un `userId` de otro tenant → debe dar error o no devolver datos.

### Test 6 — Limpieza: pantallas antiguas eliminadas
1. Navegar a la ruta antigua `/ecodatanet/publish` → debe devolver 404 o redirección a Home.
2. En `/security/page-permissions`, verificar que no aparecen las rutas antiguas de EcoDataNet.
3. En el menú lateral, verificar que no aparece "Publicar Datos" ni ningún ítem antiguo de EcoDataNet.

---

## 🧪 Tests automatizados (si el proyecto tiene tests)

Buscar el proyecto `Tests` (`GreenTransit.Tests` o similar) e inspeccionar los patrones de test existentes. Si existen tests unitarios con el patrón del proyecto, sugerir la creación de:

1. **UpsertUserEDCConnectorCommandTests**: handler crea registro nuevo y actualiza existente.
2. **GetUserEDCConnectorQueryTests**: devuelve datos correctos, null para usuario sin conector, falla si otro tenant.
3. **GetUsersForEDCListQueryTests**: solo ADMIN puede ejecutar, filtra por OwnerId, paginación funciona.
4. **GetConsumableProfilesQueryTests**: devuelve perfiles correctos según `ProfileEDCConsumer`.
5. **UpsertUserEDCConnectorValidatorTests**: rechaza `EDCServerName` vacío, `EDCConnectorId` vacío, `UserId <= 0`.

No inventar framework de test — usar el que ya exista en el proyecto (xUnit, NUnit, etc.) con el patrón de mocking existente (Moq, NSubstitute, etc.).
