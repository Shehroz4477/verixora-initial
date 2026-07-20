-- Verixora registration admission controls - SQL Server 2022+
-- The trusted-device table already enforces a globally unique DeviceId.
-- This procedure allows the application to fail closed before an insert attempt.

create or alter procedure [identity].[sp_TrustedDeviceIdExists]
    @DeviceId nvarchar(256)
as
begin
    set nocount on;
    select cast(case when exists(
        select 1
        from [identity].[TrustedDevices]
        where DeviceId = ltrim(rtrim(@DeviceId))) then 1 else 0 end as bit) as Value;
end
go
