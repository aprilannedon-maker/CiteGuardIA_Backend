using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services if needed
var app = builder.Build();

// SQLite database connection string
string dbConnectionString = "Data Source=citeguard.db";

// Initialize the SQLite database table on startup
InitializeDatabase();

// 1. VERIFY ENDPOINT: Validates DOI via Crossref, hashes, saves to SQLite, and returns clean report
app.MapGet("/api/verify", async (string doi) =>
{
    if (string.IsNullOrWhiteSpace(doi))
    {
        return Results.BadRequest(new { error = "DOI parameter is required." });
    }

    string cleanDoi = doi.Trim();

    // Fetch metadata from Crossref API
    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("User-Agent", "CiteGuardIA/1.0 (mailto:student@ccdi.edu.ph)");

    string crossrefUrl = $"https://api.crossref.org/works/{Uri.EscapeDataString(cleanDoi)}";

    try
    {
        var response = await httpClient.GetAsync(crossrefUrl);
        if (!response.IsSuccessStatusCode)
        {
            return Results.NotFound(new { error = "DOI not found in Crossref database." });
        }

        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        // Safely extract the paper title from Crossref response
        string title = "Unknown Title";
        if (root.TryGetProperty("message", out var messageProp) &&
            messageProp.TryGetProperty("title", out var titleProp) &&
            titleProp.GetArrayLength() > 0)
        {
            title = titleProp[0].GetString() ?? "Unknown Title";
        }

        int score = 100; // Default integrity score for a valid verified reference

        // Generate cryptographic SHA-256 integrity hash
        string rawData = $"{cleanDoi}-{title}-{DateTime.UtcNow}";
        string integrityHash;
        using (var sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            integrityHash = BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        // Build clean verification report object
        var report = new
        {
            StudentId = "CCDI-Student",
            Doi = cleanDoi,
            Title = title,
            Score = score,
            IntegrityHash = integrityHash,
            VerifiedAt = DateTime.UtcNow.ToString("o")
        };

        // Save record into SQLite database
        SaveVerificationRecord(report.StudentId, report.Doi, report.Title, report.Score, report.IntegrityHash, report.VerifiedAt);

        // Return the clean report JSON directly
        return Results.Ok(report);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Verification failed: {ex.Message}");
    }
});

// 2. REPORTS ENDPOINT: Returns all logged verification records for professor review
app.MapGet("/api/reports", () =>
{
    var reports = GetVerificationReports();
    return Results.Ok(reports);
});

app.Run();


// --- HELPER DATABASE METHODS ---

void InitializeDatabase()
{
    using var connection = new SqliteConnection(dbConnectionString);
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = @"
        CREATE TABLE IF NOT EXISTS VerificationReports (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            StudentId TEXT NOT NULL,
            Doi TEXT NOT NULL,
            Title TEXT NOT NULL,
            Score INTEGER NOT NULL,
            IntegrityHash TEXT NOT NULL,
            VerifiedAt TEXT NOT NULL
        );";
    command.ExecuteNonQuery();
}

void SaveVerificationRecord(string studentId, string doi, string title, int score, string integrityHash, string verifiedAt)
{
    using var connection = new SqliteConnection(dbConnectionString);
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = @"
        INSERT INTO VerificationReports (StudentId, Doi, Title, Score, IntegrityHash, VerifiedAt)
        VALUES ($studentId, $doi, $title, $score, $integrityHash, $verifiedAt);";

    command.Parameters.AddWithValue("$studentId", studentId);
    command.Parameters.AddWithValue("$doi", doi);
    command.Parameters.AddWithValue("$title", title);
    command.Parameters.AddWithValue("$score", score);
    command.Parameters.AddWithValue("$integrityHash", integrityHash);
    command.Parameters.AddWithValue("$verifiedAt", verifiedAt);

    command.ExecuteNonQuery();
}

List<object> GetVerificationReports()
{
    var reports = new List<object>();
    using var connection = new SqliteConnection(dbConnectionString);
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = "SELECT Id, StudentId, Doi, Title, Score, IntegrityHash, VerifiedAt FROM VerificationReports ORDER BY Id DESC;";

    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        reports.Add(new
        {
            id = reader.GetInt32(0),
            studentId = reader.GetString(1),
            doi = reader.GetString(2),
            title = reader.GetString(3),
            score = reader.GetInt32(4),
            integrityHash = reader.GetString(5),
            verifiedAt = reader.GetString(6)
        });
    }
    return reports;
}