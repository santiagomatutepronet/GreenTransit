# 🔐 Mapa de Autenticación y Autorización — GreenTransit

> Documento de referencia para agentes de IA. Define quién accede al sistema, cómo se autentica y qué puede ver/hacer en cada pantalla.
>
> Basado en el modelo de datos v4.1, el Mapa de Funcionalidades y las tablas `Profiles`, `Users` y `Entities`.

---

## 1. Autenticación — OpenID Connect

### 1.1. Proveedor de identidad

| Parámetro | Valor |
|---|---|
| Protocolo | OpenID Connect (OIDC) |
| Authority | `https://pronet-identity-wst-app.azurewebsites.net/` |
| Flujo | Authorization Code + PKCE |
| Tokens | ID Token + Access Token |
| Almacenamiento de credenciales | Ninguno — el sistema NO almacena contraseñas |

### 1.2. Mapeo de claims

| Claim OIDC | Campo interno | Uso |
|---|---|---|
| `sub` | `Users.ID` (mapeo) → `IdUser` | Identificador único del usuario en todas las tablas operativas |
| `email` o `preferred_username` | `Users.Login` / `Users.Email` | Identificación visual y notificaciones |
| Claim organizativo (custom) | `Users.OwnerId` | Aislamiento multi-tenant — filtra TODOS los datos operativos |

### 1.3. Flujo de autenticación

```
Usuario → Login → Servidor OIDC (Authority)
                      ↓
              Authorization Code
                      ↓
         Intercambio por ID Token + Access Token
                      ↓
         ClaimsTransformation en backend:
           - sub → buscar Users.ID
           - claim org → OwnerId
           - Users.IdProfile → cargar perfil y permisos
                      ↓
         CurrentUserService disponible en toda la app
```

### 1.4. Comportamiento de sesión

- **Sin credenciales locales**: toda la autenticación es delegada al servidor OIDC.
- **2FA opcional**: configurable en el servidor de identidad.
- **Bloqueo por inactividad**: si una `Entity` se desactiva (`IsActive = 0`), el usuario vinculado se bloquea (no se elimina).
- **Protección Blazor**: todas las páginas requieren autenticación salvo la landing de login.

---

## 2. Perfiles de usuario

### 2.1. Catálogo de perfiles (`Profiles`)

Cada usuario tiene exactamente un perfil (`Users.IdProfile → Profiles.ID`). El perfil determina qué pantallas ve y qué operaciones puede realizar.

| `Profiles.Reference` | Descripción | `EntityRole` asociado | Responsabilidad principal |
|---|---|---|---|
| `PRODUCER` | Productor / Generador de residuos | `Producer` | Crear órdenes de servicio, declarar productos, gestionar sus residuos |
| `CARRIER` | Transportista | `Carrier` / `OperatorTransfer` | Ejecutar recogidas, confirmar cargas, app móvil |
| `SCRAP` | Sistema Colectivo de Responsabilidad Ampliada | `SCRAP` | Gestionar acuerdos, validar liquidaciones, supervisar operativa, alta restringida de entidades |
| `PUBLIC_ENT` | Entidad Pública / Ayuntamiento | `PublicEntity` | Crear órdenes de servicio, revisar acuerdos y liquidaciones, reporting municipal |
| `CAC_OP` | Operador de Centro de Acopio | `CAC` | Registrar entradas en CAC, gestionar acopio |
| `PLANT_OP` | Operador de Planta de Tratamiento | `Plant` | Registrar entradas en planta, pesaje, clasificación, tratamiento, declarar energía |
| `COORDINATOR` | Coordinador del acuerdo | `Coordinator` | Lectura transversal del ámbito de los acuerdos |
| `DISPATCH_OFFICE` | Oficina de Asignación / Gestor logístico | *(perfil funcional, sin EntityRole directo)* | Crear traslados, planificar logística, asignar transportistas, gestionar maestros operativos |
| `ADMIN` | Administrador del sistema | *(superusuario del tenant)* | CRUD total, gestión de usuarios/perfiles, catálogos normativos, configuración |

### 2.2. Nuevo perfil: Oficina de Asignación (`DISPATCH_OFFICE`)

Este perfil cubre al **Gestor logístico** referenciado en el Mapa de Funcionalidades. Es el rol que:

