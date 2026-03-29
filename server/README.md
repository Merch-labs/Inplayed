# Social Database

This folder now just holds the SQL setup for the social features.

The desktop app connects to PostgreSQL directly.

Files:
- `sql/schema.sql`

The schema uses:
- integer IDs
- `VARCHAR(...)` fields with size limits
- `TIMESTAMP` values for dates and times

To use it:
1. create a PostgreSQL database
2. run `sql/schema.sql`
3. open the app settings page
4. fill in `Social Database` with your username and connection string

If no connection string is saved, the app falls back to local JSON storage for social data.
