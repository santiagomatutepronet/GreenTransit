// leaflet-interop.js
// Interop JavaScript para el componente DynamicMap.razor usando Leaflet.js

window.leafletInterop = (function () {
    const maps = {};

    /**
     * Inicializa un mapa Leaflet en el contenedor indicado.
     * @param {string} containerId - Id del elemento HTML donde se renderiza el mapa.
     * @param {number} centerLat - Latitud del centro inicial.
     * @param {number} centerLon - Longitud del centro inicial.
     * @param {number} zoom - Nivel de zoom inicial.
     */
    function initializeMap(containerId, centerLat, centerLon, zoom) {
        if (maps[containerId]) {
            maps[containerId].remove();
            delete maps[containerId];
        }

        const container = document.getElementById(containerId);
        if (!container) return;

        const map = L.map(containerId).setView([centerLat, centerLon], zoom);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
            maxZoom: 19
        }).addTo(map);

        maps[containerId] = map;
    }

    /**
     * Añade marcadores al mapa.
     * @param {string} containerId - Id del mapa.
     * @param {Array} markersData - Array de objetos { lat, lon, title, tooltipHtml }.
     */
    function addMarkers(containerId, markersData) {
        const map = maps[containerId];
        if (!map || !markersData || markersData.length === 0) return;

        const bounds = [];

        markersData.forEach(function (m) {
            if (m.lat == null || m.lon == null) return;

            const marker = L.marker([m.lat, m.lon]).addTo(map);

            if (m.tooltipHtml) {
                marker.bindPopup(m.tooltipHtml);
            } else if (m.title) {
                marker.bindPopup('<strong>' + escapeHtml(m.title) + '</strong>');
            }

            bounds.push([m.lat, m.lon]);
        });

        if (bounds.length > 0) {
            try {
                map.fitBounds(bounds, { padding: [30, 30] });
            } catch (e) {
                // Si solo hay 1 punto, fitBounds puede fallar con un punto
                if (bounds.length === 1) {
                    map.setView(bounds[0], 12);
                }
            }
        }
    }

    /**
     * Destruye el mapa y libera recursos.
     * @param {string} containerId
     */
    function destroyMap(containerId) {
        if (maps[containerId]) {
            maps[containerId].remove();
            delete maps[containerId];
        }
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.appendChild(document.createTextNode(text));
        return div.innerHTML;
    }

    return {
        initializeMap,
        addMarkers,
        destroyMap
    };
})();
