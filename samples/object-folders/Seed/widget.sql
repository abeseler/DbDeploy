/* Migration
{
    "title": "widget:sampleRows",
    "run": "always"
}
*/
INSERT INTO widget (widget_id, description, status)
VALUES (1, 'example', 'pending')
ON CONFLICT (widget_id) DO UPDATE SET
    description = EXCLUDED.description,
    status = EXCLUDED.status;
