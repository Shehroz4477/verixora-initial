set ansi_nulls on;
go
set quoted_identifier on;
go
if object_id(N'smartlocks.LockCommands', N'U') is null
begin
    create table smartlocks.LockCommands
    (
        Id uniqueidentifier not null primary key,
        LockId uniqueidentifier not null,
        DeviceId uniqueidentifier not null,
        HomeId uniqueidentifier not null,
        RequestedBy uniqueidentifier not null,
        IdempotencyKey nvarchar(128) not null,
        CommandType nvarchar(20) not null,
        Status nvarchar(20) not null,
        RequestedAtUtc datetime2(7) not null,
        ExpiresAtUtc datetime2(7) not null,
        PublishedAtUtc datetime2(7) null,
        AcknowledgedAtUtc datetime2(7) null,
        Outcome nvarchar(20) null,
        AcknowledgementNonce nvarchar(128) null,
        Details nvarchar(500) null,
        constraint UQ_LockCommands_Idempotency unique(LockId, IdempotencyKey),
        constraint CK_LockCommands_Type check(CommandType = N'Unlock'),
        constraint CK_LockCommands_Status check(Status in (N'Queued',N'Published',N'Acknowledged',N'Failed',N'Expired')),
        constraint CK_LockCommands_Expiry check(ExpiresAtUtc > RequestedAtUtc)
    );
    create index IX_LockCommands_Dispatch on smartlocks.LockCommands(Status, ExpiresAtUtc);
    create unique index UX_LockCommands_AckNonce on smartlocks.LockCommands(DeviceId, AcknowledgementNonce) where AcknowledgementNonce is not null;
end
go
if not exists(select 1 from sys.indexes where object_id=object_id(N'smartlocks.LockCommands') and name=N'IX_LockCommands_Dispatch') create index IX_LockCommands_Dispatch on smartlocks.LockCommands(Status, ExpiresAtUtc);
if not exists(select 1 from sys.indexes where object_id=object_id(N'smartlocks.LockCommands') and name=N'UX_LockCommands_AckNonce') create unique index UX_LockCommands_AckNonce on smartlocks.LockCommands(DeviceId, AcknowledgementNonce) where AcknowledgementNonce is not null;
go
create or alter procedure smartlocks.sp_CreateOrGetLockCommand @Id uniqueidentifier,@LockId uniqueidentifier,@DeviceId uniqueidentifier,@HomeId uniqueidentifier,@RequestedBy uniqueidentifier,@IdempotencyKey nvarchar(128),@RequestedAtUtc datetime2(7),@ExpiresAtUtc datetime2(7) as
begin set nocount on; set xact_abort on; begin transaction;
 if not exists(select 1 from smartlocks.LockCommands with (updlock,holdlock) where LockId=@LockId and IdempotencyKey=ltrim(rtrim(@IdempotencyKey)))
   insert into smartlocks.LockCommands(Id,LockId,DeviceId,HomeId,RequestedBy,IdempotencyKey,CommandType,Status,RequestedAtUtc,ExpiresAtUtc) values(@Id,@LockId,@DeviceId,@HomeId,@RequestedBy,ltrim(rtrim(@IdempotencyKey)),N'Unlock',N'Queued',@RequestedAtUtc,@ExpiresAtUtc);
 select Id,LockId,DeviceId,HomeId,RequestedBy,IdempotencyKey,CommandType,Status,RequestedAtUtc,ExpiresAtUtc,PublishedAtUtc,AcknowledgedAtUtc,Outcome,AcknowledgementNonce,Details from smartlocks.LockCommands where LockId=@LockId and IdempotencyKey=ltrim(rtrim(@IdempotencyKey)); commit transaction; end
go
create or alter procedure smartlocks.sp_GetLockCommand @Id uniqueidentifier as begin set nocount on; select Id,LockId,DeviceId,HomeId,RequestedBy,IdempotencyKey,CommandType,Status,RequestedAtUtc,ExpiresAtUtc,PublishedAtUtc,AcknowledgedAtUtc,Outcome,AcknowledgementNonce,Details from smartlocks.LockCommands where Id=@Id; end
go
create or alter procedure smartlocks.sp_GetQueuedLockCommands @Maximum int as begin set nocount on; update smartlocks.LockCommands set Status=N'Expired',Details=N'Command expired before delivery' where Status=N'Queued' and ExpiresAtUtc<=sysutcdatetime(); select top (case when @Maximum between 1 and 100 then @Maximum else 100 end) Id,LockId,DeviceId,HomeId,RequestedBy,IdempotencyKey,CommandType,Status,RequestedAtUtc,ExpiresAtUtc,PublishedAtUtc,AcknowledgedAtUtc,Outcome,AcknowledgementNonce,Details from smartlocks.LockCommands where Status=N'Queued' and ExpiresAtUtc>sysutcdatetime() order by RequestedAtUtc; end
go
create or alter procedure smartlocks.sp_MarkLockCommandPublished @Id uniqueidentifier as begin set nocount on; update smartlocks.LockCommands set Status=N'Published',PublishedAtUtc=sysutcdatetime() where Id=@Id and Status=N'Queued' and ExpiresAtUtc>sysutcdatetime(); select cast(case when @@rowcount=1 then 1 else 0 end as bit); end
go
create or alter procedure smartlocks.sp_AcknowledgeLockCommand @Id uniqueidentifier,@DeviceId uniqueidentifier,@Outcome nvarchar(20),@OccurredAtUtc datetime2(7),@Nonce nvarchar(128),@Details nvarchar(500) as begin set nocount on; if @Outcome not in (N'Unlocked',N'Failed') or nullif(ltrim(rtrim(@Nonce)),N'') is null throw 52010,'Controller acknowledgement is invalid.',1; update smartlocks.LockCommands set Status=case when @Outcome=N'Unlocked' then N'Acknowledged' else N'Failed' end,AcknowledgedAtUtc=@OccurredAtUtc,Outcome=@Outcome,AcknowledgementNonce=ltrim(rtrim(@Nonce)),Details=nullif(ltrim(rtrim(@Details)),N'') where Id=@Id and DeviceId=@DeviceId and Status in (N'Queued',N'Published') and ExpiresAtUtc>=@OccurredAtUtc and AcknowledgementNonce is null; select cast(case when @@rowcount=1 then 1 else 0 end as bit); end
go
