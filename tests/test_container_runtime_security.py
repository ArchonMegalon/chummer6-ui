from __future__ import annotations

import os
import re
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]

RUNTIME_DOCKERFILES = (
    Path("Chummer.Api/Dockerfile"),
    Path("Chummer.Blazor/Dockerfile"),
    Path("Chummer.Portal/Dockerfile"),
    Path("Chummer.Hub.Web/Dockerfile"),
    Path("Chummer.Avalonia.Browser/Dockerfile"),
)

STATEFUL_DOCKERFILES = (
    Path("Chummer.Api/Dockerfile"),
    Path("Chummer.Blazor/Dockerfile"),
)

RUNTIME_SERVICES = (
    "chummer-api",
    "chummer-blazor",
    "chummer-blazor-portal",
    "chummer-hub-web-portal",
    "chummer-avalonia-browser",
    "chummer-portal",
)


def _final_stage(dockerfile: Path) -> str:
    text = (ROOT / dockerfile).read_text(encoding="utf-8")
    stages = re.split(r"(?m)(?=^FROM\s)", text)
    return stages[-1]


def _service_block(compose: str, service: str) -> str:
    marker = f"  {service}:\n"
    start = compose.index(marker)
    remainder = compose[start + len(marker) :]
    next_service = re.search(r"(?m)^  [a-z0-9][a-z0-9-]*:\s*$", remainder)
    end = start + len(marker) + (next_service.start() if next_service else len(remainder))
    return compose[start:end]


def test_every_public_runtime_image_uses_the_fixed_dotnet_app_identity() -> None:
    for dockerfile in RUNTIME_DOCKERFILES:
        final_stage = _final_stage(dockerfile)
        content_mode_index = final_stage.index("chmod -R u=rwX,go=rX /app")
        user_index = final_stage.index("USER $APP_UID:$APP_UID")
        entrypoint_index = final_stage.index("ENTRYPOINT")

        assert content_mode_index < user_index, dockerfile
        assert user_index < entrypoint_index, dockerfile
        assert "USER root" not in final_stage[user_index:], dockerfile

        full_dockerfile = (ROOT / dockerfile).read_text(encoding="utf-8")
        assert "--mount=type=cache,id=chummer-nuget-packages" in full_dockerfile, dockerfile
        assert "--mount=type=cache,id=chummer-nuget-http" in full_dockerfile, dockerfile
        assert "--mount=type=cache,id=chummer-nuget-plugins" in full_dockerfile, dockerfile
        assert full_dockerfile.count("-p:RestorePackagesPath=/root/.nuget/packages") == 2, dockerfile


def test_stateful_images_preown_only_the_state_mount_before_dropping_root() -> None:
    expected = 'install -d -o "$APP_UID" -g "$APP_UID" -m 0700 /app/state'

    for dockerfile in STATEFUL_DOCKERFILES:
        final_stage = _final_stage(dockerfile)
        state_index = final_stage.index(expected)
        user_index = final_stage.index("USER $APP_UID:$APP_UID")

        assert state_index < user_index, dockerfile
        assert "chown -R" not in final_stage, dockerfile


def test_public_compose_services_drop_privilege_and_all_capabilities() -> None:
    compose = (ROOT / "docker-compose.yml").read_text(encoding="utf-8")

    for service in RUNTIME_SERVICES:
        block = _service_block(compose, service)

        assert "    init: true\n" in block, service
        assert '    restart: "${CHUMMER_RESTART_POLICY:-unless-stopped}"\n' in block, service
        assert "    stop_grace_period: 30s\n" in block, service
        assert "    logging:\n      driver: local\n" in block, service
        assert '        max-size: "10m"\n' in block, service
        assert '        max-file: "5"\n' in block, service
        assert "    read_only: true\n" in block, service
        assert "    tmpfs:\n      - /tmp:rw,nosuid,nodev,noexec,size=64m,mode=1777\n" in block, service
        assert "    security_opt:\n      - no-new-privileges:true\n" in block, service
        assert "    cap_drop:\n      - ALL\n" in block, service
        assert "cap_add:" not in block, service
        assert "privileged:" not in block, service


