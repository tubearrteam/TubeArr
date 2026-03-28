/**
 * Compare translate('Key') usages in frontend/src with backend/Shared/Localization/en.json
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, '..');

function walk(dir, acc = []) {
  for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, ent.name);
    if (ent.isDirectory()) walk(p, acc);
    else if (/\.(jsx?|tsx?)$/.test(ent.name)) acc.push(p);
  }
  return acc;
}

const enPath = path.join(root, 'backend/Shared/Localization/en.json');
const en = JSON.parse(fs.readFileSync(enPath, 'utf8'));
const enKeys = new Set(Object.keys(en));

const files = walk(path.join(root, 'frontend/src'));
const missing = new Map();

function addMissing(key, file) {
  if (!enKeys.has(key)) {
    if (!missing.has(key)) missing.set(key, new Set());
    missing.get(key).add(path.relative(root, file).replace(/\\/g, '/'));
  }
}

for (const file of files) {
  if (file.endsWith(`${path.sep}translate.ts`)) continue;
  const content = fs.readFileSync(file, 'utf8');

  // translate('Key' or translate("Key"
  const reString = /translate\s*\(\s*['"]([^'"]+)['"]/g;
  let m;
  while ((m = reString.exec(content)) !== null) {
    addMissing(m[1], file);
  }

  // translate(`StaticKey`) — template literal with no interpolation
  const reTpl = /translate\s*\(\s*`([^`${]+)`\s*[,)]/g;
  while ((m = reTpl.exec(content)) !== null) {
    addMissing(m[1], file);
  }
}

const sorted = [...missing.entries()].sort((a, b) => a[0].localeCompare(b[0]));

console.log(`en.json keys: ${enKeys.size}`);
console.log(`Files scanned: ${files.length}`);
console.log('');

if (sorted.length === 0) {
  console.log('No missing translation keys found (static analysis).');
  process.exit(0);
}

console.log(`Missing keys (${sorted.length}):\n`);
for (const [key, fileSet] of sorted) {
  console.log(`  ${key}`);
  for (const f of [...fileSet].sort()) console.log(`    ${f}`);
  console.log('');
}

process.exit(1);
