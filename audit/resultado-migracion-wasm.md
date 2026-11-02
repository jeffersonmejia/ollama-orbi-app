# Migración de presentación a Blazor WebAssembly

Estado: **en curso; el frontend aún no es WASM puro**.

## Línea base y matriz de equivalencias

El repositorio contiene 78 archivos `.cshtml` bajo `src/Features` (incluyen páginas, layouts, imports y parciales), 34 code-behind `.cshtml.cs` y 12 componentes `.razor` en `Presentation.Client`. El host combina MVC, Razor Pages y Blazor Interactive Auto. Las rutas actuales de MVC/Razor Pages conservan su procesamiento en servidor hasta que su pantalla se migre y se verifique.

| Módulo actual | Pantallas fuente principales | Destino Blazor | Datos/acciones que deben continuar en servidor | Estado |
|---|---|---|---|---|
| Identidad y cuenta | `Features/Identity/Presentation/Areas/Identity/Pages/Account/**` (acceso, registro, confirmaciones, seguridad, perfil, direcciones, pagos) | Páginas/componentes de autenticación y cuenta bajo `Presentation.Client/Pages/Identity/` | ASP.NET Core Identity, cookies HttpOnly, antiforgery, OAuth y operaciones de cuenta por endpoints HTTP | Pendiente; `/registro` ya tiene shell cliente pero aún publica a Razor Pages |
| Tiendas y compras Dash | `Features/Delivery/Presentation/Views/Delivery/Index.cshtml`, `Cart.cshtml`, `Orders.cshtml`, `MyPayments.cshtml`, `Profile.cshtml` | `Presentation.Client/Pages/Delivery/` y componentes compartidos | Catálogo, carrito, pedidos, pagos, direcciones y mutaciones por API; autorización y reglas existentes permanecen en servidor | Pendiente |
| Operación de tiendas | `Delivery/SellerStore.cshtml`, `SellerInventory.cshtml`, `Admin*.cshtml` | `Presentation.Client/Pages/Delivery/` | CRUD de tiendas/productos, inventario, usuarios, ubicaciones, pagos, órdenes y auditoría por API | Pendiente |
| Operaciones | `Features/Operations/Presentation/Views/Operations/*.cshtml` | `Presentation.Client/Pages/Operations/` | Incidencias, reservas, movimientos, auditoría, correos y estados por API | Pendiente |
| Comercio clásico | `Features/Commerce/Presentation/Views/{Store,Payment}/*.cshtml` | `Presentation.Client/Pages/Commerce/` | Carrito, órdenes y PayPal por API; secretos PayPal exclusivamente en servidor | Pendiente |
| Inventario clásico | `Features/Inventory/Presentation/Views/Inventory/index.cshtml` | `Presentation.Client/Pages/Inventory/` | Consultas y cambios de stock autorizados vía API | Pendiente |
| Inicio y panel | `Features/Home/Presentation/Views/Home/{Index,PanelAdministrador,Privacy}.cshtml` | `Presentation.Client/Pages/Home/` | Estadísticas, backups y datos por endpoints autenticados | Pendiente |
| Asistente | `Features/Home/Presentation/Views/Home/Assistant.cshtml`; `Presentation.Client/Pages/Assistant.razor` | Componente cliente existente | Si se reactiva, llamadas al servicio IA exclusivamente a través del backend | Ya existe ruta cliente `/app/asistente`; el módulo está deshabilitado |
| Compartidos y errores | Layouts, parciales, paginación, navegación y errores MVC/Razor Pages | Componentes/layouts bajo `Presentation.Client` | APIs comunes, autenticación y antiforgery en el host | Pendiente |

## Decisiones y cambios de esta etapa

- Se conservó el proyecto cliente `src/Core/Presentation.Client`.
- `src/Core/Presentation/Components/App.razor` asigna ahora `InteractiveWebAssembly` a `Routes` y `HeadOutlet`.
- Se retiró el registro de Interactive Server de `PresentationServiceCollectionExtensions.cs` y el endpoint de renderizado servidor de `WebApplicationExtensions.cs`. El shell Blazor configurado ya no requiere un circuito SignalR.
- Se mantienen temporalmente MVC y Razor Pages porque sus vistas y controladores todavía atienden las rutas existentes. Los enlaces que tienen que llegar a esas rutas no se migrarán a navegación cliente hasta que el destino Blazor/API correspondiente esté listo.
- El backend continúa siendo la única autoridad para credenciales, conexión a base de datos, reglas de negocio y autorización. El cliente debe usar endpoints autenticados con cookie HttpOnly y antiforgery para mutaciones; no guardar secretos en el bundle.
- Los contratos HTTP existentes se deben conservar. La auditoría inicial encontró endpoints JSON para contexto de sesión y opciones de registro en `WebApplicationExtensions.cs`; la mayoría de las acciones restantes son acciones MVC que devuelven vistas, redirecciones o parciales y requieren contratos API explícitos antes de conectarlas al cliente.

## Verificación y limitaciones

- Inspección estática de `.csproj`, `Program.cs`, `App.razor`, `Routes.razor`, `_Imports.razor`, registros de servicios, rutas del host y pantallas fuente.
- La migración no está completa: las rutas de negocio siguen en MVC/Razor Pages, navegación completa de documento sigue existiendo y esas pantallas aún no corren en WebAssembly.
- No se ejecutó una compilación en esta etapa porque hay procesos de `dotnet watch` y `dotnet run` activos que comparten los artefactos de salida del proyecto. Antes de declarar una etapa lista se debe compilar en salida aislada o coordinar una ventana sin esos procesos.
- Aún no hay pruebas funcionales de esta migración, medición del bundle WASM ni verificación de navegación en navegador. `InteractiveAuto` y `InteractiveServer` ya no aparecen en la configuración del shell después del cambio, pero falta verificar build/runtime.

## Próximas etapas

1. Migrar un módulo de extremo a extremo (componentes, DTOs/endpoints HTTP seguros, navegación y pruebas), conservando las URL y comparando las respuestas con el flujo MVC existente.
2. Repetir por módulo: Tiendas/compras, cuenta/identidad, comercio/pagos, operaciones, inventario y panel.
3. Migrar layouts, páginas de error, autenticación/OAuth y formularios restantes; eliminar MVC/Razor Pages solo después de reemplazar todas sus rutas y validar equivalencia funcional.
4. Aplicar lazy loading a ensamblados/rutas cuando reduzca el bundle inicial; medir descarga, navegación y funcionalidad en navegador; retirar referencias y configuración antigua únicamente al cerrar la matriz.
