/* Migration
{
	"title": "order_status:createTable"
}
*/
CREATE TABLE order_status (
    code TEXT NOT NULL PRIMARY KEY,
    description TEXT NOT NULL
);

/* Migration
{
	"title": "order_status:seed",
	"runAlways": true
}
*/
INSERT INTO order_status (code, description) VALUES ('pending', 'Pending')
ON CONFLICT (code) DO UPDATE SET description = excluded.description;
--NewStatement
INSERT INTO order_status (code, description) VALUES ('shipped', 'Shipped')
ON CONFLICT (code) DO UPDATE SET description = excluded.description;
--NewStatement
INSERT INTO order_status (code, description) VALUES ('cancelled', 'Cancelled')
ON CONFLICT (code) DO UPDATE SET description = excluded.description;
