FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Electronic Election Management System/Electronic Election Management System.csproj", "Electronic Election Management System/"]
RUN dotnet restore "Electronic Election Management System/Electronic Election Management System.csproj"
COPY . .
WORKDIR "/src/Electronic Election Management System"
RUN dotnet build "./Electronic Election Management System.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Electronic Election Management System.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish --chown=$APP_UID:$APP_UID /app/publish .
ENTRYPOINT ["dotnet", "Electronic Election Management System.dll"]
