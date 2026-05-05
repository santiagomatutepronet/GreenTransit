# 🛠️ Prompts de Implementación — Módulo de Autorización GreenTransit

> **Instrucciones de uso**: Ejecutar estos prompts en orden en GitHub Copilot Chat (VS Code).
> Antes de cada sesión, adjuntar como contexto: `COPILOT_CONTEXT.md`, `README.md`, `Crear_BD_v4_1.sql` y `Mapa_Autorizacion_GreenTransit.md`.
>
> **Convención de nomenclatura**: Los prompts siguen la numeración del proyecto existente (Pasos 1–7 completados). La autorización es el **Paso 8**.
>
> **Importante**: Cada prompt incluye una sección de verificación. No avances al siguiente sin confirmar que el anterior compila y pasa los tests.

---

## Prompt 8.0 — Instrucción base para toda la serie de autorización

```
CONTEXTO DEL PROYECTO:
- Proyecto GreenTransit — .NET 10, Blazor Web App, EF Core, SQL Server Azure.
- Clean Architecture: GreenTransit.Domain / Application / Infrastructure / Web / Tests.
- Autenticación OpenID Connect YA IMPLEMENTADA (Paso 6): Program.cs configurado, ClaimsTransformation, CurrentUserService.
- El CurrentUserService ya expone: UserId (int), OwnerId (Guid?), Email (string), Login (string).
- Entidades de dominio YA GENERADAS (Paso 5): Profiles, Users y todas las demás.
- MediatR, FluentValidation, Serilog, xUnit ya configurados.

OBJETIVO:
Implementar el sistema completo de autorización basado en perfiles (Profiles → Users).
El documento de referencia es Mapa_Autorizacion_GreenTransit.md.

PERFILES DEL SISTEMA (tabla Profiles):
- ADMIN, SCRAP, PRODUCER, CARRIER, PLANT_OP, CAC_OP, PUBLIC_ENT, COORDINATOR, DISPATCH_OFFICE

REGLAS GENERALES QUE APLICAN A TODOS LOS PROMPTS DE ESTA SERIE:
1. Respetar Clean Architecture: Domain no referencia a ningún otro proyecto.
2. Interfaces en Application, implementaciones en Infrastructure.
3. No duplicar lógica — reutilizar servicios existentes (CurrentUserService, AppDbContext).
4. Usar el sistema de policies de ASP.NET Core (IAuthorizationRequirement + AuthorizationHandler).
5. Multi-tenant: el filtro por OwnerId ya existe. La autorización añade filtro por perfil.
6. Nombrar todo en inglés (clases, métodos, variables). Comentarios en español.
7. Cada archivo nuevo debe tener su namespace correcto según la capa.
8. No generar código que ya exista — preguntar antes si hay duda.

NO generes código aún. Solo confirma que has entendido el contexto respondiendo con un resumen de lo que vas a implementar en los siguientes prompts.
```

---

## Prompt 8.1 — Constantes de perfiles y enumeración