def test_every_public_runtime_has_a_bounded_local_healthcheck() -> None:
    compose = (ROOT / "docker-compose.yml").read_text(encoding="utf-8")

    for service in RUNTIME_SERVICES:
        block = _service_block(compose, service)
        assert "    healthcheck:\n" in block, service
        assert "http://127.0.0.1:8080" in block, service
        assert "      interval: 30s\n" in block, service
        assert "      timeout: 10s\n" in block, service
        assert "      retries: 3\n" in block, service
        assert "      start_period: 30s\n" in block, service

    for dockerfile in (
        Path("Chummer.Portal/Dockerfile"),
        Path("Chummer.Hub.Web/Dockerfile"),
        Path("Chummer.Avalonia.Browser/Dockerfile"),
    ):
        final_stage = _final_stage(dockerfile)
        assert "apt-get install -y --no-install-recommends curl" in final_stage, dockerfile


def test_runtime_dependencies_wait_for_health() -> None:
    compose = (ROOT / "docker-compose.yml").read_text(encoding="utf-8")

    expected = {
        "chummer-blazor": ("chummer-api",),
        "chummer-blazor-portal": ("chummer-api",),
        "chummer-hub-web-portal": ("chummer-api",),
        "chummer-portal": (
            "chummer-api",
            "chummer-blazor-portal",
            "chummer-hub-web-portal",
            "chummer-avalonia-browser",
        ),
    }
    for service, dependencies in expected.items():
        block = _service_block(compose, service)
        for dependency in dependencies:
            assert f"      {dependency}:\n        condition: service_healthy\n" in block, (service, dependency)


def test_api_healthcheck_uses_the_state_volume_readiness_route() -> None:
    compose = (ROOT / "docker-compose.yml").read_text(encoding="utf-8")
    api = _service_block(compose, "chummer-api")
    endpoints = (ROOT / "Chummer.Api/Endpoints/InfoEndpoints.cs").read_text(encoding="utf-8")
    probe = (ROOT / "Chummer.Api/Health/StateVolumeReadinessProbe.cs").read_text(encoding="utf-8")

    assert "http://127.0.0.1:8080/health/ready" in api
    assert 'app.MapGet("/health/ready"' in endpoints
    assert "FileOptions.DeleteOnClose | FileOptions.WriteThrough" in probe
    assert "File.GetUnixFileMode(_stateRoot) != PrivateDirectoryMode" in probe


def test_hub_has_certificate_encrypted_restart_safe_data_protection_storage() -> None:
    compose = (ROOT / "docker-compose.yml").read_text(encoding="utf-8")
    hub = _service_block(compose, "chummer-hub-web-portal")
    dockerfile = (ROOT / "Chummer.Hub.Web/Dockerfile").read_text(encoding="utf-8")
    program = (ROOT / "Chummer.Hub.Web/Program.cs").read_text(encoding="utf-8")
    data_protection = (ROOT / "Chummer.Hub.Web/HubDataProtection.cs").read_text(encoding="utf-8")

    assert "chummer-hub-data-protection:/var/lib/chummer-hub/data-protection" in hub
    assert 'CHUMMER_HUB_DATA_PROTECTION_KEYS_PATH: "/var/lib/chummer-hub/data-protection"' in hub
    assert 'CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PATH: "/run/secrets/chummer-config/certificates/chummer-hub-data-protection.p12"' in hub
    assert "CHUMMER_HUB_DATA_PROTECTION_PREVIOUS_CERTIFICATE_PATH" in hub
    assert "CHUMMER_HUB_SECRETS_DIRECTORY" in hub
    assert "target: /run/secrets/chummer-config" in hub
    assert "read_only: true" in hub
    assert "create_host_path: false" in hub
    assert "CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PASSWORD:" not in hub
    assert "CHUMMER_HUB_DATA_PROTECTION_PREVIOUS_CERTIFICATE_PASSWORD:" not in hub
    assert 'install -d -o "$APP_UID" -g "$APP_UID" -m 0700 /var/lib/chummer-hub/data-protection' in dockerfile
    assert "chummer-hub-entrypoint" in dockerfile
    assert "AddKeyPerFile(" in program
    assert 'directoryPath: "/run/secrets/chummer-config"' in program
    assert "HubDataProtection.Configure" in program
    assert "HubDataProtection.VerifyOperational" in program
    assert ".ProtectKeysWithCertificate(certificates.Current)" in data_protection
    assert "UnprotectKeysWithAnyCertificate(certificates.All)" in data_protection
    assert "Pkcs12Info.Decode(" in data_protection
    assert "Pkcs12IntegrityMode.Password" in data_protection
    assert "info.VerifyMac(" in data_protection
    assert "Pkcs12KeyBag" in data_protection
    assert "Pkcs12ShroudedKeyBag" in data_protection
    assert 'string procPath = $"/proc/self/fd/' in (ROOT / "Chummer.Hub.Web/HubPinnedCertificateFile.cs").read_text(encoding="utf-8")
    assert 'element.Name.LocalName == "encryptedSecret"' in data_protection
    assert 'element.Name.LocalName == "masterKey"' in data_protection


