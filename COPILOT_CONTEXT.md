# 🛰️ GreenTransit — Contexto del Proyecto para Copilot

> **Uso:** Adjunta este archivo al inicio de cada sesión nueva de Copilot Chat en VS Code.
> Después adjunta también `README.md` y `Crear_BD_v4_1.sql` para contexto completo.

---

## 📋 Descripción del Proyecto

Aplicación web para la **gestión operativa, trazabilidad y control de residuos** en un entorno multi-actor (Productores, Transportistas, Gestores, Centros de Acopio, Plantas de tratamiento, Administraciones).

Portal transaccional multiempresa (multi-tenant) orientado a registro de operaciones, control logístico, validación normativa y trazabilidad auditable. **No es un sistema BI ni de optimización avanzada en esta fase.**

---

## 🛠️ Stack Tecnológico

| Componente | Tecnología |
|---|---|
| Runtime | .NET 10 |
| UI | Blazor Web App |
| ORM | EF Core |
| DB | SQL Server (Azure) |
| Auth | OpenID Connect |
| Mediación | MediatR |
| Validación | FluentValidation |
| Logging | Serilog |
| Testing | xUnit |

---

## 🏗️ Arquitectura

**Clean Architecture** con 5 proyectos:

```
GreenTransit/
├── GreenTransit.Domain          → Entidades, interfaces, enumeraciones
├── GreenTransit.Application     → Casos de uso MediatR, FluentValidation, DTOs, interfaces repositorio
├── GreenTransit.Infrastructure  → EF Core SQL Server, repositorios, migraciones
├── GreenTransit.Web             → Blazor Web App .NET 10, autenticación OIDC
└── GreenTransit.Tests           → xUnit, Moq, FluentAssertions
```

---

## 🔐 Autenticación

- **Proveedor:** OpenID Connect externo
- **Authority:** `https://pronet-identity-wst-app.azurewebsites.net/`
- **Flujo:** Authorization Code
- No se almacenan credenciales localmente
- Se recibe ID Token + Access Token

### Mapeo de Claims

| Claim | Uso interno |
|---|---|
| `sub` | → `IdUser` (int, lookup en tabla Users) |
| `email` / `preferred_username` | → Email del usuario |
| Claim organizativo | → `OwnerId` (Guid, filtro multi-tenant) |

---

## ⚙️ Reglas Transversales

- **Multi-tenant:** Todas las queries filtran por `OwnerId`
- **Auditoría:** `CreatedAt`, `UpdatedAt`, `IdUser` en todas las entidades operativas
- **Integridad:** Relaciones lógicas validadas en aplicación, uso de `Hash` cuando aplica
- **PKs:** `uniqueidentifier` (Guid) en tablas operativas; `int IDENTITY` en catálogos/geografía/seguridad

---

## 📊 Base de Datos

**SQL Server Azure** — Script: `Crear_BD_v4_1.sql`

### Dominios funcionales y tablas

| Dominio | Tablas |
|---|---|
| **Maestros** | Entities, LERCodes, Residues, TreatmentOperations |
| **Contratos y Liquidaciones** | Agreements, AgreementDocuments, Settlements, SettlementLines |
| **Operación Logística** | ServiceOrders, WasteMoves, WasteMoveResidues |
| **Entradas y Tratamiento** | EntryPlants, EntryPlantResidues, TreatmentPlants, TreatmentPlantResidues |
| **Entradas CAC** | EntryCACs, EntryCACResidues |
| **Producto y Ecodiseño** | ProductDeclaration, Products, ProductSpecs, EcoModulationRuleSets, EcoModulationRules |
| **Zonas DUM** | DUMZones, DUMRestrictionRules |
| **Sostenibilidad** | EmissionFactorSets, EmissionFactors, PlantEnergies |
| **Operativas de soporte** | Incidents, MarketShares |
| **Seguridad** | Profiles, Users, UserSharePointCredentials |
| **Geografía** | Country, TerritoryState, Province, Municipality, MunicipalityPopulation, MunicipalityZipCode |
| **Diccionarios** | dicProductDeclarationCategory, dicProductDeclarationPeriods, dicProductDeclarationProducts, dicProductDeclarationSource, dicProductDeclarationType, dicProductDeclarationUse, DocStates |

**Total: 38 tablas**

---

## ✅ Estado Actual — Qué está hecho

### Paso 1 — Contexto README.md ✅
El archivo `README.md` con las especificaciones del proyecto fue definido y usado como referencia base.

### Paso 2 — Contexto SQL ✅
El archivo `Crear_BD_v4_1.sql` con el script completo de creación de la BD (38 tablas) fue analizado y usado como referencia para las entidades.

### Paso 3 — Estructura del proyecto ✅
Los comandos dotnet CLI para crear la estructura Clean Architecture con los 5 proyectos fueron definidos y están listos para ejecutar.

