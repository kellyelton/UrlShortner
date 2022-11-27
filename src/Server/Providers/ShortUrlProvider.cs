using Microsoft.Data.Sqlite;

namespace UrlShortener.Providers;

public sealed class ShortUrlProvider
{
    public ShortUrlProvider()
    {
    }

    public string Assign(string long_url)
    {
        var query = @"
            UPDATE AvailableShortCodes
            SET is_available = false
            WHERE short_code = (SELECT short_code FROM AvailableShortCodes where is_available = true ORDER BY RANDOM() LIMIT 1)
            RETURNING short_code
        ";

        // Open the connection
        using var connection = new SqliteConnection("Data Source=data.db");
        connection.Open();

        // Create the command
        using var command = new SqliteCommand(query, connection);

        // Execute the command
        using var reader = command.ExecuteReader();

        // Read the result
        reader.Read();
        var short_code = reader.GetString(0);

        // Insert the long url and short code into the ShortUrls table
        query = @"
            INSERT INTO ShortUrls (long_url, short_code)
            VALUES (@long_url, @short_code)
        ";

        // Create the command
        using var command2 = new SqliteCommand(query, connection);

        // Add the parameters
        command2.Parameters.AddWithValue("@long_url", long_url);
        command2.Parameters.AddWithValue("@short_code", short_code);

        // Execute the command
        command2.ExecuteNonQuery();

        return short_code;
    }

    public void InitializeDatabase(){
        using var con = new SqliteConnection("Data Source=data.db");
        con.Open();

        var created = CreateAvailableShortCodesTable(con);
        CreateShortCodesTable(con);

        if (created)
            FillAvailableShortCodesTable(con);
    }

    private void FillAvailableShortCodesTable(SqliteConnection con) {
        Console.WriteLine(".");

        var tranny = con.BeginTransaction();

        var i = 0;
        foreach (var short_code in GenerateAllShortCodes().Take(1000000 * 10)){
            // Insert the short code into the AvailableShortCodes table
            var query = $@"
                INSERT INTO AvailableShortCodes (short_code, is_available)
                VALUES ('{short_code}', true)
            ";

            using var cmd = con.CreateCommand();
            cmd.CommandText = query;

            cmd.ExecuteNonQuery();

            i++;

            // Commit transaction every 1000 rows
            if (i % 100000 == 0) {
                tranny.Commit();
                tranny.Dispose();
                tranny = con.BeginTransaction();
                Console.WriteLine(".");
            }

        }

        tranny.Commit();
        tranny.Dispose();
    }

    private bool DoesTableExist(SqliteConnection con, string table_name) {
        var query = $@"
            SELECT name FROM sqlite_master WHERE type='table' AND name='{table_name}'
        ";

        using var cmd = new SqliteCommand(query, con);
        using var reader = cmd.ExecuteReader();

        return reader.HasRows;
    }

    private bool CreateAvailableShortCodesTable(SqliteConnection con) {
        if (DoesTableExist(con, "AvailableShortCodes"))
            return false;

        // short_code 7 char max pk
        // is_available bool default(true)
        var query = @"
            CREATE TABLE IF NOT EXISTS AvailableShortCodes (
                short_code TEXT PRIMARY KEY,
                is_available BOOLEAN DEFAULT true
            )
        ";

        using var cmd = new SqliteCommand(query, con);
        var cnt = cmd.ExecuteNonQuery();

        return true;
    }

    private void CreateShortCodesTable(SqliteConnection con) {
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

    private IEnumerable<string> GenerateAllShortCodes()
    {
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        for (var code_length = 1; code_length <= 7; code_length++)
        {
            foreach (var code in GenerateShortCodes(chars, code_length))
            {
                yield return code;
            }
        }
    }

    private IEnumerable<string> GenerateShortCodes(string chars, int code_length)
    {
        if (code_length == 1)
        {
            foreach (var c in chars)
            {
                yield return c.ToString();
            }
        }
        else
        {
            foreach (var c in chars)
            {
                foreach (var code in GenerateShortCodes(chars, code_length - 1))
                {
                    yield return c + code;
                }
            }
        }
    }
}