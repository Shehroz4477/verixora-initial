-- Verixora Audit Logs module - PostgreSQL 16+
-- Append-only operational security trail. API clients never write this table directly.

create schema if not exists auditlogs;

create table if not exists auditlogs.audit_logs
(
    id uuid primary key,
    home_id uuid not null,
    user_id uuid not null,
    device_id uuid not null,
    action varchar(100) not null,
    timestamp_utc timestamptz not null,
    result boolean not null,
    details varchar(1000) null,
    constraint ck_audit_logs_action_not_blank check (length(btrim(action)) > 0)
);

create index if not exists ix_audit_logs_home_timestamp on auditlogs.audit_logs(home_id, timestamp_utc desc);
create index if not exists ix_audit_logs_device_timestamp on auditlogs.audit_logs(device_id, timestamp_utc desc);

create or replace function auditlogs.fn_create_audit_log(
    p_id uuid,
    p_home_id uuid,
    p_user_id uuid,
    p_device_id uuid,
    p_action varchar(100),
    p_timestamp_utc timestamptz,
    p_result boolean,
    p_details varchar(1000))
returns void
language plpgsql
as $$
begin
    if nullif(btrim(p_action), '') is null then
        raise exception 'Audit action is required' using errcode = '22023';
    end if;

    insert into auditlogs.audit_logs (id, home_id, user_id, device_id, action, timestamp_utc, result, details)
    values (p_id, p_home_id, p_user_id, p_device_id, btrim(p_action), p_timestamp_utc, p_result, nullif(btrim(p_details), ''));
end;
$$;

create or replace function auditlogs.fn_get_audit_logs_by_home(p_home_id uuid, p_limit integer default 100)
returns table
(
    id uuid,
    home_id uuid,
    user_id uuid,
    device_id uuid,
    action varchar,
    timestamp_utc timestamptz,
    result boolean,
    details varchar
)
language sql
stable
as $$
    select l.id, l.home_id, l.user_id, l.device_id, l.action, l.timestamp_utc, l.result, l.details
    from auditlogs.audit_logs l
    where l.home_id = p_home_id
    order by l.timestamp_utc desc
    limit greatest(1, least(coalesce(p_limit, 100), 500));
$$;
