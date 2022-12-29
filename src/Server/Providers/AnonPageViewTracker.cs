using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Security;

namespace UrlShortener.Providers;

public sealed class AnonPageViewTracker
{
    private readonly string _logDirectory;

    public AnonPageViewTracker()
    {
        _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    private string GetSalt() {
        // The current salt is stored as a file in the current directory.
        // If the file doesn't exist, a new salt is generated and stored.
        // The salt will change every day at midnight.
        // The salt will be a random string of 32 characters.

        var saltFile = Path.Combine(AppContext.BaseDirectory, "salt.txt");

        var retry = 0;
        while (retry < 10)
        {
            try
            {
                using var fs = new FileStream(saltFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                fs.Seek(0, SeekOrigin.Begin);
                using var sr = new StreamReader(fs);

                var salt = sr.ReadToEnd();
                var salt_expiration_date = System.IO.File.GetLastWriteTimeUtc(saltFile).Date.AddDays(1);

                if (salt.Length == 32 && salt_expiration_date > DateTime.Now) {
                    // Salt is valid, return it
                    return salt;
                }

                // Salt is invalid, generate a new one
                var new_salt = GenerateRandomSalt();

                fs.SetLength(0);

                using var sw = new StreamWriter(fs);
                sw.Write(new_salt);

                return new_salt;
            }
            catch (IOException ex) when (ex.HResult == -2147024864)
            {
                retry++;
                Thread.Sleep(100);
            }
        }

        throw new InvalidOperationException("Unable to get salt");
    }

    private string GenerateRandomSalt() {
        var salt = new StringBuilder();
        var random = new Random();
        for (var i = 0; i < 32; i++)
        {
            salt.Append((char)random.Next(33, 126));
        }

        return salt.ToString();
    }

    public void Track(string url, string ip, string useragent)
    {
        var salt = GetSalt();

        var logFile = Path.Combine(_logDirectory, $"pageviews.{DateTime.Now:yyyy-MM-dd}.log");

        if (string.IsNullOrWhiteSpace(useragent)) {
            // Random user agent to anonymize the data
            useragent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.88 Safari/537.36";
        }

        // Hash the ip address, user agent, and salt to anonymize the data
        // and make it harder to track individual users.
        // We still need to keep some form of uniqueness so that we can
        // count the number of page views per URL and verify we're not
        // getting DDOSED or something.
        var hash = Hash(ip + useragent + salt);

        var entry = $"{url} {DateTime.Now:yyyy-MM-dd HH:mm:ss} {hash}{Environment.NewLine}";

        // Open file, create if it doesn't exist. Read/Write lock it
        // so that multiple threads can't write to it at the same time.
        // Retry if the file is locked by another process.

        var retry = 0;
        while (retry < 10)
        {
            try
            {
                using var fs = new FileStream(logFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                fs.Seek(0, SeekOrigin.End);
                using var sw = new StreamWriter(fs);
                sw.Write(entry);
                return;
            }
            catch (IOException ex) when (ex.HResult == -2147024864)
            {
                retry++;
                Thread.Sleep(100);
            }
        }
    }

    private static string Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}