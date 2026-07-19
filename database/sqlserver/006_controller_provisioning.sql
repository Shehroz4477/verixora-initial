if col_length('devices.Devices','ProvisioningTokenHash') is null alter table devices.Devices add ProvisioningTokenHash nvarchar(128) null;
if col_length('devices.Devices','ProvisioningExpiresAtUtc') is null alter table devices.Devices add ProvisioningExpiresAtUtc datetime2(7) null;
if col_length('devices.Devices','ControllerPublicKeyThumbprint') is null alter table devices.Devices add ControllerPublicKeyThumbprint nvarchar(128) null;
if col_length('devices.Devices','HardwareAttestationSubject') is null alter table devices.Devices add HardwareAttestationSubject nvarchar(256) null;
if col_length('devices.Devices','ProvisionedAtUtc') is null alter table devices.Devices add ProvisionedAtUtc datetime2(7) null;
go
create or alter procedure devices.sp_CreateDevice @Id uniqueidentifier,@HomeId uniqueidentifier,@HardwareId nvarchar(128),@Name nvarchar(100),@MqttTopic nvarchar(256),@Status nvarchar(20),@CreatedAtUtc datetime2(7),@ProvisioningTokenHash nvarchar(128)=null,@ProvisioningExpiresAtUtc datetime2(7)=null as
begin set nocount on;
 if @Status=N'Pending' and (nullif(ltrim(rtrim(@ProvisioningTokenHash)),N'') is null or @ProvisioningExpiresAtUtc<=sysutcdatetime()) throw 51005,'Pending controller provisioning data is required.',1;
 insert into devices.Devices(Id,HomeId,HardwareId,Name,MqttTopic,Status,CreatedAtUtc,ProvisioningTokenHash,ProvisioningExpiresAtUtc) values(@Id,@HomeId,upper(ltrim(rtrim(@HardwareId))),ltrim(rtrim(@Name)),@MqttTopic,@Status,@CreatedAtUtc,@ProvisioningTokenHash,@ProvisioningExpiresAtUtc);
 select Id,HomeId,HardwareId,Name,MqttTopic,Status,CreatedAtUtc,ProvisioningTokenHash,ProvisioningExpiresAtUtc,ControllerPublicKeyThumbprint,HardwareAttestationSubject,ProvisionedAtUtc from devices.Devices where Id=@Id; end
go
create or alter procedure devices.sp_GetDeviceById @Id uniqueidentifier as begin set nocount on; select Id,HomeId,HardwareId,Name,MqttTopic,Status,CreatedAtUtc,ProvisioningTokenHash,ProvisioningExpiresAtUtc,ControllerPublicKeyThumbprint,HardwareAttestationSubject,ProvisionedAtUtc from devices.Devices where Id=@Id; end
go
create or alter procedure devices.sp_GetDeviceByHardwareId @HardwareId nvarchar(128) as begin set nocount on; select Id,HomeId,HardwareId,Name,MqttTopic,Status,CreatedAtUtc,ProvisioningTokenHash,ProvisioningExpiresAtUtc,ControllerPublicKeyThumbprint,HardwareAttestationSubject,ProvisionedAtUtc from devices.Devices where HardwareId=upper(ltrim(rtrim(@HardwareId))); end
go
create or alter procedure devices.sp_GetDevicesForHome @HomeId uniqueidentifier as begin set nocount on; select Id,HomeId,HardwareId,Name,MqttTopic,Status,CreatedAtUtc,ProvisioningTokenHash,ProvisioningExpiresAtUtc,ControllerPublicKeyThumbprint,HardwareAttestationSubject,ProvisionedAtUtc from devices.Devices where HomeId=@HomeId order by CreatedAtUtc; end
go
create or alter procedure devices.sp_CompleteDeviceProvisioning @Id uniqueidentifier,@ProvisioningTokenHash nvarchar(128),@ControllerPublicKeyThumbprint nvarchar(128),@HardwareAttestationSubject nvarchar(256) as begin set nocount on;
 if nullif(ltrim(rtrim(@ControllerPublicKeyThumbprint)),N'') is null or nullif(ltrim(rtrim(@HardwareAttestationSubject)),N'') is null throw 51006,'Controller attestation is required.',1;
 update devices.Devices set Status=N'Active',ProvisioningTokenHash=null,ProvisioningExpiresAtUtc=null,ControllerPublicKeyThumbprint=@ControllerPublicKeyThumbprint,HardwareAttestationSubject=@HardwareAttestationSubject,ProvisionedAtUtc=sysutcdatetime() where Id=@Id and Status=N'Pending' and ProvisioningTokenHash=@ProvisioningTokenHash and ProvisioningExpiresAtUtc>sysutcdatetime() and ControllerPublicKeyThumbprint is null;
 select cast(case when @@rowcount=1 then 1 else 0 end as bit); end
go
