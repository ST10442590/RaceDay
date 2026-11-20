# RaceDay MVC front end.
# Build:  docker build -t raceday-web Part3-MVC/RaceDay.Web
# Run:    docker run -d -p 8080:8080 --add-host=host.docker.internal:host-gateway --name raceday-web raceday-web
# The API keeps running on the host machine; the container reaches it through
# host.docker.internal. Override Api__BaseUrl with -e to point elsewhere.

# Stage 1: restore and publish with the full .NET SDK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, in its own layer, so it is cached until the project file changes.
COPY RaceDay.Web.csproj ./
RUN dotnet restore RaceDay.Web.csproj

COPY . ./
RUN dotnet publish RaceDay.Web.csproj --configuration Release --no-restore --output /app/publish

# Stage 2: a much smaller runtime-only image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    Api__BaseUrl=http://host.docker.internal:5120/

EXPOSE 8080

# Run as the image's built-in non-root user.
USER $APP_UID

ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]