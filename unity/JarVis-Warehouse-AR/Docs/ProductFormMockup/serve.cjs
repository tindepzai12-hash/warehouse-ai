const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const routes = {
  '/':['index.html','text/html; charset=utf-8'],
  '/index.html':['index.html','text/html; charset=utf-8'],
  '/mockup.css':['mockup.css','text/css; charset=utf-8'],
  '/mockup.js':['mockup.js','text/javascript; charset=utf-8'],
  '/catalog.html':['catalog.html','text/html; charset=utf-8'],
  '/catalog.css':['catalog.css','text/css; charset=utf-8'],
  '/catalog.js':['catalog.js','text/javascript; charset=utf-8'],
  '/catalog-schema.js':['catalog-schema.js','text/javascript; charset=utf-8'],
  '/font.ttf':['../../Assets/Resources/BeVietnamPro-Regular.ttf','font/ttf']
};
http.createServer((request,response) => {
  const route = routes[new URL(request.url,'http://127.0.0.1').pathname];
  if(request.method !== 'GET' || !route) {response.writeHead(404);response.end('Not found');return;}
  fs.readFile(path.resolve(__dirname,route[0]),(error,data) => {
    if(error) {response.writeHead(404);response.end('Not found');return;}
    response.writeHead(200,{'Content-Type':route[1],'Cache-Control':'no-store','Content-Security-Policy':"default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; font-src 'self'; connect-src 'none'; form-action 'none'; base-uri 'none'"});response.end(data);
  });
}).listen(Number(process.argv[2] || 4196),'127.0.0.1',() => process.stdout.write('JarVis product form mockup port: '+(process.argv[2] || 4196)+'\n'));
