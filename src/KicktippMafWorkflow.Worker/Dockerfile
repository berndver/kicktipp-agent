FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
RUN apt-get update && apt-get install -y chromium && rm -rf /var/lib/apt/lists/*
ENV CHROME_BIN=/usr/bin/chromium
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/KicktippMafWorkflow.Worker/KicktippMafWorkflow.Worker.csproj", "src/KicktippMafWorkflow.Worker/"]
RUN dotnet restore "src/KicktippMafWorkflow.Worker/KicktippMafWorkflow.Worker.csproj"
COPY . .
WORKDIR "/src/src/KicktippMafWorkflow.Worker"
RUN dotnet build "./KicktippMafWorkflow.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./KicktippMafWorkflow.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KicktippMafWorkflow.Worker.dll"]
