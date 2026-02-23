# Zafiro PMS - Backend API

Sistema de gestión hotelera (PMS) robusto y escalable desarrollado con **.NET 10** y **Clean Architecture**. Este backend sirve como el núcleo lógico para la administración de propiedades, gestionando desde reservas y huéspedes hasta integraciones con OTAs (como Booking.com) y puntos de venta (POS).

## 🚀 Tecnologías y Stack

* **Core:** .NET 10 (Preview/RC) / C#
* **Arquitectura:** Clean Architecture (API, Application, Domain, Infrastructure)
* **Base de Datos:** Entity Framework Core (Soporte para SQL Server y PostgreSQL)
* **Seguridad:** Autenticación JWT & BCrypt para hashing
* **Documentación:** Swagger / OpenAPI
* **Integraciones:** Booking.com (Webhooks & Sync), Servicios de Email (SMTP)
* **Background Jobs:** `IHostedService` para sincronización de reservas y tareas de limpieza (Housekeeping)
* **DevOps:** Docker & Docker Compose

## 📂 Estructura del Proyecto

La solución sigue una separación estricta de responsabilidades:

* **src/Domain:** Entidades del núcleo (Folios, Guests, Reservations, Rooms), Enums y Reglas de negocio. Sin dependencias externas.
* **src/Application:** Casos de uso, Interfaces (Repositorios, Servicios), DTOs y validaciones.
* **src/Infrastructure:** Implementación de acceso a datos (EF Core), Migraciones, Servicios externos (Email) e Integraciones.
* **src/API:** Controladores REST, Configuración de Inyección de Dependencias, Middlewares y Workers en segundo plano.

## ✨ Funcionalidades Clave

* **Gestión de Reservas:** Creación, modificación y flujo de estados (Check-in/Check-out).
* **Motor de Folios y Transacciones:** Manejo de cuentas de huéspedes, cargos y pagos.
* **Punto de Venta (POS):** API para gestión de turnos de caja, ventas directas y productos.
* **Integración OTA:** Webhook para recibir reservas de Booking.com y workers para sincronización bidireccional.
* **Dashboard Analytics:** Endpoints optimizados para métricas de ocupación, ingresos y demografía.
* **Guest Experience:** Endpoints para Check-in online.

## 🛠️ Configuración Local

### Prerrequisitos
* .NET 10 SDK
* SQL Server o PostgreSQL (Configurable en `appsettings.json`)
* Docker (Opcional)

### Instalación

1.  **Clonar el repositorio:**
    ```bash
    git clone [https://github.com/tu-usuario/pms-zafiro-backend.git](https://github.com/tu-usuario/pms-zafiro-backend.git)
    cd pms-zafiro-backend
    ```

2.  **Configurar Variables de Entorno:**
    Actualiza el archivo `src/API/appsettings.json` o usa User Secrets para la cadena de conexión y configuraciones JWT.
    ```json
    "ConnectionStrings": {
      "DefaultConnection": "Server=localhost;Database=PmsZafiroDb;..."
    },
    "JwtSettings": {
      "Key": "TU_CLAVE_SECRETA_SUPER_SEGURA",
      "Issuer": "PmsZafiroAPI",
      ...
    }
    ```

3.  **Ejecutar Migraciones:**
    ```bash
    dotnet ef database update --project src/Infrastructure --startup-project src/API
    ```

4.  **Ejecutar el proyecto:**
    ```bash
    dotnet run --project src/API
    ```
    La documentación de la API estará disponible en: `https://localhost:7062/swagger`

### 🐳 Ejecución con Docker

El proyecto incluye orquestación con Docker Compose para levantar la API y la Base de Datos:

```bash
docker-compose up --build
