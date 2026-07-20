-- Cryptographic mobile-device binding - PostgreSQL 16+

alter table identity.trusted_devices
    add column if not exists device_public_key_spki_base64 text null,
    add column if not exists device_public_key_thumbprint varchar(64) null;

drop function if exists identity.fn_get_user_by_id(uuid);
drop function if exists identity.fn_get_user_by_phone_number(varchar);
drop function if exists identity.fn_get_user_by_email(varchar);

create function identity.fn_get_user_by_id(p_id uuid)
returns table (id uuid, phone_number varchar, password_hash text, email varchar, email_verified boolean, role varchar, created_at_utc timestamptz, trusted_device_id uuid, trusted_device_user_id uuid, trusted_device_device_id varchar, trusted_device_fingerprint varchar, trusted_device_public_key_spki_base64 text, trusted_device_public_key_thumbprint varchar, trusted_device_registered_at_utc timestamptz, trusted_device_is_active boolean)
language sql stable as $$
    select u.id, u.phone_number, u.password_hash, u.email, u.email_verified, u.role, u.created_at_utc,
           d.id, d.user_id, d.device_id, d.device_fingerprint, d.device_public_key_spki_base64, d.device_public_key_thumbprint, d.registered_at_utc, d.is_active
    from identity.users u left join identity.trusted_devices d on d.user_id = u.id where u.id = p_id;
$$;

create function identity.fn_get_user_by_phone_number(p_phone_number varchar(20))
returns table (id uuid, phone_number varchar, password_hash text, email varchar, email_verified boolean, role varchar, created_at_utc timestamptz, trusted_device_id uuid, trusted_device_user_id uuid, trusted_device_device_id varchar, trusted_device_fingerprint varchar, trusted_device_public_key_spki_base64 text, trusted_device_public_key_thumbprint varchar, trusted_device_registered_at_utc timestamptz, trusted_device_is_active boolean)
language sql stable as $$
    select u.id, u.phone_number, u.password_hash, u.email, u.email_verified, u.role, u.created_at_utc,
           d.id, d.user_id, d.device_id, d.device_fingerprint, d.device_public_key_spki_base64, d.device_public_key_thumbprint, d.registered_at_utc, d.is_active
    from identity.users u left join identity.trusted_devices d on d.user_id = u.id where u.phone_number = btrim(p_phone_number);
$$;

create function identity.fn_get_user_by_email(p_email varchar(256))
returns table (id uuid, phone_number varchar, password_hash text, email varchar, email_verified boolean, role varchar, created_at_utc timestamptz, trusted_device_id uuid, trusted_device_user_id uuid, trusted_device_device_id varchar, trusted_device_fingerprint varchar, trusted_device_public_key_spki_base64 text, trusted_device_public_key_thumbprint varchar, trusted_device_registered_at_utc timestamptz, trusted_device_is_active boolean)
language sql stable as $$
    select u.id, u.phone_number, u.password_hash, u.email, u.email_verified, u.role, u.created_at_utc,
           d.id, d.user_id, d.device_id, d.device_fingerprint, d.device_public_key_spki_base64, d.device_public_key_thumbprint, d.registered_at_utc, d.is_active
    from identity.users u left join identity.trusted_devices d on d.user_id = u.id where lower(u.email) = lower(btrim(p_email));
$$;

create or replace function identity.fn_create_user(
    p_id uuid, p_phone_number varchar(20), p_password_hash text, p_email varchar(256), p_email_verified boolean, p_role varchar(20), p_created_at_utc timestamptz,
    p_trusted_device_id uuid, p_trusted_device_device_id varchar(256), p_trusted_device_fingerprint varchar(512), p_trusted_device_public_key_spki_base64 text, p_trusted_device_public_key_thumbprint varchar(64), p_trusted_device_registered_at_utc timestamptz, p_trusted_device_is_active boolean)
returns void language plpgsql as $$
begin
    if nullif(btrim(p_phone_number), '') is null then raise exception 'Phone number is required' using errcode = '22023'; end if;
    if p_role not in ('Owner', 'Guest', 'SystemAdmin') then raise exception 'Identity role is invalid' using errcode = '22023'; end if;
    if p_trusted_device_id is not null and (nullif(btrim(p_trusted_device_device_id), '') is null or nullif(btrim(p_trusted_device_fingerprint), '') is null or nullif(btrim(p_trusted_device_public_key_spki_base64), '') is null or nullif(btrim(p_trusted_device_public_key_thumbprint), '') is null) then
        raise exception 'Cryptographic trusted-device data is incomplete' using errcode = '22023';
    end if;
    insert into identity.users (id, phone_number, password_hash, email, email_verified, role, created_at_utc)
    values (p_id, btrim(p_phone_number), p_password_hash, nullif(lower(btrim(p_email)), ''), p_email_verified, p_role, p_created_at_utc);
    if p_trusted_device_id is not null then
        insert into identity.trusted_devices (id, user_id, device_id, device_fingerprint, device_public_key_spki_base64, device_public_key_thumbprint, registered_at_utc, is_active)
        values (p_trusted_device_id, p_id, btrim(p_trusted_device_device_id), btrim(p_trusted_device_fingerprint), btrim(p_trusted_device_public_key_spki_base64), btrim(p_trusted_device_public_key_thumbprint), coalesce(p_trusted_device_registered_at_utc, now()), coalesce(p_trusted_device_is_active, true));
    end if;
end;
$$;

create or replace function identity.fn_update_user(
    p_id uuid, p_password_hash text, p_email varchar(256), p_email_verified boolean, p_role varchar(20),
    p_trusted_device_id uuid, p_trusted_device_device_id varchar(256), p_trusted_device_fingerprint varchar(512), p_trusted_device_public_key_spki_base64 text, p_trusted_device_public_key_thumbprint varchar(64), p_trusted_device_registered_at_utc timestamptz, p_trusted_device_is_active boolean)
returns void language plpgsql as $$
begin
    if p_role not in ('Owner', 'Guest', 'SystemAdmin') then raise exception 'Identity role is invalid' using errcode = '22023'; end if;
    update identity.users set password_hash = p_password_hash, email = nullif(lower(btrim(p_email)), ''), email_verified = p_email_verified, role = p_role where id = p_id;
    if not found then raise exception 'User not found' using errcode = 'P0002'; end if;
    if p_trusted_device_id is not null then
        insert into identity.trusted_devices (id, user_id, device_id, device_fingerprint, device_public_key_spki_base64, device_public_key_thumbprint, registered_at_utc, is_active)
        values (p_trusted_device_id, p_id, btrim(p_trusted_device_device_id), btrim(p_trusted_device_fingerprint), nullif(btrim(p_trusted_device_public_key_spki_base64), ''), nullif(btrim(p_trusted_device_public_key_thumbprint), ''), coalesce(p_trusted_device_registered_at_utc, now()), coalesce(p_trusted_device_is_active, true))
        on conflict (user_id) do update set id = excluded.id, device_id = excluded.device_id, device_fingerprint = excluded.device_fingerprint,
            device_public_key_spki_base64 = coalesce(excluded.device_public_key_spki_base64, identity.trusted_devices.device_public_key_spki_base64),
            device_public_key_thumbprint = coalesce(excluded.device_public_key_thumbprint, identity.trusted_devices.device_public_key_thumbprint),
            registered_at_utc = excluded.registered_at_utc, is_active = excluded.is_active;
    end if;
end;
$$;
