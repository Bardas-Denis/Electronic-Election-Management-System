FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN mkdir -p /app/data /app/plugins && chown -R $APP_UID:$APP_UID /app
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Every project the API references has to be present before restore, or its ProjectReferences
# cannot be resolved. Copying just the project files first keeps this layer cached across
# source changes.
COPY ["Electronic Election Management System/Electronic Election Management System.csproj", "Electronic Election Management System/"]
COPY ["Electronic Election Management System.Data/Electronic Election Management System.Data.csproj", "Electronic Election Management System.Data/"]
COPY ["Electronic Election Management System.PluginContracts/Electronic Election Management System.PluginContracts.csproj", "Electronic Election Management System.PluginContracts/"]
COPY ["Plugins/Eems.Providers.Sqlite/Eems.Providers.Sqlite.csproj", "Plugins/Eems.Providers.Sqlite/"]
COPY ["Plugins/Eems.Providers.Postgres/Eems.Providers.Postgres.csproj", "Plugins/Eems.Providers.Postgres/"]
COPY ["Plugins/Eems.Plugins.Exponential/Eems.Plugins.Exponential.csproj", "Plugins/Eems.Plugins.Exponential/"]
RUN dotnet restore "Electronic Election Management System/Electronic Election Management System.csproj"

COPY . .
WORKDIR "/src/Electronic Election Management System"
RUN dotnet build "./Electronic Election Management System.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Electronic Election Management System.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Plugins are loose assemblies discovered at runtime, so they are published into the folder the
# loader scans rather than into the application's own output. Without the two providers the
# container has no way to reach a database and refuses to start.
WORKDIR /src
RUN for p in Plugins/Eems.Providers.Sqlite Plugins/Eems.Providers.Postgres Plugins/Eems.Plugins.Exponential; do \
        dotnet publish "$p" -c $BUILD_CONFIGURATION -o /app/publish/plugins /p:UseAppHost=false; \
    done

FROM base AS final
WORKDIR /app
COPY --from=publish --chown=$APP_UID:$APP_UID /app/publish .
ENTRYPOINT ["dotnet", "Electronic Election Management System.dll"]
