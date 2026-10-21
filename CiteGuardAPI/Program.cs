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

app.MapGet("/api/verify", async (string doi) =>
{
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
        return Results.Content(jsonContent, "application/json");
    }
    catch (Exception)
    {
        return Results.Problem("An error occurred while verifying the reference.");
    }
});

app.Run();