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
  if (msg.method === 'document') {
    const file = msg.params && msg.params.file;
    respond(msg.id, document(file));
    return;
  }
});

function respond(id, result) {
  const response = { jsonrpc: '2.0', id: id, result: result };
  process.stdout.write(JSON.stringify(response) + '\n');
}

function discover(file) {
  const exports = [];
  const warnings = [];
  const errors = [];
  collectFromFile(file, exports, warnings, errors, new Set());
  return { exports, warnings, errors };
}

function collectFromFile(file, exports, warnings, errors, visited) {
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
  const parseDiagnostics = sourceFile.parseDiagnostics || [];
  if (parseDiagnostics.length > 0) {
    for (const d of parseDiagnostics) {
      let line = 1;
      if (d.file && typeof d.start === 'number') {
        const lc = d.file.getLineAndCharacterOfPosition(d.start);
        line = lc.line + 1;
      }
      const message = ts.flattenDiagnosticMessageText(d.messageText, '\n');
      errors.push({ file, line, message });
    }
    return;
  }
  const dir = path.dirname(file);
  ts.forEachChild(sourceFile, (node) => {
    if (ts.isExportAssignment(node) && !node.isExportEquals) {
      warnings.push({ code: 'default-export-skipped', file });
      return;
    }
    if (ts.isExportDeclaration(node)) {
      const specifierText = node.moduleSpecifier && node.moduleSpecifier.text;
      if (!node.exportClause) {
        if (specifierText) {
          const resolved = resolveModule(dir, specifierText);
          if (resolved) {
            collectFromFile(resolved, exports, warnings, errors, visited);
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
    if (hasDefaultModifier(node)) {
      warnings.push({ code: 'default-export-skipped', file });
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

// ---------------------------------------------------------------------------
// document: rich declaration detail for the `surfaceq docs` command.
// Unlike discover (which only needs names), this walks members so the host can
// render interface properties/methods, enum members, type definitions, and the
// contract type behind each InjectionToken.
// ---------------------------------------------------------------------------

function document(file) {
  const declarations = [];
  const warnings = [];
  const errors = [];
  let content;
  try {
    content = fs.readFileSync(file, 'utf8');
  } catch (e) {
    return { declarations, warnings, errors };
  }
  const sourceFile = ts.createSourceFile(file, content, ts.ScriptTarget.Latest, true);
  const parseDiagnostics = sourceFile.parseDiagnostics || [];
  if (parseDiagnostics.length > 0) {
    for (const d of parseDiagnostics) {
      let line = 1;
      if (d.file && typeof d.start === 'number') {
        line = d.file.getLineAndCharacterOfPosition(d.start).line + 1;
      }
      errors.push({ file, line, message: ts.flattenDiagnosticMessageText(d.messageText, '\n') });
    }
    return { declarations, warnings, errors };
  }
  ts.forEachChild(sourceFile, (node) => {
    if (!hasExportModifier(node) || hasDefaultModifier(node)) {
      if (hasDefaultModifier(node)) {
        warnings.push({ code: 'default-export-skipped', file });
      }
      return;
    }
    const decl = describeDeclaration(node, sourceFile, file);
    if (decl) {
      declarations.push(decl);
    }
  });
  return { declarations, warnings, errors };
}

function describeDeclaration(node, sourceFile, file) {
  if (ts.isInterfaceDeclaration(node)) {
    return {
      name: node.name.text,
      kind: 'interface',
      doc: getDoc(node, sourceFile),
      extends: heritageNames(node, ts.SyntaxKind.ExtendsKeyword, sourceFile),
      members: node.members.map((m) => describeMember(m, sourceFile)).filter(Boolean),
      file,
    };
  }
  if (ts.isClassDeclaration(node) && node.name) {
    return {
      name: node.name.text,
      kind: 'class',
      doc: getDoc(node, sourceFile),
      implements: heritageNames(node, ts.SyntaxKind.ImplementsKeyword, sourceFile),
      extends: heritageNames(node, ts.SyntaxKind.ExtendsKeyword, sourceFile),
      members: node.members.map((m) => describeMember(m, sourceFile)).filter(Boolean),
      file,
    };
  }
  if (ts.isTypeAliasDeclaration(node)) {
    return {
      name: node.name.text,
      kind: 'type',
      doc: getDoc(node, sourceFile),
      definition: collapse(node.type.getText(sourceFile)),
      file,
    };
  }
  if (ts.isEnumDeclaration(node)) {
    return {
      name: node.name.text,
      kind: 'enum',
      doc: getDoc(node, sourceFile),
      members: enumMembers(node, sourceFile),
      file,
    };
  }
  if (ts.isFunctionDeclaration(node) && node.name) {
    return {
      name: node.name.text,
      kind: 'function',
      doc: getDoc(node, sourceFile),
      parameters: node.parameters.map((p) => describeParameter(p, sourceFile)),
      returnType: node.type ? collapse(node.type.getText(sourceFile)) : '',
      file,
    };
  }
  if (ts.isVariableStatement(node)) {
    for (const d of node.declarationList.declarations) {
      if (!ts.isIdentifier(d.name)) {
        continue;
      }
      const token = tryInjectionToken(d, node, sourceFile, file);
      if (token) {
        return token;
      }
      return {
        name: d.name.text,
        kind: 'const',
        doc: getDoc(node, sourceFile),
        type: variableType(d, sourceFile),
        file,
      };
    }
  }
  return null;
}

function tryInjectionToken(decl, statement, sourceFile, file) {
  const init = decl.initializer;
  if (!init || !ts.isNewExpression(init)) {
    return null;
  }
  if (init.expression.getText(sourceFile) !== 'InjectionToken') {
    return null;
  }
  const contract = init.typeArguments && init.typeArguments.length > 0
    ? collapse(init.typeArguments[0].getText(sourceFile))
    : 'unknown';
  let description = '';
  if (init.arguments && init.arguments.length > 0 && ts.isStringLiteral(init.arguments[0])) {
    description = init.arguments[0].text;
  }
  return {
    name: decl.name.text,
    kind: 'injection-token',
    doc: getDoc(statement, sourceFile),
    contract,
    description,
    file,
  };
}

function describeMember(member, sourceFile) {
  const readonly = hasModifierOfKind(member, ts.SyntaxKind.ReadonlyKeyword);
  if (ts.isPropertySignature(member) || ts.isPropertyDeclaration(member)) {
    if (!isPublic(member) || !memberName(member, sourceFile)) {
      return null;
    }
    return {
      memberKind: 'property',
      name: memberName(member, sourceFile),
      type: member.type ? collapse(member.type.getText(sourceFile)) : '',
      optional: !!member.questionToken,
      readonly,
      doc: getDoc(member, sourceFile),
    };
  }
  if (ts.isMethodSignature(member) || ts.isMethodDeclaration(member)) {
    if (!isPublic(member) || !memberName(member, sourceFile)) {
      return null;
    }
    return {
      memberKind: 'method',
      name: memberName(member, sourceFile),
      parameters: member.parameters.map((p) => describeParameter(p, sourceFile)),
      returnType: member.type ? collapse(member.type.getText(sourceFile)) : '',
      optional: !!member.questionToken,
      doc: getDoc(member, sourceFile),
    };
  }
  if (ts.isGetAccessorDeclaration(member) || ts.isSetAccessorDeclaration(member)) {
    if (!isPublic(member) || !memberName(member, sourceFile)) {
      return null;
    }
    return {
      memberKind: 'property',
      name: memberName(member, sourceFile),
      type: member.type ? collapse(member.type.getText(sourceFile)) : '',
      optional: false,
      readonly: ts.isGetAccessorDeclaration(member),
      doc: getDoc(member, sourceFile),
    };
  }
  return null;
}

function describeParameter(param, sourceFile) {
  return {
    name: param.name.getText(sourceFile),
    type: param.type ? collapse(param.type.getText(sourceFile)) : '',
    optional: !!param.questionToken || !!param.initializer,
  };
}

function enumMembers(node, sourceFile) {
  const members = [];
  let auto = 0;
  let autoValid = true;
  for (const m of node.members) {
    const name = m.name.getText(sourceFile);
    let value = '';
    if (m.initializer) {
      value = collapse(m.initializer.getText(sourceFile));
      if (ts.isNumericLiteral(m.initializer)) {
        auto = Number(m.initializer.text) + 1;
        autoValid = true;
      } else {
        autoValid = false;
      }
    } else if (autoValid) {
      value = String(auto);
      auto += 1;
    }
    members.push({ name, value, doc: getDoc(m, sourceFile) });
  }
  return members;
}

function heritageNames(node, keyword, sourceFile) {
  const names = [];
  if (!node.heritageClauses) {
    return names;
  }
  for (const clause of node.heritageClauses) {
    if (clause.token !== keyword) {
      continue;
    }
    for (const t of clause.types) {
      names.push(collapse(t.getText(sourceFile)));
    }
  }
  return names;
}

function variableType(decl, sourceFile) {
  if (decl.type) {
    return collapse(decl.type.getText(sourceFile));
  }
  if (decl.initializer) {
    return collapse(decl.initializer.getText(sourceFile));
  }
  return '';
}

function memberName(member, sourceFile) {
  if (!member.name) {
    return '';
  }
  if (ts.isComputedPropertyName(member.name)) {
    return '';
  }
  return member.name.getText(sourceFile);
}

function isPublic(member) {
  return !hasModifierOfKind(member, ts.SyntaxKind.PrivateKeyword)
    && !hasModifierOfKind(member, ts.SyntaxKind.ProtectedKeyword);
}

function getDoc(node, sourceFile) {
  const ranges = ts.getLeadingCommentRanges(sourceFile.text, node.getFullStart()) || [];
  for (let i = ranges.length - 1; i >= 0; i--) {
    const text = sourceFile.text.slice(ranges[i].pos, ranges[i].end);
    if (text.startsWith('/**')) {
      return cleanJsDoc(text);
    }
  }
  return '';
}

function cleanJsDoc(text) {
  const inner = text.replace(/^\/\*\*/, '').replace(/\*\/$/, '');
  const lines = [];
  for (const raw of inner.split('\n')) {
    const line = raw.replace(/^\s*\*?/, '').trim();
    if (line.startsWith('@')) {
      break;
    }
    if (line) {
      lines.push(line);
    }
  }
  return collapse(lines.join(' '));
}

function collapse(text) {
  return text.replace(/\s+/g, ' ').trim();
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
  return hasModifierOfKind(node, ts.SyntaxKind.ExportKeyword);
}

function hasDefaultModifier(node) {
  return hasModifierOfKind(node, ts.SyntaxKind.DefaultKeyword);
}

function hasModifierOfKind(node, kind) {
  const modifiers = typeof ts.getModifiers === 'function'
    ? ts.getModifiers(node)
    : node.modifiers;
  if (!modifiers) {
    return false;
  }
  for (const m of modifiers) {
    if (m.kind === kind) {
      return true;
    }
  }
  return false;
}
