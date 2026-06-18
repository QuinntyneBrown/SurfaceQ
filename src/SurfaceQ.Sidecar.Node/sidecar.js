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
  if (msg.method === 'inventory') {
    const file = msg.params && msg.params.file;
    respond(msg.id, inventory(file));
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
  collectFromFile(file, exports, warnings, errors, new Set(), new Map());
  return { exports, warnings, errors };
}

function collectFromFile(file, exports, warnings, errors, visited, cache) {
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
            collectFromFile(resolved, exports, warnings, errors, visited, cache);
          }
        }
        return;
      }
      if (ts.isNamedExports(node.exportClause)) {
        const declTypeOnly = node.isTypeOnly === true;
        for (const el of node.exportClause.elements) {
          const isType = declTypeOnly || el.isTypeOnly === true;
          const publicApi = reexportPublicApi(el, specifierText, dir, sourceFile, cache);
          exports.push({ name: el.name.text, kind: 'reexport', isType, file, publicApi });
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
    const publicApi = hasPublicApiTag(node, sourceFile);
    if (ts.isClassDeclaration(node) && node.name) {
      exports.push({ name: node.name.text, kind: 'class', isType: false, file, publicApi });
    } else if (ts.isInterfaceDeclaration(node)) {
      exports.push({ name: node.name.text, kind: 'interface', isType: true, file, publicApi });
    } else if (ts.isTypeAliasDeclaration(node)) {
      exports.push({ name: node.name.text, kind: 'type', isType: true, file, publicApi });
    } else if (ts.isEnumDeclaration(node)) {
      exports.push({ name: node.name.text, kind: 'enum', isType: false, file, publicApi });
    } else if (ts.isFunctionDeclaration(node) && node.name) {
      exports.push({ name: node.name.text, kind: 'function', isType: false, file, publicApi });
    } else if (ts.isVariableStatement(node)) {
      // The JSDoc block sits on the statement, so every declarator shares it.
      for (const decl of node.declarationList.declarations) {
        if (ts.isIdentifier(decl.name)) {
          exports.push({ name: decl.name.text, kind: 'const', isType: false, file, publicApi });
        }
      }
    }
  });
}

// The @publicApi JSDoc tag marks a declaration as part of the curated surface
// (`generate --only-public-api`). Same comment-block selection rule as jsdoc(),
// and — like @deprecated — only a tag at the start of a JSDoc line counts; a
// prose mention of @publicApi does not tag the declaration.
function hasPublicApiTag(node, sourceFile) {
  const ranges = ts.getLeadingCommentRanges(sourceFile.text, node.getFullStart()) || [];
  for (let i = ranges.length - 1; i >= 0; i--) {
    const text = sourceFile.text.slice(ranges[i].pos, ranges[i].end);
    if (!text.startsWith('/**')) {
      continue;
    }
    const inner = text.replace(/^\/\*\*/, '').replace(/\*\/$/, '');
    return inner.split('\n').some((raw) => {
      const line = raw.replace(/^\s*\*?/, '').trim();
      return /^@publicApi(\s|$)/.test(line);
    });
  }
  return false;
}

// A named re-export carries no JSDoc of its own — the tag lives on the original
// declaration, so resolve the declaring module (or stay in this file for a local
// `export { X }` clause) and trace the name to it. A rename follows the original
// name; an unresolvable module reads as untagged.
function reexportPublicApi(el, specifierText, dir, sourceFile, cache) {
  const originalName = el.propertyName ? el.propertyName.text : el.name.text;
  let target = sourceFile;
  if (specifierText) {
    const resolved = resolveModule(dir, specifierText);
    target = resolved ? parseCached(resolved, cache) : null;
  }
  if (!target) {
    return false;
  }
  return declarationHasPublicApiTag(target, originalName, cache, new Set());
}

function parseCached(file, cache) {
  if (cache.has(file)) {
    return cache.get(file);
  }
  let parsed = null;
  try {
    const content = fs.readFileSync(file, 'utf8');
    parsed = ts.createSourceFile(file, content, ts.ScriptTarget.Latest, true);
  } catch (e) {
  }
  cache.set(file, parsed);
  return parsed;
}

