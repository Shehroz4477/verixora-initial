if col_length('devices.Devices','ControllerPublicKeySpkiBase64') is null alter table devices.Devices add ControllerPublicKeySpkiBase64 nvarchar(256) null;
go
create or alter procedure devices.sp_CreateDevice @Id uniqueidentifier,@HomeId uniqueidentifier,@HardwareId nvarchar(128),@Name nvarchar(100),@MqttTopic nvarchar(256),@Status nvarchar(20),@CreatedAtUtc datetime2(7),@ProvisioningTokenHash nvarchar(128)=null,@ProvisioningExpiresAtUtc datetime2(7)=null as
begin set nocount on;
 if @Status=N'Pending' and (nullif(ltrim(rtrim(@ProvisioningTokenHash)),N'') is null or @ProvisioningExpiresAtUtc<=sysutcdatetime()) throw 51005,'Pending controller provisioning data is required.',1;
 insert into devices.Devices(Id,HomeId,HardwareId,Name,MqttTopic,Status,CreatedAtUtc,ProvisioningTokenHash,ProvisioningExpiresAtUtc) values(@Id,@HomeId,upper(ltrim(rtrim(@HardwareId))),ltrim(rtrim(@Name)),@MqttTopic,@Status,@CreatedAtUtc,@ProvisioningTokenHash,@ProvisioningExpiresAtUtc);
 select Id,HomeId,HardwareId,Name,MqttTopic,Status,CreatedAtUtc,ProvisioningTokenHash,ProvisioningExpiresAtUtc,ControllerPublicKeyThumbprint,ControllerPublicKeySpkiBase64,HardwareAttestationSubject,ProvisionedAtUtc from devices.Devices where Id=@Id; end
go
create or alter procedure devices.sp_GetDeviceById @Id uniqueidentifier as begin set nocount on; select Id,HomeId,HardwareId,Name,MqttTopic,Status,CreatedAtUtc,ProvisioningTokenHash,ProvisioningExpiresAtUtc,ControllerPublicKeyThumbprint,ControllerPublicKeySpkiBase64,HardwareAttestationSubject,ProvisionedAtUtc from devices.Devices where Id=@Id; end
go
create or alter procedure devices.sp_GetDeviceByHardwareId @HardwareId nvarchar(128) as begin set nocount on; select Id,HomeId,HardwareId,Name,MqttTopic,Status,CreatedAtUtc,ProvisioningTokenHash,ProvisioningExpiresAtUtc,ControllerPublicKeyThumbprint,ControllerPublicKeySpkiBase64,HardwareAttestationSubject,ProvisionedAtUtc from devices.Devices where HardwareId=upper(ltrim(rtrim(@HardwareId))); end
go
create or alter procedure devices.sp_GetDevicesForHome @HomeId uniqueidentifier as begin set nocount on; select Id,HomeId,HardwareId,Name,MqttTopic,Status,CreatedAtUtc,ProvisioningTokenHash,ProvisioningExpiresAtUtc,ControllerPublicKeyThumbprint,ControllerPublicKeySpkiBase64,HardwareAttestationSubject,ProvisionedAtUtc from devices.Devices where HomeId=@HomeId order by CreatedAtUtc; end
go
create or alter procedure devices.sp_CompleteDeviceProvisioningV2 @Id uniqueidentifier,@ProvisioningTokenHash nvarchar(128),@ControllerPublicKeyThumbprint nvarchar(128),@ControllerPublicKeySpkiBase64 nvarchar(256),@HardwareAttestationSubject nvarchar(256) as begin set nocount on;
 if nullif(ltrim(rtrim(@ControllerPublicKeyThumbprint)),N'') is null or nullif(ltrim(rtrim(@ControllerPublicKeySpkiBase64)),N'') is null or nullif(ltrim(rtrim(@HardwareAttestationSubject)),N'') is null throw 51006,'Controller attestation is required.',1;
 update devices.Devices set Status=N'Active',ProvisioningTokenHash=null,ProvisioningExpiresAtUtc=null,ControllerPublicKeyThumbprint=@ControllerPublicKeyThumbprint,ControllerPublicKeySpkiBase64=@ControllerPublicKeySpkiBase64,HardwareAttestationSubject=@HardwareAttestationSubject,ProvisionedAtUtc=sysutcdatetime() where Id=@Id and Status=N'Pending' and ProvisioningTokenHash=@ProvisioningTokenHash and ProvisioningExpiresAtUtc>sysutcdatetime() and ControllerPublicKeyThumbprint is null;
 select cast(case when @@rowcount=1 then 1 else 0 end as bit); end
go
