# Orbi App - Requirements Specification (Tercer Parcial)

> **Proyecto:** Orbi App
> **Dominio:** Plataforma web para gestión y reparto de pedidos.
> **Arquitectura base:** ASP.NET Core MVC + Entity Framework Core + PostgreSQL + Docker Swarm.
> **Generación de datos:** Bogus para .NET con datos latinoamericanos realistas.

---

# 1. Descripción del proyecto

Orbi App es una plataforma empresarial para la gestión integral de pedidos y entregas, permitiendo administrar clientes, vendedores, repartidores, productos, tiendas, inventario, pagos y distribución de pedidos.

La aplicación deberá evolucionar sobre la arquitectura implementada en los parciales anteriores, manteniendo las funcionalidades existentes e incorporando seguridad avanzada, procesamiento transaccional, generación masiva de datos realistas, inteligencia artificial, despliegue distribuido y alta disponibilidad.

El desarrollo deberá respetar el esquema actual de la base de datos y ampliar sus entidades únicamente cuando sea necesario para cumplir los requerimientos del tercer parcial.

---

# 2. Objetivo

Desarrollar una aplicación empresarial distribuida capaz de:

* gestionar exactamente 1.000.000 de registros de negocio;
* generar datos realistas mediante Bogus para .NET;
* representar información coherente con Ecuador y Latinoamérica;
* procesar pedidos y movimientos de inventario mediante transacciones;
* integrar múltiples pasarelas de pago;
* implementar autenticación multifactor;
* utilizar servicios SMTP;
* consumir un modelo de inteligencia artificial ejecutado en contenedor;
* desplegar toda la solución mediante Docker Swarm;
* mantener trazabilidad, seguridad, escalabilidad y alta disponibilidad.

---

# 3. Roles del sistema

La aplicación deberá implementar los siguientes roles.

## Administrador

* Administración completa del sistema.
* Gestión de usuarios.
* Gestión de roles.
* Gestión de tiendas.
* Gestión de productos.
* Gestión de inventario.
* Gestión de pedidos.
* Gestión de pagos.
* Reportes.
* Configuración.
* Auditoría.
* Gestión de IA.
* Supervisión de los servicios desplegados.

## Vendedor

* Registrar pedidos.
* Gestionar clientes.
* Consultar productos.
* Consultar tiendas.
* Procesar pagos.
* Consultar inventario.
* Consultar historial de ventas.
* Consultar el estado de los pedidos registrados.

## Repartidor

* Consultar pedidos asignados.
* Consultar direcciones de entrega.
* Actualizar el estado de entrega.
* Confirmar entregas.
* Registrar incidencias.
* Consultar su historial de entregas.

## Cliente

* Registro.
* Inicio de sesión.
* Gestión de perfil.
* Administración de direcciones.
* Consultar tiendas.
* Consultar productos.
* Realizar pedidos.
* Consultar historial.
* Realizar pagos.
* Consultar el estado del pedido.
* Activar autenticación multifactor.

---

# 4. Tecnologías requeridas

## Backend

* ASP.NET Core MVC.
* C#.
* .NET.
* API REST para integraciones internas y externas.

## ORM

* Entity Framework Core.
* Npgsql Entity Framework Core Provider.

## Base de datos

* PostgreSQL.

## Seguridad

* ASP.NET Core Identity.
* Autenticación basada en cookies.
* Roles y políticas de autorización.
* MFA mediante TOTP.

## Interfaz

* Razor Views.
* Bootstrap.
* Diseño responsive para computadoras, tabletas y dispositivos móviles.

## Generación de datos

* Bogus para .NET.
* Generadores personalizados para datos ecuatorianos.
* Procesamiento por lotes.
* Semilla configurable para reproducción de resultados.

## Correo

* SMTP.

## Pagos

* PayPal Sandbox.
* PayPhone Sandbox o una pasarela equivalente autorizada.

## Inteligencia artificial

* Modelo Open Source ejecutado mediante un contenedor independiente.
* Consumo mediante API HTTP.

## Contenedores

* Docker.

## Orquestación

* Docker Swarm.

## Repositorio

* GitHub.

---

# 5. Arquitectura

La aplicación utilizará ASP.NET Core MVC y deberá aplicar separación de responsabilidades.

La lógica de negocio deberá estar desacoplada mediante servicios especializados.

Los controladores únicamente deberán:

* recibir solicitudes;
* validar entradas;
* comprobar autorización;
* invocar servicios;
* devolver vistas o respuestas;
* manejar resultados sin contener lógica compleja de negocio.

