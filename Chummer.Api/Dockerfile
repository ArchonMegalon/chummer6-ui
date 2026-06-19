FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

WORKDIR /src/chummer-presentation
RUN dotnet restore Chummer.Api/Chummer.Api.csproj
RUN dotnet publish Chummer.Api/Chummer.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/chummer-presentation/Chummer/data ./data
COPY --from=build /src/chummer-presentation/Chummer/lang ./lang
EXPOSE 8080
ENTRYPOINT ["dotnet", "Chummer.Api.dll"]