// Whether `name` in `target` traces to a declaration tagged @publicApi. The
// declaration may sit behind indirection — chained `export { X } from`,
// `export *` barrels (index.ts included), or `import { X }` feeding a local
// `export { X }` clause — so every hop is followed; `visited` (file|name pairs)
// stops circular barrels. Merged declarations (e.g. interface + namespace)
// count as tagged when any declaration of the name carries the tag.
function declarationHasPublicApiTag(target, name, cache, visited) {
  const key = target.fileName + '|' + name;
  if (visited.has(key)) {
    return false;
  }
  visited.add(key);
  let tagged = false;
  ts.forEachChild(target, (node) => {
    tagged = tagged
      || (namesDeclaration(node, name) && hasPublicApiTag(node, target))
      || followsTaggedDeclaration(node, name, target, cache, visited);
  });
  return tagged;
}

// One indirection hop toward the declaration of `name` visible in `target`.
function followsTaggedDeclaration(node, name, target, cache, visited) {
  const dir = path.dirname(target.fileName);
  if (ts.isExportDeclaration(node)) {
    const specifier = node.moduleSpecifier && node.moduleSpecifier.text;
    if (!node.exportClause) {
      return !!specifier && taggedInModule(dir, specifier, name, cache, visited);
    }
    if (!ts.isNamedExports(node.exportClause)) {
      return false;
    }
    for (const el of node.exportClause.elements) {
      if (el.name.text !== name) {
        continue;
      }
      const original = el.propertyName ? el.propertyName.text : el.name.text;
      return specifier
        ? taggedInModule(dir, specifier, original, cache, visited)
        : declarationHasPublicApiTag(target, original, cache, visited);
    }
    return false;
  }
  if (ts.isImportDeclaration(node) && node.importClause
      && node.importClause.namedBindings
      && ts.isNamedImports(node.importClause.namedBindings)) {
    const specifier = node.moduleSpecifier && node.moduleSpecifier.text;
    for (const el of node.importClause.namedBindings.elements) {
      if (el.name.text !== name) {
        continue;
      }
      const original = el.propertyName ? el.propertyName.text : el.name.text;
      return !!specifier && taggedInModule(dir, specifier, original, cache, visited);
    }
  }
  return false;
}

function taggedInModule(dir, specifier, name, cache, visited) {
  const resolved = resolveModule(dir, specifier);
  const target = resolved ? parseCached(resolved, cache) : null;
  return !!target && declarationHasPublicApiTag(target, name, cache, visited);
}

