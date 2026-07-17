# Hosted Build PostgreSQL integration contract

This project runs destructive integration tests only against a disposable
database that it creates and drops. It is intentionally separate from the
fast unit-test assembly.

Set `CHUMMER_BUILD_POSTGRES_TEST_CONNECTION_STRING` to an administrator
connection for a dedicated PostgreSQL test cluster. The identity must have
`CREATEDB` and `CREATEROLE`; each test creates a uniquely named
`chummer_build_it_*` database, and the least-privilege test creates a uniquely
named `chummer_build_it_role_*` login. Do not point this variable at a
production cluster.

When the variable is absent, database-backed tests report **Inconclusive** and
make no external changes. The secret-safety outage test still runs because it
uses an intentionally unreachable loopback endpoint.

Run the contract directly:

```bash
dotnet test --project Chummer.Workspaces.Postgres.IntegrationTests/Chummer.Workspaces.Postgres.IntegrationTests.csproj -c Release
```

Or let the repository create and remove a disposable PostgreSQL 17 container:

```bash
bash scripts/test-hosted-build-postgres.sh
```

The suite covers migration bootstrap, repeatability and concurrency; ledger,
checksum and physical-object drift; owner isolation; cross-instance CAS and
conditional-create races; provider restart persistence; readiness cleanup;
full-document tamper detection; least-privilege runtime behavior; and
secret-free outage diagnostics.
