#!/usr/bin/env node
/**
 * Client-side encrypted custody for the exact signed macOS flagship DMG.
 *
 * The seal command needs only an RSA public key. The matching private key is
 * deliberately reserved for a separate protected assembly/publication lane.
 * No network or publication operation is implemented here.
 */

import {
  closeSync,
  constants as fsConstants,
  fstatSync,
  fsyncSync,
  linkSync,
  lstatSync,
  mkdirSync,
  openSync,
  readFileSync,
  readSync,
  realpathSync,
  renameSync,
  rmSync,
  unlinkSync,
  writeSync,
} from "node:fs";
import {
  constants as cryptoConstants,
  createCipheriv,
  createDecipheriv,
  createHash,
  createPrivateKey,
  createPublicKey,
  privateDecrypt,
  publicEncrypt,
  randomBytes,
} from "node:crypto";
import { basename, dirname, join, resolve } from "node:path";

const CONTRACT = "chummer6-ui.macos-flagship-candidate-escrow.v1";
const WORKFLOW = ".github/workflows/macos-flagship-evidence.yml";
const ENVIRONMENT = "macos-flagship-evidence";
const RERUN_POLICY = "same-actor-only";
const REPOSITORY = "ArchonMegalon/chummer6-ui";
const REF = "refs/heads/main";
const RID = "osx-arm64";
const ARTIFACT_ID = "avalonia-osx-arm64-installer";
const CANDIDATE_FILE = "chummer-avalonia-osx-arm64-installer.dmg";
const CIPHERTEXT_FILE = `${CANDIDATE_FILE}.aes256gcm`;
const RECEIPT_FILE = "MACOS_FLAGSHIP_CANDIDATE_ESCROW.generated.json";
const MAX_CANDIDATE_BYTES = 512 * 1024 * 1024;
const MAX_JSON_BYTES = 1024 * 1024;
const BUFFER_BYTES = 1024 * 1024;
const SHA256 = /^[0-9a-f]{64}$/;
const COMMIT = /^[0-9a-f]{40}$/;
const POSITIVE_INTEGER = /^[1-9][0-9]*$/;
const PORTABLE = /^[A-Za-z0-9][A-Za-z0-9._+-]{0,127}$/;
const LOGIN =
  /^(?:github-actions\[bot\]|[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?)$/;

function fail(message) {
  throw new Error(message);
}

function canonicalValue(value) {
  if (Array.isArray(value)) {
    return value.map(canonicalValue);
  }
  if (value !== null && typeof value === "object") {
    return Object.fromEntries(
      Object.keys(value)
        .sort()
        .map((key) => [key, canonicalValue(value[key])]),
    );
  }
  return value;
}

function canonicalJson(value) {
  return JSON.stringify(canonicalValue(value));
}

function sha256(value) {
  return createHash("sha256").update(value).digest("hex");
}

function exactKeys(value, expected, label) {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    fail(`${label} must be an object`);
  }
  const observed = Object.keys(value).sort();
  const wanted = [...expected].sort();
  if (JSON.stringify(observed) !== JSON.stringify(wanted)) {
    fail(`${label} has missing or extra fields`);
  }
}

function boundedString(value, label, pattern, maximum = 2048) {
  if (
    typeof value !== "string" ||
    value.length < 1 ||
    value.length > maximum ||
    [...value].some((character) => character.codePointAt(0) < 32) ||
    (pattern && !pattern.test(value))
  ) {
    fail(`${label} is invalid`);
  }
  return value;
}

function positiveInteger(value, label, maximum = Number.MAX_SAFE_INTEGER) {
  if (
    !Number.isSafeInteger(value) ||
    value < 1 ||
    value > maximum
  ) {
    fail(`${label} must be a bounded positive integer`);
  }
  return value;
}

