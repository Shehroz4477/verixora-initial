-- Verixora Identity module - PostgreSQL 16+
-- Apply through the database deployment runner, never from an API request.

create schema if not exists identity;

create table if not exists identity.users
(
    id uuid primary key,
    phone_number varchar(20) not null,
    password_hash text not null,
    email varchar(256) null,
    email_verified boolean not null default false,
    role varchar(20) not null,
    created_at_utc timestamptz not null,
    constraint uq_identity_users_phone_number unique (phone_number),
    constraint ck_identity_users_phone_not_blank check (length(btrim(phone_number)) > 0),
    constraint ck_identity_users_role check (role in ('Owner', 'Guest', 'SystemAdmin'))
);

create unique index if not exists ux_identity_users_email_normalized
    on identity.users ((lower(email)))
    where email is not null;

create table if not exists identity.trusted_devices
(
    id uuid primary key,
    user_id uuid not null unique references identity.users(id) on delete cascade,
    device_id varchar(256) not null,
    device_fingerprint varchar(512) not null,
    registered_at_utc timestamptz not null,
    is_active boolean not null,
    constraint uq_identity_trusted_devices_device_id unique (device_id),
    constraint ck_identity_trusted_devices_device_id_not_blank check (length(btrim(device_id)) > 0),
    constraint ck_identity_trusted_devices_fingerprint_not_blank check (length(btrim(device_fingerprint)) > 0)
);

create table if not exists identity.face_embeddings
(
    id uuid primary key,
    user_id uuid not null references identity.users(id) on delete cascade,
    embedding_ciphertext bytea not null,
    iv bytea not null,
    created_at_utc timestamptz not null
);

create index if not exists ix_identity_face_embeddings_user_id
    on identity.face_embeddings(user_id);

create or replace function identity.fn_get_user_by_id(p_id uuid)
returns table
(
    id uuid,
    phone_number varchar,
    password_hash text,
    email varchar,
    email_verified boolean,
    role varchar,
    created_at_utc timestamptz,
    trusted_device_id uuid,
    trusted_device_user_id uuid,
    trusted_device_device_id varchar,
    trusted_device_fingerprint varchar,
    trusted_device_registered_at_utc timestamptz,
    trusted_device_is_active boolean
)
language sql
stable
as $$
    select u.id, u.phone_number, u.password_hash, u.email, u.email_verified, u.role, u.created_at_utc,
           d.id, d.user_id, d.device_id, d.device_fingerprint, d.registered_at_utc, d.is_active
    from identity.users u
    left join identity.trusted_devices d on d.user_id = u.id
    where u.id = p_id;
$$;

create or replace function identity.fn_get_user_by_phone_number(p_phone_number varchar(20))
returns table
(
    id uuid,
    phone_number varchar,
    password_hash text,
    email varchar,
    email_verified boolean,
    role varchar,
    created_at_utc timestamptz,
    trusted_device_id uuid,
    trusted_device_user_id uuid,
    trusted_device_device_id varchar,
    trusted_device_fingerprint varchar,
    trusted_device_registered_at_utc timestamptz,
    trusted_device_is_active boolean
)
language sql
stable
as $$
    select u.id, u.phone_number, u.password_hash, u.email, u.email_verified, u.role, u.created_at_utc,
           d.id, d.user_id, d.device_id, d.device_fingerprint, d.registered_at_utc, d.is_active
    from identity.users u
    left join identity.trusted_devices d on d.user_id = u.id
    where u.phone_number = btrim(p_phone_number);
$$;

create or replace function identity.fn_get_user_by_email(p_email varchar(256))
returns table
(
    id uuid,
    phone_number varchar,
    password_hash text,
    email varchar,
    email_verified boolean,
    role varchar,
    created_at_utc timestamptz,
    trusted_device_id uuid,
    trusted_device_user_id uuid,
    trusted_device_device_id varchar,
    trusted_device_fingerprint varchar,
    trusted_device_registered_at_utc timestamptz,
    trusted_device_is_active boolean
)
language sql
stable
as $$
    select u.id, u.phone_number, u.password_hash, u.email, u.email_verified, u.role, u.created_at_utc,
           d.id, d.user_id, d.device_id, d.device_fingerprint, d.registered_at_utc, d.is_active
    from identity.users u
    left join identity.trusted_devices d on d.user_id = u.id
    where lower(u.email) = lower(btrim(p_email));
