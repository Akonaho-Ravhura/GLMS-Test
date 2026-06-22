# ── Stage 1: Build ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["GLMS-CORE-APP/GLMS-CORE-APP.csproj",               "GLMS-CORE-APP/"]
COPY ["GLMS-CORE-APP.Shared/GLMS-CORE-APP.Shared.csproj", "GLMS-CORE-APP.Shared/"]

RUN dotnet restore "GLMS-CORE-APP/GLMS-CORE-APP.csproj"

COPY GLMS-CORE-APP/        GLMS-CORE-APP/
COPY GLMS-CORE-APP.Shared/ GLMS-CORE-APP.Shared/

WORKDIR "/src/GLMS-CORE-APP"
RUN dotnet publish "GLMS-CORE-APP.csproj" -c Release -o /app/publish

# ── Stage 2: Runtime ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
# ── Build & Test ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS test
WORKDIR /src

# Copy project files for restore
COPY ["GLMS-CORE-APP/GLMS-CORE-APP.csproj",               "GLMS-CORE-APP/"]
COPY ["GLMS-CORE-APP.API/GLMS-CORE-APP.API.csproj",        "GLMS-CORE-APP.API/"]
COPY ["GLMS-CORE-APP.Shared/GLMS-CORE-APP.Shared.csproj",  "GLMS-CORE-APP.Shared/"]
COPY ["GLMS-CORE-APP.Tests/GLMS-CORE-APP.Tests.csproj",    "GLMS-CORE-APP.Tests/"]

RUN dotnet restore "GLMS-CORE-APP.Tests/GLMS-CORE-APP.Tests.csproj"

# Copy all source
COPY GLMS-CORE-APP/        GLMS-CORE-APP/
COPY GLMS-CORE-APP.API/    GLMS-CORE-APP.API/
COPY GLMS-CORE-APP.Shared/ GLMS-CORE-APP.Shared/
COPY GLMS-CORE-APP.Tests/  GLMS-CORE-APP.Tests/

# Run tests — fails the build if any test fails
WORKDIR "/src/GLMS-CORE-APP.Tests"
RUN dotnet test "GLMS-CORE-APP.Tests.csproj" \
    --no-restore \
    --logger "console;verbosity=normal"

# Final stage — just confirms tests passed
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS final
RUN echo "All tests passed"
RUN mkdir -p /app/wwwroot/uploads/contracts

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "GLMS-CORE-APP.dll"]