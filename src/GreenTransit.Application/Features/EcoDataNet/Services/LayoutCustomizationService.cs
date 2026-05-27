using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

namespace GreenTransit.Application.Features.EcoDataNet.Services;

/// <summary>
/// Implementación de ILayoutCustomizationService.
/// Combina el layout automático con los overrides del usuario.
/// </summary>
public class LayoutCustomizationService : ILayoutCustomizationService
{
    /// <inheritdoc />
    public LayoutMergeResult ApplyOverrides(
        List<DynamicWidgetDescriptor> autoWidgets,
        List<WidgetLayoutOverride> overrides,
        string? savedSchemaHash,
        string currentSchemaHash)
    {
        // Sin overrides → devolver widgets automáticos sin cambios
        if (overrides.Count == 0)
        {
            return new LayoutMergeResult
            {
                Widgets       = autoWidgets.OrderBy(w => w.SortOrder).ToList(),
                SchemaChanged = false
            };
        }

        // Detectar cambio de esquema
        bool schemaChanged = savedSchemaHash is not null
                          && !string.Equals(savedSchemaHash, currentSchemaHash,
                                            StringComparison.OrdinalIgnoreCase);

        // Diccionario de overrides por WidgetId para lookup O(1)
        var overrideMap = overrides.ToDictionary(o => o.WidgetId, StringComparer.Ordinal);

        // Set de WidgetIds actuales
        var currentIds = autoWidgets.Select(w => w.WidgetId).ToHashSet(StringComparer.Ordinal);

        // Widgets nuevos (en autoWidgets pero sin override)
        var newWidgetIds = autoWidgets
            .Where(w => !overrideMap.ContainsKey(w.WidgetId))
            .Select(w => w.WidgetId)
            .ToList();

        // Overrides obsoletos (guardados para WidgetIds que ya no existen)
        var obsoleteWidgetIds = overrides
            .Where(o => !currentIds.Contains(o.WidgetId))
            .Select(o => o.WidgetId)
            .ToList();

        // Aplicar overrides a cada widget
        foreach (var widget in autoWidgets)
        {
            if (!overrideMap.TryGetValue(widget.WidgetId, out var ov))
                continue;

            if (ov.IsHidden)
                widget.IsHidden = true;

            if (ov.CustomSortOrder.HasValue)
                widget.SortOrder = ov.CustomSortOrder.Value;

            if (ov.CustomColumnSpan.HasValue)
                widget.ColumnSpan = ov.CustomColumnSpan.Value;

            if (ov.CustomTitle is not null)
                widget.Title = ov.CustomTitle;

            if (ov.CustomChartType.HasValue && widget.Type == WidgetType.Chart)
                widget.ChartType = ov.CustomChartType.Value;

            if (ov.CustomWidgetType.HasValue)
                widget.Type = ov.CustomWidgetType.Value;

            // Aplicar override de binding de campos de gráfico
            if (ov.CustomChartBinding != null && widget.Type == WidgetType.Chart)
            {
                if (ov.CustomChartBinding.CustomCategoryField != null
                    && (widget.AvailableCategoryFields?.Contains(ov.CustomChartBinding.CustomCategoryField) == true))
                {
                    widget.ChartCategoryField = ov.CustomChartBinding.CustomCategoryField;
                }

                if (ov.CustomChartBinding.CustomValueFields != null && ov.CustomChartBinding.CustomValueFields.Count > 0)
                {
                    var validFields = ov.CustomChartBinding.CustomValueFields
                        .Where(f => widget.AvailableValueFields?.Contains(f) == true)
                        .ToList();

                    if (validFields.Count > 0)
                    {
                        // Donut/Pie es mono-serie: solo el primer campo
                        widget.ChartValueFields = widget.ChartType is ChartSubType.Donut or ChartSubType.Pie
                            ? [validFields[0]]
                            : validFields;
                    }
                    // Si todos son obsoletos, se mantienen los automáticos (sin cambio)
                }

                // Garantizar mono-serie para Donut/Pie independientemente del origen
                if (widget.ChartType is ChartSubType.Donut or ChartSubType.Pie
                    && widget.ChartValueFields?.Count > 1)
                {
                    widget.ChartValueFields = [widget.ChartValueFields[0]];
                }
            }

            // Aplicar overrides de columnas de tabla
            if (ov.CustomTableColumns != null && widget.Type == WidgetType.DataTable
                && widget.TableColumns != null)
            {
                var columnOverrideMap = ov.CustomTableColumns.ToDictionary(
                    c => c.PropertyName, StringComparer.Ordinal);

                foreach (var col in widget.TableColumns)
                {
                    if (!columnOverrideMap.TryGetValue(col.PropertyName, out var colOv))
                        continue;

                    col.IsHidden = colOv.IsHidden;
                    if (colOv.CustomWidth.HasValue)
                        col.Width = colOv.CustomWidth.Value;
                }
            }
        }

        return new LayoutMergeResult
        {
            Widgets          = autoWidgets.OrderBy(w => w.SortOrder).ToList(),
            SchemaChanged    = schemaChanged,
            NewWidgetIds     = newWidgetIds,
            ObsoleteWidgetIds = obsoleteWidgetIds
        };
    }
}