def test_hub_runtime_proof_checks_encryption_and_certificate_rotation() -> None:
    proof = (ROOT / "tests/run_hub_container_security_proof.sh").read_text(encoding="utf-8")

    assert "xml.etree.ElementTree" in proof
    assert "encryptedSecret" in proof
    assert "CipherValue" in proof
    assert "masterKey" in proof
    assert "CHUMMER_HUB_DATA_PROTECTION_PREVIOUS_CERTIFICATE_PATH" in proof
    assert "wrong-certificate-password" in proof
    assert "-nomac -keypbe NONE -certpbe NONE" in proof
    assert "unprotected-pkcs12" in proof


def test_playwright_runner_uses_compose_dns_for_private_services() -> None:
    compose = (ROOT / "docker-compose.yml").read_text(encoding="utf-8")
    playwright = _service_block(compose, "chummer-playwright")

    assert "network_mode: host" not in playwright
    assert "http://chummer-blazor:8080" in playwright
    assert 'CHUMMER_API_BASE_URL: "http://chummer-api:8080"' in playwright


def test_blazor_descriptor_proof_inspects_the_dotnet_child() -> None:
    proof = (ROOT / "tests/run_blazor_container_security_proof.sh").read_text(encoding="utf-8")

    assert 'child_pids="$(docker exec' in proof
    assert 'dotnet_pid="$1"' in proof
    assert 'transferred_source_target="$(docker exec' in proof
    assert 'test "$transferred_source_target" != "/var/lib/chummer-build/data-protection"' in proof
    assert "/proc/1/fd/3" not in proof


def test_private_api_has_no_host_port_and_public_ports_are_loopback_only() -> None:
    compose = (ROOT / "docker-compose.yml").read_text(encoding="utf-8")
    api = _service_block(compose, "chummer-api")

    assert "    ports:\n" not in api
    assert '    expose:\n      - "8080"\n' in api

    for service in ("chummer-blazor", "chummer-portal"):
        block = _service_block(compose, service)
        published = [
            line.strip()
            for line in block.splitlines()
            if line.strip().startswith('- "') and ":8080\"" in line
        ]
        assert published, service
        assert all(line.startswith('- "127.0.0.1:') for line in published), service


def test_owner_propagation_key_has_no_published_default() -> None:
    compose = (ROOT / "docker-compose.yml").read_text(encoding="utf-8")

    assert "local-self-hosted-portal-shared-key" not in compose
    assert "CHUMMER_PORTAL_OWNER_SHARED_KEY:" not in compose

    for service in ("chummer-api", "chummer-portal"):
        block = _service_block(compose, service)
        assert "CHUMMER_PORTAL_OWNER_SECRETS_DIRECTORY" in block, service
        assert "target: /run/secrets/chummer-config" in block, service
        assert "read_only: true" in block, service
        assert "create_host_path: false" in block, service


