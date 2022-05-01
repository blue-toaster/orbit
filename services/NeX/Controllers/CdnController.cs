#pragma warning disable CS0618
using Microsoft.AspNetCore.Mvc;
using filesys = System.IO.File;
using System.Text.RegularExpressions;
using JWT.Builder;
using JWT.Algorithms;

namespace nex.Controllers;

[ApiController]
[Route("i")]
[ResponseCache(Duration = 86400, NoStore = false, Location = ResponseCacheLocation.Client)]
public class CdnController : ControllerBase
{
  private string ASSET_LOCATION = "./assets";
  private string JWT_SECRET = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new Exception("JWT_SECRET environment variable not set");

  [HttpGet("{name}")]
  public IActionResult GetFile(String name, int? size)
  {
    var pathToFile = $"{ASSET_LOCATION}/{name}";

    if (!filesys.Exists(pathToFile))
    {
      return NotFound("File Doesn't Exist");
    }

    var file = filesys.OpenRead(pathToFile);
    var meta = new FileInfo(pathToFile);
    // This Regex took me some time to get right... Lol
    var ext = Regex.Replace(meta.Extension, @"(\.)", " ").Trim();

    return File(file, $"image/{ext}");
  }

  [HttpPost("upload")]
  public async Task<IActionResult> PostFile([FromForm] IFormFile file)
  {
    if (VerifyJWT() is UnauthorizedObjectResult) return Unauthorized("Invalid Authentication");

    if (!(file.Length <= 0))
    {
      var imageExts = new List<String> { "jpg", "jpeg", "png", "gif" };
      var fileExt = Path.GetExtension(file.FileName).Substring(1);

      if (!imageExts.Contains(fileExt))
      {
        return BadRequest($"{fileExt} Is not allowed, only {String.Join(", ", imageExts)} are allowed");
      }

      if (filesys.Exists($"{ASSET_LOCATION}/{file.FileName}"))
      {
        return Ok("File Already Exists");
      }

      using (var stream = System.IO.File.Create($"{ASSET_LOCATION}/{file.FileName}"))
      {
        await file.CopyToAsync(stream);
      }

      return Ok("Uploaded");
    }

    return BadRequest("Invalid");
  }

  [HttpDelete("{name}")]
  public IActionResult DeleteFile(String name)
  {
    if (VerifyJWT() is UnauthorizedObjectResult) return Unauthorized("Invalid Authentication");

    var pathToFile = $"{ASSET_LOCATION}/{name}";

    if (!filesys.Exists(pathToFile))
    {
      return NotFound("File Doesn't Exist");
    }
    filesys.Delete(pathToFile);

    return Ok("Deleted");
  }

  private IActionResult VerifyJWT()
  {
    if (Request.Headers["Authorization"].Count() == 0) return Unauthorized("No Authorization");

    try
    {
      JwtBuilder.Create()
        .WithAlgorithm(new HMACSHA256Algorithm()) // symmetric
        .WithSecret(JWT_SECRET)
        .MustVerifySignature()
        .Decode(Request.Headers["Authorization"]);
    }
    catch
    {
      return Unauthorized("Invalid Token");
    }

    return Ok("Continue");
  }
}
