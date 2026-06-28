-- Create the pgvector extension before any application connects. This permanently avoids
-- the startup-ordering pitfall where a connection's type cache is built before the
-- extension exists (which makes vector writes fail until the connection is recycled).
CREATE EXTENSION IF NOT EXISTS vector;
