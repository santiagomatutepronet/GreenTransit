// mobilityCharts.js — Gráficos de movilidad urbana (UC3)
// Requiere ApexCharts (incluido en blazor-apexcharts.js)

// ── Histórico de recogidas e incidencias (MunicipalMonitoring) ──────────────
window.mobilityMunicipalHistory = (function () {
    var _chart = null;

    return {
        render: function (containerId, categories, pickups, incidents) {
            var el = document.getElementById(containerId);
            if (!el) return;

            if (_chart) {
                try { _chart.destroy(); } catch (e) { /* ignorar */ }
                _chart = null;
            }

            if (!categories || categories.length === 0) return;


            // Gráfico mixto: barras (recogidas) + línea (incidencias)
            // chart.type debe ser 'line' cuando se usa mixed chart (column + line) en ApexCharts
            var options = {
                chart: {
                    type: 'line',
                    height: 260,
                    toolbar: { show: false },
                    fontFamily: 'inherit'
                },
                series: [
                    { name: 'Recogidas',   type: 'column', data: pickups   },
                    { name: 'Incidencias', type: 'line',   data: incidents }
                ],
                xaxis: {
                    categories: categories,
                    labels: { rotate: -30, style: { fontSize: '11px' } }
                },
                yaxis: [
                    {
                        seriesName: 'Recogidas',
                        title: { text: 'Nº recogidas' },
                        labels: { formatter: function (v) { return Math.round(v); } }
                    },
                    {
                        seriesName: 'Incidencias',
                        opposite: true,
                        title: { text: 'Incidencias' },
                        labels: { formatter: function (v) { return Math.round(v); } }
                    }
                ],
                colors: ['#198754', '#dc3545'],
                stroke: { curve: 'smooth', width: [0, 3] },
                plotOptions: { bar: { borderRadius: 4, columnWidth: '55%' } },
                dataLabels: { enabled: false },
                grid: { borderColor: '#f0f0f0' },
                legend: { position: 'top' },
                tooltip: { shared: true, intersect: false }
            };

            _chart = new ApexCharts(el, options);
            _chart.render();
        },
        clear: function (containerId) {
            if (_chart) { try { _chart.destroy(); } catch (e) { } _chart = null; }
            var el = document.getElementById(containerId);
            if (el) el.innerHTML = '<p class="text-muted text-center py-4">Sin datos históricos para el periodo.</p>';
        }
    };
})();

// ── Heatmap 7×24 de recogidas (CoordinatorAnalysis) ─────────────────────────
// series: array de { name: "Lun"|"Mar"…, data: [{ x: "00h", y: n }, …] }
// Los nombres de los días ya vienen como series[i].name (eje Y).
// Las horas vienen como data[j].x (eje X). No se sobreescribe xaxis.categories.
window.mobilityHeatmap = (function () {
    var _chart = null;

    return {
        render: function (containerId, series) {
            var el = document.getElementById(containerId);
            if (!el) return;

            if (_chart) { try { _chart.destroy(); } catch (e) { /* ignorar */ } _chart = null; }

            var options = {
                chart: {
                    type: 'heatmap',
                    height: Math.max(240, series.length * 32),
                    toolbar: { show: false },
                    fontFamily: 'inherit'
                },
                series: series,
                xaxis: {
                    // Las horas (00h-23h) se leen directamente de data[j].x
                    labels: { rotate: -45, style: { fontSize: '10px' } },
                    tooltip: { enabled: false }
                },
                yaxis: {
                    labels: { style: { fontSize: '11px' } }
                },
                dataLabels: { enabled: false },
                colors: ['#198754'],
                plotOptions: {
                    heatmap: {
                        shadeIntensity: 0.5,
                        colorScale: {
                            ranges: [
                                { from: 0, to: 0,  color: '#f0f0f0', name: 'Sin recogidas' },
                                { from: 1, to: 3,  color: '#a8d5b5', name: '1-3' },
                                { from: 4, to: 7,  color: '#4caf7d', name: '4-7' },
                                { from: 8, to: 999, color: '#198754', name: '8+' }
                            ]
                        }
                    }
                },
                grid: { padding: { right: 0 } },
                legend: { show: true, position: 'bottom' },
                tooltip: {
                    y: { formatter: function (v) { return v + ' recogidas'; } }
                }
            };

            _chart = new ApexCharts(el, options);
            _chart.render();
        }
    };
})();

// ── Evolución mensual de métricas de movilidad (DispatchData) ────────────────
window.mobilityMonthlyTrend = (function () {
    var _chart = null;

    return {
        render: function (containerId, categories, peakHour, dumComp, conflict) {
            var el = document.getElementById(containerId);
            if (!el) return;

            if (_chart) {
                try { _chart.destroy(); } catch (e) { /* ignorar */ }
                _chart = null;
            }

            var options = {
                chart: {
                    type: 'line',
                    height: 260,
                    toolbar: { show: false },
                    fontFamily: 'inherit'
                },
                series: [
                    { name: '% Hora pico',     data: peakHour },
                    { name: '% Cumple DUM',    data: dumComp  },
                    { name: 'Índice conflicto', data: conflict }
                ],
                xaxis: {
                    categories: categories,
                    labels: { rotate: -30, style: { fontSize: '11px' } }
                },
                yaxis: {
                    min: 0,
                    labels: { formatter: function (v) { return v.toFixed(0) + '%'; } }
                },
                colors: ['#dc3545', '#198754', '#fd7e14'],
                stroke: { curve: 'smooth', width: 2 },
                dataLabels: { enabled: false },
                grid: { borderColor: '#f0f0f0' },
                legend: { position: 'top' },
                tooltip: {
                    shared: true,
                    intersect: false,
                    y: { formatter: function (v) { return v != null ? v.toFixed(1) + '%' : '—'; } }
                },
                markers: { size: 4 }
            };

            _chart = new ApexCharts(el, options);
            _chart.render();
        }
    };
})();

// ── Comparativa de eficiencia entre periodos (CoordinatorAnalysis) ───────────
window.mobilityComparison = (function () {
    var _chart = null;

    return {
        render: function (containerId, data) {
            var el = document.getElementById(containerId);
            if (!el) return;

            if (_chart) { try { _chart.destroy(); } catch (e) { /* ignorar */ } _chart = null; }

            var options = {
                chart: {
                    type: 'bar',
                    height: 260,
                    toolbar: { show: false },
                    fontFamily: 'inherit'
                },
                series: [
                    { name: data.periodA || 'Periodo A', data: data.valuesA },
                    { name: data.periodB || 'Periodo B', data: data.valuesB }
                ],
                xaxis: {
                    categories: data.categories,
                    labels: { style: { fontSize: '11px' } }
                },
                plotOptions: {
                    bar: { horizontal: false, borderRadius: 4, columnWidth: '55%' }
                },
                dataLabels: {
                    enabled: true,
                    formatter: function (v) { return v != null ? v.toFixed(1) : ''; },
                    style: { fontSize: '10px' }
                },
                colors: ['#0d6efd', '#fd7e14'],
                grid: { borderColor: '#f0f0f0' },
                legend: { position: 'top' },
                tooltip: { shared: true, intersect: false }
            };

            _chart = new ApexCharts(el, options);
            _chart.render();
        }
    };
})();