Interfaces mínimas:

* `IInventoryService`
* `IPaymentService`
* `IPaymentGateway`
* `IEmailService`
* `IAccountService`
* `IAIService`
* `IAuditService`
* `IOrderService`
* `IStoreService`
* `IProductService`
* `IDataGenerationService`

La capa de servicios deberá encargarse de:

* reglas de negocio;
* transacciones;
* validaciones;
* comunicación con pasarelas;
* comunicación con SMTP;
* consumo de IA;
* generación masiva de datos;
* auditoría;
* control de inventario.

No se deberá colocar lógica de negocio directamente en los controladores ni en las vistas.

---

# 6. Esquema actual de la base de datos

El proyecto parte del siguiente esquema PostgreSQL, correspondiente a las tiendas, productos, pedidos, detalles de pedidos, provincias, ciudades y perfiles de usuario de Orbi App.

Este esquema deberá conservarse y utilizarse como base para la generación de datos con Bogus.

```sql
-- Orbi App - Esquema mínimo para tiendas y entregas

CREATE TABLE IF NOT EXISTS delivery_store (
    delivery_store_id integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    name varchar(100) NOT NULL,
    category varchar(60) NOT NULL,
    address varchar(180) NOT NULL,
    is_active boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS delivery_product (
    delivery_product_id integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    delivery_store_id integer NOT NULL REFERENCES delivery_store(delivery_store_id),
    name varchar(100) NOT NULL,
    price numeric(10,2) NOT NULL CHECK (price >= 0),
    is_available boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS delivery_order (
    delivery_order_id integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    delivery_store_id integer NOT NULL REFERENCES delivery_store(delivery_store_id),
    customer_email varchar(256) NOT NULL,
    delivery_person_email varchar(256),
    delivery_address varchar(180) NOT NULL,
    status varchar(30) NOT NULL DEFAULT 'Pendiente'
        CHECK (status IN ('Pendiente', 'En preparación', 'En camino', 'Entregado', 'Cancelado')),
    total numeric(10,2) NOT NULL CHECK (total >= 0),
    created_at timestamp with time zone NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS delivery_order_item (
    delivery_order_item_id integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    delivery_order_id integer NOT NULL REFERENCES delivery_order(delivery_order_id) ON DELETE CASCADE,
    delivery_product_id integer NOT NULL REFERENCES delivery_product(delivery_product_id),
    product_name varchar(100) NOT NULL,
    quantity integer NOT NULL CHECK (quantity > 0),
    unit_price numeric(10,2) NOT NULL CHECK (unit_price >= 0),
    subtotal numeric(10,2) NOT NULL CHECK (subtotal >= 0)
);

CREATE TABLE IF NOT EXISTS ecuador_province (
    province_code varchar(2) PRIMARY KEY,
    name varchar(100) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS ecuador_city (
    city_code varchar(4) PRIMARY KEY,
    province_code varchar(2) NOT NULL REFERENCES ecuador_province(province_code),
    name varchar(100) NOT NULL,
    UNIQUE (province_code, name)
);

CREATE TABLE IF NOT EXISTS user_profile (
    identity_user_id text PRIMARY KEY REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    first_name varchar(80) NOT NULL,
    last_name varchar(80) NOT NULL,
    cedula varchar(10) NOT NULL UNIQUE CHECK (cedula ~ '^[0-9]{10}$'),
    address_line_1 varchar(160) NOT NULL,
    address_line_2 varchar(160) NOT NULL,
    province_code varchar(2) NOT NULL REFERENCES ecuador_province(province_code),
    city_code varchar(4) NOT NULL REFERENCES ecuador_city(city_code),
    reference varchar(240),
    created_at timestamp with time zone NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_delivery_order_customer
    ON delivery_order(customer_email);

CREATE INDEX IF NOT EXISTS ix_delivery_order_driver
    ON delivery_order(delivery_person_email);

CREATE INDEX IF NOT EXISTS ix_ecuador_city_province
    ON ecuador_city(province_code);

CREATE INDEX IF NOT EXISTS ix_user_profile_location
    ON user_profile(province_code, city_code);
```

Las ampliaciones realizadas al esquema deberán implementarse mediante migraciones de Entity Framework Core.

No se deberán eliminar ni renombrar las tablas actuales sin una justificación técnica.

---

# 7. Generación de datos con Bogus

La generación del millón de registros deberá realizarse principalmente mediante la librería Bogus para .NET.

Instalación requerida:

```bash
dotnet add package Bogus
```

