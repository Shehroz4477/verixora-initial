-- Verixora Audit Logs module - SQL Server 2022+
-- Append-only operational security trail. API clients never write this table directly.

set ansi_nulls on;
go
set quoted_identifier on;
go

if schema_id(N'auditlogs') is null execute(N'create schema auditlogs');
go

if object_id(N'auditlogs.AuditLogs', N'U') is null
begin
    create table auditlogs.AuditLogs
    (
        Id uniqueidentifier not null primary key,
        HomeId uniqueidentifier not null,
        UserId uniqueidentifier not null,
        DeviceId uniqueidentifier not null,
        Action nvarchar(100) not null,
        TimestampUtc datetime2(7) not null,
        Result bit not null,
        Details nvarchar(1000) null,
        constraint CK_AuditLogs_ActionNotBlank check (len(ltrim(rtrim(Action))) > 0)
    );
    create index IX_AuditLogs_Home_Timestamp on auditlogs.AuditLogs(HomeId, TimestampUtc desc);
    create index IX_AuditLogs_Device_Timestamp on auditlogs.AuditLogs(DeviceId, TimestampUtc desc);
end
go

create or alter procedure auditlogs.sp_CreateAuditLog
    @Id uniqueidentifier,
    @HomeId uniqueidentifier,
    @UserId uniqueidentifier,
    @DeviceId uniqueidentifier,
    @Action nvarchar(100),
    @TimestampUtc datetime2(7),
    @Result bit,
    @Details nvarchar(1000) = null
as
begin
    set nocount on;
    if nullif(ltrim(rtrim(@Action)), N'') is null throw 53001, 'Audit action is required.', 1;

    insert into auditlogs.AuditLogs (Id, HomeId, UserId, DeviceId, Action, TimestampUtc, Result, Details)
    values (@Id, @HomeId, @UserId, @DeviceId, ltrim(rtrim(@Action)), @TimestampUtc, @Result, nullif(ltrim(rtrim(@Details)), N''));
end
go

create or alter procedure auditlogs.sp_GetAuditLogsByHome
    @HomeId uniqueidentifier,
    @Limit int = 100
as
begin
    set nocount on;
    select top (case when @Limit < 1 then 1 when @Limit > 500 then 500 else @Limit end)
        Id, HomeId, UserId, DeviceId, Action, TimestampUtc, Result, Details
    from auditlogs.AuditLogs
    where HomeId = @HomeId
    order by TimestampUtc desc;
end
go
