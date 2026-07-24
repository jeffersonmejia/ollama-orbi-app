# Orbi App

MVP de entrega de pedidos construido a partir del proyecto base Sakila con ASP.NET Core MVC, Entity Framework Core, PostgreSQL e Identity.

## Contents

1. [Summary](#summary)
2. [Technologies](#technologies)
3. [Access Control](#access-control)
4. [Installation](#installation)
5. [Run](#run)

## Summary

El proyecto conserva los módulos del proyecto base y agrega el dominio de Orbi para tiendas, productos, pedidos y entregas con acceso según rol.

Módulos principales:

- Catálogo breve de tiendas y productos
- Creación y consulta de pedidos para usuarios
- Seguimiento de entregas para repartidores
- Administración de tiendas y estados de pedidos

## Technologies

| Technology | Version |
|---|---|
| .NET SDK | 10.0 |
| ASP.NET Core MVC | 10.0 |
| C# | 13 |
| Entity Framework Core | 10.0.2 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.1 |
| PostgreSQL | 16 |
| ASP.NET Core Identity | 10.0.2 |
| Bootstrap | 5.3 |
| jQuery | 3.7 |

## Access Control

Seeded users:

| User | Password | Role |
|---|---|---|
| admin1@orbi.app | Admin123* | Administrador |
| admin2@orbi.app | Admin123* | Administrador |
| usuario@orbi.app | Usuario123* | Usuario |
| repartidor@orbi.app | Reparto123* | Repartidor |

Permission matrix:

| Módulo / Acción | Administrador | Usuario | Repartidor |
|---|---|---|---|
| Ver tiendas y productos | Sí | Sí | Sí |
| Crear y consultar pedidos propios | No | Sí | No |
| Consultar y actualizar entregas asignadas | No | No | Sí |
| Administrar tiendas y estados | Sí | No | No |

## Installation

Clone the repository:

```bash
git clone git@github.com:jeffersonmejia/sakila-app-entity-framework.git
cd sakila-app-entity-framework
```

Restore dependencies:

```bash
dotnet restore
```

Crear una base PostgreSQL 16 llamada `orbi_app`.

Configure the connection string in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=orbi_app;Username=your_user;Password=your_password"
  }
}
```

Apply Identity migrations:

```bash
dotnet ef database update --context ApplicationDbContext
```

## Run

```bash
dotnet run
```

Open `http://localhost:5175`.

## Docker Stack

El `docker-compose.yml` se conserva para la base PostgreSQL del proyecto base. La aplicación completa se despliega mediante `docker-stack.yml`.

No es necesario instalar .NET en el equipo: el `Dockerfile` usa la imagen SDK de .NET 10 para compilar y la imagen ASP.NET 10 para ejecutar la aplicación.

En Windows, `secrets.txt` y Docker Desktop son suficientes para realizar el despliegue completo:

```powershell
.\scripts\deploy-stack.ps1
```

El script inicializa Docker Swarm cuando sea necesario, registra los secretos que todavía no existen, etiqueta el nodo para PostgreSQL, construye la imagen y despliega el stack. Nunca agrega `secrets.txt` a la imagen ni al repositorio.

Construir primero la imagen .NET 10 definida en el `Dockerfile`:

```bash
docker build -t orbiapp:latest .
```

El stack espera los secretos externos declarados al final de `docker-stack.yml` y que el nodo de PostgreSQL tenga la etiqueta `sakila.postgres-data=true`. Después se despliega con:

```bash
docker stack deploy -c docker-stack.yml orbi
```

La aplicación queda publicada en `http://localhost:5164`. En un Swarm de varios nodos se puede indicar una imagen de registro mediante `ORBI_APP_IMAGE` antes del despliegue.