function strictBase64(value, label, expectedBytes = null) {
  boundedString(value, label, /^[A-Za-z0-9+/]+={0,2}$/, 16384);
  if (value.length % 4 !== 0) {
    fail(`${label} is not padded canonical base64`);
  }
  const decoded = Buffer.from(value, "base64");
  if (
    decoded.toString("base64") !== value ||
    (expectedBytes !== null && decoded.length !== expectedBytes)
  ) {
    fail(`${label} is not canonical base64`);
  }
  return decoded;
}

function parseOptions(argv, expected) {
  const allowed = new Set(expected);
  const parsed = {};
  for (let index = 0; index < argv.length; index += 2) {
    const key = argv[index];
    const value = argv[index + 1];
    if (
      typeof key !== "string" ||
      !key.startsWith("--") ||
      typeof value !== "string" ||
      !allowed.has(key) ||
      Object.hasOwn(parsed, key)
    ) {
      fail("arguments contain an unknown, duplicate, or missing option");
    }
    parsed[key] = value;
  }
  if (
    argv.length !== expected.length * 2 ||
    expected.some((key) => !Object.hasOwn(parsed, key))
  ) {
    fail(`required options: ${expected.join(", ")}`);
  }
  return parsed;
}

function noFollowRead(path, label, maximum) {
  let descriptor;
  try {
    const before = lstatSync(path, { bigint: true });
    if (!before.isFile() || before.isSymbolicLink()) {
      fail(`${label} must be a regular non-symlink file`);
    }
    descriptor = openSync(
      path,
      fsConstants.O_RDONLY | fsConstants.O_NOFOLLOW,
    );
    const opened = fstatSync(descriptor, { bigint: true });
    if (
      !opened.isFile() ||
      opened.dev !== before.dev ||
      opened.ino !== before.ino ||
      opened.size < 1n ||
      opened.size > BigInt(maximum)
    ) {
      fail(`${label} changed before it was opened or has an invalid size`);
    }
    const data = readFileSync(descriptor);
    const after = fstatSync(descriptor, { bigint: true });
    if (
      after.dev !== opened.dev ||
      after.ino !== opened.ino ||
      after.size !== opened.size ||
      after.mtimeNs !== opened.mtimeNs ||
      after.ctimeNs !== opened.ctimeNs
    ) {
      fail(`${label} changed while it was read`);
    }
    return data;
  } finally {
    if (descriptor !== undefined) {
      closeSync(descriptor);
    }
  }
}

function outputDirectory(path) {
  const absolute = resolve(path);
  const realParent = realpathSync(dirname(absolute));
  const expectedRealPath = join(realParent, basename(absolute));
  try {
    lstatSync(absolute);
    fail("escrow output directory must not already exist");
  } catch (error) {
    if (error.code !== "ENOENT") {
      throw error;
    }
  }
  mkdirSync(absolute, { mode: 0o700 });
  if (realpathSync(absolute) !== expectedRealPath) {
    fail("escrow output directory leaf must not be a symlink");
  }
  return absolute;
}

function writeAll(descriptor, value) {
  let offset = 0;
  while (offset < value.length) {
    offset += writeSync(
      descriptor,
      value,
      offset,
      value.length - offset,
      null,
    );
  }
}

function writeCanonical(path, payload) {
  const temporary = join(dirname(path), `.${basename(path)}.tmp-${process.pid}`);
  let descriptor;
  try {
    descriptor = openSync(
      temporary,
      fsConstants.O_WRONLY |
        fsConstants.O_CREAT |
        fsConstants.O_EXCL |
        fsConstants.O_NOFOLLOW,
      0o600,
    );
    writeAll(descriptor, Buffer.from(canonicalJson(payload), "utf8"));
    fsyncSync(descriptor);
    closeSync(descriptor);
    descriptor = undefined;
    renameSync(temporary, path);
  } finally {
    if (descriptor !== undefined) {
      closeSync(descriptor);
    }
    try {
      unlinkSync(temporary);
    } catch (error) {
      if (error.code !== "ENOENT") {
        throw error;
      }
    }
  }
}

