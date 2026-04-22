# Copilot Instructions

## Directrices del proyecto
- Para entidades EF Core en el proyecto GreenTransit: mapeo exacto de tipos SQL (uniqueidentifier→Guid, nvarchar→string, datetime2→DateTime, datetime→DateTime, decimal(18,x)→decimal, int→int, bit→bool, date→DateOnly). NOT NULL → no nullable. NULL → nullable (?). Namespace: GreenTransit.Domain.Entities. Data annotations solo para [Table] y [Column] si el nombre difiere. Incluir propiedades de navegación para todas las FK. No generar DbContext ni Fluent API en el paso de entidades.