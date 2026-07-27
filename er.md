# Diagrama Entidad-Relación — Orbi App

```mermaid
erDiagram

    AspNetUsers {
        text Id PK
    }

    ecuador_province {
        varchar(2) province_code PK
        varchar(100) name UK
    }

    ecuador_city {
        varchar(4) city_code PK
        varchar(2) province_code FK
        varchar(100) name
    }

    user_profile {
        text identity_user_id PK FK
        varchar(80) first_name
        varchar(80) last_name
        varchar(10) cedula UK
        varchar(160) address_line_1
        varchar(160) address_line_2
        varchar(2) province_code FK
        varchar(4) city_code FK
        varchar(240) reference
        varchar(20) preferred_payment_method
        timestamp created_at
    }

    user_address {
        bigint user_address_id PK
        text identity_user_id FK
        varchar(40) label
        varchar(160) address_line_1
        varchar(160) address_line_2
        varchar(2) province_code FK
        varchar(4) city_code FK
        varchar(240) reference
        boolean is_default
        timestamp created_at
        timestamp updated_at
    }

    delivery_store {
        integer delivery_store_id PK
        varchar(100) name
        varchar(60) category
        varchar(180) address
        varchar(2) province_code FK
        varchar(4) city_code FK
        boolean is_active
        timestamp created_at
    }

    delivery_product {
        integer delivery_product_id PK
        integer delivery_store_id FK
        text created_by_user_id FK
        varchar(100) name
        numeric price
        numeric unit_cost
        integer stock
        boolean is_available
        timestamp created_at
        timestamp updated_at
    }

    delivery_order {
        integer delivery_order_id PK
        integer delivery_store_id FK
        varchar(256) customer_email
        varchar(256) delivery_person_email
        varchar(180) delivery_address
        varchar(30) status
        numeric total
        timestamp created_at
    }

    delivery_order_item {
        integer delivery_order_item_id PK
        integer delivery_order_id FK
        integer delivery_product_id FK
        varchar(100) product_name
        integer quantity
        numeric unit_price
        numeric subtotal
    }

    payment {
        bigint payment_id PK
        integer delivery_order_id FK
        varchar(80) external_id UK
        varchar(30) provider
        varchar(20) status
        numeric amount
        timestamp created_at
        timestamp confirmed_at
    }

    delivery_cart_item {
        bigint delivery_cart_item_id PK
        varchar(256) user_email
        integer delivery_product_id FK
        integer quantity
        timestamp created_at
    }

    inventory_movement {
        bigint inventory_movement_id PK
        integer delivery_product_id FK
        integer delivery_order_id FK
        text performed_by_user_id FK
        varchar(20) movement_type
        integer quantity_delta
        numeric unit_cost
        jsonb metadata
        timestamp created_at
    }

    delivery_incident {
        bigint delivery_incident_id PK
        integer delivery_order_id FK
        text reported_by_user_id FK
        varchar(60) incident_type
        varchar(20) severity
        text description
        varchar(20) status
        jsonb details
        timestamp created_at
        timestamp resolved_at
    }

    audit_log {
        bigint audit_log_id PK
        text user_id FK
        varchar(80) action
        varchar(120) entity_type
        varchar(128) entity_id
        jsonb old_values
        jsonb new_values
        varchar(64) ip_address
        varchar(512) user_agent
        varchar(100) correlation_id
        timestamp created_at
    }

    order_status_history {
        bigint order_status_history_id PK
        integer delivery_order_id FK
        text changed_by_user_id FK
        varchar(30) previous_status
        varchar(30) new_status
        varchar(500) note
        jsonb metadata
        timestamp changed_at
    }

    stock_reservation {
        bigint stock_reservation_id PK
        integer delivery_product_id FK
        integer delivery_order_id FK
        text reserved_by_user_id FK
        integer quantity
        varchar(20) status
        timestamp expires_at
        timestamp created_at
        timestamp released_at
    }

    email_queue {
        bigint email_queue_id PK
        varchar(320) recipient_email
        varchar(255) subject
        text body_html
        varchar(20) status
        integer attempt_count
        integer max_attempts
        timestamp scheduled_at
        timestamp last_attempt_at
        timestamp sent_at
        text last_error
        jsonb metadata
        timestamp created_at
    }

    ai_consumption_log {
        bigint ai_consumption_log_id PK
        text user_id FK
        varchar(120) model_name
        varchar(80) operation
        varchar(500) prompt_text
        integer prompt_tokens
        integer completion_tokens
        integer total_tokens
        numeric estimated_cost
        integer duration_milliseconds
        varchar(45) ip_address
        jsonb metadata
        timestamp created_at
    }

    %% --- Relaciones ---

    ecuador_province ||--o{ ecuador_city : "tiene"
    ecuador_province ||--o{ delivery_store : "ubicada en"
    ecuador_province ||--o{ user_profile : "ubicado en"
    ecuador_province ||--o{ user_address : "ubicada en"

    ecuador_city ||--o{ delivery_store : "ubicada en"
    ecuador_city ||--o{ user_profile : "ubicado en"
    ecuador_city ||--o{ user_address : "ubicada en"

    AspNetUsers ||--o| user_profile : "tiene"
    AspNetUsers ||--o{ user_address : "tiene"
    AspNetUsers ||--o{ delivery_product : "crea"
    AspNetUsers ||--o{ inventory_movement : "realiza"
    AspNetUsers ||--o{ delivery_incident : "reporta"
    AspNetUsers ||--o{ audit_log : "ejecuta"
    AspNetUsers ||--o{ order_status_history : "cambia"
    AspNetUsers ||--o{ stock_reservation : "reserva"
    AspNetUsers ||--o{ ai_consumption_log : "usa"

    user_profile ||--o{ user_address : "tiene"

    delivery_store ||--o{ delivery_product : "contiene"
    delivery_store ||--o{ delivery_order : "recibe"

    delivery_product ||--o{ delivery_order_item : "se ordena"
    delivery_product ||--o{ delivery_cart_item : "se agrega"
    delivery_product ||--o{ inventory_movement : "se mueve"
    delivery_product ||--o{ stock_reservation : "se reserva"

    delivery_order ||--o{ delivery_order_item : "contiene"
    delivery_order ||--o{ payment : "se paga"
    delivery_order ||--o{ inventory_movement : "afecta"
    delivery_order ||--o{ delivery_incident : "genera"
    delivery_order ||--o{ order_status_history : "historial"
    delivery_order ||--o{ stock_reservation : "reserva"
```