### Paso 4 — AppDbContext + EF Core ✅
Se definió la configuración de EF Core en `GreenTransit.Infrastructure`:
- `AppDbContext` con SQL Server
- Filtro global de `OwnerId` (multi-tenant)
- `CreatedAt` y `UpdatedAt` automáticos en `SaveChanges`

### Paso 5 — Entidades de dominio ✅ PROMPTS DEFINIDOS
Se han definido todos los prompts para generar las 38 entidades, dominio a dominio. Los prompts están listos para enviar a Copilot en este orden:

| Prompt | Estado | Tablas |
|---|---|---|
| 5.0 — Instrucción base | ⬜ Pendiente ejecutar | Reglas generales de mapeo |
| 5.1 — Maestros 1/2 | ⬜ Pendiente ejecutar | Entities, LERCodes |
| 5.2 — Maestros 2/2 | ⬜ Pendiente ejecutar | Residues, TreatmentOperations |
| 5.3 — Contratos y Liquidaciones | ⬜ Pendiente ejecutar | Agreements, AgreementDocuments, Settlements, SettlementLines |
| 5.4 — Operación Logística | ⬜ Pendiente ejecutar | ServiceOrders, WasteMoves, WasteMoveResidues |
| 5.5 — Entradas y Tratamiento Planta | ⬜ Pendiente ejecutar | EntryPlants, EntryPlantResidues, TreatmentPlants, TreatmentPlantResidues |
| 5.6 — Entradas CAC | ⬜ Pendiente ejecutar | EntryCACs, EntryCACResidues |
| 5.7 — Producto y Ecodiseño | ⬜ Pendiente ejecutar | ProductDeclaration, Products, ProductSpecs, EcoModulationRuleSets, EcoModulationRules |
| 5.8 — Zonas DUM y Sostenibilidad | ⬜ Pendiente ejecutar | DUMZones, DUMRestrictionRules, EmissionFactorSets, EmissionFactors, PlantEnergies |
| 5.9 — Operativas de soporte | ⬜ Pendiente ejecutar | Incidents, MarketShares |
| 5.10 — Seguridad | ⬜ Pendiente ejecutar | Profiles, Users, UserSharePointCredentials |
| 5.11 — Geografía | ⬜ Pendiente ejecutar | Country, TerritoryState, Province, Municipality, MunicipalityPopulation, MunicipalityZipCode |
| 5.12 — Diccionarios | ⬜ Pendiente ejecutar | 6 tablas dic* + DocStates |

### Paso 6 — Autenticación OpenID Connect ✅ PROMPT DEFINIDO
Prompt completo definido y listo para ejecutar. Incluye:
- Configuración OIDC en `Program.cs`
- `ClaimsTransformation` (IClaimsTransformation)
- `ICurrentUserService` + `CurrentUserService`
- Protección de rutas Blazor con `<AuthorizeRouteView>`
- Sección `appsettings.json` para OIDC

### Paso 7A — Serilog ✅ PROMPT DEFINIDO
Prompt completo definido y listo para ejecutar. Incluye:
- Configuración en `Program.cs` con `UseSerilog()`
- Sección `"Serilog"` en `appsettings.json`
- Sinks Console + File con rolling diario
- Ejemplos de inyección en Blazor y MediatR handlers
- `app.UseSerilogRequestLogging()`
- `try/catch` global y `Log.CloseAndFlush()`

### Paso 7B — xUnit ✅ PROMPT DEFINIDO
Prompt completo definido y listo para ejecutar. Incluye:
- Estructura de carpetas Tests/Domain, Tests/Application, Tests/Infrastructure
- `TestDbContextFactory` con InMemory database
- `FakeCurrentUserService`
- Tests de ejemplo para query handler con filtro multi-tenant
- Tests de ejemplo para entidad de dominio
- Configuración Coverlet para cobertura de código

---

## 🚀 Próximos Pasos — Por dónde continuar

**El siguiente paso es el Paso 5.** Hay que enviar los prompts de entidades a Copilot en orden, empezando por el 5.0.

### Orden de ejecución pendiente:

