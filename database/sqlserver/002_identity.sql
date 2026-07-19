-- Verixora Identity module - SQL Server 2022+
-- Apply through the database deployment runner, never from an API request.

set ansi_nulls on;
go
set quoted_identifier on;
go

if schema_id(N'identity') is null execute(N'create schema [identity]');
go

if object_id(N'identity.Users', N'U') is null
begin
    create table [identity].Users
    (
        Id uniqueidentifier not null primary key,
        PhoneNumber nvarchar(20) not null,
        PasswordHash nvarchar(max) not null,
        Email nvarchar(256) null,
        EmailNormalized as lower(Email) persisted,
        EmailVerified bit not null constraint DF_IdentityUsers_EmailVerified default 0,
        Role nvarchar(20) not null,
        CreatedAtUtc datetime2(7) not null,
        constraint UQ_IdentityUsers_PhoneNumber unique (PhoneNumber),
        constraint CK_IdentityUsers_PhoneNotBlank check (len(ltrim(rtrim(PhoneNumber))) > 0),
        constraint CK_IdentityUsers_Role check (Role in (N'Owner', N'Guest', N'SystemAdmin'))
    );
    create unique index UX_IdentityUsers_EmailNormalized on [identity].Users(EmailNormalized) where Email is not null;
end
go

if not exists (select 1 from sys.indexes where object_id = object_id(N'identity.Users') and name = N'UX_IdentityUsers_EmailNormalized')
    create unique index UX_IdentityUsers_EmailNormalized on [identity].Users(EmailNormalized) where Email is not null;
go

if object_id(N'identity.TrustedDevices', N'U') is null
begin
    create table [identity].TrustedDevices
    (
        Id uniqueidentifier not null primary key,
        UserId uniqueidentifier not null,
        DeviceId nvarchar(256) not null,
        DeviceFingerprint nvarchar(512) not null,
        RegisteredAtUtc datetime2(7) not null,
        IsActive bit not null,
        constraint FK_IdentityTrustedDevices_Users foreign key (UserId) references [identity].Users(Id) on delete cascade,
        constraint UQ_IdentityTrustedDevices_UserId unique (UserId),
        constraint UQ_IdentityTrustedDevices_DeviceId unique (DeviceId),
        constraint CK_IdentityTrustedDevices_DeviceIdNotBlank check (len(ltrim(rtrim(DeviceId))) > 0),
        constraint CK_IdentityTrustedDevices_FingerprintNotBlank check (len(ltrim(rtrim(DeviceFingerprint))) > 0)
    );
end
go

if object_id(N'identity.FaceEmbeddings', N'U') is null
begin
    create table [identity].FaceEmbeddings
    (
        Id uniqueidentifier not null primary key,
        UserId uniqueidentifier not null,
        EmbeddingCiphertext varbinary(max) not null,
        Iv varbinary(64) not null,
        CreatedAtUtc datetime2(7) not null,
        constraint FK_IdentityFaceEmbeddings_Users foreign key (UserId) references [identity].Users(Id) on delete cascade
    );
    create index IX_IdentityFaceEmbeddings_UserId on [identity].FaceEmbeddings(UserId);
end
go

create or alter procedure [identity].sp_GetUserById
    @Id uniqueidentifier
as
begin
    set nocount on;
    select u.Id, u.PhoneNumber, u.PasswordHash, u.Email, u.EmailVerified, u.Role, u.CreatedAtUtc,
           d.Id as TrustedDeviceId, d.UserId as TrustedDeviceUserId, d.DeviceId as TrustedDeviceDeviceId,
           d.DeviceFingerprint as TrustedDeviceFingerprint, d.RegisteredAtUtc as TrustedDeviceRegisteredAtUtc,
           d.IsActive as TrustedDeviceIsActive
    from [identity].Users u
    left join [identity].TrustedDevices d on d.UserId = u.Id
    where u.Id = @Id;
end
go

create or alter procedure [identity].sp_GetUserByPhoneNumber
    @PhoneNumber nvarchar(20)
as
begin
    set nocount on;
    select u.Id, u.PhoneNumber, u.PasswordHash, u.Email, u.EmailVerified, u.Role, u.CreatedAtUtc,
           d.Id as TrustedDeviceId, d.UserId as TrustedDeviceUserId, d.DeviceId as TrustedDeviceDeviceId,
           d.DeviceFingerprint as TrustedDeviceFingerprint, d.RegisteredAtUtc as TrustedDeviceRegisteredAtUtc,
           d.IsActive as TrustedDeviceIsActive
    from [identity].Users u
    left join [identity].TrustedDevices d on d.UserId = u.Id
    where u.PhoneNumber = ltrim(rtrim(@PhoneNumber));
end
go

create or alter procedure [identity].sp_GetUserByEmail
    @Email nvarchar(256)