No se deberán generar datos inflados, repetitivos o artificiales como:

* Persona 1.
* Persona 2.
* Cliente 1.
* Producto 1.
* Tienda 1.
* Dirección 1.
* [correo1@example.com](mailto:correo1@example.com).
* pedido generado 1.

Los datos deberán parecer registros utilizados en un sistema real.

## Configuración regional

Se deberá utilizar una configuración regional compatible con español y Latinoamérica, por ejemplo:

```csharp
new Faker("es")
```

Cuando Bogus no proporcione directamente datos ecuatorianos, deberán implementarse generadores personalizados.

## Características de los datos

Los datos generados deberán incluir:

* nombres y apellidos latinoamericanos;
* correos derivados de nombres y apellidos;
* números telefónicos con formatos válidos;
* direcciones coherentes;
* provincias reales de Ecuador;
* ciudades reales relacionadas con su provincia;
* nombres de tiendas creíbles;
* categorías comerciales reales;
* nombres de productos coherentes con la categoría de la tienda;
* precios comercialmente razonables;
* fechas distribuidas en períodos realistas;
* estados de pedidos coherentes;
* cantidades de productos razonables;
* direcciones de entrega completas;
* referencias domiciliarias;
* repartidores y clientes diferenciados;
* relaciones válidas entre tiendas, productos, pedidos y usuarios.

## Datos ecuatorianos

Los perfiles deberán utilizar:

* nombres frecuentes en Ecuador y Latinoamérica;
* apellidos frecuentes en Ecuador y Latinoamérica;
* provincias oficiales de Ecuador;
* ciudades existentes;
* direcciones con calles, avenidas, ciudadelas, barrios, manzanas y solares;
* números de cédula de diez dígitos;
* correos únicos;
* referencias domiciliarias realistas.

Las cédulas generadas no deberán ser cadenas aleatorias de diez dígitos. Deberán superar una validación algorítmica de cédula ecuatoriana.

Los códigos de provincia y ciudad deberán corresponder a registros existentes en:

* `ecuador_province`;
* `ecuador_city`.

Una ciudad no podrá asociarse con una provincia incorrecta.

## Tiendas

Los nombres de tiendas deberán ser creíbles, por ejemplo:

* Mercado Santa Elena.
* Farmacia Vida Sana.
* Panadería El Trigal.
* Minimarket La Esquina.
* Restaurante Sabor Manabita.
* Tecnología Andina.
* Librería Nuevo Horizonte.

Las categorías podrán incluir:

* restaurantes;
* farmacias;
* supermercados;
* minimarkets;
* panaderías;
* tecnología;
* librerías;
* ferreterías;
* ropa;
* artículos para el hogar.

## Productos

Los nombres de los productos deberán depender de la categoría de la tienda.

Ejemplos:

* una farmacia podrá vender medicamentos de venta libre y productos de cuidado personal;
* una panadería podrá vender panes, tortas y bebidas;
* un restaurante podrá vender platos y bebidas;
* una tienda tecnológica podrá vender periféricos y accesorios;
* una ferretería podrá vender herramientas y materiales.

No se deberá asignar un producto incompatible con la categoría de la tienda.

Los precios deberán:

* ser mayores o iguales a cero;
* utilizar dos decimales;
* mantenerse dentro de rangos razonables;
* conservar coherencia entre productos similares;
* usar dólares estadounidenses.

## Pedidos

Los pedidos generados deberán mantener coherencia referencial y temporal.

Cada pedido deberá:

* pertenecer a una tienda existente;
* utilizar el correo de un cliente existente;
* contener una dirección de entrega válida;
* tener entre uno y varios detalles;
* contener productos de la misma tienda;
* calcular el subtotal como cantidad por precio unitario;
* calcular el total a partir de sus detalles;
* usar estados permitidos por la base de datos;
* asignar repartidor únicamente cuando corresponda;
* utilizar fechas razonables.

Reglas sugeridas:

* un pedido pendiente podrá no tener repartidor;
* un pedido en preparación podrá tener repartidor asignado o pendiente;
* un pedido en camino deberá tener repartidor;
* un pedido entregado deberá tener repartidor;
* un pedido cancelado podrá no tener repartidor;
* los pedidos recientes podrán estar pendientes o en preparación;
* los pedidos antiguos tendrán mayor probabilidad de estar entregados o cancelados.

## Reproducibilidad

La generación deberá utilizar una semilla configurable:

```csharp
Randomizer.Seed = new Random(2026);
```

Al utilizar la misma semilla y configuración, el proceso deberá generar resultados equivalentes.