def test_portal_owns_the_fail_closed_hub_web_boundary() -> None:
    portal = (ROOT / "Chummer.Portal/Program.cs").read_text(encoding="utf-8")
    security = (ROOT / "Chummer.Portal/PortalBoundarySecurity.cs").read_text(encoding="utf-8")
    transformer = (ROOT / "Chummer.Portal/PortalProxyTransformer.cs").read_text(encoding="utf-8")
    hub_program = (ROOT / "Chummer.Hub.Web/Program.cs").read_text(encoding="utf-8")
    build_program = (ROOT / "Chummer.Blazor/Program.cs").read_text(encoding="utf-8")
    hub_home = (ROOT / "Chummer.Hub.Web/Components/Pages/Home.razor").read_text(encoding="utf-8")

    assert 'directoryPath: "/run/secrets/chummer-config"' in portal
    assert portal.index("app.UseWebSockets();") < portal.index("app.Use(async (context, next)")
    assert "PortalBoundarySecurity.IsProtectedHubUiPath" in portal
    assert "PortalBoundarySecurity.RequiresSameOriginProtection" in portal
    assert "PortalBoundarySecurity.ShouldRejectBrowserOrigin" in portal
    assert 'MapPassThroughProxy(app, "/hub/{**catchall}", options.HubProxyUrl, hubTransformer)' in portal
    assert 'app.Map("/api/hub/{**catchall}"' in portal
    assert 'app.Map("/api/ai/{**catchall}"' in portal
    assert 'public const string OwnerCookieName = "__Host-chummer_portal_owner"' in security
    assert 'public const string HubAntiforgeryCookieName = "__Host-chummer_hub_antiforgery"' in security
    assert 'proxyRequest.Headers.Remove("Authorization")' in transformer
    assert 'proxyRequest.Headers.Remove("Cookie")' in transformer
    assert "ForwardAllowedCookies" in transformer
    assert 'options.Cookie.Name = "__Host-chummer_hub_antiforgery"' in hub_program
    assert "options.Cookie.SecurePolicy = CookieSecurePolicy.Always" in hub_program
    assert "options.Cookie.SameSite = SameSiteMode.Strict" in hub_program
    assert 'options.Cookie.Name = "__Host-chummer_build_antiforgery"' in build_program
    assert 'MapPassThroughProxy(app, "/blazor/{**catchall}", options.BlazorProxyUrl, blazorTransformer)' in portal
    assert 'href="#moderation"' not in hub_home
    assert "data-hub-approve" not in hub_home
    assert "data-hub-reject" not in hub_home


def test_private_api_maps_the_browser_hub_contract_and_separates_moderation_authority() -> None:
    api = (ROOT / "Chummer.Api/Program.cs").read_text(encoding="utf-8")
    endpoints = (ROOT / "Chummer.Api/Endpoints/HubEndpoints.cs").read_text(encoding="utf-8")
    authorization = (ROOT / "Chummer.Api/Owners/PortalApiBoundaryAuthorization.cs").read_text(encoding="utf-8")
    browser_client = (ROOT / "Chummer.Hub.Web/BrowserHubApiClient.cs").read_text(encoding="utf-8")

    for mapping in (
        "app.MapHubCatalogEndpoints();",
        "app.MapHubPublisherEndpoints();",
        "app.MapHubReviewEndpoints();",
        "app.MapHubPublicationEndpoints();",
    ):
        assert mapping in api

    for route in (
        '"/api/hub/search"',
        '"/api/hub/projects/{kind}/{itemId}"',
        '"/api/hub/projects/{kind}/{itemId}/compatibility"',
        '"/api/hub/projects/{kind}/{itemId}/install-preview"',
        '"/api/hub/publish/drafts"',
        '"/api/hub/publish/drafts/{draftId}"',
        '"/api/hub/publish/drafts/{draftId}/archive"',
        '"/api/hub/publish/{kind}/{itemId}/submit"',
        '"/api/hub/moderation/queue"',
        '"/api/hub/moderation/queue/{caseId}/approve"',
        '"/api/hub/moderation/queue/{caseId}/reject"',
    ):
        assert route in endpoints

    assert 'ModeratorSignatureHeaderName = "X-Chummer-Portal-Moderator-Signature"' in authorization
    assert 'ModeratorSharedKeyConfigurationKey = "CHUMMER_PORTAL_MODERATOR_SHARED_KEY"' in authorization
    assert 'SignedOwnerEnabledConfigurationKey = "CHUMMER_PORTAL_SIGNED_OWNER_ENABLED"' in authorization
    assert "portalSignedOwnerEnabled" in api
    assert 'error = "signed_portal_owner_boundary_disabled"' in api
    assert "ShouldRejectWhenSignedOwnerDisabled" in api
    assert 'path.StartsWithSegments("/api/ai"' in authorization
    assert "TryResolveSignedOwner(context, ownerSharedKey" in authorization
    assert "CreateModeratorSignature(owner.NormalizedValue" in authorization
    assert "new RuleProfileApplyTarget(RuleProfileApplyTargetKinds.GlobalDefaults, \"hub-preview\")" in browser_client


