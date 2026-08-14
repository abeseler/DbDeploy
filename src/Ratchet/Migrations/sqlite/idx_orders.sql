/* Migration
{
	"title": "orders.customer_id:createIndex",
	"dependsOn": ["sqlite/orders.sql"],
	"runInTransaction": false,
	"timeout": 60
}
*/
CREATE INDEX IF NOT EXISTS ix_orders_customer_id ON orders (customer_id);
