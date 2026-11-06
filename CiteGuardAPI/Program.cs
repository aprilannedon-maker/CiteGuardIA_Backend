using Microsoft.Data.Sqlite;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseCors("AllowReactApp");

// Define SQLite Connection String & Initialize Database Table
const string dbConnectionString = "Data Source=citeguard.db";
InitializeDatabase();

// 1. VERIFY ENDPOINT (Logs verification attempt to SQLite)
app.MapGet("/api/verify", async (string doi) =>
{
    if (string.IsNullOrWhiteSpace(doi))
    {
        return Results.NotFound(new { message = "Please enter a valid DOI." });
    }

    doi = doi.Trim();
    if (doi.StartsWith("https://doi.org/", StringComparison.OrdinalIgnoreCase))
    {
        doi = doi.Substring("https://doi.org/".Length);
    }
    else if (doi.StartsWith("http://doi.org/", StringComparison.OrdinalIgnoreCase))
    {
        doi = doi.Substring("http://doi.org/".Length);
    }
    else if (doi.StartsWith("doi:", StringComparison.OrdinalIgnoreCase))
    {
        doi = doi.Substring("doi:".Length);
    }

    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("User-Agent", "CiteGuardIA-Capstone/1.0 (mailto:student@ccdi.edu.ph)");

    try
    {
        var response = await client.GetAsync($"https://api.crossref.org/works/{doi}");
        if (!response.IsSuccessStatusCode)
        {
            return Results.NotFound(new { message = "DOI not found or invalid academic reference." });
        }

        var jsonContent = await response.Content.ReadAsStringAsync();

        // Parse JSON metadata to calculate score and extract title for the audit log
        using var doc = JsonDocument.Parse(jsonContent);
        var messageProp = doc.RootElement.GetProperty("message");

        bool hasTitle = messageProp.TryGetProperty("title", out var titleProp) && titleProp.GetArrayLength() > 0;
        bool hasJournal = messageProp.TryGetProperty("container-title", out var journalProp) && journalProp.GetArrayLength() > 0;
        bool hasYear = messageProp.TryGetProperty("published", out _);
        bool hasAuthor = messageProp.TryGetProperty("author", out var authorProp) && authorProp.GetArrayLength() > 0;

        int score = (hasTitle && hasJournal && hasYear && hasAuthor) ? 100 : 50;
        string paperTitle = hasTitle ? titleProp[0].GetString() ?? "Unknown Title" : "Unknown Title";

        // Generate SHA-256 Hash and Timestamp
        string timestamp = DateTime.UtcNow.ToString("o");
        string rawHashInput = $"{doi}-{timestamp}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawHashInput));
        string hash = Convert.ToHexString(hashBytes).ToLower();

        // Save record into SQLite database
        SaveVerificationRecord("CCDI-Student", doi, paperTitle, score, hash, timestamp);

        return Results.Content(jsonContent, "application/json");
    }
    catch (Exception)
    {
        return Results.Problem("An error occurred while verifying the reference.");
    }
});

// 2. NEW ENDPOINT: Get All Logged Verification Reports for Professors
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

void SaveVerificationRecord(string studentId, string doi, string title, int score, string hash, string timestamp)
{
    using var connection = new SqliteConnection(dbConnectionString);
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = @"
        INSERT INTO VerificationReports (StudentId, Doi, Title, Score, IntegrityHash, VerifiedAt)
        VALUES ($studentId, $doi, $title, $score, $hash, $timestamp);";
    command.Parameters.AddWithValue("$studentId", studentId);
    command.Parameters.AddWithValue("$doi", doi);
    command.Parameters.AddWithValue("$title", title);
    command.Parameters.AddWithValue("$score", score);
    command.Parameters.AddWithValue("$hash", hash);
    command.Parameters.AddWithValue("$timestamp", timestamp);
    command.ExecuteNonQuery();
}

List<object> GetVerificationReports()
{
    var list = new List<object>();
    using var connection = new SqliteConnection(dbConnectionString);
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = "SELECT Id, StudentId, Doi, Title, Score, IntegrityHash, VerifiedAt FROM VerificationReports ORDER BY Id DESC;";
    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        list.Add(new
        {
            Id = reader.GetInt32(0),
            StudentId = reader.GetString(1),
            Doi = reader.GetString(2),
            Title = reader.GetString(3),
            Score = reader.GetInt32(4),
            IntegrityHash = reader.GetString(5),
            VerifiedAt = reader.GetString(6)
        });
    }
    return list;
}