-- Datos demo mensuales para el panel administrativo de Orbi.
--
-- Fuentes de referencia:
-- - INEC: Ecuador tiene 24 provincias y usa códigos provinciales de dos dígitos.
--   https://www.ecuadorencifras.gob.ec/documentos/web-inec/Multiproposito/2019/201912_Marco_Maestro_de_Muestreo_Multiproposito.pdf
-- - INEC CIIU G4721.05: venta minorista especializada de panadería y repostería.
--   https://aplicaciones2.ecuadorencifras.gob.ec/SIN/resul_correspondencia.php?ciiu=12&id=G4721.05
-- - INEC CIIU I5610.01: restaurantes, cafeterías y comida para llevar.
--   https://aplicaciones2.ecuadorencifras.gob.ec/SIN/resul_correspondencia.php?ciiu=12&id=I5610.01
--
-- La carga no representa transacciones comerciales reales. Genera datos demo realistas,
-- pequeños e idempotentes usando usuarios, ciudades, tiendas, productos y precios de Orbi.

BEGIN;
SET LOCAL TIME ZONE 'America/Guayaquil';

DO $seed$
DECLARE
    province_row record;
    city_row record;
    profile_row record;
    product_row record;
    profiles_needed integer;
    sale_number integer;
    selected_store_id integer;
    selected_order_id integer;
    customer_email text;
    customer_address text;
    external_payment_id text;
    month_key text := to_char(current_date, 'YYYYMM');
    order_created_at timestamptz;
    order_total numeric(10,2);
    order_status text;
