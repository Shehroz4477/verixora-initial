-- Verixora biometric template storage - PostgreSQL 16+
-- Face images are not persisted. Every embedding is encrypted by the API with
-- AES-256-GCM before it reaches this table.

create or replace function identity.fn_delete_face_embeddings_for_user(p_user_id uuid)
returns void
language plpgsql
as $$
begin
    delete from identity.face_embeddings where user_id = p_user_id;
end;
$$;

create or replace function identity.fn_create_face_embedding(
    p_id uuid,
    p_user_id uuid,
    p_ciphertext bytea,
    p_iv bytea,
    p_created_at_utc timestamptz)
returns void
language plpgsql
as $$
begin
    if octet_length(p_iv) <> 12 then
        raise exception 'Face template IV must be a 96-bit AES-GCM nonce' using errcode = '22023';
    end if;

    if octet_length(p_ciphertext) <= 16 then
        raise exception 'Face template ciphertext is invalid' using errcode = '22023';
    end if;

    insert into identity.face_embeddings (id, user_id, embedding_ciphertext, iv, created_at_utc)
    values (p_id, p_user_id, p_ciphertext, p_iv, p_created_at_utc);
end;
$$;

create or replace function identity.fn_get_face_embeddings_by_user(p_user_id uuid)
returns table
(
    id uuid,
    user_id uuid,
    embedding_ciphertext bytea,
    iv bytea,
    created_at_utc timestamptz
)
language sql
stable
as $$
    select id, user_id, embedding_ciphertext, iv, created_at_utc
    from identity.face_embeddings
    where user_id = p_user_id
    order by created_at_utc asc, id asc;
$$;