La semilla deberá poder modificarse mediante:

* variable de entorno;
* archivo de configuración;
* parámetro de línea de comandos.

## Unicidad

Se deberá garantizar unicidad en:

* cédulas;
* correos;
* identificadores externos;
* códigos que tengan restricción única.

No se deberá confiar únicamente en llamadas independientes a `UniqueIndex`.

La generación deberá controlar duplicados mediante:

* conjuntos en memoria para volúmenes moderados;
* generación determinista;
* validación previa;
* restricciones de base de datos;
* reintentos limitados;
* inserciones transaccionales.

## Procesamiento por lotes

Los registros deberán insertarse en lotes para evitar consumo excesivo de memoria.

El tamaño del lote deberá ser configurable, por ejemplo:

* 1.000 registros;
* 5.000 registros;
* 10.000 registros.

No se deberá construir el millón de entidades completo en memoria antes de guardarlo.

El proceso deberá:

1. generar un lote;
2. insertar el lote;
3. confirmar la transacción;
4. limpiar el seguimiento de Entity Framework Core;
5. continuar con el siguiente lote;
6. registrar el progreso.

Después de cada lote se deberá utilizar una estrategia equivalente a:

```csharp
context.ChangeTracker.Clear();
```

Para cargas masivas se podrá utilizar:

* Entity Framework Core por lotes;
* `COPY` de PostgreSQL;
* Npgsql Binary Import;
* una librería de inserción masiva compatible con PostgreSQL.

El uso de una técnica de carga masiva no elimina la obligación de utilizar Bogus para generar los datos.

---

# 8. Cantidad de registros

La base de datos deberá contener exactamente:

**1.000.000 de registros de negocio generados.**

No deberán contabilizarse como registros de negocio:

* tablas internas de ASP.NET Core Identity;
* historial de migraciones;
* catálogos mínimos del framework;
* configuraciones del sistema;
* registros técnicos de Docker;
* tablas temporales.

Podrán contabilizarse:

* tiendas;
* productos;
* perfiles de clientes;
* perfiles de vendedores;
* perfiles de repartidores;
* pedidos;
* detalles de pedidos;
* pagos;
* movimientos de inventario;
* auditorías;
* incidencias de entrega;
* direcciones adicionales.

La distribución deberá adaptarse al esquema real de Orbi App.

Distribución inicial propuesta:

| Tabla                     |     Registros |
| ------------------------- | ------------: |
| `delivery_store`          |         2.000 |
| `delivery_product`        |        80.000 |
| `user_profile`            |       120.000 |
| `delivery_order`          |       240.000 |
| `delivery_order_item`     |       420.000 |
| Pagos                     |        90.000 |
| Movimientos de inventario |        35.000 |
| Auditorías                |        10.000 |
| Incidencias de entrega    |         3.000 |
| **Total**                 | **1.000.000** |

Las tablas que todavía no existen deberán crearse mediante migraciones.

Las provincias y ciudades de Ecuador se considerarán catálogos geográficos y no deberán utilizarse para inflar artificialmente el total.

El sistema deberá incluir una consulta de comprobación que muestre el total exacto de registros de negocio.

Ejemplo conceptual:

```sql
SELECT
    (SELECT COUNT(*) FROM delivery_store) +
    (SELECT COUNT(*) FROM delivery_product) +
    (SELECT COUNT(*) FROM user_profile) +
    (SELECT COUNT(*) FROM delivery_order) +
    (SELECT COUNT(*) FROM delivery_order_item) +
    (SELECT COUNT(*) FROM payment) +
    (SELECT COUNT(*) FROM inventory_movement) +
    (SELECT COUNT(*) FROM audit_log) +
    (SELECT COUNT(*) FROM delivery_incident)
    AS total_business_records;
```

El resultado deberá ser:

```text
1000000
```

---

# 9. Ejecución del generador

El generador deberá poder ejecutarse mediante un comando independiente.

Ejemplo esperado:

```bash
dotnet run --project OrbiApp.DataGenerator -- \
  --records 1000000 \
  --batch-size 5000 \
  --seed 2026
```

También podrá ejecutarse desde la aplicación mediante un comando administrativo protegido, siempre que:

* no sea accesible para usuarios no autorizados;
* no se ejecute automáticamente en producción;
* permita visualizar el progreso;
* impida una ejecución duplicada accidental;
* permita cancelar el proceso;
* registre errores y resultados.

Configuraciones mínimas:

