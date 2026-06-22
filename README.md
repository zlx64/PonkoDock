# PonkoDock

PonkoDock is a lightweight Docker container management dashboard built with **.NET 10** and **Blazor**. It provides a streamlined interface for monitoring and managing your containers.

## ✨ Features

- **Real-time Log Streaming**: Stream container logs instantly with a high-performance implementation.
- **Container Management**: Easily Start, Stop, and Restart containers directly from the UI.
- **Lightweight**: Built using .NET 10 chiseled images for a minimal footprint.

## 🚀 Quick Start (Docker)

The easiest way to run PonkoDock is using the official image hosted on GitHub Container Registry (GHCR).

### Using Docker Run

```bash
docker run -d \
  -p 8080:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -e Docker__Uri=unix:///var/run/docker.sock \
  --name ponkodock \
  ghcr.io/zlx64/ponkodock:latest
```

### Using Docker Compose

Create a `docker-compose.yml` file:

```yaml
services:
  ponkodock:
    image: ghcr.io/zlx64/ponkodock:latest
    container_name: ponkodock
    restart: unless-stopped
    ports:
      - "8080:8080"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
    environment:
      - DOTNET_ENVIRONMENT=Production
      - Docker__Uri=unix:///var/run/docker.sock
```

Then run:
```bash
docker compose up -d
```

## 🛠 Configuration

| Environment Variable | Description | Default |
|----------------------|-------------|---------|
| `Docker__Uri`        | The URI to the Docker daemon (e.g., `unix:///var/run/docker.sock` for Linux or `npipe:////./pipe/docker_engine` for Windows) | `unix:///var/run/docker.sock` |
| `DOTNET_ENVIRONMENT` | The .NET environment (e.g., `Production`, `Development`) | `Production` |
| `ASPNETCORE_HTTP_PORTS` | The port the application listens on | `8080` |

## 🖥️ Accessing the Dashboard

Once the container is running, open your browser and navigate to:
`http://localhost:8080`