BEGIN
    -- Dos categorías faltantes, reconocidas por la clasificación económica del INEC.
    IF NOT EXISTS (SELECT 1 FROM delivery_store WHERE name = 'Panadería local de Azogues') THEN
        INSERT INTO delivery_store (name, category, address, province_code, city_code, is_active, created_at)
        VALUES ('Panadería local de Azogues', 'Panadería', 'Centro de Azogues', '03', '0301', true, now())
        RETURNING delivery_store_id INTO selected_store_id;

        INSERT INTO delivery_product
            (delivery_store_id, name, price, unit_cost, stock, is_available, created_at, updated_at)
        SELECT selected_store_id, source.name, source.price, source.unit_cost, 40, true, now(), now()
        FROM (
            SELECT DISTINCT ON (product.name)
                product.name, product.price, product.unit_cost
            FROM delivery_product product
            JOIN delivery_store store ON store.delivery_store_id = product.delivery_store_id
            WHERE store.category = 'Restaurantes'
              AND product.name IN (
                  'Pan de yuca (6 pzs)',
                  'Empanada de viento (3 pzs)',
                  'Empanada de verde con queso (3 pzs)',
                  'Pastel de choclo',
                  'Tamal de viento (2 pzs)')
            ORDER BY product.name, product.delivery_product_id
        ) source;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM delivery_store WHERE name = 'Cafetería local de Tulcán') THEN
        INSERT INTO delivery_store (name, category, address, province_code, city_code, is_active, created_at)
        VALUES ('Cafetería local de Tulcán', 'Cafetería', 'Centro de Tulcán', '04', '0401', true, now())
        RETURNING delivery_store_id INTO selected_store_id;

        INSERT INTO delivery_product
            (delivery_store_id, name, price, unit_cost, stock, is_available, created_at, updated_at)
        SELECT selected_store_id, source.name, source.price, source.unit_cost, 40, true, now(), now()
        FROM (
            SELECT DISTINCT ON (product.name)
                product.name, product.price, product.unit_cost
            FROM delivery_product product
            JOIN delivery_store store ON store.delivery_store_id = product.delivery_store_id
            WHERE store.category = 'Restaurantes'
              AND product.name IN (
                  'Morocho (vaso)',
                  'Colada morada (vaso)',
                  'Jugo de naranja natural',
                  'Pan de yuca (6 pzs)',
                  'Empanada de viento (3 pzs)')
            ORDER BY product.name, product.delivery_product_id
        ) source;
    END IF;

    FOR province_row IN
        SELECT province_code, name
        FROM ecuador_province
        ORDER BY province_code
    LOOP
        SELECT city_code, name
        INTO city_row
        FROM ecuador_city
        WHERE province_code = province_row.province_code
        ORDER BY CASE WHEN city_code = province_row.province_code || '01' THEN 0 ELSE 1 END, city_code
        LIMIT 1;

        IF city_row.city_code IS NULL THEN
            RAISE EXCEPTION 'La provincia % no tiene ciudades configuradas.', province_row.name;
        END IF;

        -- Las provincias sin comercio reciben una tienda genérica existente con todo su
        -- catálogo. Se toma de provincias que tienen más de 180 tiendas, sin propietario.
        IF NOT EXISTS (
            SELECT 1
            FROM delivery_store store
            WHERE store.province_code = province_row.province_code
              AND store.is_active
              AND EXISTS (
                  SELECT 1 FROM delivery_product product
                  WHERE product.delivery_store_id = store.delivery_store_id
                    AND product.is_available)
              AND NOT EXISTS (
                  SELECT 1
                  FROM delivery_order tagged_order
                  JOIN payment tagged_payment
                    ON tagged_payment.delivery_order_id = tagged_order.delivery_order_id
                  WHERE tagged_order.delivery_store_id = store.delivery_store_id
                    AND tagged_payment.external_id LIKE 'ORBI-EC-' || month_key || '-%')
        ) THEN
            SELECT store.delivery_store_id
            INTO selected_store_id
            FROM delivery_store store
            WHERE store.owner_user_id IS NULL
              AND store.is_active
              AND store.province_code IN ('01','06','09','10','11','13','17','18','23')
              AND EXISTS (
                  SELECT 1 FROM delivery_product product
                  WHERE product.delivery_store_id = store.delivery_store_id
                    AND product.is_available)
            ORDER BY md5(store.delivery_store_id::text || province_row.province_code)
            LIMIT 1;

            IF selected_store_id IS NULL THEN
                RAISE EXCEPTION 'No hay una tienda disponible para cubrir %.', province_row.name;
            END IF;

            UPDATE delivery_store
            SET province_code = province_row.province_code,
                city_code = city_row.city_code,
                address = left(
                    regexp_replace(address, ', [^,]+$', '') || ', ' || city_row.name,
                    180)
            WHERE delivery_store_id = selected_store_id;
        END IF;

        -- Completa diez clientes locales por provincia usando perfiles demo existentes.
        SELECT greatest(0, 10 - count(*))::integer
        INTO profiles_needed
        FROM user_profile
        WHERE province_code = province_row.province_code;

        FOR profile_row IN
            SELECT profile.identity_user_id
            FROM user_profile profile
            JOIN "AspNetUsers" identity_user ON identity_user."Id" = profile.identity_user_id
            WHERE profile.province_code = '02'
              AND NOT EXISTS (
                  SELECT 1 FROM "AspNetUserRoles" user_role
                  WHERE user_role."UserId" = identity_user."Id")
            ORDER BY md5(profile.identity_user_id || province_row.province_code)
            LIMIT profiles_needed
        LOOP
            UPDATE user_profile
            SET province_code = province_row.province_code,
                city_code = city_row.city_code,
                address_line_1 = left(
                    regexp_replace(address_line_1, ', [^,]+$', '') || ', ' || city_row.name,
                    160)
            WHERE identity_user_id = profile_row.identity_user_id;

            UPDATE user_address
            SET province_code = province_row.province_code,
                city_code = city_row.city_code,
                address_line_1 = left(
                    regexp_replace(address_line_1, ', [^,]+$', '') || ', ' || city_row.name,
                    160),
                updated_at = now()
            WHERE identity_user_id = profile_row.identity_user_id;
        END LOOP;

        FOR sale_number IN 1..10 LOOP
            external_payment_id := format(
                'ORBI-EC-%s-%s-%s', month_key, province_row.province_code, lpad(sale_number::text, 2, '0'));

            IF EXISTS (SELECT 1 FROM payment WHERE external_id = external_payment_id) THEN
                CONTINUE;
            END IF;

            SELECT identity_user."Email", profile.address_line_1
            INTO customer_email, customer_address
            FROM user_profile profile
            JOIN "AspNetUsers" identity_user ON identity_user."Id" = profile.identity_user_id
            WHERE profile.province_code = province_row.province_code
            ORDER BY md5(profile.identity_user_id || sale_number::text)
            LIMIT 1;

            IF province_row.province_code = '03' THEN
                SELECT delivery_store_id INTO selected_store_id
                FROM delivery_store WHERE name = 'Panadería local de Azogues' LIMIT 1;
            ELSIF province_row.province_code = '04' THEN
                SELECT delivery_store_id INTO selected_store_id
                FROM delivery_store WHERE name = 'Cafetería local de Tulcán' LIMIT 1;
            ELSE
                SELECT store.delivery_store_id
                INTO selected_store_id
                FROM delivery_store store
                WHERE store.province_code = province_row.province_code
                  AND store.is_active
                  AND EXISTS (
                      SELECT 1 FROM delivery_product product
                      WHERE product.delivery_store_id = store.delivery_store_id
                        AND product.is_available)
                ORDER BY md5(store.delivery_store_id::text || sale_number::text)
                LIMIT 1;
            END IF;

            order_status := CASE sale_number % 3
                WHEN 0 THEN 'Entregado'
                WHEN 1 THEN 'En camino'
                ELSE 'En preparación'
            END;
            order_created_at := date_trunc('month', now())
                + make_interval(
                    days => ((province_row.province_code::integer * 3 + sale_number * 2)
                        % greatest(extract(day FROM now())::integer - 1, 1)),
                    hours => 9 + (sale_number % 10),
                    mins => province_row.province_code::integer + sale_number);

            INSERT INTO delivery_order
                (delivery_store_id, customer_email, delivery_address, status, total, created_at)
            VALUES
                (selected_store_id, customer_email, left(customer_address, 180), order_status, 0, order_created_at)
            RETURNING delivery_order_id INTO selected_order_id;

            order_total := 0;
            FOR product_row IN
                SELECT product.delivery_product_id, product.name, product.price
                FROM delivery_product product
                WHERE product.delivery_store_id = selected_store_id
                  AND product.is_available
                ORDER BY md5(product.delivery_product_id::text || sale_number::text)
                LIMIT (1 + sale_number % 2)
            LOOP
                INSERT INTO delivery_order_item
                    (delivery_order_id, delivery_product_id, product_name, quantity, unit_price, subtotal)
                VALUES
                    (selected_order_id, product_row.delivery_product_id, product_row.name, 1, product_row.price, product_row.price);
                order_total := order_total + product_row.price;
            END LOOP;

            IF order_total <= 0 THEN
                RAISE EXCEPTION 'La tienda % no tiene productos vendibles.', selected_store_id;
            END IF;

            UPDATE delivery_order SET total = order_total WHERE delivery_order_id = selected_order_id;

            INSERT INTO payment
                (delivery_order_id, external_id, provider, status, amount, created_at, confirmed_at)
            VALUES
                (selected_order_id, external_payment_id,
                 CASE WHEN sale_number % 2 = 0 THEN 'PayPhone' ELSE 'PayPal' END,
                 'Aprobado', order_total, order_created_at + interval '2 minutes', order_created_at + interval '2 minutes');
        END LOOP;
    END LOOP;
