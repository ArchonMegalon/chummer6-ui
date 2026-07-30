FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

WORKDIR /src/chummer-presentation
RUN --mount=type=cache,id=chummer-nuget-packages,target=/root/.nuget/packages,sharing=locked \
    --mount=type=cache,id=chummer-nuget-http,target=/root/.local/share/NuGet/http-cache,sharing=locked \
    --mount=type=cache,id=chummer-nuget-plugins,target=/root/.local/share/NuGet/plugin-cache,sharing=locked \
    dotnet restore Chummer.Api/Chummer.Api.csproj -p:RestorePackagesPath=/root/.nuget/packages
RUN --mount=type=cache,id=chummer-nuget-packages,target=/root/.nuget/packages,sharing=locked \
    dotnet publish Chummer.Api/Chummer.Api.csproj -c Release -o /app/publish --no-restore -p:RestorePackagesPath=/root/.nuget/packages

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
COPY --from=build /src/chummer-presentation/Chummer/data ./data
COPY --from=build /src/chummer-presentation/Chummer/lang ./lang
COPY --from=build --chmod=0555 /src/chummer-presentation/Docker/migrate-state-volume-ownership.sh /usr/local/bin/chummer-state-ownership-migration
COPY --from=build --chmod=0555 /src/chummer-presentation/Docker/normalize-state-volume-modes.sh /usr/local/bin/chummer-state-mode-normalization
RUN chmod -R u=rwX,go=rX /app \
 && install -d -o "$APP_UID" -g "$APP_UID" -m 0700 /app/state
EXPOSE 8080
USER $APP_UID:$APP_UID
ENTRYPOINT ["dotnet", "Chummer.Api.dll"]