def test_hub_and_portal_container_builds_explicitly_use_the_local_pinned_contract_tree() -> None:
    for dockerfile in (Path("Chummer.Portal/Dockerfile"), Path("Chummer.Hub.Web/Dockerfile")):
        text = (ROOT / dockerfile).read_text(encoding="utf-8")
        assert "ARG CHUMMER_USE_LOCAL_COMPATIBILITY_TREE=true" in text, dockerfile
        assert "ENV ChummerUseLocalCompatibilityTree=$CHUMMER_USE_LOCAL_COMPATIBILITY_TREE" in text, dockerfile
        assert "WORKDIR /src/chummer-presentation" in text, dockerfile


def test_portal_profile_never_starts_a_test_runner() -> None:
    compose = (ROOT / "docker-compose.yml").read_text(encoding="utf-8")
    playwright = _service_block(compose, "chummer-playwright-portal")

    assert 'profiles: ["portal-e2e"]' in playwright
    assert 'profiles: ["portal"]' not in playwright


def test_portal_e2e_profile_includes_every_required_portal_runtime() -> None:
    compose = (ROOT / "docker-compose.yml").read_text(encoding="utf-8")

    for service in (
        "chummer-blazor-portal",
        "chummer-hub-web-portal",
        "chummer-avalonia-browser",
        "chummer-portal",
    ):
        block = _service_block(compose, service)
        assert 'profiles: ["portal", "portal-e2e"]' in block, service


def test_effective_parent_build_context_excludes_common_secret_material() -> None:
    dockerignore = (ROOT.parent / ".dockerignore").read_text(encoding="utf-8")
    required_patterns = {
        "**/.env",
        "**/.env.*",
        "**/.vexp/",
        "**/.aider.tags.cache*",
        "**/.codex-design/",
        "**/.pytest_cache/",
        "**/docs/",
        "**/*.key",
        "**/*.p12",
        "**/*.pem",
        "**/*.pfx",
        "**/credentials*.json",
        "**/secrets*.yaml",
        "**/tests/",
        "**/node_modules/",
        "**/Docker/Secrets/",
        "**/docker-compose*.yml",
    }

    assert required_patterns <= set(dockerignore.splitlines())


def test_build_production_material_is_outside_content_root_and_shared() -> None:
    compose = (ROOT / "docker-compose.yml").read_text(encoding="utf-8")
    dockerfile = (ROOT / "Chummer.Blazor/Dockerfile").read_text(encoding="utf-8")
    program = (ROOT / "Chummer.Blazor/Program.cs").read_text(encoding="utf-8")

    assert "AddKeyPerFile(" in program
    assert 'directoryPath: "/run/secrets/chummer-config"' in program
    assert "chummer-blazor-data-protection:/var/lib/chummer-build/data-protection" in compose
    assert compose.count("chummer-blazor-data-protection:/var/lib/chummer-build/data-protection") == 2
    assert compose.count("CHUMMER_BUILD_SECRETS_DIRECTORY") == 2
    assert compose.count("create_host_path: false") == 5
    assert "/app/state" not in compose.split("CHUMMER_BLAZOR_DATA_PROTECTION_CERTIFICATE_PATH", 1)[1].splitlines()[0]
    assert "/var/lib/chummer-build/data-protection" in dockerfile
    assert "chummer-blazor-entrypoint" in dockerfile

    for service in ("chummer-blazor", "chummer-blazor-portal"):
        block = _service_block(compose, service)
        assert "    read_only: true\n" in block, service
        assert "    tmpfs:\n      - /tmp:rw,nosuid,nodev,noexec,size=64m,mode=1777\n" in block, service