function namesDeclaration(node, name) {
  if ((ts.isClassDeclaration(node) || ts.isInterfaceDeclaration(node)
      || ts.isTypeAliasDeclaration(node) || ts.isEnumDeclaration(node)
      || ts.isFunctionDeclaration(node)) && node.name) {
    return node.name.text === name;
  }
  if (ts.isModuleDeclaration(node) && node.name && ts.isIdentifier(node.name)) {
    return node.name.text === name;
  }
  if (ts.isVariableStatement(node)) {
    for (const decl of node.declarationList.declarations) {
      if (ts.isIdentifier(decl.name) && decl.name.text === name) {
        return true;
      }
    }
  }
  return false;
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
    const meta = jsdoc(node, sourceFile);
    return {
      name: node.name.text,
      kind: 'interface',
      doc: meta.summary,
      deprecated: meta.deprecated,
      deprecationReason: meta.deprecationReason,
      extends: heritageNames(node, ts.SyntaxKind.ExtendsKeyword, sourceFile),
      members: node.members.map((m) => describeMember(m, sourceFile)).filter(Boolean),
      file,
    };
  }
  if (ts.isClassDeclaration(node) && node.name) {
    const meta = jsdoc(node, sourceFile);
    return {
      name: node.name.text,
      kind: 'class',
      // Angular role (Service, Component, …) so `docs --services` can keep only
      // @Injectable services; reuses the same classifier the inventory path uses.
      role: classRole(node, sourceFile),
      doc: meta.summary,
      deprecated: meta.deprecated,
      deprecationReason: meta.deprecationReason,
      implements: heritageNames(node, ts.SyntaxKind.ImplementsKeyword, sourceFile),
      extends: heritageNames(node, ts.SyntaxKind.ExtendsKeyword, sourceFile),
      members: node.members.map((m) => describeMember(m, sourceFile)).filter(Boolean),
      file,
    };
  }
  if (ts.isTypeAliasDeclaration(node)) {
    const meta = jsdoc(node, sourceFile);
    return {
      name: node.name.text,
      kind: 'type',
      doc: meta.summary,
      deprecated: meta.deprecated,
      deprecationReason: meta.deprecationReason,
      definition: collapse(node.type.getText(sourceFile)),
      file,
    };
  }
  if (ts.isEnumDeclaration(node)) {
    const meta = jsdoc(node, sourceFile);
    return {
      name: node.name.text,
      kind: 'enum',
      doc: meta.summary,
      deprecated: meta.deprecated,
      deprecationReason: meta.deprecationReason,
      members: enumMembers(node, sourceFile),
      file,
    };
  }
  if (ts.isFunctionDeclaration(node) && node.name) {
    const meta = jsdoc(node, sourceFile);
    return {
      name: node.name.text,
      kind: 'function',
      doc: meta.summary,
      deprecated: meta.deprecated,
      deprecationReason: meta.deprecationReason,
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
      const meta = jsdoc(node, sourceFile);
      return {
        name: d.name.text,
        kind: 'const',
        doc: meta.summary,
        deprecated: meta.deprecated,
        deprecationReason: meta.deprecationReason,
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
    // collapse() like every other text field, so a multi-line string-literal
    // description never carries a raw newline into a Markdown table row.
    description = collapse(init.arguments[0].text);
  }
  const meta = jsdoc(statement, sourceFile);
  return {
    name: decl.name.text,
    kind: 'injection-token',
    doc: meta.summary,
    deprecated: meta.deprecated,
    deprecationReason: meta.deprecationReason,
    contract,
    description,
    file,
  };
}

// ---------------------------------------------------------------------------
// inventory: a complete census of developer-authored declarations in a file,
// for the `surfaceq inventory` command. Unlike document/discover, it reports
// EVERY top-level declaration (exported or not) and classifies each by its
// Angular role (Component, Service, Guard, …) on top of its TypeScript kind.
// ---------------------------------------------------------------------------

function inventory(file) {
  const items = [];
  const warnings = [];
  const errors = [];
  let content;
  try {
    content = fs.readFileSync(file, 'utf8');
  } catch (e) {
    return { items, warnings, errors };
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
    return { items, warnings, errors };
  }
  ts.forEachChild(sourceFile, (node) => {
    for (const item of inventoryNode(node, sourceFile, file)) {
      items.push(item);
    }
  });
  return { items, warnings, errors };
}

function inventoryNode(node, sourceFile, file) {
  const exported = hasExportModifier(node);
  if (ts.isClassDeclaration(node) && node.name) {
    return [inventoryItem(node.name.text, 'class', classRole(node, sourceFile), exported, node, sourceFile, file)];
  }
  if (ts.isInterfaceDeclaration(node)) {
    return [inventoryItem(node.name.text, 'interface', 'Interface', exported, node, sourceFile, file)];
  }
  if (ts.isEnumDeclaration(node)) {
    return [inventoryItem(node.name.text, 'enum', 'Enum', exported, node, sourceFile, file)];
  }
  if (ts.isTypeAliasDeclaration(node)) {
    return [inventoryItem(node.name.text, 'type', 'Type Alias', exported, node, sourceFile, file)];
  }
  if (ts.isFunctionDeclaration(node) && node.name) {
    const role = functionalRole(node.type ? node.type.getText(sourceFile) : '') || 'Function';
    return [inventoryItem(node.name.text, 'function', role, exported, node, sourceFile, file)];
  }
  if (ts.isVariableStatement(node)) {
    return variableItems(node, exported, sourceFile, file);
  }
  return [];
}

function variableItems(statement, exported, sourceFile, file) {
  const kind = variableKeyword(statement);
  const out = [];
  for (const decl of statement.declarationList.declarations) {
    if (!ts.isIdentifier(decl.name)) {
      continue;
    }
    // Role comes from the type annotation, or — for the inline `(...) as Fn`
    // / `... satisfies Fn` form common in functional guards — the assertion type.
    const typeText = decl.type ? decl.type.getText(sourceFile) : assertionType(decl, sourceFile);
    const role = isInjectionToken(decl, sourceFile)
      ? 'Injection Token'
      : functionalRole(typeText) || 'Constant';
    out.push(inventoryItem(decl.name.text, kind, role, exported, statement, sourceFile, file));
  }
  return out;
}

function variableKeyword(statement) {
  const flags = statement.declarationList.flags;
  if (flags & ts.NodeFlags.Const) {
    return 'const';
  }
  if (flags & ts.NodeFlags.Let) {
    return 'let';
  }
  return 'var';
}

function inventoryItem(name, kind, role, exported, jsdocNode, sourceFile, file) {
  const meta = jsdoc(jsdocNode, sourceFile);
  return {
    name,
    kind,
    role,
    exported,
    doc: meta.summary,
    deprecated: meta.deprecated,
    deprecationReason: meta.deprecationReason,
    file,
  };
}

// Role precedence: structural decorators (Component/Directive/Pipe/NgModule)
// first, then router/HTTP roles inferred from implemented interfaces, then a
// plain @Injectable Service, else just a class. A guard/resolver/interceptor
// interface therefore outranks @Injectable on the same class.
function classRole(node, sourceFile) {
  const decorated = decoratorRole(node);
  if (decorated) {
    return decorated;
  }
  const implemented = heritageRole(heritageNames(node, ts.SyntaxKind.ImplementsKeyword, sourceFile));
  if (implemented) {
    return implemented;
  }
  if (hasDecorator(node, 'Injectable')) {
    return 'Service';
  }
  return 'Class';
}

function decoratorRole(node) {
  if (hasDecorator(node, 'Component')) {
    return 'Component';
  }
  if (hasDecorator(node, 'Directive')) {
    return 'Directive';
  }
  if (hasDecorator(node, 'Pipe')) {
    return 'Pipe';
  }
  if (hasDecorator(node, 'NgModule')) {
    return 'NgModule';
  }
  return '';
}

function hasDecorator(node, name) {
  for (const dec of getDecorators(node)) {
    // baseTypeName strips a namespace qualifier so `@ng.Component` (namespace
    // import) matches the same way `@Component` does — consistent with heritage.
    if (baseTypeName(decoratorName(dec)) === name) {
      return true;
    }
  }
  return false;
}

// The asserted type of an `(expr) as T` or `expr satisfies T` initializer, used
// to recover the Angular role when a const has no explicit type annotation.
function assertionType(decl, sourceFile) {
  const init = decl.initializer;
  if (init && (ts.isAsExpression(init) || ts.isSatisfiesExpression(init)) && init.type) {
    return init.type.getText(sourceFile);
  }
  return '';
}

function getDecorators(node) {
  if (typeof ts.canHaveDecorators === 'function' && typeof ts.getDecorators === 'function') {
    return (ts.canHaveDecorators(node) ? ts.getDecorators(node) : undefined) || [];
  }
  return node.decorators || [];
}

function decoratorName(dec) {
  let expr = dec.expression;
  if (expr && ts.isCallExpression(expr)) {
    expr = expr.expression;
  }
  if (expr && ts.isIdentifier(expr)) {
    return expr.text;
  }
  return expr && expr.getText ? expr.getText() : '';
}

function heritageRole(names) {
  for (const raw of names) {
    const base = baseTypeName(raw);
    if (GUARD_INTERFACES.has(base)) {
      return 'Guard';
    }
    if (base === 'Resolve') {
      return 'Resolver';
    }
    if (base === 'HttpInterceptor') {
      return 'Interceptor';
    }
  }
  return '';
}

function functionalRole(typeText) {
  const base = baseTypeName(typeText);
  if (GUARD_FNS.has(base)) {
    return 'Guard';
  }
  if (base === 'ResolveFn') {
    return 'Resolver';
  }
  if (base === 'HttpInterceptorFn') {
    return 'Interceptor';
  }
  return '';
}

function isInjectionToken(decl, sourceFile) {
  const init = decl.initializer;
  if (!init || !ts.isNewExpression(init)) {
    return false;
  }
  const expr = init.expression.getText(sourceFile);
  return expr === 'InjectionToken' || expr.endsWith('.InjectionToken');
}

// "ng.CanActivateFn<Foo>" -> "CanActivateFn": drop type args, then namespace.
function baseTypeName(text) {
  if (!text) {
    return '';
  }
  let name = text.trim();
  const lt = name.indexOf('<');
  if (lt >= 0) {
    name = name.slice(0, lt);
  }
  const dot = name.lastIndexOf('.');
  if (dot >= 0) {
    name = name.slice(dot + 1);
  }
  return name.trim();
}

const GUARD_INTERFACES = new Set([
  'CanActivate', 'CanActivateChild', 'CanDeactivate', 'CanMatch', 'CanLoad',
]);
const GUARD_FNS = new Set([
  'CanActivateFn', 'CanActivateChildFn', 'CanDeactivateFn', 'CanMatchFn', 'CanLoadFn',
]);

function describeMember(member, sourceFile) {
  const readonly = hasModifierOfKind(member, ts.SyntaxKind.ReadonlyKeyword);
  const meta = jsdoc(member, sourceFile);
  if (ts.isPropertySignature(member) || ts.isPropertyDeclaration(member)) {
    if (!isPublic(member) || !memberName(member, sourceFile)) {
      return null;
    }
    return {
      memberKind: 'property',
      name: memberName(member, sourceFile),
      type: propertyType(member, sourceFile),
      optional: !!member.questionToken,
      readonly,
      deprecated: meta.deprecated,
      deprecationReason: meta.deprecationReason,
      doc: meta.summary,
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
      deprecated: meta.deprecated,
      deprecationReason: meta.deprecationReason,
      doc: meta.summary,
    };
  }
  if (ts.isGetAccessorDeclaration(member) || ts.isSetAccessorDeclaration(member)) {
    if (!isPublic(member) || !memberName(member, sourceFile)) {
      return null;
    }
    return {
      memberKind: 'property',
      name: memberName(member, sourceFile),
      type: propertyType(member, sourceFile),
      optional: false,
      readonly: ts.isGetAccessorDeclaration(member),
      deprecated: meta.deprecated,
      deprecationReason: meta.deprecationReason,
      doc: meta.summary,
    };
  }
  // A call signature makes the interface itself callable — i.e. a function exposed
  // via an interface + InjectionToken. It has no name; the host renders it as the
  // interface's call signature rather than dropping it (which left "no members").
  if (ts.isCallSignatureDeclaration(member)) {
    return {
      memberKind: 'call',
      name: '',
      parameters: member.parameters.map((p) => describeParameter(p, sourceFile)),
      returnType: member.type ? collapse(member.type.getText(sourceFile)) : '',
      optional: false,
      deprecated: meta.deprecated,
      deprecationReason: meta.deprecationReason,
      doc: meta.summary,
    };
  }
  return null;
}

// Class properties often have no explicit type annotation, carrying the value
// type in the initializer's type argument instead — e.g. Angular's
// `input<T>()`, `output<T>()`, `signal<T>()`, `model<T>()`, `input.required<T>()`.
// Fall back to that type argument. computed(() => …) has no type argument and
// stays blank rather than dumping the arrow body.
function propertyType(member, sourceFile) {
  if (member.type) {
    return collapse(member.type.getText(sourceFile));
  }
  const init = member.initializer;
  if (init && ts.isCallExpression(init) && init.typeArguments && init.typeArguments.length > 0) {
    return collapse(init.typeArguments[0].getText(sourceFile));
  }
  return '';
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
    const meta = jsdoc(m, sourceFile);
    members.push({
      name,
      value,
      deprecated: meta.deprecated,
      deprecationReason: meta.deprecationReason,
      doc: meta.summary,
    });
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

// Single pass over a node's leading /** */ block: returns the summary text,
// whether it carries an @deprecated tag, and that tag's trailing reason.
function jsdoc(node, sourceFile) {
  const ranges = ts.getLeadingCommentRanges(sourceFile.text, node.getFullStart()) || [];
  for (let i = ranges.length - 1; i >= 0; i--) {
    const text = sourceFile.text.slice(ranges[i].pos, ranges[i].end);
    if (text.startsWith('/**')) {
      return parseJsDocBlock(text);
    }
  }
  return { summary: '', deprecated: false, deprecationReason: '' };
}

function parseJsDocBlock(text) {
  const inner = text.replace(/^\/\*\*/, '').replace(/\*\/$/, '');
  const summaryLines = [];
  const deprecationLines = [];
  let deprecated = false;
  let mode = 'summary';
  for (const raw of inner.split('\n')) {
    const line = raw.replace(/^\s*\*?/, '').trim();
    if (/^@deprecated(\s|$)/.test(line)) {
      deprecated = true;
      mode = 'deprecated';
      const rest = line.slice('@deprecated'.length).trim();
      if (rest) {
        deprecationLines.push(rest);
      }
      continue;
    }
    if (line.startsWith('@')) {
      mode = 'tag';
      continue;
    }
    if (!line) {
      continue;
    }
    if (mode === 'summary') {
      summaryLines.push(line);
    } else if (mode === 'deprecated') {
      deprecationLines.push(line);
    }
  }
  return {
    summary: collapse(summaryLines.join(' ')),
    deprecated,
    deprecationReason: collapse(deprecationLines.join(' ')),
  };
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