- **Crea traslados** (`WasteMoves`) a partir de órdenes de servicio.
- **Planifica la logística**: asigna transportista, vehículo, ventanas horarias.
- **Gestiona maestros operativos**: CRUD en Entidades, Residuos y Operaciones R/D.
- **Supervisa incidencias** y puede resolverlas.
- **No tiene acceso** a la gestión de usuarios/perfiles (eso es del ADMIN).

Debe añadirse a la tabla `Profiles`:

```sql
INSERT INTO Profiles (Reference, Description)
VALUES ('DISPATCH_OFFICE', 'Oficina de Asignación — Gestor logístico');
```

### 2.3. Provisión automática de usuario al crear entidad

Al dar de alta una `Entity`, el sistema crea automáticamente un `Users` vinculado según este mapeo:

| `EntityRole` | `Profiles.Reference` asignado |
|---|---|
| `SCRAP` | `SCRAP` |
| `Producer` | `PRODUCER` |
| `Carrier` | `CARRIER` |
| `OperatorTransfer` | `CARRIER` |
| `Plant` | `PLANT_OP` |
| `CAC` | `CAC_OP` |
| `PublicEntity` | `PUBLIC_ENT` |
| `Coordinator` | `COORDINATOR` |
| `Source` / `Destination` / `Other` | *(no se crea usuario automáticamente)* |

> **Nota**: `DISPATCH_OFFICE` y `ADMIN` se crean manualmente por un administrador, ya que no corresponden a una entidad del ecosistema sino a roles funcionales internos.

---

## 3. Reglas transversales de filtrado de datos

### 3.1. Multi-tenant (`OwnerId`)

Todas las consultas operativas filtran por `Users.OwnerId`. Un usuario NUNCA ve datos de otro tenant. Excepción: catálogos compartidos (`LERCodes`, `TreatmentOperations`, geografía).

### 3.2. Filtrado por entidad vinculada ("Propios")

Cuando un permiso indica **"Propios"**, el filtro adicional depende del perfil:

| Perfil | Campo de filtro | Lógica |
|---|---|---|
| `PRODUCER` | `ServiceOrders.IdIssuedBy`, `Residues.IdProducer` | Solo ve SOs que emitió y residuos tipo Product/ProductSpec que declaró |
| `CARRIER` | `WasteMoveResidues.IdCarrier` | Solo ve traslados donde es el transportista asignado |
| `SCRAP` | `Agreements.IdScrap`, `WasteMoves.IdScrap` | Ve operativa vinculada a sus acuerdos |
| `PUBLIC_ENT` | `Agreements.IdPublicEntity`, `ServiceOrders.IdIssuedBy` | Ve acuerdos de su municipio y SOs que emitió |
| `PLANT_OP` | `EntryPlants` / `TreatmentPlants` de su entidad | Solo su planta |
| `CAC_OP` | `EntryCACs` de su entidad | Solo su CAC |
| `COORDINATOR` | `Agreements` donde figura como `IdCoordinator` | Lectura transversal del ámbito del acuerdo |

#### ✅ Implementación del filtrado en `ServiceOrders`

El filtro se aplica en **servidor** a través de `GetServiceOrdersQuery.IdIssuedBy`:

- **`ServiceOrderList.razor`**: al cargar la lista, si el perfil es `PRODUCER` o `PUBLIC_ENT`, se pasa automáticamente `IdIssuedBy = CurrentUser.LinkedEntityId`. El usuario no puede anularlo desde la UI.
- **`ServiceOrderForm.razor`**: al crear una nueva SO, el campo **Emisor** se autocompleta con `CurrentUser.LinkedEntityId` y se muestra como campo de solo lectura. En edición, el valor ya existe en BD y no se modifica.
- **Componentes implicados**: `ServiceOrderList.razor`, `ServiceOrderForm.razor`, `GetServiceOrdersQuery.cs`.
- **Servicio de contexto**: `ICurrentUserService.LinkedEntityId` + `ICurrentUserService.IsInAnyProfile(...)`.

#### ✅ Implementación del filtrado en `Incidents`

- **`GetIncidentsQuery.cs`**: si el perfil es `PRODUCER`, se filtra `i.ServiceOrderId != null && i.ServiceOrder.IdIssuedBy == LinkedEntityId`. Solo ve incidencias cuya SO fue emitida por su entidad.
- **`IncidentForm.razor`**: el campo de traslado vinculado se convierte en un `<select>` cargado con `GetWasteMovesQuery(ServiceOrderIssuedBy: LinkedEntityId)`. El productor solo puede vincular incidencias a traslados propios.
- **Componentes implicados**: `IncidentList.razor`, `IncidentForm.razor`, `GetIncidentsQuery.cs`, `GetWasteMovesQuery.cs`.

