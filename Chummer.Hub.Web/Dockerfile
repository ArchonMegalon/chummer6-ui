FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

WORKDIR /src/chummer-presentation
RUN --mount=type=cache,id=chummer-nuget-packages,target=/root/.nuget/packages,sharing=locked \
    --mount=type=cache,id=chummer-nuget-http,target=/root/.local/share/NuGet/http-cache,sharing=locked \
    --mount=type=cache,id=chummer-nuget-plugins,target=/root/.local/share/NuGet/plugin-cache,sharing=locked \
    dotnet restore Chummer.Hub.Web/Chummer.Hub.Web.csproj -p:RestorePackagesPath=/root/.nuget/packages
RUN --mount=type=cache,id=chummer-nuget-packages,target=/root/.nuget/packages,sharing=locked \
    dotnet publish Chummer.Hub.Web/Chummer.Hub.Web.csproj -c Release -o /app/publish --no-restore -p:RestorePackagesPath=/root/.nuget/packages

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
COPY chummer-presentation/Chummer.Hub.Web/docker-entrypoint.sh /usr/local/bin/chummer-hub-entrypoint
RUN chmod -R u=rwX,go=rX /app \
 && chmod 0555 /usr/local/bin/chummer-hub-entrypoint \
 && install -d -o "$APP_UID" -g "$APP_UID" -m 0700 /var/lib/chummer-hub/data-protection \
 && install -d -o "$APP_UID" -g "$APP_UID" -m 0700 /run/secrets/chummer-config
EXPOSE 8080
USER $APP_UID:$APP_UID
ENTRYPOINT ["/usr/local/bin/chummer-hub-entrypoint"]
