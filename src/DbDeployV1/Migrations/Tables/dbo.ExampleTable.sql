/* Migration title=createTable */
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE dbo.ExampleTable
(
	ExampleTableId INT NOT NULL IDENTITY(1,1) PRIMARY KEY
);
GO

/* Migration title=addExampleTableColumn1 */
ALTER TABLE dbo.ExampleTable
ADD ExampleTableColumn1 VARCHAR(50) NULL;

/* Migration title=addExampleTableColumn2 */
ALTER TABLE dbo.ExampleTable
ADD ExampleTableColumn2 VARCHAR(50) NULL;
