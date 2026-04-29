// wasteMoveMap.js — Leaflet interop para la vista 360º del traslado

window.wasteMoveMap = {
    _map: null,

    /**
     * Inicializa el mapa con los puntos de origen y destino.
     * @param {string} containerId  - id del div contenedor
     * @param {number} srcLat       - latitud origen
     * @param {number} srcLng       - longitud origen
     * @param {string} srcLabel     - nombre del punto origen
     * @param {number} dstLat       - latitud destino
     * @param {number} dstLng       - longitud destino
     * @param {string} dstLabel     - nombre del punto destino
     */
    init(containerId, srcLat, srcLng, srcLabel, dstLat, dstLng, dstLabel) {
        if (this._map) {
            this._map.remove();
            this._map = null;
        }

        const points = [];

        this._map = L.map(containerId, { zoomControl: true });

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
            maxZoom: 18
        }).addTo(this._map);

        const greenIcon = L.divIcon({
            html: '<i class="bi bi-geo-alt-fill" style="color:#198754;font-size:1.6rem"></i>',
            className: '',
            iconAnchor: [10, 28]
        });

        const redIcon = L.divIcon({
            html: '<i class="bi bi-geo-alt-fill" style="color:#dc3545;font-size:1.6rem"></i>',
            className: '',
            iconAnchor: [10, 28]
        });

        if (srcLat && srcLng) {
            const srcMarker = L.marker([srcLat, srcLng], { icon: greenIcon })
                .addTo(this._map)
                .bindPopup(`<strong>Origen</strong><br>${srcLabel ?? ''}`);
            points.push([srcLat, srcLng]);
        }

        if (dstLat && dstLng) {
            const dstMarker = L.marker([dstLat, dstLng], { icon: redIcon })
                .addTo(this._map)
                .bindPopup(`<strong>Destino</strong><br>${dstLabel ?? ''}`);
            points.push([dstLat, dstLng]);
        }

        if (points.length === 2) {
            // Línea de ruta entre los dos puntos
            L.polyline(points, { color: '#0d6efd', weight: 3, dashArray: '6 4' })
                .addTo(this._map);
            this._map.fitBounds(points, { padding: [40, 40] });
        } else if (points.length === 1) {
            this._map.setView(points[0], 12);
        } else {
            // Sin coordenadas: centrar en España por defecto
            this._map.setView([40.4, -3.7], 6);
        }
    },

    dispose() {
        if (this._map) {
            this._map.remove();
            this._map = null;
        }
    }
};
