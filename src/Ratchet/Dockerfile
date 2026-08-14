FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Ratchet/Ratchet.csproj", "Ratchet/"]
RUN dotnet restore "./Ratchet/Ratchet.csproj"
COPY ./src .
WORKDIR "/src/Ratchet"
RUN dotnet publish "./Ratchet.csproj" --no-restore -c Release -o /app/build

FROM base AS final
WORKDIR /app
RUN mkdir -p /app/Migrations
COPY --from=build /app/build .
ENTRYPOINT ["dotnet", "Ratchet.dll"]