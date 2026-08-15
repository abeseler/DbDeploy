/* Migration { "title": "customers:createTable" } */
CREATE TABLE customers (
    id INT NOT NULL IDENTITY(1, 1),
    name NVARCHAR(200) NOT NULL,
    email NVARCHAR(320) NOT NULL,
    created_on DATETIMEOFFSET NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_customers PRIMARY KEY (id)
);

/* Migration { "title": "customers.phone:addColumn" } */
ALTER TABLE customers
ADD phone NVARCHAR(50) NULL;
