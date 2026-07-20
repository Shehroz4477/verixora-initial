-- Cryptographic mobile-device binding - SQL Server 2022+

set ansi_nulls on;
go
set quoted_identifier on;
go

if col_length(N'identity.TrustedDevices', N'DevicePublicKeySpkiBase64') is null
    alter table [identity].TrustedDevices add DevicePublicKeySpkiBase64 nvarchar(max) null;
go
if col_length(N'identity.TrustedDevices', N'DevicePublicKeyThumbprint') is null
    alter table [identity].TrustedDevices add DevicePublicKeyThumbprint nvarchar(64) null;
go

create or alter procedure [identity].sp_GetUserById @Id uniqueidentifier as begin
    set nocount on;
    select u.Id, u.PhoneNumber, u.PasswordHash, u.Email, u.EmailVerified, u.Role, u.CreatedAtUtc, d.Id as TrustedDeviceId, d.UserId as TrustedDeviceUserId, d.DeviceId as TrustedDeviceDeviceId, d.DeviceFingerprint as TrustedDeviceFingerprint, d.DevicePublicKeySpkiBase64 as TrustedDevicePublicKeySpkiBase64, d.DevicePublicKeyThumbprint as TrustedDevicePublicKeyThumbprint, d.RegisteredAtUtc as TrustedDeviceRegisteredAtUtc, d.IsActive as TrustedDeviceIsActive
    from [identity].Users u left join [identity].TrustedDevices d on d.UserId = u.Id where u.Id = @Id;
end
go
create or alter procedure [identity].sp_GetUserByPhoneNumber @PhoneNumber nvarchar(20) as begin
    set nocount on;
    select u.Id, u.PhoneNumber, u.PasswordHash, u.Email, u.EmailVerified, u.Role, u.CreatedAtUtc, d.Id as TrustedDeviceId, d.UserId as TrustedDeviceUserId, d.DeviceId as TrustedDeviceDeviceId, d.DeviceFingerprint as TrustedDeviceFingerprint, d.DevicePublicKeySpkiBase64 as TrustedDevicePublicKeySpkiBase64, d.DevicePublicKeyThumbprint as TrustedDevicePublicKeyThumbprint, d.RegisteredAtUtc as TrustedDeviceRegisteredAtUtc, d.IsActive as TrustedDeviceIsActive
    from [identity].Users u left join [identity].TrustedDevices d on d.UserId = u.Id where u.PhoneNumber = ltrim(rtrim(@PhoneNumber));
end
go
create or alter procedure [identity].sp_GetUserByEmail @Email nvarchar(256) as begin
    set nocount on;
    select u.Id, u.PhoneNumber, u.PasswordHash, u.Email, u.EmailVerified, u.Role, u.CreatedAtUtc, d.Id as TrustedDeviceId, d.UserId as TrustedDeviceUserId, d.DeviceId as TrustedDeviceDeviceId, d.DeviceFingerprint as TrustedDeviceFingerprint, d.DevicePublicKeySpkiBase64 as TrustedDevicePublicKeySpkiBase64, d.DevicePublicKeyThumbprint as TrustedDevicePublicKeyThumbprint, d.RegisteredAtUtc as TrustedDeviceRegisteredAtUtc, d.IsActive as TrustedDeviceIsActive
    from [identity].Users u left join [identity].TrustedDevices d on d.UserId = u.Id where u.EmailNormalized = lower(ltrim(rtrim(@Email)));
end
go

create or alter procedure [identity].sp_CreateUser
    @Id uniqueidentifier, @PhoneNumber nvarchar(20), @PasswordHash nvarchar(max), @Email nvarchar(256) = null, @EmailVerified bit, @Role nvarchar(20), @CreatedAtUtc datetime2(7),
    @TrustedDeviceId uniqueidentifier = null, @TrustedDeviceDeviceId nvarchar(256) = null, @TrustedDeviceFingerprint nvarchar(512) = null, @TrustedDevicePublicKeySpkiBase64 nvarchar(max) = null, @TrustedDevicePublicKeyThumbprint nvarchar(64) = null, @TrustedDeviceRegisteredAtUtc datetime2(7) = null, @TrustedDeviceIsActive bit = null
