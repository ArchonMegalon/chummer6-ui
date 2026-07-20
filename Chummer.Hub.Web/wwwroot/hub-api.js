window.chummerHubApi = window.chummerHubApi || {};
window.chummerHubApi.send = async function(path, method, body) {
    const normalizedMethod = String(method || "GET").toUpperCase();
    const options = {
        method: normalizedMethod,
        credentials: "same-origin",
        cache: "no-store",
        referrerPolicy: "no-referrer",
        headers: {}
    };

    const unsafeMethods = new Set(["POST", "PUT", "PATCH", "DELETE"]);
    if (unsafeMethods.has(normalizedMethod)) {
        const tokenResponse = await fetch("/api/v1/antiforgery", {
            method: "GET",
            credentials: "same-origin",
            cache: "no-store",
            referrerPolicy: "no-referrer",
            headers: {
                "Accept": "application/json"
            }
        });
        if (!tokenResponse.ok) {
            throw new Error("Antiforgery handoff is unavailable.");
        }

        const tokenPayload = await tokenResponse.json();
        const requestToken = String(tokenPayload && tokenPayload.requestToken || "");
        const headerName = String(tokenPayload && tokenPayload.headerName || "");
        if (!requestToken
            || requestToken.length > 8192
            || !/^[A-Za-z][A-Za-z0-9-]{0,63}$/.test(headerName)) {
            throw new Error("Antiforgery handoff is invalid.");
        }

        options.headers[headerName] = requestToken;
    }

    if (body !== undefined && body !== null && body !== "") {
        options.headers["Content-Type"] = "application/json";
        options.body = body;
    }

    const response = await fetch(path, options);
    const text = await response.text();
    return JSON.stringify({
        status: response.status,
        text: text
    });
};
