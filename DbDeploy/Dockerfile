FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DbDeploy/DbDeploy.csproj", "DbDeploy/"]
RUN dotnet restore "./DbDeploy/./DbDeploy.csproj"
COPY . .
WORKDIR "/src/DbDeploy"
RUN dotnet build "./DbDeploy.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DbDeploy.csproj" -c $BUILD_CONFIGURATION -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DbDeploy.dll"]