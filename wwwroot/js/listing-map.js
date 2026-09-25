/*
 * Listing proximity + map view (DIS-02) for /Equipment and /Godown.
 * - "Within N km" of the user's own position (browser geolocation, rounded to ~100 m) or of the chosen district.
 * - A list/map toggle kept in the URL (?view=map) so the view can be shared and survives reload.
 * - Leaflet + OpenStreetMap tiles are fetched only when the map is first opened, which matters on 2G.
 * The page's own filter code calls KrishiListingGeo.appendParams / fetchParams / render; everything else is bound here.
 */
(function () {
    'use strict';

    const root = document.getElementById('listingGeoControls');
    if (!root) return;

    const LEAFLET_CSS = { href: 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css', integrity: 'sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=' };
    const LEAFLET_JS = { src: 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js', integrity: 'sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo=' };
    const MAP_PAGE_SIZE = 200;
    const BANGLADESH = [[20.6, 88.0], [26.7, 92.7]];

    const msg = root.dataset;
    const radiusSelect = document.getElementById('radiusSelect');
    const nearMeBtn = document.getElementById('nearMeBtn');
    const nearStatus = document.getElementById('nearStatus');
    const mapEl = document.getElementById('listingMap');
    const mapNote = document.getElementById('listingMapNote');
    const viewButtons = Array.from(root.querySelectorAll('[data-view]'));
    const hideInMap = (root.dataset.hideInMap || '').split(',').map(id => document.getElementById(id.trim())).filter(Boolean);

    const url = new URL(window.location.href);
    const state = {
        view: url.searchParams.get('view') === 'map' ? 'map' : 'list',
        nearLat: parseFloat(url.searchParams.get('nearLat')) || null,
        nearLng: parseFloat(url.searchParams.get('nearLng')) || null
    };

    let leafletPromise = null;
    let map = null;
    let markers = null;

    function refresh() {
        if (typeof window.onFilterChanged === 'function') window.onFilterChanged(1);
    }

    function districtName() {
        const select = document.getElementById(root.dataset.districtSelect || '');
        return select && select.value ? select.options[select.selectedIndex].text : '';
    }

    function updateStatus() {
        const radius = radiusSelect ? radiusSelect.value : '';
        nearMeBtn?.setAttribute('aria-pressed', state.nearLat !== null ? 'true' : 'false');
        nearMeBtn?.classList.toggle('active', state.nearLat !== null);
        if (!nearStatus) return;
        if (!radius) { nearStatus.textContent = ''; return; }
        if (state.nearLat !== null) nearStatus.textContent = msg.msgNearYou.replace('{0}', radius);
        else if (districtName()) nearStatus.textContent = msg.msgNearDistrict.replace('{0}', radius).replace('{1}', districtName());
        else nearStatus.textContent = msg.msgNeedOrigin;
    }

    function loadLeaflet() {
        if (window.L) return Promise.resolve(window.L);
        if (leafletPromise) return leafletPromise;
        leafletPromise = new Promise(function (resolve, reject) {
            const css = document.createElement('link');
            css.rel = 'stylesheet';
            css.href = LEAFLET_CSS.href;
            css.integrity = LEAFLET_CSS.integrity;
            css.crossOrigin = '';
            document.head.appendChild(css);

            const script = document.createElement('script');
            script.src = LEAFLET_JS.src;
            script.integrity = LEAFLET_JS.integrity;
            script.crossOrigin = '';
            script.onload = function () {
                // The stylesheet may still be loading, so Leaflet cannot detect its icon folder by itself.
                window.L.Icon.Default.imagePath = 'https://unpkg.com/leaflet@1.9.4/dist/images/';
                resolve(window.L);
            };
            script.onerror = function () { leafletPromise = null; reject(new Error('leaflet')); };
            document.head.appendChild(script);
        });
        return leafletPromise;
    }

    function setView(view, fetch) {
        state.view = view;
        // Retry a failed live update on the page the user was on (the page script's top-level `currentPage`).
    document.getElementById('resultsRetry')?.addEventListener('click', function () {
        // eslint-disable-next-line no-undef
        if (typeof window.onFilterChanged === 'function') window.onFilterChanged(typeof currentPage === 'number' ? currentPage : 1);
    });

    viewButtons.forEach(function (btn) {
            const on = btn.dataset.view === view;
            btn.classList.toggle('active', on);
            btn.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
        const isMap = view === 'map';
        mapEl?.classList.toggle('d-none', !isMap);
        if (!isMap) mapNote?.classList.add('d-none');
        hideInMap.forEach(function (el) { el.classList.toggle('krishi-hidden-in-map', isMap); });
        if (fetch) refresh();
    }

    function popupFor(item) {
        const box = document.createElement('div');
        box.className = 'krishi-map-popup';
        const link = document.createElement('a');
        link.href = item.detailsUrl;
        link.className = 'fw-semibold';
        link.textContent = item.name;
        box.appendChild(link);
        const meta = document.createElement('div');
        meta.className = 'small text-muted';
        const parts = [item[root.dataset.priceField], item.district || item.location];
        if (item.distanceKm > 0) parts.push(msg.msgKmAway.replace('{0}', item.distanceKm.toFixed(1)));
        meta.textContent = parts.filter(Boolean).join(' · ');
        box.appendChild(meta);
        return box;
    }

    function render(data) {
        const items = (data && data.items) || [];
        loadLeaflet().then(function (L) {
            if (!map) {
                map = L.map(mapEl, { scrollWheelZoom: false });
                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                    maxZoom: 18,
                    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
                }).addTo(map);
                markers = L.layerGroup().addTo(map);
            }
            markers.clearLayers();
            const points = [];
            let unmapped = 0;
            items.forEach(function (item) {
                if (typeof item.latitude !== 'number' || typeof item.longitude !== 'number') { unmapped++; return; }
                const point = [item.latitude, item.longitude];
                points.push(point);
                L.marker(point, { title: item.name, alt: item.name }).bindPopup(popupFor(item)).addTo(markers);
            });
            if (state.nearLat !== null) {
                L.circleMarker([state.nearLat, state.nearLng], { radius: 7, color: '#1b4332', fillColor: '#52b788', fillOpacity: 1 })
                    .bindTooltip(msg.msgYou).addTo(markers);
                points.push([state.nearLat, state.nearLng]);
            }
            if (points.length) map.fitBounds(points, { padding: [32, 32], maxZoom: 13 });
            else map.fitBounds(BANGLADESH);
            // The container may have just become visible.
            setTimeout(function () { map.invalidateSize(); }, 0);

            if (mapNote) {
                const notes = [];
                if (data.totalCount > items.length) notes.push(msg.msgMapCapped.replace('{0}', items.length).replace('{1}', data.totalCount));
                if (unmapped > 0) notes.push(msg.msgUnmapped.replace('{0}', unmapped));
                mapNote.textContent = notes.join(' ');
                mapNote.classList.toggle('d-none', notes.length === 0);
            }
        }).catch(function () {
            if (mapNote) {
                mapNote.textContent = msg.msgMapFailed;
                mapNote.classList.remove('d-none');
            }
        });
    }

    radiusSelect?.addEventListener('change', function () { updateStatus(); refresh(); });
    document.getElementById(root.dataset.districtSelect || '')?.addEventListener('change', updateStatus);

    nearMeBtn?.addEventListener('click', function () {
        if (state.nearLat !== null) {
            state.nearLat = state.nearLng = null;
            updateStatus();
            refresh();
            return;
        }
        if (!window.isSecureContext || !navigator.geolocation) {
            nearStatus.textContent = msg.msgNoGeo;
            return;
        }
        nearStatus.textContent = msg.msgLocating;
        navigator.geolocation.getCurrentPosition(function (pos) {
            // Rounded to ~100 m: enough to find nearby listings, not enough to pinpoint a home.
            state.nearLat = Math.round(pos.coords.latitude * 1000) / 1000;
            state.nearLng = Math.round(pos.coords.longitude * 1000) / 1000;
            if (radiusSelect && !radiusSelect.value) radiusSelect.value = '25';
            const sort = document.getElementById('mainSortSelect');
            if (sort) sort.value = 'distance';
            updateStatus();
            refresh();
        }, function () {
            nearStatus.textContent = msg.msgGeoDenied;
        }, { enableHighAccuracy: false, timeout: 10000, maximumAge: 600000 });
    });

    viewButtons.forEach(function (btn) {
        btn.addEventListener('click', function () { if (btn.dataset.view !== state.view) setView(btn.dataset.view, true); });
    });

    window.KrishiListingGeo = {
        isMap: function () { return state.view === 'map'; },
        /** Adds the shareable state (radius, own position, view) to the page URL / request. */
        appendParams: function (params) {
            if (radiusSelect && radiusSelect.value) params.append('radiusKm', radiusSelect.value);
            if (state.nearLat !== null) { params.append('nearLat', state.nearLat); params.append('nearLng', state.nearLng); }
            if (state.view === 'map') params.append('view', 'map');
        },
        /** Request-only parameters: the map wants every pin at once rather than one page. */
        fetchParams: function (params) {
            if (state.view === 'map') { params.delete('page'); params.set('pageSize', MAP_PAGE_SIZE); }
        },
        render: render
    };

    updateStatus();
    if (state.view === 'map') setView('map', true);
})();