function keyAuthority(key, expectedPin, label) {
  if (key.asymmetricKeyType !== "rsa") {
    fail(`${label} must be an RSA key`);
  }
  const details = key.asymmetricKeyDetails || {};
  const modulusBits = details.modulusLength;
  const exponent = details.publicExponent;
  if (
    !Number.isSafeInteger(modulusBits) ||
    modulusBits < 3072 ||
    modulusBits > 8192 ||
    modulusBits % 256 !== 0 ||
    exponent !== 65537n
  ) {
    fail(`${label} must be RSA 3072-8192 with exponent 65537`);
  }
  const publicKey = key.type === "public" ? key : createPublicKey(key);
  const spki = publicKey.export({ format: "der", type: "spki" });
  const spkiSha256 = sha256(spki);
  if (spkiSha256 !== expectedPin) {
    fail(`${label} SPKI SHA-256 does not match protected authority`);
  }
  return { modulusBits, publicKey, spkiSha256 };
}

function verifyRecipient(argv) {
  const options = parseOptions(argv, [
    "--public-key",
    "--expected-spki-sha256",
  ]);
  const expectedPin = boundedString(
    options["--expected-spki-sha256"],
    "expected recipient SPKI SHA-256",
    SHA256,
    64,
  );
  const publicKeyBytes = noFollowRead(
    resolve(options["--public-key"]),
    "escrow recipient public key",
    32 * 1024,
  );
  const authority = keyAuthority(
    createPublicKey(publicKeyBytes),
    expectedPin,
    "escrow recipient public key",
  );
  process.stdout.write(
    `macos_flagship_escrow_recipient_spki_sha256=${authority.spkiSha256}\n`,
  );
}

function snapshotIdentity(stats) {
  return {
    ctimeNs: stats.ctimeNs,
    dev: stats.dev,
    ino: stats.ino,
    mtimeNs: stats.mtimeNs,
    size: stats.size,
  };
}

function identitiesEqual(left, right) {
  return Object.keys(left).every((key) => left[key] === right[key]);
}

