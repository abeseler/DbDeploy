/* Migration
{
	"title": "orders.customer_id:createIndex",
	"dependsOn": ["mssql/orders.sql"],
	"runInTransaction": false,
	"timeout": 60
}
*/
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_orders_customer_id' AND object_id = OBJECT_ID('orders'))
    CREATE INDEX ix_orders_customer_id ON orders (customer_id);
