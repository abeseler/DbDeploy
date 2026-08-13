/* Migration
{
	"title": "example:1"
}
*/
CREATE TABLE example (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    created_on TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now') || '+00:00')
);
