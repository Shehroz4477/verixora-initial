create or alter procedure smartlocks.sp_GetSmartLocksForHome @HomeId uniqueidentifier
as
begin
    set nocount on;
    select Id, DeviceId, HomeId, Name, Status, RequiresFace, LastUnlockedAtUtc, LastUnlockedBy
    from smartlocks.SmartLocks
    where HomeId = @HomeId
    order by Name;
end
go
