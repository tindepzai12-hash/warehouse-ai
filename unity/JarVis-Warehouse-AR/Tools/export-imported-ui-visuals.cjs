// Run only after replacing the imported Figma snapshot; does not download or edit Figma.
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');
const root = path.resolve(__dirname, '..');
const document = JSON.parse(zlib.gunzipSync(fs.readFileSync(path.join(root, 'Assets/JarVis/DesignImports/JarVis-App-Imported.figma'))));
const shapes = [];
function visit(node) {
  const fill = (node.fills || []).find(p => p.type === 'SOLID' && p.visible !== false);
  const bounds = node.absoluteBoundingBox;
  const radius = node.type === 'ELLIPSE' && bounds ? Math.min(bounds.width, bounds.height) / 2 : node.cornerRadius;
  if (fill && bounds && radius > 0 && !(node.fills || []).some(p => p.type === 'IMAGE')) {
    const stroke = (node.strokes || []).find(p => p.type === 'SOLID' && p.visible !== false);
    shapes.push({ id: node.id, r: Math.min(radius, bounds.width / 2, bounds.height / 2), fill: fill.color,
      stroke: stroke ? stroke.color : {r: 0, g: 0, b: 0, a: 0}, width: stroke ? (node.strokeWeight || 1) : 0 });
  }
  (node.children || []).forEach(visit);
}
visit(document.document);
fs.writeFileSync(path.join(root, 'Assets/Resources/JarVisImportedVisuals.json'), JSON.stringify({shapes}));
console.log(`Extracted ${shapes.length} native rounded shape definitions from the imported Figma.`);
