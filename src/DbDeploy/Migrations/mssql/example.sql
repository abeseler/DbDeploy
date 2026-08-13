/* Migration
{
	"title": "example:1"
}
*/
CREATE TABLE example (
    id INT NOT NULL,
    created_on DATETIMEOFFSET NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT pk_example PRIMARY KEY (id)
);
