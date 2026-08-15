/* Migration { "title": "order_status:createTable" } */
CREATE TABLE order_status (
    code TEXT NOT NULL,
    description TEXT NOT NULL,
    CONSTRAINT pk_order_status PRIMARY KEY (code)
);

/* Migration
{
	"title": "order_status:seed",
	"run": "always"
}
*/
INSERT INTO order_status (code, description) VALUES ('pending', 'Pending')
ON CONFLICT (code) DO UPDATE SET description = EXCLUDED.description;
--NewStatement
INSERT INTO order_status (code, description) VALUES ('shipped', 'Shipped')
ON CONFLICT (code) DO UPDATE SET description = EXCLUDED.description;
--NewStatement
INSERT INTO order_status (code, description) VALUES ('cancelled', 'Cancelled')
ON CONFLICT (code) DO UPDATE SET description = EXCLUDED.description;
