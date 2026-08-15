/* Migration { "title": "orders:createTable" } */
CREATE TABLE orders (
    id INT NOT NULL IDENTITY(1, 1),
    customer_id INT NOT NULL,
    amount DECIMAL(12, 2) NOT NULL DEFAULT (0),
    created_on DATETIMEOFFSET NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_orders PRIMARY KEY (id),
    CONSTRAINT fk_orders_customers FOREIGN KEY (customer_id) REFERENCES customers (id)
);

/* Migration { "title": "orders.status:addColumn" } */
ALTER TABLE orders
ADD status NVARCHAR(30) NOT NULL DEFAULT ('pending');
