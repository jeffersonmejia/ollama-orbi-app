using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bogus;
using Npgsql;
using NpgsqlTypes;

namespace OrbiApp.DataGenerator;

internal sealed class OrbiDataGenerator(GeneratorOptions options)
{
    private readonly GenerationPlan _plan = GenerationPlan.Create(options.TotalRecords);
    private readonly Faker _faker = new(options.Locale);
    private readonly Stopwatch _clock = new();
    private NpgsqlConnection _connection = null!;
    private Location[] _locations = [];
    private Location[] _allLocations = [];
    private UserInfo[] _users = [];
    private ProductInfo[] _products = [];
    private OrderInfo[] _orders = [];

private static readonly Dictionary<string, (string[] Products, decimal Min, decimal Max)> Catalog = new(StringComparer.Ordinal)
    {
        ["Restaurantes"] = (
        [
            "Empanada de viento (3 pzs)", "Empanada de verde con queso (3 pzs)", "Empanada de carne (3 pzs)",
            "Llapingachos (2 pzs)", "Pan de yuca (6 pzs)", "Choclo asado", "Corviche frito",
            "Patacones con ají (3 pzs)", "Yuca frita con salsa", "Tamal de viento (2 pzs)",
            "Chifles con ají", "Salchipapas", "Canguil (palomitas)"
        ], 1.00m, 3.50m),
        ["Restaurantes_Comida"] = (
        [
            "Hamburguesa clásica", "Hamburguesa con queso doble", "Hot dog clásico",
            "Hot dog ecuatoriano con totopo", "Wrap de pollo", "Tacos de carne asada (2 pzs)",
            "Nuggets de pollo (6 pzs)", "Nuggets de pollo (10 pzs)", "Pollo crujiente (2 pzs)",
            "Pollo crujiente (4 pzs)"
        ], 3.00m, 7.50m),
        ["Restaurantes_Plato"] = (
        [
            "Hornado con llapingachos", "Fritada Quiteña", "Seco de chivo con arroz y plátano",
            "Seco de pollo con arroz", "Chuleta valluna", "Arroz con menestra y carne",
            "Arroz con menestra y pollo", "Locro de papas con aguacate", "Mote pillo", "Mote sucio",
            "Caldo de gallina", "Caldo de maní", "Pastel de choclo", "Hornado panzazo",
            "Fritada con arroz y aguacate"
        ], 4.50m, 9.00m),
        ["Restaurantes_Mariscos"] = (
        [
            "Encebollado de pescado", "Ceviche de camarón", "Ceviche de pescado",
            "Arroz marinero", "Arroz con camarón", "Arroz con chuleta de cerdo"
        ], 5.50m, 12.00m),
        ["Restaurantes_Pizza"] = (
        [
            "Pizza personal pepperoni", "Pizza personal hawaiana"
        ], 5.00m, 8.50m),
        ["Restaurantes_Bebida"] = (
        [
            "Gaseosa 500ml", "Jugo en agua 300ml", "Jugo de naranja natural",
            "Jugo de guayaba", "Agua mineral 600ml", "Jugo de maracuyá",
            "Jugo de limón con Hierbabuena", "Colada morada (vaso)", "Morocho (vaso)"
        ], 0.60m, 2.00m),
        ["Restaurantes_Dulce"] = (
        [
            "Helado de mandarina", "Sundae de chocolate", "Ensalada de frutas con helado",
            "Papas fritas medianas", "Papas fritas grandes"
        ], 1.50m, 4.50m),
        ["Farmacia"] = (
        [
            "Paracetamol 500mg (10 tabs)", "Ibuprofeno 400mg (10 tabs)", "Amoxicilina 500mg (12 caps)",
            "Omeprazol 20mg (14 caps)", "Losartán 50mg (10 tabs)", "Amlodipino 5mg (10 tabs)",
            "Metformina 850mg (10 tabs)", "Ciprofloxacino 500mg (10 tabs)", "Diclofenaco 100mg (10 tabs)",
            "Naproxeno 250mg (10 tabs)", "Loratadina 10mg (10 tabs)", "Cetirizina 10mg (10 tabs)",
            "Ranitidina 150mg (10 tabs)", "Buscapina 10mg (10 tabs)",
            "Aspirina 500mg (10 tabs)", "Novalgina 500mg (10 tabs)"
        ], 0.80m, 5.50m),
        ["Farmacia_Spray"] = (
        [
            "Salbutamol spray 100mcg", "Descongestionante nasal 15ml"
        ], 4.00m, 9.00m),
        ["Farmacia_Vitaminas"] = (
        [
            "Complex B (30 caps)", "Vitamina C 1000mg (20 tabs)", "Vitamina D3 1000UI (30 caps)",
            "Hierro + Ácido Fólico (30 tabs)", "Magnesio 400mg (30 caps)", "Zinc 50mg (30 tabs)",
            "Vitamina B12 1000mcg (30 tabs)", "Multivitamínico (30 tabs)", "Melatonina 3mg (30 tabs)",
            "Aceite de onagra 1000mg (30 caps)", "Probióticos (30 caps)", "Glucosamina 500mg (30 caps)",
            "Colágeno hidrolizado 500g"
        ], 2.50m, 12.00m),
        ["Farmacia_Topico"] = (
        [
            "Calcitriol 0.25mcg (20 caps)", "Albendazol 400mg (2 tabs)", "Ivermectina 6mg (2 tabs)",
            "Alcohol antiséptico 120ml", "Alcohol en gel 60ml", "Protector solar FPS50 50ml",
            "Suero oral sobres (6 und)", "Crema antiinflamatoria 30g", "Jarabe para tos 120ml",
            "Vendas elásticas 5cm", "Gel antibacterial 250ml"
        ], 1.00m, 8.00m),
        ["Farmacia_Curitas"] = (
        [
            "Curitas adhesivas (20 und)", "Jabón neutro 100g", "Algodón 50g",
            "Gasas estériles (5 und)", "Termómetro digital", "Tiritas impermeables (10 und)",
            "Mascarilla quirúrgica (10 und)", "Guantes de látex (10 pares)"
        ], 0.50m, 5.00m),
        ["Supermercado"] = (
        [
            "Arroz blanco Pillao 1kg", "Arroz integral 1kg", "Arroz premium 1kg",
            "Azúcar blanca 1kg", "Azúcar morena 1kg", "Azúcar gulupa 500g",
            "Sal refinada 1kg", "Sal rosada 1kg",
            "Aceite vegetal girasol 1L", "Aceite de palma 1L",
            "Harina de trigo 1kg", "Harina de maíz precocida 1kg", "Harina de plátano 500g",
            "Fideos espagueti 500g", "Fideos tallarines 500g", "Fideos maizena 200g",
            "Frijoles rojos 400g", "Frijoles negros 400g", "Frejol blanco 500g",
            "Lentejas 500g", "Garbanzos 400g", "Menestra de papas 500g",
            "Avena en hojuelas 500g", "Avena integral 500g", "Quinoa 500g",
            "Café molido 250g", "Café instantáneo 100g", "Café especial Loja 250g",
            "Té negro (25 bolsas)", "Té de manzanilla (25 bolsas)", "Té de cedrón (25 bolsas)",
            "Caldo de pollo (6 cubos)", "Caldo de verduras (6 cubos)",
            "Pimienta negra 50g", "Comino molido 50g", "Orégano seco 20g",
            "Laurel (10 hojas)", "Canela en polvo 50g", "Achiote molido 50g",
            "Vainilla esencia 60ml", "Levadura seca 10g", "Polvo de hornear 100g",
            "Bicarbonato de sodio 200g", "Maicena 200g",
            "Gelatina sin sabor 10g", "Coco rallado 100g",
            "Vinagre de manzana 500ml", "Salsa de soya 250ml", "Salsa de tomate 500ml",
            "Mayonesa 350g", "Kétchup 350g", "Mostaza 200g",
            "Aceitunas verdes 200g", "Pepinillos 350g",
            "Tomate triturado 400g", "Pasta de tomate 130g", "Salsa de ají 250ml"
        ], 0.50m, 5.00m),
        ["Supermercado_Lacteos"] = (
        [
            "Leche entera 1L", "Leche descremada 1L", "Leche de soya 1L",
            "Yogur natural 170g", "Yogur de frutas 170g", "Yogur bebible 300ml",
            "Queso fresco 250g", "Queso manaba 250g", "Queso de hoja 200g",
            "Mantequilla 200g", "Margarina 250g",
            "Leche condensada 397g", "Crema de leche 200ml"
        ], 0.80m, 4.50m),
        ["Supermercado_Huevos"] = (
        [
            "Huevos medianos (12 und)", "Huevos grandes (12 und)",
            "Pan de yuca precocido (300g)", "Pan de molde integral", "Pan de molde blanco"
        ], 1.00m, 3.50m),
        ["Supermercado_Conservas"] = (
        [
            "Atún en agua 170g", "Atún en aceite 170g", "Sardinas en tomate 155g"
        ], 1.20m, 2.80m),
        ["Supermercado_Snacks"] = (
        [
            "Chocolate en polvo 200g", "Cacao en polvo 100g",
            "Galletas de agua 200g", "Galletas de chocolate 150g", "Galletas María 200g",
            "Papas fritas saborizado 150g", "Chifles de plátano 100g", "Maní tostado 200g",
            "Pasas 100g", "Miel de abeja 350g", "Mermelada de fresa 350g",
            "Mermelada de guayaba 450g", "Salsa de maní 350g",
            "Nueces 100g", "Almendras 100g", "Maní sin cáscara 200g",
            "Semillas de girasol 150g", "Chía 200g", "Amaranto 300g",
            "Aceite de oliva 500ml"
        ], 1.00m, 6.50m),
        ["Supermercado_Frutas"] = (
        [
            "Plátano verde (kg)", "Plátano maduro (kg)", "Yuca fresca (kg)",
            "Papa criolla 1kg", "Papa pastusa 1kg", "Cebolla paiteña 1kg",
            "Tomate italiano 1kg", "Ají verde 500g", "Ají rojo 500g",
            "Cilantro manojo", "Perejil manojo", "Limón tahití 1kg",
            "Naranja valencia 1kg", "Mandarina 1kg", "Manzana roja 1kg",
            "Guayaba 1kg", "Mango Tommy 1kg", "Papaya 1kg", "Sandía pieza",
            "Melón pieza", "Fresa 500g", "Mora 500g", "Durazno 1kg"
        ], 0.30m, 4.00m)
    };