```
PASO 8.1 — Constantes de perfiles y permisos

OBJETIVO: Crear las constantes que representan los perfiles y los permisos del sistema, 
de forma que el resto de la implementación las referencie sin strings mágicos.

UBICACIÓN: GreenTransit.Domain/Authorization/

CREAR estos archivos:

1. ProfileConstants.cs
   - Clase estática con constantes string para cada perfil:
     ADMIN, SCRAP, PRODUCER, CARRIER, PLANT_OP, CAC_OP, PUBLIC_ENT, COORDINATOR, DISPATCH_OFFICE
   - Los valores deben coincidir EXACTAMENTE con Profiles.Reference en la BD.

2. PermissionType.cs
   - Enum con los tipos de permiso: None, Read, ReadOwn, Create, CreateOwn, 
     Update, UpdateOwn, Delete, FullCrud, FullCrudOwn, CreateAndRead, Validate

3. PolicyConstants.cs
   - Clase estática con constantes string para cada policy de autorización.
   - Policies necesarias (basadas en el Mapa de Autorización):

   // Maestros
   CanManageEntities         // CRUD: DISPATCH_OFFICE, ADMIN
   CanCreateEntitiesRestricted // C+R: SCRAP (ámbito restringido)
   CanManageLER              // CRUD: ADMIN
   CanManageResidues         // CRUD: DISPATCH_OFFICE, ADMIN
   CanManageOwnResidues      // CRUD-P: PRODUCER (Product/ProductSpec)
   CanManageTreatmentOps     // CRUD: ADMIN

   // Operaciones
   CanManageServiceOrders    // CRUD: DISPATCH_OFFICE, ADMIN
   CanCreateOwnServiceOrders // CRUD-P: PRODUCER, PUBLIC_ENT
   CanManageWasteMoves       // CRUD: DISPATCH_OFFICE, ADMIN
   CanUpdateAssignedMoves    // U-P: CARRIER
   CanManageEntryPlants      // CRUD-P: PLANT_OP; CRUD: ADMIN
   CanManageEntryCACs        // CRUD-P: CAC_OP; CRUD: ADMIN
   CanManageTreatments       // CRUD-P: PLANT_OP; CRUD: ADMIN

   // Sostenibilidad
   CanCreateIncidents        // C+R: TODOS los autenticados
   CanResolveIncidents       // CRUD: DISPATCH_OFFICE, ADMIN
   CanManageDUMZones         // CRUD: ADMIN
   CanManagePlantEnergy      // CRUD-P: PLANT_OP; CRUD: ADMIN
   CanManageEmissionFactors  // CRUD: ADMIN

   // Reporting
   CanViewKPIs               // R: SCRAP, PUBLIC_ENT, PLANT_OP, COORDINATOR, DISPATCH_OFFICE, ADMIN
   CanViewReporting          // R/R-P: TODOS (con filtrado)

   // Seguridad
   CanManageUsers            // CRUD: ADMIN
   CanManageProfiles         // CRUD: ADMIN
   CanViewOwnUsers           // R-P: SCRAP

VERIFICACIÓN:
- El proyecto GreenTransit.Domain compila sin errores.
- No hay dependencias externas en Domain (solo System.*).
- Las constantes son const string, no readonly.
```

---

## Prompt 8.2 — Extender CurrentUserService con perfil y entidad vinculada

```
PASO 8.2 — Extender CurrentUserService con información de perfil

CONTEXTO: El CurrentUserService ya existe (Paso 6) y expone UserId, OwnerId, Email, Login.
Necesitamos ampliarlo para que también exponga el perfil del usuario y su entidad vinculada.

OBJETIVO: Que cualquier componente del sistema pueda consultar el perfil del usuario 
autenticado sin hacer queries adicionales a la BD en cada petición.

MODIFICACIONES:

1. En GreenTransit.Application/Interfaces/ICurrentUserService.cs
   AÑADIR estas propiedades a la interfaz (sin romper las existentes):
   - string ProfileReference { get; }    // Profiles.Reference del usuario (ADMIN, CARRIER, etc.)
   - int ProfileId { get; }              // Profiles.ID
   - Guid? LinkedEntityId { get; }       // Id de la Entity vinculada al usuario (si existe)
   - bool IsInProfile(string profileRef) // Comprueba si el usuario tiene un perfil concreto
   - bool IsInAnyProfile(params string[] profileRefs) // Comprueba si tiene alguno de los perfiles

2. En GreenTransit.Infrastructure/Services/CurrentUserService.cs
   MODIFICAR la implementación para:
   - En el ClaimsTransformation (o al resolver el servicio), cargar de BD:
     Users.IdProfile → Profiles.Reference
     Buscar en Entities si existe una entidad vinculada al usuario (por Users.OwnerId + lógica de mapeo)
   - Cachear estos valores en el servicio (scoped, una vez por request).
   - Implementar IsInProfile e IsInAnyProfile comparando contra ProfileReference.

3. IMPORTANTE: 
   - No romper la funcionalidad existente de CurrentUserService.
   - El servicio es Scoped (una instancia por request HTTP).
   - Si el usuario no está autenticado, ProfileReference = "" y LinkedEntityId = null.
   - La carga desde BD debe ser LAZY (solo al primer acceso) para no penalizar requests 
     que no necesiten el perfil.

VERIFICACIÓN:
- El proyecto compila.
- CurrentUserService sigue funcionando para lo que ya hacía.
- Las nuevas propiedades devuelven datos correctos.
```

---

## Prompt 8.3 — Requirements y Handlers de autorización

