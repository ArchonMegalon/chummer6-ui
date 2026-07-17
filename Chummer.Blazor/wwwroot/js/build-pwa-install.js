(() => {
  const handoffContractKey = "chummerBuildPwaHandoff";
  const handoffContract = window[handoffContractKey] || (() => {
    const allowedDevicePreferences = new Set(["auto", "mobile", "desktop"]);
    const qrVersion = 10;
    const qrSize = qrVersion * 4 + 17;
    const qrDataCodewords = 274;
    const qrEccCodewordsPerBlock = 18;
    const qrDataBlockLengths = Object.freeze([68, 68, 69, 69]);

    const normalizeDevicePreference = (value) => {
      const normalized = String(value || "").trim().toLowerCase();
      return allowedDevicePreferences.has(normalized) ? normalized : "auto";
    };

    const resolveEffectiveDevice = (preference, signals = {}) => {
      if (signals.standalone === true) return "standalone";

      const normalizedPreference = normalizeDevicePreference(preference);
      if (normalizedPreference === "mobile" || normalizedPreference === "desktop") {
        return normalizedPreference;
      }

      if (typeof signals.userAgentDataMobile === "boolean") {
        return signals.userAgentDataMobile ? "mobile" : "desktop";
      }

      return signals.coarsePointer === true && Number(signals.maxTouchPoints || 0) > 0
        ? "mobile"
        : "desktop";
    };

    const buildCanonicalInstallUrl = ({ origin, scope }) => {
      const expectedOrigin = new URL(origin).origin;
      const scopeUrl = new URL(scope, `${expectedOrigin}/`);
      if (scopeUrl.origin !== expectedOrigin) {
        throw new Error("Build install scope must remain on this origin.");
      }

      scopeUrl.username = "";
      scopeUrl.password = "";
      scopeUrl.search = "";
      scopeUrl.hash = "";
      if (!scopeUrl.pathname.endsWith("/")) scopeUrl.pathname = `${scopeUrl.pathname}/`;

      const installUrl = new URL("app", scopeUrl);
      installUrl.username = "";
      installUrl.password = "";
      installUrl.search = "";
      installUrl.hash = "";
      if (installUrl.origin !== expectedOrigin || !installUrl.pathname.startsWith(scopeUrl.pathname)) {
        throw new Error("Build install URL escaped its owned scope.");
      }

      return installUrl.href;
    };

    const appendBits = (value, length, bits) => {
      if (!Number.isInteger(value) || value < 0 || length < 0 || (length < 31 && value >>> length !== 0)) {
        throw new RangeError("QR bit value is out of range.");
      }
      for (let index = length - 1; index >= 0; index -= 1) {
        bits.push((value >>> index) & 1);
      }
    };

    const multiplyGalois = (left, right) => {
      let x = left;
      let y = right;
      let result = 0;
      while (y !== 0) {
        if ((y & 1) !== 0) result ^= x;
        y >>>= 1;
        x = (x << 1) ^ ((x >>> 7) * 0x11d);
      }
      return result;
    };

    const computeReedSolomonDivisor = (degree) => {
      const result = new Array(degree).fill(0);
      result[degree - 1] = 1;
      let root = 1;
      for (let index = 0; index < degree; index += 1) {
        for (let coefficient = 0; coefficient < result.length; coefficient += 1) {
          result[coefficient] = multiplyGalois(result[coefficient], root);
          if (coefficient + 1 < result.length) result[coefficient] ^= result[coefficient + 1];
        }
        root = multiplyGalois(root, 0x02);
      }
      return result;
    };

    const computeReedSolomonRemainder = (data, divisor) => {
      const result = new Array(divisor.length).fill(0);
      for (const byte of data) {
        const factor = byte ^ result.shift();
        result.push(0);
        for (let index = 0; index < result.length; index += 1) {
          result[index] ^= multiplyGalois(divisor[index], factor);
        }
      }
      return result;
    };

    const encodeQrCodewords = (payload) => {
      const bytes = [...new TextEncoder().encode(String(payload))];
      const capacityBits = qrDataCodewords * 8;
      const bits = [];
      appendBits(0x4, 4, bits);
      appendBits(bytes.length, 16, bits);
      for (const byte of bytes) appendBits(byte, 8, bits);
      if (bits.length > capacityBits) {
        throw new RangeError("The canonical Build install URL is too long for the local QR code.");
      }

      appendBits(0, Math.min(4, capacityBits - bits.length), bits);
      while (bits.length % 8 !== 0) bits.push(0);

      const data = [];
      for (let index = 0; index < bits.length; index += 8) {
        let value = 0;
        for (let offset = 0; offset < 8; offset += 1) value = (value << 1) | bits[index + offset];
        data.push(value);
      }
      for (let padIndex = 0; data.length < qrDataCodewords; padIndex += 1) {
        data.push((padIndex & 1) === 0 ? 0xec : 0x11);
      }

      const divisor = computeReedSolomonDivisor(qrEccCodewordsPerBlock);
      const blocks = [];
      const eccBlocks = [];
      let dataOffset = 0;
      for (const blockLength of qrDataBlockLengths) {
        const block = data.slice(dataOffset, dataOffset + blockLength);
        dataOffset += blockLength;
        blocks.push(block);
        eccBlocks.push(computeReedSolomonRemainder(block, divisor));
      }

      const result = [];
      const maxDataBlockLength = Math.max(...qrDataBlockLengths);
      for (let index = 0; index < maxDataBlockLength; index += 1) {
        for (const block of blocks) {
          if (index < block.length) result.push(block[index]);
        }
      }
      for (let index = 0; index < qrEccCodewordsPerBlock; index += 1) {
        for (const block of eccBlocks) result.push(block[index]);
      }
      return result;
    };

    const cloneMatrix = (matrix) => matrix.map((row) => row.slice());

    const drawFormatBits = (modules, isFunction, mask) => {
      const data = (0x1 << 3) | mask;
      let remainder = data;
      for (let index = 0; index < 10; index += 1) {
        remainder = (remainder << 1) ^ ((remainder >>> 9) * 0x537);
      }
      const bits = ((data << 10) | remainder) ^ 0x5412;
      const set = (x, y, dark) => {
        modules[y][x] = dark;
        isFunction[y][x] = true;
      };

      for (let index = 0; index <= 5; index += 1) set(8, index, ((bits >>> index) & 1) !== 0);
      set(8, 7, ((bits >>> 6) & 1) !== 0);
      set(8, 8, ((bits >>> 7) & 1) !== 0);
      set(7, 8, ((bits >>> 8) & 1) !== 0);
      for (let index = 9; index < 15; index += 1) set(14 - index, 8, ((bits >>> index) & 1) !== 0);
      for (let index = 0; index < 8; index += 1) set(qrSize - 1 - index, 8, ((bits >>> index) & 1) !== 0);
      for (let index = 8; index < 15; index += 1) set(8, qrSize - 15 + index, ((bits >>> index) & 1) !== 0);
      set(8, qrSize - 8, true);
    };

    const maskApplies = (mask, x, y) => {
      switch (mask) {
        case 0: return (x + y) % 2 === 0;
        case 1: return y % 2 === 0;
        case 2: return x % 3 === 0;
        case 3: return (x + y) % 3 === 0;
        case 4: return (Math.floor(x / 3) + Math.floor(y / 2)) % 2 === 0;
        case 5: return (x * y) % 2 + (x * y) % 3 === 0;
        case 6: return ((x * y) % 2 + (x * y) % 3) % 2 === 0;
        case 7: return ((x + y) % 2 + (x * y) % 3) % 2 === 0;
        default: throw new RangeError("Unsupported QR mask.");
      }
    };

    const qrPenalty = (modules) => {
      let penalty = 0;
      const scoreRuns = (values) => {
        let result = 0;
        let runColor = values[0];
        let runLength = 1;
        for (let index = 1; index <= values.length; index += 1) {
          if (index < values.length && values[index] === runColor) {
            runLength += 1;
          } else {
            if (runLength >= 5) result += 3 + runLength - 5;
            if (index < values.length) {
              runColor = values[index];
              runLength = 1;
            }
          }
        }
        return result;
      };
      const patternA = "10111010000";
      const patternB = "00001011101";

      for (let y = 0; y < qrSize; y += 1) {
        const row = modules[y];
        penalty += scoreRuns(row);
        const rowBits = row.map((dark) => dark ? "1" : "0").join("");
        for (let index = 0; index <= qrSize - 11; index += 1) {
          const candidate = rowBits.slice(index, index + 11);
          if (candidate === patternA || candidate === patternB) penalty += 40;
        }
      }
      for (let x = 0; x < qrSize; x += 1) {
        const column = modules.map((row) => row[x]);
        penalty += scoreRuns(column);
        const columnBits = column.map((dark) => dark ? "1" : "0").join("");
        for (let index = 0; index <= qrSize - 11; index += 1) {
          const candidate = columnBits.slice(index, index + 11);
          if (candidate === patternA || candidate === patternB) penalty += 40;
        }
      }
      for (let y = 0; y < qrSize - 1; y += 1) {
        for (let x = 0; x < qrSize - 1; x += 1) {
          const color = modules[y][x];
          if (modules[y][x + 1] === color
              && modules[y + 1][x] === color
              && modules[y + 1][x + 1] === color) penalty += 3;
        }
      }
      const darkModules = modules.reduce(
        (count, row) => count + row.filter(Boolean).length,
        0);
      penalty += Math.floor(Math.abs(darkModules * 20 - qrSize * qrSize * 10) / (qrSize * qrSize)) * 10;
      return penalty;
    };

    const encodeQrMatrix = (payload) => {
      const codewords = encodeQrCodewords(payload);
      const modules = Array.from({ length: qrSize }, () => new Array(qrSize).fill(false));
      const isFunction = Array.from({ length: qrSize }, () => new Array(qrSize).fill(false));
      const setFunction = (x, y, dark) => {
        modules[y][x] = dark;
        isFunction[y][x] = true;
      };

      for (let index = 0; index < qrSize; index += 1) {
        setFunction(6, index, index % 2 === 0);
        setFunction(index, 6, index % 2 === 0);
      }
      for (const [centerX, centerY] of [[3, 3], [qrSize - 4, 3], [3, qrSize - 4]]) {
        for (let offsetY = -4; offsetY <= 4; offsetY += 1) {
          for (let offsetX = -4; offsetX <= 4; offsetX += 1) {
            const x = centerX + offsetX;
            const y = centerY + offsetY;
            if (x < 0 || y < 0 || x >= qrSize || y >= qrSize) continue;
            const distance = Math.max(Math.abs(offsetX), Math.abs(offsetY));
            setFunction(x, y, distance !== 2 && distance !== 4);
          }
        }
      }

      const alignmentPositions = [6, 28, 50];
      for (let rowIndex = 0; rowIndex < alignmentPositions.length; rowIndex += 1) {
        for (let columnIndex = 0; columnIndex < alignmentPositions.length; columnIndex += 1) {
          const overlapsFinder = (rowIndex === 0 && columnIndex === 0)
            || (rowIndex === 0 && columnIndex === alignmentPositions.length - 1)
            || (rowIndex === alignmentPositions.length - 1 && columnIndex === 0);
          if (overlapsFinder) continue;
          const centerX = alignmentPositions[columnIndex];
          const centerY = alignmentPositions[rowIndex];
          for (let offsetY = -2; offsetY <= 2; offsetY += 1) {
            for (let offsetX = -2; offsetX <= 2; offsetX += 1) {
              setFunction(
                centerX + offsetX,
                centerY + offsetY,
                Math.max(Math.abs(offsetX), Math.abs(offsetY)) !== 1);
            }
          }
        }
      }

      drawFormatBits(modules, isFunction, 0);
      let versionRemainder = qrVersion;
      for (let index = 0; index < 12; index += 1) {
        versionRemainder = (versionRemainder << 1) ^ ((versionRemainder >>> 11) * 0x1f25);
      }
      const versionBits = (qrVersion << 12) | versionRemainder;
      for (let index = 0; index < 18; index += 1) {
        const dark = ((versionBits >>> index) & 1) !== 0;
        setFunction(qrSize - 11 + index % 3, Math.floor(index / 3), dark);
        setFunction(Math.floor(index / 3), qrSize - 11 + index % 3, dark);
      }

      let bitIndex = 0;
      for (let right = qrSize - 1; right >= 1; right -= 2) {
        if (right === 6) right = 5;
        for (let vertical = 0; vertical < qrSize; vertical += 1) {
          const upward = ((right + 1) & 2) === 0;
          const y = upward ? qrSize - 1 - vertical : vertical;
          for (let column = 0; column < 2; column += 1) {
            const x = right - column;
            if (isFunction[y][x]) continue;
            modules[y][x] = bitIndex < codewords.length * 8
              && ((codewords[bitIndex >>> 3] >>> (7 - (bitIndex & 7))) & 1) !== 0;
            bitIndex += 1;
          }
        }
      }

      let best = null;
      for (let mask = 0; mask < 8; mask += 1) {
        const candidate = cloneMatrix(modules);
        const candidateFunction = cloneMatrix(isFunction);
        for (let y = 0; y < qrSize; y += 1) {
          for (let x = 0; x < qrSize; x += 1) {
            if (!candidateFunction[y][x] && maskApplies(mask, x, y)) candidate[y][x] = !candidate[y][x];
          }
        }
        drawFormatBits(candidate, candidateFunction, mask);
        const penalty = qrPenalty(candidate);
        if (best === null || penalty < best.penalty) best = { mask, modules: candidate, penalty };
      }

      return Object.freeze({
        version: qrVersion,
        size: qrSize,
        mask: best.mask,
        modules: best.modules
      });
    };

    const matrixSignature = (encoded) => {
      let hash = 0x811c9dc5;
      const update = (value) => {
        hash ^= value & 0xff;
        hash = Math.imul(hash, 0x01000193) >>> 0;
      };
      update(encoded.version);
      update(encoded.size);
      update(encoded.mask);
      for (const row of encoded.modules) {
        for (const dark of row) update(dark ? 1 : 0);
      }
      return hash.toString(16).padStart(8, "0");
    };

    const renderQrSvg = (container, encoded, label) => {
      if (!(container instanceof HTMLElement)) return;
      const namespace = "http://www.w3.org/2000/svg";
      const quietZone = 4;
      const canvasSize = encoded.size + quietZone * 2;
      const svg = document.createElementNS(namespace, "svg");
      svg.setAttribute("viewBox", `0 0 ${canvasSize} ${canvasSize}`);
      svg.setAttribute("role", "img");
      svg.setAttribute("aria-label", label);
      svg.setAttribute("focusable", "false");
      const title = document.createElementNS(namespace, "title");
      title.textContent = label;
      svg.appendChild(title);
      const background = document.createElementNS(namespace, "rect");
      background.setAttribute("width", String(canvasSize));
      background.setAttribute("height", String(canvasSize));
      background.setAttribute("fill", "#ffffff");
      svg.appendChild(background);
      const path = document.createElementNS(namespace, "path");
      const commands = [];
      for (let y = 0; y < encoded.size; y += 1) {
        for (let x = 0; x < encoded.size; x += 1) {
          if (encoded.modules[y][x]) commands.push(`M${x + quietZone},${y + quietZone}h1v1h-1z`);
        }
      }
      path.setAttribute("d", commands.join(""));
      path.setAttribute("fill", "#000000");
      svg.appendChild(path);
      container.replaceChildren(svg);
    };

    return Object.freeze({
      buildCanonicalInstallUrl,
      encodeQrMatrix,
      matrixSignature,
      normalizeDevicePreference,
      renderQrSvg,
      resolveEffectiveDevice
    });
  })();
  if (!window[handoffContractKey]) {
    Object.defineProperty(window, handoffContractKey, {
      value: handoffContract,
      writable: false,
      configurable: false,
      enumerable: true
    });
  }

  const controllerKey = "chummerBuildPwaInstallController";
  const existingController = window[controllerKey];
  if (existingController && typeof existingController.refresh === "function") {
    existingController.refresh();
    return;
  }

  const root = document.querySelector("[data-build-pwa-install]");
  if (!(root instanceof HTMLElement)) return;

  const immutableExpectedAuthority = Object.isFrozen(window.chummerPwa?.expectedAuthority)
    ? window.chummerPwa.expectedAuthority
    : null;

  const listenerRemovers = [];
  const listen = (target, type, handler, options) => {
    if (!target || typeof target.addEventListener !== "function") return;
    target.addEventListener(type, handler, options);
    listenerRemovers.push(() => target.removeEventListener(type, handler, options));
  };

  const helpButton = document.querySelector("[data-build-pwa-install-help]");
  const status = root.querySelector("[data-build-pwa-install-status]");
  const installButton = root.querySelector("[data-build-pwa-install-action]");
  const updateButton = root.querySelector("[data-build-pwa-update-action]");
  const updateGuidance = root.querySelector("[data-build-pwa-update-guidance]");
  const dismissButton = root.querySelector("[data-build-pwa-dismiss-action]");
  const manualSteps = root.querySelector("[data-build-pwa-manual]");
  const handoffRoot = root.querySelector("[data-build-pwa-install-handoff]");
  const deviceStatus = root.querySelector("[data-build-pwa-install-device-status]");
  const desktopHandoff = root.querySelector("[data-build-pwa-desktop-handoff]");
  const mobileHandoff = root.querySelector("[data-build-pwa-mobile-handoff]");
  const qrContainer = root.querySelector("[data-build-pwa-install-qr]");
  const installLink = root.querySelector("[data-build-pwa-install-link]");
  const installLinkText = root.querySelector("[data-build-pwa-install-link-text]");
  const copyInstallLinkButton = root.querySelector("[data-build-pwa-copy-install-link]");
  const dismissalKey = "chummer-build-install-guidance-dismissed";
  const devicePreferenceKey = "chummer.build-pwa.install-device.v1";
  const registrationEventName = "chummer-build:service-worker-registration";
  const registrationFailedEventName = "chummer-build:service-worker-registration-failed";
  const integrityChangedEventName = "chummer:build-integrity-changed";
  const updateActivatedMessageType = "chummer-build-update-activated";
  const CHUMMER_BUILD_PWA_CACHE_VERSION = "v6";
  const CHUMMER_BUILD_PWA_CACHE_LEASE_REQUEST = "chummer-build-pwa-cache-lease-request";
  const CHUMMER_BUILD_PWA_CACHE_LEASE_RESPONSE = "chummer-build-pwa-cache-lease-response";
  const CHUMMER_BUILD_PWA_CACHE_LEASE_SWEEP = "chummer-build-pwa-cache-lease-sweep";
  let deferredInstallPrompt = null;
  let updateRegistration = null;
  let buildRegistrationAuthority = null;
  let announcedWaitingWorker = null;
  const watchedRegistrations = new WeakSet();
  let controllerChangedPending = false;
  let controllerCheckInFlight = false;
  let reloadCommitted = false;
  let hadControllerAtStartup = "serviceWorker" in navigator && Boolean(navigator.serviceWorker.controller);
  let guidanceDismissed = false;
  let installControlsSuppressed = false;
  let appInstallConfirmed = false;
  let cacheSweepScheduled = false;
  let cacheSweepTimer = null;
  let launcherHydrationObserver = null;
  let disposed = false;
  const standaloneMediaQuery = window.matchMedia("(display-mode: standalone)");
  const coarsePointerMediaQuery = window.matchMedia("(any-pointer: coarse)");
  let memoryDevicePreference = null;
  let effectiveInstallDevice = "desktop";
  let renderedQrUrl = null;

  try {
    guidanceDismissed = window.sessionStorage.getItem(dismissalKey) === "1";
  } catch {
    guidanceDismissed = false;
  }

  const setStatus = (message) => {
    if (status) status.textContent = message;
  };

  const isStandalone = () =>
    standaloneMediaQuery.matches
    || window.navigator.standalone === true;

  const readDevicePreference = () => {
    if (memoryDevicePreference !== null) return memoryDevicePreference;
    try {
      memoryDevicePreference = handoffContract.normalizeDevicePreference(
        window.localStorage.getItem(devicePreferenceKey));
    } catch {
      memoryDevicePreference = "auto";
    }
    return memoryDevicePreference;
  };

  const setDevicePreference = (value) => {
    memoryDevicePreference = handoffContract.normalizeDevicePreference(value);
    try {
      window.localStorage.setItem(devicePreferenceKey, memoryDevicePreference);
    } catch {
      // The in-memory override remains usable when persistent storage is blocked.
    }
    return memoryDevicePreference;
  };

  const currentDeviceSignals = () => {
    const userAgentData = window.navigator.userAgentData;
    return {
      standalone: isStandalone(),
      userAgentDataMobile: typeof userAgentData?.mobile === "boolean"
        ? userAgentData.mobile
        : null,
      coarsePointer: coarsePointerMediaQuery.matches,
      maxTouchPoints: Number(window.navigator.maxTouchPoints || 0)
    };
  };

  const resolveCanonicalInstallUrl = () => {
    if (!immutableExpectedAuthority
        || typeof immutableExpectedAuthority.scope !== "string"
        || typeof immutableExpectedAuthority.scriptUrl !== "string") {
      throw new Error("A frozen Build registration authority is required for install handoff.");
    }

    let authorityScope;
    let authorityScript;
    try {
      authorityScope = new URL(immutableExpectedAuthority.scope);
      authorityScript = new URL(immutableExpectedAuthority.scriptUrl);
    } catch {
      throw new Error("The frozen Build registration authority is invalid.");
    }

    const expectedWorker = new URL("service-worker.js", authorityScope);
    const scriptQueryKeys = [...authorityScript.searchParams.keys()];
    if (authorityScope.origin !== window.location.origin
        || authorityScript.origin !== window.location.origin
        || authorityScope.username
        || authorityScope.password
        || authorityScope.search
        || authorityScope.hash
        || !authorityScope.pathname.endsWith("/")
        || authorityScript.pathname !== expectedWorker.pathname
        || authorityScript.hash
        || scriptQueryKeys.length !== 1
        || scriptQueryKeys[0] !== "build"
        || !/^[a-f0-9]{64}$/.test(authorityScript.searchParams.get("build") || "")) {
      throw new Error("The frozen Build registration authority does not own this app scope.");
    }

    return handoffContract.buildCanonicalInstallUrl({
      origin: window.location.origin,
      scope: authorityScope.href
    });
  };

  const handoffStatusText = (preference, effective) => {
    if (effective === "standalone") {
      return "Chummer Build is already running as an installed app on this device.";
    }
    if (preference === "mobile") {
      return "Mobile guidance selected. Install here or follow Add to Home Screen steps.";
    }
    if (preference === "desktop") {
      return "Desktop handoff selected. Scan the private-by-construction code or copy its clean link.";
    }
    return effective === "mobile"
      ? "Auto is using mobile installation guidance for this browser."
      : "Auto is using desktop-to-mobile handoff for this browser.";
  };

  const renderDeviceHandoff = () => {
    if (!(handoffRoot instanceof HTMLElement)) return effectiveInstallDevice;

    const preference = readDevicePreference();
    effectiveInstallDevice = handoffContract.resolveEffectiveDevice(
      preference,
      currentDeviceSignals());
    handoffRoot.dataset.buildPwaHandoffPreference = preference;
    handoffRoot.dataset.buildPwaHandoffEffective = effectiveInstallDevice;
    root.querySelectorAll("[data-build-pwa-install-device-choice]").forEach((choice) => {
      if (choice instanceof HTMLInputElement) choice.checked = choice.value === preference;
    });

    if (deviceStatus instanceof HTMLElement) {
      deviceStatus.textContent = handoffStatusText(preference, effectiveInstallDevice);
    }
    if (desktopHandoff instanceof HTMLElement) {
      desktopHandoff.hidden = effectiveInstallDevice !== "desktop";
    }
    if (mobileHandoff instanceof HTMLElement) {
      mobileHandoff.hidden = effectiveInstallDevice !== "mobile";
    }
    if (installButton instanceof HTMLButtonElement) {
      installButton.textContent = effectiveInstallDevice === "desktop"
        ? "Install on this desktop"
        : "Install Chummer Build";
    }

    if (effectiveInstallDevice !== "desktop") return effectiveInstallDevice;

    let canonicalInstallUrl;
    try {
      canonicalInstallUrl = resolveCanonicalInstallUrl();
    } catch {
      renderedQrUrl = null;
      if (qrContainer instanceof HTMLElement) {
        qrContainer.replaceChildren();
        qrContainer.hidden = true;
        qrContainer.removeAttribute("data-build-pwa-qr-version");
        qrContainer.removeAttribute("data-build-pwa-qr-mask");
        qrContainer.removeAttribute("data-build-pwa-qr-signature");
      }
      if (installLink instanceof HTMLAnchorElement) {
        installLink.removeAttribute("href");
        installLink.hidden = true;
      }
      if (installLinkText instanceof HTMLElement) installLinkText.textContent = "Install link unavailable.";
      if (copyInstallLinkButton instanceof HTMLButtonElement) copyInstallLinkButton.disabled = true;
      if (deviceStatus instanceof HTMLElement) {
        deviceStatus.textContent = "The clean mobile install link could not be verified for this app scope.";
      }
      return effectiveInstallDevice;
    }

    if (installLink instanceof HTMLAnchorElement) {
      installLink.href = canonicalInstallUrl;
      installLink.hidden = false;
    }
    if (installLinkText instanceof HTMLElement) installLinkText.textContent = canonicalInstallUrl;
    if (copyInstallLinkButton instanceof HTMLButtonElement) copyInstallLinkButton.disabled = false;
    if (qrContainer instanceof HTMLElement && renderedQrUrl !== canonicalInstallUrl) {
      try {
        const encoded = handoffContract.encodeQrMatrix(canonicalInstallUrl);
        qrContainer.hidden = false;
        handoffContract.renderQrSvg(
          qrContainer,
          encoded,
          "QR code for the clean Chummer Build mobile install page");
        qrContainer.dataset.buildPwaQrVersion = String(encoded.version);
        qrContainer.dataset.buildPwaQrMask = String(encoded.mask);
        qrContainer.dataset.buildPwaQrSignature = handoffContract.matrixSignature(encoded);
        renderedQrUrl = canonicalInstallUrl;
      } catch {
        renderedQrUrl = null;
        qrContainer.replaceChildren();
        qrContainer.hidden = true;
        qrContainer.removeAttribute("data-build-pwa-qr-version");
        qrContainer.removeAttribute("data-build-pwa-qr-mask");
        qrContainer.removeAttribute("data-build-pwa-qr-signature");
        if (deviceStatus instanceof HTMLElement) {
          deviceStatus.textContent = "The QR code could not be generated. Copy the verified clean install link instead.";
        }
      }
    }
    return effectiveInstallDevice;
  };

  const isVisibleStableFocusTarget = (target) => {
    if (!(target instanceof HTMLElement)
        || target.hidden
        || target.closest("[hidden]")
        || target.getAttribute("aria-hidden") === "true") {
      return false;
    }

    const style = window.getComputedStyle(target);
    return style.display !== "none" && style.visibility !== "hidden";
  };

  const focusStableTargetBeforeHiding = (includeLauncher) => {
    const candidates = includeLauncher && helpButton instanceof HTMLElement
      ? [helpButton]
      : [];
    candidates.push(...document.querySelectorAll(
      "#chummer-workspace-main, #chummer-online-app[tabindex], main[tabindex], "
      + "main button:not([hidden]):not([disabled]), main a[href], "
      + "main input:not([hidden]):not([disabled]), main select:not([hidden]):not([disabled]), "
      + "main textarea:not([hidden]):not([disabled])"));

    for (const target of candidates) {
      if (!isVisibleStableFocusTarget(target) || root.contains(target)) continue;
      target.focus({ preventScroll: true });
      if (document.activeElement === target) return true;
    }
    return false;
  };

  const copyCanonicalInstallLink = async () => {
    let canonicalInstallUrl;
    try {
      canonicalInstallUrl = resolveCanonicalInstallUrl();
    } catch {
      if (deviceStatus instanceof HTMLElement) {
        deviceStatus.textContent = "The mobile install link could not be verified, so nothing was copied.";
      }
      return false;
    }

    let copied = false;
    try {
      if (window.navigator.clipboard?.writeText) {
        await window.navigator.clipboard.writeText(canonicalInstallUrl);
        copied = true;
      }
    } catch {
      copied = false;
    }
    if (!copied) {
      const returnFocus = document.activeElement;
      const field = document.createElement("textarea");
      field.value = canonicalInstallUrl;
      field.readOnly = true;
      field.setAttribute("aria-hidden", "true");
      field.style.position = "fixed";
      field.style.opacity = "0";
      document.body.appendChild(field);
      field.select();
      try {
        copied = document.execCommand("copy") === true;
      } catch {
        copied = false;
      } finally {
        field.remove();
        if (returnFocus instanceof HTMLElement) returnFocus.focus({ preventScroll: true });
      }
    }

    if (deviceStatus instanceof HTMLElement) {
      deviceStatus.textContent = copied
        ? "Clean mobile install link copied."
        : "Copy was blocked. Select and copy the clean link shown below the QR code.";
    }
    return copied;
  };

  const setPanelVisible = (visible) => {
    root.hidden = !visible;
    if (helpButton instanceof HTMLButtonElement) {
      helpButton.setAttribute("aria-expanded", visible ? "true" : "false");
    }
  };

  const setUpdateGuidanceVisible = (visible) => {
    if (updateGuidance instanceof HTMLElement) updateGuidance.hidden = !visible;
    if (updateButton instanceof HTMLButtonElement) {
      updateButton.setAttribute("aria-expanded", visible ? "true" : "false");
    }
  };

  const renderInstallLauncherState = () => {
    const activeElement = document.activeElement;
    const focusWasInsideInstallUi = activeElement instanceof HTMLElement
      && (root.contains(activeElement) || activeElement === helpButton);
    renderDeviceHandoff();
    installControlsSuppressed = appInstallConfirmed || isStandalone();
    if (installControlsSuppressed) {
      if (focusWasInsideInstallUi) focusStableTargetBeforeHiding(false);
      setPanelVisible(false);
      if (helpButton instanceof HTMLButtonElement) helpButton.hidden = true;
      if (installButton instanceof HTMLButtonElement) installButton.hidden = true;
      return;
    }

    if (helpButton instanceof HTMLButtonElement) helpButton.hidden = false;
    if (installButton instanceof HTMLButtonElement) {
      installButton.hidden = deferredInstallPrompt === null;
    }
  };

  const hideInstalledControls = () => {
    appInstallConfirmed = true;
    renderInstallLauncherState();
  };

  const preserveLauncherAcrossHydration = () => {
    if (!installControlsSuppressed && helpButton instanceof HTMLButtonElement && helpButton.hidden) {
      helpButton.hidden = false;
    }
  };

  const showManualSteps = (message) => {
    deferredInstallPrompt = null;
    if (installButton instanceof HTMLButtonElement) {
      installButton.hidden = true;
      installButton.disabled = false;
    }
    if (manualSteps instanceof HTMLDetailsElement) manualSteps.open = true;
    setStatus(message);
  };

  const registrationStillMatchesAuthority = (authority) => {
    if (!authority || authority.registration.scope !== authority.scope) return false;
    return [
      authority.registration.active,
      authority.registration.waiting,
      authority.registration.installing
    ].some((worker) => worker?.scriptURL === authority.scriptUrl);
  };

  const refreshWaitingUpdate = ({ announce = true } = {}) => {
    const authority = buildRegistrationAuthority;
    if (!registrationStillMatchesAuthority(authority)) return false;
    const registration = authority.registration;
    const waiting = registration.waiting;
    if (!waiting || waiting.scriptURL !== authority.scriptUrl) {
      if (updateRegistration === registration) updateRegistration = null;
      return false;
    }

    updateRegistration = registration;
    setUpdateDeferred(true);
    if (!guidanceDismissed) setPanelVisible(true);
    setUpdateGuidanceVisible(false);
    if (updateButton instanceof HTMLButtonElement) {
      updateButton.hidden = false;
      updateButton.disabled = false;
      updateButton.textContent = "Review update steps";
    }
    if (announce && announcedWaitingWorker !== waiting) {
      announcedWaitingWorker = waiting;
      setStatus("A Chummer Build update is downloaded and waiting. Save or copy your work, then close every Chummer Build tab and installed-app window before reopening.");
    }
    return true;
  };

  const integrityApi = () => {
    const integrity = window.chummerBuildPwaIntegrity;
    return integrity
      && typeof integrity.canReload === "function"
      && typeof integrity.getSnapshot === "function"
      && typeof integrity.setUpdateDeferred === "function"
      ? integrity
      : null;
  };

  const setUpdateDeferred = (deferred) => {
    const integrity = integrityApi();
    if (!integrity) return;
    integrity.setUpdateDeferred(deferred);
  };

  const queryReloadSafety = async () => {
    const integrity = integrityApi();
    if (!integrity) {
      return { safe: false, available: false, snapshot: null };
    }

    try {
      const safe = await integrity.canReload();
      const snapshot = integrity.getSnapshot();
      return {
        safe: safe === true
          && snapshot?.bridgeAvailable === true
          && snapshot?.isDirty !== true
          && snapshot?.hasConflict !== true,
        available: snapshot?.bridgeAvailable === true,
        snapshot
      };
    } catch {
      return { safe: false, available: false, snapshot: null };
    }
  };

  const deferUpdate = (message) => {
    setUpdateDeferred(true);
    if (!guidanceDismissed) setPanelVisible(true);
    if (!controllerChangedPending) setUpdateGuidanceVisible(true);
    if (updateButton instanceof HTMLButtonElement) {
      updateButton.hidden = false;
      updateButton.disabled = false;
      updateButton.textContent = controllerChangedPending ? "Reload when safe" : "Review update steps";
    }
    setStatus(message);
  };

  const commitReloadIfSafe = async () => {
    if (reloadCommitted || controllerCheckInFlight) return;
    controllerCheckInFlight = true;
    const safety = await queryReloadSafety();
    controllerCheckInFlight = false;
    if (!safety.safe) {
      deferUpdate(safety.available
        ? "The browser's active worker changed, but this runner has unsaved or conflicted work. Save or copy it before reloading this tab."
        : "The browser's active worker changed, but workspace safety could not be verified. Reconnect Chummer Build before reloading.");
      return;
    }

    reloadCommitted = true;
    controllerChangedPending = false;
    setUpdateDeferred(false);
    setStatus("Workspace safety verified. Reloading the updated Chummer Build…");
    window.location.reload();
  };

  const reviewWaitingUpdate = async () => {
    if (controllerChangedPending) {
      await commitReloadIfSafe();
      return;
    }

    const waiting = updateRegistration?.waiting;
    if (!waiting) {
      if (updateButton instanceof HTMLButtonElement) updateButton.hidden = true;
      setUpdateGuidanceVisible(false);
      setStatus("No downloaded Chummer Build update is currently waiting.");
      return;
    }

    if (updateButton instanceof HTMLButtonElement) updateButton.disabled = true;
    const safety = await queryReloadSafety();
    setUpdateDeferred(true);
    setUpdateGuidanceVisible(true);
    if (!safety.safe) {
      setStatus(safety.available
        ? "The update stays waiting because this runner has unsaved or conflicted work. Save it or save a copy, then close every Chummer Build tab and window before reopening."
        : "The update stays waiting because workspace safety could not be verified. Reconnect and save or copy your work, then close every Chummer Build tab and window before reopening.");
    } else {
      setStatus("This runner is saved. Close every Chummer Build browser tab and installed-app window, then reopen Chummer Build to start the waiting version.");
    }

    if (updateButton instanceof HTMLButtonElement) {
      updateButton.disabled = false;
      updateButton.textContent = "Update steps shown";
    }
    if (updateGuidance instanceof HTMLElement) updateGuidance.focus({ preventScroll: true });
  };

  const watchRegistration = (registration) => {
    if (!registration || watchedRegistrations.has(registration)) {
      refreshWaitingUpdate();
      return;
    }

    watchedRegistrations.add(registration);
    refreshWaitingUpdate();
    listen(registration, "updatefound", () => {
      const installing = registration.installing;
      if (!installing) return;
      listen(installing, "statechange", () => {
        if (installing.state === "installed" && navigator.serviceWorker.controller) {
          refreshWaitingUpdate();
        }
      });
    });
  };

  const registrationMatchesBuild = (registration, detail) => {
    if (!registration
        || !immutableExpectedAuthority
        || !detail?.scope
        || !detail?.scriptUrl) return false;

    let expectedScope;
    let expectedScriptUrl;
    try {
      expectedScope = new URL(immutableExpectedAuthority.scope).href;
      expectedScriptUrl = new URL(immutableExpectedAuthority.scriptUrl).href;
    } catch {
      return false;
    }

    if (detail.scope !== expectedScope || detail.scriptUrl !== expectedScriptUrl) return false;
    if (new URL(expectedScope).origin !== window.location.origin
        || new URL(expectedScriptUrl).origin !== window.location.origin) return false;
    if (registration.scope !== expectedScope) return false;
    return [registration.active, registration.waiting, registration.installing]
      .some((worker) => worker?.scriptURL === expectedScriptUrl);
  };

  const postCacheLeaseSweep = async () => {
    const authority = buildRegistrationAuthority;
    if (!("serviceWorker" in navigator) || !registrationStillMatchesAuthority(authority)) return;

    try {
      const active = authority.registration.active;
      if (!active || active.scriptURL !== authority.scriptUrl) return;
      active.postMessage({ type: CHUMMER_BUILD_PWA_CACHE_LEASE_SWEEP });
    } catch {
      // Cache reclamation is optional and fail-closed in the worker. A page must
      // never trade workspace availability for an unsuccessful maintenance pass.
    }
  };

  const scheduleCacheLeaseSweep = () => {
    if (cacheSweepScheduled) return;
    cacheSweepScheduled = true;
    cacheSweepTimer = window.setTimeout(() => {
      cacheSweepScheduled = false;
      cacheSweepTimer = null;
      void postCacheLeaseSweep();
    }, 0);
  };

  const isPlainExactMessage = (message, expectedKeys) => {
    if (!message
        || typeof message !== "object"
        || Array.isArray(message)
        || Object.getPrototypeOf(message) !== Object.prototype) {
      return false;
    }

    const actualKeys = Object.keys(message).sort();
    const sortedExpectedKeys = [...expectedKeys].sort();
    return actualKeys.length === sortedExpectedKeys.length
      && actualKeys.every((key, index) => key === sortedExpectedKeys[index]);
  };

  const isValidCacheLeaseRequestId = (requestId) =>
    typeof requestId === "string"
    && requestId.length >= 1
    && requestId.length <= 128
    && /^build-cache-lease-[0-9]+-[1-9][0-9]*$/.test(requestId);

  const isBuildWorkerSource = (event) => {
    const authority = buildRegistrationAuthority;
    if (!registrationStillMatchesAuthority(authority)) return false;
    const source = event.source;
    if (!source || typeof source.postMessage !== "function") return false;
    return source.scriptURL === authority.scriptUrl
      && [
        authority.registration.active,
        authority.registration.waiting,
        authority.registration.installing
      ].some((worker) => worker === source);
  };

  const isBuildWorkerLeaseRequest = (event) =>
    event.data?.type === CHUMMER_BUILD_PWA_CACHE_LEASE_REQUEST
    && isPlainExactMessage(event.data, ["type", "requestId"])
    && isValidCacheLeaseRequestId(event.data.requestId)
    && isBuildWorkerSource(event);

  const isBuildWorkerActivation = (event) =>
    event.data?.type === updateActivatedMessageType
    && isPlainExactMessage(event.data, ["type"])
    && isBuildWorkerSource(event);

  const bindBuildRegistration = (detail) => {
    const registration = detail?.registration;
    if (!registrationMatchesBuild(registration, detail)) {
      setStatus("Automatic Build update checks are unavailable. You can still follow the manual install steps.");
      return false;
    }

    buildRegistrationAuthority = Object.freeze({
      registration,
      scope: new URL(immutableExpectedAuthority.scope).href,
      scriptUrl: new URL(immutableExpectedAuthority.scriptUrl).href
    });
    watchRegistration(registration);
    scheduleCacheLeaseSweep();
    return true;
  };

  // A launcher rendered outside the Build worker's scope stays manual-only
  // until this page receives an explicit, validated registration handoff.
  listen(window, registrationEventName, (event) => {
    bindBuildRegistration(event.detail);
  });
  listen(window, registrationFailedEventName, () => {
    setStatus("Automatic Build install and update checks are unavailable. You can still follow the manual install steps.");
  });

  listen(root, "change", (event) => {
    const choice = event.target instanceof Element
      ? event.target.closest("[data-build-pwa-install-device-choice]")
      : null;
    if (!(choice instanceof HTMLInputElement)) return;
    setDevicePreference(choice.value);
    renderDeviceHandoff();
  });

  if (copyInstallLinkButton instanceof HTMLButtonElement) {
    listen(copyInstallLinkButton, "click", () => {
      void copyCanonicalInstallLink();
    });
  }

  const handleDeviceCapabilityChange = () => {
    if (readDevicePreference() === "auto") renderDeviceHandoff();
  };
  if (typeof coarsePointerMediaQuery.addEventListener === "function") {
    listen(coarsePointerMediaQuery, "change", handleDeviceCapabilityChange);
  } else if (typeof coarsePointerMediaQuery.addListener === "function") {
    coarsePointerMediaQuery.addListener(handleDeviceCapabilityChange);
    listenerRemovers.push(() => coarsePointerMediaQuery.removeListener(handleDeviceCapabilityChange));
  }

  listen(window, "beforeinstallprompt", (event) => {
    event.preventDefault();
    deferredInstallPrompt = event;
    if (installButton instanceof HTMLButtonElement) installButton.hidden = false;
    if (!guidanceDismissed) setPanelVisible(true);
    setStatus("Chummer Build is ready to install from this browser.");
  });

  listen(window, "appinstalled", () => {
    deferredInstallPrompt = null;
    setStatus("Chummer Build was installed on this device.");
    if (deviceStatus instanceof HTMLElement) {
      deviceStatus.textContent = "Chummer Build was installed. Returning focus to the workspace.";
    }
    hideInstalledControls();
  });

  if (helpButton instanceof HTMLButtonElement) {
    helpButton.hidden = false;
    launcherHydrationObserver = new MutationObserver(preserveLauncherAcrossHydration);
    launcherHydrationObserver.observe(helpButton, { attributes: true, attributeFilter: ["hidden"] });
    listen(helpButton, "click", () => {
      setPanelVisible(true);
      const effective = renderDeviceHandoff();
      if (manualSteps instanceof HTMLDetailsElement
          && !deferredInstallPrompt
          && effective === "mobile") manualSteps.open = true;
      const focusTarget = effective === "desktop"
        ? copyInstallLinkButton
        : installButton instanceof HTMLButtonElement && !installButton.hidden
          ? installButton
          : manualSteps?.querySelector("summary") || dismissButton;
      if (focusTarget instanceof HTMLElement) focusTarget.focus();
    });
  }

  if (dismissButton instanceof HTMLButtonElement) {
    listen(dismissButton, "click", () => {
      guidanceDismissed = true;
      try {
        window.sessionStorage.setItem(dismissalKey, "1");
      } catch {
        // A blocked preference store must not block dismissal for this page.
      }
      focusStableTargetBeforeHiding(true);
      setPanelVisible(false);
    });
  }

  if (installButton instanceof HTMLButtonElement) {
    listen(installButton, "click", async () => {
      const prompt = deferredInstallPrompt;
      if (!prompt) {
        showManualSteps("This browser did not offer a direct install prompt. Follow the manual steps below.");
        return;
      }

      installButton.disabled = true;
      setStatus("Waiting for your browser's install choice…");
      try {
        await prompt.prompt();
        const choice = await prompt.userChoice;
        deferredInstallPrompt = null;
        installButton.hidden = true;
        setStatus(choice?.outcome === "accepted"
          ? "Install accepted. Your browser will finish adding Chummer Build."
          : "Install was dismissed. You can still use the manual install steps below.");
      } catch {
        showManualSteps("The direct install prompt was unavailable. Follow the manual steps below.");
      } finally {
        installButton.disabled = false;
      }
    });
  }

  if (updateButton instanceof HTMLButtonElement) {
    listen(updateButton, "click", () => {
      void reviewWaitingUpdate();
    });
  }

  listen(window, integrityChangedEventName, (event) => {
    const next = event.detail;
    if (!next || (!updateRegistration?.waiting && !controllerChangedPending)) return;

    if (next.bridgeAvailable !== true) {
      deferUpdate(controllerChangedPending
        ? "Reload deferred because workspace safety could not be verified. Reconnect Chummer Build before reloading."
        : "The waiting update cannot be reviewed because workspace safety could not be verified. Reconnect and save or copy your work before closing every Chummer Build window.");
      return;
    }

    if (next.updateDeferred === true && next.isDirty !== true && next.hasConflict !== true) {
      if (!guidanceDismissed) setPanelVisible(true);
      if (updateButton instanceof HTMLButtonElement) {
        updateButton.hidden = false;
        updateButton.disabled = false;
        updateButton.textContent = controllerChangedPending ? "Reload updated app" : "Review update steps";
      }
      if (controllerChangedPending) {
        setStatus("This runner is now clean. Reload when you are ready to use the browser's active worker.");
      } else {
        setUpdateGuidanceVisible(true);
        setStatus("This runner is now saved. Close every Chummer Build tab and installed-app window, then reopen to start the waiting version.");
      }
    }
  });

  if ("serviceWorker" in navigator) {
    listen(navigator.serviceWorker, "message", (event) => {
      if (isBuildWorkerLeaseRequest(event)) {
        try {
          event.source.postMessage({
            type: CHUMMER_BUILD_PWA_CACHE_LEASE_RESPONSE,
            requestId: event.data.requestId,
            cacheVersion: CHUMMER_BUILD_PWA_CACHE_VERSION
          });
        } catch {
          // No response means the worker retains every managed cache.
        }
        return;
      }

      if (!isBuildWorkerActivation(event)) return;
      if (!hadControllerAtStartup) {
        setStatus("Chummer Build is installed. It will use the new app version after your next safe reload.");
        return;
      }

      controllerChangedPending = true;
      void commitReloadIfSafe();
    });

    listen(navigator.serviceWorker, "controllerchange", () => {
      if (!hadControllerAtStartup) {
        hadControllerAtStartup = true;
        return;
      }

      hadControllerAtStartup = true;
      controllerChangedPending = true;
      void commitReloadIfSafe();
    });

    const refreshPassiveWorkerState = () => {
      scheduleCacheLeaseSweep();
      refreshWaitingUpdate();
    };
    listen(window, "focus", refreshPassiveWorkerState);
    listen(window, "pageshow", refreshPassiveWorkerState);
    listen(document, "visibilitychange", () => {
      if (document.visibilityState === "visible") refreshPassiveWorkerState();
    });

    const pwa = window.chummerPwa;
    if (pwa?.registration) {
      bindBuildRegistration({
        registration: pwa.registration,
        scriptUrl: immutableExpectedAuthority?.scriptUrl,
        scope: immutableExpectedAuthority?.scope
      });
    }

    scheduleCacheLeaseSweep();
  }

  const handleDisplayModeChange = () => {
    if (!isStandalone()) appInstallConfirmed = false;
    renderInstallLauncherState();
  };
  if (typeof standaloneMediaQuery.addEventListener === "function") {
    listen(standaloneMediaQuery, "change", handleDisplayModeChange);
  } else if (typeof standaloneMediaQuery.addListener === "function") {
    standaloneMediaQuery.addListener(handleDisplayModeChange);
    listenerRemovers.push(() => standaloneMediaQuery.removeListener(handleDisplayModeChange));
  }

  const refresh = () => {
    if (disposed) return;
    renderInstallLauncherState();
    refreshWaitingUpdate();
    scheduleCacheLeaseSweep();
  };
  const dispose = () => {
    if (disposed) return;
    disposed = true;
    for (const remove of listenerRemovers.splice(0).reverse()) {
      try {
        remove();
      } catch {
        // Teardown is best-effort during document replacement.
      }
    }
    launcherHydrationObserver?.disconnect();
    launcherHydrationObserver = null;
    if (cacheSweepTimer !== null) window.clearTimeout(cacheSweepTimer);
    cacheSweepTimer = null;
    cacheSweepScheduled = false;
    if (window[controllerKey]?.dispose === dispose) delete window[controllerKey];
  };

  window[controllerKey] = Object.freeze({ refresh, dispose });
  renderInstallLauncherState();
})();
