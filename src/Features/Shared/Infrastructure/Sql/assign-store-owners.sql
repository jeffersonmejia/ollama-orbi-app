-- Asigna propietarios existentes a las primeras y últimas 100 tiendas.
-- Es idempotente: no modifica tiendas asignadas y nunca reutiliza un propietario.

BEGIN;

WITH target_pool AS (
    (SELECT delivery_store_id
     FROM delivery_store
     ORDER BY delivery_store_id
     LIMIT 100)
    UNION ALL
    (SELECT delivery_store_id
     FROM delivery_store
     ORDER BY delivery_store_id DESC
     LIMIT 100)
), targets AS (
    SELECT store.delivery_store_id,
           store.province_code,
           row_number() OVER (
               PARTITION BY store.province_code
               ORDER BY store.delivery_store_id) AS province_position
    FROM delivery_store store
    JOIN target_pool target ON target.delivery_store_id = store.delivery_store_id
    WHERE store.owner_user_id IS NULL
), available_users AS (
    SELECT users."Id" AS user_id,
           profile.province_code,
           row_number() OVER (
               PARTITION BY profile.province_code
               ORDER BY users."Id") AS province_position
    FROM "AspNetUsers" users
    JOIN user_profile profile ON profile.identity_user_id = users."Id"
    WHERE NOT EXISTS (
        SELECT 1
        FROM delivery_store assigned_store
        WHERE assigned_store.owner_user_id = users."Id")
      AND NOT EXISTS (
        SELECT 1
        FROM "AspNetUserRoles" user_role
        WHERE user_role."UserId" = users."Id")
), assignments AS (
    SELECT target.delivery_store_id, available.user_id
    FROM targets target
    JOIN available_users available
      ON available.province_code = target.province_code
     AND available.province_position = target.province_position
)
UPDATE delivery_store store
SET owner_user_id = assignment.user_id
FROM assignments assignment
WHERE store.delivery_store_id = assignment.delivery_store_id
  AND store.owner_user_id IS NULL;

COMMIT;

WITH target AS (
    (SELECT delivery_store_id, owner_user_id, 'first' AS segment
     FROM delivery_store ORDER BY delivery_store_id LIMIT 100)
    UNION ALL
    (SELECT delivery_store_id, owner_user_id, 'last' AS segment
     FROM delivery_store ORDER BY delivery_store_id DESC LIMIT 100)
)
SELECT segment,
       count(*) AS stores,
       count(owner_user_id) AS assigned,
       count(DISTINCT owner_user_id) AS distinct_owners
FROM target
GROUP BY segment
ORDER BY segment;

SELECT count(*) AS owners_with_multiple_stores
FROM (
    SELECT owner_user_id
    FROM delivery_store
    WHERE owner_user_id IS NOT NULL
    GROUP BY owner_user_id
    HAVING count(*) > 1
) duplicates;
