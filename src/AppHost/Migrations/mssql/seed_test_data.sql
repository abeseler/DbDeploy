/* Migration
{
	"title": "seed:sampleCustomers",
	"dependsOn": ["mssql/customers.sql"],
	"contextFilter": ["test"],
	"onError": "Skip"
}
*/
INSERT INTO customers (name, email) VALUES (N'Ada Lovelace', N'ada@example.com');
--NewStatement
INSERT INTO customers (name, email) VALUES (N'Alan Turing', N'alan@example.com');
