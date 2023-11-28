/* Migration title=createTable */
CREATE TABLE dbo.ExampleTable4
(
	ExampleTableId INT NOT NULL IDENTITY(1,1) PRIMARY KEY
);

/* Migration title=addExampleTableColumn1 */
ALTER TABLE dbo.ExampleTable4
ADD ExampleTableColumn1 VARCHAR(50) NULL;

/* Migration title=addExampleTableColumn2 */
ALTER TABLE dbo.ExampleTable4
ADD ExampleTableColumn2 VARCHAR(50) NULL;
