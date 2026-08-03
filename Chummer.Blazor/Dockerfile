FROM mcr.microsoft.com/dotnet/sdk:10.0.103 AS build
WORKDIR /src

COPY . .

RUN dotnet pack /src/chummer-hub-registry/Chummer.Hub.Registry.Contracts/Chummer.Hub.Registry.Contracts.csproj \
      -c Release \
      -o /chummer-owner-feed \
      --nologo \
      -p:PackageVersion=0.1.0-preview \
      -p:Version=0.1.0-preview \
      -p:RestorePackagesPath=/tmp/chummer-nuget-packages
RUN dotnet pack /src/chummer.run-services/Chummer.Play.Contracts/Chummer.Play.Contracts.csproj \
      -c Release \
      -o /chummer-owner-feed \
      --nologo \
      -p:PackageVersion=0.1.0-preview \
      -p:Version=0.1.0-preview \
      -p:RestorePackagesPath=/tmp/chummer-nuget-packages
RUN dotnet pack /src/chummer.run-services/Chummer.Run.Contracts/Chummer.Run.Contracts.csproj \
      -c Release \
      -o /chummer-owner-feed \
      --nologo \
      -p:PackageVersion=0.1.0-preview \
      -p:Version=0.1.0-preview \
      -p:RestoreAdditionalProjectSources=/chummer-owner-feed \
      -p:RestorePackagesPath=/tmp/chummer-nuget-packages \
      -p:ChummerUseLocalCompatibilityTree=true

WORKDIR /src/chummer-presentation
RUN dotnet restore Chummer.Blazor/Chummer.Blazor.csproj \
      -p:RestorePackagesPath=/tmp/chummer-nuget-packages \
      -p:RestoreAdditionalProjectSources=/chummer-owner-feed \
      -p:ChummerUseLocalCompatibilityTree=true
RUN dotnet publish Chummer.Blazor/Chummer.Blazor.csproj \
      -c Release \
      -o /app/publish \
      --no-restore \
      -p:RestorePackagesPath=/tmp/chummer-nuget-packages \
      -p:RestoreAdditionalProjectSources=/chummer-owner-feed \
      -p:ChummerUseLocalCompatibilityTree=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
COPY --from=build --chmod=0555 /src/chummer-presentation/Chummer.Blazor/docker-entrypoint.sh /usr/local/bin/chummer-blazor-entrypoint
COPY --from=build --chmod=0555 /src/chummer-presentation/Docker/migrate-state-volume-ownership.sh /usr/local/bin/chummer-state-ownership-migration
RUN chmod -R u=rwX,go=rX /app \
 && install -d -o "$APP_UID" -g "$APP_UID" -m 0700 /app/state \
 && install -d -o "$APP_UID" -g "$APP_UID" -m 0700 /var/lib/chummer-build/data-protection \
 && install -d -o "$APP_UID" -g "$APP_UID" -m 0700 /run/secrets/chummer-config
EXPOSE 8080
USER $APP_UID:$APP_UID
ENTRYPOINT ["/usr/local/bin/chummer-blazor-entrypoint"]