```
PASO 8.3 — Authorization Requirements y Handlers

OBJETIVO: Implementar el motor de autorización de ASP.NET Core con Requirements y Handlers
que evalúan los permisos del usuario según su perfil.

UBICACIÓN: GreenTransit.Infrastructure/Authorization/

CREAR:

1. ProfileRequirement.cs
   - Implementa IAuthorizationRequirement.
   - Constructor recibe params string[] allowedProfiles.
   - Propiedad pública: IReadOnlyList<string> AllowedProfiles.

2. ProfileAuthorizationHandler.cs
   - Hereda de AuthorizationHandler<ProfileRequirement>.
   - Inyecta ICurrentUserService.
   - Lógica de HandleRequirementAsync:
     a) Si el usuario no está autenticado → fail (no hacer nada, ASP.NET ya falla).
     b) Obtener currentUser.ProfileReference.
     c) Si el perfil está en AllowedProfiles → context.Succeed(requirement).
     d) Si no → no hacer nada (deja que otros handlers operen o que falle).

3. OwnDataRequirement.cs
   - Implementa IAuthorizationRequirement.
   - Para validaciones de "datos propios" (CRUD-P, R-P, U-P).
   - Constructor recibe params string[] allowedProfiles.
   - Propiedad: IReadOnlyList<string> AllowedProfiles.
   - Propiedad: bool RequiresEntityLink (indica si el perfil necesita entidad vinculada).

4. OwnDataAuthorizationHandler.cs
   - Hereda de AuthorizationHandler<OwnDataRequirement>.
   - Inyecta ICurrentUserService.
   - Lógica:
     a) Verificar que el perfil está en AllowedProfiles.
     b) Verificar que LinkedEntityId != null (si RequiresEntityLink = true).
     c) Succeed si ambas condiciones se cumplen.
   - NOTA: Este handler valida que el usuario PUEDE operar con datos propios.
     El filtrado real de datos (WHERE IdCarrier = @entityId) se hace en los query handlers 
     de MediatR, no aquí. Este handler solo confirma el "derecho a intentarlo".

IMPORTANTE:
- NO crear un handler por cada policy. Un solo ProfileAuthorizationHandler maneja TODAS 
  las policies basadas en perfil. La diferencia entre policies está en qué perfiles 
  incluye cada ProfileRequirement.
- Los handlers deben ser thread-safe y stateless (solo dependen de ICurrentUserService scoped).

VERIFICACIÓN:
- Infrastructure compila.
- Los handlers no acceden directamente a DbContext (usan ICurrentUserService).
```

---

## Prompt 8.4 — Registro de policies en Program.cs

```
PASO 8.4 — Registrar todas las policies de autorización en Program.cs

OBJETIVO: Configurar AddAuthorization con todas las policies definidas en PolicyConstants,
asociando cada policy con su ProfileRequirement y los perfiles permitidos.

UBICACIÓN: GreenTransit.Web/Program.cs (sección de servicios, después de AddAuthentication).

MODIFICACIONES:

1. Registrar los handlers como servicios:
   builder.Services.AddScoped<IAuthorizationHandler, ProfileAuthorizationHandler>();
   builder.Services.AddScoped<IAuthorizationHandler, OwnDataAuthorizationHandler>();

2. Configurar builder.Services.AddAuthorization(options => { ... }) con TODAS las policies.
   Usar las constantes de PolicyConstants y ProfileConstants.

   MAPEO COMPLETO (extraído del Mapa de Autorización):

   // === MAESTROS ===
   CanManageEntities → DISPATCH_OFFICE, ADMIN
   CanCreateEntitiesRestricted → SCRAP (OwnDataRequirement)
   CanManageLER → ADMIN
   CanManageResidues → DISPATCH_OFFICE, ADMIN
   CanManageOwnResidues → PRODUCER (OwnDataRequirement)
   CanManageTreatmentOps → ADMIN

   // === OPERACIONES ===
   CanManageServiceOrders → DISPATCH_OFFICE, ADMIN
   CanCreateOwnServiceOrders → PRODUCER, PUBLIC_ENT (OwnDataRequirement)
   CanManageWasteMoves → DISPATCH_OFFICE, ADMIN
   CanUpdateAssignedMoves → CARRIER (OwnDataRequirement, RequiresEntityLink=true)
   CanManageEntryPlants → PLANT_OP, ADMIN (PLANT_OP con OwnDataRequirement)
   CanManageEntryCACs → CAC_OP, ADMIN (CAC_OP con OwnDataRequirement)
   CanManageTreatments → PLANT_OP, ADMIN (PLANT_OP con OwnDataRequirement)

   // === SOSTENIBILIDAD ===
   CanCreateIncidents → TODOS los perfiles (usar RequireAuthenticatedUser como base)
   CanResolveIncidents → DISPATCH_OFFICE, ADMIN
   CanManageDUMZones → ADMIN
   CanManagePlantEnergy → PLANT_OP, ADMIN
   CanManageEmissionFactors → ADMIN

   // === REPORTING ===
   CanViewKPIs → SCRAP, PUBLIC_ENT, PLANT_OP, COORDINATOR, DISPATCH_OFFICE, ADMIN
   CanViewReporting → TODOS los perfiles autenticados

   // === SEGURIDAD ===
   CanManageUsers → ADMIN
   CanManageProfiles → ADMIN
   CanViewOwnUsers → SCRAP (OwnDataRequirement)

3. Para policies que mezclan acceso completo (ADMIN) con acceso propio (PLANT_OP), 
   registrar DOS requirements en la misma policy con lógica OR:
   - Opción A: Crear un CompositeRequirement que tenga una lista de perfiles con acceso 
     completo Y otra lista de perfiles con acceso propio.
   - Opción B: Registrar la policy con el set completo de perfiles y manejar la distinción 
     "completo vs propio" en el query handler de MediatR (recomendado — más simple).

   USAR OPCIÓN B: La policy solo valida "¿puede acceder?". 
   El query handler decide "¿ve todo o solo lo suyo?" basándose en el perfil.

VERIFICACIÓN:
- Program.cs compila.
- La aplicación arranca sin errores.
- Verificar en los logs que no hay warnings de policies no registradas.
```