as begin
    set nocount on; set xact_abort on;
    if nullif(ltrim(rtrim(@PhoneNumber)), N'') is null throw 51101, 'Phone number is required.', 1;
    if @Role not in (N'Owner', N'Guest', N'SystemAdmin') throw 51102, 'Identity role is invalid.', 1;
    if @TrustedDeviceId is not null and (nullif(ltrim(rtrim(@TrustedDeviceDeviceId)), N'') is null or nullif(ltrim(rtrim(@TrustedDeviceFingerprint)), N'') is null or nullif(ltrim(rtrim(@TrustedDevicePublicKeySpkiBase64)), N'') is null or nullif(ltrim(rtrim(@TrustedDevicePublicKeyThumbprint)), N'') is null) throw 51103, 'Cryptographic trusted-device data is incomplete.', 1;
    begin transaction;
        insert into [identity].Users (Id, PhoneNumber, PasswordHash, Email, EmailVerified, Role, CreatedAtUtc) values (@Id, ltrim(rtrim(@PhoneNumber)), @PasswordHash, nullif(lower(ltrim(rtrim(@Email))), N''), @EmailVerified, @Role, @CreatedAtUtc);
        if @TrustedDeviceId is not null insert into [identity].TrustedDevices (Id, UserId, DeviceId, DeviceFingerprint, DevicePublicKeySpkiBase64, DevicePublicKeyThumbprint, RegisteredAtUtc, IsActive) values (@TrustedDeviceId, @Id, ltrim(rtrim(@TrustedDeviceDeviceId)), ltrim(rtrim(@TrustedDeviceFingerprint)), ltrim(rtrim(@TrustedDevicePublicKeySpkiBase64)), ltrim(rtrim(@TrustedDevicePublicKeyThumbprint)), coalesce(@TrustedDeviceRegisteredAtUtc, sysutcdatetime()), coalesce(@TrustedDeviceIsActive, 1));
    commit transaction;
end
go

create or alter procedure [identity].sp_UpdateUser
    @Id uniqueidentifier, @PasswordHash nvarchar(max), @Email nvarchar(256) = null, @EmailVerified bit, @Role nvarchar(20),
    @TrustedDeviceId uniqueidentifier = null, @TrustedDeviceDeviceId nvarchar(256) = null, @TrustedDeviceFingerprint nvarchar(512) = null, @TrustedDevicePublicKeySpkiBase64 nvarchar(max) = null, @TrustedDevicePublicKeyThumbprint nvarchar(64) = null, @TrustedDeviceRegisteredAtUtc datetime2(7) = null, @TrustedDeviceIsActive bit = null
as begin
    set nocount on; set xact_abort on;
    if @Role not in (N'Owner', N'Guest', N'SystemAdmin') throw 51104, 'Identity role is invalid.', 1;
    begin transaction;
        update [identity].Users set PasswordHash = @PasswordHash, Email = nullif(lower(ltrim(rtrim(@Email))), N''), EmailVerified = @EmailVerified, Role = @Role where Id = @Id;
        if @@rowcount = 0 throw 51105, 'User not found.', 1;
        if @TrustedDeviceId is not null
        begin
            if exists(select 1 from [identity].TrustedDevices where UserId = @Id)
                update [identity].TrustedDevices set Id = @TrustedDeviceId, DeviceId = ltrim(rtrim(@TrustedDeviceDeviceId)), DeviceFingerprint = ltrim(rtrim(@TrustedDeviceFingerprint)), DevicePublicKeySpkiBase64 = coalesce(nullif(ltrim(rtrim(@TrustedDevicePublicKeySpkiBase64)), N''), DevicePublicKeySpkiBase64), DevicePublicKeyThumbprint = coalesce(nullif(ltrim(rtrim(@TrustedDevicePublicKeyThumbprint)), N''), DevicePublicKeyThumbprint), RegisteredAtUtc = coalesce(@TrustedDeviceRegisteredAtUtc, RegisteredAtUtc), IsActive = coalesce(@TrustedDeviceIsActive, IsActive) where UserId = @Id;
            else
                insert into [identity].TrustedDevices (Id, UserId, DeviceId, DeviceFingerprint, DevicePublicKeySpkiBase64, DevicePublicKeyThumbprint, RegisteredAtUtc, IsActive) values (@TrustedDeviceId, @Id, ltrim(rtrim(@TrustedDeviceDeviceId)), ltrim(rtrim(@TrustedDeviceFingerprint)), nullif(ltrim(rtrim(@TrustedDevicePublicKeySpkiBase64)), N''), nullif(ltrim(rtrim(@TrustedDevicePublicKeyThumbprint)), N''), coalesce(@TrustedDeviceRegisteredAtUtc, sysutcdatetime()), coalesce(@TrustedDeviceIsActive, 1));
        end
    commit transaction;
end
go
