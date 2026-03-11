FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the UI project and shared repo build props needed for package restore.
COPY chummer-presentation/global.json chummer-presentation/
COPY chummer-presentation/Directory.Build.props chummer-presentation/
COPY chummer-presentation/Directory.Build.targets chummer-presentation/
COPY chummer-presentation/Chummer.Blazor/Chummer.Blazor.csproj chummer-presentation/Chummer.Blazor/

# Restore dependencies
RUN dotnet restore chummer-presentation/Chummer.Blazor/Chummer.Blazor.csproj

# Copy the actual code
COPY chummer-presentation/Chummer.Blazor/ chummer-presentation/Chummer.Blazor/

# Publish
WORKDIR /src/chummer-presentation/Chummer.Blazor
RUN dotnet publish -c Release -o /app/publish

# Build the final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Chummer.Blazor.dll"]
