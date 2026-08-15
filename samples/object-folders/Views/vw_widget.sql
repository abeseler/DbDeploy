/* Migration
{
    "title": "vw_widget:create",
    "run": "onChange"
}
*/
CREATE OR REPLACE VIEW vw_widget AS
SELECT widget_id, description, status, created_on_utc
FROM widget;
