-- Add session grouping to existing installations.
USE edu_platform;

ALTER TABLE user_activities ADD IF NOT EXISTS session_id uuid;
