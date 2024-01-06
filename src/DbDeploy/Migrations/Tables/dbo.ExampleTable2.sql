/* Migration title=createTable */
CREATE TABLE dbo.ExampleTable2
(
	ExampleTableId INT NOT NULL IDENTITY(1,1) PRIMARY KEY
);

/* Migration title=addExampleTableColumn1 */
ALTER TABLE dbo.ExampleTable2
ADD ExampleTableColumn1 VARCHAR(50) NULL;
GO

/* Migration title=addExampleTableColumn2 */
ALTER TABLE dbo.ExampleTable2
ADD ExampleTableColumn2 VARCHAR(50) NULL;
