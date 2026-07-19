-- Verixora Devices module - SQL Server 2022+
-- Apply through the database deployment runner, never from an API request.

set ansi_nulls on;
go
set quoted_identifier on;
go

if schema_id(N'devices') is null execute(N'create schema devices');
go

if object_id(N'devices.Devices', N'U') is null
begin
    create table devices.Devices
    (
        Id uniqueidentifier not null primary key,
        HomeId uniqueidentifier not null,
        HardwareId nvarchar(128) not null,
        Name nvarchar(100) not null,
        MqttTopic nvarchar(256) not null,
        Status nvarchar(20) not null,
        CreatedAtUtc datetime2(7) not null,
        constraint UQ_Devices_HardwareId unique (HardwareId),
        constraint UQ_Devices_MqttTopic unique (MqttTopic),
        constraint CK_Devices_HardwareIdNotBlank check (len(ltrim(rtrim(HardwareId))) > 0),
        constraint CK_Devices_NameNotBlank check (len(ltrim(rtrim(Name))) > 0),
        constraint CK_Devices_Status check (Status in (N'Pending', N'Active', N'Online', N'Offline', N'Decommissioned'))
    );
    create index IX_Devices_HomeId on devices.Devices(HomeId);
end
go

create or alter procedure devices.sp_CreateDevice
    @Id uniqueidentifier,
    @HomeId uniqueidentifier,
    @HardwareId nvarchar(128),
    @Name nvarchar(100),
    @MqttTopic nvarchar(256),
    @Status nvarchar(20),
    @CreatedAtUtc datetime2(7)
as
begin
    set nocount on;

    if nullif(ltrim(rtrim(@HardwareId)), N'') is null or nullif(ltrim(rtrim(@Name)), N'') is null
        throw 51001, 'Hardware ID and name are required.', 1;
    if @Status not in (N'Pending', N'Active', N'Online', N'Offline', N'Decommissioned')
        throw 51002, 'Device status is invalid.', 1;

    insert into devices.Devices (Id, HomeId, HardwareId, Name, MqttTopic, Status, CreatedAtUtc)
    values (@Id, @HomeId, upper(ltrim(rtrim(@HardwareId))), ltrim(rtrim(@Name)), @MqttTopic, @Status, @CreatedAtUtc);

    select Id, HomeId, HardwareId, Name, MqttTopic, Status, CreatedAtUtc
    from devices.Devices where Id = @Id;
end
go

create or alter procedure devices.sp_GetDeviceById @Id uniqueidentifier
as
begin
    set nocount on;
    select Id, HomeId, HardwareId, Name, MqttTopic, Status, CreatedAtUtc from devices.Devices where Id = @Id;
end
go

create or alter procedure devices.sp_GetDeviceByHardwareId @HardwareId nvarchar(128)
as
begin
    set nocount on;
    select Id, HomeId, HardwareId, Name, MqttTopic, Status, CreatedAtUtc from devices.Devices where HardwareId = upper(ltrim(rtrim(@HardwareId)));
end
go

create or alter procedure devices.sp_GetDevicesForHome @HomeId uniqueidentifier
as
begin
    set nocount on;
    select Id, HomeId, HardwareId, Name, MqttTopic, Status, CreatedAtUtc from devices.Devices where HomeId = @HomeId order by CreatedAtUtc asc;
end
go

create or alter procedure devices.sp_UpdateDeviceStatus @Id uniqueidentifier, @Status nvarchar(20)
as
begin
    set nocount on;
    if @Status not in (N'Pending', N'Active', N'Online', N'Offline', N'Decommissioned')
        throw 51003, 'Device status is invalid.', 1;
    update devices.Devices set Status = @Status where Id = @Id;
    if @@rowcount = 0 throw 51004, 'Device not found.', 1;
end
go
