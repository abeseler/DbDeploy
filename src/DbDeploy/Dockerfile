FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/DbDeploy/DbDeploy.csproj", "DbDeploy/"]
RUN dotnet restore "./DbDeploy/DbDeploy.csproj"
COPY ./src .
WORKDIR "/src/DbDeploy"
RUN dotnet publish "./DbDeploy.csproj" --no-restore -c Release -o /app/build

FROM base AS final
WORKDIR /app
RUN mkdir -p /app/Migrations
COPY --from=build /app/build .
ENTRYPOINT ["dotnet", "DbDeploy.dll"]