$$;

create or replace function identity.fn_phone_number_exists(p_phone_number varchar(20))
returns boolean
language sql
stable
as $$
    select exists(select 1 from identity.users where phone_number = btrim(p_phone_number));
$$;

create or replace function identity.fn_create_user(
    p_id uuid,
    p_phone_number varchar(20),
    p_password_hash text,
    p_email varchar(256),
    p_email_verified boolean,
    p_role varchar(20),
    p_created_at_utc timestamptz,
    p_trusted_device_id uuid default null,
    p_trusted_device_device_id varchar(256) default null,
    p_trusted_device_fingerprint varchar(512) default null,
    p_trusted_device_registered_at_utc timestamptz default null,
    p_trusted_device_is_active boolean default null)
returns void
language plpgsql
as $$
begin
    if nullif(btrim(p_phone_number), '') is null then
        raise exception 'Phone number is required' using errcode = '22023';
    end if;

    if p_role not in ('Owner', 'Guest', 'SystemAdmin') then
        raise exception 'Identity role is invalid' using errcode = '22023';
    end if;

    if p_trusted_device_id is not null and
       (nullif(btrim(p_trusted_device_device_id), '') is null or nullif(btrim(p_trusted_device_fingerprint), '') is null) then
        raise exception 'Trusted device data is incomplete' using errcode = '22023';
    end if;

    insert into identity.users (id, phone_number, password_hash, email, email_verified, role, created_at_utc)
    values (p_id, btrim(p_phone_number), p_password_hash, nullif(lower(btrim(p_email)), ''), p_email_verified, p_role, p_created_at_utc);

    if p_trusted_device_id is not null then
        insert into identity.trusted_devices
            (id, user_id, device_id, device_fingerprint, registered_at_utc, is_active)
        values
            (p_trusted_device_id, p_id, btrim(p_trusted_device_device_id), btrim(p_trusted_device_fingerprint),
             coalesce(p_trusted_device_registered_at_utc, now()), coalesce(p_trusted_device_is_active, true));
    end if;
end;
$$;

create or replace function identity.fn_update_user(
    p_id uuid,
    p_password_hash text,
    p_email varchar(256),
    p_email_verified boolean,
    p_role varchar(20),
    p_trusted_device_id uuid default null,
    p_trusted_device_device_id varchar(256) default null,
    p_trusted_device_fingerprint varchar(512) default null,
    p_trusted_device_registered_at_utc timestamptz default null,
    p_trusted_device_is_active boolean default null)
returns void
language plpgsql
as $$
begin
    if p_role not in ('Owner', 'Guest', 'SystemAdmin') then
        raise exception 'Identity role is invalid' using errcode = '22023';
    end if;

    update identity.users
    set password_hash = p_password_hash,
        email = nullif(lower(btrim(p_email)), ''),
        email_verified = p_email_verified,
        role = p_role
    where id = p_id;

    if not found then
        raise exception 'User not found' using errcode = 'P0002';
    end if;

    if p_trusted_device_id is not null then
        insert into identity.trusted_devices
            (id, user_id, device_id, device_fingerprint, registered_at_utc, is_active)
        values
            (p_trusted_device_id, p_id, btrim(p_trusted_device_device_id), btrim(p_trusted_device_fingerprint),
             coalesce(p_trusted_device_registered_at_utc, now()), coalesce(p_trusted_device_is_active, true))
        on conflict (user_id) do update
        set id = excluded.id,
            device_id = excluded.device_id,
            device_fingerprint = excluded.device_fingerprint,
            registered_at_utc = excluded.registered_at_utc,
            is_active = excluded.is_active;
    end if;
end;
$$;
