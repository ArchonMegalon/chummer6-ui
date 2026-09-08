# Real-Core Creation projection check

This executable uses the Presentation project's locked Core packages, a new
temporary workspace store, and one explicitly supplied Core content checkout.
It does not link Core source projects or fabricate Core service responses.

The same scenario is linked into the mandatory `Chummer.Product.UnitTests`
assembly, as one integration test with 39 assertions. The fresh-checkout package
plane passes the content root explicitly, checks all `Chummer/data`,
`Chummer/lang` and `Chummer/customdata` bytes and directory membership against
the Core semantic commit, and checks again before accepting the test result.
The data root is outside the consumer's sibling directory. It is never a Core
source project reference, and missing input is a failure rather than a skip.

For a local diagnostic, build with the installed pinned .NET 10 SDK and the
same isolated package cache used to restore Presentation, then run the binary:

```sh
dotnet build /absolute/ui/Chummer.CreationWizard.CoreProjection.Tests/Chummer.CreationWizard.CoreProjection.Tests.csproj \
  --no-restore -m:1 -p:RestorePackagesPath=/absolute/isolated/nuget/packages
dotnet /absolute/ui/Chummer.CreationWizard.CoreProjection.Tests/bin/Debug/net10.0/Chummer.CreationWizard.CoreProjection.Tests.dll \
  /absolute/pinned/core-content
```

The content argument must be absolute and contain `Chummer/data/settings.xml`.
The caller must verify the checkout against the approved Core content identity;
accepting the argument is not proof of its provenance. Missing inputs fail,
never skip. Only the newly allocated test workspace directory is removed.

To execute the full Product suite, pass
`--test-parameter ChummerCoreContentRoot=/absolute/pinned/core-content` to its
already-built test binary. `scripts/ai/verify.sh` in integration/release mode
requires `CHUMMER_CORE_PROJECTION_CONTENT_ROOT` and validates that input before
and after the tests. The checkout must already contain the pinned recipe and
semantic Git objects. `scripts/ai/verify_creation_projection_content.py
--core-root /absolute/pinned/core-content` performs just that read-only input
check; it does not build packages or grant release authority.

The fixture starts with a real Bootstrap activation bundle and confirms a Human/Mundane Priority
selection, base attributes, a native language, empty qualities, resource
allocation, and empty gear through the actual Core preview/confirm APIs. It
then checks that Core finalization readiness reaches the wizard's Review step
without inventing contact or lifestyle budgets. Stale, rehashed, malformed or
missing authority must not permit completion or mutate the workspace.
The asynchronous lifecycle is also exercised with these real Core services and
an already captured Core overview. Background preparation must preserve the
exact wizard digest and document identity; the compatibility overview loader
does not grant canonical recovery authority.

Fresh activation is tested separately from loading an existing draft. The real
Core validator runs outside a caller-supplied UI synchronization context.
Cancellation before entry must not consume the one-shot activation bundle;
cancellation immediately after real Core validation must stop before further
domain reads. Retrying that consumed bundle uses the existing character's
captured persisted overview and does not create a duplicate. An already
committed character is not rolled back by canceling its UI activation.

The observations around Core count calls and capture synchronization context;
they delegate to actual Core validation/finalization rather than returning
synthetic rule results. These checks do not establish concurrent store-drift
rejection between creation and activation, or canonical recovery authority for
the compatibility loader.

The CI wiring is source code, not proof that this unsealed change has run in
hosted CI. It needs the normal reviewed package preseal/seal and exact hosted
consumer run. This scenario is not a complete Priority choice matrix, Android
tap-path, process-restart, performance, package-publication or Google Play receipt.