```
1. Ejecutar Prompt 5.0 (instrucción base) → sin generar código
2. Ejecutar Prompt 5.1 → guardar Entity.cs y LERCode.cs
3. Ejecutar Prompt 5.2 → guardar Residue.cs y TreatmentOperation.cs
4. Ejecutar Prompt 5.3 → guardar Agreement.cs, AgreementDocument.cs, Settlement.cs, SettlementLine.cs
5. Ejecutar Prompt 5.4 → guardar ServiceOrder.cs, WasteMove.cs, WasteMoveResidue.cs
6. Ejecutar Prompt 5.5 → guardar EntryPlant.cs, EntryPlantResidue.cs, TreatmentPlant.cs, TreatmentPlantResidue.cs
7. Ejecutar Prompt 5.6 → guardar EntryCAC.cs, EntryCACResidue.cs
8. Ejecutar Prompt 5.7 → guardar ProductDeclaration.cs, Product.cs, ProductSpec.cs, EcoModulationRuleSet.cs, EcoModulationRule.cs
9. Ejecutar Prompt 5.8 → guardar DUMZone.cs, DUMRestrictionRule.cs, EmissionFactorSet.cs, EmissionFactor.cs, PlantEnergy.cs
10. Ejecutar Prompt 5.9 → guardar Incident.cs, MarketShare.cs
11. Ejecutar Prompt 5.10 → guardar Profile.cs, User.cs, UserSharePointCredential.cs
12. Ejecutar Prompt 5.11 → guardar Country.cs, TerritoryState.cs, Province.cs, Municipality.cs, MunicipalityPopulation.cs, MunicipalityZipCode.cs
13. Ejecutar Prompt 5.12 → guardar los 7 archivos de diccionarios
14. Una vez completadas todas las entidades → Ejecutar Prompt 6 (OIDC)
15. Ejecutar Prompt 7A (Serilog)
16. Ejecutar Prompt 7B (xUnit)
```

> ⚠️ **Importante:** Guarda cada archivo generado por Copilot en su carpeta correspondiente dentro de `GreenTransit.Domain/Entities/` antes de pasar al siguiente prompt.

---

## 📁 Ubicación esperada de archivos generados

```
GreenTransit.Domain/
└── Entities/
    ├── Entity.cs
    ├── LERCode.cs
    ├── Residue.cs
    ├── TreatmentOperation.cs
    ├── Agreement.cs
    ├── AgreementDocument.cs
    ├── Settlement.cs
    ├── SettlementLine.cs
    ├── ServiceOrder.cs
    ├── WasteMove.cs
    ├── WasteMoveResidue.cs
    ├── EntryPlant.cs
    ├── EntryPlantResidue.cs
    ├── TreatmentPlant.cs
    ├── TreatmentPlantResidue.cs
    ├── EntryCAC.cs
    ├── EntryCACResidue.cs
    ├── ProductDeclaration.cs
    ├── Product.cs
    ├── ProductSpec.cs
    ├── EcoModulationRuleSet.cs
    ├── EcoModulationRule.cs
    ├── DUMZone.cs
    ├── DUMRestrictionRule.cs
    ├── EmissionFactorSet.cs
    ├── EmissionFactor.cs
    ├── PlantEnergy.cs
    ├── Incident.cs
    ├── MarketShare.cs
    ├── Profile.cs
    ├── User.cs
    ├── UserSharePointCredential.cs
    ├── Country.cs
    ├── TerritoryState.cs
    ├── Province.cs
    ├── Municipality.cs
    ├── MunicipalityPopulation.cs
    ├── MunicipalityZipCode.cs
    └── Dictionaries/
        ├── DicProductDeclarationCategory.cs
        ├── DicProductDeclarationPeriods.cs
        ├── DicProductDeclarationProducts.cs
        ├── DicProductDeclarationSource.cs
        ├── DicProductDeclarationType.cs
        ├── DicProductDeclarationUse.cs
        └── DocState.cs

GreenTransit.Application/
├── Common/
│   ├── Interfaces/
│   │   ├── ICurrentUserService.cs
│   │   └── IUserRepository.cs
│   └── Behaviours/
│       └── ClaimsTransformation.cs
└── ServiceOrders/
    └── Queries/
        └── GetServiceOrdersQuery.cs

GreenTransit.Infrastructure/
├── Persistence/
│   └── AppDbContext.cs
└── Identity/
    └── CurrentUserService.cs

GreenTransit.Web/
├── Program.cs              ← OIDC + Serilog configurados aquí
├── appsettings.json        ← Secciones OpenIdConnect + Serilog + ConnectionStrings
└── Components/
    └── RedirectToLogin.razor

GreenTransit.Tests/
├── Domain/
├── Application/
│   └── ServiceOrders/
│       └── GetServiceOrdersQueryHandlerTests.cs
├── Infrastructure/
│   └── Repositories/
└── Helpers/
    ├── TestDbContextFactory.cs
    └── FakeCurrentUserService.cs
```

---

## 🗂️ Archivos de referencia del proyecto

| Archivo | Contenido |
|---|---|
| `README.md` | Especificaciones completas del proyecto, stack y auth |
| `Crear_BD_v4_1.sql` | Script SQL con las 38 tablas de la base de datos |
| `Modelo_Datos_GreenTransit_v4_1.docx` | Documento técnico del modelo de datos con descripción de cada tabla |
| `COPILOT_CONTEXT.md` | Este archivo — estado del proyecto para retomar el trabajo |

---

*Última actualización: Inicio del proyecto — Entidades pendientes de generar*