---

## Prompt 8.5 — Servicio de filtrado de datos por perfil (Data Scope)

```
PASO 8.5 — Servicio de filtrado de datos por perfil (IDataScopeService)

OBJETIVO: Crear un servicio que los query handlers de MediatR usen para aplicar el filtrado
de datos según el perfil del usuario. Este servicio responde a la pregunta:
"¿Este usuario ve TODOS los datos del tenant o solo los SUYOS?"

UBICACIÓN:
- Interfaz: GreenTransit.Application/Interfaces/IDataScopeService.cs
- Implementación: GreenTransit.Infrastructure/Services/DataScopeService.cs

CREAR:

1. IDataScopeService.cs
   Métodos:
   
   // Indica si el usuario actual tiene acceso completo (ve todo del tenant) o restringido
   bool HasFullAccess(string functionalArea);
   
   // Devuelve el EntityId para filtrar, o null si tiene acceso completo
   Guid? GetEntityFilter(string functionalArea);
   
   // Aplica el filtro apropiado a un IQueryable de ServiceOrders
   IQueryable<ServiceOrder> ApplyScope(IQueryable<ServiceOrder> query);
   
   // Aplica el filtro apropiado a un IQueryable de WasteMoves
   IQueryable<WasteMove> ApplyScope(IQueryable<WasteMove> query);
   
   // Aplica el filtro apropiado a un IQueryable de EntryPlants
   IQueryable<EntryPlant> ApplyScope(IQueryable<EntryPlant> query);
   
   // Aplica el filtro apropiado a un IQueryable de EntryCACs
   IQueryable<EntryCAC> ApplyScope(IQueryable<EntryCAC> query);
   
   // Aplica el filtro apropiado a un IQueryable de TreatmentPlants
   IQueryable<TreatmentPlant> ApplyScope(IQueryable<TreatmentPlant> query);
   
   // Aplica el filtro apropiado a un IQueryable de Incidents
   IQueryable<Incident> ApplyScope(IQueryable<Incident> query);
   
   // Aplica el filtro apropiado a un IQueryable de Residues
   IQueryable<Residue> ApplyScope(IQueryable<Residue> query);

2. DataScopeService.cs
   - Inyecta ICurrentUserService.
   - Lógica de filtrado según perfil:

   REGLAS DE FILTRADO (del Mapa de Autorización §3.2):

   ServiceOrders:
     PRODUCER → WHERE IdIssuedBy = LinkedEntityId
     PUBLIC_ENT → WHERE IdIssuedBy = LinkedEntityId
     Resto con acceso → sin filtro adicional (ya filtra por OwnerId)

   WasteMoves:
     PRODUCER → WHERE ServiceOrderId IN (SOs del productor) — o join lógico
     CARRIER → WHERE EXISTS (WasteMoveResidues WHERE IdCarrier = LinkedEntityId)
     Resto → sin filtro adicional

   EntryPlants:
     PLANT_OP → WHERE OwnerId = OwnerId (ya filtrado) — la planta solo ve sus entradas
     Resto → sin filtro adicional

   EntryCACs:
     CAC_OP → WHERE OwnerId = OwnerId — solo su CAC
     Resto → sin filtro adicional

   TreatmentPlants:
     PLANT_OP → igual que EntryPlants
     Resto → sin filtro adicional

   Incidents:
     Sin filtro adicional por perfil (todos ven todas las del tenant)

   Residues:
     PRODUCER → WHERE IdProducer = LinkedEntityId AND ResidueType IN ('Product','ProductSpec')
     Resto → sin filtro adicional

3. Registrar como Scoped en Program.cs:
   builder.Services.AddScoped<IDataScopeService, DataScopeService>();

VERIFICACIÓN:
- Compila.
- Los métodos ApplyScope devuelven IQueryable (no materializan la query).
- El filtrado es composable con el filtro de OwnerId ya existente.
```

