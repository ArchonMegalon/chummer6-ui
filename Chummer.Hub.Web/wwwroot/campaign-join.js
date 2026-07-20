(function () {
    "use strict";

    const inviteKeys = new Set(["secret", "invite", "invitesecret", "campaigninvite"]);

    function containsInviteKey(params) {
        for (const key of params.keys()) {
            if (inviteKeys.has(String(key).toLowerCase())) {
                return true;
            }
        }

        return false;
    }

    function readInviteFragment() {
        const query = new URLSearchParams(window.location.search || "");
        if (containsInviteKey(query)) {
            return {
                status: "rejected-query",
                secret: null,
                mustScrub: true
            };
        }

        const rawFragment = String(window.location.hash || "").replace(/^#/, "");
        if (!rawFragment) {
            return {
                status: "none",
                secret: null,
                mustScrub: false
            };
        }

        const fragment = new URLSearchParams(rawFragment);
        if (!containsInviteKey(fragment)) {
            return {
                status: "none",
                secret: null,
                mustScrub: false
            };
        }

        const inviteValues = [];
        for (const [key, value] of fragment.entries()) {
            if (inviteKeys.has(String(key).toLowerCase())) {
                inviteValues.push(String(value || ""));
            }
        }

        if (inviteValues.length !== 1 || !inviteValues[0] || inviteValues[0].length > 256) {
            return {
                status: "invalid-fragment",
                secret: null,
                mustScrub: true
            };
        }

        return {
            status: "fragment",
            secret: inviteValues[0],
            mustScrub: true
        };
    }

    function scrubInviteLocation(safePath) {
        let cleanPath = window.location.pathname || "/";
        if (typeof safePath === "string"
            && safePath.startsWith("/")
            && !safePath.includes("?")
            && !safePath.includes("#")) {
            const candidate = new URL(safePath, window.location.origin);
            if (candidate.origin === window.location.origin) {
                cleanPath = candidate.pathname;
            }
        }

        window.history.replaceState(window.history.state, document.title, cleanPath);
    }

    window.chummerCampaignJoin = Object.freeze({
        readInviteFragment,
        scrubInviteLocation
    });
}());
