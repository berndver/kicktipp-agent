FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/KicktippAgent.Worker/KicktippAgent.Worker.csproj", "src/KicktippAgent.Worker/"]
RUN dotnet restore "src/KicktippAgent.Worker/KicktippAgent.Worker.csproj"
COPY . .
WORKDIR "/src/src/KicktippAgent.Worker"
RUN dotnet build "./KicktippAgent.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./KicktippAgent.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KicktippAgent.Worker.dll"]
