/* Migration
{
	"title": "example:1"
}
*/
CREATE TABLE IF NOT EXISTS example (
    id INT GENERATED ALWAYS AS IDENTITY,
    created_on TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
    CONSTRAINT pk_example PRIMARY KEY (id)
);

/* Migration
{
	"title": "example:2",
	"runAlways": true,
	"runOnChange": true,
	"runInTransaction": false,
	"contextFilter": ["one", "two"],
	"requireContext": true,
	"timeout": 42069,
	"onError": "Skip"
}
*/
ALTER TABLE example
ADD column_one TEXT NULL;

--NewStatement

ALTER TABLE example
ADD column_two TEXT NULL;
