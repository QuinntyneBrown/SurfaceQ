#!/usr/bin/env node
'use strict';

const fs = require('fs');
const readline = require('readline');
const ts = require('typescript');

const rl = readline.createInterface({ input: process.stdin });

rl.on('line', (line) => {
  let msg;
  try {
    msg = JSON.parse(line);
  } catch (e) {
    return;
  }
  if (!msg || typeof msg !== 'object') {
    return;
  }
  if (msg.method === 'ping') {
    respond(msg.id, 'pong');
    return;
  }
  if (msg.method === 'discover') {
    const file = msg.params && msg.params.file;
    respond(msg.id, discover(file));
    return;
  }
});

function respond(id, result) {
  const response = { jsonrpc: '2.0', id: id, result: result };
  process.stdout.write(JSON.stringify(response) + '\n');
}

function discover(file) {
  const content = fs.readFileSync(file, 'utf8');
  const sourceFile = ts.createSourceFile(file, content, ts.ScriptTarget.Latest, true);
  const exports = [];
  ts.forEachChild(sourceFile, (node) => {
    if (!hasExportModifier(node)) {
      return;
    }
    if (ts.isClassDeclaration(node) && node.name) {
      exports.push({ name: node.name.text, kind: 'class', isType: false });
    } else if (ts.isInterfaceDeclaration(node)) {
      exports.push({ name: node.name.text, kind: 'interface', isType: true });
    } else if (ts.isTypeAliasDeclaration(node)) {
      exports.push({ name: node.name.text, kind: 'type', isType: true });
    } else if (ts.isEnumDeclaration(node)) {
      exports.push({ name: node.name.text, kind: 'enum', isType: false });
    }
  });
  return exports;
}

function hasExportModifier(node) {
  const modifiers = typeof ts.getModifiers === 'function'
    ? ts.getModifiers(node)
    : node.modifiers;
  if (!modifiers) {
    return false;
  }
  for (const m of modifiers) {
    if (m.kind === ts.SyntaxKind.ExportKeyword) {
      return true;
    }
  }
  return false;
}