```json
{
  "DataGeneration": {
    "Enabled": false,
    "TotalRecords": 1000000,
    "BatchSize": 5000,
    "Seed": 2026,
    "Locale": "es"
  }
}
```

Las credenciales de conexión no deberán almacenarse directamente en el código fuente.

---

# 10. Integridad de los datos generados

Después de la generación se deberán ejecutar validaciones automáticas.

Las validaciones deberán comprobar:

* total exacto de 1.000.000 de registros;
* ausencia de correos duplicados;
* ausencia de cédulas duplicadas;
* cédulas con formato y dígito verificador válido;
* ciudades asociadas con provincias correctas;
* productos asociados con tiendas existentes;
* pedidos asociados con tiendas existentes;
* detalles asociados con pedidos existentes;
* productos del pedido pertenecientes a la tienda seleccionada;
* subtotales correctamente calculados;
* totales correctamente calculados;
* ausencia de cantidades negativas;
* ausencia de precios negativos;
* ausencia de pedidos sin detalles;
* estados válidos;
* repartidores asignados de forma coherente;
* ausencia de claves foráneas inválidas.

También se deberá generar un resumen del proceso con:

* fecha de inicio;
* fecha de finalización;
* duración total;
* registros generados por tabla;
* registros rechazados;
* duplicados detectados;
* errores;
* tamaño de lote;
* semilla utilizada.

---

# 11. Rendimiento

La aplicación deberá utilizar consultas LINQ y operaciones de base de datos como:

* `Where`
* `OrderBy`
* `OrderByDescending`
* `Skip`
* `Take`
* `Count`
* `Sum`
* `Average`
* `GroupBy`
* `Select`
* `AsNoTracking`
* `ILike`
* `Contains`

Además, deberá implementar:

* índices;
* índices compuestos cuando sean necesarios;
* consultas optimizadas;
* proyecciones;
* consultas asíncronas;
* `EXPLAIN ANALYZE`;
* comparación antes y después de crear un índice;
* medición del tiempo de respuesta;
* revisión del plan de ejecución;
* prevención de consultas N+1.

Las consultas de solo lectura deberán utilizar `AsNoTracking` cuando sea apropiado.

No se deberá recuperar una entidad completa cuando únicamente sean necesarias determinadas columnas.

---

# 12. Paginación

## Obligatoria

Se deberá implementar paginación física desde PostgreSQL para:

* clientes;
* tiendas;
* productos;
* pedidos;
* pagos;
* inventario;
* historial de entregas;
* auditorías.

Se utilizará:

* `Skip`;
* `Take`;
* `LIMIT`;
* `OFFSET`.

No deberá cargarse el millón de registros en memoria.

Cada consulta paginada deberá incluir:

* número de página;
* tamaño de página;
* total de registros;
* total de páginas;
* filtros;
* criterio de ordenamiento;
* validación de límites.

El tamaño máximo de página deberá estar controlado para impedir consultas excesivas.

---

# 13. Gestión transaccional de inventario

El inventario deberá actualizarse automáticamente.

Operaciones mínimas:

* ingreso de productos;
* compra de inventario;
* pedido confirmado;
* pedido cancelado;
* pago aprobado;
* pago fallido;
* devolución;
* ajuste de inventario;
* eliminación lógica del producto.

Cada movimiento deberá generar un registro de auditoría.

Las operaciones deberán ejecutarse mediante transacciones de Entity Framework Core.

El sistema deberá impedir:

* stock negativo;
* condiciones de carrera;
* ventas simultáneas inconsistentes;
* confirmaciones duplicadas;
* descuentos dobles de inventario;
* liberaciones duplicadas de reservas.

Se deberán implementar mecanismos como:

* control de concurrencia optimista;
* token de concurrencia;
* nivel de aislamiento adecuado;
* actualización condicional;
* validación dentro de la transacción.

---

# 14. Flujo de pedidos

Flujo esperado:

1. El cliente inicia sesión.
2. Selecciona una tienda.
3. Selecciona productos pertenecientes a esa tienda.
4. El sistema valida disponibilidad e inventario.
5. Se crea el pedido.
6. Se crean los detalles del pedido.
7. Se reserva el stock.
8. El cliente selecciona una pasarela de pago.
9. Se procesa el pago.
10. El backend verifica la transacción.
11. Si el pago es aprobado:

    * confirma el pedido;
    * descuenta o confirma la reserva de inventario;
    * registra el movimiento;
    * registra el pago;
    * asigna el pedido para preparación;
    * envía correo.
