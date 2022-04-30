#pragma warning disable CS0618
using JWT.Builder;
using JWT.Algorithms;

var builder = WebApplication.CreateBuilder(args);
var secret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new Exception("JWT_SECRET environment variable not set");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddResponseCaching();

var app = builder.Build();

app.UseResponseCaching();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

if (!Directory.Exists("./assets"))
{
  Directory.CreateDirectory("./assets");
}

var token = JwtBuilder.Create()
    .WithAlgorithm(new HMACSHA256Algorithm()) // symmetric
    .WithSecret(secret)
    .MustVerifySignature()
    .AddClaim("iss", "NeX CDN")
    .Encode();

Console.WriteLine($"Here's a JWT token for you to use! {token}");

app.Run();

