// Service worker de producción (se reemplaza por service-worker.js antes de deploy).
// Estrategia: red primero con respaldo de caché — contenido fresco online,
// offline con lo último visitado. Nunca sirve versiones viejas cuando hay red.
const CACHE = 'enigma-v1';

self.addEventListener('install', event => {
    event.waitUntil((async () => {
        const cache = await caches.open(CACHE);
        await cache.addAll([
            './',
            'index.html',
            'css/app.css',
            'Enigma.Client.styles.css',
            'images/logo_size.jpg',
            'images/logo_size_invert.png',
            'images/logo_size.svg',
            'images/logo_size_invert.svg',
            'images/icon.svg',
            'images/icon-invert.svg',
            'manifest.webmanifest',
            'js/login-motion.js',
            'fonts/manrope-latin.woff2'
        ]);
        await self.skipWaiting();
    })());
});

self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        await self.clients.claim();
        const keys = await caches.keys();
        await Promise.all(keys.filter(key => key !== CACHE).map(key => caches.delete(key)));
    })());
});

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') {
        return;
    }
    event.respondWith((async () => {
        const cache = await caches.open(CACHE);
        try {
            const fresh = await fetch(event.request);
            if (fresh && fresh.status === 200 && fresh.type === 'basic') {
                await cache.put(event.request, fresh.clone());
            }
            return fresh;
        } catch {
            const cached = await cache.match(event.request, { ignoreSearch: true });
            if (cached) {
                return cached;
            }
            if (event.request.mode === 'navigate') {
                return cache.match('index.html');
            }
            return Response.error();
        }
    })());
});
