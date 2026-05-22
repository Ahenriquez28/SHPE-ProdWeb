# Use the official .NET 10 SDK image for development
# SDK includes everything needed to build and run dotnet watch
FROM mcr.microsoft.com/dotnet/sdk:10.0

# Set working directory inside the container
WORKDIR /app

# Install the EF Core CLI tool inside the container
# This lets us run dotnet ef migrations and database update from inside Docker
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

# Copy project file first and restore packages
# This layer is cached so packages aren't re-downloaded on every build
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the source code
COPY . .

# Expose port 8080 (mapped to localhost:5001 in docker-compose.yml)
EXPOSE 8080