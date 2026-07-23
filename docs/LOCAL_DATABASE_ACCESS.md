# Local database access with DBeaver

DBeaver Community is installed on this PC and can manage both local Verixora
databases from a single desktop application. Start it from the Windows Start menu,
then create the following connections. Keep the password values in
`infrastructure/.env` private.

| Connection | Driver | Host / port | Database | User |
| --- | --- | --- | --- | --- |
| Verixora SQL Server | SQL Server | `127.0.0.1,14333` | `master` or `verixora` | `sa` |
| Verixora PostgreSQL | PostgreSQL | `127.0.0.1:5433` | `verixora` | `verixora_app` |

For SQL Server enable **Trust server certificate** in the driver properties for
the local Docker container. For PostgreSQL use the `POSTGRES_PASSWORD` value from
`infrastructure/.env`. For SQL Server use `MSSQL_SA_PASSWORD` from the same file.

The API selects the active provider through `DatabaseProvider` and
`ConnectionStrings:DefaultConnection`; do not edit data in both databases and
expect it to synchronize automatically. Use DBeaver for inspection and manual
development queries, while the application continues to use its versioned SQL
scripts and stored procedures for schema changes.

Useful first checks:

```sql
-- SQL Server
SELECT name FROM sys.tables ORDER BY name;

-- PostgreSQL
SELECT table_schema, table_name
FROM information_schema.tables
WHERE table_schema = 'public'
ORDER BY table_name;
```
