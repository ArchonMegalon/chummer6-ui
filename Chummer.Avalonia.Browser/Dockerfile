FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Chummer.Avalonia.Browser/Chummer.Avalonia.Browser.csproj Chummer.Avalonia.Browser/
RUN --mount=type=cache,id=chummer-nuget-packages,target=/root/.nuget/packages,sharing=locked \
    --mount=type=cache,id=chummer-nuget-http,target=/root/.local/share/NuGet/http-cache,sharing=locked \
    --mount=type=cache,id=chummer-nuget-plugins,target=/root/.local/share/NuGet/plugin-cache,sharing=locked \
    dotnet restore Chummer.Avalonia.Browser/Chummer.Avalonia.Browser.csproj -p:RestorePackagesPath=/root/.nuget/packages

COPY Chummer.Avalonia.Browser/ Chummer.Avalonia.Browser/
RUN --mount=type=cache,id=chummer-nuget-packages,target=/root/.nuget/packages,sharing=locked \
    dotnet publish Chummer.Avalonia.Browser/Chummer.Avalonia.Browser.csproj -c Release -o /out --no-restore -p:RestorePackagesPath=/root/.nuget/packages

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /out/ .
RUN chmod -R u=rwX,go=rX /app
EXPOSE 8080
USER $APP_UID:$APP_UID
ENTRYPOINT ["dotnet", "Chummer.Avalonia.Browser.dll"]
