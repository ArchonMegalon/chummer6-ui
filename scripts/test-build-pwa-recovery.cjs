#!/usr/bin/env node
'use strict';

const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(
  path.join(__dirname, '..', 'Chummer.Blazor', 'wwwroot', 'js', 'build-pwa-recovery.js'),
  'utf8');

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

async function run() {
  let pickerCalls = 0;
  let nextTimer = 0;
  const timers = new Map();
  class HarnessElement {}
  const saveButton = new HarnessElement();
  saveButton.closest = selector => selector.includes('data-build-pwa-recovery-save')
    ? saveButton
    : null;
  const document = {
    addEventListener() {},
    createElement() { throw new Error('Fallback download was not expected.'); },
    body: { appendChild() {} }
  };
  const window = {
    navigator: { userActivation: { isActive: true } },
    showSaveFilePicker: async () => {
      pickerCalls += 1;
      return { createWritable: async () => ({ write: async () => {}, close: async () => {} }) };
    },
    setTimeout(callback) {
      nextTimer += 1;
      timers.set(nextTimer, callback);
      return nextTimer;
    },
    clearTimeout(timer) { timers.delete(timer); }
  };
  vm.runInNewContext(source, {
    window,
    document,
    Element: HarnessElement,
    ArrayBuffer,
    Uint8Array,
    Blob,
    URL,
    Date,
    Error,
    Promise,
    Object,
    Set
  }, { filename: 'build-pwa-recovery.js' });

  window.chummerDownloads.captureRecoverySaveGesture({ isTrusted: false, target: saveButton });
  assert(pickerCalls === 0 && window.chummerDownloads._pendingRecoveryPicker === null,
    'An untrusted synthetic click opened or retained a file-system handle.');

  window.chummerDownloads.captureRecoverySaveGesture({ isTrusted: true, target: saveButton });
  await Promise.resolve();
  const pending = window.chummerDownloads._pendingRecoveryPicker;
  assert(pickerCalls === 1 && pending?.handle,
    'A trusted active user gesture did not bind the picker handle.');
  const expiration = timers.get(pending.expirationTimer);
  assert(typeof expiration === 'function', 'The pending picker had no expiry timer.');
  expiration();
  assert(window.chummerDownloads._pendingRecoveryPicker === null
    && pending.handle === null
    && pending.error === null
    && pending.settledPromise === null,
  'Picker expiry retained a native handle or promise reference.');

  window.navigator.userActivation.isActive = false;
  window.chummerDownloads.captureRecoverySaveGesture({ isTrusted: true, target: saveButton });
  assert(pickerCalls === 1 && window.chummerDownloads._pendingRecoveryPicker === null,
    'A trusted event without active user activation opened the picker.');
}

run()
  .then(() => console.log('Build PWA trusted recovery gesture and handle expiry checks passed.'))
  .catch(error => {
    console.error(error && error.stack ? error.stack : error);
    process.exitCode = 1;
  });
