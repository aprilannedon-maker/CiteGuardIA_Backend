var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5175") 
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseHttpsRedirection();

app.UseCors("AllowReactApp");


app.MapGet("/api/verify", async (string doi) =>
{
    using var client = new HttpClient();

    // Crossref recommends adding a User-Agent header with contact info
    client.DefaultRequestHeaders.Add("User-Agent", "CiteGuardIA-Capstone/1.0 (mailto:student@ccdi.edu.ph)");

    try
    {
        var response = await client.GetAsync($"https://api.crossref.org/works/{doi}");

        if (!response.IsSuccessStatusCode)
            return Results.NotFound(new { message = "DOI not found or invalid academic reference." });

        var jsonContent = await response.Content.ReadAsStringAsync();

        // Return the raw Crossref JSON payload back to your React frontend
        return Results.Content(jsonContent, "application/json");
    }
    catch (Exception)
    {
        return Results.Problem("An error occurred while communicating with the Crossref API.");
    }
});

app.Run();