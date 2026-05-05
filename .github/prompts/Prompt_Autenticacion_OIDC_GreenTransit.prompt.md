# 🔐 Prompt Copilot — Autenticación OIDC + Vinculación de Usuarios + Provisión Automática

> **Instrucciones de uso**: Copia este prompt completo y pégalo en GitHub Copilot Chat (VS Code).
> Antes, adjunta como contexto: `COPILOT_CONTEXT.md`, `README.md`, `Crear_BD_v4_1.sql` y `Mapa_Autorizacion_GreenTransit.md`.
>
> Este prompt sustituye al Paso 6 anterior (que se marcó como completado pero necesita rehacerse con la configuración real y la lógica de vinculación + provisión automática).

---

## PROMPT PARA COPILOT

```
CONTEXTO DEL PROYECTO:
- Proyecto GreenTransit — .NET 10, Blazor Web App (.razor), EF Core, SQL Server Azure.
- Clean Architecture con 5 proyectos:
  GreenTransit.Domain       → Entidades, interfaces puras, sin dependencias externas
  GreenTransit.Application  → Interfaces de servicios, DTOs, MediatR handlers
  GreenTransit.Infrastructure → Implementaciones (DbContext, servicios, auth)
  GreenTransit.Web          → Program.cs, Blazor pages, componentes UI
  GreenTransit.Tests        → xUnit
- AppDbContext ya existe con TODAS las entidades mapeadas (Paso 5 completado).
- Las entidades de dominio Profiles, Users, Entities ya existen en GreenTransit.Domain.
- MediatR, FluentValidation y Serilog ya están configurados.

═══════════════════════════════════════════════════════════════
PARTE 1 — AUTENTICACIÓN OPENID CONNECT
═══════════════════════════════════════════════════════════════

OBJETIVO: Configurar la autenticación contra un servidor de identidad externo con 
protocolo OpenID Connect. El servidor ya existe y los usuarios se crean allí manualmente.

CONFIGURACIÓN DEL PROVEEDOR OIDC:

  "OpenIdConnect": {
    "Authority": "https://pronet-identity-wst-app.azurewebsites.net",
    "ClientId": "GreenTransit",
    "ClientSecret": "c7c3b487-10e4-4772-bfd9-4bcc8ccfbaf6"
  }

IMPLEMENTAR:

1. En appsettings.json (GreenTransit.Web):
   - Añadir la sección OpenIdConnect con Authority, ClientId y ClientSecret.
   - NO hardcodear el ClientSecret en código — leerlo de configuración.

2. En Program.cs (GreenTransit.Web):
   - Configurar Authentication con Cookie + OpenIdConnect:
   
     builder.Services.AddAuthentication(options =>
     {
         options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
         options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
     })
     .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
     {
         options.LoginPath = "/login";
         options.LogoutPath = "/logout";
         options.ExpireTimeSpan = TimeSpan.FromHours(8);
         options.SlidingExpiration = true;
     })
     .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
     {
         options.Authority = builder.Configuration["OpenIdConnect:Authority"];
         options.ClientId = builder.Configuration["OpenIdConnect:ClientId"];
         options.ClientSecret = builder.Configuration["OpenIdConnect:ClientSecret"];
         options.ResponseType = "code";          // Authorization Code Flow
         options.SaveTokens = true;
         options.GetClaimsFromUserInfoEndpoint = true;
         options.Scope.Add("openid");
         options.Scope.Add("profile");
         options.Scope.Add("email");
         // Mapeo de claims — ajustar según lo que devuelva el servidor OIDC
         options.TokenValidationParameters = new TokenValidationParameters
         {
             NameClaimType = "name",
             RoleClaimType = "role"
         };
     });

   - Añadir Authorization:
     builder.Services.AddAuthorization();

   - En el pipeline (después de UseRouting, antes de MapBlazorHub):
     app.UseAuthentication();
     app.UseAuthorization();

   - Proteger TODA la aplicación Blazor requiriendo autenticación:
     app.MapBlazorHub().RequireAuthorization();
     (o usar @attribute [Authorize] en _Imports.razor / App.razor)

3. Crear páginas de Login/Logout (GreenTransit.Web/Pages/):
   
   Login.cshtml / Login.cshtml.cs:
   - GET /login → Challenge con OpenIdConnect (redirige al servidor OIDC)
   - El servidor OIDC devuelve al callback con el Authorization Code
   - ASP.NET intercambia el code por tokens automáticamente

   Logout.cshtml / Logout.cshtml.cs:
   - GET /logout → SignOut de Cookie + OpenIdConnect
   - Redirige al servidor OIDC para cerrar la sesión allí también

4. IMPORTANTE — NO almacenar contraseñas:
   - La aplicación GreenTransit NO guarda passwords. Solo recibe tokens del servidor OIDC.
   - Los usuarios se crean MANUALMENTE en el servidor OIDC.
   - En GreenTransit solo se crea el registro en la tabla Users (ver Parte 2).

═══════════════════════════════════════════════════════════════
PARTE 2 — VINCULACIÓN CON TABLA USERS AL HACER LOGIN (ClaimsTransformation)
═══════════════════════════════════════════════════════════════

OBJETIVO: Cuando un usuario se autentica vía OIDC, el sistema debe buscar su registro 
en la tabla Users (por email o por el claim "sub") y cargar su perfil, OwnerId y 
entidad vinculada. Si el usuario no existe en la tabla Users, se le deniega el acceso.

IMPLEMENTAR:

1. GreenTransitClaimsTransformation.cs (GreenTransit.Infrastructure/Authentication/)
   - Implementa IClaimsTransformation.
   - Se ejecuta automáticamente en cada request autenticado.
   - Lógica:

     a) Extraer el email del ClaimsPrincipal (claim "email" o "preferred_username" o "sub").
     b) Buscar en la tabla Users por Login (WHERE Users.Login = email).
     c) Si NO encuentra el usuario → el usuario no tiene acceso a GreenTransit.
        Añadir un claim custom "gt_user_found" = "false" para que el middleware lo rechace.
     d) Si SÍ encuentra el usuario:
        - Añadir claim "gt_user_id" = Users.ID (int)
        - Añadir claim "gt_profile_id" = Users.IdProfile (int)
        - Añadir claim "gt_profile_ref" = Profiles.Reference (string, ej: "ADMIN")
        - Añadir claim "gt_owner_id" = Users.OwnerId (Guid, si no es null)
        - Añadir claim "gt_user_found" = "true"
        - Buscar si existe una Entity vinculada:
          Buscar en Entities WHERE Email = Users.Email AND IsActive = 1
          Si encuentra → añadir claim "gt_entity_id" = Entities.Id (Guid)
     e) Usar JOIN con Profiles para traer Profiles.Reference en la misma query.
     f) Cachear los claims en el ClaimsPrincipal para no repetir la query en el mismo request.

   - IMPORTANTE: Usar una query eficiente (un solo SELECT con JOIN a Profiles).
   - IMPORTANTE: No cachear en memoria estática — es un servicio scoped.

2. Registrar en Program.cs:
   builder.Services.AddScoped<IClaimsTransformation, GreenTransitClaimsTransformation>();

3. Middleware de rechazo (o configurar en el evento OnTokenValidated):
   Si el claim "gt_user_found" = "false" después de la transformación:
   - Redirigir a una página /acceso-denegado que informe:
     "Tu cuenta existe en el servidor de identidad pero no tienes un usuario 
      asignado en GreenTransit. Contacta con el administrador."
   - NO dar error 403 genérico — dar un mensaje claro.

4. Crear página AccesoDenegado.razor (GreenTransit.Web/Pages/):
   - Página simple con mensaje explicativo.
   - Botón "Cerrar sesión" que redirige a /logout.
   - No requiere autenticación (@attribute [AllowAnonymous]).

5. CurrentUserService.cs (GreenTransit.Infrastructure/Services/)
   - Crear (o modificar si ya existe) el servicio que expone los datos del usuario actual.
   - Interfaz en GreenTransit.Application/Interfaces/ICurrentUserService.cs:

     public interface ICurrentUserService
     {
         int UserId { get; }                    // Users.ID
         string Login { get; }                  // Users.Login
         string Email { get; }                  // Users.Email
         Guid? OwnerId { get; }                 // Users.OwnerId (multi-tenant)
         int ProfileId { get; }                 // Profiles.ID
         string ProfileReference { get; }       // Profiles.Reference ("ADMIN", "CARRIER", etc.)
         Guid? LinkedEntityId { get; }          // Entities.Id vinculada (si existe)
         bool IsAuthenticated { get; }          // true si tiene gt_user_found = "true"
         bool IsInProfile(string profileRef);
         bool IsInAnyProfile(params string[] profileRefs);
     }

   - Implementación: lee los claims "gt_*" del HttpContext.User.
   - Registrar como Scoped:
     builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

═══════════════════════════════════════════════════════════════
PARTE 3 — PROVISIÓN AUTOMÁTICA DE USUARIO AL CREAR ENTIDAD
═══════════════════════════════════════════════════════════════

OBJETIVO: Cuando se crea una nueva Entity en la aplicación, si el EntityRole tiene un 
perfil mapeado, se debe crear automáticamente un registro en la tabla Users con el perfil 
correspondiente. El usuario se crea en el servidor de identidad MANUALMENTE (fuera del 
sistema), pero el registro en la tabla Users se crea automáticamente aquí.

IMPLEMENTAR:

1. EntityRoleToProfileMapping.cs (GreenTransit.Domain/Authorization/)
   - Clase estática con un Dictionary<string, string> que mapea EntityRole → Profiles.Reference.
   - Mapeo:

     | EntityRole         | Profiles.Reference | Crea usuario? |
     |--------------------|--------------------|---------------|
     | "SCRAP"            | "SCRAP"            | ✅ Sí         |
     | "Producer"         | "PRODUCER"         | ✅ Sí         |
     | "Carrier"          | "CARRIER"          | ✅ Sí         |
     | "OperatorTransfer" | "CARRIER"          | ✅ Sí         |
     | "Plant"            | "PLANT_OP"         | ✅ Sí         |
     | "CAC"              | "CAC_OP"           | ✅ Sí         |
     | "PublicEntity"     | "PUBLIC_ENT"       | ✅ Sí         |
     | "Coordinator"      | "COORDINATOR"      | ✅ Sí         |
     | "Source"            | —                  | ❌ No         |
     | "Destination"       | —                  | ❌ No         |
     | "Other"            | —                  | ❌ No         |

   - Método: bool ShouldCreateUser(string entityRole)
   - Método: string? GetProfileReference(string entityRole)

2. IEntityUserProvisioningService.cs (GreenTransit.Application/Interfaces/)
   - Interfaz:
     Task<int?> ProvisionUserForEntityAsync(Entity entity, CancellationToken ct);
   - Devuelve el Users.ID creado, o null si el EntityRole no requiere usuario.

3. EntityUserProvisioningService.cs (GreenTransit.Infrastructure/Services/)
   - Inyecta AppDbContext y ICurrentUserService.
   - Lógica de ProvisionUserForEntityAsync:

     a) Verificar que EntityRoleToProfileMapping.ShouldCreateUser(entity.EntityRole) == true.
        Si no → return null.
     
     b) Verificar que entity.Email no esté vacío.
        Si está vacío → usar entity.NationalId como Login fallback.
        Si ambos vacíos → lanzar excepción descriptiva.
     
     c) Verificar que no exista ya un Users con ese Login.
        Si existe → NO crear duplicado. Devolver el ID del usuario existente.
        (Puede pasar si la entidad se edita y se vuelve a guardar)
     
     d) Buscar el Profiles.ID correspondiente al perfil mapeado:
        SELECT ID FROM Profiles WHERE Reference = @profileRef
     
     e) Resolver las FKs geográficas a partir de los códigos de la Entity:
        - entity.CountryCode → buscar Country.id WHERE Code = @countryCode → Users.NationalId
        - entity.StateCode → buscar TerritoryState.id WHERE Code = @stateCode → Users.GeographicalId
        - entity.MunicipalityCode → buscar Municipality.Id WHERE Code = @municipalityCode → Users.MunicipalityId
        (Si algún código no existe o es null → dejar la FK en null, no fallar)
     
     f) Crear el registro Users:
        Users.Login = entity.Email ?? entity.NationalId
        Users.Email = entity.Email
        Users.IdProfile = profileId (del paso d)
        Users.OwnerId = entity.OwnerId (heredar del tenant de la entidad)
        Users.NationalId = countryId (del paso e)
        Users.GeographicalId = stateId (del paso e)
        Users.MunicipalityId = municipalityId (del paso e)
     
     g) SaveChangesAsync.
     h) Return el Users.ID creado.

   - TODO en una transacción (junto con la creación de la Entity).

4. Integración con el CRUD de Entities:
   - Cuando se use el CreateEntityCommand (MediatR handler para crear entidades):
     Después de insertar la Entity y antes del SaveChanges (o en la misma transacción):
     Llamar a IEntityUserProvisioningService.ProvisionUserForEntityAsync(entity, ct).
   
   - Si el handler de crear entidad NO existe aún, documentar el patrón:
     
     // En el handler de CreateEntityCommand:
     var entity = new Entity { ... }; // mapear desde el command
     _dbContext.Entities.Add(entity);
     
     // Provisión automática de usuario
     var userId = await _provisioningService.ProvisionUserForEntityAsync(entity, ct);
     
     await _dbContext.SaveChangesAsync(ct); // todo en la misma transacción
   
   - NOTA: Si la Entity se desactiva (IsActive = false), el usuario vinculado debería
     marcarse como inactivo también. Implementar esto en el UpdateEntityCommand:
     Buscar Users WHERE Login = entity.Email, y si entity.IsActive = false → 
     marcar como inactivo (añadir un campo IsActive a Users si no existe, 
     o simplemente documentarlo para implementación futura).

5. Desplegable de EntityRole en el formulario de Entidades:
   - El campo EntityRole debe ser un <select> / dropdown con TODOS los valores posibles:
     Source, Destination, Carrier, OperatorTransfer, SCRAP, Producer, Plant, CAC, 
     PublicEntity, Coordinator, Other
   
   - Cuando el usuario seleccione un EntityRole que tiene perfil mapeado, 
     mostrar un mensaje informativo debajo del desplegable:
     "Al crear esta entidad se generará automáticamente un usuario con perfil [NOMBRE_PERFIL].
      El email de la entidad se usará como Login del usuario."
   
   - Si el EntityRole NO tiene perfil mapeado (Source, Destination, Other):
     Mostrar: "Este rol no genera un usuario de acceso al sistema."
   
   - Si el email está vacío y el EntityRole requiere usuario:
     Mostrar validación: "El email es obligatorio para este rol porque se creará un usuario."

═══════════════════════════════════════════════════════════════
PARTE 4 — SEED DE PERFILES
═══════════════════════════════════════════════════════════════

Asegurarse de que la tabla Profiles contenga los 9 perfiles del sistema.
Crear un seeder o usar HasData en el DbContext:

INSERT INTO Profiles (Reference, Description) VALUES
('ADMIN',           'Administrador del sistema'),
('SCRAP',           'Sistema Colectivo de Responsabilidad Ampliada'),
('PRODUCER',        'Productor / Generador de residuos'),
('CARRIER',         'Transportista'),
('PLANT_OP',        'Operador de Planta de Tratamiento'),
('CAC_OP',          'Operador de Centro de Acopio'),
('PUBLIC_ENT',      'Entidad Pública / Ayuntamiento'),
('COORDINATOR',     'Coordinador del acuerdo'),
('DISPATCH_OFFICE', 'Oficina de Asignación — Gestor logístico');

El seed debe ser IDEMPOTENTE — no insertar duplicados si ya existen.

═══════════════════════════════════════════════════════════════
RESUMEN DE ARCHIVOS A GENERAR/MODIFICAR
═══════════════════════════════════════════════════════════════

NUEVOS:
  GreenTransit.Domain/Authorization/EntityRoleToProfileMapping.cs
  GreenTransit.Application/Interfaces/ICurrentUserService.cs
  GreenTransit.Application/Interfaces/IEntityUserProvisioningService.cs
  GreenTransit.Infrastructure/Authentication/GreenTransitClaimsTransformation.cs
  GreenTransit.Infrastructure/Services/CurrentUserService.cs
  GreenTransit.Infrastructure/Services/EntityUserProvisioningService.cs
  GreenTransit.Web/Pages/Login.cshtml + Login.cshtml.cs
  GreenTransit.Web/Pages/Logout.cshtml + Logout.cshtml.cs
  GreenTransit.Web/Pages/AccesoDenegado.razor

MODIFICADOS:
  GreenTransit.Web/appsettings.json (añadir sección OpenIdConnect)
  GreenTransit.Web/Program.cs (autenticación, autorización, registro de servicios)
  GreenTransit.Infrastructure/Persistence/AppDbContext.cs (seed de Profiles si se usa HasData)

═══════════════════════════════════════════════════════════════
REGLAS GENERALES
═══════════════════════════════════════════════════════════════

1. Respetar Clean Architecture: Domain no referencia a ningún otro proyecto.
2. Interfaces en Application, implementaciones en Infrastructure.
3. Nombrar todo en inglés (clases, métodos, variables). Comentarios en español.
4. No generar código que ya exista — preguntar antes si hay duda.
5. Usar los NuGet packages correctos:
   - Microsoft.AspNetCore.Authentication.OpenIdConnect
   - Microsoft.AspNetCore.Authentication.Cookies
   - Microsoft.IdentityModel.Tokens
6. El ClientSecret debe leerse de configuración (appsettings.json), 
   NUNCA hardcodeado en el código C#.

═══════════════════════════════════════════════════════════════
FLUJO COMPLETO QUE DEBE FUNCIONAR AL TERMINAR
═══════════════════════════════════════════════════════════════

FLUJO DE LOGIN:
1. Usuario accede a la app → no está autenticado → redirige a /login
2. /login hace Challenge OIDC → redirige al servidor de identidad
3. Usuario se autentica en el servidor OIDC → vuelve con Authorization Code
4. ASP.NET intercambia el code por tokens (ID Token + Access Token)
5. GreenTransitClaimsTransformation se ejecuta:
   - Extrae email del token
   - Busca en tabla Users por Login = email
   - Si NO existe → claim gt_user_found = false → redirige a /acceso-denegado
   - Si SÍ existe → añade claims gt_user_id, gt_profile_ref, gt_owner_id, gt_entity_id
6. CurrentUserService expone todos los datos del usuario para el resto de la app
7. El usuario ve solo lo que su perfil permite (menú, datos, acciones)

FLUJO DE CREAR ENTIDAD:
1. Un usuario con perfil DISPATCH_OFFICE o ADMIN accede a la pantalla de Entidades
2. Rellena el formulario: Nombre, NIF, Email, EntityRole (desplegable), etc.
3. Si selecciona EntityRole = "Carrier":
   - El sistema muestra: "Se creará un usuario con perfil CARRIER"
   - El email es obligatorio
4. Al guardar:
   a) Se crea el registro en la tabla Entities
   b) EntityUserProvisioningService detecta que "Carrier" → CARRIER
   c) Se crea el registro en la tabla Users con Login = Email, IdProfile = CARRIER
   d) Todo en la misma transacción
5. El administrador crea MANUALMENTE el mismo usuario en el servidor OIDC 
   (con el mismo email como identificador)
6. Cuando ese usuario haga login, el ClaimsTransformation lo encontrará en la tabla Users
   y le asignará el perfil CARRIER con todos sus permisos

VERIFICACIÓN FINAL:
- La app compila y arranca sin errores.
- Al acceder sin login → redirige al servidor OIDC.
- Al autenticarse con un usuario que SÍ existe en Users → entra y ve su perfil.
- Al autenticarse con un usuario que NO existe en Users → ve /acceso-denegado.
- Al crear una entidad con EntityRole = "Plant" → se crea un User con perfil PLANT_OP.
- Al crear una entidad con EntityRole = "Source" → NO se crea usuario.
- CurrentUserService.ProfileReference devuelve el perfil correcto.
- CurrentUserService.OwnerId devuelve el tenant correcto.
```

