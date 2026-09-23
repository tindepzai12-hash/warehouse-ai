const path = require('node:path');
const { Knowledge } = require(path.join(__dirname, '..', 'server'));

const knowledge = new Knowledge();
const localRuntimeDocuments = knowledge.catalog.filter(document => document.runtime_approved);
const localDraftDocuments = knowledge.unverifiedDocuments;

console.log(JSON.stringify({
  runtimeChunkCount: knowledge.chunks.length,
  localRuntimeDocuments: localRuntimeDocuments.map(document => ({
    documentId: document.documentId,
    sectionId: document.sectionId,
    source_status: document.source_status
  })),
  localDraftDocuments: localDraftDocuments.map(document => ({
    documentId: document.documentId,
    sectionId: document.sectionId,
    source_status: document.source_status
  }))
}, null, 2));
