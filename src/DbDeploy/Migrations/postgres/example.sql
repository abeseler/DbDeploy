/* Migration
{
	"title": "example:1"
}
*/
CREATE TABLE example (
    id INT GENERATED ALWAYS AS IDENTITY,
    created_on TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT pk_example PRIMARY KEY (id)
);