---

## Notas adicionales para el desarrollador

### ¿Qué pasa si el servidor OIDC devuelve claims diferentes?

El mapeo de claims (`email`, `preferred_username`, `sub`) depende de cómo esté configurado el servidor de identidad. Si al probar ves que el claim del email viene con otro nombre, ajusta la línea de extracción en `GreenTransitClaimsTransformation`. Puedes inspeccionar los claims recibidos añadiendo un log temporal:

```csharp
foreach (var claim in principal.Claims)
    _logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
```

### ¿Y el campo OwnerId?

El servidor OIDC probablemente NO envía el OwnerId como claim. No pasa nada — el `GreenTransitClaimsTransformation` lo lee directamente de la tabla `Users.OwnerId` en la base de datos, no del token. El token solo necesita traer el email para identificar al usuario; todo lo demás se resuelve en la base de datos local.

### Orden de ejecución respecto a los prompts de Autorización (8.x)

Este prompt debe ejecutarse ANTES de los prompts 8.1–8.10 del archivo `Prompts_Autorizacion_GreenTransit.md`, ya que establece la base sobre la que se construye la autorización. En concreto:

- Este prompt crea `ICurrentUserService` con `ProfileReference` → el Prompt 8.3 lo usa para los AuthorizationHandlers.
- Este prompt crea `EntityRoleToProfileMapping` → el Prompt 8.1 lo referencia para las constantes.
- Este prompt crea el seed de Profiles → el Prompt 8.7 lo verifica.

La secuencia correcta es: **Este prompt → 8.1 → 8.2 (ya no necesario, se hizo aquí) → 8.3 → 8.4 → ...**.
