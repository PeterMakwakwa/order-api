#=========================================================================
# Build:  docker build -f build/docker/Dockerfile -t order-api .     (from the repository root)
# Run:    docker compose --profile api up -d                         (from src/, with the database stack)
#=========================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY . .
RUN dotnet publish src/Order.Presentation.Api/Order.Presentation.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_HTTP_PORTS=5000

COPY --from=build /app/publish .

# Non-root user provided by the .NET images.
USER $APP_UID

ENTRYPOINT ["dotnet", "Order.Presentation.Api.dll"]
