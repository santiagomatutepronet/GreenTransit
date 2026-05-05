
🛰️ GreenTransit — Contexto del Proyecto para Copilot
Uso: Adjunta este archivo al inicio de cada sesión nueva de Copilot Chat en VS Code. Después adjunta también README.md y Crear_BD_v4_1.sql para contexto completo.


📋 Descripción del Proyecto
Aplicación web para la gestión operativa, trazabilidad y control de residuos en un entorno multi-actor (Productores, Transportistas, Gestores, Centros de Acopio, Plantas de tratamiento, Administraciones). Portal transaccional multiempresa (multi-tenant) orientado a registro de operaciones, control logístico, validación normativa y trazabilidad auditable. No es un sistema BI ni de optimización avanzada en esta fase.


🛠️ Stack Tecnológico

Componente	Tecnología
Runtime	.NET 10
UI	Blazor Web App
ORM	EF Core
DB	SQL Server (Azure)
Auth	OpenID Connect
Mediación	MediatR
Validación	FluentValidation
Logging	Serilog
Testing	xUnit



🏗️ Arquitectura
Clean Architecture con 5 proyectos:
GreenTransit/
├── GreenTransit.Domain
├── GreenTransit.Application
├── GreenTransit.Infrastructure
├── GreenTransit.Web
└── GreenTransit.Tests




🔐 Autenticación

Proveedor: OpenID Connect externo
Authority: https://pronet-identity-wst-app.azurewebsites.net/
Flujo: Authorization Code
ID Token + Access Token
Mapeo de Claims

Claim	Uso interno
sub	IdUser
email / preferred_username	Email
Claim organizativo	OwnerId (multi-tenant)



⚙️ Reglas Transversales

Multi-tenant: filtro por OwnerId en todas las queries
Auditoría: CreatedAt, UpdatedAt, IdUser
PKs: Guid (operativas) / int identity (catálogos)


📊 Base de Datos
SQL Server Azure — 38 tablas (Crear_BD_v4_1.sql)


✅ ESTADO DEL PROYECTO — TODO COMPLETADO
Paso 1 — Contexto README.md ✅ COMPLETADO
Paso 2 — Contexto SQL ✅ COMPLETADO
Paso 3 — Estructura del proyecto ✅ COMPLETADO
Paso 4 — AppDbContext + EF Core ✅ COMPLETADO
Paso 5 — Entidades de dominio ✅ COMPLETADO (TODAS)

Prompt	Estado	Tablas
5.0 — Instrucción base	✅ Completado	Reglas generales
5.1 — Maestros 1/2	✅ Completado	Entities, LERCodes
5.2 — Maestros 2/2	✅ Completado	Residues, TreatmentOperations
5.3 — Contratos y Liquidaciones	 Pendiente	Agreements*, Settlements*
5.4 — Operación Logística	✅ Completado	ServiceOrders, WasteMoves
5.5 — Entradas Planta	✅ Completado	EntryPlants*, TreatmentPlants*
5.6 — Entradas CAC	✅ Completado	EntryCACs*
5.7 — Producto y Ecodiseño	✅ Completado	Product*, EcoModulation*
5.8 — Zonas DUM y Sostenibilidad	✅ Completado	DUM*, EmissionFactors*
5.9 — Operativas soporte	✅ Completado	Incidents, MarketShares
5.10 — Seguridad	✅ Completado	Profiles, Users
5.11 — Geografía	✅ Completado	Country → ZipCodes
5.12 — Diccionarios	✅ Completado	dic* + DocStates

Paso 6 — Autenticación OpenID Connect  ✅ COMPLETADO

Program.cs configurado (OIDC + Cookies, ClaimsTransformation, AddCascadingAuthenticationState, AddControllers)
ClaimsTransformation (ya existía — integrada en el DI)
CurrentUserService (lee claims reales: IsAuthenticated, IdUser, OwnerId, Email, UserName, UserProfile)
AccountController (GET /account/login → Challenge OIDC; GET /account/logout → SignOut)
Routes.razor con AuthorizeRouteView + NotAuthorized → RedirectToLogin
App.razor con CascadingAuthenticationState
MainLayout.razor con botón de logout (AuthorizeView)
Paso 7A — Serilog ✅ COMPLETADO

Console + File
Middleware logging
Paso 7B — xUnit ✅ COMPLETADO

Infraestructura tests
InMemory DB
Ejemplos funcionales

Paso 8 — Seguridad completa (Partes 1-4) ✅ COMPLETADO
Parte 1 — OIDC (Program.cs, AccountController, AddCascadingAuthenticationState)
Parte 2 — ClaimsTransformation (gt_* claims, gt_user_found, gt_entity_id), CurrentUserService, AccesoDenegado.razor
Parte 3 — EntityUserProvisioningService, EntityRoles (DISPATCH_OFFICE), EntityForm mensajes
Parte 4 — DbInitializer (seed idempotente de 9 Profiles en startup)


📌 ESTADO FINAL
✅ Todos los prompts hasta el Paso 7 ejecutados y completados.
El proyecto queda listo para:

Implementar casos de uso reales (CQRS)
Construir pantallas Blazor
Endurecer validaciones de dominio
Integración externa / APIs / reporting
Este archivo refleja un estado 100 % completado y sirve como punto de continuidad para futuras sesiones.
Última actualización: Proyecto base completamente generado
