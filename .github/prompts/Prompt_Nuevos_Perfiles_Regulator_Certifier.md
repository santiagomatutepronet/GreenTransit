# 🔧 Prompt para GitHub Copilot — Integración de perfiles REGULATOR y CERTIFIER en GreenTransit

> **Objetivo**: Integrar dos nuevos perfiles de usuario (`REGULATOR` y `CERTIFIER`) en la aplicación GreenTransit. Los perfiles **ya existen en la base de datos** (tabla `Profiles`). El trabajo consiste en integrarlos en el código C# (constantes, autorización, provisión de usuarios, menú, gestión de permisos por pantalla), ampliar el seed sandbox con nuevos usuarios y configurar sus conectores EDC.

---

## 📎 Archivos de contexto a adjuntar

Adjunta estos archivos al inicio de la sesión de Copilot:
1. `README.md`
2. `Crear_BD_v4_1.sql`
3. `COPILOT_CONTEXT.md`
4. `Mapa_Funcionalidades.md`

---

## 🎯 Prompt

```
Necesito integrar dos nuevos perfiles de usuario en GreenTransit que YA EXISTEN
en la tabla Profiles de la base de datos:

  - REGULATOR  → Perfil Regulador (autoridad reguladora, supervisión normativa)
  - CERTIFIER  → Perfil Certificador / Auditor (organismo certificador tipo AENOR,
                  consume indicadores y evidencias para validación/coherencia)

Los registros en Profiles son:
  | Reference   | Description                                            |
  |-------------|--------------------------------------------------------|
  | REGULATOR   | Regulador — Autoridad de supervisión normativa         |
  | CERTIFIER   | Certificador / Auditor — Validación y coherencia       |

NO se deben crear nuevas tablas ni entidades de dominio. Se reutiliza el modelo v4.1
existente. Estos perfiles son "consumidores de datos" — leen indicadores, KPIs,
evidencias de cumplimiento y datos de reporting, pero NO realizan operaciones
de escritura sobre el flujo operativo (traslados, entradas en planta, etc.).

═══════════════════════════════════════════════════════════════
PARTE 1 — CONSTANTES Y MAPEOS EN CÓDIGO
═══════════════════════════════════════════════════════════════

1.1. ProfileConstants.cs (GreenTransit.Domain/Authorization/)

Añadir las dos nuevas constantes al final de la clase existente:

    public const string REGULATOR = "REGULATOR";
    public const string CERTIFIER = "CERTIFIER";

Verificar que ahora hay 11 constantes en total:
ADMIN, SCRAP, PRODUCER, CARRIER, PLANT_OP, CAC_OP, PUBLIC_ENT,
COORDINATOR, DISPATCH_OFFICE, REGULATOR, CERTIFIER.

1.2. EntityRoleToProfileMapping.cs (GreenTransit.Domain/Authorization/)

Añadir dos nuevas entradas al diccionario de mapeo EntityRole → Profile:

    | EntityRole   | Profiles.Reference | Crea usuario? |
    |--------------|--------------------|---------------|
    | "Regulator"  | "REGULATOR"        | ✅ Sí         |
    | "Certifier"  | "CERTIFIER"        | ✅ Sí         |

Esto permitirá que al crear una Entity con EntityRole = "Regulator" o "Certifier",
el sistema cree automáticamente un usuario con el perfil correspondiente.

1.3. Entities.EntityRole — valores válidos actualizados

El discriminador EntityRole de la tabla Entities ahora admite:
  Producer, OperatorTransfer, SCRAP, PublicEntity, Carrier, CAC, Plant,
  Coordinator, Regulator, Certifier, Other

Si hay algún enum, lista de valores válidos o validación de EntityRole en el código,
añadir "Regulator" y "Certifier" a la lista.

Buscar en todo el código:
  - Enums de EntityRole
  - Arrays/listas de valores válidos de EntityRole
  - Validaciones tipo switch/if que listen los EntityRoles permitidos
  - Selectores/dropdowns de EntityRole en formularios Blazor

Y añadir los dos nuevos valores.

═══════════════════════════════════════════════════════════════
PARTE 2 — POLICIES DE AUTORIZACIÓN
═══════════════════════════════════════════════════════════════

2.1. PolicyConstants.cs (GreenTransit.Domain/Authorization/)

Añadir las siguientes policies nuevas:

    // Reporting / lectura para regulador y certificador
    public const string CanViewRegulatoryDashboard = "CanViewRegulatoryDashboard";
    public const string CanViewCertificationDashboard = "CanViewCertificationDashboard";

2.2. Program.cs — Registro de policies

Añadir en la sección AddAuthorization(options => { ... }):

    // REGULATOR — lectura transversal de KPIs y cumplimiento normativo
    options.AddPolicy(PolicyConstants.CanViewRegulatoryDashboard, policy =>
        policy.Requirements.Add(new ProfileRequirement(
            ProfileConstants.REGULATOR,
            ProfileConstants.ADMIN)));

    // CERTIFIER — lectura de indicadores y evidencias para auditoría
    options.AddPolicy(PolicyConstants.CanViewCertificationDashboard, policy =>
        policy.Requirements.Add(new ProfileRequirement(
            ProfileConstants.CERTIFIER,
            ProfileConstants.ADMIN)));

2.3. Añadir REGULATOR y CERTIFIER a policies de reporting EXISTENTES

Los siguientes perfiles existentes deben incluir ahora a REGULATOR y/o CERTIFIER
porque estos roles necesitan acceso de lectura a los dashboards de reporting:

    CanViewKPIs → añadir REGULATOR, CERTIFIER
    CanViewReporting → ya permite todos los autenticados, verificar que les aplica

    // Cumplimiento normativo — REGULATOR necesita ver estos dashboards:
    CanViewScrapComplianceOverview → añadir REGULATOR, CERTIFIER
    CanViewMarketShareAudit → añadir REGULATOR, CERTIFIER
    CanViewAgreementComplianceMonitoring → añadir REGULATOR, CERTIFIER
    CanViewDispatchOfficeComplianceData → añadir REGULATOR, CERTIFIER

    // Huella de carbono — CERTIFIER valida evidencias de sostenibilidad:
    CanViewCarbonFootprintOverview → añadir CERTIFIER
    CanViewCarbonFootprintTransport → añadir CERTIFIER
    CanViewCarbonFootprintPlantEnergy → añadir CERTIFIER

IMPORTANTE: buscar TODAS las policies registradas en Program.cs y evaluar
cuáles necesitan incluir REGULATOR y/o CERTIFIER. La regla general:
  - REGULATOR: accede a TODOS los dashboards de cumplimiento normativo,
    KPIs, indicadores agregados. NO accede a operaciones de escritura.
  - CERTIFIER: accede a dashboards de cumplimiento, huella de carbono,
    KPIs, evidencias de tratamiento. NO accede a operaciones de escritura.
  - Ninguno de los dos accede a: gestión de usuarios, CRUD de entidades,
    creación de traslados, gestión de acuerdos/liquidaciones.

═══════════════════════════════════════════════════════════════
PARTE 3 — DATOS DE ÁMBITO (DATA SCOPE)
═══════════════════════════════════════════════════════════════

3.1. IDataScopeService — Filtrado para REGULATOR y CERTIFIER

Estos perfiles ven TODOS los datos del tenant (solo filtro OwnerId),
similar a DISPATCH_OFFICE y ADMIN. NO tienen filtro por LinkedEntityId.

En el DataScopeService (o equivalente), cuando el perfil sea REGULATOR
o CERTIFIER, no aplicar ningún filtro adicional más allá de OwnerId.

Si hay un switch/if que liste los perfiles con acceso completo al tenant,
añadir REGULATOR y CERTIFIER:

    // Perfiles con visión completa del tenant:
    if (currentUser.IsInAnyProfile(
        ProfileConstants.ADMIN,
        ProfileConstants.DISPATCH_OFFICE,
        ProfileConstants.REGULATOR,
        ProfileConstants.CERTIFIER))
    {
        // Solo filtro OwnerId — sin restricción adicional
        return query;
    }

═══════════════════════════════════════════════════════════════
PARTE 4 — INTERFAZ DE USUARIO (BLAZOR)
═══════════════════════════════════════════════════════════════

4.1. NavMenu.razor — Actualizar visibilidad del menú

REGULATOR y CERTIFIER deben ver las secciones de menú de Reporting
(KPIs, Trazabilidad, Documentos, Cumplimiento Normativo, Huella de Carbono).

Buscar en NavMenu.razor:
  - Los diccionarios _groupRoutes que definen qué rutas pertenecen a cada grupo
  - La lógica HasAnyVisibleChild()

No es necesario añadir nuevas secciones de menú para estos perfiles.
Su acceso se configura dinámicamente mediante PagePermissions.
Verificar que los rutas de reporting ya existentes les aparecerán
cuando el administrador configure sus permisos desde /security/page-permissions.

4.2. Formulario de Entidades — Actualizar desplegable EntityRole

En el formulario de creación/edición de Entities (probablemente en
Web/Components/Pages/Configuration/ o similar):

Añadir "Regulator" y "Certifier" al dropdown/selector de EntityRole.

Al seleccionar "Regulator":
  → Mensaje: "Al crear esta entidad se generará automáticamente un usuario
     con perfil REGULATOR. El email de la entidad se usará como Login."

Al seleccionar "Certifier":
  → Mensaje: "Al crear esta entidad se generará automáticamente un usuario
     con perfil CERTIFIER. El email de la entidad se usará como Login."

4.3. Gestión de permisos por pantalla — /security/page-permissions

NO requiere cambios en código. Los nuevos perfiles aparecerán automáticamente
en la matriz de permisos porque PagePermissionMatrixQuery carga TODOS los
perfiles de la tabla Profiles.

Verificar que:
  - GetPagePermissionMatrixQuery carga perfiles sin filtro hardcodeado.
  - La UI de la matriz muestra tantas columnas como perfiles existan.
  - UpdatePagePermissionCommand acepta cualquier IdProfile válido.

Tras el despliegue, el administrador deberá configurar desde esta pantalla
qué páginas pueden ver REGULATOR y CERTIFIER.

═══════════════════════════════════════════════════════════════
PARTE 5 — SEED DE USUARIOS SANDBOX
═══════════════════════════════════════════════════════════════

5.1. Actualizar el seed de perfiles (si es necesario)

Verificar que el seeder/HasData incluye los 11 perfiles:

    INSERT INTO Profiles (Reference, Description)
    SELECT Reference, Description
    FROM (VALUES
        ('ADMIN',           'Administrador del sistema'),
        ('SCRAP',           'Sistema Colectivo de Responsabilidad Ampliada'),
        ('PRODUCER',        'Productor / Generador de residuos'),
        ('CARRIER',         'Transportista'),
        ('PLANT_OP',        'Operador de Planta de Tratamiento'),
        ('CAC_OP',          'Operador de Centro de Acopio'),
        ('PUBLIC_ENT',      'Entidad Pública / Ayuntamiento'),
        ('COORDINATOR',     'Coordinador del acuerdo'),
        ('DISPATCH_OFFICE', 'Oficina de Asignación — Gestor logístico'),
        ('REGULATOR',       'Regulador — Autoridad de supervisión normativa'),
        ('CERTIFIER',       'Certificador / Auditor — Validación y coherencia')
    ) AS src (Reference, Description)
    WHERE NOT EXISTS (
        SELECT 1 FROM Profiles p WHERE p.Reference = src.Reference
    );

5.2. Crear usuarios sandbox para TODOS los perfiles del ecosistema

Ampliar el DbInitializer/SandboxDataSeeder para crear los siguientes
usuarios sandbox (además de los que ya existen). Los datos completos
de TODOS los usuarios sandbox son:

    | Login                  | Perfil           | CompleteName                      |
    |------------------------|------------------|-----------------------------------|
    | ayuntamiento_uc        | PUBLIC_ENT       | Ayuntamiento UC (Sandbox)         |
    | ofiasignacion_uc       | DISPATCH_OFFICE  | Oficina de Asignación UC          |
    | scrapa_uc              | SCRAP            | SCRAP A UC                        |
    | scrapb_uc              | SCRAP            | SCRAP B UC                        |
    | transportista_uc       | CARRIER          | Transportista UC                  |
    | clusterlogistico_uc    | COORDINATOR      | Clúster Logístico UC              |
    | puntorecogida_uc       | CAC_OP           | Punto de Recogida UC              |
    | certificador_uc        | CERTIFIER        | Certificador UC (AENOR Sandbox)   |
    | productor_uc           | PRODUCER         | Productor UC                      |
    | regulador_uc           | REGULATOR        | Regulador UC (Sandbox)            |
    | plantatratamiento_uc   | PLANT_OP         | Planta de Tratamiento UC          |

Para cada usuario:
  - Login = valor de la columna Login
  - Email = Login + "@sandbox.greentransit.es"
  - IdProfile = SELECT ID FROM Profiles WHERE Reference = <perfil>
  - OwnerId = OwnerId del tenant de demo (mismo que las entidades existentes)
  - IsActive = true
  - CreateDate = GETDATE()

IMPORTANTE: el seed debe ser IDEMPOTENTE. Si ya existe un Users con ese Login,
no duplicar. Usar el patrón IF NOT EXISTS:

    IF NOT EXISTS (SELECT 1 FROM Users WHERE Login = 'regulador_uc')
    BEGIN
        INSERT INTO Users (Login, CompleteName, Email, IdProfile, OwnerId, IsActive, CreateDate)
        VALUES ('regulador_uc', 'Regulador UC (Sandbox)',
                'regulador_uc@sandbox.greentransit.es',
                (SELECT ID FROM Profiles WHERE Reference = 'REGULATOR'),
                @demoOwnerId, 1, GETDATE());
    END

Repetir para cada usuario de la tabla anterior. NO tocar el usuario ADMIN existente.

═══════════════════════════════════════════════════════════════
PARTE 6 — CONFIGURACIÓN EDC (CONECTORES Y CONSUMO)
═══════════════════════════════════════════════════════════════

6.1. Tabla UserEDCConnector — Registrar conectores para TODOS los usuarios sandbox

La tabla UserEDCConnector ya existe en base de datos (creada con el script adjunto).
Insertar un registro por cada usuario sandbox con su configuración EDC:

    | Login                  | EDCServerName                                                          | EDCConnectorId            | ApiKey      |
    |------------------------|------------------------------------------------------------------------|---------------------------|-------------|
    | ayuntamiento_uc        | ecoucayuntamiento.ecodatanetconn3.dataspace.wastenode.com              | eco_uc_ayuntamiento       | ecodatanet  |
    | ofiasignacion_uc       | ecoucofiasignacion.ecodatanetconn3.dataspace.wastenode.com             | eco_uc_ofiasignacion      | ecodatanet  |
    | scrapa_uc              | ecoucscrapa.ecodatanetconn3.dataspace.wastenode.com                    | eco_uc_scrapa             | ecodatanet  |
    | scrapb_uc              | ecoucscrapb.ecodatanetconn3.dataspace.wastenode.com                    | eco_uc_scrapb             | ecodatanet  |
    | transportista_uc       | ecouctransportista.ecodatanetconn3.dataspace.wastenode.com             | eco_uc_transportista      | ecodatanet  |
    | clusterlogistico_uc    | ecoucclusterlogistico.ecodatanetconn3.dataspace.wastenode.com          | eco_uc_clusterlogistico   | ecodatanet  |
    | puntorecogida_uc       | ecoucpuntorecogida.ecodatanetconn3.dataspace.wastenode.com             | eco_uc_puntorecogida      | ecodatanet  |
    | certificador_uc        | ecouccertificador.ecodatanetconn3.dataspace.wastenode.com              | eco_uc_certificador       | ecodatanet  |
    | productor_uc           | ecoucproductor.ecodatanetconn3.dataspace.wastenode.com                 | eco_uc_productor          | ecodatanet  |
    | regulador_uc           | ecoucregulador.ecodatanetconn3.dataspace.wastenode.com                 | eco_uc_regulador          | ecodatanet  |
    | plantatratamiento_uc   | ecoucplantatratamiento.ecodatanetconn3.dataspace.wastenode.com         | eco_uc_plantatratamiento  | ecodatanet  |

Patrón de inserción idempotente:

    DECLARE @userId INT = (SELECT ID FROM Users WHERE Login = 'regulador_uc');
    IF @userId IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM UserEDCConnector WHERE UserId = @userId)
    BEGIN
        INSERT INTO UserEDCConnector (UserId, EDCServerName, EDCConnectorId, ApiKey)
        VALUES (@userId,
                'ecoucregulador.ecodatanetconn3.dataspace.wastenode.com',
                'eco_uc_regulador',
                'ecodatanet');
    END

6.2. Tabla ProfileEDCConsumer — Relaciones de consumo para los nuevos perfiles

La tabla ProfileEDCConsumer define qué perfiles consumen datasets de qué otros perfiles.
Añadir las relaciones para REGULATOR y CERTIFIER:

    -- REGULADOR consume de: TODOS los perfiles operativos (visión supervisora)
    REGULATOR consume de → DISPATCH_OFFICE, SCRAP, PUBLIC_ENT, PLANT_OP, CARRIER,
                           CAC_OP, PRODUCER, COORDINATOR

    -- CERTIFICADOR consume de: perfiles que generan evidencias auditables
    CERTIFIER consume de → DISPATCH_OFFICE, SCRAP, PLANT_OP, PRODUCER

Patrón SQL idempotente (usar las mismas variables que el script adjunto
Añadir_tablas_configuracion_EDC.sql):

    DECLARE @regulator  INT = (SELECT ID FROM Profiles WHERE Reference = 'REGULATOR');
    DECLARE @certifier  INT = (SELECT ID FROM Profiles WHERE Reference = 'CERTIFIER');
    -- Reutilizar las variables existentes: @dispatch_office, @scrap, @public_entity,
    -- @carrier, @cac, @plant, @producer, @coordinator

    -- REGULATOR consume de todos los perfiles operativos
    IF NOT EXISTS (SELECT 1 FROM ProfileEDCConsumer
                   WHERE ProfileId = @regulator AND ConsumedProfileId = @dispatch_office)
        INSERT INTO ProfileEDCConsumer (ProfileId, ConsumedProfileId)
        VALUES (@regulator, @dispatch_office);

    IF NOT EXISTS (SELECT 1 FROM ProfileEDCConsumer
                   WHERE ProfileId = @regulator AND ConsumedProfileId = @scrap)
        INSERT INTO ProfileEDCConsumer (ProfileId, ConsumedProfileId)
        VALUES (@regulator, @scrap);

    IF NOT EXISTS (SELECT 1 FROM ProfileEDCConsumer
                   WHERE ProfileId = @regulator AND ConsumedProfileId = @public_entity)
        INSERT INTO ProfileEDCConsumer (ProfileId, ConsumedProfileId)
        VALUES (@regulator, @public_entity);

    IF NOT EXISTS (SELECT 1 FROM ProfileEDCConsumer
                   WHERE ProfileId = @regulator AND ConsumedProfileId = @plant)
        INSERT INTO ProfileEDCConsumer (ProfileId, ConsumedProfileId)
        VALUES (@regulator, @plant);

    IF NOT EXISTS (SELECT 1 FROM ProfileEDCConsumer
                   WHERE ProfileId = @regulator AND ConsumedProfileId = @carrier)
        INSERT INTO ProfileEDCConsumer (ProfileId, ConsumedProfileId)
        VALUES (@regulator, @carrier);

    IF NOT EXISTS (SELECT 1 FROM ProfileEDCConsumer
                   WHERE ProfileId = @regulator AND ConsumedProfileId = @cac)
        INSERT INTO ProfileEDCConsumer (ProfileId, ConsumedProfileId)
        VALUES (@regulator, @cac);

    IF NOT EXISTS (SELECT 1 FROM ProfileEDCConsumer
                   WHERE ProfileId = @regulator AND ConsumedProfileId = @producer)
        INSERT INTO ProfileEDCConsumer (ProfileId, ConsumedProfileId)
        VALUES (@regulator, @producer);

    IF NOT EXISTS (SELECT 1 FROM ProfileEDCConsumer
                   WHERE ProfileId = @regulator AND ConsumedProfileId = @coordinator)
        INSERT INTO ProfileEDCConsumer (ProfileId, ConsumedProfileId)
        VALUES (@regulator, @coordinator);

    -- CERTIFIER consume de: dispatch-office, scrap, plant, producer
    IF NOT EXISTS (SELECT 1 FROM ProfileEDCConsumer
                   WHERE ProfileId = @certifier AND ConsumedProfileId = @dispatch_office)
        INSERT INTO ProfileEDCConsumer (ProfileId, ConsumedProfileId)
        VALUES (@certifier, @dispatch_office);

    IF NOT EXISTS (SELECT 1 FROM ProfileEDCConsumer
                   WHERE ProfileId = @certifier AND ConsumedProfileId = @scrap)
        INSERT INTO ProfileEDCConsumer (ProfileId, ConsumedProfileId)
        VALUES (@certifier, @scrap);

    IF NOT EXISTS (SELECT 1 FROM ProfileEDCConsumer
                   WHERE ProfileId = @certifier AND ConsumedProfileId = @plant)
        INSERT INTO ProfileEDCConsumer (ProfileId, ConsumedProfileId)
        VALUES (@certifier, @plant);

    IF NOT EXISTS (SELECT 1 FROM ProfileEDCConsumer
                   WHERE ProfileId = @certifier AND ConsumedProfileId = @producer)
        INSERT INTO ProfileEDCConsumer (ProfileId, ConsumedProfileId)
        VALUES (@certifier, @producer);

═══════════════════════════════════════════════════════════════
PARTE 7 — ACTUALIZACIÓN DE DOCUMENTACIÓN
═══════════════════════════════════════════════════════════════

7.1. COPILOT_CONTEXT.md

Actualizar la sección de perfiles para reflejar 11 perfiles:

    El sistema tiene 11 perfiles:
    ADMIN, SCRAP, PRODUCER, CARRIER, PLANT_OP, CAC_OP, PUBLIC_ENT,
    COORDINATOR, DISPATCH_OFFICE, REGULATOR, CERTIFIER

Actualizar la tabla de mapeo EntityRole → Profile con:
    | "Regulator"  | "REGULATOR"  | ✅ Sí |
    | "Certifier"  | "CERTIFIER"  | ✅ Sí |

7.2. README.md

Actualizar la sección de Seguridad/Autorización:

    ## 🔐 Autorización

    Sistema basado en perfiles con 11 roles:
    ADMIN, SCRAP, PRODUCER, CARRIER, PLANT_OP, CAC_OP, PUBLIC_ENT,
    COORDINATOR, DISPATCH_OFFICE, REGULATOR, CERTIFIER

    Nuevos perfiles v2:
    - REGULATOR: autoridad reguladora con acceso de lectura a KPIs,
      cumplimiento normativo e indicadores del ecosistema.
    - CERTIFIER: organismo certificador/auditor (ej. AENOR) con acceso
      de lectura a evidencias de tratamiento, huella de carbono y reporting.

═══════════════════════════════════════════════════════════════
RESUMEN DE ARCHIVOS A MODIFICAR
═══════════════════════════════════════════════════════════════

MODIFICADOS:
  GreenTransit.Domain/Authorization/ProfileConstants.cs         → +2 constantes
  GreenTransit.Domain/Authorization/EntityRoleToProfileMapping.cs → +2 mapeos
  GreenTransit.Domain/Authorization/PolicyConstants.cs          → +2 policies nuevas
  GreenTransit.Web/Program.cs                                   → registrar policies nuevas
                                                                   + añadir perfiles a policies existentes
  GreenTransit.Infrastructure/Services/DataScopeService.cs      → REGULATOR/CERTIFIER = sin filtro
  GreenTransit.Infrastructure/Persistence/SandboxDataSeeder.cs  → +11 usuarios + EDC connectors
  GreenTransit.Web/Components/Pages/NavMenu.razor               → verificar (no debería necesitar cambios)
  GreenTransit.Web/Components/Pages/Configuration/EntityForm     → +2 EntityRoles en dropdown
  COPILOT_CONTEXT.md                                            → 11 perfiles
  README.md                                                     → 11 perfiles

NUEVOS (opcionales):
  Ninguno. Todo se integra en archivos existentes.

SQL A EJECUTAR:
  1. Seed de perfiles (verificar que REGULATOR y CERTIFIER existen)
  2. Seed de usuarios sandbox (11 usuarios)
  3. Seed de UserEDCConnector (11 registros)
  4. Seed de ProfileEDCConsumer (12 nuevas relaciones: 8 para REGULATOR + 4 para CERTIFIER)

═══════════════════════════════════════════════════════════════
REGLAS GENERALES
═══════════════════════════════════════════════════════════════

1. Respetar Clean Architecture: Domain no referencia a ningún otro proyecto.
2. Interfaces en Application, implementaciones en Infrastructure.
3. Nombrar todo en inglés (clases, métodos, variables). Comentarios en español.
4. NO crear nuevas tablas ni entidades de dominio.
5. NO crear nuevas páginas Blazor para estos perfiles (su acceso se configura
   dinámicamente desde /security/page-permissions por el administrador).
6. El seed debe ser IDEMPOTENTE — no insertar duplicados.
7. No tocar el usuario ADMIN existente.
8. La autorización sigue siendo DINÁMICA (PageDefinitions/PagePermissions).
   Las policies son el "suelo" de seguridad; los permisos dinámicos restringen más.
9. REGULATOR y CERTIFIER son perfiles de SOLO LECTURA sobre datos operativos.
   No deben aparecer en ninguna policy de escritura/CRUD.
10. Estos perfiles ven todo el tenant (filtro solo por OwnerId, sin LinkedEntityId).

═══════════════════════════════════════════════════════════════
VERIFICACIÓN FINAL
═══════════════════════════════════════════════════════════════

- [ ] ProfileConstants.cs tiene 11 constantes.
- [ ] EntityRoleToProfileMapping incluye "Regulator" y "Certifier".
- [ ] Program.cs registra las 2 nuevas policies Y actualiza las existentes.
- [ ] DataScopeService trata REGULATOR/CERTIFIER como visión completa del tenant.
- [ ] El dropdown de EntityRole en el formulario de entidades incluye "Regulator" y "Certifier".
- [ ] La tabla Profiles tiene 11 registros.
- [ ] La tabla Users tiene los 11 usuarios sandbox.
- [ ] La tabla UserEDCConnector tiene 11 registros (uno por usuario sandbox).
- [ ] La tabla ProfileEDCConsumer tiene 12 nuevas filas para REGULATOR y CERTIFIER.
- [ ] La aplicación compila sin errores.
- [ ] La pantalla /security/page-permissions muestra las 11 columnas de perfiles.
- [ ] COPILOT_CONTEXT.md y README.md actualizados.
```
