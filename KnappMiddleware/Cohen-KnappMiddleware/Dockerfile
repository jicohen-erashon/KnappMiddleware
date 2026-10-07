# syntax=docker/dockerfile:1

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY KnappMiddleware.csproj ./
RUN dotnet restore "KnappMiddleware.csproj"

COPY . .
RUN dotnet publish "KnappMiddleware.csproj" -c Release -o /app/publish --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Usuario sin privilegios; /app/logs es el punto de montaje del volumen de logs (FileLogging:BasePath).
RUN useradd --user-group --no-create-home appuser \
    && mkdir -p /app/logs \
    && chown -R appuser:appuser /app
USER appuser

COPY --from=build --chown=appuser:appuser /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "KnappMiddleware.dll"]