def test_build_compose_declares_its_single_instance_file_store_without_database_secrets() -> None:
    compose = (ROOT / "docker-compose.yml").read_text(encoding="utf-8")
    program = (ROOT / "Chummer.Blazor/Program.cs").read_text(encoding="utf-8")

    assert "builder.Configuration.AddKeyPerFile(" in program
    assert 'directoryPath: "/run/secrets/chummer-config"' in program

    for service in ("chummer-blazor", "chummer-blazor-portal"):
        block = _service_block(compose, service)
        environment = block.split("    environment:\n", 1)[1].split("\n    volumes:\n", 1)[0]

        assert '      CHUMMER_BUILD_WORKSPACE_STORE_PROVIDER: "file"\n' in environment, service
        assert '      CHUMMER_BUILD_EXPECTED_REPLICA_COUNT: "1"\n' in environment, service
        assert "CHUMMER_BUILD_POSTGRES_CONNECTION_STRING" not in environment, service
        assert 'source: "${CHUMMER_BUILD_SECRETS_DIRECTORY:-./Docker/Secrets/build}"' in block, service
        assert "target: /run/secrets/chummer-config" in block, service
        assert "read_only: true" in block, service
        assert "create_host_path: false" in block, service


def test_build_health_and_durability_docs_identify_file_as_single_instance() -> None:
    program = (ROOT / "Chummer.Blazor/Program.cs").read_text(encoding="utf-8")
    selection = (
        ROOT / "Chummer.Blazor/Services/HostedBuildWorkspaceStoreConfiguration.cs"
    ).read_text(encoding="utf-8")
    runbook = (ROOT / "docs/HOSTED_BUILD_POSTGRES_DURABILITY.md").read_text(encoding="utf-8")

    assert "HostedBuildWorkspaceStoreSelection workspaceStore" in program
    assert program.count("workspaceStore,") >= 2
    assert "Provider: FileProvider" in selection
    assert "MultiInstanceSafe: false" in selection
    assert 'DurabilityBoundary: "single_instance_local_filesystem"' in selection
    assert 'DurabilityBoundary: "shared_transactional_postgresql"' in selection
    assert "Production must load" in selection
    assert "from the mounted KeyPerFile secret directory" in selection
    assert "`provider=file` is a single-instance development or recovery posture only." in runbook
    assert "must run exactly\n  one Build application instance" in runbook
    assert "Database passwords, client keys, and complete connection strings are supplied\n  through read-only secret files." in runbook


def test_production_owner_propagation_fails_closed_and_has_no_implicit_default() -> None:
    compose = (ROOT / "docker-compose.yml").read_text(encoding="utf-8")
    api = (ROOT / "Chummer.Api/Program.cs").read_text(encoding="utf-8")
    portal = (ROOT / "Chummer.Portal/Program.cs").read_text(encoding="utf-8")
    portal_security = (ROOT / "Chummer.Portal/PortalBoundarySecurity.cs").read_text(encoding="utf-8")

    assert 'CHUMMER_PORTAL_IMPLICIT_OWNER: "${CHUMMER_PORTAL_IMPLICIT_OWNER:-}"' in compose
    assert "local@self-host" not in compose
    assert "ValidatePortalOwnerSharedKey" in api
    assert "Encoding.UTF8.GetByteCount(normalized) < 32" in api
    assert "local-self-hosted-portal-shared-key" in api
    assert "PortalBoundarySecurity.ValidateProductionConfiguration" in portal
    assert "Encoding.UTF8.GetByteCount(normalizedKey) < 32" in portal_security
    assert "local-self-hosted-portal-shared-key" in portal_security


