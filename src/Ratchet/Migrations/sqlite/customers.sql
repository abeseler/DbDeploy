/* Migration
{
	"title": "customers:createTable"
}
*/
CREATE TABLE customers (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    email TEXT NOT NULL,
    created_on TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now') || '+00:00')
);

/* Migration
{
	"title": "customers.phone:addColumn"
}
*/
ALTER TABLE customers
ADD COLUMN phone TEXT NULL;
