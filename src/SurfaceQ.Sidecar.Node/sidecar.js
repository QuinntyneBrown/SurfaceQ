#!/usr/bin/env node
'use strict';

const fs = require('fs');
const path = require('path');
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
  const exports = [];
  collectFromFile(file, exports, new Set());
  return exports;
}

function collectFromFile(file, exports, visited) {
  if (visited.has(file)) {
    return;
  }
  visited.add(file);
  let content;
  try {
    content = fs.readFileSync(file, 'utf8');
  } catch (e) {
    return;
  }
  const sourceFile = ts.createSourceFile(file, content, ts.ScriptTarget.Latest, true);
  const dir = path.dirname(file);
  ts.forEachChild(sourceFile, (node) => {
    if (ts.isExportDeclaration(node)) {
      const specifierText = node.moduleSpecifier && node.moduleSpecifier.text;
      if (!node.exportClause) {
        if (specifierText) {
          const resolved = resolveModule(dir, specifierText);
          if (resolved) {
            collectFromFile(resolved, exports, visited);
          }
        }
        return;
      }
      if (ts.isNamedExports(node.exportClause)) {
        const declTypeOnly = node.isTypeOnly === true;
        for (const el of node.exportClause.elements) {
          const isType = declTypeOnly || el.isTypeOnly === true;
          exports.push({ name: el.name.text, kind: 'reexport', isType, file });
        }
      }
      return;
    }
    if (!hasExportModifier(node)) {
      return;
    }
    if (ts.isClassDeclaration(node) && node.name) {
      exports.push({ name: node.name.text, kind: 'class', isType: false, file });
    } else if (ts.isInterfaceDeclaration(node)) {
      exports.push({ name: node.name.text, kind: 'interface', isType: true, file });
    } else if (ts.isTypeAliasDeclaration(node)) {
      exports.push({ name: node.name.text, kind: 'type', isType: true, file });
    } else if (ts.isEnumDeclaration(node)) {
      exports.push({ name: node.name.text, kind: 'enum', isType: false, file });
    } else if (ts.isFunctionDeclaration(node) && node.name) {
      exports.push({ name: node.name.text, kind: 'function', isType: false, file });
    } else if (ts.isVariableStatement(node)) {
      for (const decl of node.declarationList.declarations) {
        if (ts.isIdentifier(decl.name)) {
          exports.push({ name: decl.name.text, kind: 'const', isType: false, file });
        }
      }
    }
  });
}

function resolveModule(dir, specifier) {
  const base = path.resolve(dir, specifier);
  const candidates = [base + '.ts', path.join(base, 'index.ts'), base];
  for (const c of candidates) {
    try {
      if (fs.statSync(c).isFile()) {
        return c;
      }
    } catch (e) {
    }
  }
  return null;
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