12. Si el pago falla:

    * libera la reserva;
    * registra el intento;
    * mantiene el pedido pendiente o lo cancela.
13. El vendedor prepara el pedido.
14. Se asigna un repartidor.
15. El repartidor actualiza el pedido a `En camino`.
16. El repartidor confirma la entrega.
17. Todas las acciones se registran para auditoría.

El flujo deberá ser idempotente.

La aplicación deberá impedir que una misma confirmación de pago o pedido sea procesada más de una vez.

---

# 15. Pasarelas de pago

Se deberán integrar al menos dos pasarelas:

* PayPal Sandbox.
* PayPhone Sandbox.

La lógica deberá implementarse mediante:

```csharp
IPaymentGateway
```

Implementaciones mínimas:

```csharp
PayPalPaymentGateway
PayPhonePaymentGateway
```

Estados mínimos:

* Pendiente.
* Aprobado.
* Cancelado.
* Fallido.
* Expirado.
* Reembolsado.

La aplicación deberá permitir reportes por:

* pasarela;
* estado;
* período;
* monto;
* cliente;
* tienda.

Se deberán registrar:

* identificador externo;
* identificador interno;
* monto;
* moneda;
* estado;
* fecha;
* respuesta de la pasarela;
* cantidad de intentos.

No se deberán almacenar datos completos de tarjetas bancarias.

---

# 16. Servicios SMTP

La aplicación deberá enviar correos para:

* confirmación de cuenta;
* recuperación de contraseña;
* cambio de contraseña;
* bloqueo de cuenta;
* activación de MFA;
* pedido confirmado;
* pago aprobado;
* pago fallido;
* pedido en camino;
* pedido entregado;
* inventario crítico.

Los correos deberán ejecutarse mediante un servicio independiente.

Las credenciales deberán almacenarse mediante:

* variables de entorno;
* Docker Secrets;
* configuración segura del entorno.

Los errores de SMTP no deberán provocar pérdida de pedidos o pagos confirmados.

Se deberá implementar:

* manejo de errores;
* reintentos controlados;
* registro de intentos;
* plantillas de correo;
* envío asíncrono cuando corresponda.

---

# 17. Seguridad

La aplicación deberá implementar:

* ASP.NET Core Identity;
* confirmación de correo;
* recuperación de contraseña;
* MFA mediante TOTP;
* políticas de contraseña;
* bloqueo por intentos fallidos;
* roles;
* permisos;
* políticas de autorización;
* página `AccessDenied`;
* cierre de sesión;
* protección de acciones críticas;
* registro de accesos;
* protección CSRF;
* validación de modelos;
* cookies seguras;
* almacenamiento seguro de secretos;
* prevención de escalamiento de privilegios.

Las acciones administrativas deberán estar disponibles únicamente para el rol Administrador.

Las operaciones de vendedor, repartidor y cliente deberán estar limitadas a las funciones correspondientes a cada rol.

---

# 18. Eliminación lógica

Las entidades maestras deberán implementar:

* `Activo`;
* `FechaEliminacion`;
* `EliminadoPor`.

No se eliminarán físicamente:

* pedidos;
* pagos;
* movimientos de inventario;
* auditorías;
* incidencias de entrega.

Las consultas normales deberán excluir registros eliminados mediante filtros globales o condiciones explícitas.

La eliminación lógica deberá registrar:

* usuario;
* fecha;
* entidad;
* identificador;
* motivo;
* valor anterior;
* valor nuevo.

---

# 19. Auditoría

Se deberán registrar como mínimo:

* inicio de sesión;
* intentos fallidos;
* cambios de contraseña;
* cambios de roles;
* creación de usuarios;
* modificación de perfiles;
* creación de tiendas;
* creación de productos;
* creación de pedidos;
* cambios de estado;
* pagos;
* cambios de inventario;
* eliminación lógica;
* ejecución de IA;
* ejecución del generador de datos;
* acciones administrativas.

Información registrada:

* usuario;
* dirección IP;
* acción;
* entidad;
* identificador de entidad;
* fecha;
* valor anterior;
* valor nuevo;
* resultado;
* origen de la solicitud.

Los valores sensibles no deberán registrarse en texto plano.

---

# 20. Inteligencia artificial

La IA deberá ejecutarse mediante un contenedor independiente.

La aplicación consumirá la IA mediante una API HTTP.

Funcionalidades propuestas para Orbi App:

* recomendación de productos;
* sugerencias para completar pedidos;
* explicación de productos;
* respuesta automática a consultas frecuentes;
* resumen de pedidos;
* generación de recomendaciones basadas en historial;
* asistencia al vendedor.