function seal(argv) {
  const options = parseOptions(argv, [
    "--candidate",
    "--output-dir",
    "--recipient-public-key",
    "--recipient-spki-sha256",
    "--expected-candidate-sha256",
    "--expected-candidate-size",
    "--candidate-id",
    "--generation-id",
    "--release-version",
    "--repository",
    "--workflow",
    "--environment",
    "--ref",
    "--sha",
    "--actor",
    "--triggering-actor",
    "--run-id",
    "--run-attempt",
  ]);
  const candidatePath = resolve(options["--candidate"]);
  if (basename(candidatePath) !== CANDIDATE_FILE) {
    fail("candidate file name is not the governed macOS DMG");
  }
  const expectedCandidateSha = boundedString(
    options["--expected-candidate-sha256"],
    "expected candidate SHA-256",
    SHA256,
    64,
  );
  if (!POSITIVE_INTEGER.test(options["--expected-candidate-size"])) {
    fail("expected candidate size is invalid");
  }
  const expectedCandidateSize = Number(options["--expected-candidate-size"]);
  positiveInteger(
    expectedCandidateSize,
    "expected candidate size",
    MAX_CANDIDATE_BYTES,
  );
  const recipientPin = boundedString(
    options["--recipient-spki-sha256"],
    "recipient SPKI SHA-256",
    SHA256,
    64,
  );
  const publicKeyBytes = noFollowRead(
    resolve(options["--recipient-public-key"]),
    "escrow recipient public key",
    32 * 1024,
  );
  const authority = keyAuthority(
    createPublicKey(publicKeyBytes),
    recipientPin,
    "escrow recipient public key",
  );
  const candidate = {
    artifactId: ARTIFACT_ID,
    fileName: CANDIDATE_FILE,
    sha256: expectedCandidateSha,
    sizeBytes: expectedCandidateSize,
  };
  const producer = {
    actor: boundedString(options["--actor"], "producer actor", LOGIN, 160),
    environment: boundedString(
      options["--environment"],
      "producer environment",
      null,
      160,
    ),
    ref: boundedString(options["--ref"], "producer ref", null, 240),
    repository: boundedString(
      options["--repository"],
      "producer repository",
      null,
      200,
    ),
    rerunPolicy: RERUN_POLICY,
    runAttempt: boundedString(
      options["--run-attempt"],
      "producer run attempt",
      POSITIVE_INTEGER,
      20,
    ),
    runId: boundedString(
      options["--run-id"],
      "producer run ID",
      POSITIVE_INTEGER,
      20,
    ),
    sha: boundedString(options["--sha"], "producer SHA", COMMIT, 40),
    triggeringActor: boundedString(
      options["--triggering-actor"],
      "producer triggering actor",
      LOGIN,
      160,
    ),
    workflow: boundedString(
      options["--workflow"],
      "producer workflow",
      null,
      200,
    ),
  };
  if (
    producer.repository !== REPOSITORY ||
    producer.ref !== REF ||
    producer.workflow !== WORKFLOW ||
    producer.environment !== ENVIRONMENT ||
    producer.triggeringActor !== producer.actor
  ) {
    fail("producer is outside the governed macOS workflow boundary");
  }
  const aad = {
    candidate,
    candidateId: boundedString(
      options["--candidate-id"],
      "candidate ID",
      PORTABLE,
      128,
    ),
    generationId: boundedString(
      options["--generation-id"],
      "generation ID",
      PORTABLE,
      128,
    ),
    producer,
    recipientSpkiSha256: authority.spkiSha256,
    releaseVersion: boundedString(
      options["--release-version"],
      "release version",
      PORTABLE,
      128,
    ),
    rid: RID,
  };
  const aadBytes = Buffer.from(canonicalJson(aad), "utf8");
  const aadSha256 = sha256(aadBytes);
  const oaepLabel = Buffer.from(`${CONTRACT}\0${aadSha256}`, "utf8");
  const contentKey = randomBytes(32);
  const nonce = randomBytes(12);
  let outputRoot;
  let candidateDescriptor;
  let ciphertextDescriptor;
  try {
    outputRoot = outputDirectory(options["--output-dir"]);
    const ciphertextPath = join(outputRoot, CIPHERTEXT_FILE);
    const before = lstatSync(candidatePath, { bigint: true });
    if (!before.isFile() || before.isSymbolicLink()) {
      fail("candidate must be a regular non-symlink file");
    }
    candidateDescriptor = openSync(
      candidatePath,
      fsConstants.O_RDONLY | fsConstants.O_NOFOLLOW,
    );
    const opened = fstatSync(candidateDescriptor, { bigint: true });
    if (
      !opened.isFile() ||
      opened.dev !== before.dev ||
      opened.ino !== before.ino ||
      opened.size !== BigInt(expectedCandidateSize)
    ) {
      fail("candidate identity or size changed before encryption");
    }
    ciphertextDescriptor = openSync(
      ciphertextPath,
      fsConstants.O_WRONLY |
        fsConstants.O_CREAT |
        fsConstants.O_EXCL |
        fsConstants.O_NOFOLLOW,
      0o600,
    );
    const cipher = createCipheriv("aes-256-gcm", contentKey, nonce);
    cipher.setAAD(aadBytes);
    const plaintextHash = createHash("sha256");
    const ciphertextHash = createHash("sha256");
    const buffer = Buffer.allocUnsafe(BUFFER_BYTES);
    let plaintextSize = 0;
    let ciphertextSize = 0;
    for (;;) {
      const count = readSync(candidateDescriptor, buffer, 0, buffer.length);
      if (count === 0) {
        break;
      }
      const chunk = buffer.subarray(0, count);
      plaintextHash.update(chunk);
      plaintextSize += count;
      const encrypted = cipher.update(chunk);
      ciphertextHash.update(encrypted);
      ciphertextSize += encrypted.length;
      writeAll(ciphertextDescriptor, encrypted);
    }
    const finalChunk = cipher.final();
    ciphertextHash.update(finalChunk);
    ciphertextSize += finalChunk.length;
    writeAll(ciphertextDescriptor, finalChunk);
    fsyncSync(ciphertextDescriptor);
    closeSync(ciphertextDescriptor);
    ciphertextDescriptor = undefined;
    const after = fstatSync(candidateDescriptor, { bigint: true });
    if (
      !identitiesEqual(snapshotIdentity(opened), snapshotIdentity(after)) ||
      plaintextSize !== expectedCandidateSize ||
      plaintextHash.digest("hex") !== expectedCandidateSha
    ) {
      fail("candidate changed or did not match its signed evidence identity");
    }
    closeSync(candidateDescriptor);
    candidateDescriptor = undefined;
    if (ciphertextSize !== expectedCandidateSize) {
      fail("AES-GCM ciphertext size did not preserve the plaintext byte count");
    }
    const wrappedKey = publicEncrypt(
      {
        key: authority.publicKey,
        oaepHash: "sha256",
        oaepLabel,
        padding: cryptoConstants.RSA_PKCS1_OAEP_PADDING,
      },
      contentKey,
    );
    const receipt = {
      aad,
      aadSha256,
      candidate,
      ciphertext: {
        fileName: CIPHERTEXT_FILE,
        sha256: ciphertextHash.digest("hex"),
        sizeBytes: ciphertextSize,
      },
      contractName: CONTRACT,
      contractVersion: 1,
      encryption: {
        authenticationTagBase64: cipher.getAuthTag().toString("base64"),
        cipher: "aes-256-gcm",
        keyWrap: "rsa-oaep-sha256",
        nonceBase64: nonce.toString("base64"),
        oaepLabelSha256: sha256(oaepLabel),
        wrappedKeyBase64: wrappedKey.toString("base64"),
      },
      recipient: {
        keyType: "rsa",
        modulusBits: authority.modulusBits,
        publicExponent: 65537,
        spkiSha256: authority.spkiSha256,
      },
      status: "sealed",
    };
    writeCanonical(join(outputRoot, RECEIPT_FILE), receipt);
    process.stdout.write(`macos_flagship_escrow_receipt=${join(outputRoot, RECEIPT_FILE)}\n`);
    process.stdout.write(`macos_flagship_escrow_ciphertext=${ciphertextPath}\n`);
  } catch (error) {
    if (outputRoot !== undefined) {
      rmSync(outputRoot, { recursive: true, force: true });
    }
    throw error;
  } finally {
    contentKey.fill(0);
    if (candidateDescriptor !== undefined) {
      closeSync(candidateDescriptor);
    }
    if (ciphertextDescriptor !== undefined) {
      closeSync(ciphertextDescriptor);
    }
  }
}

