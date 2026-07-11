# Docker

Jigen publishes three Docker images to Docker Hub, all Linux images built from `mcr.microsoft.com/dotnet/aspnet:10.0`. Each exposes ports `3223` (gRPC) and `13223` (REST/UI) as described in the [server overview](overview.md).

| Image | Contents | Data volumes |
|---|---|---|
| `ppossanzini/jigendb` | Database server only; embedding generation delegated to remote workers over RabbitMQ | `/data/jigendb` |
| `ppossanzini/jigendb-all-in-one` | Database server + in-process ONNX embedding generation | `/data/jigendb`, `/data/onnx` |
| `ppossanzini/jigen-embeddings` | Standalone embedding worker (ONNX Runtime), consumes requests from RabbitMQ, also exposes its own `/api/embeddings` REST endpoint | `/data/onnx` |

## Running a single container

### All-in-one server

```bash
docker run -d \
  --name jigendb \
  -p 3223:3223 -p 13223:13223 \
  -v jigendb-data:/data/jigendb \
  -v jigendb-models:/data/onnx \
  ppossanzini/jigendb-all-in-one:latest
```

Model files for the ONNX embedding pipeline must be present under `/data/onnx` (see [embeddings configuration](../embeddings/configuration.md) for the expected layout, e.g. `/data/onnx/nomic-embed-text-v1.5/`).

### Database server (distributed topology)

```bash
docker run -d \
  --name jigendb \
  -p 3223:3223 -p 13223:13223 \
  -v jigendb-data:/data/jigendb \
  -e Kaido__Enabled=true \
  -e RabbitMQ__HostName=rabbitmq \
  -e RabbitMQ__UserName=jigen \
  -e RabbitMQ__Password=change-me \
  ppossanzini/jigendb:latest
```

### Embedding worker

```bash
docker run -d \
  --name jigen-embeddings \
  -p 3223:3223 -p 13223:13223 \
  -v jigendb-models:/data/onnx \
  -e RabbitMQ__HostName=rabbitmq \
  -e RabbitMQ__UserName=jigen \
  -e RabbitMQ__Password=change-me \
  ppossanzini/jigen-embeddings:latest
```

The worker's own REST/gRPC ports are only useful if you call its `/api/embeddings` endpoint directly; in the distributed topology, requests normally arrive through RabbitMQ from the database server.

## docker compose: all-in-one

```yaml
services:
  jigendb:
    image: ppossanzini/jigendb-all-in-one:latest
    ports:
      - "3223:3223"
      - "13223:13223"
    volumes:
      - jigendb-data:/data/jigendb
      - jigendb-models:/data/onnx
    restart: unless-stopped

volumes:
  jigendb-data:
  jigendb-models:
```

## docker compose: distributed topology

```yaml
services:
  jigendb:
    image: ppossanzini/jigendb:latest
    ports:
      - "3223:3223"
      - "13223:13223"
    volumes:
      - jigendb-data:/data/jigendb
    environment:
      - Kaido__Enabled=true
      - RabbitMQ__HostName=rabbitmq
      - RabbitMQ__UserName=jigen
      - RabbitMQ__Password=change-me
      - RabbitMQ__VirtualHost=Jigen
    depends_on:
      - rabbitmq
    restart: unless-stopped

  jigen-embeddings:
    image: ppossanzini/jigen-embeddings:latest
    volumes:
      - jigendb-models:/data/onnx
    environment:
      - RabbitMQ__HostName=rabbitmq
      - RabbitMQ__UserName=jigen
      - RabbitMQ__Password=change-me
      - RabbitMQ__VirtualHost=Jigen
    depends_on:
      - rabbitmq
    deploy:
      replicas: 2
    restart: unless-stopped

  rabbitmq:
    image: rabbitmq:3-management
    environment:
      - RABBITMQ_DEFAULT_USER=jigen
      - RABBITMQ_DEFAULT_PASS=change-me
      - RABBITMQ_DEFAULT_VHOST=Jigen
    restart: unless-stopped

volumes:
  jigendb-data:
  jigendb-models:
```

Scale embedding throughput horizontally by adding more `jigen-embeddings` replicas (`deploy.replicas`, or `docker compose up --scale jigen-embeddings=N`) — they compete as independent RabbitMQ consumers, so the database server needs no configuration change.

See [server configuration](configuration.md) for the full list of environment variables (`JigenServer__*`, `RabbitMQ__*`, `Kaido__Enabled`) and [embeddings configuration](../embeddings/configuration.md) for the `JigenEmbeddings__*` variables used by the all-in-one and embedding-worker images.
