/* Migration { "title": "order_status:createTable" } */
CREATE TABLE order_status (
    code NVARCHAR(30) NOT NULL,
    description NVARCHAR(100) NOT NULL,
    CONSTRAINT pk_order_status PRIMARY KEY (code)
);

/* Migration
{
	"title": "order_status:seed",
	"run": "always"
}
*/
MERGE order_status AS target
USING (VALUES ('pending', 'Pending'), ('shipped', 'Shipped'), ('cancelled', 'Cancelled')) AS source (code, description)
ON target.code = source.code
WHEN MATCHED THEN UPDATE SET description = source.description
WHEN NOT MATCHED THEN INSERT (code, description) VALUES (source.code, source.description);
