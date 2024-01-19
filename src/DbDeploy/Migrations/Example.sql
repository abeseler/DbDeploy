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
CREATE NONCLUSTERED INDEX [IX_Example_Name] ON [dbo].[Example]([Name] ASC);
GO

/* Migration
{
    "title": "CreateProcedure:2",
	"runOnChange": true,
	"runInTransaction": false
}
*/
CREATE PROCEDURE [dbo].[GetExample]
AS
BEGIN
	SELECT * FROM [dbo].[Example]
END
GO

GRANT EXECUTE ON [dbo].[GetExample] TO [ExampleRole]
GO
