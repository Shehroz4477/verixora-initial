namespace BuildingBlocks.Infrastructure;

public static class SqlDialectHelper
{
    public static string GetUnlockProcedure(string provider) => provider switch
    {
        "SqlServer" => "[smartlocks].[usp_UnlockDoor]",
        "MySql" => "usp_UnlockDoor",
        "PostgreSql" => "smartlocks.usp_UnlockDoor",
        "Sqlite" => "UPDATE SmartLocks SET Status = 'Unlocked', UnlockedAt = @UnlockedAt, UnlockedBy = @UserId WHERE Id = @LockId;",
        _ => throw new NotSupportedException($"Provider {provider} not supported")
    };

    public static string GetInsertAuditLog(string provider) => provider switch
    {
        "SqlServer" => @"INSERT INTO [auditlogs].[AuditLogs] (Id, UserId, DeviceId, Action, Timestamp, Details) VALUES (@Id, @UserId, @DeviceId, @Action, @Timestamp, @Details);",
        "MySql" => "INSERT INTO auditlogs.AuditLogs (Id, UserId, DeviceId, Action, Timestamp, Details) VALUES (@Id, @UserId, @DeviceId, @Action, @Timestamp, @Details);",
        "PostgreSql" => @"INSERT INTO auditlogs.""AuditLogs"" (""Id"", ""UserId"", ""DeviceId"", ""Action"", ""Timestamp"", ""Details"") VALUES (@Id, @UserId, @DeviceId, @Action, @Timestamp, @Details);",
        "Sqlite" => "INSERT INTO AuditLogs (Id, UserId, DeviceId, Action, Timestamp, Details) VALUES (@Id, @UserId, @DeviceId, @Action, @Timestamp, @Details);",
        _ => throw new NotSupportedException($"Provider {provider} not supported")
    };

    public static string GetSelectLockById(string provider) => provider switch
    {
        "SqlServer" => "SELECT * FROM smartlocks.SmartLocks WHERE Id = @Id;",
        "MySql" => "SELECT * FROM smartlocks.SmartLocks WHERE Id = @Id;",
        "PostgreSql" => @"SELECT * FROM smartlocks.""SmartLocks"" WHERE ""Id"" = @Id;",
        "Sqlite" => "SELECT * FROM SmartLocks WHERE Id = @Id;",
        _ => throw new NotSupportedException($"Provider {provider} not supported")
    };

    public static string GetParameterPrefix(string provider) => provider switch
    {
        "SqlServer" => "@",
        "MySql" => "@",
        "PostgreSql" => "@",
        "Sqlite" => "@",
        _ => throw new NotSupportedException()
    };
}