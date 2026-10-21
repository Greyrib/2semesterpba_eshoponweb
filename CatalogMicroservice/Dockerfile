## See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.
#
## This stage is used when running from VS in fast mode (Default for Debug configuration)
#FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
#USER $APP_UID
#WORKDIR /app
#EXPOSE 8080
#EXPOSE 8081
#
#
## This stage is used to build the service project
#FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
#ARG BUILD_CONFIGURATION=Release
#WORKDIR /src
#COPY ["Directory.Packages.props", "."]
#COPY ["CatalogMicroservice/CatalogMicroservice.csproj", "CatalogMicroservice/"]
#RUN dotnet restore "./CatalogMicroservice/CatalogMicroservice.csproj"
#COPY . .
#WORKDIR "/src/CatalogMicroservice"
#RUN dotnet build "./CatalogMicroservice.csproj" -c $BUILD_CONFIGURATION -o /app/build
#
## This stage is used to publish the service project to be copied to the final stage
#FROM build AS publish
#ARG BUILD_CONFIGURATION=Release
#RUN dotnet publish "./CatalogMicroservice.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false
#
## This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
#FROM base AS final
#WORKDIR /app
#COPY --from=publish /app/publish .
#ENTRYPOINT ["dotnet", "CatalogMicroservice.dll"]





#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY . .
#COPY ["src/PublicApi/PublicApi.csproj", "./PublicApi/"]
#RUN dotnet restore "./PublicApi/PublicApi.csproj"
#COPY . .
#WORKDIR "/app/src/PublicApi"
RUN dotnet restore

RUN dotnet build "./CatalogMicroservice.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./CatalogMicroservice.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CatalogMicroservice.dll"]