    public async Task RunAsync()
    {
        Randomizer.Seed = new Random(options.Seed);
        _clock.Start();

        await using var connection = new NpgsqlConnection(options.ConnectionString);
        _connection = connection;
        await connection.OpenAsync();

        if (options.OnlyProducts)
        {
            await ApplySchemaAsync();
            await GenerateProductsOnlyAsync();
            Console.WriteLine($"Productos generados en {_clock.Elapsed:c}.");
            return;
        }

        PrintPlan();
        await EnsureIdentitySchemaAsync();
        await ApplySchemaAsync();
        await PrepareDatabaseAsync();
        _locations = await ReadLocationsAsync();
        _allLocations = await ReadAllLocationsAsync();

        await GenerateStoresAsync();
        await GenerateProductsAsync();
        await GenerateProfilesAsync();
        await GenerateOrdersAndItemsAsync();
        await GeneratePaymentsAsync();
        await GenerateInventoryMovementsAsync();
        await GenerateAuditLogsAsync();
        await GenerateIncidentsAsync();
        await ResetSequencesAsync();
        await ValidateAsync();

        Console.WriteLine($"Generación completada: {_plan.Total:N0} registros en {_clock.Elapsed:c}.");
    }

    private void PrintPlan()
    {
        Console.WriteLine($"Bogus locale={options.Locale}, seed={options.Seed}, lote={options.BatchSize:N0}, fecha={options.ReferenceDate:O}");
        Console.WriteLine($"Plan: tiendas={_plan.Stores:N0}, productos={_plan.Products:N0}, perfiles={_plan.Profiles:N0}, pedidos={_plan.Orders:N0}, detalles={_plan.OrderItems:N0}, pagos={_plan.Payments:N0}, inventario={_plan.InventoryMovements:N0}, auditorías={_plan.AuditLogs:N0}, incidencias={_plan.Incidents:N0}; total={_plan.Total:N0}");
    }

