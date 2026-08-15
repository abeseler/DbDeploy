/* Migration { "title": "orders:createTable" } */
CREATE TABLE orders (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    customer_id INTEGER NOT NULL REFERENCES customers (id),
    amount NUMERIC NOT NULL DEFAULT 0,
    created_on TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now') || '+00:00')
);

/* Migration { "title": "orders.status:addColumn" } */
ALTER TABLE orders
ADD COLUMN status TEXT NOT NULL DEFAULT 'pending';
