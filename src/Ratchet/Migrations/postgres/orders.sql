/* Migration
{
	"title": "orders:createTable"
}
*/
CREATE TABLE orders (
    id INT GENERATED ALWAYS AS IDENTITY,
    customer_id INT NOT NULL,
    amount NUMERIC(12, 2) NOT NULL DEFAULT 0,
    created_on TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT pk_orders PRIMARY KEY (id),
    CONSTRAINT fk_orders_customers FOREIGN KEY (customer_id) REFERENCES customers (id)
);

/* Migration
{
	"title": "orders.status:addColumn"
}
*/
ALTER TABLE orders
ADD COLUMN status TEXT NOT NULL DEFAULT 'pending';
