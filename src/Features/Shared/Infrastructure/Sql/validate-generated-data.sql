-- Debe devolver exactamente una fila con total_business_records = 1000000
-- y cero en todas las columnas invalid_*.
SELECT
    (SELECT COUNT(*) FROM delivery_store) +
    (SELECT COUNT(*) FROM delivery_product) +
    (SELECT COUNT(*) FROM user_profile) +
    (SELECT COUNT(*) FROM delivery_order) +
    (SELECT COUNT(*) FROM delivery_order_item) +
    (SELECT COUNT(*) FROM payment) +
    (SELECT COUNT(*) FROM inventory_movement) +
    (SELECT COUNT(*) FROM audit_log) +
    (SELECT COUNT(*) FROM delivery_incident) AS total_business_records,
    (SELECT COUNT(*) - COUNT(DISTINCT cedula) FROM user_profile) AS invalid_duplicate_cedulas,
    (SELECT COUNT(*) - COUNT(DISTINCT "Email") FROM "AspNetUsers" WHERE "Email" LIKE '%@datos.orbi.ec') AS invalid_duplicate_emails,
    (SELECT COUNT(*) FROM user_profile WHERE
        substring(cedula, 1, 2)::int NOT BETWEEN 1 AND 24 OR
        substring(cedula, 3, 1)::int NOT BETWEEN 0 AND 5 OR
        substring(cedula, 10, 1)::int <> (10 - (
            (CASE WHEN substring(cedula,1,1)::int*2>9 THEN substring(cedula,1,1)::int*2-9 ELSE substring(cedula,1,1)::int*2 END) +
            substring(cedula,2,1)::int +
            (CASE WHEN substring(cedula,3,1)::int*2>9 THEN substring(cedula,3,1)::int*2-9 ELSE substring(cedula,3,1)::int*2 END) +
            substring(cedula,4,1)::int +
            (CASE WHEN substring(cedula,5,1)::int*2>9 THEN substring(cedula,5,1)::int*2-9 ELSE substring(cedula,5,1)::int*2 END) +
            substring(cedula,6,1)::int +
            (CASE WHEN substring(cedula,7,1)::int*2>9 THEN substring(cedula,7,1)::int*2-9 ELSE substring(cedula,7,1)::int*2 END) +
            substring(cedula,8,1)::int +
            (CASE WHEN substring(cedula,9,1)::int*2>9 THEN substring(cedula,9,1)::int*2-9 ELSE substring(cedula,9,1)::int*2 END)
        ) % 10) % 10) AS invalid_cedulas,
    (SELECT COUNT(*) FROM user_profile u JOIN ecuador_city c ON c.city_code = u.city_code WHERE c.province_code <> u.province_code) AS invalid_locations,
    (SELECT COUNT(*) FROM delivery_order_item i JOIN delivery_order o USING (delivery_order_id) JOIN delivery_product p USING (delivery_product_id) WHERE o.delivery_store_id <> p.delivery_store_id) AS invalid_product_stores,
    (SELECT COUNT(*) FROM delivery_order_item WHERE subtotal <> quantity * unit_price) AS invalid_subtotals,
    (SELECT COUNT(*) FROM delivery_order o JOIN (SELECT delivery_order_id, SUM(subtotal) total FROM delivery_order_item GROUP BY delivery_order_id) i USING (delivery_order_id) WHERE o.total <> i.total) AS invalid_order_totals,
    (SELECT COUNT(*) FROM payment p JOIN delivery_order o USING (delivery_order_id) WHERE p.amount <> o.total) AS invalid_payment_amounts;
