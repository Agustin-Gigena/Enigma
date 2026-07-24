-- Placeholder: replace with actual schema via EF Core migrations.
-- The Server's Program.cs runs auto-migration in development;
-- this file only ensures the DB is non-empty on first start.
CREATE TABLE IF NOT EXISTS _schema_version (
    id INT PRIMARY KEY AUTO_INCREMENT,
    description VARCHAR(255) NOT NULL,
    applied_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO _schema_version (description) VALUES ('initial placeholder');
