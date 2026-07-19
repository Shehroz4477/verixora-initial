-- Read model routine used by authenticated mobile and web clients. Authorization remains in application code.
create or replace function smartlocks.fn_get_smart_locks_for_home(p_home_id uuid)
returns table (id uuid, device_id uuid, home_id uuid, name varchar, status varchar, requires_face boolean, last_unlocked_at_utc timestamptz, last_unlocked_by uuid)
language sql stable as $$
    select l.id, l.device_id, l.home_id, l.name, l.status, l.requires_face, l.last_unlocked_at_utc, l.last_unlocked_by
    from smartlocks.smart_locks l
    where l.home_id = p_home_id
    order by l.name;
$$;
