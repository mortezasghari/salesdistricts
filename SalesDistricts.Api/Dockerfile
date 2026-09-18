# syntax=docker/dockerfile:1

# Native AOT build. The SDK image needs a C toolchain (clang + zlib headers)
# for the ILCompiler link step. TARGETARCH is supplied by BuildKit (amd64/arm64).
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
ARG BUILD_CONFIGURATION=Release
RUN apt-get update \
    && apt-get install -y --no-install-recommends clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src

# Restore first so the NuGet layer is cached across source changes.
COPY ["SalesDistricts.Api/SalesDistricts.Api.csproj", "SalesDistricts.Api/"]
RUN dotnet restore "SalesDistricts.Api/SalesDistricts.Api.csproj" -a $TARGETARCH

COPY . .
WORKDIR "/src/SalesDistricts.Api"
RUN dotnet publish "./SalesDistricts.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -a $TARGETARCH \
    --no-restore \
    -o /app/publish

# Runtime: distroless, no .NET runtime, no shell, non-root. runtime-deps carries
# only the native libs an AOT binary needs (app uses invariant globalization, so no ICU).
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS final
WORKDIR /app
EXPOSE 8080
# Leave the separated debug symbols (*.dbg) out of the runtime image.
COPY --from=build --exclude=*.dbg /app/publish .
USER $APP_UID
ENTRYPOINT ["./SalesDistricts.Api"]