#### ✅ Implementación del filtrado en `Dashboard`

El `GetDashboardSummaryQuery` adapta todos sus KPIs al perfil `PRODUCER`:

| KPI | Filtro adicional para PRODUCER |
|---|---|
| WasteMoves by status | `WasteMove.ServiceOrder.IdIssuedBy == LinkedEntityId` |
| Kg recogidos (mes) | WasteMoveResidues de los traslados anteriores |
| CO₂ (mes actual y anterior) | WasteMoveResidues de los traslados anteriores |
| Incidencias abiertas | `Incident.ServiceOrder.IdIssuedBy == LinkedEntityId` |
| Próximas recogidas | `ServiceOrders.IdIssuedBy == LinkedEntityId` |
| Kg recogidos vs tratados (6 meses) | WasteMoveResidues de los traslados anteriores |

### 3.3. Regla de creación

**Toda pantalla que muestra datos debe tener al menos un perfil con capacidad de creación.** Esta regla garantiza que no existan pantallas "huérfanas" donde nadie puede generar registros.

---

## 4. Matriz de autorización por pantalla

### Leyenda de permisos

| Código | Significado |
|---|---|
| **CRUD** | Crear, Leer, Editar, Eliminar — acceso completo |
| **CRUD-P** | CRUD filtrado por datos propios (ver §3.2) |
| **C+R** | Crear y Leer (no editar ni eliminar) |
| **C+R-P** | Crear y Leer, filtrado por datos propios |
| **R** | Solo lectura (todos los datos del tenant) |
| **R-P** | Solo lectura, filtrado por datos propios |
| **V** | Validar (aprobar/rechazar, sin crear) |
| **—** | Sin acceso a la pantalla |

---

### 4.1. ENTIDADES (Maestros)

| Pantalla | Entidad BD | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH_OFFICE | ADMIN |
|---|---|---|---|---|---|---|---|---|---|---|
| **Entidades** | `Entities` | R | R | C+R (su ámbito) | R | R | R | R | CRUD | CRUD |
| **LER** | `LERCodes` | R | R | R | R | R | R | R | R | CRUD |
| **Residuos** | `Residues` | CRUD-P (Product/ProductSpec) | R | R | R | R | R | R | CRUD | CRUD |
| **Operaciones R/D** | `TreatmentOperations` | R | R | R | R | R | R | R | R | CRUD |

**Justificación de creadores:**
- **Entidades**: `DISPATCH_OFFICE` y `ADMIN` son los creadores principales. `SCRAP` puede dar de alta entidades dentro de su ámbito (p.ej. productores adheridos).
- **LER**: Catálogo normativo inmutable. Solo `ADMIN` lo mantiene (cambios normativos muy esporádicos).
- **Residuos**: `PRODUCER` crea sus propios productos y fichas técnicas (`ResidueType = Product | ProductSpec`). `DISPATCH_OFFICE` y `ADMIN` gestionan los residuos operativos (`ResidueType = Waste`).
- **Operaciones R/D**: Catálogo normativo (Directiva 2008/98/CE). Solo `ADMIN`.

---

### 4.2. OPERACIONES

| Pantalla | Entidad BD | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH_OFFICE | ADMIN |
|---|---|---|---|---|---|---|---|---|---|---|
| **Órdenes de Servicio** | `ServiceOrders` | CRUD-P | R | R | CRUD-P | R | R | R | CRUD | CRUD |
| **Traslados** | `WasteMoves` / `WasteMoveResidues` | R-P | U-P (asignados) | R | R | R | R | R | CRUD | CRUD |
| **Entradas Planta** | `EntryPlants` / `EntryPlantResidues` | — | — | R | R | — | CRUD-P | R | R | CRUD |
| **Entradas CAC** | `EntryCACs` / `EntryCACResidues` | — | — | R | R | CRUD-P | — | R | R | CRUD |
| **Tratamiento** | `TreatmentPlants` / `TreatmentPlantResidues` | — | — | R | R | — | CRUD-P | R | R | CRUD |