La integración deberá incluir:

* servicio .NET;
* endpoint;
* cliente HTTP;
* timeout;
* manejo de errores;
* fallback;
* reintentos limitados;
* registro del consumo;
* validación de la respuesta;
* protección de datos personales.

La aplicación deberá continuar funcionando cuando el servicio de IA no esté disponible.

La IA no deberá:

* inventar productos;
* inventar precios;
* inventar disponibilidad;
* modificar pagos;
* confirmar pedidos;
* alterar inventario directamente.

Las respuestas relacionadas con productos deberán basarse en el catálogo oficial de la aplicación.

---

# 21. Docker Swarm

La solución deberá desplegarse mediante Docker Swarm.

Servicios mínimos:

* aplicación ASP.NET Core con dos o más réplicas;
* PostgreSQL;
* contenedor de IA;
* servicio SMTP o Worker;
* red Overlay;
* Docker Configs;
* Docker Secrets;
* volúmenes persistentes;
* Health Checks.

Infraestructura mínima:

* un nodo Manager;
* un nodo Worker.

El servicio web deberá poder escalar horizontalmente.

PostgreSQL deberá utilizar un volumen persistente y una restricción de ubicación cuando sea necesario proteger la ubicación de los datos.

La aplicación no deberá depender del almacenamiento local de una réplica web.

---

# 22. Reportes

La aplicación deberá incluir al menos diez reportes.

Propuesta para Orbi App:

1. Pedidos por fecha.
2. Pedidos por cliente.
3. Pedidos por vendedor.
4. Pedidos por repartidor.
5. Pedidos por tienda.
6. Productos más vendidos.
7. Productos con bajo inventario.
8. Pagos por pasarela.
9. Pagos por estado.
10. Clientes con más compras.
11. Tiempo promedio de entrega.
12. Ventas por provincia y ciudad.

Todos deberán permitir filtros dinámicos.

Los filtros podrán incluir:

* fecha inicial;
* fecha final;
* estado;
* tienda;
* vendedor;
* repartidor;
* cliente;
* provincia;
* ciudad;
* pasarela;
* categoría.

Los reportes deberán realizar agregaciones en PostgreSQL y no en memoria.

---

# 23. Pruebas obligatorias

Se deberán demostrar como mínimo:

* registro de usuario;
* confirmación de correo;
* recuperación de contraseña;
* activación de MFA;
* inicio de sesión con MFA;
* pedido exitoso;
* pedido sin inventario;
* pago aprobado;
* pago cancelado;
* pago fallido;
* confirmación duplicada;
* eliminación lógica;
* consulta paginada;
* generación de datos con Bogus;
* validación del millón de registros;
* validación de cédulas;
* validación de relaciones entre provincia y ciudad;
* validación de subtotales y totales;
* IA disponible;
* IA detenida;
* caída de una réplica;
* usuario sin permisos;
* intento de stock negativo;
* pedidos simultáneos sobre el mismo producto.

Cada prueba deberá incluir:

* identificador;
* objetivo;
* precondiciones;
* datos de entrada;
* procedimiento;
* resultado esperado;
* resultado obtenido;
* evidencia;
* estado final.

---

# 24. Pruebas del generador Bogus

Se deberán ejecutar pruebas específicas sobre el generador.

## Prueba de cantidad

Comprobar que el total de registros de negocio sea exactamente:

```text
1.000.000
```

## Prueba de reproducción

Ejecutar el generador dos veces en bases de datos vacías con la misma semilla y comprobar que los resultados principales sean equivalentes.

## Prueba de variación

Ejecutar el generador con una semilla diferente y comprobar que los datos cambien.

## Prueba de realismo

Revisar una muestra de registros para comprobar que no existan nombres como:

* Persona 1.
* Producto 1.
* Cliente 1.
* Tienda genérica 1.

## Prueba de ubicación

Comprobar que todas las ciudades pertenezcan a la provincia registrada.

## Prueba de cédula

Comprobar que:

* tengan diez dígitos;
* sean únicas;
* superen el algoritmo de validación ecuatoriano.

## Prueba de coherencia comercial

Comprobar que los productos sean compatibles con la categoría de la tienda.

## Prueba de integridad de pedidos

Comprobar que:

* cada pedido tenga detalles;
* cada detalle pertenezca al pedido correcto;
* los productos pertenezcan a la tienda del pedido;
* los subtotales sean correctos;
* el total sea igual a la suma de subtotales.

## Prueba de memoria

