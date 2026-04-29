// dashboardMap.js — Leaflet interop para el mapa del Dashboard operativo

window.dashboardMap = {
    _map: null,
    _observer: null,

    /**
     * Registra un IntersectionObserver sobre el contenedor del mapa.
     * El mapa solo se inicializa cuando el elemento entra en el viewport (lazy load).
     * @param {string}   containerId  - id del div contenedor del mapa
     * @param {Array}    points       - [{ name, role, lat, lng }, ...]
     * @param {Array}    dumZones     - [{ zoneCode, name, geometryJson }, ...]
     */
    initLazy(containerId, points, dumZones) {
        const el = document.getElementById(containerId);
        if (!el) return;

        // Si ya hay un mapa activo en este contenedor, destruirlo primero
        if (this._map) {
            this._map.remove();
            this._map = null;
        }

        // Desconectar observer anterior
        if (this._observer) {
            this._observer.disconnect();
            this._observer = null;
        }

        const self = this;
        this._observer = new IntersectionObserver((entries) => {
            if (entries[0].isIntersecting) {
                self._observer.disconnect();
                self._observer = null;
                self._initMap(containerId, points, dumZones);
            }
        }, { threshold: 0.1 });

        this._observer.observe(el);
    },

    /**
     * Inicializa el mapa Leaflet con los puntos de entidades y polígonos DUM.
     */
    _initMap(containerId, points, dumZones) {
        const el = document.getElementById(containerId);
        if (!el) return;

        // Centro por defecto: España
        const defaultCenter = [40.4165, -3.7026];
        const defaultZoom   = 6;

        this._map = L.map(containerId, { zoomControl: true });

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
            maxZoom: 18
        }).addTo(this._map);

        // ── Paleta de colores por EntityRole ──────────────────────────────────
        const roleColors = {
            'Producer'        : '#198754',
            'Plant'           : '#dc3545',
            'CAC'             : '#fd7e14',
            'Carrier'         : '#0d6efd',
            'SCRAP'           : '#6f42c1',
            'PublicEntity'    : '#0dcaf0',
            'OperatorTransfer': '#ffc107',
            'Coordinator'     : '#20c997',
        };

        const markerBounds = [];

        // ── Puntos de entidades ───────────────────────────────────────────────
        if (points && points.length > 0) {
            const layerGroup = L.layerGroup().addTo(this._map);

            points.forEach(pt => {
                if (!pt.lat || !pt.lng) return;
                const color = roleColors[pt.role] ?? '#6c757d';
                const icon = L.divIcon({
                    html: `<i class="bi bi-geo-alt-fill" style="color:${color};font-size:1.4rem;line-height:1"></i>`,
                    className: '',
                    iconSize: [20, 28],
                    iconAnchor: [10, 28],
                    popupAnchor: [0, -28]
                });

                L.marker([pt.lat, pt.lng], { icon })
                    .addTo(layerGroup)
                    .bindPopup(
                        `<strong>${pt.name}</strong><br>` +
                        `<span class="badge" style="background:${color}">${pt.role}</span>`
                    );

                markerBounds.push([pt.lat, pt.lng]);
            });
        }

        // ── Polígonos DUM ─────────────────────────────────────────────────────
        if (dumZones && dumZones.length > 0) {
            dumZones.forEach(zone => {
                try {
                    const geojson = typeof zone.geometryJson === 'string'
                        ? JSON.parse(zone.geometryJson)
                        : zone.geometryJson;

                    L.geoJSON(geojson, {
                        style: {
                            color       : '#ff6600',
                            weight      : 2,
                            opacity     : 0.8,
                            fillColor   : '#ff6600',
                            fillOpacity : 0.15
                        }
                    })
                    .bindPopup(`<strong>Zona DUM: ${zone.name}</strong><br>${zone.zoneCode}`)
                    .addTo(this._map);
                } catch (e) {
                    console.warn('[dashboardMap] Error al parsear GeoJSON de zona DUM:', zone.zoneCode, e);
                }
            });
        }

        // ── Ajustar vista ─────────────────────────────────────────────────────
        if (markerBounds.length > 0) {
            try {
                this._map.fitBounds(L.latLngBounds(markerBounds), { padding: [30, 30], maxZoom: 12 });
            } catch {
                this._map.setView(defaultCenter, defaultZoom);
            }
        } else {
            this._map.setView(defaultCenter, defaultZoom);
        }
    },

    /**
     * Destruye el mapa y libera recursos (para llamar desde Dispose de Blazor).
     */
    dispose(containerId) {
        if (this._observer) {
            this._observer.disconnect();
            this._observer = null;
        }
        if (this._map) {
            this._map.remove();
            this._map = null;
        }
    }
};
