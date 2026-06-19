FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

WORKDIR /src/chummer-presentation
RUN dotnet restore Chummer.Blazor/Chummer.Blazor.csproj
RUN dotnet publish Chummer.Blazor/Chummer.Blazor.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Chummer.Blazor.dll"]
