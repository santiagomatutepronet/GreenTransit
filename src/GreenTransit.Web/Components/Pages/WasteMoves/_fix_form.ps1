$file = "C:\work\proyectos\GreenTransit\src\GreenTransit.Web\Components\Pages\WasteMoves\WasteMoveForm.razor"
$lines = [System.IO.File]::ReadAllLines($file, [System.Text.Encoding]::UTF8)

# Lines 47-99 (0-based 46-98) = the old RadzenFieldset block for SOs
# We keep: 0-45 (before), then new block, then 99-end (after)

$newBlock = @'
        <RadzenFieldset Text="Ordenes de Servicio a agrupar" class="rz-mb-4">
            @if (_eligibleSOs.Count == 0)
            {
                <RadzenAlert AlertStyle="AlertStyle.Warning">
                    No hay Ordenes de Servicio disponibles (Pendiente/Planificada sin traslado asignado).
                </RadzenAlert>
            }
            else
            {
                <div class="so-search-bar rz-mb-3">
                    <i class="bi bi-search so-search-icon"></i>
                    <input class="so-search-input" placeholder="Filtrar por numero, LER o punto de recogida..."
                           @bind="_soFilter" @bind:event="oninput" />
                    @if (_lines.Select(l => l.SoId).Distinct().Any())
                    {
                        <span class="so-selected-badge">@(_lines.Select(l => l.SoId).Distinct().Count()) seleccionada(s)</span>
                    }
                </div>
                <div class="so-card-grid">
                    @foreach (var so in _eligibleSOs.Where(SoMatchesFilter))
                    {
                        var isSelected = _lines.Any(l => l.SoId == so.Id);
                        <div class="so-card @(isSelected ? "so-card--selected" : "")"
                             @onclick="async () => await ToggleSoAsync(so)">
                            <div class="so-card-check">
                                @if (isSelected) { <i class="bi bi-check-circle-fill"></i> }
                                else             { <i class="bi bi-circle"></i> }
                            </div>
                            <div class="so-card-body">
                                <div class="so-card-header-row">
                                    <span class="so-card-number">@so.ServiceOrderNumber</span>
                                    <span class="@ServiceOrderStatuses.BadgeCss(so.Status)">@ServiceOrderStatuses.Label(so.Status)</span>
                                </div>
                                <div class="so-card-meta">
                                    @if (!string.IsNullOrEmpty(so.LerCodesDisplay))
                                    {
                                        <span class="so-card-chip"><i class="bi bi-tag"></i> @so.LerCodesDisplay</span>
                                    }
                                    else if (so.LerCodeCode != null)
                                    {
                                        <span class="so-card-chip" title="@so.LerCodeDescription"><i class="bi bi-tag"></i> @so.LerCodeCode</span>
                                    }
                                    @if (so.EstimatedWeight.HasValue)
                                    {
                                        <span class="so-card-chip"><i class="bi bi-box-seam"></i> @($"{so.EstimatedWeight:N0} kg")</span>
                                    }
                                    @if (so.PlannedPickupStart.HasValue)
                                    {
                                        <span class="so-card-chip"><i class="bi bi-calendar3"></i> @so.PlannedPickupStart.Value.ToString("dd/MM/yyyy")</span>
                                    }
                                    @if (!string.IsNullOrEmpty(so.PickupPointName))
                                    {
                                        <span class="so-card-chip so-card-chip--location"><i class="bi bi-geo-alt"></i> @so.PickupPointName</span>
                                    }
                                </div>
                            </div>
                        </div>
                    }
                    @if (!_eligibleSOs.Any(SoMatchesFilter))
                    {
                        <p class="so-no-results">Ninguna orden coincide con el filtro.</p>
                    }
                </div>
                @if (_lines.Count == 0)
                {
                    <small class="rz-color-danger rz-mt-2 d-block">Selecciona al menos una Orden de Servicio.</small>
                }
            }
        </RadzenFieldset>
'@

$before   = $lines[0..45]
$after    = $lines[99..($lines.Length - 1)]
$newLines = $before + ($newBlock -split "\r?\n") + $after

[System.IO.File]::WriteAllLines($file, $newLines, [System.Text.Encoding]::UTF8)
Write-Host "OK: $($newLines.Length) lines written"
