(function (global) {
    'use strict';

    const analyticsConsentStorageKey = 'chummer.analytics.consent.v1';
    const analyticsProviderOptOutStorageKey = 'disable-rybbit';
    const analyticsConfigSelector = 'meta[name="chummer-analytics-config"]';
    const analyticsEventPrefix = 'chummer_';
    const fixedAnalyticsPath = '/editor';
    const allowedAnalyticsEvents = Object.freeze({
        editor_route: Object.freeze({
            route_family: new Set(['chummer_app', 'preview', 'showcase', 'downloads', 'docs', 'other'])
        }),
        editor_action: Object.freeze({
            action_category: new Set(['create', 'open', 'edit', 'save', 'print', 'export', 'validate', 'navigate'])
        }),
        editor_section: Object.freeze({
            section_category: new Set(['profile', 'attributes', 'skills', 'qualities', 'gear', 'combat', 'magic', 'matrix', 'contacts', 'rules', 'other'])
        }),
        editor_outcome: Object.freeze({
            outcome: new Set(['success', 'cancelled', 'conflict', 'unavailable'])
        }),
        editor_layout: Object.freeze({
            layout: new Set(['auto', 'compact', 'workspace'])
        })
    });

    function browserPrivacySignalEnabled() {
        if (navigator.globalPrivacyControl === true) {
            return true;
        }

        const signals = [navigator.doNotTrack, global.doNotTrack, navigator.msDoNotTrack];
        return signals.some((value) => {
            const normalized = String(value || '').trim().toLowerCase();
            return normalized === '1' || normalized === 'yes';
        });
    }

    function readStoredAnalyticsConsent() {
        try {
            return global.localStorage.getItem(analyticsConsentStorageKey) === 'granted';
        } catch (_error) {
            return false;
        }
    }

    function persistAnalyticsConsent(granted) {
        try {
            global.localStorage.setItem(analyticsConsentStorageKey, granted ? 'granted' : 'denied');
        } catch (_error) {
        }
    }

    function setProviderOptOut(disabled) {
        global.__RYBBIT_OPTOUT__ = disabled;
        try {
            if (disabled) {
                global.localStorage.setItem(analyticsProviderOptOutStorageKey, 'true');
            } else {
                global.localStorage.removeItem(analyticsProviderOptOutStorageKey);
            }
        } catch (_error) {
        }
    }

    function readAnalyticsProviderConfig() {
        const element = document.querySelector(analyticsConfigSelector);
        if (!(element instanceof HTMLMetaElement)) {
            return null;
        }

        const siteId = String(element.dataset.siteId || '').trim();
        if (!/^[a-z0-9_-]{1,128}$/i.test(siteId)) {
            return null;
        }

        let endpoint;
        try {
            endpoint = new URL(String(element.dataset.endpointUrl || ''), global.location.origin);
        } catch (_error) {
            return null;
        }

        const isSameOrigin = endpoint.origin === global.location.origin;
        if ((!isSameOrigin && endpoint.protocol !== 'https:')
            || endpoint.username
            || endpoint.password
            || endpoint.search
            || endpoint.hash) {
            return null;
        }

        return Object.freeze({ endpointUrl: endpoint.href, siteId });
    }

    function sanitizeCategoricalProperties(eventName, properties) {
        const schema = allowedAnalyticsEvents[eventName];
        if (!schema || !properties || typeof properties !== 'object' || Array.isArray(properties)) {
            return null;
        }

        const sanitized = {};
        for (const [key, allowedValues] of Object.entries(schema)) {
            const value = properties[key];
            if (typeof value === 'string' && allowedValues.has(value)) {
                sanitized[key] = value;
            }
        }

        return Object.keys(sanitized).length > 0 ? sanitized : null;
    }

    function routeFamilyFromLocation() {
        const path = global.location.pathname.toLowerCase().replace(/\/$/, '');
        if (path.endsWith('/blazor') || path.endsWith('/app') || path.endsWith('/online') || path.endsWith('/workbench')) {
            return 'chummer_app';
        }
        if (path.endsWith('/preview')) {
            return 'preview';
        }
        if (path.endsWith('/showcase')) {
            return 'showcase';
        }
        if (path.includes('/downloads')) {
            return 'downloads';
        }
        if (path.includes('/docs')) {
            return 'docs';
        }
        return 'other';
    }

    let analyticsConsentGranted = readStoredAnalyticsConsent() && !browserPrivacySignalEnabled();
    const activeAnalyticsRequests = new Set();
    setProviderOptOut(!analyticsConsentGranted);

    function abortActiveAnalyticsRequests() {
        for (const controller of activeAnalyticsRequests) {
            controller.abort();
        }
        activeAnalyticsRequests.clear();
    }

    function refreshAnalyticsConsentControls() {
        const privacySignalEnabled = browserPrivacySignalEnabled();
        const consentEffective = analyticsConsentGranted && !privacySignalEnabled;
        document.querySelectorAll('[data-chummer-analytics-preferences]').forEach((preferences) => {
            const status = preferences.querySelector('[data-chummer-analytics-consent-status]');
            const grant = preferences.querySelector('[data-chummer-analytics-consent-grant]');
            const revoke = preferences.querySelector('[data-chummer-analytics-consent-revoke]');
            if (status) {
                status.textContent = privacySignalEnabled
                    ? 'Anonymous editor analytics remain off because your browser privacy signal is enabled.'
                    : consentEffective
                        ? 'Anonymous editor analytics are on. Only the fixed categories described above are sent.'
                        : 'Anonymous editor analytics are off. No usage events are sent.';
            }
            if (grant instanceof HTMLButtonElement) {
                grant.hidden = consentEffective;
                grant.disabled = privacySignalEnabled;
                grant.setAttribute('aria-disabled', privacySignalEnabled ? 'true' : 'false');
            }
            if (revoke instanceof HTMLButtonElement) {
                revoke.hidden = !consentEffective;
            }
        });
    }

    function initializeAnalyticsConsentControls() {
        document.querySelectorAll('[data-chummer-analytics-preferences]').forEach((preferences) => {
            if (preferences.dataset.chummerAnalyticsInitialized === 'true') {
                return;
            }
            preferences.dataset.chummerAnalyticsInitialized = 'true';
            preferences.querySelector('[data-chummer-analytics-consent-grant]')
                ?.addEventListener('click', () => analytics.setConsent(true));
            preferences.querySelector('[data-chummer-analytics-consent-revoke]')
                ?.addEventListener('click', () => analytics.setConsent(false));
        });
        refreshAnalyticsConsentControls();
    }

    const analytics = global.chummerAnalytics = {};
    analytics.allowedEvents = Object.freeze(Object.keys(allowedAnalyticsEvents));
    analytics.status = function () {
        return Object.freeze({
            consentGranted: analyticsConsentGranted && !browserPrivacySignalEnabled(),
            privacySignalEnabled: browserPrivacySignalEnabled(),
            providerConfigured: readAnalyticsProviderConfig() !== null,
            automaticPageviews: false,
            pendingEventCount: 0
        });
    };
    analytics.setConsent = function (granted) {
        const requestedGrant = granted === true;
        if (requestedGrant && browserPrivacySignalEnabled()) {
            analyticsConsentGranted = false;
            persistAnalyticsConsent(false);
            setProviderOptOut(true);
            abortActiveAnalyticsRequests();
            refreshAnalyticsConsentControls();
            return false;
        }

        analyticsConsentGranted = requestedGrant;
        persistAnalyticsConsent(requestedGrant);
        setProviderOptOut(!requestedGrant);
        if (!requestedGrant) {
            abortActiveAnalyticsRequests();
        }
        refreshAnalyticsConsentControls();
        return analyticsConsentGranted;
    };
    analytics.event = async function (eventName, properties) {
        if (!analyticsConsentGranted || browserPrivacySignalEnabled()) {
            return false;
        }

        const config = readAnalyticsProviderConfig();
        const sanitized = sanitizeCategoricalProperties(eventName, properties);
        if (!config || !sanitized) {
            return false;
        }

        const payload = {
            site_id: config.siteId,
            type: 'custom_event',
            pathname: fixedAnalyticsPath,
            event_name: `${analyticsEventPrefix}${eventName}`,
            properties: JSON.stringify(sanitized)
        };

        const controller = new AbortController();
        activeAnalyticsRequests.add(controller);
        try {
            const response = await global.fetch(config.endpointUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload),
                mode: 'cors',
                credentials: 'omit',
                cache: 'no-store',
                redirect: 'error',
                referrerPolicy: 'no-referrer',
                keepalive: true,
                signal: controller.signal
            });
            return response.ok;
        } catch (_error) {
            return false;
        } finally {
            activeAnalyticsRequests.delete(controller);
        }
    };
    analytics.trackRoute = function () {
        return analytics.event('editor_route', { route_family: routeFamilyFromLocation() });
    };
    initializeAnalyticsConsentControls();

    const allowedPrintMimeTypes = new Set([
        'text/html',
        'text/plain',
        'application/json',
        'application/xml',
        'text/xml'
    ]);
    const allowedPrintElements = new Set([
        'ARTICLE', 'BLOCKQUOTE', 'BR', 'CODE', 'DD', 'DIV', 'DL', 'DT', 'EM',
        'FOOTER', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'HEADER', 'HR', 'IMG',
        'LI', 'MAIN', 'OL', 'P', 'PRE', 'SECTION', 'SMALL', 'SPAN', 'STRONG',
        'SUB', 'SUP', 'TABLE', 'TBODY', 'TD', 'TFOOT', 'TH', 'THEAD', 'TR', 'UL'
    ]);
    const discardedPrintElements = new Set([
        'BASE', 'BUTTON', 'CANVAS', 'EMBED', 'FORM', 'FRAME', 'FRAMESET', 'IFRAME',
        'INPUT', 'LINK', 'MATH', 'META', 'NOSCRIPT', 'OBJECT', 'SCRIPT', 'SELECT',
        'SOURCE', 'STYLE', 'SVG', 'TEMPLATE', 'TEXTAREA', 'VIDEO', 'AUDIO'
    ]);
    const safeRasterDataUrlPattern = /^data:image\/(?:png|jpeg|gif|webp);base64,[a-z0-9+/=\s]+$/i;
    const maxPrintBase64Length = 8 * 1024 * 1024;
    const printCsp = "default-src 'none'; script-src 'none'; connect-src 'none'; img-src data:; style-src 'unsafe-inline'; font-src 'none'; media-src 'none'; object-src 'none'; frame-src 'none'; child-src 'none'; worker-src 'none'; base-uri 'none'; form-action 'none'";
    const trustedPrintCss = '@page{margin:12mm}html{color:#111;background:#fff;font:11pt/1.45 system-ui,sans-serif}body{margin:0}h1,h2,h3,h4,h5,h6{break-after:avoid}table{width:100%;border-collapse:collapse}th,td{border:1px solid #777;padding:.3rem;text-align:left;vertical-align:top}pre,code{white-space:pre-wrap;overflow-wrap:anywhere}img{max-width:100%;height:auto}';

    function normalizePrintMimeType(value) {
        const normalized = String(value || '').split(';', 1)[0].trim().toLowerCase();
        if (!allowedPrintMimeTypes.has(normalized)) {
            throw new TypeError('Unsupported print MIME type.');
        }
        return normalized;
    }

    function decodePrintBase64(contentBase64) {
        const encoded = String(contentBase64 || '');
        if (!encoded || encoded.length > maxPrintBase64Length) {
            throw new TypeError('Print content is empty or too large.');
        }

        const binary = global.atob(encoded);
        const bytes = new Uint8Array(binary.length);
        for (let index = 0; index < binary.length; index += 1) {
            bytes[index] = binary.charCodeAt(index);
        }
        return new TextDecoder('utf-8', { fatal: true }).decode(bytes);
    }

    function copySafePrintAttributes(source, target) {
        const tagName = source.localName.toUpperCase();
        const direction = source.getAttribute('dir');
        if (direction && ['ltr', 'rtl', 'auto'].includes(direction.toLowerCase())) {
            target.setAttribute('dir', direction.toLowerCase());
        }

        if (tagName === 'TH') {
            const scope = String(source.getAttribute('scope') || '').toLowerCase();
            if (['row', 'col', 'rowgroup', 'colgroup'].includes(scope)) {
                target.setAttribute('scope', scope);
            }
        }

        if (tagName === 'TD' || tagName === 'TH') {
            for (const attribute of ['colspan', 'rowspan']) {
                const value = Number.parseInt(source.getAttribute(attribute) || '', 10);
                if (Number.isInteger(value) && value >= 1 && value <= 100) {
                    target.setAttribute(attribute, String(value));
                }
            }
        }

        if (tagName === 'IMG') {
            const sourceUrl = String(source.getAttribute('src') || '');
            if (sourceUrl.length <= 2 * 1024 * 1024 && safeRasterDataUrlPattern.test(sourceUrl)) {
                target.setAttribute('src', sourceUrl);
            }
            const alternativeText = String(source.getAttribute('alt') || '').slice(0, 500);
            target.setAttribute('alt', alternativeText);
        }
    }

    function appendSanitizedPrintNode(source, destinationParent, destinationDocument) {
        if (source.nodeType === Node.TEXT_NODE) {
            destinationParent.appendChild(destinationDocument.createTextNode(source.nodeValue || ''));
            return;
        }
        if (source.nodeType !== Node.ELEMENT_NODE) {
            return;
        }

        const sourceElement = source;
        const tagName = sourceElement.localName.toUpperCase();
        if (sourceElement.namespaceURI !== 'http://www.w3.org/1999/xhtml'
            || discardedPrintElements.has(tagName)) {
            return;
        }
        if (!allowedPrintElements.has(tagName)) {
            for (const child of sourceElement.childNodes) {
                appendSanitizedPrintNode(child, destinationParent, destinationDocument);
            }
            return;
        }

        const destination = destinationDocument.createElement(tagName.toLowerCase());
        copySafePrintAttributes(sourceElement, destination);
        for (const child of sourceElement.childNodes) {
            appendSanitizedPrintNode(child, destination, destinationDocument);
        }
        destinationParent.appendChild(destination);
    }

    function buildSafePrintDocument(decoded, mimeType, title) {
        const safeDocument = document.implementation.createHTMLDocument('');
        safeDocument.documentElement.setAttribute('lang', 'en');
        safeDocument.head.replaceChildren();

        const charset = safeDocument.createElement('meta');
        charset.setAttribute('charset', 'utf-8');
        const policy = safeDocument.createElement('meta');
        policy.setAttribute('http-equiv', 'Content-Security-Policy');
        policy.setAttribute('content', printCsp);
        const referrer = safeDocument.createElement('meta');
        referrer.setAttribute('name', 'referrer');
        referrer.setAttribute('content', 'no-referrer');
        const safeTitle = safeDocument.createElement('title');
        safeTitle.textContent = String(title || 'Chummer print').slice(0, 200);
        const style = safeDocument.createElement('style');
        style.textContent = trustedPrintCss;
        safeDocument.head.append(charset, policy, referrer, safeTitle, style);

        safeDocument.body.replaceChildren();
        if (mimeType === 'text/html') {
            const parsed = new DOMParser().parseFromString(decoded, 'text/html');
            for (const child of parsed.body.childNodes) {
                appendSanitizedPrintNode(child, safeDocument.body, safeDocument);
            }
        } else {
            const preformatted = safeDocument.createElement('pre');
            preformatted.textContent = decoded;
            safeDocument.body.appendChild(preformatted);
        }

        return `<!doctype html>${safeDocument.documentElement.outerHTML}`;
    }

    function removeExistingPrintSurface() {
        document.querySelectorAll('[data-chummer-print-surface], [data-chummer-print-style]')
            .forEach((element) => element.remove());
    }

    const prints = global.chummerPrints = {};
    prints.openBase64 = function (fileName, contentBase64, mimeType, title) {
        const normalizedMimeType = normalizePrintMimeType(mimeType);
        const decoded = decodePrintBase64(contentBase64);
        const printTitle = String(title || fileName || 'Chummer print');
        const safePrintDocument = buildSafePrintDocument(decoded, normalizedMimeType, printTitle);
        removeExistingPrintSurface();

        const printStyle = document.createElement('style');
        printStyle.setAttribute('data-chummer-print-style', 'true');
        printStyle.textContent = '@media print{body> *:not([data-chummer-print-surface]):not([data-chummer-print-style]){display:none!important}[data-chummer-print-surface]{display:block!important;position:static!important;width:100%!important;height:100vh!important;border:0!important}}';

        const frame = document.createElement('iframe');
        frame.setAttribute('data-chummer-print-surface', 'true');
        frame.setAttribute('sandbox', '');
        frame.setAttribute('referrerpolicy', 'no-referrer');
        frame.setAttribute('aria-label', 'Sandboxed Chummer print preview');
        frame.style.cssText = 'position:fixed;left:-10000px;top:0;width:1px;height:1px;border:0;';
        frame.srcdoc = safePrintDocument;

        let printTriggered = false;
        let cleanupTimer = 0;
        const cleanup = function () {
            global.clearTimeout(cleanupTimer);
            frame.remove();
            printStyle.remove();
        };
        const triggerPrint = function () {
            if (printTriggered) {
                return;
            }
            printTriggered = true;
            global.requestAnimationFrame(() => global.print());
        };

        global.addEventListener('afterprint', cleanup, { once: true });
        frame.addEventListener('load', triggerPrint, { once: true });
        document.body.append(printStyle, frame);
        cleanupTimer = global.setTimeout(cleanup, 120000);
        return true;
    };
})(window);
