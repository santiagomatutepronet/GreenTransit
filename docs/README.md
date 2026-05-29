
# 🛰️ Proyecto Eco‑Waste‑Management: Waste Operations Coordinator

[![Platform: .NET 10](https://img.shields.io/badge/.NET-10.0-blueviolet)](https://dotnet.microsoft.com/)
[![Framework: Blazor](https://img.shields.io/badge/UI-Blazor-blue)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![AI: GitHub Copilot Pro](https://img.shields.io/badge/AI-Copilot_Pro-brightgreen)](https://github.com/features/copilot)


---

## 🎯 Propósito del Proyecto

Aplicación web para la **gestión operativa, trazabilidad y control de residuos** en un entorno multi-actor:

- Productores
- Transportistas
- Gestores
- Centros de Acopio (CAC)
- Plantas de tratamiento
- Administraciones

El sistema permite coordinar operaciones reales, asegurar cumplimiento normativo y mantener trazabilidad completa desde origen hasta tratamiento.


---

## 🧭 Alcance funcional

El sistema es un **portal transaccional multiempresa (multi-tenant)** orientado a:

- Registro de operaciones de residuos
- Control logístico y operativo
- Validación normativa
- Trazabilidad auditable
- Base para cálculo económico y ambiental

No es un sistema BI ni de optimización avanzada en esta fase.

---

## 🛠️ Stack tecnológico

| Componente | Tecnología |
|---|---|
| Runtime | .NET 10 |
| UI | Blazor Web App |
| Componentes UI | Radzen Blazor Components (DataGrid, Charts, Dialogs, …) |
| ORM | EF Core |
| DB | SQL Server |
| Auth | OpenID Connect |
| Mediación | MediatR |
| Validación | FluentValidation |
| Logging | Serilog |
| Testing | xUnit |

---

## 📈 Gráficos y visualizaciones

Todos los dashboards y páginas de reporting usan **Radzen Blazor Charts** (`RadzenChart`, `RadzenLineSeries`, `RadzenBarSeries`, `RadzenColumnSeries`, `RadzenDonutSeries`, etc.).

- Componente envoltorio reutilizable: `Components/Shared/AppChart.razor`
- Paleta de colores centralizada: `Components/Shared/ChartPalette.cs`
- Los **heatmaps 2D** (sin equivalente nativo en Radzen) se renderizan como tablas HTML con color de fondo CSS proporcional al valor.
- No se usa ApexCharts, Chart.js ni ninguna otra librería de gráficos JS.

---

## 🔐 Autenticación

Proveedor externo OpenID Connect:

- Authority: https://pronet-identity-wst-app.azurewebsites.net/
- Flujo: Authorization Code

### Comportamiento

- No se almacenan credenciales
- Se recibe ID Token + Access Token

### Claims necesarios

- `sub` → identificador usuario
- `email` o `preferred_username`
- claim organizativo → `OwnerId`

### Uso interno

- `IdUser` ← mapeo de `sub`
- `OwnerId` ← claim organizativo

---

## 🔐 Seguridad y multi-tenant

- Aislamiento por `OwnerId`
- Sin acceso entre tenants
- Autorización por:
  - `Users`
  - `Profiles`

### Sistema de perfiles (11 roles)

| Perfil | Descripción |
|--------|-------------|
| `ADMIN` | Administrador del sistema |
| `SCRAP` | Sistema Colectivo de Responsabilidad Ampliada |
| `PRODUCER` | Productor / Generador de residuos |
| `CARRIER` | Transportista |
| `PLANT_OP` | Operador de Planta de Tratamiento |
| `CAC_OP` | Operador de Centro de Acopio |
| `PUBLIC_ENT` | Entidad Pública / Ayuntamiento |
| `COORDINATOR` | Coordinador del acuerdo |
| `DISPATCH_OFFICE` | Oficina de Asignación — Gestor logístico |
| `REGULATOR` | Regulador — Autoridad de supervisión normativa (solo lectura) |
| `CERTIFIER` | Certificador / Auditor — Validación y coherencia (solo lectura) |

`REGULATOR` y `CERTIFIER` tienen acceso de lectura a KPIs, cumplimiento normativo,
evidencias de tratamiento y huella de carbono. No realizan operaciones de escritura.

---

## ⚙️ Reglas transversales

### Multi-tenant
- Todas las queries filtran por `OwnerId`

### Auditoría
- `CreatedAt`, `UpdatedAt`
- `IdUser`

### Integridad
- Relaciones lógicas validadas en aplicación
- Uso de `Hash` cuando aplica

---

## 🚀 Ejecución

```bash
dotnet restore
dotnet run