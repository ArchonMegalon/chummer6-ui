FROM mcr.microsoft.com/dotnet/sdk:10.0.103 AS build
WORKDIR /src

COPY . .

WORKDIR /src/chummer-presentation
RUN --mount=type=cache,id=chummer-nuget-packages,target=/root/.nuget/packages,sharing=locked \
    --mount=type=cache,id=chummer-nuget-http,target=/root/.local/share/NuGet/http-cache,sharing=locked \
    --mount=type=cache,id=chummer-nuget-plugins,target=/root/.local/share/NuGet/plugin-cache,sharing=locked \
    dotnet restore Chummer.Blazor/Chummer.Blazor.csproj \
      -p:RestorePackagesPath=/root/.nuget/packages \
      -p:ChummerUseLocalCompatibilityTree=true
RUN --mount=type=cache,id=chummer-nuget-packages,target=/root/.nuget/packages,sharing=locked \
    dotnet publish Chummer.Blazor/Chummer.Blazor.csproj \
      -c Release \
      -o /app/publish \
      --no-restore \
      -p:RestorePackagesPath=/root/.nuget/packages \
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