---

## Prompt 8.6 — Atributo de autorización para componentes Blazor

```
PASO 8.6 — Componente de autorización para Blazor

OBJETIVO: Crear los componentes y atributos necesarios para proteger las páginas Blazor 
con las policies definidas, y para mostrar/ocultar elementos de menú según el perfil.

UBICACIÓN: GreenTransit.Web/

CREAR:

1. Components/Authorization/ProfileAuthorizeView.razor
   - Componente wrapper que muestra contenido solo si el usuario tiene el perfil adecuado.
   - Uso:
     <ProfileAuthorizeView Profiles="@(new[]{ProfileConstants.ADMIN, ProfileConstants.DISPATCH_OFFICE})">
         <Authorized>Contenido visible</Authorized>
         <NotAuthorized>Sin acceso</NotAuthorized>
     </ProfileAuthorizeView>
   - Internamente usa AuthorizeView de Blazor + CascadingAuthenticationState.
   - Inyecta ICurrentUserService para comprobar el perfil.

2. Components/Layout/NavMenuAuthorized.razor (o modificar el NavMenu existente)
   - Mostrar/ocultar elementos del menú según el perfil del usuario.
   - Estructura del menú:

   ENTIDADES (visible para todos los autenticados, las opciones internas varían):
     - Entidades → todos
     - LER → todos
     - Residuos → todos
     - Operaciones R/D → todos

   OPERACIONES:
     - Órdenes de Servicio → todos
     - Traslados → todos EXCEPTO PRODUCER (ve R-P en reporting, no en operaciones directas)
     - Entradas Planta → PLANT_OP, ADMIN, SCRAP, PUBLIC_ENT, COORDINATOR, DISPATCH_OFFICE
     - Entradas CAC → CAC_OP, ADMIN, SCRAP, PUBLIC_ENT, COORDINATOR, DISPATCH_OFFICE
     - Tratamiento → PLANT_OP, ADMIN, SCRAP, PUBLIC_ENT, COORDINATOR, DISPATCH_OFFICE

   SOSTENIBILIDAD:
     - Incidencias → todos
     - Zonas DUM → todos EXCEPTO CAC_OP, PLANT_OP
     - Simulador DUM → todos EXCEPTO CAC_OP, PLANT_OP
     - Emisiones → todos EXCEPTO CAC_OP
     - Energía Planta → PLANT_OP, ADMIN, SCRAP, PUBLIC_ENT, COORDINATOR, DISPATCH_OFFICE
     - Factores Emisión → ADMIN, SCRAP, PUBLIC_ENT, PLANT_OP, COORDINATOR, DISPATCH_OFFICE

   REPORTING:
     - Trazabilidad → todos
     - Vista 360° → todos
     - KPIs → SCRAP, PUBLIC_ENT, PLANT_OP, COORDINATOR, DISPATCH_OFFICE, ADMIN
     - Documentos → todos

   SEGURIDAD (solo visible para ADMIN y SCRAP):
     - Usuarios → ADMIN, SCRAP
     - Perfiles → ADMIN, SCRAP

3. Proteger cada página Blazor con el atributo @attribute [Authorize(Policy = "...")]:
   - NO implementar las páginas ahora — solo documentar el patrón a seguir.
   - Crear un archivo PATRON_AUTORIZACION_PAGINAS.md con el mapeo:
     /entidades → Policy: RequireAuthenticatedUser (lectura base) + comprobar en código si puede editar
     /traslados → Policy: RequireAuthenticatedUser + comprobar en código
     /usuarios → Policy: CanManageUsers
     etc.
   
   Patrón recomendado para páginas con permisos mixtos (ej: todos ven, pero solo algunos editan):
   - La página se protege con @attribute [Authorize] (cualquier autenticado).
   - Los botones de Crear/Editar/Eliminar se envuelven en <ProfileAuthorizeView>.
   - La query de datos usa IDataScopeService para filtrar.

VERIFICACIÓN:
- Los componentes compilan.
- El NavMenu muestra solo las opciones permitidas para cada perfil.
```

