-- Verixora Devices module - PostgreSQL 16+
-- Apply through the database deployment runner, never from an API request.

create schema if not exists devices;

create table if not exists devices.devices
(
    id uuid primary key,
    home_id uuid not null,
    hardware_id varchar(128) not null,
    name varchar(100) not null,
    mqtt_topic varchar(256) not null,
    status varchar(20) not null,
    created_at_utc timestamptz not null,
    constraint uq_devices_hardware_id unique (hardware_id),
    constraint uq_devices_mqtt_topic unique (mqtt_topic),
    constraint ck_devices_hardware_id_not_blank check (length(btrim(hardware_id)) > 0),
    constraint ck_devices_name_not_blank check (length(btrim(name)) > 0),
    constraint ck_devices_status check (status in ('Pending', 'Active', 'Online', 'Offline', 'Decommissioned'))
);

create index if not exists ix_devices_home_id on devices.devices(home_id);

create or replace function devices.fn_create_device(
    p_id uuid,
    p_home_id uuid,
    p_hardware_id varchar(128),
    p_name varchar(100),
    p_mqtt_topic varchar(256),
    p_status varchar(20),
    p_created_at_utc timestamptz)
returns table (id uuid, home_id uuid, hardware_id varchar, name varchar, mqtt_topic varchar, status varchar, created_at_utc timestamptz)
language plpgsql
as $$
begin
    if nullif(btrim(p_hardware_id), '') is null or nullif(btrim(p_name), '') is null then
        raise exception 'Hardware ID and name are required' using errcode = '22023';
    end if;

    if p_status not in ('Pending', 'Active', 'Online', 'Offline', 'Decommissioned') then
        raise exception 'Device status is invalid' using errcode = '22023';
    end if;

    insert into devices.devices (id, home_id, hardware_id, name, mqtt_topic, status, created_at_utc)
    values (p_id, p_home_id, upper(btrim(p_hardware_id)), btrim(p_name), p_mqtt_topic, p_status, p_created_at_utc);

    return query
    select d.id, d.home_id, d.hardware_id, d.name, d.mqtt_topic, d.status, d.created_at_utc
    from devices.devices d
    where d.id = p_id;
end;
$$;

create or replace function devices.fn_get_device_by_id(p_id uuid)
returns table (id uuid, home_id uuid, hardware_id varchar, name varchar, mqtt_topic varchar, status varchar, created_at_utc timestamptz)
language sql
stable
as $$
    select d.id, d.home_id, d.hardware_id, d.name, d.mqtt_topic, d.status, d.created_at_utc
    from devices.devices d
    where d.id = p_id;
$$;

create or replace function devices.fn_get_device_by_hardware_id(p_hardware_id varchar(128))
returns table (id uuid, home_id uuid, hardware_id varchar, name varchar, mqtt_topic varchar, status varchar, created_at_utc timestamptz)
language sql
stable
as $$
    select d.id, d.home_id, d.hardware_id, d.name, d.mqtt_topic, d.status, d.created_at_utc
    from devices.devices d
    where d.hardware_id = upper(btrim(p_hardware_id));
$$;

create or replace function devices.fn_get_devices_for_home(p_home_id uuid)
returns table (id uuid, home_id uuid, hardware_id varchar, name varchar, mqtt_topic varchar, status varchar, created_at_utc timestamptz)
language sql
stable
as $$
    select d.id, d.home_id, d.hardware_id, d.name, d.mqtt_topic, d.status, d.created_at_utc
    from devices.devices d
    where d.home_id = p_home_id
    order by d.created_at_utc asc;
$$;

create or replace function devices.fn_update_device_status(p_id uuid, p_status varchar(20))
returns void
language plpgsql
as $$
begin
    if p_status not in ('Pending', 'Active', 'Online', 'Offline', 'Decommissioned') then
        raise exception 'Device status is invalid' using errcode = '22023';
    end if;

    update devices.devices set status = p_status where id = p_id;
    if not found then
        raise exception 'Device not found' using errcode = 'P0002';
    end if;
end;
$$;