END
$seed$;

-- Si una tienda fue redistribuida durante una ejecución previa, completa cualquier
-- provincia que haya quedado por debajo del mínimo solicitado.
DO $reconcile$
DECLARE
    province_row record;
    product_row record;
    missing_sales integer;
    correction_number integer;
    selected_store_id integer;
    selected_order_id integer;
    customer_email text;
    customer_address text;
    order_total numeric(10,2);
    month_key text := to_char(current_date, 'YYYYMM');
    external_payment_id text;
BEGIN
    FOR province_row IN
        SELECT province.province_code, province.name,
               greatest(0, 10 - count(tagged_payment.payment_id))::integer AS missing
        FROM ecuador_province province
        LEFT JOIN delivery_store store ON store.province_code = province.province_code
        LEFT JOIN delivery_order tagged_order ON tagged_order.delivery_store_id = store.delivery_store_id
        LEFT JOIN payment tagged_payment
          ON tagged_payment.delivery_order_id = tagged_order.delivery_order_id
         AND tagged_payment.external_id LIKE 'ORBI-EC-' || month_key || '-%'
        GROUP BY province.province_code, province.name
        HAVING count(tagged_payment.payment_id) < 10
        ORDER BY province.province_code
    LOOP
        missing_sales := province_row.missing;

        FOR correction_number IN 1..missing_sales LOOP
            external_payment_id := format(
                'ORBI-EC-%s-%s-X%s', month_key, province_row.province_code,
                lpad(correction_number::text, 2, '0'));

            IF EXISTS (SELECT 1 FROM payment WHERE external_id = external_payment_id) THEN
                CONTINUE;
            END IF;

            SELECT store.delivery_store_id
            INTO selected_store_id
            FROM delivery_store store
            WHERE store.province_code = province_row.province_code
              AND store.is_active
              AND EXISTS (
                  SELECT 1 FROM delivery_product product
                  WHERE product.delivery_store_id = store.delivery_store_id
                    AND product.is_available)
            ORDER BY md5(store.delivery_store_id::text || correction_number::text)
            LIMIT 1;

            SELECT identity_user."Email", profile.address_line_1
            INTO customer_email, customer_address
            FROM user_profile profile
            JOIN "AspNetUsers" identity_user ON identity_user."Id" = profile.identity_user_id
            WHERE profile.province_code = province_row.province_code
            ORDER BY md5(profile.identity_user_id || correction_number::text)
            LIMIT 1;

            SELECT product.delivery_product_id, product.name, product.price
            INTO product_row
            FROM delivery_product product
            WHERE product.delivery_store_id = selected_store_id
              AND product.is_available
            ORDER BY md5(product.delivery_product_id::text || correction_number::text)
            LIMIT 1;

            order_total := product_row.price;
            INSERT INTO delivery_order
                (delivery_store_id, customer_email, delivery_address, status, total, created_at)
            VALUES
                (selected_store_id, customer_email, left(customer_address, 180), 'Entregado', order_total,
                 date_trunc('month', now()) + interval '12 days 10 hours')
            RETURNING delivery_order_id INTO selected_order_id;

            INSERT INTO delivery_order_item
                (delivery_order_id, delivery_product_id, product_name, quantity, unit_price, subtotal)
            VALUES
                (selected_order_id, product_row.delivery_product_id, product_row.name, 1,
                 product_row.price, product_row.price);

            INSERT INTO payment
                (delivery_order_id, external_id, provider, status, amount, created_at, confirmed_at)
            VALUES
                (selected_order_id, external_payment_id, 'PayPhone', 'Aprobado', order_total,
                 date_trunc('month', now()) + interval '12 days 10 hours 2 minutes',
                 date_trunc('month', now()) + interval '12 days 10 hours 2 minutes');
        END LOOP;
    END LOOP;
