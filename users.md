# Usuarios de demostración de Orbi App

Los usuarios se crean automáticamente al iniciar la aplicación mediante `IdentitySeeder`.
Todos los usuarios están pre-confirmados (`EmailConfirmed = true`) y se reseedean en cada arranque.

## Credenciales

| Nombre | Rol | Correo | Contraseña |
|---|---|---|---|
| Jefferson Mejía | Administrador | jefferson.mejia@orbi.com | Admin123* |
| María López | Vendedor | maria.lopez@orbi.com | Vendedor123* |
| Carlos Pérez | Repartidor | carlos.perez@orbi.com | Reparto123* |
| Ana Torres | Usuario | ana.torres@orbi.com | Usuario123* |

## Datos por rol

### Administrador — Jefferson Mejía
- **Cedula:** 0912345675
- **Dirección:** Av. Principal 101, Calle 9 de Octubre — Guayaquil (Guayas)
- **Referencia:** Frente al parque
- **Perfil:** Acceso total a la administración de tiendas, productos, pedidos, usuarios e incidencias.

### Vendedor — María López
- **Cedula:** 1712345671
- **Dirección:** Av. Amazonas, Calle Naciones Unidas — Quito (Pichincha)
- **Tiendas asignadas:** Mercado Popular, Tienda Gourmet, Café Artesanal
- **Productos:** 12 productos creados (alimentos, gourmet, café)
- **Perfil:** Gestiona catálogo de productos y revisa pedidos de sus tiendas.

### Repartidor — Carlos Pérez
- **Cedula:** 0923456784
- **Dirección:** Av. Nicolás Lapentti, Calle Loja — Durán (Guayas)
- **Pedidos asignados:** 5 pedidos con diferentes estados
- **Incidencias:** 2 incidencias reportadas (retraso en ruta, dirección incorrecta)
- **Perfil:** Visualiza y actualiza el estado de entregas asignadas.

### Usuario — Ana Torres
- **Cedula:** 0123456782
- **Dirección:** Av. de las Américas, Calle del Batán — Cuenca (Azuay)
- **Referencia:** Casa esquinera
- **Pedidos:** 3 pedidos realizados (entregado, en camino, pendiente)
- **Perfil:** Explora tiendas, realiza pedidos y revisa su estado.

## Datos sembrados automáticamente

El `IdentitySeeder` también crea:

- **3 tiendas:** Mercado Popular (Alimentos), Tienda Gourmet (Gourmet), Café Artesanal (Cafetería)
- **12 productos:** 4 por tienda con precios y stock realistas
- **6 pedidos:** Con diferentes estados (Pendiente, En preparación, En camino, Entregado, Cancelado)
- **8 ítems de pedido:** Detalle de productos por pedido
- **6 pagos:** PayPhone y PayPal, en estados Pendiente y Aprobado
- **4 movimientos de inventario:** Entradas y salidas registradas
- **2 incidencias:** Reportadas por el repartidor

Todos los datos son idempotentes: si ya existen, no se vuelven a crear.
