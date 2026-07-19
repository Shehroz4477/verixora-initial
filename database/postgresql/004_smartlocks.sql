-- Verixora Smart Locks module - PostgreSQL 16+
-- One ESP32 controller may control exactly one exterior door in this product.

create schema if not exists smartlocks;

create table if not exists smartlocks.smart_locks
(
    id uuid primary key,
    device_id uuid not null,
    home_id uuid not null,
    name varchar(100) not null,
    status varchar(20) not null,
    requires_face boolean not null,
    last_unlocked_at_utc timestamptz null,
    last_unlocked_by uuid null,
    constraint uq_smart_locks_device_id unique (device_id),
    constraint ck_smart_locks_name_not_blank check (length(btrim(name)) > 0),
    constraint ck_smart_locks_status check (status in ('Locked', 'Unlocked', 'EmergencyLocked'))
);

create index if not exists ix_smart_locks_home_id on smartlocks.smart_locks(home_id);

create or replace function smartlocks.fn_create_smart_lock(
    p_id uuid,
    p_device_id uuid,
    p_home_id uuid,
    p_name varchar(100),
    p_status varchar(20),
    p_requires_face boolean)
returns table (id uuid, device_id uuid, home_id uuid, name varchar, status varchar, requires_face boolean, last_unlocked_at_utc timestamptz, last_unlocked_by uuid)
language plpgsql
as $$
begin
    if nullif(btrim(p_name), '') is null then
        raise exception 'Lock name is required' using errcode = '22023';
    end if;
    if p_status not in ('Locked', 'Unlocked', 'EmergencyLocked') then
        raise exception 'Lock status is invalid' using errcode = '22023';
    end if;

    insert into smartlocks.smart_locks (id, device_id, home_id, name, status, requires_face)
    values (p_id, p_device_id, p_home_id, btrim(p_name), p_status, p_requires_face);

    return query select l.id, l.device_id, l.home_id, l.name, l.status, l.requires_face, l.last_unlocked_at_utc, l.last_unlocked_by
    from smartlocks.smart_locks l where l.id = p_id;
end;
$$;

create or replace function smartlocks.fn_get_smart_lock_by_id(p_id uuid)
returns table (id uuid, device_id uuid, home_id uuid, name varchar, status varchar, requires_face boolean, last_unlocked_at_utc timestamptz, last_unlocked_by uuid)
language sql stable as $$
    select l.id, l.device_id, l.home_id, l.name, l.status, l.requires_face, l.last_unlocked_at_utc, l.last_unlocked_by
    from smartlocks.smart_locks l where l.id = p_id;
$$;

create or replace function smartlocks.fn_update_smart_lock_state(
    p_id uuid,
    p_status varchar(20),
    p_last_unlocked_at_utc timestamptz,
    p_last_unlocked_by uuid)
returns void
language plpgsql
as $$
begin
    if p_status not in ('Locked', 'Unlocked', 'EmergencyLocked') then
        raise exception 'Lock status is invalid' using errcode = '22023';
    end if;
    update smartlocks.smart_locks
    set status = p_status,
        last_unlocked_at_utc = p_last_unlocked_at_utc,
        last_unlocked_by = p_last_unlocked_by
    where id = p_id;
    if not found then raise exception 'Smart lock not found' using errcode = 'P0002'; end if;
end;
$$;