END
$reconcile$;

-- Normaliza clientes locales si una tienda fue trasladada durante la preparación.
WITH mismatched_orders AS (
    SELECT orders.delivery_order_id, stores.province_code
    FROM payment payments
    JOIN delivery_order orders ON orders.delivery_order_id = payments.delivery_order_id
    JOIN delivery_store stores ON stores.delivery_store_id = orders.delivery_store_id
    JOIN "AspNetUsers" identity_user ON identity_user."Email" = orders.customer_email
    JOIN user_profile profile ON profile.identity_user_id = identity_user."Id"
    WHERE payments.external_id LIKE 'ORBI-EC-' || to_char(current_date, 'YYYYMM') || '-%'
      AND profile.province_code <> stores.province_code
), local_replacements AS (
    SELECT mismatch.delivery_order_id, replacement.email, replacement.address_line_1
    FROM mismatched_orders mismatch
    CROSS JOIN LATERAL (
        SELECT identity_user."Email" AS email, profile.address_line_1
        FROM user_profile profile
        JOIN "AspNetUsers" identity_user ON identity_user."Id" = profile.identity_user_id
        WHERE profile.province_code = mismatch.province_code
        ORDER BY md5(profile.identity_user_id || mismatch.delivery_order_id::text)
        LIMIT 1
    ) replacement
)
UPDATE delivery_order orders
SET customer_email = replacement.email,
    delivery_address = left(replacement.address_line_1, 180)
FROM local_replacements replacement
WHERE orders.delivery_order_id = replacement.delivery_order_id;

COMMIT;

-- Verificación: deben existir al menos 10 ventas demo por cada una de las 24 provincias.
SELECT province.name AS province, count(*) AS demo_sales, sum(orders.total)::numeric(12,2) AS revenue
FROM payment payments
JOIN delivery_order orders ON orders.delivery_order_id = payments.delivery_order_id
JOIN delivery_store stores ON stores.delivery_store_id = orders.delivery_store_id
JOIN ecuador_province province ON province.province_code = stores.province_code
WHERE payments.external_id LIKE 'ORBI-EC-' || to_char(current_date, 'YYYYMM') || '-%'
GROUP BY province.province_code, province.name
ORDER BY province.province_code;
