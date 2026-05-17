// carbonFootprintCharts.js — Gráficos ApexCharts para los dashboards de Huella de Carbono (HC)
// Requiere: ApexCharts (blazor-apexcharts.js)

window.carbonCharts = (function () {

    var _charts = {}; // { containerId: ApexCharts }

    function destroy(id) {
        if (_charts[id]) {
            try { _charts[id].destroy(); } catch (e) { /* ignorar */ }
            delete _charts[id];
        }
    }

    function el(id) { return document.getElementById(id); }

    var MONTH_LABELS = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun',
                        'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];

    // ─────────────────────────────────────────────────────────────────────────
    // HC-A: Evolución mensual de emisiones (área)
    // months: [{ year, month, co2eTonnes }]
    // ─────────────────────────────────────────────────────────────────────────
    function renderMonthlyEvolution(containerId, months) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !months || months.length === 0) return;

        var labels  = months.map(function (m) { return MONTH_LABELS[m.month - 1] + ' ' + m.year; });
        var co2Data = months.map(function (m) { return +(m.co2eTonnes).toFixed(3); });

        var options = {
            chart: { type: 'area', height: 280, toolbar: { show: false }, fontFamily: 'inherit' },
            series: [{ name: 't CO₂e', data: co2Data }],
            xaxis: { categories: labels, labels: { rotate: -30, style: { fontSize: '11px' } } },
            yaxis: {
                title: { text: 't CO₂e' },
                labels: { formatter: function (v) { return v.toFixed(2); } }
            },
            colors: ['#198754'],
            stroke: { curve: 'smooth', width: 2 },
            fill: { type: 'gradient', opacity: 0.25 },
            dataLabels: { enabled: false },
            grid: { borderColor: '#f0f0f0' },
            legend: { show: false },
            tooltip: { y: { formatter: function (v) { return v.toFixed(3) + ' t CO₂e'; } } }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HC-A / HC-B: Donut por tipo de combustible
    // fuels: [{ fuelType, co2eTonnes, pct }]
    // ─────────────────────────────────────────────────────────────────────────
    function renderFuelDonut(containerId, fuels) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !fuels || fuels.length === 0) return;

        var labels  = fuels.map(function (f) { return f.fuelType || 'Desconocido'; });
        var values  = fuels.map(function (f) { return +(f.co2eTonnes).toFixed(3); });
        var colors  = ['#198754','#0d6efd','#fd7e14','#6f42c1','#20c997','#dc3545','#ffc107','#0dcaf0'];

        var options = {
            chart: { type: 'donut', height: 280, fontFamily: 'inherit' },
            series: values,
            labels: labels,
            colors: colors.slice(0, labels.length),
            legend: { position: 'bottom', fontSize: '12px' },
            dataLabels: { enabled: true, formatter: function (val) { return val.toFixed(1) + '%'; } },
            plotOptions: { pie: { donut: { size: '65%', labels: {
                show: true,
                total: { show: true, label: 'Total', formatter: function (w) {
                    return w.globals.seriesTotals.reduce(function (a, b) { return a + b; }, 0).toFixed(2) + ' t';
                }}
            }}}},
            tooltip: { y: { formatter: function (v) { return v.toFixed(3) + ' t CO₂e'; } } }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HC-B: Barras apiladas emisiones mensuales por combustible
    // rows: [{ year, month, fuelType, co2eTonnes }]
    // ─────────────────────────────────────────────────────────────────────────
    function renderMonthlyFuelStacked(containerId, rows) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !rows || rows.length === 0) return;

        // Agrupar por etiqueta de mes y combustible
        var labelMap = {};
        rows.forEach(function (r) {
            var lbl = MONTH_LABELS[r.month - 1] + ' ' + r.year;
            if (!labelMap[lbl]) labelMap[lbl] = {};
            labelMap[lbl][r.fuelType || 'Desconocido'] = (labelMap[lbl][r.fuelType || 'Desconocido'] || 0) + r.co2eTonnes;
        });

        var categories = Object.keys(labelMap);
        var fuels = [];
        rows.forEach(function (r) {
            var f = r.fuelType || 'Desconocido';
            if (fuels.indexOf(f) === -1) fuels.push(f);
        });

        var colors = ['#198754','#0d6efd','#fd7e14','#6f42c1','#20c997','#dc3545','#ffc107','#0dcaf0'];
        var series = fuels.map(function (fuel, i) {
            return {
                name: fuel,
                data: categories.map(function (lbl) { return +(labelMap[lbl][fuel] || 0).toFixed(3); })
            };
        });

        var options = {
            chart: { type: 'bar', height: 280, stacked: true, toolbar: { show: false }, fontFamily: 'inherit' },
            series: series,
            xaxis: { categories: categories, labels: { rotate: -30, style: { fontSize: '11px' } } },
            yaxis: { title: { text: 't CO₂e' }, labels: { formatter: function (v) { return v.toFixed(2); } } },
            colors: colors.slice(0, fuels.length),
            plotOptions: { bar: { borderRadius: 2, columnWidth: '60%' } },
            dataLabels: { enabled: false },
            grid: { borderColor: '#f0f0f0' },
            legend: { position: 'top' },
            tooltip: { shared: true, intersect: false,
                y: { formatter: function (v) { return v.toFixed(3) + ' t'; } } }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HC-B: Barras horizontales emisiones por SCRAP
    // scraps: [{ scrapName, co2eTonnes, co2ePerTonne }]
    // ─────────────────────────────────────────────────────────────────────────
    function renderScrapBars(containerId, scraps) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !scraps || scraps.length === 0) return;

        var names  = scraps.map(function (s) { return s.scrapName || '—'; });
        var values = scraps.map(function (s) { return +(s.co2eTonnes).toFixed(3); });

        var options = {
            chart: { type: 'bar', height: Math.max(220, names.length * 36 + 60),
                     toolbar: { show: false }, fontFamily: 'inherit' },
            series: [{ name: 't CO₂e', data: values }],
            xaxis: { categories: names, labels: { style: { fontSize: '11px' } } },
            yaxis: { title: { text: 't CO₂e' } },
            colors: ['#0d6efd'],
            plotOptions: { bar: { horizontal: true, borderRadius: 4, barHeight: '60%' } },
            dataLabels: { enabled: true, formatter: function (v) { return v.toFixed(2) + ' t'; },
                          style: { fontSize: '11px' } },
            grid: { borderColor: '#f0f0f0' },
            tooltip: { y: { formatter: function (v) { return v.toFixed(3) + ' t CO₂e'; } } }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HC-C: Barras verticales comparativa kWh entre plantas
    // plants: [{ plantName, kwhTotal, scope2CO2eKg, co2ePerTonneKg }]
    // ─────────────────────────────────────────────────────────────────────────
    function renderPlantComparison(containerId, plants) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !plants || plants.length === 0) return;

        var names = plants.map(function (p) {
            return p.plantName && p.plantName.length > 18 ? p.plantName.substring(0, 16) + '…' : (p.plantName || '—');
        });
        var kwh   = plants.map(function (p) { return +(p.kwhTotal).toFixed(0); });
        var co2   = plants.map(function (p) { return +(p.scope2CO2eKg / 1000).toFixed(3); }); // → t

        var options = {
            chart: { type: 'bar', height: 280, toolbar: { show: false }, fontFamily: 'inherit' },
            series: [
                { name: 'kWh', type: 'column', data: kwh },
                { name: 'CO₂e Scope 2 (t)', type: 'line', data: co2 }
            ],
            xaxis: { categories: names, labels: { rotate: -30, style: { fontSize: '11px' } } },
            yaxis: [
                { seriesName: 'kWh', title: { text: 'kWh' },
                  labels: { formatter: function (v) { return Math.round(v).toLocaleString('es-ES'); } } },
                { seriesName: 'CO₂e Scope 2 (t)', opposite: true, title: { text: 't CO₂e' },
                  labels: { formatter: function (v) { return v.toFixed(2); } } }
            ],
            colors: ['#ffc107', '#dc3545'],
            stroke: { curve: 'smooth', width: [0, 3] },
            plotOptions: { bar: { borderRadius: 4, columnWidth: '55%' } },
            dataLabels: { enabled: false },
            grid: { borderColor: '#f0f0f0' },
            legend: { position: 'top' },
            tooltip: { shared: true, intersect: false }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HC-C: Donut por fuente energética (kWh)
    // sources: [{ source, kwhTotal, pct }]
    // ─────────────────────────────────────────────────────────────────────────
    function renderSourceDonut(containerId, sources) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !sources || sources.length === 0) return;

        var labels = sources.map(function (s) { return s.source || 'Desconocida'; });
        var values = sources.map(function (s) { return +(s.kwhTotal).toFixed(0); });
        var colors = ['#ffc107','#198754','#0d6efd','#fd7e14','#6f42c1','#20c997'];

        var options = {
            chart: { type: 'donut', height: 280, fontFamily: 'inherit' },
            series: values,
            labels: labels,
            colors: colors.slice(0, labels.length),
            legend: { position: 'bottom', fontSize: '12px' },
            dataLabels: { enabled: true, formatter: function (val) { return val.toFixed(1) + '%'; } },
            plotOptions: { pie: { donut: { size: '65%', labels: {
                show: true,
                total: { show: true, label: 'Total kWh', formatter: function (w) {
                    return w.globals.seriesTotals.reduce(function (a, b) { return a + b; }, 0).toLocaleString('es-ES');
                }}
            }}}},
            tooltip: { y: { formatter: function (v) { return Math.round(v).toLocaleString('es-ES') + ' kWh'; } } }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HC-D: Mixto barras (CO₂e kg) + línea (intensidad kg/t) mensual
    // months: [{ year, month, co2eKg, intensityKgPerTonne }]
    // ─────────────────────────────────────────────────────────────────────────
    function renderProducerMonthly(containerId, months) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !months || months.length === 0) return;

        var labels   = months.map(function (m) { return MONTH_LABELS[m.month - 1] + ' ' + m.year; });
        var co2Data  = months.map(function (m) { return +(m.co2eKg / 1000).toFixed(3); }); // → t
        var intData  = months.map(function (m) { return +(m.intensityKgPerTonne).toFixed(2); });

        var options = {
            chart: { type: 'bar', height: 280, toolbar: { show: false }, fontFamily: 'inherit' },
            series: [
                { name: 't CO₂e', type: 'column', data: co2Data },
                { name: 'Intensidad (kg/t)', type: 'line', data: intData }
            ],
            xaxis: { categories: labels, labels: { rotate: -30, style: { fontSize: '11px' } } },
            yaxis: [
                { seriesName: 't CO₂e', title: { text: 't CO₂e' },
                  labels: { formatter: function (v) { return v.toFixed(2); } } },
                { seriesName: 'Intensidad (kg/t)', opposite: true, title: { text: 'kg/t' },
                  labels: { formatter: function (v) { return v.toFixed(1); } } }
            ],
            colors: ['#0d6efd', '#fd7e14'],
            stroke: { curve: 'smooth', width: [0, 3] },
            plotOptions: { bar: { borderRadius: 4, columnWidth: '55%' } },
            dataLabels: { enabled: false },
            grid: { borderColor: '#f0f0f0' },
            legend: { position: 'top' },
            tooltip: { shared: true, intersect: false,
                y: [{ formatter: function (v) { return v.toFixed(3) + ' t CO₂e'; } },
                    { formatter: function (v) { return v.toFixed(1) + ' kg/t'; } }] }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HC-D: Donut por código LER
    // lers: [{ lerCode, lerDescription, co2eKg }]
    // ─────────────────────────────────────────────────────────────────────────
    function renderLerDonut(containerId, lers) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !lers || lers.length === 0) return;

        var labels  = lers.map(function (l) { return l.lerCode || '—'; });
        var values  = lers.map(function (l) { return +(l.co2eKg / 1000).toFixed(3); }); // → t
        var colors  = ['#0d6efd','#198754','#fd7e14','#6f42c1','#20c997','#dc3545','#ffc107','#0dcaf0'];

        var options = {
            chart: { type: 'donut', height: 280, fontFamily: 'inherit' },
            series: values,
            labels: labels,
            colors: colors.slice(0, labels.length),
            legend: { position: 'bottom', fontSize: '11px' },
            dataLabels: { enabled: true, formatter: function (val) { return val.toFixed(1) + '%'; } },
            plotOptions: { pie: { donut: { size: '65%', labels: {
                show: true,
                total: { show: true, label: 'Total (t)', formatter: function (w) {
                    return w.globals.seriesTotals.reduce(function (a, b) { return a + b; }, 0).toFixed(2) + ' t';
                }}
            }}}},
            tooltip: { y: { formatter: function (v, opts) {
                var desc = lers[opts.seriesIndex] ? lers[opts.seriesIndex].lerDescription : '';
                return v.toFixed(3) + ' t CO₂e' + (desc ? ' — ' + desc : '');
            }}}
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Limpieza genérica por id
    // ─────────────────────────────────────────────────────────────────────────
    function clear(id) {
        destroy(id);
        var target = el(id);
        if (target) target.innerHTML = '';
    }

    return {
        renderMonthlyEvolution:   renderMonthlyEvolution,
        renderFuelDonut:          renderFuelDonut,
        renderMonthlyFuelStacked: renderMonthlyFuelStacked,
        renderScrapBars:          renderScrapBars,
        renderPlantComparison:    renderPlantComparison,
        renderSourceDonut:        renderSourceDonut,
        renderProducerMonthly:    renderProducerMonthly,
        renderLerDonut:           renderLerDonut,
        clear:                    clear
    };

})();