Comprobar que la generación por lotes no cargue simultáneamente el millón de registros en memoria.

## Prueba de reejecución

Comprobar que una ejecución accidental no duplique los registros existentes.

---

# 25. Docker y despliegue

Se documentarán los comandos:

```bash
docker swarm init
docker swarm join
docker node ls
docker stack deploy
docker stack services
docker stack ps
docker service ls
docker service scale
docker stack rm
```

También se deberán documentar comandos para:

* revisar logs;
* revisar tareas;
* inspeccionar servicios;
* comprobar secretos;
* comprobar redes;
* validar Health Checks;
* escalar réplicas;
* detener una réplica;
* comprobar recuperación automática.

El sistema deberá continuar funcionando cuando una réplica web deje de estar disponible.

---

# 26. Entregables

* Código fuente de Orbi App.
* Repositorio GitHub actualizado.
* Aplicación ASP.NET Core MVC.
* Migraciones de Entity Framework Core.
* Esquema PostgreSQL actualizado.
* Generador de datos basado en Bogus.
* Configuración regional para datos en español.
* Generadores personalizados para Ecuador.
* Base de datos con exactamente 1.000.000 de registros.
* Consulta de validación del total.
* Evidencia de datos realistas.
* Dockerfiles.
* Archivo de despliegue de Docker Swarm.
* Docker Swarm operativo.
* Servicios SMTP.
* MFA mediante TOTP.
* Dos pasarelas de pago.
* Contenedor de inteligencia artificial.
* Pruebas funcionales.
* Pruebas de rendimiento.
* Evidencias de alta disponibilidad.

---

# 27. Criterios de aceptación de los datos

La carga será aceptada únicamente cuando:

* existan exactamente 1.000.000 de registros de negocio;
* el proceso utilice Bogus;
* los datos sean reproducibles;
* no existan nombres secuenciales artificiales;
* los nombres y apellidos sean latinoamericanos;
* las ubicaciones correspondan a Ecuador;
* las cédulas sean válidas y únicas;
* los correos sean únicos;
* las relaciones sean consistentes;
* las tiendas tengan categorías realistas;
* los productos correspondan a la categoría de su tienda;
* los pedidos tengan detalles válidos;
* los subtotales y totales sean correctos;
* el proceso se ejecute por lotes;
* no se cargue el millón de registros en memoria;
* se registre el progreso;
* se genere un resumen final;
* el sistema permita repetir el proceso desde una base vacía.

---

# 28. Restricciones

* No utilizar datos secuenciales artificiales.
* No utilizar únicamente un script SQL con textos genéricos.
* No copiar un mismo nombre o dirección miles de veces.
* No generar cédulas mediante números completamente aleatorios.
* No asociar ciudades con provincias incorrectas.
* No crear pedidos sin detalles.
* No incluir productos de diferentes tiendas dentro del mismo pedido.
* No calcular los totales con valores aleatorios independientes.
* No insertar todo el millón de registros en una sola operación de Entity Framework Core.
* No almacenar credenciales en el repositorio.
* No ejecutar el generador automáticamente en producción.
* No utilizar los catálogos de provincias y ciudades para inflar el total.
* No depender de la inteligencia artificial para operaciones críticas.

---

# 29. Criterios de calidad

La solución deberá cumplir con:

* arquitectura limpia;
* separación de responsabilidades;
* programación orientada a servicios;
* código mantenible;
* datos coherentes;
* datos realistas;
* generación reproducible;
* procesamiento eficiente;
* seguridad;
* escalabilidad;
* alta disponibilidad;
* optimización de consultas;
* trazabilidad;
* despliegue distribuido;
* validación automática;
* manejo de errores;
* control de concurrencia;
* documentación técnica.

---

# 30. Resultado esperado

El resultado final deberá ser una versión funcional de Orbi App que utilice ASP.NET Core MVC, Entity Framework Core, PostgreSQL y Docker Swarm.

La aplicación deberá trabajar sobre el esquema actual, incorporar las entidades adicionales requeridas y utilizar Bogus para generar exactamente 1.000.000 de registros de negocio realistas.

Los datos deberán representar usuarios, tiendas, productos, pedidos y ubicaciones coherentes con Ecuador y Latinoamérica, evitando contenido secuencial, repetitivo o evidentemente artificial.

El sistema deberá demostrar funcionamiento distribuido, paginación física, transacciones, seguridad, MFA, pasarelas de pago, SMTP, inteligencia artificial en contenedor, auditoría, control de inventario, rendimiento y recuperación frente a la caída de una réplica.
