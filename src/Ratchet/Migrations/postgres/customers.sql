/* Migration
{
	"title": "customers:createTable"
}
*/
CREATE TABLE customers (
    id INT GENERATED ALWAYS AS IDENTITY,
    name TEXT NOT NULL,
    email TEXT NOT NULL,
    created_on TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT pk_customers PRIMARY KEY (id)
);

/* Migration
{
	"title": "customers.phone:addColumn"
}
*/
ALTER TABLE customers
ADD COLUMN phone TEXT NULL;