**Justificación de creadores:**
- **Órdenes de Servicio**: `PRODUCER` y `PUBLIC_ENT` crean SOs (solicitan recogida). `DISPATCH_OFFICE` también las crea (planificación centralizada). Cada uno ve solo las suyas.
- **Traslados**: **`DISPATCH_OFFICE` es el creador principal**. Agrupa SOs en movimientos logísticos reales, asigna transportista y planifica. `CARRIER` solo actualiza los traslados donde está asignado (confirma carga, registra tiempos reales). `PRODUCER` ve solo los traslados que se originan de sus SOs.
- **Entradas Planta**: Solo `PLANT_OP` las crea (pesaje en báscula de su planta).
- **Entradas CAC**: Solo `CAC_OP` las crea (registro de entrada en su centro de acopio).
- **Tratamiento**: Solo `PLANT_OP` (clasificación y balance de masas en su planta).

**Detalle del permiso `U-P` del Transportista en Traslados:**
- Puede actualizar: `ActualPickupStart/End`, `GatheredDate`, `DocumentId`, `DocumentHash`, `SignatureStatus`, datos de transporte en `WasteMoveResidues` (`NTNumber`, `DINumber`, `DIPhase`).
- NO puede modificar: origen, destino, SCRAP asignado, ni crear nuevos traslados.

---

### 4.3. SOSTENIBILIDAD

| Pantalla | Entidad BD | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH_OFFICE | ADMIN |
|---|---|---|---|---|---|---|---|---|---|---|
| **Incidencias** | `Incidents` | C+R | C+R | C+R | C+R | C+R | C+R | C+R | CRUD | CRUD |
| **Zonas DUM** | `DUMZones` / `DUMRestrictionRules` | R | R | R | R | — | — | R | R | CRUD |
| **Simulador DUM** | *(lógica sobre `DUMZones`)* | R | R | R | R | — | — | R | R | CRUD |
| **Emisiones** | `WasteMoveResidues` (campos CO₂) | R-P | R-P | R | R | — | R | R | R | CRUD |
| **Energía Planta** | `PlantEnergies` | — | — | R | R | — | CRUD-P | R | R | CRUD |
| **Factores Emisión** | `EmissionFactorSets` / `EmissionFactors` | — | — | R | R | — | R | R | R | CRUD |

**Justificación de creadores:**
- **Incidencias**: Cualquier perfil puede abrir una incidencia (la apertura es universal). Solo `DISPATCH_OFFICE` y `ADMIN` pueden resolver/cerrar y eliminar. La resolución también la puede hacer el perfil responsable según el `Type` de incidencia.
- **Zonas DUM y Simulador**: Solo `ADMIN` crea/edita zonas y reglas. El resto las consulta (el simulador es de solo lectura: "¿puedo entrar con mi vehículo?").
- **Emisiones**: Dato calculado automáticamente por el backend al pasar a RECOGIDO. `ADMIN` puede forzar re-cálculo.
- **Energía Planta**: Solo `PLANT_OP` declara consumo eléctrico de su planta (Scope 2).
- **Factores Emisión**: Catálogo versionado. Solo `ADMIN` sube nuevas versiones.

---

### 4.4. REPORTING

| Pantalla | Entidad BD | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH_OFFICE | ADMIN |
|---|---|---|---|---|---|---|---|---|---|---|
| **Trazabilidad** | *(vista cruzada)* | R-P | R-P | R | R | R-P | R-P | R | R | R |
| **Vista 360° Traslado** | *(vista cruzada)* | R-P | R-P | R | R | R-P | R-P | R | R | R |
| **KPIs** | *(vistas agregadas)* | — | — | R | R | — | R | R | R | R |
| **Documentos** | `AgreementDocuments` + campos Doc en `WasteMoves` | R-P | R-P | R | R | R-P | R-P | R | R | CRUD |

**Justificación:**
- **Trazabilidad y Vista 360°**: Todos los perfiles acceden pero ven solo los traslados en los que participan. `SCRAP`, `PUBLIC_ENT`, `COORDINATOR`, `DISPATCH_OFFICE` y `ADMIN` ven transversalmente.
- **KPIs**: Solo perfiles con responsabilidad de supervisión o cumplimiento normativo. No tiene sentido para `PRODUCER`, `CARRIER` o `CAC_OP` aislados.
- **Documentos**: `ADMIN` gestiona el repositorio documental. El resto consulta documentos de los traslados donde participa.

---

### 4.5. SEGURIDAD

