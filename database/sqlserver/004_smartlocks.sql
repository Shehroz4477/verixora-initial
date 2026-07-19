-- Verixora Smart Locks module - SQL Server 2022+
-- One ESP32 controller may control exactly one exterior door in this product.

set ansi_nulls on;
go
set quoted_identifier on;
go

if schema_id(N'smartlocks') is null execute(N'create schema smartlocks');
go

if object_id(N'smartlocks.SmartLocks', N'U') is null
begin
    create table smartlocks.SmartLocks
    (
        Id uniqueidentifier not null primary key,
        DeviceId uniqueidentifier not null,
        HomeId uniqueidentifier not null,
        Name nvarchar(100) not null,
        Status nvarchar(20) not null,
        RequiresFace bit not null,
        LastUnlockedAtUtc datetime2(7) null,
        LastUnlockedBy uniqueidentifier null,
        constraint UQ_SmartLocks_DeviceId unique (DeviceId),
        constraint CK_SmartLocks_NameNotBlank check (len(ltrim(rtrim(Name))) > 0),
        constraint CK_SmartLocks_Status check (Status in (N'Locked', N'Unlocked', N'EmergencyLocked'))
    );
    create index IX_SmartLocks_HomeId on smartlocks.SmartLocks(HomeId);
end
go

create or alter procedure smartlocks.sp_CreateSmartLock
    @Id uniqueidentifier,
    @DeviceId uniqueidentifier,
    @HomeId uniqueidentifier,
    @Name nvarchar(100),
    @Status nvarchar(20),
    @RequiresFace bit
as
begin
    set nocount on;
    if nullif(ltrim(rtrim(@Name)), N'') is null throw 52001, 'Lock name is required.', 1;
    if @Status not in (N'Locked', N'Unlocked', N'EmergencyLocked') throw 52002, 'Lock status is invalid.', 1;
    insert into smartlocks.SmartLocks (Id, DeviceId, HomeId, Name, Status, RequiresFace)
    values (@Id, @DeviceId, @HomeId, ltrim(rtrim(@Name)), @Status, @RequiresFace);
    select Id, DeviceId, HomeId, Name, Status, RequiresFace, LastUnlockedAtUtc, LastUnlockedBy
    from smartlocks.SmartLocks where Id = @Id;
end
go

create or alter procedure smartlocks.sp_GetSmartLockById @Id uniqueidentifier
as
begin
    set nocount on;
    select Id, DeviceId, HomeId, Name, Status, RequiresFace, LastUnlockedAtUtc, LastUnlockedBy
    from smartlocks.SmartLocks where Id = @Id;
end
go

create or alter procedure smartlocks.sp_UpdateSmartLockState
    @Id uniqueidentifier,
    @Status nvarchar(20),
    @LastUnlockedAtUtc datetime2(7) = null,
    @LastUnlockedBy uniqueidentifier = null
as
begin
    set nocount on;
    if @Status not in (N'Locked', N'Unlocked', N'EmergencyLocked') throw 52003, 'Lock status is invalid.', 1;
    update smartlocks.SmartLocks
    set Status = @Status,
        LastUnlockedAtUtc = @LastUnlockedAtUtc,
        LastUnlockedBy = @LastUnlockedBy
    where Id = @Id;
    if @@rowcount = 0 throw 52004, 'Smart lock not found.', 1;
end
go
