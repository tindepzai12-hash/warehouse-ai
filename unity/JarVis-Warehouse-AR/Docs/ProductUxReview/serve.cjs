const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const routes = {
  '/': ['index.html', 'text/html; charset=utf-8'],
  '/index.html': ['index.html', 'text/html; charset=utf-8'],
  '/catalog.html': ['index.html', 'text/html; charset=utf-8'],
  '/app.css': ['app.css', 'text/css; charset=utf-8'],
  '/app.js': ['app.js', 'text/javascript; charset=utf-8'],
  '/schema.js': ['../ProductFormMockup/catalog-schema.js', 'text/javascript; charset=utf-8'],
  '/font.ttf': ['../../Assets/Resources/BeVietnamPro-Regular.ttf', 'font/ttf']
};
http.createServer((request, response) => {
  const route = routes[new URL(request.url, 'http://127.0.0.1').pathname];
  if (request.method !== 'GET' || !route) {
    response.writeHead(404); response.end('Not found'); return;
  }
  fs.readFile(path.resolve(__dirname, route[0]), (error, data) => {
    if (error) { response.writeHead(404); response.end('Not found'); return; }
    response.writeHead(200, {
      'Content-Type': route[1], 'Cache-Control': 'no-store',
      'Content-Security-Policy': "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; font-src 'self'; connect-src 'none'; form-action 'none'; base-uri 'none'; frame-ancestors 'none'"
    });
    response.end(data);
  });
}).listen(Number(process.argv[2] || 4198), '127.0.0.1', () => console.log('JarVis UX review http://127.0.0.1:' + (process.argv[2] || 4198)));