| Pantalla | Entidad BD | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH_OFFICE | ADMIN |
|---|---|---|---|---|---|---|---|---|---|---|
| **Usuarios** | `Users` | — | — | R-P (sus operadores) | — | — | — | — | — | CRUD |
| **Perfiles** | `Profiles` | — | — | R | — | — | — | — | — | CRUD |

**Justificación:**
- Solo `ADMIN` tiene CRUD completo en Usuarios y Perfiles.
- `SCRAP` puede ver los usuarios asociados a su ámbito (lectura restringida), porque necesita verificar quién opera bajo sus acuerdos.
- `DISPATCH_OFFICE` no gestiona usuarios — su rol es operativo, no de administración de seguridad.

---

## 5. Matriz compacta de referencia rápida

> Tabla resumen para validación rápida. Usar §4 para detalles y justificaciones.

| Pantalla | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH | ADMIN |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Entidades | R | R | C+R | R | R | R | R | **CRUD** | CRUD |
| LER | R | R | R | R | R | R | R | R | **CRUD** |
| Residuos | CRUD-P | R | R | R | R | R | R | **CRUD** | CRUD |
| Operaciones R/D | R | R | R | R | R | R | R | R | **CRUD** |
| Órdenes Servicio | **CRUD-P** | R | R | **CRUD-P** | R | R | R | **CRUD** | CRUD |
| Traslados | R-P | U-P | R | R | R | R | R | **CRUD** | CRUD |
| Entradas Planta | — | — | R | R | — | **CRUD-P** | R | R | CRUD |
| Entradas CAC | — | — | R | R | **CRUD-P** | — | R | R | CRUD |
| Tratamiento | — | — | R | R | — | **CRUD-P** | R | R | CRUD |
| Incidencias | C+R | C+R | C+R | C+R | C+R | C+R | C+R | **CRUD** | CRUD |
| Zonas DUM | R | R | R | R | — | — | R | R | **CRUD** |
| Simulador DUM | R | R | R | R | — | — | R | R | **CRUD** |
| Emisiones | R-P | R-P | R | R | — | R | R | R | **CRUD** |
| Energía Planta | — | — | R | R | — | **CRUD-P** | R | R | CRUD |
| Factores Emisión | — | — | R | R | — | R | R | R | **CRUD** |
| Trazabilidad | R-P | R-P | R | R | R-P | R-P | R | R | R |
| Vista 360° | R-P | R-P | R | R | R-P | R-P | R | R | R |
| KPIs | — | — | R | R | — | R | R | R | R |
| Documentos | R-P | R-P | R | R | R-P | R-P | R | R | CRUD |
| Usuarios | — | — | R-P | — | — | — | — | — | **CRUD** |
| Perfiles | — | — | R | — | — | — | — | — | **CRUD** |

**Negrita** = perfil creador principal de esa pantalla.

---

## 6. Verificación de cobertura de creación

> Regla: toda pantalla debe tener al menos un perfil con capacidad de creación (C, CRUD, o CRUD-P).

| Pantalla | Perfiles creadores | ¿Cubierto? |
|---|---|---|
| Entidades | DISPATCH_OFFICE, ADMIN, SCRAP (restringido) | ✅ |
| LER | ADMIN | ✅ |
| Residuos | PRODUCER (Product/ProductSpec), DISPATCH_OFFICE (Waste), ADMIN | ✅ |
| Operaciones R/D | ADMIN | ✅ |
| Órdenes de Servicio | PRODUCER, PUBLIC_ENT, DISPATCH_OFFICE, ADMIN | ✅ |
| Traslados | DISPATCH_OFFICE, ADMIN | ✅ |
| Entradas Planta | PLANT_OP, ADMIN | ✅ |
| Entradas CAC | CAC_OP, ADMIN | ✅ |
| Tratamiento | PLANT_OP, ADMIN | ✅ |
| Incidencias | Todos (apertura), DISPATCH_OFFICE + ADMIN (resolución) | ✅ |
| Zonas DUM | ADMIN | ✅ |
| Emisiones | Automático (backend) + ADMIN (re-cálculo) | ✅ |
| Energía Planta | PLANT_OP, ADMIN | ✅ |
| Factores Emisión | ADMIN | ✅ |
| Documentos | ADMIN | ✅ |
| Usuarios | ADMIN | ✅ |
| Perfiles | ADMIN | ✅ |

