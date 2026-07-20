-- Verixora registration admission controls - PostgreSQL 16+
-- The trusted-device table already enforces a globally unique device_id.
-- This routine allows the application to fail closed before an insert attempt.

create or replace function identity.fn_trusted_device_id_exists(p_device_id varchar(256))
returns boolean
language sql
stable
as $$
    select exists(
        select 1
        from identity.trusted_devices
        where device_id = btrim(p_device_id));
$$;
