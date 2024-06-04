/* Migration
{
	"title": "warehouse_reader:CreateRole",
}
*/
CREATE ROLE warehouse_reader;

/* Migration
{
	"title": "warehouse_writer:CreateRole",
}
*/
CREATE ROLE warehouse_writer;

/* Migration
{
	"title": "warehouse:CreateSchema",
}
*/
CREATE SCHEMA IF NOT EXISTS warehouse;

GRANT USAGE ON SCHEMA warehouse TO warehouse_reader;
GRANT USAGE ON SCHEMA warehouse TO warehouse_writer;

ALTER DEFAULT PRIVILEGES IN SCHEMA warehouse
GRANT SELECT ON TABLES TO warehouse_reader;

ALTER DEFAULT PRIVILEGES IN SCHEMA warehouse
GRANT INSERT, UPDATE, DELETE ON TABLES TO warehouse_writer;

/* Migration
{
	"title": "warehouse:CreateTable",
}
*/
CREATE TABLE IF NOT EXISTS warehouse.warehouse (
  warehouse_id		INT GENERATED ALWAYS AS IDENTITY,
  display_name		VARCHAR(50) NOT NULL,
  created_on		TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
  CONSTRAINT pk_warehouse
  	PRIMARY KEY (warehouse_id)
);

/* Migration
{
	"title": "location:CreateTable",
}
*/
CREATE TABLE IF NOT EXISTS warehouse.location (
  location_id		VARCHAR(50) NOT NULL,
  warehouse_id		INT	NOT NULL,
  type				VARCHAR(10) NOT NULL,
  created_on		TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
  CONSTRAINT pk_location
  	PRIMARY KEY (location_id, warehouse_id),
  CONSTRAINT fk_location_warehouse_id
  	FOREIGN KEY (warehouse_id)
  	REFERENCES warehouse.warehouse(warehouse_id)
);

/* Migration
{
	"title": "item:CreateTable",
}
*/
CREATE TABLE IF NOT EXISTS warehouse.item (
  item_id			INT GENERATED ALWAYS AS IDENTITY,
  display_name		VARCHAR(100) NOT NULL,
  created_on		TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
  last_modified_on	TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);
