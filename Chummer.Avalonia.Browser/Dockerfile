FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Chummer.Avalonia.Browser/Chummer.Avalonia.Browser.csproj Chummer.Avalonia.Browser/
RUN dotnet restore Chummer.Avalonia.Browser/Chummer.Avalonia.Browser.csproj

COPY Chummer.Avalonia.Browser/ Chummer.Avalonia.Browser/
RUN dotnet publish Chummer.Avalonia.Browser/Chummer.Avalonia.Browser.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out/ .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Chummer.Avalonia.Browser.dll"]
