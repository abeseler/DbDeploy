/* Migration { "title": "order_status:createType" } */
CREATE TYPE order_status AS ENUM (
    'pending',
    'shipped',
    'cancelled'
);
