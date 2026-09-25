/*
 * KrishiLink service worker (REA-01): the crop calendar and the app shell keep working on a dropped 2G connection.
 *
 * Caching is by ALLOW-LIST. A request is only ever cached when it matches one of the rules below:
 *   - static assets (/css, /js, /lib, /images, the manifest)            cache-first
 *   - the crop calendar data (/Advisory/CalendarJson)                    cached copy first, refreshed in the background
 *   - advisory pages, only when the server marks the response
 *     "X-Offline-Cacheable: 1" (it does so for signed-out visitors only) network-first, cached copy when offline
 * Everything else — accounts, bookings, payments, verification, owner pages, any POST — goes straight to the network
 * and is never stored: a cached booking status or someone's signed-in page is a support incident, not a feature.
 */
'use strict';

const VERSION = 'v1';
const STATIC_CACHE = `krishilink-static-${VERSION}`;
const DATA_CACHE = `krishilink-data-${VERSION}`;
const PAGE_CACHE = `krishilink-pages-${VERSION}`;
const OFFLINE_URL = '/Home/Offline';
const LANGUAGES = ['en', 'bn'];
const LANG_KEY = '/__krishilink/lang';
const CALENDAR_URL = '/Advisory/CalendarJson';
const MAX_PAGES = 20;

const STATIC_PREFIXES = ['/css/', '/js/', '/lib/', '/images/'];
const STATIC_FILES = ['/manifest.json', '/favicon.ico'];
const ADVISORY_PAGES = /^\/Advisory(?:\/(?:Index|Calendar|Alerts|Suggestions|Planner))?\/?$/i;

self.addEventListener('install', event => {
    event.waitUntil((async () => {
        // The offline page carries no user data; it is fetched without cookies in both languages.
        const pages = await caches.open(PAGE_CACHE);
        await pages.addAll(LANGUAGES.map(lang => new Request(`${OFFLINE_URL}?culture=${lang}`, { credentials: 'omit' })));
        // Its stylesheet, script and icon are versioned URLs: keep exactly those, or the page renders unstyled offline.
        const html = await (await pages.match(`${OFFLINE_URL}?culture=en`)).text();
        const assets = [...html.matchAll(/(?:href|src)="(\/(?:css|js|lib|images)\/[^"]+)"/g)].map(m => m[1].replace(/&amp;/g, '&'));
        await (await caches.open(STATIC_CACHE)).addAll([...new Set(assets)]);
        const data = await caches.open(DATA_CACHE);
        await stamp(data, CALENDAR_URL, await fetch(CALENDAR_URL, { credentials: 'omit' }));
        await self.skipWaiting();
    })());
});

self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        const keep = new Set([STATIC_CACHE, DATA_CACHE, PAGE_CACHE]);
        for (const name of await caches.keys())
            if (name.startsWith('krishilink-') && !keep.has(name)) await caches.delete(name);
        await self.clients.claim();
    })());
});

/**
 * How a request is handled: 'static' | 'calendar' | 'advisory' (may be cached) | 'network' (a page that is never
 * cached, only replaced by the offline page when the network is down) | null (not touched at all).
 */
function routeFor(method, sameOrigin, pathname, mode) {
    if (method !== 'GET' || !sameOrigin) return null;
    if (STATIC_FILES.includes(pathname) || STATIC_PREFIXES.some(p => pathname.startsWith(p))) return 'static';
    if (pathname === CALENDAR_URL) return 'calendar';
    if (mode !== 'navigate') return null;
    return ADVISORY_PAGES.test(pathname) ? 'advisory' : 'network';
}

self.addEventListener('fetch', event => {
    const request = event.request;
    const url = new URL(request.url);
    switch (routeFor(request.method, url.origin === self.location.origin, url.pathname, request.mode)) {
        case 'static': event.respondWith(cacheFirst(request)); break;
        case 'calendar': event.respondWith(staleWhileRevalidate(event, request)); break;
        case 'advisory': event.respondWith(advisoryPage(request)); break;
        case 'network': event.respondWith(withOfflineFallback(request)); break;
    }
});

async function cacheFirst(request) {
    const cached = await caches.match(request);
    if (cached) return cached;
    const response = await fetch(request);
    if (response.ok && response.type === 'basic') (await caches.open(STATIC_CACHE)).put(request, response.clone());
    return response;
}

async function staleWhileRevalidate(event, request) {
    const cache = await caches.open(DATA_CACHE);
    const cached = await cache.match(CALENDAR_URL);
    const refresh = fetch(request).then(response => stamp(cache, CALENDAR_URL, response)).catch(() => null);
    if (cached) {
        event.waitUntil(refresh);
        return cached;
    }
    return (await refresh) || new Response('{"crops":[]}', { status: 503, headers: { 'Content-Type': 'application/json' } });
}

/** Network first; a signed-out advisory page is kept (at most MAX_PAGES) and shown, dated, when the network is gone. */
async function advisoryPage(request) {
    try {
        const response = await fetch(request);
        if (response.ok && response.headers.get('X-Offline-Cacheable') === '1') {
            const cache = await caches.open(PAGE_CACHE);
            await stamp(cache, request.url, response.clone());
            await trim(cache);
        }
        return response;
    } catch {
        const cached = await caches.match(request.url, { cacheName: PAGE_CACHE, ignoreVary: true });
        return cached ? markAsSaved(cached) : offlinePage();
    }
}

async function withOfflineFallback(request) {
    try {
        return await fetch(request);
    } catch {
        return offlinePage();
    }
}

/** Pages tell the worker their language, so the offline page matches what the farmer was reading. */
self.addEventListener('message', event => {
    const lang = event.data && event.data.type === 'lang' && LANGUAGES.includes(event.data.lang) ? event.data.lang : null;
    if (lang) event.waitUntil(caches.open(DATA_CACHE).then(cache => cache.put(LANG_KEY, new Response(lang))));
});

async function offlinePage() {
    const stored = await caches.match(LANG_KEY, { cacheName: DATA_CACHE });
    const lang = stored ? await stored.text() : 'en';
    return (await caches.match(`${OFFLINE_URL}?culture=${LANGUAGES.includes(lang) ? lang : 'en'}`, { cacheName: PAGE_CACHE })) ||
        new Response('<!doctype html><title>Offline</title><p>You are offline.</p>', { headers: { 'Content-Type': 'text/html; charset=utf-8' } });
}

/** Stores a response with the time it was saved, so the page can say "showing data from …". */
async function stamp(cache, key, response) {
    if (!response || !response.ok) return response;
    const headers = new Headers(response.headers);
    headers.set('X-Saved-At', new Date().toISOString());
    const copy = new Response(await response.clone().blob(), { status: response.status, statusText: response.statusText, headers });
    await cache.put(key, copy);
    return response;
}

/** Marks a cached page so site.js can show "You are offline — saved on {date}". */
async function markAsSaved(response) {
    const savedAt = response.headers.get('X-Saved-At') || '';
    const html = (await response.text()).replace(/<html(\s|>)/i, `<html data-saved-at="${savedAt.replace(/[^0-9TZ:.\-]/g, '')}"$1`);
    return new Response(html, { status: 200, headers: { 'Content-Type': 'text/html; charset=utf-8' } });
}

async function trim(cache) {
    const keys = await cache.keys();
    const pages = keys.filter(k => new URL(k.url).pathname !== OFFLINE_URL);
    for (const old of pages.slice(0, Math.max(0, pages.length - MAX_PAGES))) await cache.delete(old);
}

// For the Node routing test (tests/js/sw-routing.test.js); `module` does not exist in a service worker.
if (typeof module !== 'undefined') module.exports = { routeFor };
