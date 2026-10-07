const fs = require('node:fs');
const path = require('node:path');

const ROOT = path.resolve(__dirname, '..');
const DEFAULT_TARGET = path.join(ROOT, 'knowledge', 'warehouse-sops.json');
const REQUIRED_FIELDS = ['documentId', 'sectionId', 'title', 'content', 'source_status', 'runtime_approved', 'language', 'keywords'];
const RUNTIME_SOURCE_STATUSES = new Set(['official_release', 'spec_confirmed_project_policy']);

function usage() {
  console.log('Usage: node scripts/update-knowledge.cjs <input.json> [--apply] [--target <catalog.json>]');
  console.log('Default mode is dry-run. Use --apply to create a backup and update the catalog.');
}

function parseArgs(argv) {
  let input;
  let target = DEFAULT_TARGET;
  let apply = false;

  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (argument === '--apply') {
      apply = true;
    } else if (argument === '--target') {
      index += 1;
      if (!argv[index]) throw new Error('--target requires a file path');
      target = path.resolve(argv[index]);
    } else if (argument === '--help' || argument === '-h') {
      usage();
      process.exit(0);
    } else if (argument.startsWith('-')) {
      throw new Error(`Unknown option: ${argument}`);
    } else if (!input) {
      input = path.resolve(argument);
    } else {
      throw new Error(`Unexpected argument: ${argument}`);
    }
  }

  if (!input) throw new Error('An input JSON file is required');
  return { input, target, apply };
}

function readJsonArray(filePath, label) {
  let value;
  try {
    value = JSON.parse(fs.readFileSync(filePath, 'utf8'));
  } catch (error) {
    throw new Error(`${label} is not valid readable JSON: ${error.message}`);
  }
  if (!Array.isArray(value)) throw new Error(`${label} must contain a JSON array`);
  return value;
}

function validateDocument(document, index, label) {
  if (!document || typeof document !== 'object' || Array.isArray(document)) {
    throw new Error(`${label}[${index}] must be an object`);
  }

  for (const field of REQUIRED_FIELDS) {
    if (!Object.hasOwn(document, field)) throw new Error(`${label}[${index}] is missing ${field}`);
  }

  for (const field of ['documentId', 'sectionId', 'title', 'content', 'source_status', 'language']) {
    if (typeof document[field] !== 'string' || !document[field].trim()) {
      throw new Error(`${label}[${index}].${field} must be a non-empty string`);
    }
  }

  if (typeof document.runtime_approved !== 'boolean') {
    throw new Error(`${label}[${index}].runtime_approved must be a boolean`);
  }
  if (!Array.isArray(document.keywords) || !document.keywords.length ||
      document.keywords.some(keyword => typeof keyword !== 'string' || !keyword.trim())) {
    throw new Error(`${label}[${index}].keywords must be a non-empty array of strings`);
  }
  if (document.runtime_approved && !RUNTIME_SOURCE_STATUSES.has(document.source_status)) {
    throw new Error(`${label}[${index}] uses a source_status that is not allowed in runtime RAG`);
  }
}

function keyOf(document) {
  return `${document.documentId}\u0000${document.sectionId}`;
}

function validateUnique(documents, label) {
  const keys = new Set();
  documents.forEach((document, index) => {
    validateDocument(document, index, label);
    const key = keyOf(document);
    if (keys.has(key)) throw new Error(`${label} contains duplicate key ${document.documentId}/${document.sectionId}`);
    keys.add(key);
  });
}

function comparable(document) {
  const copy = { ...document, keywords: [...document.keywords] };
  return JSON.stringify(copy);
}

function backupName(target) {
  const stamp = new Date().toISOString().replace(/[:.]/g, '-');
  const extension = path.extname(target);
  return `${target.slice(0, -extension.length)}.${stamp}.bak`;
}

function main() {
  const { input, target, apply } = parseArgs(process.argv.slice(2));
  const current = readJsonArray(target, 'Target catalog');
  const incoming = readJsonArray(input, 'Input catalog');
  validateUnique(current, 'Target catalog');
  validateUnique(incoming, 'Input catalog');

  const currentByKey = new Map(current.map(document => [keyOf(document), document]));
  const additions = [];
  const unchanged = [];

  for (const document of incoming) {
    const existing = currentByKey.get(keyOf(document));
    if (!existing) {
      additions.push(document);
      continue;
    }
    if (comparable(existing) !== comparable(document)) {
      throw new Error(`Conflicting record already exists: ${document.documentId}/${document.sectionId}`);
    }
    unchanged.push(`${document.documentId}/${document.sectionId}`);
  }

  const merged = current.concat(additions);
  validateUnique(merged, 'Merged catalog');
  const output = `${JSON.stringify(merged, null, 2)}\n`;
  JSON.parse(output);

  console.log(JSON.stringify({
    mode: apply ? 'apply' : 'dry-run',
    target,
    input,
    existingCount: current.length,
    inputCount: incoming.length,
    addedCount: additions.length,
    unchangedCount: unchanged.length,
    finalCount: merged.length,
    added: additions.map(document => `${document.documentId}/${document.sectionId}`),
    unchanged
  }, null, 2));

  if (!apply || additions.length === 0) return;

  const backup = backupName(target);
  fs.copyFileSync(target, backup);
  try {
    fs.writeFileSync(target, output, 'utf8');
    const written = readJsonArray(target, 'Written catalog');
    validateUnique(written, 'Written catalog');
    console.log(`Updated ${target}`);
    console.log(`Backup: ${backup}`);
  } catch (error) {
    fs.copyFileSync(backup, target);
    throw new Error(`Update failed; restored the backup. ${error.message}`);
  }
}

try {
  main();
} catch (error) {
  console.error(`Knowledge update failed: ${error.message}`);
  process.exitCode = 1;
}
