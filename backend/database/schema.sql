CREATE TABLE IF NOT EXISTS owners (
  id uuid PRIMARY KEY,
  email varchar(254) NOT NULL UNIQUE,
  password_hash text NOT NULL,
  role text NOT NULL DEFAULT 'owner' CHECK (role IN ('owner', 'staff'))
);
CREATE TABLE IF NOT EXISTS service_groups (
  id uuid PRIMARY KEY,
  name varchar(100) NOT NULL CHECK (char_length(trim(name)) > 0),
  name_key varchar(100) NOT NULL UNIQUE,
  display_order integer NOT NULL CHECK (display_order BETWEEN 0 AND 9999),
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS services (
  id uuid PRIMARY KEY,
  group_id uuid REFERENCES service_groups(id) ON DELETE SET NULL,
  name varchar(150) NOT NULL,
  is_active boolean NOT NULL DEFAULT true
);
CREATE INDEX IF NOT EXISTS services_group_index ON services(group_id);

-- PostgreSQL delivers NOTIFY only after COMMIT, including changes from other API instances.
CREATE OR REPLACE FUNCTION notify_salon_catalog_changed() RETURNS trigger AS $$
BEGIN
  PERFORM pg_notify('salon_catalog_changed', 'refresh');
  RETURN NULL;
END;
$$ LANGUAGE plpgsql;
DROP TRIGGER IF EXISTS service_groups_catalog_changed ON service_groups;
CREATE TRIGGER service_groups_catalog_changed
  AFTER INSERT OR UPDATE OR DELETE OR TRUNCATE ON service_groups
  FOR EACH STATEMENT EXECUTE FUNCTION notify_salon_catalog_changed();
DROP TRIGGER IF EXISTS services_catalog_changed ON services;
CREATE TRIGGER services_catalog_changed
  AFTER INSERT OR UPDATE OR DELETE OR TRUNCATE ON services
  FOR EACH STATEMENT EXECUTE FUNCTION notify_salon_catalog_changed();
