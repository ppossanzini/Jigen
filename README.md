# Jigen DB

**Jigen DB** is vector database written from scratch in c#. 
Actually is a study/research project. It aims to explore vector databases and their use cases. 

Many performance tips will be added and explored, analyzing different approaches and optimizations.


> Tech stack: **.NET (net8.0 / net10.0)**, **ASP.NET Core** (hosting), **gRPC**.

---

## Table of contents

- [Prerequisites](#prerequisites)
- [High-level structure](#high-level-structure)
- [Running the gRPC server](#running-the-grpc-server)
- [Using the client](#using-the-client)
- [CORS / gRPC-Web](#cors--grpc-web)
- [Tests](#tests)
- [Troubleshooting](#troubleshooting)
- [License](#license)

---

## Prerequisites

- A **.NET SDK** compatible with the project targets (**net8.0** and/or **net10.0**)

Check your installation:


## High-level structure

- **Server**: Exposes a gRPC service for vector database operations.
- **Client**: Provides a client library for interacting with the gRPC server.
- **Tests**: Includes unit tests and performance benchmarks for the database operations.
- **Documentation**: Contains detailed documentation for developers and users.


