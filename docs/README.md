
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
| ORM | EF Core |
| DB | SQL Server |
| Auth | OpenID Connect |
| Mediación | MediatR |
| Validación | FluentValidation |
| Logging | Serilog |
| Testing | xUnit |

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