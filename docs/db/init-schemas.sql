-- Bounded context basina ayri sema. Bkz. ARCHITECTURE.md AD-005.
-- Cross-schema JOIN YASAKTIR; sema ayrimi bu kurali gorunur kilar.

CREATE SCHEMA IF NOT EXISTS hbys;
CREATE SCHEMA IF NOT EXISTS lis;
CREATE SCHEMA IF NOT EXISTS pacs;
CREATE SCHEMA IF NOT EXISTS pathology;
CREATE SCHEMA IF NOT EXISTS vna;

-- Keycloak kendi semasini kullanir.
CREATE SCHEMA IF NOT EXISTS keycloak;
