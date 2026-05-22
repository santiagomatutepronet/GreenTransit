// complianceCharts.js — Gráficos ApexCharts para los dashboards de Cumplimiento Normativo (CN)
// Requiere: ApexCharts

window.complianceCharts = (function () {

    var _charts = {};

    function destroy(id) {
        if (_charts[id]) {
            try { _charts[id].destroy(); } catch (e) { /* ignorar */ }
            delete _charts[id];
        }
    }

    function el(id) { return document.getElementById(id); }

    var MONTH_LABELS = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun',
                        'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];

    var STATUS_COLORS = { GREEN: '#198754', ORANGE: '#fd7e14', RED: '#dc3545' };

    // ── CN-A: Evolución trimestral (line chart multi-serie) ───────────────────
    // series: [{ year, quarter, recyclingPct, valorizationPct, reusePct, targetPct }]
    function renderQuarterlyTrend(containerId, series) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !series || series.length === 0) return;

        var labels = series.map(function (s) { return 'T' + s.quarter + ' ' + s.year; });

        function safeNum(v) { var n = parseFloat(v); return isFinite(n) ? +n.toFixed(1) : 0; }

        var options = {
            chart: { type: 'line', height: 280, toolbar: { show: false }, fontFamily: 'inherit' },
            series: [
                { name: 'Reciclaje %',       data: series.map(function (s) { return safeNum(s.recyclingPct); }) },
                { name: 'Valorización %',    data: series.map(function (s) { return safeNum(s.valorizationPct); }) },
                { name: 'Reutilización %',   data: series.map(function (s) { return safeNum(s.reusePct); }) },
                { name: 'Objetivo Reciclaje', data: series.map(function (s) { return safeNum(s.targetPct); }), dashArray: 6 }
            ],
            xaxis: { categories: labels },
            yaxis: { title: { text: '%' }, min: 0, max: 100 },
            colors: ['#198754', '#fd7e14', '#0d6efd', '#dc3545'],
            stroke: { width: [2, 2, 2, 1], curve: 'smooth', dashArray: [0, 0, 0, 6] },
            dataLabels: { enabled: false },
            legend: { position: 'top' },
            grid: { borderColor: '#f0f0f0' },
            tooltip: { y: { formatter: function (v) { return v.toFixed(1) + '%'; } } }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ── CN-A: Liquidaciones mensuales (bar apilado) ────────────────────────────
    // series: [{ year, month, pending, approved, rejected }]
    function renderSettlementBar(containerId, series) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !series || series.length === 0) return;

        var labels = series.map(function (s) { return MONTH_LABELS[s.month - 1] + ' ' + s.year; });

        var options = {
            chart: { type: 'bar', stacked: true, height: 280, toolbar: { show: false }, fontFamily: 'inherit' },
            series: [
                { name: 'Pendiente', data: series.map(function (s) { return +s.pending.toFixed(2); }) },
                { name: 'Aprobada',  data: series.map(function (s) { return +s.approved.toFixed(2); }) },
                { name: 'Rechazada', data: series.map(function (s) { return +s.rejected.toFixed(2); }) }
            ],
            xaxis: { categories: labels, labels: { rotate: -30, style: { fontSize: '11px' } } },
            yaxis: { labels: { formatter: function (v) { return v.toFixed(0) + ' €'; } } },
            colors: ['#fd7e14', '#198754', '#dc3545'],
            dataLabels: { enabled: false },
            legend: { position: 'top' },
            grid: { borderColor: '#f0f0f0' }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ── CN-B: Donut de proporción de SCRAPs ────────────────────────────────────
    // slices: [{ name, value }]
    function renderScrapDonut(containerId, slices) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !slices || slices.length === 0) return;

        var options = {
            chart: { type: 'donut', height: 280, fontFamily: 'inherit' },
            series: slices.map(function (s) { return +s.value.toFixed(1); }),
            labels: slices.map(function (s) { return s.name; }),
            colors: ['#2d6a4f', '#40916c', '#52b788', '#74c69d', '#95d5b2', '#b7e4c7', '#d8f3dc'],
            dataLabels: { enabled: true, formatter: function (v) { return v.toFixed(1) + '%'; } },
            legend: { position: 'bottom' },
            plotOptions: { pie: { donut: { size: '60%' } } },
            tooltip: { y: { formatter: function (v) { return v.toFixed(1) + '%'; } } }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ── CN-B: Bar horizontal de desviación ─────────────────────────────────────
    // data: [{ name, value, color }]
    function renderDeviationBar(containerId, data) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !data || data.length === 0) return;

        var options = {
            chart: { type: 'bar', height: Math.max(200, data.length * 40), toolbar: { show: false }, fontFamily: 'inherit' },
            series: [{ name: 'Desviación %', data: data.map(function (d) { return +d.value.toFixed(1); }) }],
            xaxis: { categories: data.map(function (d) { return d.name; }) },
            yaxis: { title: { text: 'Desviación %' } },
            plotOptions: { bar: { horizontal: true, colors: {
                ranges: [
                    { from: -999, to: -0.001, color: '#dc3545' },
                    { from: 0,    to:  999,   color: '#198754' }
                ]
            }}},
            dataLabels: { enabled: true, formatter: function (v) { return v.toFixed(1) + '%'; } },
            annotations: { xaxis: [{ x: 0, strokeDashArray: 0, borderColor: '#333' }] },
            grid: { borderColor: '#f0f0f0' }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ── CN-B: Stacked area mensual por SCRAP ───────────────────────────────────
    // data: [{ year, month, scrap, realKg, targetKg }]
    // scraps: string[]
    function renderMonthlyStackedArea(containerId, data, scraps) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !data || data.length === 0) return;

        var months = data.filter(function (d) { return d.scrap === scraps[0]; })
                         .map(function (d) { return MONTH_LABELS[d.month - 1] + ' ' + d.year; });

        var series = scraps.map(function (scrap) {
            return {
                name: scrap,
                data: data.filter(function (d) { return d.scrap === scrap; })
                          .map(function (d) { return +d.realKg.toFixed(0); })
            };
        });

        var options = {
            chart: { type: 'area', stacked: true, height: 280, toolbar: { show: false }, fontFamily: 'inherit' },
            series: series,
            xaxis: { categories: months, labels: { rotate: -30, style: { fontSize: '11px' } } },
            yaxis: { title: { text: 'kg' } },
            stroke: { curve: 'smooth', width: 2 },
            fill: { type: 'gradient', opacity: 0.3 },
            dataLabels: { enabled: false },
            legend: { position: 'top' },
            grid: { borderColor: '#f0f0f0' }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ── CN-C: Bar agrupado de cobertura por CCAA ───────────────────────────────
    // data: [{ region, scrap, agreements, tonnes }]
    function renderCoverageGroupedBar(containerId, data) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !data || data.length === 0) return;

        var regions = Array.from(new Set(data.map(function (d) { return d.region; }))).sort();
        var scraps  = Array.from(new Set(data.map(function (d) { return d.scrap; }))).sort();

        var series = scraps.map(function (scrap) {
            return {
                name: scrap,
                data: regions.map(function (region) {
                    var item = data.find(function (d) { return d.region === region && d.scrap === scrap; });
                    return item ? item.agreements : 0;
                })
            };
        });

        var options = {
            chart: { type: 'bar', height: 280, toolbar: { show: false }, fontFamily: 'inherit' },
            series: series,
            xaxis: { categories: regions, labels: { rotate: -30, style: { fontSize: '11px' } } },
            yaxis: { title: { text: 'Nº Convenios' } },
            plotOptions: { bar: { columnWidth: '70%', grouped: true } },
            dataLabels: { enabled: false },
            legend: { position: 'top' },
            grid: { borderColor: '#f0f0f0' }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ── CN-C: Line chart de liquidaciones por SCRAP ────────────────────────────
    // data: [{ year, month, scrap, amount }]
    // scraps: string[]
    function renderSettlementLineByScrap(containerId, data, scraps) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !data || data.length === 0) return;

        var months = data.filter(function (d) { return d.scrap === scraps[0]; })
                         .map(function (d) { return MONTH_LABELS[d.month - 1] + ' ' + d.year; });

        var series = scraps.map(function (scrap) {
            return {
                name: scrap,
                data: data.filter(function (d) { return d.scrap === scrap; })
                          .map(function (d) { return +d.amount.toFixed(2); })
            };
        });

        var options = {
            chart: { type: 'line', height: 280, toolbar: { show: false }, fontFamily: 'inherit' },
            series: series,
            xaxis: { categories: months, labels: { rotate: -30, style: { fontSize: '11px' } } },
            yaxis: { title: { text: '€' }, labels: { formatter: function (v) { return v.toFixed(0); } } },
            stroke: { curve: 'smooth', width: 2 },
            dataLabels: { enabled: false },
            legend: { position: 'top' },
            grid: { borderColor: '#f0f0f0' }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ── CN-D: Stacked bar mensual por SCRAP ────────────────────────────────────
    // data: [{ year, month, scrap, kg, targetKg }]
    // scraps: string[]
    function renderMonthlyStackedBar(containerId, data, scraps) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !data || data.length === 0) return;

        var months = data.filter(function (d) { return d.scrap === scraps[0]; })
                         .map(function (d) { return MONTH_LABELS[d.month - 1] + ' ' + d.year; });

        var series = scraps.map(function (scrap) {
            return {
                name: scrap,
                data: data.filter(function (d) { return d.scrap === scrap; })
                          .map(function (d) { return +d.kg.toFixed(0); })
            };
        });

        var options = {
            chart: { type: 'bar', stacked: true, height: 280, toolbar: { show: false }, fontFamily: 'inherit' },
            series: series,
            xaxis: { categories: months, labels: { rotate: -30, style: { fontSize: '11px' } } },
            yaxis: { title: { text: 'kg' } },
            dataLabels: { enabled: false },
            legend: { position: 'top' },
            grid: { borderColor: '#f0f0f0' }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ── CN-E: Bar horizontal ranking SCRAPs ────────────────────────────────────
    // data: [{ name, value, status }]
    function renderRankingBar(containerId, data) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !data || data.length === 0) return;

        var sorted = data.slice().sort(function (a, b) { return a.value - b.value; });

        var options = {
            chart: { type: 'bar', height: Math.max(200, sorted.length * 45), toolbar: { show: false }, fontFamily: 'inherit' },
            series: [{ name: '% Cumplimiento', data: sorted.map(function (d) { return +d.value.toFixed(1); }) }],
            xaxis: { categories: sorted.map(function (d) { return d.name; }) },
            yaxis: { min: 0, max: 120, title: { text: '% Cumplimiento' } },
            plotOptions: {
                bar: {
                    horizontal: true,
                    colors: {
                        ranges: [
                            { from:   0, to:  79.99, color: '#dc3545' },
                            { from:  80, to:  99.99, color: '#fd7e14' },
                            { from: 100, to: 120,    color: '#198754' }
                        ]
                    }
                }
            },
            annotations: { xaxis: [{ x: 100, strokeDashArray: 4, borderColor: '#0d6efd', label: { text: 'Objetivo 100%' } }] },
            dataLabels: { enabled: true, formatter: function (v) { return v.toFixed(1) + '%'; } },
            grid: { borderColor: '#f0f0f0' }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ── CN-E: Line chart interanual ────────────────────────────────────────────
    // series: [{ year, recyclingPct, valorizationPct, reusePct, targetPct }]
    function renderInterannualTrend(containerId, series) {
        destroy(containerId);
        var target = el(containerId);
        if (!target || !series || series.length === 0) return;

        var labels = series.map(function (s) { return s.year.toString(); });

        var options = {
            chart: { type: 'line', height: 280, toolbar: { show: false }, fontFamily: 'inherit' },
            series: [
                { name: 'Reciclaje %',    data: series.map(function (s) { return +s.recyclingPct.toFixed(1); }) },
                { name: 'Valorización %', data: series.map(function (s) { return +s.valorizationPct.toFixed(1); }) },
                { name: 'Reutilización %', data: series.map(function (s) { return +s.reusePct.toFixed(1); }) },
                { name: 'Objetivo Reciclaje', data: series.map(function (s) { return +s.targetPct.toFixed(1); }), dashArray: 6 }
            ],
            xaxis: { categories: labels },
            yaxis: { title: { text: '%' }, min: 0, max: 100 },
            colors: ['#198754', '#fd7e14', '#0d6efd', '#dc3545'],
            stroke: { width: [2, 2, 2, 1], curve: 'smooth', dashArray: [0, 0, 0, 6] },
            dataLabels: { enabled: false },
            markers: { size: 4 },
            legend: { position: 'top' },
            grid: { borderColor: '#f0f0f0' },
            tooltip: { y: { formatter: function (v) { return v.toFixed(1) + '%'; } } }
        };

        var chart = new ApexCharts(target, options);
        chart.render();
        _charts[containerId] = chart;
    }

    // ── clear ──────────────────────────────────────────────────────────────────
    function clear(id) { destroy(id); }

    return {
        renderQuarterlyTrend:        renderQuarterlyTrend,
        renderSettlementBar:         renderSettlementBar,
        renderScrapDonut:            renderScrapDonut,
        renderDeviationBar:          renderDeviationBar,
        renderMonthlyStackedArea:    renderMonthlyStackedArea,
        renderCoverageGroupedBar:    renderCoverageGroupedBar,
        renderSettlementLineByScrap: renderSettlementLineByScrap,
        renderMonthlyStackedBar:     renderMonthlyStackedBar,
        renderRankingBar:            renderRankingBar,
        renderInterannualTrend:      renderInterannualTrend,
        clear:                       clear
    };

})();
