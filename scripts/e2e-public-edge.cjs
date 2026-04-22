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
  '/participate',
  '/contact',
  '/what-is-chummer',
  '/artifacts',
  '/faq'
];

const checks = [
  {
    url: `${baseUrl}/`,
    assert: text =>
      text.includes('Chummer')
      && text.includes('Create account to install')
      && requiredLandingLinks.every(link => text.includes(link))
  },
  {
    url: `${baseUrl}/downloads/`,
    assert: text =>
      text.includes('Install the current preview')
      && text.includes('Install Chummer')
      && text.includes('Main platform downloads')
      && text.includes('Chummer for Windows')
      && text.includes('avalonia-win-x64-installer')
      && !text.includes('Create account to get preview')
  },
  {
    url: `${baseUrl}/downloads/`,
    headers: {
      'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)'
    },
    assert: text =>
      text.includes('Recommended for Windows')
      && text.includes('Create account to install')
      && text.includes('avalonia-win-x64-installer')
      && !text.includes('Open Windows preview build')
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
        && location.includes('/auth/google/start?next=')
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
          && location.includes('/auth/google/start?next=')
          && decodeURIComponent(location).includes('/downloads/install/avalonia-win-x64-installer'))
        || (response.status === 200
          && text.includes('Start download again')
          && (text.includes('setup .exe') || text.includes('default browser')))
      );
    }
  },
  {
    url: `${baseUrl}/roadmap/shadowcasters-network`,
    assert: text =>
      text.includes('SHADOWCASTERS NETWORK')
      && text.includes('Why this horizon matters now')
      && text.includes('Need a decision instead?')
      && text.includes('/roadmap/black-ledger')
  },
  {
    url: `${baseUrl}/roadmap/black-ledger`,
    assert: text =>
      text.includes('BLACK LEDGER')
      && text.includes('Why this horizon matters now')
      && text.includes('Need a decision instead?')
      && text.includes('/artifacts/replay-after-action')
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
    assert: text => text.includes('Artifacts')
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
      && text.includes('Install the current preview')
  },
  {
    url: `${baseUrl}/avalonia/`,
    assert: (text, response) =>
      /\/downloads\/?$/.test(response.url)
      && text.includes('Install the current preview')
  },
  {
    url: `${baseUrl}/session/`,
    assert: (text, response) =>
      /\/participate\/?$/.test(response.url)
      && text.includes('Participate')
  },
  {
    url: `${baseUrl}/coach/`,
    assert: (text, response) =>
      /\/status\/?$/.test(response.url)
      && text.includes('Status')
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
