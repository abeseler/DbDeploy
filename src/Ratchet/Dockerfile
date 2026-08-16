# Cross-compile on the builder's arch (amd64 default). Buildx sets TARGETARCH per platform.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH=amd64
WORKDIR /src
COPY ["src/Ratchet/Ratchet.csproj", "Ratchet/"]
RUN dotnet restore "./Ratchet/Ratchet.csproj" -a $TARGETARCH
COPY ./src .
WORKDIR "/src/Ratchet"
RUN dotnet publish "./Ratchet.csproj" --no-restore -c Release -a $TARGETARCH -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS final
USER app
WORKDIR /app
RUN mkdir -p /app/Migrations
COPY --from=build /app/publish .
ENTRYPOINT ["./Ratchet"]