-- System-administrator monitoring read model - SQL Server 2022+

set ansi_nulls on;
go
set quoted_identifier on;
go

create or alter procedure homes.sp_GetAllHomes
as
begin
    set nocount on;
    select Id, Name, OwnerId, N'SystemAdmin' as Role, MaxDevices, CreatedAtUtc
    from homes.Homes
    order by CreatedAtUtc asc;
end
go
