/* Migration
{
	"title": "seed:sampleCustomers",
	"dependsOn": ["postgres/customers.sql"],
	"contextFilter": ["test"],
	"onError": "Skip"
}
*/
INSERT INTO customers (name, email) VALUES ('Ada Lovelace', 'ada@example.com');
--NewStatement
INSERT INTO customers (name, email) VALUES ('Alan Turing', 'alan@example.com');