function validateReceipt(receipt, raw) {
  if (raw.toString("utf8") !== canonicalJson(receipt)) {
    fail("escrow receipt is not exact canonical JSON");
  }
  exactKeys(
    receipt,
    [
      "aad",
      "aadSha256",
      "candidate",
      "ciphertext",
      "contractName",
      "contractVersion",
      "encryption",
      "recipient",
      "status",
    ],
    "escrow receipt",
  );
  if (
    receipt.contractName !== CONTRACT ||
    receipt.contractVersion !== 1 ||
    receipt.status !== "sealed"
  ) {
    fail("escrow receipt identity or status is invalid");
  }
  exactKeys(
    receipt.candidate,
    ["artifactId", "fileName", "sha256", "sizeBytes"],
    "escrow candidate",
  );
  if (
    receipt.candidate.artifactId !== ARTIFACT_ID ||
    receipt.candidate.fileName !== CANDIDATE_FILE ||
    !SHA256.test(receipt.candidate.sha256)
  ) {
    fail("escrow candidate identity is invalid");
  }
  positiveInteger(
    receipt.candidate.sizeBytes,
    "escrow candidate size",
    MAX_CANDIDATE_BYTES,
  );
  exactKeys(
    receipt.ciphertext,
    ["fileName", "sha256", "sizeBytes"],
    "escrow ciphertext",
  );
  if (
    receipt.ciphertext.fileName !== CIPHERTEXT_FILE ||
    !SHA256.test(receipt.ciphertext.sha256) ||
    receipt.ciphertext.sizeBytes !== receipt.candidate.sizeBytes
  ) {
    fail("escrow ciphertext identity is invalid");
  }
  exactKeys(
    receipt.recipient,
    ["keyType", "modulusBits", "publicExponent", "spkiSha256"],
    "escrow recipient",
  );
  if (
    receipt.recipient.keyType !== "rsa" ||
    receipt.recipient.publicExponent !== 65537 ||
    !Number.isSafeInteger(receipt.recipient.modulusBits) ||
    receipt.recipient.modulusBits < 3072 ||
    receipt.recipient.modulusBits > 8192 ||
    receipt.recipient.modulusBits % 256 !== 0 ||
    !SHA256.test(receipt.recipient.spkiSha256)
  ) {
    fail("escrow recipient authority is invalid");
  }
  exactKeys(
    receipt.encryption,
    [
      "authenticationTagBase64",
      "cipher",
      "keyWrap",
      "nonceBase64",
      "oaepLabelSha256",
      "wrappedKeyBase64",
    ],
    "escrow encryption",
  );
  if (
    receipt.encryption.cipher !== "aes-256-gcm" ||
    receipt.encryption.keyWrap !== "rsa-oaep-sha256" ||
    !SHA256.test(receipt.encryption.oaepLabelSha256)
  ) {
    fail("escrow encryption algorithms are invalid");
  }
  strictBase64(
    receipt.encryption.authenticationTagBase64,
    "escrow authentication tag",
    16,
  );
  strictBase64(receipt.encryption.nonceBase64, "escrow nonce", 12);
  strictBase64(
    receipt.encryption.wrappedKeyBase64,
    "escrow wrapped key",
    receipt.recipient.modulusBits / 8,
  );
  exactKeys(
    receipt.aad,
    [
      "candidate",
      "candidateId",
      "generationId",
      "producer",
      "recipientSpkiSha256",
      "releaseVersion",
      "rid",
    ],
    "escrow AAD",
  );
  if (
    canonicalJson(receipt.aad.candidate) !==
      canonicalJson(receipt.candidate) ||
    receipt.aad.recipientSpkiSha256 !== receipt.recipient.spkiSha256 ||
    receipt.aad.rid !== RID ||
    !PORTABLE.test(receipt.aad.candidateId) ||
    !PORTABLE.test(receipt.aad.generationId) ||
    !PORTABLE.test(receipt.aad.releaseVersion)
  ) {
    fail("escrow AAD candidate or release authority is invalid");
  }
  exactKeys(
    receipt.aad.producer,
    [
      "actor",
      "environment",
      "ref",
      "repository",
      "rerunPolicy",
      "runAttempt",
      "runId",
      "sha",
      "triggeringActor",
      "workflow",
    ],
    "escrow AAD producer",
  );
  const producer = receipt.aad.producer;
  if (
    !LOGIN.test(producer.actor) ||
    producer.environment !== ENVIRONMENT ||
    producer.ref !== REF ||
    producer.repository !== REPOSITORY ||
    producer.workflow !== WORKFLOW ||
    producer.rerunPolicy !== RERUN_POLICY ||
    producer.triggeringActor !== producer.actor ||
    !LOGIN.test(producer.triggeringActor) ||
    !POSITIVE_INTEGER.test(producer.runId) ||
    !POSITIVE_INTEGER.test(producer.runAttempt) ||
    !COMMIT.test(producer.sha)
  ) {
    fail("escrow AAD producer authority is invalid");
  }
  const aadBytes = Buffer.from(canonicalJson(receipt.aad), "utf8");
  const aadSha256 = sha256(aadBytes);
  const oaepLabel = Buffer.from(`${CONTRACT}\0${aadSha256}`, "utf8");
  if (
    receipt.aadSha256 !== aadSha256 ||
    receipt.encryption.oaepLabelSha256 !== sha256(oaepLabel)
  ) {
    fail("escrow AAD or OAEP label digest is invalid");
  }
  return { aadBytes, oaepLabel };
}

