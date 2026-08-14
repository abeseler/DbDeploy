/* Migration
{
	"title": "vw_customer_orders:create",
	"runOnChange": true,
	"dependsOn": ["mssql/customers.sql", "mssql/orders.sql"]
}
*/
CREATE OR ALTER VIEW vw_customer_orders AS
SELECT c.id AS customer_id,
       c.name AS customer_name,
       COUNT(o.id) AS order_count,
       COALESCE(SUM(o.amount), 0) AS total_amount
FROM customers c
LEFT JOIN orders o ON o.customer_id = c.id
GROUP BY c.id, c.name;
