-- Verixora Homes module - PostgreSQL 16+
-- Apply through the database deployment runner, never from an API request.

create schema if not exists homes;

create table if not exists homes.homes
(
    id uuid primary key,
    name varchar(150) not null,
    owner_id uuid not null,
    max_devices integer not null default 20 check (max_devices >= 1),
    created_at_utc timestamptz not null,
    constraint ck_homes_name_not_blank check (length(btrim(name)) > 0)
);

create table if not exists homes.home_members
(
    id uuid primary key,
    home_id uuid not null references homes.homes(id) on delete cascade,
    user_id uuid not null,
    role varchar(30) not null,
    joined_at_utc timestamptz not null,
    constraint uq_home_members_home_user unique (home_id, user_id),
    constraint ck_home_members_role check (role in ('Owner', 'Manager', 'Resident', 'Guest'))
);

create index if not exists ix_home_members_user_id on homes.home_members(user_id);
create index if not exists ix_homes_owner_id on homes.homes(owner_id);

-- The owner is atomically made a member of the newly created home.
create or replace function homes.fn_create_home(
    p_home_id uuid,
    p_owner_member_id uuid,
    p_owner_id uuid,
    p_name varchar(150))
returns table (id uuid, name varchar, owner_id uuid, max_devices integer, created_at_utc timestamptz)
language plpgsql
as $$
declare
    v_created_at timestamptz := now();
begin
    if nullif(btrim(p_name), '') is null then
        raise exception 'Home name is required' using errcode = '22023';
    end if;

    insert into homes.homes (id, name, owner_id, max_devices, created_at_utc)
    values (p_home_id, btrim(p_name), p_owner_id, 20, v_created_at);

    insert into homes.home_members (id, home_id, user_id, role, joined_at_utc)
    values (p_owner_member_id, p_home_id, p_owner_id, 'Owner', v_created_at);

    return query
    select h.id, h.name, h.owner_id, h.max_devices, h.created_at_utc
    from homes.homes h
    where h.id = p_home_id;
end;
$$;

create or replace function homes.fn_get_homes_for_user(p_user_id uuid)
returns table (id uuid, name varchar, owner_id uuid, role varchar, max_devices integer, created_at_utc timestamptz)
language sql
stable
as $$
    select h.id, h.name, h.owner_id, hm.role, h.max_devices, h.created_at_utc
    from homes.home_members hm
    inner join homes.homes h on h.id = hm.home_id
    where hm.user_id = p_user_id
    order by h.created_at_utc asc;
$$;
