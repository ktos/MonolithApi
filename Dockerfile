FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -o out

# Download monolith binary
RUN curl -L -o /app/monolith https://github.com/Y2Z/monolith/releases/download/v2.10.1/monolith-gnu-linux-x86_64    

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .
COPY --from=build /app/monolith .
RUN chmod +x /app/monolith

EXPOSE 8080

ENV ASPNETCORE_USEBUNDLEDMONOLITH=true

ENTRYPOINT ["dotnet", "MonolithApi.dll"]
