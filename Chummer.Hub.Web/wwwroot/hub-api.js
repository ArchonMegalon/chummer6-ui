window.chummerHubApi = window.chummerHubApi || {};
window.chummerHubApi.send = async function(path, method, body) {
    const options = {
        method: method || "GET",
        credentials: "same-origin",
        headers: {}
    };

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
