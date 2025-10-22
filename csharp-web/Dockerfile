FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy source code and build
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install tools that will help the attacker (simulating a non-hardened image)
RUN apt-get update && apt-get install -y \
    curl \
    wget \
    netcat-traditional \
    procps \
    iproute2 \
    dnsutils \
    net-tools \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Intentionally run as root (bad practice)
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "VulnerableApp.dll"]