---

## Prompt 8.7 — Seed de perfiles en la base de datos

```
PASO 8.7 — Seed de datos: Perfiles y usuario administrador

OBJETIVO: Crear la migración o seed que inserte los 9 perfiles en la tabla Profiles si no existen
y un usuario administrador inicial si no existe

UBICACIÓN: GreenTransit.Infrastructure/Persistence/

CREAR O MODIFICAR:

1. Seed de Profiles
   Si ya existe un método de seed (HasData en el DbContext o un DataSeeder):
   - AÑADIR el perfil DISPATCH_OFFICE a los perfiles existentes.
   - Verificar que los 9 perfiles existen:

   | ID | Reference         | Description                                      |
   |----|-------------------|--------------------------------------------------|
   | 1  | ADMIN             | Administrador del sistema                        |
   | 2  | SCRAP             | Sistema Colectivo de Responsabilidad Ampliada    |
   | 3  | PRODUCER          | Productor / Generador de residuos                |
   | 4  | CARRIER           | Transportista                                    |
   | 5  | PLANT_OP          | Operador de Planta de Tratamiento                |
   | 6  | CAC_OP            | Operador de Centro de Acopio                     |
   | 7  | PUBLIC_ENT        | Entidad Pública / Ayuntamiento                   |
   | 8  | COORDINATOR       | Coordinador del acuerdo                          |
   | 9  | DISPATCH_OFFICE   | Oficina de Asignación — Gestor logístico         |

   Si NO existe un método de seed:
   - Crear AppDbContextSeeder.cs con un método estático SeedAsync(AppDbContext context).
   - Que inserte los perfiles solo si la tabla está vacía (idempotente).
   - Llamarlo desde Program.cs después de app.UseAuthorization().

2. Crear migración EF Core:
   dotnet ef migrations add AddDispatchOfficeProfile -p GreenTransit.Infrastructure -s GreenTransit.Web
   
   Solo si se usa HasData. Si se usa un seeder manual, no hace falta migración.

3. Script SQL alternativo (para ejecución directa si se prefiere):
   Generar un script INSERT que sea idempotente (IF NOT EXISTS).

VERIFICACIÓN:
- Ejecutar la aplicación.
- Verificar en la BD que existen los 9 perfiles.
- Verificar que el DISPATCH_OFFICE tiene ID asignado.
```

---

## Prompt 8.8 — Pipeline de autorización en MediatR (Behavior)

```
PASO 8.8 — MediatR Authorization Behavior (pipeline transversal)

OBJETIVO: Crear un behavior de MediatR que valide automáticamente la autorización 
antes de ejecutar cualquier command/query, sin tener que repetir la lógica en cada handler.

UBICACIÓN:
- Atributo: GreenTransit.Application/Authorization/AuthorizeAttribute.cs
- Behavior: GreenTransit.Application/Behaviors/AuthorizationBehavior.cs
- Excepción: GreenTransit.Application/Exceptions/ForbiddenAccessException.cs

CREAR:

1. AuthorizeAttribute.cs (atributo para decorar requests de MediatR)
   [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
   public class AuthorizeAttribute : Attribute
   {
       public string Policy { get; set; } = "";
       public string Profiles { get; set; } = ""; // CSV: "ADMIN,DISPATCH_OFFICE"
   }

2. ForbiddenAccessException.cs
   public class ForbiddenAccessException : Exception
   {
       public ForbiddenAccessException() : base("Acceso denegado.") { }
       public ForbiddenAccessException(string message) : base(message) { }
   }

3. AuthorizationBehavior.cs
   - Implementa IPipelineBehavior<TRequest, TResponse>.
   - En Handle:
     a) Obtener los [Authorize] attributes del TRequest via reflexión.
     b) Si no tiene ninguno → continuar (no requiere autorización).
     c) Si tiene:
        - Verificar que el usuario está autenticado (ICurrentUserService.UserId > 0).
        - Si el atributo tiene Profiles → comprobar con ICurrentUserService.IsInAnyProfile().
        - Si el atributo tiene Policy → usar IAuthorizationService.AuthorizeAsync().
        - Si falla cualquier comprobación → throw ForbiddenAccessException.
     d) Si todo OK → await next().

4. Registrar el behavior en Program.cs (o en el DI de Application):
   builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
   
   IMPORTANTE: Registrarlo ANTES del ValidationBehavior si existe, para que la autorización
   se evalúe antes de la validación de datos.

5. Ejemplo de uso en un Command futuro:
   [Authorize(Profiles = "DISPATCH_OFFICE,ADMIN")]
   public record CreateWasteMoveCommand : IRequest<Guid>
   {
       // propiedades...
   }

VERIFICACIÓN:
- Compila.
- Un request sin [Authorize] pasa sin problema.
- Un request con [Authorize(Profiles = "ADMIN")] lanza ForbiddenAccessException si el usuario 
  no es ADMIN.
```

