#!/usr/bin/env node
'use strict';

const readline = require('readline');

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
    const response = { jsonrpc: '2.0', id: msg.id, result: 'pong' };
    process.stdout.write(JSON.stringify(response) + '\n');
  }
});
