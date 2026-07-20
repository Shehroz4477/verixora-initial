-- Durable, short-lived controller command outbox. Controller firmware must treat command id + expiry as idempotent.
create table if not exists smartlocks.lock_commands
(
    id uuid primary key,
    lock_id uuid not null,
    device_id uuid not null,
    home_id uuid not null,
    requested_by uuid not null,
    idempotency_key varchar(128) not null,
    command_type varchar(20) not null,
    status varchar(20) not null,
    requested_at_utc timestamptz not null,
    expires_at_utc timestamptz not null,
    published_at_utc timestamptz null,
    acknowledged_at_utc timestamptz null,
    outcome varchar(20) null,
    acknowledgement_nonce varchar(128) null,
    details varchar(500) null,
    constraint uq_lock_commands_idempotency unique(lock_id, idempotency_key),
    constraint uq_lock_commands_ack_nonce unique(device_id, acknowledgement_nonce),
    constraint ck_lock_commands_type check(command_type = 'Unlock'),
    constraint ck_lock_commands_status check(status in ('Queued','Published','Acknowledged','Failed','Expired')),
    constraint ck_lock_commands_expiry check(expires_at_utc > requested_at_utc)
);
create index if not exists ix_lock_commands_dispatch on smartlocks.lock_commands(status, expires_at_utc);

create or replace function smartlocks.fn_create_or_get_lock_command(p_id uuid,p_lock_id uuid,p_device_id uuid,p_home_id uuid,p_requested_by uuid,p_idempotency_key varchar(128),p_requested_at_utc timestamptz,p_expires_at_utc timestamptz)
returns table(id uuid,lock_id uuid,device_id uuid,home_id uuid,requested_by uuid,idempotency_key varchar,command_type varchar,status varchar,requested_at_utc timestamptz,expires_at_utc timestamptz,published_at_utc timestamptz,acknowledged_at_utc timestamptz,outcome varchar,acknowledgement_nonce varchar,details varchar)
language plpgsql as $$
begin
  insert into smartlocks.lock_commands(id,lock_id,device_id,home_id,requested_by,idempotency_key,command_type,status,requested_at_utc,expires_at_utc)
  values(p_id,p_lock_id,p_device_id,p_home_id,p_requested_by,btrim(p_idempotency_key),'Unlock','Queued',p_requested_at_utc,p_expires_at_utc)
  on conflict on constraint uq_lock_commands_idempotency do nothing;
  return query select c.id,c.lock_id,c.device_id,c.home_id,c.requested_by,c.idempotency_key,c.command_type,c.status,c.requested_at_utc,c.expires_at_utc,c.published_at_utc,c.acknowledged_at_utc,c.outcome,c.acknowledgement_nonce,c.details from smartlocks.lock_commands c where c.lock_id=p_lock_id and c.idempotency_key=btrim(p_idempotency_key);
end; $$;

create or replace function smartlocks.fn_get_lock_command(p_id uuid)
returns table(id uuid,lock_id uuid,device_id uuid,home_id uuid,requested_by uuid,idempotency_key varchar,command_type varchar,status varchar,requested_at_utc timestamptz,expires_at_utc timestamptz,published_at_utc timestamptz,acknowledged_at_utc timestamptz,outcome varchar,acknowledgement_nonce varchar,details varchar)
language sql stable as $$ select c.id,c.lock_id,c.device_id,c.home_id,c.requested_by,c.idempotency_key,c.command_type,c.status,c.requested_at_utc,c.expires_at_utc,c.published_at_utc,c.acknowledged_at_utc,c.outcome,c.acknowledgement_nonce,c.details from smartlocks.lock_commands c where c.id=p_id; $$;

create or replace function smartlocks.fn_get_queued_lock_commands(p_maximum integer)
returns table(id uuid,lock_id uuid,device_id uuid,home_id uuid,requested_by uuid,idempotency_key varchar,command_type varchar,status varchar,requested_at_utc timestamptz,expires_at_utc timestamptz,published_at_utc timestamptz,acknowledged_at_utc timestamptz,outcome varchar,acknowledgement_nonce varchar,details varchar)
language plpgsql as $$
begin
  update smartlocks.lock_commands set status='Expired',details='Command expired before delivery' where status='Queued' and expires_at_utc <= now();
  return query select c.id,c.lock_id,c.device_id,c.home_id,c.requested_by,c.idempotency_key,c.command_type,c.status,c.requested_at_utc,c.expires_at_utc,c.published_at_utc,c.acknowledged_at_utc,c.outcome,c.acknowledgement_nonce,c.details from smartlocks.lock_commands c where c.status='Queued' and c.expires_at_utc>now() order by c.requested_at_utc limit greatest(1,least(p_maximum,100));
end; $$;

create or replace function smartlocks.fn_mark_lock_command_published(p_id uuid) returns boolean language plpgsql as $$
begin
  update smartlocks.lock_commands set status='Published',published_at_utc=now() where id=p_id and status='Queued' and expires_at_utc>now();
  return found;
end; $$;

create or replace function smartlocks.fn_acknowledge_lock_command(p_id uuid,p_device_id uuid,p_outcome varchar(20),p_occurred_at_utc timestamptz,p_nonce varchar(128),p_details varchar(500)) returns boolean language plpgsql as $$
begin
  if p_outcome not in ('Unlocked','Failed') or nullif(btrim(p_nonce),'') is null then raise exception 'Controller acknowledgement is invalid' using errcode='22023'; end if;
  update smartlocks.lock_commands set status=case when p_outcome='Unlocked' then 'Acknowledged' else 'Failed' end,acknowledged_at_utc=p_occurred_at_utc,outcome=p_outcome,acknowledgement_nonce=btrim(p_nonce),details=nullif(btrim(p_details),'') where id=p_id and device_id=p_device_id and status in ('Queued','Published') and expires_at_utc>=p_occurred_at_utc and acknowledgement_nonce is null;
  return found;
end; $$;
