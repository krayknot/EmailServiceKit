using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

public class SqliteEmailQueueRepository : IEmailQueueRepository
{
    private readonly string _connectionString;
    public SqliteEmailQueueRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var sql = @"
CREATE TABLE IF NOT EXISTS EmailQueue (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  FromAddress TEXT NOT NULL,
  ToAddress TEXT NOT NULL,
  Cc TEXT,
  Bcc TEXT,
  Subject TEXT,
  Body TEXT,
  IsHtml INTEGER,
  CreatedAt TEXT,
  SendAt TEXT,
  AttemptCount INTEGER DEFAULT 0,
  Status INTEGER DEFAULT 0,
  LastError TEXT
);";
        await conn.ExecuteAsync(sql);
    }

    public async Task<long> EnqueueAsync(EmailMessage message)
    {
        var sql = @"
INSERT INTO EmailQueue (FromAddress, ToAddress, Cc, Bcc, Subject, Body, IsHtml, CreatedAt, SendAt, AttemptCount, Status)
VALUES (@From, @To, @Cc, @Bcc, @Subject, @Body, @IsHtml, @CreatedAt, @SendAt, @AttemptCount, @Status);
SELECT last_insert_rowid();";
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var id = await conn.ExecuteScalarAsync<long>(sql, new
        {
            From = message.From,
            To = message.To,
            Cc = message.Cc,
            Bcc = message.Bcc,
            Subject = message.Subject,
            Body = message.Body,
            IsHtml = message.IsHtml ? 1 : 0,
            CreatedAt = message.CreatedAt.ToString("o"),
            SendAt = message.SendAt?.ToString("o"),
            AttemptCount = message.AttemptCount,
            Status = (int)message.Status
        });
        return id;
    }

    public async Task<IEnumerable<EmailMessage>> GetPendingAsync(int maxCount)
    {
        var sql = @"
SELECT Id, FromAddress AS From, ToAddress AS To, Cc, Bcc, Subject, Body, IsHtml, CreatedAt, SendAt, AttemptCount, Status, LastError
FROM EmailQueue
WHERE Status = 0 AND (SendAt IS NULL OR datetime(SendAt) <= datetime('now')) 
ORDER BY CreatedAt LIMIT @Max;";
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var rows = await conn.QueryAsync<EmailMessage>(sql, new { Max = maxCount });
        return rows;
    }

    public async Task MarkSentAsync(long id)
    {
        var sql = "UPDATE EmailQueue SET Status = @Status WHERE Id = @Id";
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, new { Status = (int)EmailStatus.Sent, Id = id });
    }

    public async Task IncrementAttemptAsync(long id, string? error = null)
    {
        var sql = "UPDATE EmailQueue SET AttemptCount = AttemptCount + 1, LastError = @Error, Status = @Status WHERE Id = @Id";
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, new { Error = error, Status = (int)EmailStatus.Failed, Id = id });
    }
}