---

## Prompt 8.9 — Tests unitarios de autorización

```
PASO 8.9 — Tests unitarios del sistema de autorización

OBJETIVO: Crear tests que verifiquen que la matriz de permisos se cumple correctamente.

UBICACIÓN: GreenTransit.Tests/Authorization/

CREAR:

1. ProfileAuthorizationHandlerTests.cs
   Tests para el ProfileAuthorizationHandler:
   
   a) Admin_ShouldSucceed_ForAnyPolicy
      - Mock ICurrentUserService con ProfileReference = "ADMIN"
      - Verificar que pasa para TODAS las policies.

   b) Producer_ShouldSucceed_ForCanCreateOwnServiceOrders
      - ProfileReference = "PRODUCER"
      - Policy = CanCreateOwnServiceOrders → succeed.

   c) Producer_ShouldFail_ForCanManageWasteMoves
      - ProfileReference = "PRODUCER"
      - Policy = CanManageWasteMoves → no succeed.

   d) DispatchOffice_ShouldSucceed_ForCanManageWasteMoves
      - ProfileReference = "DISPATCH_OFFICE"
      - Policy = CanManageWasteMoves → succeed.

   e) Carrier_ShouldFail_ForCanManageEntryPlants
      - ProfileReference = "CARRIER"
      - Policy = CanManageEntryPlants → fail.

   f) PlantOp_ShouldSucceed_ForCanManageEntryPlants
      - ProfileReference = "PLANT_OP"
      - Policy = CanManageEntryPlants → succeed.

   g) CacOp_ShouldSucceed_ForCanManageEntryCACs
      - ProfileReference = "CAC_OP"
      - Policy = CanManageEntryCACs → succeed.

   h) CacOp_ShouldFail_ForCanManageEntryPlants
      - ProfileReference = "CAC_OP"
      - Policy = CanManageEntryPlants → fail.

2. DataScopeServiceTests.cs
   Tests para el DataScopeService:

   a) Admin_ShouldHaveFullAccess_ForAllAreas
   b) Producer_ShouldFilterServiceOrders_ByLinkedEntity
   c) Carrier_ShouldFilterWasteMoves_ByLinkedEntity
   d) PlantOp_ShouldNotFilterEntryPlants_BeyondOwnerId (ya filtrado por tenant)
   e) CacOp_ShouldNotFilterEntryCACs_BeyondOwnerId
   f) DispatchOffice_ShouldHaveFullAccess_ForWasteMoves

3. AuthorizationBehaviorTests.cs
   Tests para el MediatR behavior:

   a) Request_WithoutAuthorizeAttribute_ShouldPassThrough
   b) Request_WithAuthorizeAttribute_ShouldFail_WhenNotInProfile
   c) Request_WithAuthorizeAttribute_ShouldSucceed_WhenInProfile
   d) Request_WithMultipleAuthorizeAttributes_ShouldRequireAll

PATRÓN DE TEST:
- Usar Moq para mockear ICurrentUserService.
- Configurar el mock con los datos del perfil a probar.
- Cada test valida UN escenario de la matriz de autorización.
- Nombrar los tests con el patrón: {Perfil}_{Should/ShouldNot}_{Acción}_{Contexto}

VERIFICACIÓN:
- Todos los tests pasan (dotnet test).
- Cobertura mínima: al menos un test por cada perfil para cada área funcional.
```

