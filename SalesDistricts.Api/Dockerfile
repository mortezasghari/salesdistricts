FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SalesDistricts.Api/SalesDistricts.Api.csproj", "SalesDistricts.Api/"]
COPY ["SalesDistricts.Data/SalesDistricts.Data.csproj", "SalesDistricts.Data/"]
RUN dotnet restore "SalesDistricts.Api/SalesDistricts.Api.csproj"
COPY . .
WORKDIR "/src/SalesDistricts.Api"
RUN dotnet publish "./SalesDistricts.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SalesDistricts.Api.dll"]
