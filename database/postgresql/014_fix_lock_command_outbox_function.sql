-- Fix PL/pgSQL output-column shadowing in the lock command outbox routines.
-- PostgreSQL treats output table column names as variables inside PL/pgSQL;
-- target table columns must therefore be qualified in DML predicates.

create or replace function smartlocks.fn_get_queued_lock_commands(p_maximum integer)
returns table(id uuid,lock_id uuid,device_id uuid,home_id uuid,requested_by uuid,idempotency_key varchar,command_type varchar,status varchar,requested_at_utc timestamptz,expires_at_utc timestamptz,published_at_utc timestamptz,acknowledged_at_utc timestamptz,outcome varchar,acknowledgement_nonce varchar,details varchar)
language plpgsql as $$
begin
  update smartlocks.lock_commands as command_row
  set status='Expired',details='Command expired before delivery'
  where command_row.status='Queued' and command_row.expires_at_utc <= now();

  return query
  select c.id,c.lock_id,c.device_id,c.home_id,c.requested_by,c.idempotency_key,c.command_type,c.status,c.requested_at_utc,c.expires_at_utc,c.published_at_utc,c.acknowledged_at_utc,c.outcome,c.acknowledgement_nonce,c.details
  from smartlocks.lock_commands c
  where c.status='Queued' and c.expires_at_utc > now()
  order by c.requested_at_utc
  limit greatest(1,least(p_maximum,100));
end; $$;

create or replace function smartlocks.fn_mark_lock_command_published(p_id uuid)
returns boolean
language plpgsql as $$
begin
  update smartlocks.lock_commands as command_row
  set status='Published',published_at_utc=now()
  where command_row.id=p_id and command_row.status='Queued' and command_row.expires_at_utc > now();
  return found;
end; $$;

create or replace function smartlocks.fn_acknowledge_lock_command(p_id uuid,p_device_id uuid,p_outcome varchar(20),p_occurred_at_utc timestamptz,p_nonce varchar(128),p_details varchar(500))
returns boolean
language plpgsql as $$
begin
  if p_outcome not in ('Unlocked','Failed') or nullif(btrim(p_nonce),'') is null then
    raise exception 'Controller acknowledgement is invalid' using errcode='22023';
  end if;

  update smartlocks.lock_commands as command_row
  set status=case when p_outcome='Unlocked' then 'Acknowledged' else 'Failed' end,
      acknowledged_at_utc=p_occurred_at_utc,
      outcome=p_outcome,
      acknowledgement_nonce=btrim(p_nonce),
      details=nullif(btrim(p_details),'')
  where command_row.id=p_id
    and command_row.device_id=p_device_id
    and command_row.status in ('Queued','Published')
    and command_row.expires_at_utc >= p_occurred_at_utc
    and command_row.acknowledgement_nonce is null;
  return found;
end; $$;
