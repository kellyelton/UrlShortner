using System.Text;
using Microsoft.Data.Sqlite;

namespace UrlShortener.Providers;

public sealed class ShortUrlProvider
{
    private readonly Queue<string> _codes = new();
    private readonly Random _rnd = new();
    private readonly object _fillShortCodeQueueTaskLock = new();

    private Task? _fillShortCodeQueueTask;

    const string CODE_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public ShortUrlProvider()
    {
        FillShortCodeQueue();
    }

    public string Assign(string long_url)
    {
        using var connection = new SqliteConnection("Data Source=data.db");
        connection.Open();

        while (true)
        {
            if (!_codes.TryDequeue(out var short_code))
            {
                short_code = GenerateShortCode();

                FillShortCodeQueueInBackground();
            }

            using var cmd = new SqliteCommand("INSERT INTO ShortUrls (short_code, long_url) VALUES (@short_code, @long_url)", connection);
            cmd.Parameters.AddWithValue("@short_code", short_code);
            cmd.Parameters.AddWithValue("@long_url", long_url);

            try
            {
                cmd.ExecuteNonQuery();
                return short_code;
            }
            catch (SqliteException e) when (e.SqliteErrorCode == 19 && e.SqliteExtendedErrorCode == 1555)
            {
                // ignore duplicate key errors
            }
        }
    }

    public string? GetUrl(string short_code)
    {
        using var connection = new SqliteConnection("Data Source=data.db");
        connection.Open();

        using var cmd = new SqliteCommand("SELECT long_url FROM ShortUrls WHERE short_code = @short_code", connection);
        cmd.Parameters.AddWithValue("@short_code", short_code);

        var long_url = cmd.ExecuteScalar() as string;

        if (long_url != null)
        {
            using var updateCmd = new SqliteCommand("UPDATE ShortUrls SET access_count = access_count + 1 WHERE short_code = @short_code", connection);
            updateCmd.Parameters.AddWithValue("@short_code", short_code);
            updateCmd.ExecuteNonQuery();
        }

        return long_url;
    }

    public IEnumerable<ShortUrl> All() {
        using var connection = new SqliteConnection("Data Source=data.db");
        connection.Open();

        using var cmd = new SqliteCommand("SELECT short_code, long_url, access_count, created, last_accessed FROM ShortUrls", connection);

        using var reader = cmd.ExecuteReader();

        var all = new List<ShortUrl>();
        while (reader.Read())
        {
            var s = new ShortUrl
            {
                ShortCode = reader.GetString(0),
                LongUrl = reader.GetString(1),
                AccessCount = reader.GetInt32(2),
                Created = reader.GetDateTime(3),
                LastAccessed = reader.GetDateTime(4)
            };

            all.Add(s);
        }

        return all;
    }

    public bool Delete(string short_code) {
        using var connection = new SqliteConnection("Data Source=data.db");
        connection.Open();

        using var cmd = new SqliteCommand("DELETE FROM ShortUrls WHERE short_code = @short_code", connection);
        cmd.Parameters.AddWithValue("@short_code", short_code);
        
        return cmd.ExecuteNonQuery() > 0;
    }

    public void UpdateUrl(string shortCode, string longUrl)
    {
        using var connection = new SqliteConnection("Data Source=data.db");
        connection.Open();

        using var cmd = new SqliteCommand("UPDATE ShortUrls SET long_url = @long_url WHERE short_code = @short_code", connection);
        cmd.Parameters.AddWithValue("@short_code", shortCode);
        cmd.Parameters.AddWithValue("@long_url", longUrl);
        cmd.ExecuteNonQuery();
    }

    public void InitializeDatabase()
    {
        using var con = new SqliteConnection("Data Source=data.db");
        con.Open();

        CreateShortCodesTable(con);
    }

    private void FillShortCodeQueue()
    {
        while (_codes.Count < 10000)
        {
            var code = GenerateShortCode();

            if (!_codes.Contains(code.ToString()))
                _codes.Enqueue(code.ToString());
        }
    }

    private void FillShortCodeQueueInBackground()
    {
        // Store the task in the _fillShortCodeQueueTask field
        // lock to make sure that only one task is running at a time
        lock (_fillShortCodeQueueTaskLock)
        {
            if (_fillShortCodeQueueTask == null || _fillShortCodeQueueTask.IsCompleted)
                _fillShortCodeQueueTask = Task.Run(FillShortCodeQueue);
        }
    }

    private string GenerateShortCode()
    {
        var length = _rnd.Next(1, 7);
        var code = new StringBuilder();
        for (int j = 0; j < length; j++)
        {
            code.Append(CODE_CHARS[_rnd.Next(CODE_CHARS.Length)]);
        }
        return code.ToString();
    }

    private void CreateShortCodesTable(SqliteConnection con)
    {
        // short_code 7 char max pk
        // long_url 2048 char max
        // access_count int default(0)
        // created datetime default current timestamp
        // last_accessed datetime default current timestamp

        var query = @"
            CREATE TABLE IF NOT EXISTS ShortUrls (
                short_code TEXT PRIMARY KEY,
                long_url TEXT NOT NULL,
                access_count INTEGER DEFAULT 0,
                created DATETIME DEFAULT CURRENT_TIMESTAMP,
                last_accessed DATETIME DEFAULT CURRENT_TIMESTAMP
            )
        ";

        using var cmd = new SqliteCommand(query, con);
        cmd.ExecuteNonQuery();
    }
}