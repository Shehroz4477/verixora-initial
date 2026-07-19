-- Verixora Homes module - SQL Server 2022+
-- Apply through the database deployment runner, never from an API request.

if schema_id(N'homes') is null execute(N'create schema homes');
go

if object_id(N'homes.Homes', N'U') is null
begin
    create table homes.Homes
    (
        Id uniqueidentifier not null primary key,
        Name nvarchar(150) not null,
        OwnerId uniqueidentifier not null,
        MaxDevices int not null constraint DF_Homes_MaxDevices default 20,
        CreatedAtUtc datetime2(7) not null,
        constraint CK_Homes_MaxDevices check (MaxDevices >= 1),
        constraint CK_Homes_NameNotBlank check (len(ltrim(rtrim(Name))) > 0)
    );
    create index IX_Homes_OwnerId on homes.Homes(OwnerId);
end
go

if object_id(N'homes.HomeMembers', N'U') is null
begin
    create table homes.HomeMembers
    (
        Id uniqueidentifier not null primary key,
        HomeId uniqueidentifier not null,
        UserId uniqueidentifier not null,
        Role nvarchar(30) not null,
        JoinedAtUtc datetime2(7) not null,
        constraint FK_HomeMembers_Homes foreign key (HomeId) references homes.Homes(Id) on delete cascade,
        constraint UQ_HomeMembers_Home_User unique (HomeId, UserId),
        constraint CK_HomeMembers_Role check (Role in (N'Owner', N'Manager', N'Resident', N'Guest'))
    );
    create index IX_HomeMembers_UserId on homes.HomeMembers(UserId);
end
go

create or alter procedure homes.sp_CreateHome
    @HomeId uniqueidentifier,
    @OwnerMemberId uniqueidentifier,
    @OwnerId uniqueidentifier,
    @Name nvarchar(150)
as
begin
    set nocount on;
    set xact_abort on;

    if nullif(ltrim(rtrim(@Name)), N'') is null
        throw 50001, 'Home name is required.', 1;

    declare @CreatedAtUtc datetime2(7) = sysutcdatetime();

    begin transaction;
        insert into homes.Homes (Id, Name, OwnerId, MaxDevices, CreatedAtUtc)
        values (@HomeId, ltrim(rtrim(@Name)), @OwnerId, 20, @CreatedAtUtc);

        insert into homes.HomeMembers (Id, HomeId, UserId, Role, JoinedAtUtc)
        values (@OwnerMemberId, @HomeId, @OwnerId, N'Owner', @CreatedAtUtc);
    commit transaction;

    select Id, Name, OwnerId, MaxDevices, CreatedAtUtc
    from homes.Homes
    where Id = @HomeId;
end
go

create or alter procedure homes.sp_GetHomesForUser
    @UserId uniqueidentifier
as
begin
    set nocount on;

    select h.Id, h.Name, h.OwnerId, hm.Role, h.MaxDevices, h.CreatedAtUtc
    from homes.HomeMembers hm
    inner join homes.Homes h on h.Id = hm.HomeId
    where hm.UserId = @UserId
    order by h.CreatedAtUtc asc;
end
go
