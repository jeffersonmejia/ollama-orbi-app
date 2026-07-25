-- Orbi App - Datos breves para demostración

INSERT INTO delivery_store (delivery_store_id, name, category, address, is_active) VALUES
    (1, 'Orbi Market', 'Supermercado', 'Av. Principal 101', true),
    (2, 'Sabor Urbano', 'Restaurante', 'Calle Central 25', true)
ON CONFLICT (delivery_store_id) DO NOTHING;

INSERT INTO delivery_product (delivery_product_id, delivery_store_id, name, price, is_available) VALUES
    (1, 1, 'Canasta básica', 12.50, true),
    (2, 1, 'Bebidas surtidas', 6.25, true),
    (3, 2, 'Hamburguesa Orbi', 7.90, true),
    (4, 2, 'Menú ejecutivo', 5.50, true)
ON CONFLICT (delivery_product_id) DO NOTHING;

INSERT INTO delivery_order
    (delivery_order_id, delivery_store_id, customer_email, delivery_person_email, delivery_address, status, total)
VALUES
    (1, 2, 'ana.torres@orbi.com', 'carlos.perez@orbi.com', 'Av. Los Jardines 45', 'En preparación', 7.90)
ON CONFLICT (delivery_order_id) DO NOTHING;

INSERT INTO delivery_order_item
    (delivery_order_item_id, delivery_order_id, delivery_product_id, product_name, quantity, unit_price, subtotal)
VALUES
    (1, 1, 3, 'Hamburguesa Orbi', 1, 7.90, 7.90)
ON CONFLICT (delivery_order_item_id) DO NOTHING;

SELECT setval(pg_get_serial_sequence('delivery_store', 'delivery_store_id'), (SELECT MAX(delivery_store_id) FROM delivery_store));
SELECT setval(pg_get_serial_sequence('delivery_product', 'delivery_product_id'), (SELECT MAX(delivery_product_id) FROM delivery_product));
SELECT setval(pg_get_serial_sequence('delivery_order', 'delivery_order_id'), (SELECT MAX(delivery_order_id) FROM delivery_order));
SELECT setval(pg_get_serial_sequence('delivery_order_item', 'delivery_order_item_id'), (SELECT MAX(delivery_order_item_id) FROM delivery_order_item));

INSERT INTO user_profile
    (identity_user_id, first_name, last_name, cedula, address_line_1, address_line_2, province_code, city_code, reference)
SELECT "Id", 'Jefferson', 'Mejía', '0912345675', 'Av. Principal 101', 'Calle 9 de Octubre', '09', '0901', 'Frente al parque'
FROM "AspNetUsers" WHERE "NormalizedEmail" = 'JEFFERSON.MEJIA@ORBI.COM'
ON CONFLICT (identity_user_id) DO NOTHING;

INSERT INTO user_profile
    (identity_user_id, first_name, last_name, cedula, address_line_1, address_line_2, province_code, city_code, reference)
SELECT "Id", 'María', 'López', '1712345675', 'Av. Amazonas', 'Calle Naciones Unidas', '17', '1701', NULL
FROM "AspNetUsers" WHERE "NormalizedEmail" = 'MARIA.LOPEZ@ORBI.COM'
ON CONFLICT (identity_user_id) DO NOTHING;

INSERT INTO user_profile
    (identity_user_id, first_name, last_name, cedula, address_line_1, address_line_2, province_code, city_code, reference)
SELECT "Id", 'Carlos', 'Pérez', '0923456784', 'Av. Nicolás Lapentti', 'Calle Loja', '09', '0907', NULL
FROM "AspNetUsers" WHERE "NormalizedEmail" = 'CARLOS.PEREZ@ORBI.COM'
ON CONFLICT (identity_user_id) DO NOTHING;

INSERT INTO user_profile
    (identity_user_id, first_name, last_name, cedula, address_line_1, address_line_2, province_code, city_code, reference)
SELECT "Id", 'Ana', 'Torres', '0123456782', 'Av. de las Américas', 'Calle del Batán', '01', '0101', 'Casa esquinera'
FROM "AspNetUsers" WHERE "NormalizedEmail" = 'ANA.TORRES@ORBI.COM'
ON CONFLICT (identity_user_id) DO NOTHING;