---

## Prompt 8.10 — Documentación y actualización de COPILOT_CONTEXT.md

```
PASO 8.10 — Actualizar documentación del proyecto

OBJETIVO: Actualizar COPILOT_CONTEXT.md y README.md para reflejar que el Paso 8 
(Autorización) está completado.

MODIFICACIONES:

1. En COPILOT_CONTEXT.md, añadir en la tabla de estado:

   Paso 8 — Autorización por perfiles ✅ COMPLETADO
   
   8.0 — Instrucción base               ✅ Completado  Contexto
   8.1 — Constantes de perfiles          ✅ Completado  ProfileConstants, PolicyConstants
   8.2 — CurrentUserService extendido    ✅ Completado  ProfileReference, LinkedEntityId
   8.3 — Requirements y Handlers         ✅ Completado  ProfileRequirement, OwnDataRequirement
   8.4 — Registro de policies            ✅ Completado  Program.cs, 22 policies
   8.5 — DataScopeService                ✅ Completado  Filtrado por perfil en queries
   8.6 — Componentes Blazor              ✅ Completado  ProfileAuthorizeView, NavMenu
   8.7 — Seed de perfiles                ✅ Completado  9 perfiles + DISPATCH_OFFICE
   8.8 — MediatR AuthorizationBehavior   ✅ Completado  Pipeline transversal
   8.9 — Tests unitarios                 ✅ Completado  Authorization tests
   8.10 — Documentación                  ✅ Completado  Este prompt

2. En la sección "ESTADO FINAL" de COPILOT_CONTEXT.md, actualizar:
   
   El proyecto queda listo para:
   - Implementar casos de uso reales (CQRS) CON AUTORIZACIÓN INTEGRADA
   - Construir pantallas Blazor CON CONTROL DE ACCESO POR PERFIL
   - Los commands/queries se decoran con [Authorize(Profiles = "...")] 
   - Las queries usan IDataScopeService para filtrar datos propios
   - El menú de navegación se adapta al perfil del usuario

3. En README.md, añadir en la sección de Seguridad:

   ## 🔐 Autorización
   
   Sistema basado en perfiles con 9 roles:
   - ADMIN, SCRAP, PRODUCER, CARRIER, PLANT_OP, CAC_OP, PUBLIC_ENT, COORDINATOR, DISPATCH_OFFICE
   
   Documento de referencia: Mapa_Autorizacion_GreenTransit.md
   
   Implementación:
   - ASP.NET Core Authorization Policies (22 policies)
   - MediatR Authorization Behavior (pipeline)
   - IDataScopeService (filtrado de datos por perfil)
   - ProfileAuthorizeView (componente Blazor)

4. Añadir Mapa_Autorizacion_GreenTransit.md a la raíz del proyecto 
   como documento de referencia para agentes de IA.

VERIFICACIÓN:
- Documentación actualizada y coherente.
- El proyecto compila y todos los tests pasan.
- El Mapa de Autorización está accesible en la raíz del proyecto.
```

---

## Resumen de ejecución

| Prompt | Capa principal | Archivos nuevos | Dependencias |
|--------|---------------|-----------------|-------------|
| 8.0 | — | Ninguno | Contexto |
| 8.1 | Domain | 3 archivos | Ninguna |
| 8.2 | Application + Infrastructure | 2 modificados | CurrentUserService existente |
| 8.3 | Infrastructure | 4 archivos | 8.1, 8.2 |
| 8.4 | Web | 1 modificado (Program.cs) | 8.1, 8.3 |
| 8.5 | Application + Infrastructure | 2 archivos | 8.2 |
| 8.6 | Web | 2-3 archivos | 8.1, 8.2 |
| 8.7 | Infrastructure | 1-2 archivos | 8.1 |
| 8.8 | Application | 3 archivos | 8.2 |
| 8.9 | Tests | 3 archivos | 8.3, 8.5, 8.8 |
| 8.10 | — | 0 (modificaciones) | Todos |

**Tiempo estimado**: 2-3 sesiones de Copilot Chat.

**Orden de dependencias**: 8.0 → 8.1 → 8.2 → 8.3 → 8.4 → 8.5 → 8.6 → 8.7 → 8.8 → 8.9 → 8.10