def _fake_dotnet(tmp_path: Path) -> Path:
    executable = tmp_path / "dotnet"
    executable.write_text(
        "#!/bin/sh\n"
        "fd=\"${CHUMMER_BLAZOR_DATA_PROTECTION_KEYS_DIRECTORY_FD:-unset}\"\n"
        "printf 'fd=%s\\n' \"$fd\"\n"
        "if [ \"$fd\" != unset ]; then readlink \"/proc/self/fd/$fd\"; fi\n",
        encoding="utf-8",
    )
    executable.chmod(0o755)
    return executable


def test_build_launcher_passes_a_real_directory_descriptor_across_exec(tmp_path: Path) -> None:
    repository = tmp_path / "repository"
    repository.mkdir(mode=0o700)
    fake_bin = tmp_path / "bin"
    fake_bin.mkdir()
    _fake_dotnet(fake_bin)
    environment = os.environ.copy()
    environment.update(
        {
            "ASPNETCORE_ENVIRONMENT": "Production",
            "CHUMMER_BLAZOR_DATA_PROTECTION_KEYS_DIRECTORY": str(repository),
            "PATH": f"{fake_bin}:{environment['PATH']}",
        }
    )

    completed = subprocess.run(
        ["/bin/sh", str(ROOT / "Chummer.Blazor/docker-entrypoint.sh")],
        check=False,
        capture_output=True,
        text=True,
        env=environment,
    )

    assert completed.returncode == 0, completed.stderr
    assert completed.stdout.splitlines() == ["fd=3", str(repository)]


def test_build_launcher_fails_closed_without_production_repository(tmp_path: Path) -> None:
    missing = tmp_path / "private-material-must-not-leak"
    environment = os.environ.copy()
    environment.update(
        {
            "ASPNETCORE_ENVIRONMENT": "Production",
            "CHUMMER_BLAZOR_DATA_PROTECTION_KEYS_DIRECTORY": str(missing),
        }
    )

    completed = subprocess.run(
        ["/bin/sh", str(ROOT / "Chummer.Blazor/docker-entrypoint.sh")],
        check=False,
        capture_output=True,
        text=True,
        env=environment,
    )

    assert completed.returncode == 78
    assert "repository is unavailable" in completed.stderr
    assert str(missing) not in completed.stderr


def test_build_launcher_does_not_invent_production_authority_in_development(tmp_path: Path) -> None:
    fake_bin = tmp_path / "bin"
    fake_bin.mkdir()
    _fake_dotnet(fake_bin)
    environment = os.environ.copy()
    environment.update(
        {
            "ASPNETCORE_ENVIRONMENT": "Development",
            "PATH": f"{fake_bin}:{environment['PATH']}",
        }
    )
    environment.pop("CHUMMER_BLAZOR_DATA_PROTECTION_KEYS_DIRECTORY_FD", None)
    environment.pop("CHUMMER_BLAZOR_DATA_PROTECTION_KEYS_DIRECTORY", None)

    completed = subprocess.run(
        ["/bin/sh", str(ROOT / "Chummer.Blazor/docker-entrypoint.sh")],
        check=False,
        capture_output=True,
        text=True,
        env=environment,
    )

    assert completed.returncode == 0, completed.stderr
    assert completed.stdout.splitlines() == ["fd=unset"]


def test_nonroot_volume_migration_runbook_is_explicit_and_bounded() -> None:
    runbook = (ROOT / "docs/CONTAINER_RUNTIME_HARDENING.md").read_text(encoding="utf-8")

    assert "UID/GID `1654:1654`" in runbook
    assert "back up" in runbook.lower()
    assert "write freeze" in runbook.lower()
    assert "chummer-state" in runbook
    assert "chummer-blazor-state" in runbook
    assert "chummer-blazor-portal-state" in runbook
    assert "--user 0" in runbook
    assert "--entrypoint /usr/local/bin/chummer-state-ownership-migration" in runbook
    assert "chown -R" not in runbook
    assert "content SHA-256" in runbook
    assert "Rollback" in runbook
