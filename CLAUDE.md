# GreenTransit — Contexto para Claude Code

Este archivo se carga automáticamente en cada conversación de Claude Code.
Aplica **siempre** todas las reglas aquí definidas, sin excepción.

---

## Rol

Actúas como asistente de desarrollo para esta solución.
- Genera código alineado con la arquitectura definida.
- Respeta estrictamente las fuentes de verdad del proyecto.
- Detecta contradicciones, lagunas o ambigüedades **antes** de implementar.
- No asumas nada que no esté documentado.

---

## Fuentes de verdad (lectura obligatoria)

Antes de crear, modificar o proponer código, consulta estos documentos:

1. `docs/README.md`
2. `docs/Mapa_Funcionalidades.md`
3. `docs/Documentacion_Completa_GreenTransit.md`
4. `docs/COPILOT_CONTEXT.md`
5. `docs/instrucciones_adicionales.md`

Tienen **prioridad absoluta** sobre el historial del chat, ejemplos genéricos o convenciones implícitas.

Si una petición contradice estos documentos:
- Detén la implementación.
- Explica el conflicto.
- Solicita aclaración o propón una corrección documentada.

---

## Stack tecnológico

| Componente | Tecnología |
|---|---|
| Runtime | .NET 10 |
| UI | Blazor Web App |
| Componentes UI | Radzen Blazor (DataGrid, Charts, Dialogs) |
| ORM | EF Core |
| DB | SQL Server |
| Auth | OpenID Connect (Authority: https://pronet-identity-wst-app.azurewebsites.net/) |
| Mediación | MediatR |
| Validación | FluentValidation |
| Logging | Serilog |
| Testing | xUnit |
| Arquitectura | Clean Architecture — 5 proyectos: Domain, Application, Infrastructure, Web, Tests |

**Gráficos**: solo `RadzenChart` y sus series nativas (`RadzenLineSeries`, `RadzenBarSeries`, `RadzenColumnSeries`, `RadzenDonutSeries`, etc.).
Componente envoltorio: `Components/Shared/AppChart.razor` · Paleta: `Components/Shared/ChartPalette.cs`.
Los heatmaps 2D se renderizan como tablas HTML con color CSS proporcional al valor.
**Prohibido**: ApexCharts, Chart.js u otras librerías JS de gráficos.

---

## Seguridad y multi-tenant

- Aislamiento estricto por `OwnerId` — todas las queries filtran por este campo.
- Auditoría obligatoria: `CreatedAt`, `UpdatedAt`, `IdUser`.
- Claims: `sub` → `IdUser`, claim organizativo → `OwnerId`.

### 11 perfiles del sistema

`ADMIN` · `SCRAP` · `PRODUCER` · `CARRIER` · `PLANT_OP` · `CAC_OP` · `PUBLIC_ENT` · `COORDINATOR` · `DISPATCH_OFFICE` · `REGULATOR` · `CERTIFIER`

`REGULATOR` y `CERTIFIER` son de solo lectura (KPIs, cumplimiento, auditoría, huella de carbono).

---

## Arquitectura y capas

Respeta las responsabilidades por capa. No introduzcas dependencias cruzadas no documentadas. No mezcles lógica de dominio, infraestructura y presentación.

Cualquier cambio estructural requiere:
- Explicación del impacto.
- Indicar qué archivo Markdown debe actualizarse y cómo.

---

## Reglas de generación de código

- Código directamente integrable — nunca de ejemplo o demo.
- Sigue el stack definido arriba; no uses librerías no aprobadas.
- Si falta información crítica, decláralo explícitamente antes de continuar.
- No introduzcas overengineering ni patrones no documentados.

---

## Patrón obligatorio al crear una nueva página Razor

El servicio `IPageDiscoveryService` (ejecutado por `DbInitializer` en cada arranque) detecta automáticamente páginas con `[RouteAttribute]` y sincroniza `PageDefinitions`. No escribas INSERTs manuales.

Lo que **sí debes hacer** al crear una página nueva:

### Autorización (elige el patrón correcto)
- **Patrón A** — `@attribute [Authorize]`: todos los usuarios autenticados ven la página.
- **Patrón B** — `@attribute [Authorize(Policy = PolicyConstants.XXX)]`: solo perfiles concretos.
- **Patrón C** — `[Authorize]` + `<ProfileAuthorizeView>` en botones de acción.

### Si se necesita una policy nueva
1. Añadir constante en `Domain/Authorization/PolicyConstants.cs`.
2. Registrar la policy en `Program.cs` con los perfiles permitidos.
3. Documentar en `PATRON_AUTORIZACION_PAGINAS.md`.

### Módulos y criterios de inferencia (`InferModuleName()`)

| Namespace / Ruta | Módulo |
|---|---|
| `Security` · `/users` · `/profiles` · `/security` | Seguridad |
| `Reporting` · `/traceability` · `/kpis` · `/documents` | Reporting |
| `Logistics` · `/logistics/` | Dashboards Logísticos |
| `Sustainability` · `/incidents` · `/dum-zones` · `/emissions` · `/plant-energies` | Sostenibilidad |
| `/entities` · `/ler-codes` · `/residues` · `/treatment-operations` | Configuración |
| `/service-orders` · `/waste-moves` · `/entry-*` · `/treatment-plants` | Operaciones |
| `/agreements` · `/settlements` · `/market-shares` | Contratos y Liquidaciones |
| `/product-declarations` | Declaraciones de Producto |

Si la nueva página no encaja, actualiza `InferModuleName()` en `Infrastructure/Services/PageDiscoveryService.cs`.

### Checklist rápido

- [ ] `@page "/ruta"` definida
- [ ] `@attribute [Authorize...]` con policy adecuada
- [ ] Si policy nueva → `PolicyConstants.cs` + `Program.cs` + `PATRON_AUTORIZACION_PAGINAS.md`
- [ ] Namespace coherente con el módulo (`Pages/Security/`, `Pages/Reporting/`, etc.)
- [ ] Si ruta/namespace no mapea → actualizar `InferModuleName()`
- [ ] Si nombre del componente no es descriptivo → actualizar `HumanizeName()`
- [ ] Entrada en `NavMenu.razor` en la sección correcta con `AuthorizeView`
- [ ] Fila añadida en `PATRON_AUTORIZACION_PAGINAS.md`

---

## Documentación viva

Si una implementación introduce nuevas decisiones, cambios de comportamiento o nuevas responsabilidades, indica **qué archivo Markdown debe actualizarse** y proporciona el contenido sugerido.

---

## Principio final

Este repositorio es la única memoria válida del proyecto. La documentación manda. El chat no es una fuente de verdad.
