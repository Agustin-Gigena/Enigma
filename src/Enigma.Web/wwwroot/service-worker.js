// Service worker de desarrollo: no intercepta nada para no servir builds viejos.
// En producción, se reemplaza por service-worker.published.js antes de deploy.
self.addEventListener('install', event => event.waitUntil(self.skipWaiting()));
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
