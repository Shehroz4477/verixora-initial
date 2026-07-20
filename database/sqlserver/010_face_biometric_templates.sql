-- Verixora biometric template storage - SQL Server 2022+
-- Face images are not persisted. Every embedding is encrypted by the API with
-- AES-256-GCM before it reaches this table.

set ansi_nulls on;
go
set quoted_identifier on;
go

create or alter procedure [identity].sp_DeleteFaceEmbeddingsForUser
    @UserId uniqueidentifier
as
begin
    set nocount on;
    delete from [identity].FaceEmbeddings where UserId = @UserId;
end
go

create or alter procedure [identity].sp_CreateFaceEmbedding
    @Id uniqueidentifier,
    @UserId uniqueidentifier,
    @Ciphertext varbinary(max),
    @Iv varbinary(64),
    @CreatedAtUtc datetime2(7)
as
begin
    set nocount on;

    if datalength(@Iv) <> 12 throw 51001, 'Face template IV must be a 96-bit AES-GCM nonce.', 1;
    if datalength(@Ciphertext) <= 16 throw 51002, 'Face template ciphertext is invalid.', 1;

    insert into [identity].FaceEmbeddings (Id, UserId, EmbeddingCiphertext, Iv, CreatedAtUtc)
    values (@Id, @UserId, @Ciphertext, @Iv, @CreatedAtUtc);
end
go

create or alter procedure [identity].sp_GetFaceEmbeddingsByUser
    @UserId uniqueidentifier
as
begin
    set nocount on;
    select Id, UserId, EmbeddingCiphertext as Ciphertext, Iv, CreatedAtUtc
    from [identity].FaceEmbeddings
    where UserId = @UserId
    order by CreatedAtUtc asc, Id asc;
end
go
