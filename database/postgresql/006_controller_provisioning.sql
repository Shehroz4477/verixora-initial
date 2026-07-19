-- Controller pairing state. A raw pairing secret is never stored.
alter table devices.devices add column if not exists provisioning_token_hash varchar(128) null;
alter table devices.devices add column if not exists provisioning_expires_at_utc timestamptz null;
alter table devices.devices add column if not exists controller_public_key_thumbprint varchar(128) null;
alter table devices.devices add column if not exists hardware_attestation_subject varchar(256) null;
alter table devices.devices add column if not exists provisioned_at_utc timestamptz null;

drop function if exists devices.fn_create_device(uuid, uuid, varchar, varchar, varchar, varchar, timestamptz);
drop function if exists devices.fn_get_device_by_id(uuid);
drop function if exists devices.fn_get_device_by_hardware_id(varchar);
drop function if exists devices.fn_get_devices_for_home(uuid);

create function devices.fn_create_device(p_id uuid,p_home_id uuid,p_hardware_id varchar(128),p_name varchar(100),p_mqtt_topic varchar(256),p_status varchar(20),p_created_at_utc timestamptz,p_provisioning_token_hash varchar(128),p_provisioning_expires_at_utc timestamptz)
returns table(id uuid,home_id uuid,hardware_id varchar,name varchar,mqtt_topic varchar,status varchar,created_at_utc timestamptz,provisioning_token_hash varchar,provisioning_expires_at_utc timestamptz,controller_public_key_thumbprint varchar,hardware_attestation_subject varchar,provisioned_at_utc timestamptz)
language plpgsql as $$
begin
  if p_status = 'Pending' and (nullif(btrim(p_provisioning_token_hash),'') is null or p_provisioning_expires_at_utc <= now()) then raise exception 'Pending controller provisioning data is required' using errcode='22023'; end if;
  insert into devices.devices(id,home_id,hardware_id,name,mqtt_topic,status,created_at_utc,provisioning_token_hash,provisioning_expires_at_utc)
  values(p_id,p_home_id,upper(btrim(p_hardware_id)),btrim(p_name),p_mqtt_topic,p_status,p_created_at_utc,p_provisioning_token_hash,p_provisioning_expires_at_utc);
  return query select d.id,d.home_id,d.hardware_id,d.name,d.mqtt_topic,d.status,d.created_at_utc,d.provisioning_token_hash,d.provisioning_expires_at_utc,d.controller_public_key_thumbprint,d.hardware_attestation_subject,d.provisioned_at_utc from devices.devices d where d.id=p_id;
end; $$;

create function devices.fn_get_device_by_id(p_id uuid) returns table(id uuid,home_id uuid,hardware_id varchar,name varchar,mqtt_topic varchar,status varchar,created_at_utc timestamptz,provisioning_token_hash varchar,provisioning_expires_at_utc timestamptz,controller_public_key_thumbprint varchar,hardware_attestation_subject varchar,provisioned_at_utc timestamptz) language sql stable as $$ select d.id,d.home_id,d.hardware_id,d.name,d.mqtt_topic,d.status,d.created_at_utc,d.provisioning_token_hash,d.provisioning_expires_at_utc,d.controller_public_key_thumbprint,d.hardware_attestation_subject,d.provisioned_at_utc from devices.devices d where d.id=p_id; $$;
create function devices.fn_get_device_by_hardware_id(p_hardware_id varchar(128)) returns table(id uuid,home_id uuid,hardware_id varchar,name varchar,mqtt_topic varchar,status varchar,created_at_utc timestamptz,provisioning_token_hash varchar,provisioning_expires_at_utc timestamptz,controller_public_key_thumbprint varchar,hardware_attestation_subject varchar,provisioned_at_utc timestamptz) language sql stable as $$ select d.id,d.home_id,d.hardware_id,d.name,d.mqtt_topic,d.status,d.created_at_utc,d.provisioning_token_hash,d.provisioning_expires_at_utc,d.controller_public_key_thumbprint,d.hardware_attestation_subject,d.provisioned_at_utc from devices.devices d where d.hardware_id=upper(btrim(p_hardware_id)); $$;
create function devices.fn_get_devices_for_home(p_home_id uuid) returns table(id uuid,home_id uuid,hardware_id varchar,name varchar,mqtt_topic varchar,status varchar,created_at_utc timestamptz,provisioning_token_hash varchar,provisioning_expires_at_utc timestamptz,controller_public_key_thumbprint varchar,hardware_attestation_subject varchar,provisioned_at_utc timestamptz) language sql stable as $$ select d.id,d.home_id,d.hardware_id,d.name,d.mqtt_topic,d.status,d.created_at_utc,d.provisioning_token_hash,d.provisioning_expires_at_utc,d.controller_public_key_thumbprint,d.hardware_attestation_subject,d.provisioned_at_utc from devices.devices d where d.home_id=p_home_id order by d.created_at_utc; $$;

create or replace function devices.fn_complete_device_provisioning(p_id uuid,p_provisioning_token_hash varchar(128),p_controller_public_key_thumbprint varchar(128),p_hardware_attestation_subject varchar(256)) returns boolean language plpgsql as $$
begin
  if nullif(btrim(p_controller_public_key_thumbprint),'') is null or nullif(btrim(p_hardware_attestation_subject),'') is null then raise exception 'Controller attestation is required' using errcode='22023'; end if;
  update devices.devices set status='Active',provisioning_token_hash=null,provisioning_expires_at_utc=null,controller_public_key_thumbprint=p_controller_public_key_thumbprint,hardware_attestation_subject=p_hardware_attestation_subject,provisioned_at_utc=now() where id=p_id and status='Pending' and provisioning_token_hash=p_provisioning_token_hash and provisioning_expires_at_utc > now() and controller_public_key_thumbprint is null;
  return found;
end; $$;
