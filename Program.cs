using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var logger = app.Logger;

// Simple API key for demo purposes
const string ApiKeyHeaderName = "X-Api-Key";
const string ApiKeyValue = "supersecretkey";

// Authentication middleware (very simple)
app.Use(async (context, next) =>
{
    if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey) ||
        extractedApiKey != ApiKeyValue)
    {
        logger.LogWarning("Unauthorized request to {Path}", context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Unauthorized");
        return;
    }

    await next();
});

// In-memory list of users (domain model)
var users = new List<User>();

// Helper: Validate DTO input
bool ValidateUserDto(UserDto dto, out string error, out int parsedAge)
{
    parsedAge = 0;

    if (string.IsNullOrWhiteSpace(dto.Username))
    {
        error = "Username is required.";
        return false;
    }

    if (string.IsNullOrWhiteSpace(dto.UserAge) ||
        !Regex.IsMatch(dto.UserAge, @"^[0-9]+$"))
    {
        error = "UserAge must contain only numbers.";
        return false;
    }

    parsedAge = int.Parse(dto.UserAge);

    if (parsedAge < 0 || parsedAge > 120)
    {
        error = "UserAge must be between 0 and 120.";
        return false;
    }

    error = string.Empty;
    return true;
}

// CREATE
app.MapPost("/users", (UserDto dto) =>
{
    if (!ValidateUserDto(dto, out var error, out var age))
        return Results.BadRequest(error);

    if (users.Any(u => u.Username == dto.Username))
        return Results.Conflict("A user with this username already exists.");

    var user = new User
    {
        Username = dto.Username!,
        UserAge = age
    };

    users.Add(user);
    logger.LogInformation("User created: {Username}", user.Username);

    return Results.Created($"/users/{user.Username}", user);
});

// READ ALL
app.MapGet("/users", () =>
{
    logger.LogInformation("Fetching all users");
    return Results.Ok(users);
});

// READ ONE
app.MapGet("/users/{username}", (string username) =>
{
    var user = users.FirstOrDefault(u => u.Username == username);

    if (user is null)
    {
        logger.LogWarning("User not found: {Username}", username);
        return Results.NotFound("User not found.");
    }

    logger.LogInformation("Fetched user: {Username}", username);
    return Results.Ok(user);
});

// UPDATE
app.MapPut("/users/{username}", (string username, UserDto dto) =>
{
    var user = users.FirstOrDefault(u => u.Username == username);

    if (user is null)
    {
        logger.LogWarning("Attempted update on missing user: {Username}", username);
        return Results.NotFound("User not found.");
    }

    if (!ValidateUserDto(dto, out var error, out var age))
        return Results.BadRequest(error);

    user.UserAge = age;

    logger.LogInformation("User updated: {Username}", username);
    return Results.Ok(user);
});

// DELETE
app.MapDelete("/users/{username}", (string username) =>
{
    var user = users.FirstOrDefault(u => u.Username == username);

    if (user is null)
    {
        logger.LogWarning("Attempted delete on missing user: {Username}", username);
        return Results.NotFound("User not found.");
    }

    users.Remove(user);

    logger.LogInformation("User deleted: {Username}", username);
    return Results.Ok(user);
});

app.Run();

// Domain model
public class User
{
    public string Username { get; set; } = string.Empty;
    public int UserAge { get; set; }
}

// DTO for input
public class UserDto
{
    public string? Username { get; set; }
    public string? UserAge { get; set; }
}
