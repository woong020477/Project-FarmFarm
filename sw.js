'use strict';
const CACHE = 'farmfarm-page-v1';
const SHELL = ['./', './index.html', './site.css', './site.js', './icon.svg', './icon-180.png', './icon-192.png', './icon-512.png', './logo.png', './manifest.webmanifest'];
const allowed = new Set(SHELL.map(path => new URL(path, self.registration.scope).href));
self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(SHELL)));
  // Keep an active game under its existing worker until its tabs close.
});
self.addEventListener('activate', event => {
  event.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(key => key.startsWith('farmfarm-page-') && key !== CACHE).map(key => caches.delete(key)))));
});
self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET' || !allowed.has(event.request.url)) return;
  event.respondWith(fetch(event.request).then(response => {
    if (response.ok) {
      const copy = response.clone();
      event.waitUntil(caches.open(CACHE).then(cache => cache.put(event.request, copy)).catch(() => {}));
    }
    return response;
  }).catch(() => caches.match(event.request).then(cached => cached || Response.error())));
});
