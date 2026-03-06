CREATE TABLE IF NOT EXISTS templates (
    name TEXT PRIMARY KEY,
    family TEXT NOT NULL,
    size_preset TEXT NOT NULL,
    modified_at TEXT NOT NULL,
    thumbnail_path TEXT
);
