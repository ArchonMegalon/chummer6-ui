#!/usr/bin/env node
'use strict';

const baseUrl = (process.env.CHUMMER_PORTAL_BASE_URL || 'http://127.0.0.1:8091').replace(/\/$/, '');
const portalPublicHost = process.env.CHUMMER_PORTAL_PUBLIC_HOST || 'chummer.run';
const useForwardedPublicHeaders = /^http:\/\/(?:127\.0\.0\.1|localhost)(?::\d+)?$/i.test(baseUrl);
const defaultHeaders = useForwardedPublicHeaders
  ? {
      Host: portalPublicHost,
      'X-Forwarded-Proto': 'https'
    }
  : {};

const requiredLandingLinks = [
  '/downloads',
  '/now',
  '/help',
  '/signup',
  '/ledger',
  '/contact',
  '/faq'
];

const checks = [
  {
    url: `${baseUrl}/`,
    assert: text =>
      text.includes('Chummer')
      && text.includes('Open downloads')
      && text.includes('Black Ledger command deck')
      && requiredLandingLinks.every(link => text.includes(link))
  },
  {
    url: `${baseUrl}/downloads/`,
    assert: text =>
      text.includes('Install Chummer')
      && text.includes('guided install handoff')
      && text.includes('Create an account first')
      && text.includes('Main platform downloads')
      && text.includes('Chummer for Windows')
      && text.includes('Recommended desktop build for Linux')
  },
  {
    url: `${baseUrl}/downloads/`,
    headers: {
      'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)'
    },
    assert: text =>
      text.includes('Recommended for Windows')
      && text.includes('Chummer for Windows')
      && text.includes('guided install handoff')
      && text.includes('Open downloads')
  },
  {
    url: `${baseUrl}/downloads/releases.json`,
    assert: text => {
      const payload = JSON.parse(text);
      return typeof payload?.version === 'string'
        && typeof payload?.channel === 'string'
        && Array.isArray(payload?.downloads)
        && payload.downloads.length > 0;
    }
  },
  {
    url: `${baseUrl}/downloads/install/avalonia-linux-x64-installer`,
    redirect: 'manual',
    assert: (_text, response) => {
      const location = response.headers.get('location') || '';
      return [301, 302, 303, 307, 308].includes(response.status)
        && location.includes('/login?next=')
        && decodeURIComponent(location).includes('/downloads/install/avalonia-linux-x64-installer');
    }
  },
  {
    url: `${baseUrl}/downloads/install/avalonia-win-x64-installer`,
    redirect: 'manual',
    assert: (text, response) => {
      const location = response.headers.get('location') || '';
      return (
        ([301, 302, 303, 307, 308].includes(response.status)
          && location.includes('/login?next=')
          && decodeURIComponent(location).includes('/downloads/install/avalonia-win-x64-installer'))
        || (response.status === 200
          && text.includes('Start download again')
          && (text.includes('setup .exe') || text.includes('default browser')))
      );
    }
  },
  {
    url: `${baseUrl}/play`,
    assert: text =>
      text.includes('Player entry')
      && text.includes('Installable app shell live')
      && text.includes('Offline and reconnect lane cached')
  },
  {
    url: `${baseUrl}/status`,
    assert: text =>
      text.includes('Current status')
      && text.includes('Public Stable')
      && text.includes('What works now, what needs caution, and where to go next')
  },
  {
    url: `${baseUrl}/ledger`,
    assert: text =>
      text.includes('Black Ledger command deck')
      && text.includes('Emerald Sprawl: First Pressure')
      && text.includes('Open command map')
  },
  {
    url: `${baseUrl}/contact`,
    assert: text =>
      text.includes('Open the right support case')
      && text.includes('Product bug')
  },
  {
    url: `${baseUrl}/what-is-chummer`,
    assert: text => text.includes('What Is Chummer?')
  },
  {
    url: `${baseUrl}/artifacts`,
    assert: text => text.includes('Proof gallery')
  },
  {
    url: `${baseUrl}/faq`,
    assert: text => text.includes('FAQ')
  },
  {
    url: `${baseUrl}/hub`,
    assert: (text, response) =>
      response.url.endsWith('/login?next=%2Faccount')
      && text.includes('Sign in')
  },
  {
    url: `${baseUrl}/hub/`,
    assert: (text, response) =>
      response.url.endsWith('/login?next=%2Faccount')
      && text.includes('Sign in')
  },
  {
    url: `${baseUrl}/blazor/`,
    assert: (text, response) =>
      /\/downloads\/?$/.test(response.url)
      && text.includes('Install Chummer')
  },
  {
    url: `${baseUrl}/avalonia/`,
    assert: (text, response) =>
      /\/downloads\/?$/.test(response.url)
      && text.includes('Install Chummer')
  },
  {
    url: `${baseUrl}/session/`,
    assert: (text, response) =>
      /\/play\/?$/.test(response.url)
      && text.includes('Player entry')
  },
  {
    url: `${baseUrl}/coach/`,
    assert: (text, response) =>
      /\/status\/?$/.test(response.url)
      && text.includes('Current status')
  }
];

(async () => {
  for (const check of checks) {
    const response = await fetch(check.url, {
      method: check.method ?? 'GET',
      headers: {
        ...defaultHeaders,
        ...(check.headers || {})
      },
      body: check.body,
      redirect: check.redirect ?? 'follow'
    });
    const body = await response.text();
    const statusAccepted = response.ok || [301, 302, 303, 307, 308].includes(response.status);
    if (!statusAccepted) {
      throw new Error(`Portal check failed: ${check.url} -> HTTP ${response.status}`);
    }

    let passed = false;
    try {
      passed = Boolean(check.assert(body, response));
    } catch (error) {
      throw new Error(`Portal check failed: ${check.url} -> assertion threw: ${error.message}`);
    }

    if (!passed) {
      throw new Error(`Portal check failed: ${check.url} -> assertion returned false`);
    }

    console.log(`ok: ${check.url}`);
  }

  console.log('public-edge route probe completed');
})().catch(error => {
  console.error(error.message);
  process.exit(1);
});