function openEscrow(argv) {
  const options = parseOptions(argv, [
    "--receipt",
    "--ciphertext",
    "--private-key",
    "--expected-recipient-spki-sha256",
    "--output",
  ]);
  const receiptRaw = noFollowRead(
    resolve(options["--receipt"]),
    "escrow receipt",
    MAX_JSON_BYTES,
  );
  let receipt;
  try {
    receipt = JSON.parse(receiptRaw.toString("utf8"));
  } catch (error) {
    fail(`escrow receipt is not valid UTF-8 JSON: ${error.message}`);
  }
  const validated = validateReceipt(receipt, receiptRaw);
  const expectedPin = boundedString(
    options["--expected-recipient-spki-sha256"],
    "expected recipient SPKI SHA-256",
    SHA256,
    64,
  );
  if (receipt.recipient.spkiSha256 !== expectedPin) {
    fail("escrow receipt recipient does not match downstream authority");
  }
  const privateKeyBytes = noFollowRead(
    resolve(options["--private-key"]),
    "escrow recipient private key",
    64 * 1024,
  );
  const passphrase = process.env.CHUMMER_MACOS_ESCROW_PRIVATE_KEY_PASSPHRASE;
  let privateKey;
  try {
    privateKey = createPrivateKey({
      key: privateKeyBytes,
      ...(passphrase === undefined ? {} : { passphrase }),
    });
  } finally {
    privateKeyBytes.fill(0);
  }
  const authority = keyAuthority(
    privateKey,
    expectedPin,
    "escrow recipient private key",
  );
  if (authority.modulusBits !== receipt.recipient.modulusBits) {
    fail("escrow recipient modulus does not match the sealed receipt");
  }
  const wrappedKey = strictBase64(
    receipt.encryption.wrappedKeyBase64,
    "escrow wrapped key",
    authority.modulusBits / 8,
  );
  const contentKey = privateDecrypt(
    {
      key: privateKey,
      oaepHash: "sha256",
      oaepLabel: validated.oaepLabel,
      padding: cryptoConstants.RSA_PKCS1_OAEP_PADDING,
    },
    wrappedKey,
  );
  if (contentKey.length !== 32) {
    contentKey.fill(0);
    fail("unwrapped escrow content key has the wrong size");
  }
  const ciphertextPath = resolve(options["--ciphertext"]);
  if (basename(ciphertextPath) !== receipt.ciphertext.fileName) {
    contentKey.fill(0);
    fail("ciphertext file name does not match the escrow receipt");
  }
  const outputPath = resolve(options["--output"]);
  if (basename(outputPath) !== receipt.candidate.fileName) {
    contentKey.fill(0);
    fail("decrypted output file name does not match the candidate");
  }
  try {
    lstatSync(outputPath);
    contentKey.fill(0);
    fail("decrypted output must not already exist");
  } catch (error) {
    if (error.code !== "ENOENT") {
      contentKey.fill(0);
      throw error;
    }
  }
  const temporary = join(
    dirname(outputPath),
    `.${basename(outputPath)}.tmp-${process.pid}`,
  );
  let ciphertextDescriptor;
  let outputDescriptor;
  try {
    const before = lstatSync(ciphertextPath, { bigint: true });
    if (!before.isFile() || before.isSymbolicLink()) {
      fail("escrow ciphertext must be a regular non-symlink file");
    }
    ciphertextDescriptor = openSync(
      ciphertextPath,
      fsConstants.O_RDONLY | fsConstants.O_NOFOLLOW,
    );
    const opened = fstatSync(ciphertextDescriptor, { bigint: true });
    if (
      !opened.isFile() ||
      opened.dev !== before.dev ||
      opened.ino !== before.ino ||
      opened.size !== BigInt(receipt.ciphertext.sizeBytes)
    ) {
      fail("escrow ciphertext identity or size changed before decryption");
    }
    outputDescriptor = openSync(
      temporary,
      fsConstants.O_WRONLY |
        fsConstants.O_CREAT |
        fsConstants.O_EXCL |
        fsConstants.O_NOFOLLOW,
      0o600,
    );
    const decipher = createDecipheriv(
      "aes-256-gcm",
      contentKey,
      strictBase64(receipt.encryption.nonceBase64, "escrow nonce", 12),
    );
    decipher.setAAD(validated.aadBytes);
    decipher.setAuthTag(
      strictBase64(
        receipt.encryption.authenticationTagBase64,
        "escrow authentication tag",
        16,
      ),
    );
    const ciphertextHash = createHash("sha256");
    const plaintextHash = createHash("sha256");
    const buffer = Buffer.allocUnsafe(BUFFER_BYTES);
    let ciphertextSize = 0;
    let plaintextSize = 0;
    for (;;) {
      const count = readSync(ciphertextDescriptor, buffer, 0, buffer.length);
      if (count === 0) {
        break;
      }
      const chunk = buffer.subarray(0, count);
      ciphertextHash.update(chunk);
      ciphertextSize += count;
      const plaintext = decipher.update(chunk);
      plaintextHash.update(plaintext);
      plaintextSize += plaintext.length;
      writeAll(outputDescriptor, plaintext);
    }
    const after = fstatSync(ciphertextDescriptor, { bigint: true });
    if (
      !identitiesEqual(snapshotIdentity(opened), snapshotIdentity(after)) ||
      ciphertextSize !== receipt.ciphertext.sizeBytes ||
      ciphertextHash.digest("hex") !== receipt.ciphertext.sha256
    ) {
      fail("escrow ciphertext changed or did not match the receipt");
    }
    const finalChunk = decipher.final();
    plaintextHash.update(finalChunk);
    plaintextSize += finalChunk.length;
    writeAll(outputDescriptor, finalChunk);
    if (
      plaintextSize !== receipt.candidate.sizeBytes ||
      plaintextHash.digest("hex") !== receipt.candidate.sha256
    ) {
      fail("decrypted candidate does not match the signed evidence identity");
    }
    fsyncSync(outputDescriptor);
    closeSync(outputDescriptor);
    outputDescriptor = undefined;
    closeSync(ciphertextDescriptor);
    ciphertextDescriptor = undefined;
    linkSync(temporary, outputPath);
    unlinkSync(temporary);
    process.stdout.write(`macos_flagship_candidate=${outputPath}\n`);
  } finally {
    contentKey.fill(0);
    if (ciphertextDescriptor !== undefined) {
      closeSync(ciphertextDescriptor);
    }
    if (outputDescriptor !== undefined) {
      closeSync(outputDescriptor);
    }
    try {
      unlinkSync(temporary);
    } catch (error) {
      if (error.code !== "ENOENT") {
        throw error;
      }
    }
  }
}

function main() {
  const [command, ...argv] = process.argv.slice(2);
  if (command === "verify-recipient") {
    verifyRecipient(argv);
    return;
  }
  if (command === "seal") {
    seal(argv);
    return;
  }
  if (command === "open") {
    openEscrow(argv);
    return;
  }
  fail(
    "usage: macos_flagship_candidate_escrow.mjs " +
      "verify-recipient|seal|open [options]",
  );
}

try {
  main();
} catch (error) {
  process.stderr.write(`macOS candidate escrow failed: ${error.message}\n`);
  process.exitCode = 1;
}