    private async Task EnsureIdentitySchemaAsync()
    {
        await using var command = new NpgsqlCommand("SELECT to_regclass('public.\"AspNetUsers\"') IS NOT NULL", _connection);
        if (await command.ExecuteScalarAsync() is not true)
            throw new InvalidOperationException("No existe AspNetUsers. Ejecute primero las migraciones de SakilaApp.");
    }

    private async Task ApplySchemaAsync()
    {
        foreach (var name in new[] { "orbi-schema.sql", "orbi-locations.sql" })
        {
            var sql = await File.ReadAllTextAsync(Path.Combine(options.SchemaDirectory, name));
            await using var command = new NpgsqlCommand(sql, _connection) { CommandTimeout = 300 };
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task PrepareDatabaseAsync()
    {
        var existing = await BusinessCountAsync();
        if (existing > 0 && !options.Reset)
            throw new InvalidOperationException($"La base contiene {existing:N0} registros de negocio. Use --reset únicamente si desea reemplazarlos.");
        if (!options.Reset) return;

        const string sql = """
            TRUNCATE TABLE delivery_incident, audit_log, inventory_movement, payment,
                delivery_order_item, delivery_order, delivery_product, delivery_store, user_profile
                RESTART IDENTITY CASCADE;
            DELETE FROM "AspNetUsers" WHERE "Email" LIKE '%@datos.orbi.ec';
            """;
        await using var command = new NpgsqlCommand(sql, _connection) { CommandTimeout = 300 };
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("Tablas de negocio reiniciadas por solicitud explícita (--reset).");
    }

    private static readonly string[] TargetCityNames = new[]
    {
        "Guayaquil", "Distrito Metropolitano de Quito", "Santo Domingo",
        "Portoviejo", "Manta", "Ambato", "Cuenca", "Loja", "Riobamba", "Ibarra"
    };

    private async Task<Location[]> ReadLocationsAsync()
    {
        var result = new List<Location>();
        var inClause = string.Join(",", TargetCityNames.Select(n => $"'{n}'"));
        var sql = $"SELECT city_code, province_code, name FROM ecuador_city WHERE name IN ({inClause}) ORDER BY city_code";
        await using var command = new NpgsqlCommand(sql, _connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        if (result.Count == 0) throw new InvalidOperationException("El catálogo ecuador_city está vacío para las ciudades objetivo.");
        return result.ToArray();
    }

    private async Task<Location[]> ReadAllLocationsAsync()
    {
        var result = new List<Location>();
        await using var command = new NpgsqlCommand("SELECT city_code, province_code, name FROM ecuador_city ORDER BY city_code", _connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return result.ToArray();
    }

    private async Task GenerateStoresAsync()
    {
        var categories = new[] { "Restaurantes", "Farmacia", "Supermercado" };
        var suffixes = new[] { "Express", "Central", "La Esquina", "Del Valle", "Norte", "Sur", "Centro", "Los Ceibos" };
        await CopyBatchesAsync("tiendas", _plan.Stores,
            "COPY delivery_store (delivery_store_id, name, category, address, province_code, city_code, is_active) FROM STDIN (FORMAT BINARY)",
            (writer, index) =>
            {
                var category = _faker.PickRandom(categories);
                var location = _faker.PickRandom(_locations);
                writer.Write(index, NpgsqlDbType.Integer);
                writer.Write($"{StorePrefix(category)} {_faker.PickRandom(suffixes)}", NpgsqlDbType.Varchar);
                writer.Write(category, NpgsqlDbType.Varchar);
                writer.Write(Address(location.City), NpgsqlDbType.Varchar);
                writer.Write(location.ProvinceCode, NpgsqlDbType.Varchar);
                writer.Write(location.CityCode, NpgsqlDbType.Varchar);
                writer.Write(_faker.Random.Bool(0.96f), NpgsqlDbType.Boolean);
            });
    }

    private async Task GenerateProductsAsync()
    {
        var storeCategories = new string[_plan.Stores + 1];
        await using (var command = new NpgsqlCommand("SELECT delivery_store_id, category FROM delivery_store ORDER BY delivery_store_id", _connection))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) storeCategories[reader.GetInt32(0)] = reader.GetString(1);

        _products = new ProductInfo[_plan.Products + 1];
        await CopyBatchesAsync("productos", _plan.Products,
            "COPY delivery_product (delivery_product_id, delivery_store_id, name, price, is_available) FROM STDIN (FORMAT BINARY)",
            (writer, index) =>
            {
                var storeId = ((index - 1) % _plan.Stores) + 1;
                var category = storeCategories[storeId];
                var product = PickRandomProduct(category);
                var price = Money(_faker.Random.Decimal(product.Min, product.Max));
                _products[index] = new(storeId, product.Name, price);
                writer.Write(index, NpgsqlDbType.Integer);
                writer.Write(storeId, NpgsqlDbType.Integer);
                writer.Write(product.Name, NpgsqlDbType.Varchar);
                writer.Write(price, NpgsqlDbType.Numeric);
                writer.Write(_faker.Random.Bool(0.94f), NpgsqlDbType.Boolean);
            });
    }

    private async Task GenerateProductsOnlyAsync()
    {
        const string truncateSql = "TRUNCATE TABLE delivery_product RESTART IDENTITY CASCADE;";
        await using var truncateCmd = new NpgsqlCommand(truncateSql, _connection) { CommandTimeout = 300 };
        await truncateCmd.ExecuteNonQueryAsync();
        Console.WriteLine("Tabla delivery_product reiniciada. Tiendas, perfiles y pedidos conservados.");

        var storeIds = new List<int>();
        var storeCategoryMap = new Dictionary<int, string>();
        await using (var command = new NpgsqlCommand("SELECT delivery_store_id, category FROM delivery_store ORDER BY delivery_store_id", _connection))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                storeIds.Add(id);
                storeCategoryMap[id] = reader.GetString(1);
            }

        var storeCount = storeIds.Count;
        if (storeCount == 0)
            throw new InvalidOperationException("No hay tiendas en la base. Primero ejecute el generador completo.");

        var productCount = storeCount * 40;
        _products = new ProductInfo[productCount + 1];

        Console.WriteLine($"Generando {productCount:N0} productos para {storeCount:N0} tiendas...");

        await CopyBatchesAsync("productos", productCount,
            "COPY delivery_product (delivery_product_id, delivery_store_id, name, price, is_available) FROM STDIN (FORMAT BINARY)",
            (writer, index) =>
            {
                var storeId = storeIds[(index - 1) % storeCount];
                var category = storeCategoryMap[storeId];
                var product = PickRandomProduct(category);
                var price = Money(_faker.Random.Decimal(product.Min, product.Max));
                _products[index] = new(storeId, product.Name, price);
                writer.Write(index, NpgsqlDbType.Integer);
                writer.Write(storeId, NpgsqlDbType.Integer);
                writer.Write(product.Name, NpgsqlDbType.Varchar);
                writer.Write(price, NpgsqlDbType.Numeric);
                writer.Write(_faker.Random.Bool(0.94f), NpgsqlDbType.Boolean);
            });

        await new NpgsqlCommand($"""
            SELECT setval(pg_get_serial_sequence('delivery_product','delivery_product_id'),
                COALESCE(MAX(delivery_product_id), 1), MAX(delivery_product_id) IS NOT NULL) FROM delivery_product;
            """, _connection).ExecuteNonQueryAsync();

        Console.WriteLine($"Generación de productos completada: {productCount:N0} registros.");
    }

    private async Task GenerateProfilesAsync()
    {
        _users = new UserInfo[_plan.Profiles];
        for (var start = 0; start < _plan.Profiles; start += options.BatchSize)
        {
            var end = Math.Min(start + options.BatchSize, _plan.Profiles);
            await using var transaction = await _connection.BeginTransactionAsync();
            await using (var users = await _connection.BeginBinaryImportAsync("""
                COPY "AspNetUsers" ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnd", "LockoutEnabled", "AccessFailedCount") FROM STDIN (FORMAT BINARY)
                """))
            {
                for (var i = start; i < end; i++)
                {
                    var person = _faker.Person;
                    var email = UniqueEmail(person.FirstName, person.LastName, i);
                    var id = DeterministicGuid(options.Seed, i).ToString();
                    var cedula = EcuadorianId(i, options.Seed, out var provinceCode);
                    var candidates = _locations.Where(x => x.ProvinceCode == provinceCode).ToArray();
                    if (candidates.Length == 0)
                        candidates = _allLocations.Where(x => x.ProvinceCode == provinceCode).ToArray();
                    if (candidates.Length == 0)
                        candidates = _allLocations;
                    var location = _faker.PickRandom(candidates);
                    _users[i] = new(id, email, person.FirstName, person.LastName, cedula, location);

                    users.StartRow();
                    users.Write(id, NpgsqlDbType.Text);
                    users.Write(email, NpgsqlDbType.Varchar);
                    users.Write(email.ToUpperInvariant(), NpgsqlDbType.Varchar);
                    users.Write(email, NpgsqlDbType.Varchar);
                    users.Write(email.ToUpperInvariant(), NpgsqlDbType.Varchar);
                    users.Write(true, NpgsqlDbType.Boolean);
                    users.WriteNull();
                    users.Write(DeterministicGuid(options.Seed + 1, i).ToString("N"), NpgsqlDbType.Text);
                    users.Write(DeterministicGuid(options.Seed + 2, i).ToString("N"), NpgsqlDbType.Text);
                    users.Write($"+5939{_faker.Random.Number(10_000_000, 99_999_999)}", NpgsqlDbType.Text);
                    users.Write(false, NpgsqlDbType.Boolean);
                    users.Write(false, NpgsqlDbType.Boolean);
                    users.WriteNull();
                    users.Write(true, NpgsqlDbType.Boolean);
                    users.Write(0, NpgsqlDbType.Integer);
                }
                await users.CompleteAsync();
            }

            await using (var profiles = await _connection.BeginBinaryImportAsync("COPY user_profile (identity_user_id, first_name, last_name, cedula, address_line_1, address_line_2, province_code, city_code, reference, created_at) FROM STDIN (FORMAT BINARY)"))
            {
                for (var i = start; i < end; i++)
                {
                    var user = _users[i];
                    profiles.StartRow();
                    profiles.Write(user.Id, NpgsqlDbType.Text);
                    profiles.Write(Trim(user.FirstName, 80), NpgsqlDbType.Varchar);
                    profiles.Write(Trim(user.LastName, 80), NpgsqlDbType.Varchar);
                    profiles.Write(user.Cedula, NpgsqlDbType.Varchar);
                    profiles.Write(Trim(Address(user.Location.City), 160), NpgsqlDbType.Varchar);
                    profiles.Write(Trim($"Sector {_faker.Address.StreetName()}", 160), NpgsqlDbType.Varchar);
                    profiles.Write(user.Location.ProvinceCode, NpgsqlDbType.Varchar);
                    profiles.Write(user.Location.CityCode, NpgsqlDbType.Varchar);
                    profiles.Write(Trim(_faker.PickRandom("Frente al parque", "Junto a la farmacia", "Casa de portón negro", "Diagonal a la iglesia", "Edificio con fachada blanca"), 240), NpgsqlDbType.Varchar);
                    profiles.Write(RandomDate(730), NpgsqlDbType.TimestampTz);
                }
                await profiles.CompleteAsync();
            }
            await transaction.CommitAsync();
            Progress("perfiles", end, _plan.Profiles);
        }
    }

    private async Task GenerateOrdersAndItemsAsync()
    {
        _orders = new OrderInfo[_plan.Orders + 1];
        var itemId = 0;
        var itemsPerOrder = _plan.OrderItems / _plan.Orders;
        var ordersWithExtraItem = _plan.OrderItems % _plan.Orders;

        for (var start = 1; start <= _plan.Orders; start += options.BatchSize)
        {
            var end = Math.Min(start + options.BatchSize - 1, _plan.Orders);
            var orderRows = new List<OrderRow>(end - start + 1);
            var itemRows = new List<ItemRow>((end - start + 1) * 2);
            for (var orderId = start; orderId <= end; orderId++)
            {
                var storeId = _faker.Random.Int(1, _plan.Stores);
                var created = RandomDate(730);
                var status = OrderStatus(created);
                var itemCount = itemsPerOrder + (orderId <= ordersWithExtraItem ? 1 : 0);
                decimal total = 0;
                for (var j = 0; j < itemCount; j++)
                {
                    var productId = ProductForStore(storeId);
                    var product = _products[productId];
                    var quantity = _faker.Random.Int(1, 5);
                    var subtotal = product.Price * quantity;
                    total += subtotal;
                    itemRows.Add(new(++itemId, orderId, productId, product.Name, quantity, product.Price, subtotal));
                }
                var customer = _users[_faker.Random.Int(0, Math.Max(0, _users.Length * 9 / 10 - 1))];
                var driver = status is "En camino" or "Entregado" || (status == "En preparación" && _faker.Random.Bool())
                    ? _users[_faker.Random.Int(Math.Max(0, _users.Length * 9 / 10), _users.Length - 1)].Email : null;
                var address = $"{Address(customer.Location.City)}, {customer.Location.City}";
                orderRows.Add(new(orderId, storeId, customer.Email, driver, Trim(address, 180), status, total, created));
                _orders[orderId] = new(total, created);
            }

            await using var transaction = await _connection.BeginTransactionAsync();
            await using (var writer = await _connection.BeginBinaryImportAsync("COPY delivery_order (delivery_order_id, delivery_store_id, customer_email, delivery_person_email, delivery_address, status, total, created_at) FROM STDIN (FORMAT BINARY)"))
            {
                foreach (var row in orderRows)
                {
                    writer.StartRow(); writer.Write(row.Id, NpgsqlDbType.Integer); writer.Write(row.StoreId, NpgsqlDbType.Integer);
                    writer.Write(row.Customer, NpgsqlDbType.Varchar); WriteNullable(writer, row.Driver, NpgsqlDbType.Varchar);
                    writer.Write(row.Address, NpgsqlDbType.Varchar); writer.Write(row.Status, NpgsqlDbType.Varchar);
                    writer.Write(row.Total, NpgsqlDbType.Numeric); writer.Write(row.Created, NpgsqlDbType.TimestampTz);
                }
                await writer.CompleteAsync();
            }
            await using (var writer = await _connection.BeginBinaryImportAsync("COPY delivery_order_item (delivery_order_item_id, delivery_order_id, delivery_product_id, product_name, quantity, unit_price, subtotal) FROM STDIN (FORMAT BINARY)"))
            {
                foreach (var row in itemRows)
                {
                    writer.StartRow(); writer.Write(row.Id, NpgsqlDbType.Integer); writer.Write(row.OrderId, NpgsqlDbType.Integer);
                    writer.Write(row.ProductId, NpgsqlDbType.Integer); writer.Write(row.Name, NpgsqlDbType.Varchar);
                    writer.Write(row.Quantity, NpgsqlDbType.Integer); writer.Write(row.Price, NpgsqlDbType.Numeric); writer.Write(row.Subtotal, NpgsqlDbType.Numeric);
                }
                await writer.CompleteAsync();
            }
            await transaction.CommitAsync();
            Progress("pedidos/detalles", end, _plan.Orders);
        }
    }

    private Task GeneratePaymentsAsync() => CopyBatchesAsync("pagos", _plan.Payments,
        "COPY payment (payment_id, delivery_order_id, external_id, provider, status, amount, created_at, confirmed_at) FROM STDIN (FORMAT BINARY)",
        (writer, index) =>
        {
            var orderId = ((index * 7919 - 1) % _plan.Orders) + 1;
            var order = _orders[orderId];
            var provider = _faker.PickRandom("PayPhone", "PayPal");
            var status = _faker.PickRandom("Aprobado", "Aprobado", "Aprobado", "Pendiente", "Rechazado", "Reembolsado");
            writer.Write((long)index, NpgsqlDbType.Bigint); writer.Write(orderId, NpgsqlDbType.Integer);
            writer.Write($"{(provider == "PayPhone" ? "PF" : "PP")}-{options.Seed:X8}-{index:X10}", NpgsqlDbType.Varchar);
            writer.Write(provider, NpgsqlDbType.Varchar); writer.Write(status, NpgsqlDbType.Varchar);
            writer.Write(order.Total, NpgsqlDbType.Numeric); writer.Write(order.Created.AddMinutes(_faker.Random.Int(1, 90)), NpgsqlDbType.TimestampTz);
            if (status == "Aprobado") writer.Write(order.Created.AddMinutes(_faker.Random.Int(2, 120)), NpgsqlDbType.TimestampTz); else writer.WriteNull();
        });

    private Task GenerateInventoryMovementsAsync() => CopyBatchesAsync("movimientos", _plan.InventoryMovements,
        "COPY inventory_movement (inventory_movement_id, delivery_product_id, delivery_order_id, performed_by_user_id, movement_type, quantity_delta, unit_cost, metadata, created_at) FROM STDIN (FORMAT BINARY)",
        (writer, index) =>
        {
            var productId = _faker.Random.Int(1, _plan.Products);
            var type = _faker.PickRandom("Entrada", "Salida", "Ajuste", "Reserva", "Liberación");
            var quantity = type is "Entrada" or "Liberación" ? _faker.Random.Int(1, 40) : -_faker.Random.Int(1, 8);
            writer.Write((long)index, NpgsqlDbType.Bigint); writer.Write(productId, NpgsqlDbType.Integer);
            WriteNullable(writer, _faker.Random.Bool(0.75f) ? _faker.Random.Int(1, _plan.Orders) : null, NpgsqlDbType.Integer);
            WriteNullable(writer, _users[_faker.Random.Int(0, _users.Length - 1)].Id, NpgsqlDbType.Text);
            writer.Write(type, NpgsqlDbType.Varchar); writer.Write(quantity, NpgsqlDbType.Integer);
            writer.Write(_products[productId].Price, NpgsqlDbType.Numeric);
            writer.Write(JsonSerializer.Serialize(new { origen = "generador-bogus", lote = (index - 1) / options.BatchSize + 1 }), NpgsqlDbType.Jsonb);
            writer.Write(RandomDate(730), NpgsqlDbType.TimestampTz);
        });

    private Task GenerateAuditLogsAsync() => CopyBatchesAsync("auditorías", _plan.AuditLogs,
        "COPY audit_log (audit_log_id, user_id, action, entity_type, entity_id, old_values, new_values, ip_address, user_agent, correlation_id, created_at) FROM STDIN (FORMAT BINARY)",
        (writer, index) =>
        {
            var entity = _faker.PickRandom("DeliveryOrder", "DeliveryProduct", "UserProfile", "Payment");
            writer.Write((long)index, NpgsqlDbType.Bigint);
            WriteNullable(writer, _users[_faker.Random.Int(0, _users.Length - 1)].Id, NpgsqlDbType.Text);
            writer.Write(_faker.PickRandom("Crear", "Actualizar", "Consultar", "CambiarEstado"), NpgsqlDbType.Varchar);
            writer.Write(entity, NpgsqlDbType.Varchar); writer.Write(_faker.Random.Int(1, _plan.Orders).ToString(CultureInfo.InvariantCulture), NpgsqlDbType.Varchar);
            writer.WriteNull(); writer.Write("{\"origen\":\"generador-bogus\"}", NpgsqlDbType.Jsonb);
            writer.Write(_faker.Internet.Ip(), NpgsqlDbType.Varchar); writer.Write(Trim(_faker.Internet.UserAgent(), 512), NpgsqlDbType.Varchar);
            writer.Write(DeterministicGuid(options.Seed + 3, index).ToString("N"), NpgsqlDbType.Varchar);
            writer.Write(RandomDate(730), NpgsqlDbType.TimestampTz);
        });

    private Task GenerateIncidentsAsync() => CopyBatchesAsync("incidencias", _plan.Incidents,
        "COPY delivery_incident (delivery_incident_id, delivery_order_id, reported_by_user_id, incident_type, severity, description, status, details, created_at, resolved_at) FROM STDIN (FORMAT BINARY)",
        (writer, index) =>
        {
            var orderId = _faker.Random.Int(1, _plan.Orders);
            var created = _orders[orderId].Created.AddHours(_faker.Random.Int(1, 48));
            var status = _faker.PickRandom("Abierto", "En revisión", "Resuelto", "Cerrado");
            writer.Write((long)index, NpgsqlDbType.Bigint); writer.Write(orderId, NpgsqlDbType.Integer);
            WriteNullable(writer, _users[_faker.Random.Int(0, _users.Length - 1)].Id, NpgsqlDbType.Text);
            writer.Write(_faker.PickRandom("Demora en entrega", "Producto incompleto", "Dirección no localizada", "Producto dañado", "Cobro incorrecto"), NpgsqlDbType.Varchar);
            writer.Write(_faker.PickRandom("Baja", "Media", "Media", "Alta", "Crítica"), NpgsqlDbType.Varchar);
            writer.Write(_faker.PickRandom("El cliente reportó una novedad durante la entrega.", "El repartidor solicitó apoyo para completar la entrega.", "Se requiere verificar el contenido y contactar al cliente."), NpgsqlDbType.Text);
            writer.Write(status, NpgsqlDbType.Varchar); writer.Write("{\"canal\":\"soporte\"}", NpgsqlDbType.Jsonb);
            writer.Write(created, NpgsqlDbType.TimestampTz);
            if (status is "Resuelto" or "Cerrado") writer.Write(created.AddHours(_faker.Random.Int(1, 72)), NpgsqlDbType.TimestampTz); else writer.WriteNull();
        });

    private async Task CopyBatchesAsync(string label, int count, string copyCommand, Action<NpgsqlBinaryImporter, int> write)
    {
        for (var start = 1; start <= count; start += options.BatchSize)
        {
            var end = Math.Min(start + options.BatchSize - 1, count);
            await using var transaction = await _connection.BeginTransactionAsync();
            await using (var writer = await _connection.BeginBinaryImportAsync(copyCommand))
            {
                for (var index = start; index <= end; index++) { writer.StartRow(); write(writer, index); }
                await writer.CompleteAsync();
            }
            await transaction.CommitAsync();
            Progress(label, end, count);
        }
    }

    private async Task ResetSequencesAsync()
    {
        const string sql = """
            SELECT setval(pg_get_serial_sequence('delivery_store','delivery_store_id'), COALESCE(MAX(delivery_store_id), 1), MAX(delivery_store_id) IS NOT NULL) FROM delivery_store;
            SELECT setval(pg_get_serial_sequence('delivery_product','delivery_product_id'), COALESCE(MAX(delivery_product_id), 1), MAX(delivery_product_id) IS NOT NULL) FROM delivery_product;
            SELECT setval(pg_get_serial_sequence('delivery_order','delivery_order_id'), COALESCE(MAX(delivery_order_id), 1), MAX(delivery_order_id) IS NOT NULL) FROM delivery_order;
            SELECT setval(pg_get_serial_sequence('delivery_order_item','delivery_order_item_id'), COALESCE(MAX(delivery_order_item_id), 1), MAX(delivery_order_item_id) IS NOT NULL) FROM delivery_order_item;
            SELECT setval(pg_get_serial_sequence('payment','payment_id'), COALESCE(MAX(payment_id), 1), MAX(payment_id) IS NOT NULL) FROM payment;
            """;
        await using var command = new NpgsqlCommand(sql, _connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ValidateAsync()
    {
        var total = await BusinessCountAsync();
        var checks = new Dictionary<string, string>
        {
            ["correos duplicados"] = "SELECT COUNT(*) - COUNT(DISTINCT \"Email\") FROM \"AspNetUsers\" WHERE \"Email\" LIKE '%@datos.orbi.ec'",
            ["cédulas duplicadas"] = "SELECT COUNT(*) - COUNT(DISTINCT cedula) FROM user_profile",
            ["cédulas inválidas"] = """
                SELECT COUNT(*) FROM user_profile WHERE
                    substring(cedula,1,2)::int NOT BETWEEN 1 AND 24 OR substring(cedula,3,1)::int NOT BETWEEN 0 AND 5 OR
                    substring(cedula,10,1)::int <> (10 - (
                        (CASE WHEN substring(cedula,1,1)::int*2>9 THEN substring(cedula,1,1)::int*2-9 ELSE substring(cedula,1,1)::int*2 END) +
                        substring(cedula,2,1)::int +
                        (CASE WHEN substring(cedula,3,1)::int*2>9 THEN substring(cedula,3,1)::int*2-9 ELSE substring(cedula,3,1)::int*2 END) +
                        substring(cedula,4,1)::int +
                        (CASE WHEN substring(cedula,5,1)::int*2>9 THEN substring(cedula,5,1)::int*2-9 ELSE substring(cedula,5,1)::int*2 END) +
                        substring(cedula,6,1)::int +
                        (CASE WHEN substring(cedula,7,1)::int*2>9 THEN substring(cedula,7,1)::int*2-9 ELSE substring(cedula,7,1)::int*2 END) +
                        substring(cedula,8,1)::int +
                        (CASE WHEN substring(cedula,9,1)::int*2>9 THEN substring(cedula,9,1)::int*2-9 ELSE substring(cedula,9,1)::int*2 END)
                    ) % 10) % 10
                """,
            ["ciudad/provincia inválida"] = "SELECT COUNT(*) FROM user_profile u JOIN ecuador_city c ON c.city_code=u.city_code WHERE c.province_code<>u.province_code",
            ["producto de otra tienda"] = "SELECT COUNT(*) FROM delivery_order_item i JOIN delivery_order o USING(delivery_order_id) JOIN delivery_product p USING(delivery_product_id) WHERE p.delivery_store_id<>o.delivery_store_id",
            ["subtotal incorrecto"] = "SELECT COUNT(*) FROM delivery_order_item WHERE subtotal<>quantity*unit_price",
            ["total incorrecto"] = "SELECT COUNT(*) FROM delivery_order o JOIN (SELECT delivery_order_id,SUM(subtotal) total FROM delivery_order_item GROUP BY delivery_order_id) i USING(delivery_order_id) WHERE o.total<>i.total",
            ["pago incorrecto"] = "SELECT COUNT(*) FROM payment p JOIN delivery_order o USING(delivery_order_id) WHERE p.amount<>o.total"
        };
        var failures = new List<string>();
        if (total != options.TotalRecords) failures.Add($"total esperado={options.TotalRecords:N0}, obtenido={total:N0}");
        foreach (var check in checks)
        {
            await using var command = new NpgsqlCommand(check.Value, _connection) { CommandTimeout = 300 };
            var invalid = Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            Console.WriteLine($"Validación {check.Key}: {invalid:N0}");
            if (invalid != 0) failures.Add($"{check.Key}={invalid:N0}");
        }
        if (failures.Count > 0) throw new InvalidOperationException("Falló la validación: " + string.Join(", ", failures));
        Console.WriteLine($"Validación total exacto: {total:N0}");
    }

    private async Task<long> BusinessCountAsync()
    {
        const string sql = """
            SELECT (SELECT COUNT(*) FROM delivery_store) + (SELECT COUNT(*) FROM delivery_product) +
                   (SELECT COUNT(*) FROM user_profile) + (SELECT COUNT(*) FROM delivery_order) +
                   (SELECT COUNT(*) FROM delivery_order_item) + (SELECT COUNT(*) FROM payment) +
                   (SELECT COUNT(*) FROM inventory_movement) + (SELECT COUNT(*) FROM audit_log) +
                   (SELECT COUNT(*) FROM delivery_incident)
            """;
        await using var command = new NpgsqlCommand(sql, _connection) { CommandTimeout = 300 };
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private int ProductForStore(int storeId)
    {
        var count = ((_plan.Products - storeId) / _plan.Stores) + 1;
        return storeId + _faker.Random.Int(0, count - 1) * _plan.Stores;
    }

    private string OrderStatus(DateTimeOffset created)
    {
        var age = (options.ReferenceDate - created).TotalDays;
        if (age < 7) return _faker.PickRandom("Pendiente", "En preparación", "En preparación", "En camino", "Entregado");
        return _faker.PickRandom("Entregado", "Entregado", "Entregado", "Entregado", "Cancelado", "En camino");
    }

    private DateTimeOffset RandomDate(int daysBack) => options.ReferenceDate.AddDays(-_faker.Random.Int(0, daysBack)).AddMinutes(-_faker.Random.Int(0, 1439));
    private string Address(string city) => Trim($"{_faker.PickRandom("Av.", "Calle", "Cdla.", "Barrio", "Coop.")} {_faker.Address.StreetName()} {_faker.Random.Int(1, 999)}, {city}", 180);
    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string StorePrefix(string category) => category switch { "Restaurantes" => "Restaurante", "Farmacia" => "Farmacia", "Supermercado" => "Supermercado", _ => category };

    private (string Name, decimal Min, decimal Max) PickRandomProduct(string storeCategory)
    {
        var candidates = Catalog.Where(kvp => kvp.Key == storeCategory || kvp.Key.StartsWith(storeCategory + "_")).ToList();
        var sub = _faker.PickRandom(candidates);
        var name = _faker.PickRandom(sub.Value.Products);
        return (name, sub.Value.Min, sub.Value.Max);
    }
    private static string Trim(string value, int max) => value.Length <= max ? value : value[..max];

    private static string UniqueEmail(string first, string last, int index)
    {
        static string Slug(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);
            return new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && (char.IsLetterOrDigit(c) || c == '.')).ToArray()).ToLowerInvariant();
        }
        var domains = new[] { "gmail.com", "outlook.com", "hotmail.com" };
        var domain = domains[index % domains.Length];
        return $"{Slug(first)}.{Slug(last)}{index + 1}@{domain}";
    }

    private static string EcuadorianId(int index, int seed, out string provinceCode)
    {
        const int space = 24 * 6 * 1_000_000;
        var value = (int)(((long)index + Math.Abs((long)seed) * 1_000_003) % space);
        var province = value / 6_000_000 + 1;
        var third = value / 1_000_000 % 6;
        var suffix = value % 1_000_000;
        var nine = $"{province:00}{third}{suffix:000000}";
        var sum = 0;
        for (var i = 0; i < nine.Length; i++)
        {
            var digit = nine[i] - '0';
            if (i % 2 == 0 && (digit *= 2) > 9) digit -= 9;
            sum += digit;
        }
        provinceCode = province.ToString("00", CultureInfo.InvariantCulture);
        return nine + ((10 - sum % 10) % 10).ToString(CultureInfo.InvariantCulture);
    }

    private static Guid DeterministicGuid(int seed, int index) => new(MD5.HashData(Encoding.UTF8.GetBytes($"orbi:{seed}:{index}")));
    private static void WriteNullable(NpgsqlBinaryImporter writer, string? value, NpgsqlDbType type)
    {
        if (value is null) writer.WriteNull(); else writer.Write(value, type);
    }

    private static void WriteNullable(NpgsqlBinaryImporter writer, int? value, NpgsqlDbType type)
    {
        if (value.HasValue) writer.Write(value.Value, type); else writer.WriteNull();
    }
    private void Progress(string label, int done, int total) => Console.WriteLine($"[{_clock.Elapsed:hh\\:mm\\:ss}] {label}: {done:N0}/{total:N0} ({done * 100.0 / total:F1} %)");

    private sealed record Location(string CityCode, string ProvinceCode, string City);
    private sealed record UserInfo(string Id, string Email, string FirstName, string LastName, string Cedula, Location Location);
    private sealed record ProductInfo(int StoreId, string Name, decimal Price);
    private sealed record OrderInfo(decimal Total, DateTimeOffset Created);
    private sealed record OrderRow(int Id, int StoreId, string Customer, string? Driver, string Address, string Status, decimal Total, DateTimeOffset Created);
    private sealed record ItemRow(int Id, int OrderId, int ProductId, string Name, int Quantity, decimal Price, decimal Subtotal);
}