---

## 7. Implementación técnica en .NET

### 7.1. Policies de autorización recomendadas

```
Policy                     Perfiles permitidos
────────────────────────── ──────────────────────────────────────────
CanManageMasters           DISPATCH_OFFICE, ADMIN
CanCreateServiceOrders     PRODUCER, PUBLIC_ENT, DISPATCH_OFFICE, ADMIN
CanManageWasteMoves        DISPATCH_OFFICE, ADMIN
CanUpdateAssignedMoves     CARRIER (solo los suyos)
CanManageEntryPlants       PLANT_OP, ADMIN
CanManageEntryCACs         CAC_OP, ADMIN
CanManageTreatments        PLANT_OP, ADMIN
CanCreateIncidents         Todos los perfiles autenticados
CanResolveIncidents        DISPATCH_OFFICE, ADMIN
CanManageDUMZones          ADMIN
CanManagePlantEnergy       PLANT_OP, ADMIN
CanManageEmissionFactors   ADMIN
CanManageUsers             ADMIN
CanManageProfiles          ADMIN
CanViewKPIs                SCRAP, PUBLIC_ENT, PLANT_OP, COORDINATOR, DISPATCH_OFFICE, ADMIN
CanViewReporting           Todos (con filtrado por datos propios)
CanManageEntities          DISPATCH_OFFICE, ADMIN
CanCreateEntitiesRestricted SCRAP (alta limitada a su ámbito)
```

### 7.2. Filtro multi-tenant (middleware)

```
Todas las queries operativas → WHERE OwnerId = @currentUserOwnerId
Excepción: LERCodes, TreatmentOperations, tablas geográficas (compartidas)
```

### 7.3. Filtro por datos propios (query filter)

```
Si Policy = "Propios" (CRUD-P, R-P, U-P, C+R-P):
  → Aplicar filtro adicional por entidad vinculada del usuario
  → Ejemplo CARRIER: WHERE WasteMoveResidues.IdCarrier = @currentUserEntityId
  → Ejemplo PRODUCER: WHERE ServiceOrders.IdIssuedBy = @currentUserEntityId
```

### 7.4. Seed de perfiles

```sql
INSERT INTO Profiles (Reference, Description) VALUES
('ADMIN', 'Administrador del sistema'),
('SCRAP', 'Sistema Colectivo de Responsabilidad Ampliada'),
('PRODUCER', 'Productor / Generador de residuos'),
('CARRIER', 'Transportista'),
('PLANT_OP', 'Operador de Planta de Tratamiento'),
('CAC_OP', 'Operador de Centro de Acopio'),
('PUBLIC_ENT', 'Entidad Pública / Ayuntamiento'),
('COORDINATOR', 'Coordinador del acuerdo'),
('DISPATCH_OFFICE', 'Oficina de Asignación — Gestor logístico');
```

---

## 8. Notas de diseño

### 8.1. ¿Por qué DISPATCH_OFFICE y no expandir ADMIN?

El `ADMIN` es un superusuario con acceso a configuración de seguridad (usuarios, perfiles). La Oficina de Asignación es un rol **operativo** que no debería poder gestionar usuarios ni perfiles. Separar ambos sigue el principio de mínimo privilegio.

### 8.2. ¿Por qué CARRIER no crea traslados?

El transportista **ejecuta** traslados, no los planifica. La creación del `WasteMove` (agrupación de SOs, asignación de origen/destino/SCRAP) es responsabilidad de la Oficina de Asignación. El transportista recibe el traslado ya planificado y solo actualiza datos de ejecución.

### 8.3. ¿Por qué SCRAP puede dar de alta entidades?

El SCRAP necesita registrar nuevos productores adheridos a su sistema colectivo. Esta alta está **restringida a su ámbito** (solo puede crear entidades vinculadas a sus acuerdos, no entidades de otros SCRAPs).

### 8.4. ¿Por qué KPIs no está visible para PRODUCER y CARRIER?

Los KPIs muestran datos agregados de cumplimiento normativo (tasas de reciclaje, valorización, cumplimiento de cuotas de mercado). Son relevantes para perfiles con responsabilidad de supervisión (`SCRAP`, `PUBLIC_ENT`, `COORDINATOR`) o de gestión operativa (`DISPATCH_OFFICE`, `PLANT_OP`, `ADMIN`), pero no aportan valor a un productor o transportista individual.
