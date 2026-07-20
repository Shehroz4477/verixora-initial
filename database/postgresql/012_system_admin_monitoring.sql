-- System-administrator monitoring read model - PostgreSQL 16+

create or replace function homes.fn_get_all_homes()
returns table (id uuid, name varchar, owner_id uuid, role varchar, max_devices integer, created_at_utc timestamptz)
language sql stable as $$
    select h.id, h.name, h.owner_id, 'SystemAdmin'::varchar, h.max_devices, h.created_at_utc
    from homes.homes h
    order by h.created_at_utc asc;
$$;
