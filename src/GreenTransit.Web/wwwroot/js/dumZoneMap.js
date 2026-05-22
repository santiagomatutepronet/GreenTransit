// dumZoneMap.js — Leaflet interop para editor visual de Zonas DUM

window.dumZoneMap = {
    _map: null,
    _layerGroup: null,
    _drawLayer: null,
    _drawControl: null,
    _dotNetRef: null,

    /**
     * Inicializa el mapa con los polígonos de las zonas DUM.
     * @param {string} containerId - id del div contenedor
     * @param {Array}  zones       - array de { id, zoneCode, geometryJson, actionColor, popupHtml }
     * @param {object} dotNetRef   - referencia .NET para callbacks
     */
    init(containerId, zones, dotNetRef) {
        if (this._map) {
            this._map.remove();
            this._map = null;
            this._layerGroup = null;
            this._drawLayer = null;
            this._drawControl = null;
        }

        this._dotNetRef = dotNetRef;

        this._map = L.map(containerId, { zoomControl: true });
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
            maxZoom: 19
        }).addTo(this._map);

        this._layerGroup = L.layerGroup().addTo(this._map);
        this._zoneLayerMap = {};

        const allBounds = [];

        (zones || []).forEach(z => {
            try {
                const geoJson = typeof z.geometryJson === 'string'
                    ? JSON.parse(z.geometryJson)
                    : z.geometryJson;

                const layer = L.geoJSON(geoJson, {
                    style: {
                        color: z.actionColor,
                        fillColor: z.actionColor,
                        fillOpacity: 0.25,
                        weight: 2
                    }
                }).bindPopup(z.popupHtml);

                layer.on('click', () => {
                    if (this._dotNetRef) {
                        this._dotNetRef.invokeMethodAsync('OnZoneClickedFromMap', z.id);
                    }
                });

                layer.addTo(this._layerGroup);
                this._zoneLayerMap[z.id] = layer;

                const bounds = layer.getBounds();
                if (bounds.isValid()) allBounds.push(bounds);
            } catch (e) {
                console.warn('dumZoneMap: error renderizando zona', z.zoneCode, e);
            }
        });

        if (allBounds.length > 0) {
            const combined = allBounds.reduce((acc, b) => acc.extend(b));
            this._map.fitBounds(combined, { padding: [30, 30] });
        } else {
            // España por defecto
            this._map.setView([40.416, -3.703], 6);
        }
    },

    /**
     * Hace zoom al polígono de la zona indicada.
     * @param {string} zoneId
     */
    flyToZone(zoneId) {
        const layer = this._zoneLayerMap && this._zoneLayerMap[zoneId];
        if (!layer) return;
        const bounds = layer.getBounds();
        if (bounds.isValid()) {
            this._map.flyToBounds(bounds, { padding: [40, 40], duration: 0.8 });
            layer.openPopup();
        }
    },

    /**
     * Activa el modo dibujo de polígono usando la API nativa de Leaflet.
     * - Clic: añade vértice
     * - Doble clic: cierra el polígono
     * - Clic sobre el primer vértice: cierra el polígono
     * Al completar el polígono, invoca OnPolygonDrawn con el GeoJSON.
     */
    startDrawing() {
        if (!this._map) {
            console.error('dumZoneMap.startDrawing: mapa no inicializado');
            return;
        }

        this._cancelDrawing();   // limpia estado anterior si hubiera

        // Asegurar que el mapa ocupa correctamente el contenedor (puede haber cambiado
        // al cerrar el modal que estaba superpuesto)
        this._map.invalidateSize();

        // Desactivar el zoom por doble clic para que el dblclick cierre el polígono
        this._map.doubleClickZoom.disable();

        const vertices  = [];
        const markers   = [];
        let   previewLine    = null;
        let   previewPolygon = null;

        this._map.getContainer().style.cursor = 'crosshair';

        const cleanup = () => {
            this._map.off('click',     onMapClick);
            this._map.off('dblclick',  onMapDblClick);
            this._map.off('mousemove', onMouseMove);
            this._map.doubleClickZoom.enable();
            this._map.getContainer().style.cursor = '';
            if (previewLine)    { this._map.removeLayer(previewLine);    previewLine    = null; }
            if (previewPolygon) { this._map.removeLayer(previewPolygon); previewPolygon = null; }
            markers.forEach(m => this._map.removeLayer(m));
            markers.length   = 0;
            vertices.length  = 0;
            this._drawCleanup = null;
        };
        this._drawCleanup = cleanup;

        const finishPolygon = () => {
            if (vertices.length < 3) return;
            cleanup();

            const coords = vertices.map(v => [v.lng, v.lat]);
            coords.push(coords[0]);   // cerrar el anillo
            const geoJson = { type: 'Polygon', coordinates: [coords] };

            if (this._dotNetRef) {
                this._dotNetRef.invokeMethodAsync('OnPolygonDrawn', JSON.stringify(geoJson));
            }
        };

        const onMouseMove = (e) => {
            if (vertices.length === 0) return;
            const pts = [...vertices.map(v => [v.lat, v.lng]), [e.latlng.lat, e.latlng.lng]];
            if (previewLine)    previewLine.setLatLngs(pts);
            else                previewLine = L.polyline(pts, { color: '#0d6efd', dashArray: '6,4', weight: 2 }).addTo(this._map);
            if (vertices.length >= 2) {
                const polyPts = [...pts, [vertices[0].lat, vertices[0].lng]];
                if (previewPolygon) previewPolygon.setLatLngs(polyPts);
                else                previewPolygon = L.polygon(polyPts, { color: '#0d6efd', fillOpacity: 0.1, weight: 2 }).addTo(this._map);
            }
        };

        const onMapClick = (e) => {
            L.DomEvent.stopPropagation(e);
            vertices.push(e.latlng);
            const isFirst = vertices.length === 1;
            const marker  = L.circleMarker(e.latlng, {
                radius: isFirst ? 7 : 5,
                color: '#0d6efd', fillColor: isFirst ? '#0d6efd' : '#fff',
                fillOpacity: 1, weight: 2
            }).addTo(this._map);
            if (isFirst) marker.on('click', (ev) => { L.DomEvent.stopPropagation(ev); finishPolygon(); });
            markers.push(marker);
        };

        const onMapDblClick = (e) => {
            L.DomEvent.stopPropagation(e);
            if (vertices.length > 0) vertices.pop();   // el dblclick ya contó un clic extra
            if (markers.length > 0)  { this._map.removeLayer(markers.pop()); }
            finishPolygon();
        };

        this._map.on('click',     onMapClick);
        this._map.on('dblclick',  onMapDblClick);
        this._map.on('mousemove', onMouseMove);
    },

    /** Cancela el modo dibujo en curso sin invocar callback. */
    _cancelDrawing() {
        if (this._drawCleanup) {
            this._drawCleanup();
            this._drawCleanup = null;
        }
    },

    /** Expuesto para llamada desde Blazor (botón Cancelar). */
    cancelDrawing() {
        this._cancelDrawing();
    },

    /**
     * Añade o actualiza un único polígono en el mapa (usado tras crear una zona nueva).
     */
    addZone(zone) {
        if (!this._map || !this._layerGroup) return;
        try {
            const geoJson = typeof zone.geometryJson === 'string'
                ? JSON.parse(zone.geometryJson)
                : zone.geometryJson;

            const layer = L.geoJSON(geoJson, {
                style: {
                    color: zone.actionColor,
                    fillColor: zone.actionColor,
                    fillOpacity: 0.25,
                    weight: 2
                }
            }).bindPopup(zone.popupHtml);

            layer.on('click', () => {
                if (this._dotNetRef) {
                    this._dotNetRef.invokeMethodAsync('OnZoneClickedFromMap', zone.id);
                }
            });

            layer.addTo(this._layerGroup);
            if (!this._zoneLayerMap) this._zoneLayerMap = {};
            this._zoneLayerMap[zone.id] = layer;
        } catch (e) {
            console.warn('dumZoneMap: error añadiendo zona', e);
        }
    },

    dispose() {
        this._cancelDrawing();
        if (this._map) {
            this._map.remove();
            this._map = null;
        }
        this._layerGroup  = null;
        this._drawLayer   = null;
        this._drawControl = null;
        this._dotNetRef   = null;
        this._zoneLayerMap = {};
    }
};
