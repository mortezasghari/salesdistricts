FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SalesDistricts.Api/SalesDistricts.Api.csproj", "SalesDistricts.Api/"]
RUN dotnet restore "SalesDistricts.Api/SalesDistricts.Api.csproj"
COPY . .
WORKDIR "/src/SalesDistricts.Api"
RUN dotnet build "./SalesDistricts.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SalesDistricts.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SalesDistricts.Api.dll"]
