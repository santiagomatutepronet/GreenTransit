// heatMapCharts.js — Gráficos e interactividad para el módulo de Mapas de Calor (HM)
// Requiere: Leaflet (dashboardMap.js o leaflet CDN), ApexCharts, Leaflet.heat plugin

window.heatMapCharts = (function () {

    // ── Registro interno de instancias ──────────────────────────────────────
    var _maps   = {};   // { containerId: L.Map }
    var _charts = {};   // { containerId: ApexCharts }

    // ── Utilidades privadas ──────────────────────────────────────────────────

    function destroyChart(id) {
        if (_charts[id]) {
            try { _charts[id].destroy(); } catch (e) { /* ignorar */ }
            delete _charts[id];
        }
    }

    function destroyMap(id) {
        if (_maps[id]) {
            try { _maps[id].remove(); } catch (e) { /* ignorar */ }
            delete _maps[id];
        }
    }

    function getEl(id) {
        return document.getElementById(id);
    }

    var MONTH_LABELS = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun',
                        'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];

    var DAY_LABELS   = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'];

    // ── Mapa Leaflet con capa de heatmap de densidad ─────────────────────────
    // points: [{ lat, lng, value, name }]
    function renderLeafletHeatMap(containerId, points) {
        var el = getEl(containerId);
        if (!el) return;

        destroyMap(containerId);

        // Coordenadas por defecto: centro de España
        var centerLat = 40.416775;
        var centerLng = -3.703790;

        if (points && points.length > 0) {
            centerLat = points.reduce(function (s, p) { return s + p.lat; }, 0) / points.length;
            centerLng = points.reduce(function (s, p) { return s + p.lng; }, 0) / points.length;
        }

        var map = L.map(containerId, { zoomControl: true, scrollWheelZoom: false })
                   .setView([centerLat, centerLng], 6);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '© OpenStreetMap contributors',
            maxZoom: 18
        }).addTo(map);

        if (!points || points.length === 0) {
            _maps[containerId] = map;
            return;
        }

        // Capa de heatmap (Leaflet.heat)
        if (typeof L.heatLayer !== 'undefined') {
            var heatData = points.map(function (p) {
                return [p.lat, p.lng, p.value];
            });
            L.heatLayer(heatData, {
                radius: 35,
                blur: 20,
                maxZoom: 10,
                gradient: { 0.2: '#74c69d', 0.5: '#f9c74f', 0.8: '#f8961e', 1.0: '#d62828' }
            }).addTo(map);
        }

        // Marcadores circulares con popup
        var maxVal = points.reduce(function (m, p) { return Math.max(m, p.value); }, 0);
        points.forEach(function (p) {
            var radius  = maxVal > 0 ? Math.max(6, Math.min(20, 6 + (p.value / maxVal) * 14)) : 8;
            var color   = p.value >= maxVal * 0.9 ? '#d62828'
                        : p.value >= maxVal * 0.75 ? '#f8961e'
                        : '#2d6a4f';
            var marker = L.circleMarker([p.lat, p.lng], {
                radius:      radius,
                fillColor:   color,
                color:       '#fff',
                weight:      1,
                opacity:     0.9,
                fillOpacity: 0.7
            });
            marker.bindPopup(
                '<strong>' + (p.name || 'Punto de recogida') + '</strong><br>' +
                'Kg totales: <strong>' + (p.value ? p.value.toLocaleString('es-ES', { maximumFractionDigits: 0 }) : '0') + ' kg</strong>'
            );
            marker.addTo(map);
        });

        _maps[containerId] = map;
    }

    // ── Donut de tipología de residuos ───────────────────────────────────────
    // labels: string[], values: number[], title: string
    function renderDonut(containerId, labels, values, title) {
        var el = getEl(containerId);
        if (!el) return;
        destroyChart(containerId);

        if (!labels || labels.length === 0) {
            el.innerHTML = '<p class="text-muted text-center py-4">Sin datos de tipología.</p>';
            return;
        }

        var options = {
            chart: {
                type: 'donut',
                height: 280,
                fontFamily: 'inherit',
                toolbar: { show: false }
            },
            series: values,
            labels: labels,
            colors: ['#2d6a4f', '#52b788', '#74c69d', '#95d5b2', '#b7e4c7',
                     '#f9c74f', '#f8961e', '#d62828'],
            legend: {
                position: 'bottom',
                fontSize: '12px'
            },
            dataLabels: {
                enabled: true,
                formatter: function (val) { return val.toFixed(1) + '%'; }
            },
            tooltip: {
                y: {
                    formatter: function (val) {
                        return val >= 1000
                            ? (val / 1000).toFixed(1) + ' t'
                            : val.toFixed(0) + ' kg';
                    }
                }
            },
            plotOptions: {
                pie: { donut: { size: '60%', labels: { show: true, total: { show: true, label: title || 'Total' } } } }
            }
        };

        _charts[containerId] = new ApexCharts(el, options);
        _charts[containerId].render();
    }

    // ── Heatmap temporal 12 meses × capítulo LER ─────────────────────────────
    // data: [{ month: number, chapter: string, kg: number }]
    function renderTemporalHeatMap(containerId, data) {
        var el = getEl(containerId);
        if (!el) return;
        destroyChart(containerId);

        if (!data || data.length === 0) {
            el.innerHTML = '<p class="text-muted text-center py-4">Sin datos temporales.</p>';
            return;
        }

        // Construir series por capítulo
        var chapters = [...new Set(data.map(function (d) { return d.chapter; }))];
        var series = chapters.map(function (ch) {
            return {
                name: ch,
                data: MONTH_LABELS.map(function (_, i) {
                    var item = data.find(function (d) { return d.chapter === ch && d.month === (i + 1); });
                    return { x: MONTH_LABELS[i], y: item ? Math.round(item.kg) : 0 };
                })
            };
        });

        var options = {
            chart: {
                type: 'heatmap',
                height: Math.max(180, chapters.length * 40 + 60),
                fontFamily: 'inherit',
                toolbar: { show: false }
            },
            series: series,
            dataLabels: { enabled: false },
            colors: ['#2d6a4f'],
            xaxis: {
                type: 'category',
                labels: { style: { fontSize: '11px' } }
            },
            tooltip: {
                y: {
                    formatter: function (val) {
                        return val >= 1000
                            ? (val / 1000).toFixed(1) + ' t'
                            : val + ' kg';
                    }
                }
            },
            plotOptions: {
                heatmap: {
                    shadeIntensity: 0.8,
                    colorScale: {
                        ranges: [
                            { from: 0,     to: 0,       color: '#f8f9fa', name: 'Sin datos' },
                            { from: 1,     to: 1000,    color: '#b7e4c7', name: 'Bajo' },
                            { from: 1001,  to: 10000,   color: '#52b788', name: 'Medio' },
                            { from: 10001, to: 100000,  color: '#f9c74f', name: 'Alto' },
                            { from: 100001,to: 9999999, color: '#d62828', name: 'Muy alto' }
                        ]
                    }
                }
            }
        };

        _charts[containerId] = new ApexCharts(el, options);
        _charts[containerId].render();
    }

    // ── Gráfico de líneas multi-serie con media móvil ─────────────────────────
    // seriesData: [{ name, data: number[], movAvg: number[] }]
    function renderMultiLineChart(containerId, seriesData) {
        var el = getEl(containerId);
        if (!el) return;
        destroyChart(containerId);

        if (!seriesData || seriesData.length === 0) {
            el.innerHTML = '<p class="text-muted text-center py-4">Sin datos de tendencia.</p>';
            return;
        }

        var series = [];
        seriesData.forEach(function (s) {
            series.push({ name: s.name, type: 'line', data: s.data });
            if (s.movAvg && s.movAvg.length > 0) {
                series.push({ name: s.name + ' (MM3)', type: 'line', data: s.movAvg });
            }
        });

        var colors = ['#2d6a4f', '#74c69d', '#f9c74f', '#f9844a', '#d62828',
                      '#52b788', '#95d5b2', '#f3722c', '#577590', '#f8961e'];

        var options = {
            chart: {
                type: 'line',
                height: 280,
                fontFamily: 'inherit',
                toolbar: { show: false }
            },
            series: series,
            xaxis: {
                categories: MONTH_LABELS,
                labels: { style: { fontSize: '11px' } }
            },
            yaxis: {
                labels: {
                    formatter: function (v) {
                        return v >= 1000 ? (v / 1000).toFixed(0) + 't' : v.toFixed(0) + 'kg';
                    }
                }
            },
            colors: colors,
            stroke: { curve: 'smooth', width: 2, dashArray: series.map(function (s, i) { return s.name.includes('MM3') ? 4 : 0; }) },
            dataLabels: { enabled: false },
            legend: { position: 'top', fontSize: '12px' },
            grid: { borderColor: '#f0f0f0' },
            tooltip: {
                shared: true,
                y: {
                    formatter: function (v) {
                        return v >= 1000
                            ? (v / 1000).toFixed(1) + ' t'
                            : v.toFixed(0) + ' kg';
                    }
                }
            }
        };

        _charts[containerId] = new ApexCharts(el, options);
        _charts[containerId].render();
    }

    // ── Heatmap semanal 7 días × 24 horas ───────────────────────────────────
    // data: [{ day: 0-6, hour: 0-23, count: number }]
    function renderWeeklyHeatMap(containerId, data) {
        var el = getEl(containerId);
        if (!el) return;
        destroyChart(containerId);

        if (!data || data.length === 0) {
            el.innerHTML = '<p class="text-muted text-center py-4">Sin datos de frecuencia horaria.</p>';
            return;
        }

        var series = DAY_LABELS.map(function (day, dayIdx) {
            return {
                name: day,
                data: Array.from({ length: 24 }, function (_, h) {
                    var item = data.find(function (d) { return d.day === dayIdx && d.hour === h; });
                    return { x: h + 'h', y: item ? item.count : 0 };
                })
            };
        });

        var options = {
            chart: {
                type: 'heatmap',
                height: 320,
                fontFamily: 'inherit',
                toolbar: { show: false }
            },
            series: series,
            dataLabels: { enabled: false },
            colors: ['#2d6a4f'],
            xaxis: {
                type: 'category',
                labels: {
                    rotate: -45,
                    style: { fontSize: '10px' },
                    formatter: function (v, i) { return (typeof i === 'number' && i % 3 === 0) ? v : ''; }
                }
            },
            tooltip: { y: { formatter: function (v) { return v + ' recogidas'; } } },
            plotOptions: {
                heatmap: {
                    shadeIntensity: 0.8,
                    colorScale: {
                        ranges: [
                            { from: 0, to: 0,   color: '#f8f9fa', name: 'Sin recogidas' },
                            { from: 1, to: 3,   color: '#b7e4c7', name: 'Bajo' },
                            { from: 4, to: 10,  color: '#52b788', name: 'Medio' },
                            { from: 11, to: 50, color: '#f9c74f', name: 'Alto' },
                            { from: 51, to: 999,color: '#d62828', name: 'Muy alto' }
                        ]
                    }
                }
            }
        };

        _charts[containerId] = new ApexCharts(el, options);
        _charts[containerId].render();
    }

    // ── Gráfico de barras comparativo entre dos periodos ─────────────────────
    // labels: string[], periodAKg: number[], periodBKg: number[]
    function renderComparisonBar(containerId, labels, periodAKg, periodBKg) {
        var el = getEl(containerId);
        if (!el) return;
        destroyChart(containerId);

        if (!labels || labels.length === 0) {
            el.innerHTML = '<p class="text-muted text-center py-4">Sin datos comparativos.</p>';
            return;
        }

        var options = {
            chart: {
                type: 'bar',
                height: 280,
                fontFamily: 'inherit',
                toolbar: { show: false }
            },
            series: [
                { name: 'Periodo A', data: periodAKg },
                { name: 'Periodo B', data: periodBKg }
            ],
            xaxis: {
                categories: labels,
                labels: { rotate: -30, style: { fontSize: '11px' } }
            },
            yaxis: {
                labels: {
                    formatter: function (v) {
                        return v >= 1000 ? (v / 1000).toFixed(0) + 't' : v.toFixed(0) + 'kg';
                    }
                }
            },
            colors: ['#2d6a4f', '#f9c74f'],
            plotOptions: {
                bar: { borderRadius: 4, columnWidth: '55%', grouped: true }
            },
            dataLabels: { enabled: false },
            legend: { position: 'top' },
            grid: { borderColor: '#f0f0f0' },
            tooltip: {
                shared: true,
                y: {
                    formatter: function (v) {
                        return v >= 1000
                            ? (v / 1000).toFixed(1) + ' t'
                            : v.toFixed(0) + ' kg';
                    }
                }
            }
        };

        _charts[containerId] = new ApexCharts(el, options);
        _charts[containerId].render();
    }

    // ── API pública ──────────────────────────────────────────────────────────
    return {
        renderLeafletHeatMap:  renderLeafletHeatMap,
        renderDonut:           renderDonut,
        renderTemporalHeatMap: renderTemporalHeatMap,
        renderMultiLineChart:  renderMultiLineChart,
        renderWeeklyHeatMap:   renderWeeklyHeatMap,
        renderComparisonBar:   renderComparisonBar
    };

})();