as
begin
    set nocount on;
    select u.Id, u.PhoneNumber, u.PasswordHash, u.Email, u.EmailVerified, u.Role, u.CreatedAtUtc,
           d.Id as TrustedDeviceId, d.UserId as TrustedDeviceUserId, d.DeviceId as TrustedDeviceDeviceId,
           d.DeviceFingerprint as TrustedDeviceFingerprint, d.RegisteredAtUtc as TrustedDeviceRegisteredAtUtc,
           d.IsActive as TrustedDeviceIsActive
    from [identity].Users u
    left join [identity].TrustedDevices d on d.UserId = u.Id
    where u.EmailNormalized = lower(ltrim(rtrim(@Email)));
end
go

create or alter procedure [identity].sp_PhoneNumberExists
    @PhoneNumber nvarchar(20)
as
begin
    set nocount on;
    select cast(case when exists(select 1 from [identity].Users where PhoneNumber = ltrim(rtrim(@PhoneNumber))) then 1 else 0 end as bit) as Value;
end
go

create or alter procedure [identity].sp_CreateUser
    @Id uniqueidentifier,
    @PhoneNumber nvarchar(20),
    @PasswordHash nvarchar(max),
    @Email nvarchar(256) = null,
    @EmailVerified bit,
    @Role nvarchar(20),
    @CreatedAtUtc datetime2(7),
    @TrustedDeviceId uniqueidentifier = null,
    @TrustedDeviceDeviceId nvarchar(256) = null,
    @TrustedDeviceFingerprint nvarchar(512) = null,
    @TrustedDeviceRegisteredAtUtc datetime2(7) = null,
    @TrustedDeviceIsActive bit = null
as
begin
    set nocount on;
    set xact_abort on;

    if nullif(ltrim(rtrim(@PhoneNumber)), N'') is null throw 50001, 'Phone number is required.', 1;
    if @Role not in (N'Owner', N'Guest', N'SystemAdmin') throw 50002, 'Identity role is invalid.', 1;
    if @TrustedDeviceId is not null and (nullif(ltrim(rtrim(@TrustedDeviceDeviceId)), N'') is null or nullif(ltrim(rtrim(@TrustedDeviceFingerprint)), N'') is null)
        throw 50003, 'Trusted device data is incomplete.', 1;

    begin transaction;
        insert into [identity].Users (Id, PhoneNumber, PasswordHash, Email, EmailVerified, Role, CreatedAtUtc)
        values (@Id, ltrim(rtrim(@PhoneNumber)), @PasswordHash, nullif(lower(ltrim(rtrim(@Email))), N''), @EmailVerified, @Role, @CreatedAtUtc);

        if @TrustedDeviceId is not null
            insert into [identity].TrustedDevices (Id, UserId, DeviceId, DeviceFingerprint, RegisteredAtUtc, IsActive)
            values (@TrustedDeviceId, @Id, ltrim(rtrim(@TrustedDeviceDeviceId)), ltrim(rtrim(@TrustedDeviceFingerprint)),
                    coalesce(@TrustedDeviceRegisteredAtUtc, sysutcdatetime()), coalesce(@TrustedDeviceIsActive, 1));
    commit transaction;
end
go

create or alter procedure [identity].sp_UpdateUser
    @Id uniqueidentifier,
    @PasswordHash nvarchar(max),
    @Email nvarchar(256) = null,
    @EmailVerified bit,
    @Role nvarchar(20),
    @TrustedDeviceId uniqueidentifier = null,
    @TrustedDeviceDeviceId nvarchar(256) = null,
    @TrustedDeviceFingerprint nvarchar(512) = null,
    @TrustedDeviceRegisteredAtUtc datetime2(7) = null,
    @TrustedDeviceIsActive bit = null
as
begin
    set nocount on;
    set xact_abort on;

    if @Role not in (N'Owner', N'Guest', N'SystemAdmin') throw 50004, 'Identity role is invalid.', 1;

    begin transaction;
        update [identity].Users
        set PasswordHash = @PasswordHash,
            Email = nullif(lower(ltrim(rtrim(@Email))), N''),
            EmailVerified = @EmailVerified,
            Role = @Role
        where Id = @Id;

        if @@rowcount = 0 throw 50005, 'User not found.', 1;

        if @TrustedDeviceId is not null
        begin
            if exists(select 1 from [identity].TrustedDevices where UserId = @Id)
                update [identity].TrustedDevices
                set Id = @TrustedDeviceId,
                    DeviceId = ltrim(rtrim(@TrustedDeviceDeviceId)),
                    DeviceFingerprint = ltrim(rtrim(@TrustedDeviceFingerprint)),
                    RegisteredAtUtc = coalesce(@TrustedDeviceRegisteredAtUtc, RegisteredAtUtc),
                    IsActive = coalesce(@TrustedDeviceIsActive, IsActive)
                where UserId = @Id;
            else
                insert into [identity].TrustedDevices (Id, UserId, DeviceId, DeviceFingerprint, RegisteredAtUtc, IsActive)
                values (@TrustedDeviceId, @Id, ltrim(rtrim(@TrustedDeviceDeviceId)), ltrim(rtrim(@TrustedDeviceFingerprint)),
                        coalesce(@TrustedDeviceRegisteredAtUtc, sysutcdatetime()), coalesce(@TrustedDeviceIsActive, 1));
        end
    commit transaction;
end
go
