/* Migration
{
	"title": "CreateTable:1",
	"timeout": 1000,
	"contextFilter": [
		"testing"
	]
}
*/
CREATE TABLE [dbo].[Example] (
	[Id] INT IDENTITY (1, 1) NOT NULL,
	[Name] NVARCHAR (MAX) NULL,
	CONSTRAINT [PK_Example] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

/* Migration
{
	"title": "CreateProcedure:2",
	"runOnChange": true,
	"runInTransaction": false
}
*/
CREATE OR ALTER PROCEDURE [dbo].[GetExample]
AS
BEGIN
	SELECT 2;
END
GO

/* Migration
{
	"title": "CreateProcedure:3",
	"runOnChange": true,
	"runInTransaction": false
}
*/
CREATE OR ALTER PROCEDURE [dbo].[GetExample3]
AS
BEGIN
	SELECT 3;
END
GO
