/* Migration
{
	"title": "seed:sampleCustomers",
	"dependsOn": ["sqlite/customers.sql"],
	"contextFilter": ["test"],
	"onError": "Skip"
}
*/
INSERT INTO customers (name, email, phone) VALUES ('Ada Lovelace', 'ada@example.com', NULL);
--NewStatement
INSERT INTO customers (name, email, phone) VALUES ('Alan Turing', 'alan@example.com', NULL);
