from __future__ import annotations

import json
from pathlib import Path
import subprocess

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
INSTALL_SCRIPT = REPO_ROOT / "Chummer.Blazor" / "wwwroot" / "js" / "build-pwa-install.js"
GOLDEN_SIGNATURES = {
    "http://127.0.0.1:41789/app": "eacbfd90",
    "http://127.0.0.1:41789/blazor/app": "08b160cc",
}


def _encode_with_runtime(payloads: list[str]) -> dict[str, object]:
    probe = r"""
const fs = require("fs");
global.HTMLElement = class HTMLElement {};
global.window = {};
global.document = { querySelector: () => null };
eval(fs.readFileSync(process.argv[1], "utf8"));
const handoff = window.chummerBuildPwaHandoff;
const payloads = JSON.parse(process.argv[2]);
const origin = "http://127.0.0.1:41789";
const encoded = payloads.map((payload) => {
  const matrix = handoff.encodeQrMatrix(payload);
  return {
    payload,
    version: matrix.version,
    size: matrix.size,
    mask: matrix.mask,
    signature: handoff.matrixSignature(matrix),
    modules: matrix.modules
  };
});
let capacityError = "";
try {
  handoff.encodeQrMatrix("x".repeat(272));
} catch (error) {
  capacityError = String(error?.message || error);
}
let rejectedExternalScope = false;
try {
  handoff.buildCanonicalInstallUrl({ origin, scope: "https://example.invalid/blazor/" });
} catch {
  rejectedExternalScope = true;
}
process.stdout.write(JSON.stringify({
  encoded,
  capacityError,
  canonical: {
    root: handoff.buildCanonicalInstallUrl({
      origin,
      scope: `${origin}/?workspace=private&runner=private&token=private#owner`
    }),
    pathBase: handoff.buildCanonicalInstallUrl({
      origin,
      scope: `${origin}/blazor/?auth=private&owner=private#workspace`
    }),
    rejectedExternalScope
  },
  deviceCases: {
    uaMobile: handoff.resolveEffectiveDevice("auto", {
      standalone: false, userAgentDataMobile: true, coarsePointer: false, maxTouchPoints: 0
    }),
    uaDesktop: handoff.resolveEffectiveDevice("auto", {
      standalone: false, userAgentDataMobile: false, coarsePointer: true, maxTouchPoints: 10
    }),
    touchFallback: handoff.resolveEffectiveDevice("auto", {
      standalone: false, userAgentDataMobile: null, coarsePointer: true, maxTouchPoints: 5
    }),
    noTouchFallback: handoff.resolveEffectiveDevice("auto", {
      standalone: false, userAgentDataMobile: null, coarsePointer: true, maxTouchPoints: 0
    }),
    desktopOverride: handoff.resolveEffectiveDevice("desktop", {
      standalone: false, userAgentDataMobile: true, coarsePointer: true, maxTouchPoints: 5
    }),
    mobileOverride: handoff.resolveEffectiveDevice("mobile", {
      standalone: false, userAgentDataMobile: false, coarsePointer: false, maxTouchPoints: 0
    }),
    standalone: handoff.resolveEffectiveDevice("mobile", {
      standalone: true, userAgentDataMobile: true, coarsePointer: true, maxTouchPoints: 5
    })
  }
}));
"""
    result = subprocess.run(
        ["node", "-e", probe, str(INSTALL_SCRIPT), json.dumps(payloads)],
        cwd=REPO_ROOT,
        check=True,
        capture_output=True,
        text=True,
    )
    return json.loads(result.stdout)


def test_handoff_contract_classifies_without_ua_sniffing_and_cleans_urls() -> None:
    result = _encode_with_runtime(list(GOLDEN_SIGNATURES))

    assert result["canonical"] == {
        "root": "http://127.0.0.1:41789/app",
        "pathBase": "http://127.0.0.1:41789/blazor/app",
        "rejectedExternalScope": True,
    }
    assert result["deviceCases"] == {
        "uaMobile": "mobile",
        "uaDesktop": "desktop",
        "touchFallback": "mobile",
        "noTouchFallback": "desktop",
        "desktopOverride": "desktop",
        "mobileOverride": "mobile",
        "standalone": "standalone",
    }
    assert "too long" in result["capacityError"]


def test_local_qr_is_independently_decodable_for_root_and_pathbase() -> None:
    cv2 = pytest.importorskip("cv2")
    numpy = pytest.importorskip("numpy")
    result = _encode_with_runtime(list(GOLDEN_SIGNATURES))

    assert "too long" in result["capacityError"]
    assert len(result["encoded"]) == 2
    decoder = cv2.QRCodeDetector()
    for encoded in result["encoded"]:
        payload = encoded["payload"]
        modules = numpy.asarray(encoded["modules"], dtype=numpy.uint8)

        assert encoded["version"] == 10
        assert encoded["size"] == 57
        assert 0 <= encoded["mask"] <= 7
        assert encoded["signature"] == GOLDEN_SIGNATURES[payload]
        assert modules.shape == (57, 57)
        assert set(numpy.unique(modules)).issubset({0, 1})

        # OpenCV is independent from the in-browser encoder. Its successful
        # decode proves the golden module matrix is a standards-compliant QR,
        # rather than merely agreeing with the encoder's own signature helper.
        pixels = numpy.where(modules, 0, 255).astype(numpy.uint8)
        pixels = numpy.pad(pixels, 4, constant_values=255)
        image = cv2.resize(
            pixels,
            None,
            fx=12,
            fy=12,
            interpolation=cv2.INTER_NEAREST,
        )
        decoded, points, _ = decoder.detectAndDecode(image)
        assert points is not None
        assert decoded == payload
