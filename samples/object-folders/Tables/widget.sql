/* Migration { "title": "widget:createTable" } */
CREATE TABLE widget (
    widget_id INT NOT NULL DEFAULT nextval('widget_id_seq'),
    description TEXT NOT NULL,
    status order_status NOT NULL DEFAULT 'pending',
    created_on_utc TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT pk_widget PRIMARY KEY (widget_id)
);
