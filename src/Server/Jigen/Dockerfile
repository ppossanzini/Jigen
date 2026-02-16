FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Server/Jigen/Jigen.csproj", "src/Server/Jigen/"]
COPY ["src/Tools/SharedTools/SharedTools.csproj", "src/Tools/SharedTools/"]
COPY ["src/Server/Jigen.API/Jigen.API.csproj", "src/Server/Jigen.API/"]
COPY ["src/Jigen/Jigen.SemanticTools/Jigen.SemanticTools.csproj", "src/Jigen/Jigen.SemanticTools/"]
COPY ["src/Server/Jigen.Core/Jigen.Core.csproj", "src/Server/Jigen.Core/"]
COPY ["src/Jigen/Jigen.Store/Jigen.Store.csproj", "src/Jigen/Jigen.Store/"]
COPY ["src/Server/Jigen.Handlers/Jigen.Handlers.csproj", "src/Server/Jigen.Handlers/"]
RUN dotnet restore "src/Server/Jigen/Jigen.csproj"
COPY . .
WORKDIR "/src/src/Server/Jigen"
RUN dotnet build "./Jigen.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Jigen.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Jigen.dll"]
