# Usuarios de demostración de Orbi App

Estas cuentas son únicamente para el entorno local del MVP.

| Nombre | Rol | Correo | Contraseña |
|---|---|---|---|
| Jefferson Mejía | Administrador | jefferson.mejia@orbi.com | Admin123* |
| María López | Vendedor | maria.lopez@orbi.com | Vendedor123* |
| Carlos Pérez | Repartidor | carlos.perez@orbi.com | Reparto123* |
| Ana Torres | Usuario | ana.torres@orbi.com | Usuario123* |

Los usuarios se crean al iniciar la aplicación mediante `IdentitySeeder`. El registro público permite crear cuentas con los roles Usuario, Vendedor y Repartidor; el rol Administrador solo se aprovisiona mediante este seed. Para una entrega real, las contraseñas deben almacenarse como secretos y cambiarse después del primer acceso.
