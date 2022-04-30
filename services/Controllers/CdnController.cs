#pragma warning disable CS0618
using Microsoft.AspNetCore.Mvc;
using filesys = System.IO.File;
using System.Text.RegularExpressions;
using JWT.Builder;
using JWT.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace nex.Controllers;

[ApiController]
[Route("i")]
[ResponseCache(Duration = 86400, NoStore = false, Location = ResponseCacheLocation.Client)]
public class CdnController : ControllerBase
{
  private string ASSET_LOCATION = "./assets";
  private string JWT_SECRET = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new Exception("JWT_SECRET environment variable not set");
  int[] SIZES = { 16, 32, 64, 128, 256, 512, 1024, 2048, 4096 };

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
        return BadRequest("Unallowed File Extention");
      }

      if (filesys.Exists($"{ASSET_LOCATION}/{file.FileName}"))
      {
        return Ok("File Already Exists");
      }

      using (var stream = System.IO.File.Create($"{ASSET_LOCATION}/{file.FileName}"))
      {
        await file.CopyToAsync(stream);
      }

      ResizeFile(file.FileName);

      return Ok("File Successfully Uploaded");
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

    DeleteResizedFiles(name);
    filesys.Delete(pathToFile);

    return Ok("Deleted");
  }

  private async void ResizeFile(string filename)
  {
    var path = $"{ASSET_LOCATION}/{filename}";
    var name = Path.GetFileNameWithoutExtension(path);
    var ext = new FileInfo(path).Extension;

    for (int i = 0; i < SIZES.Length; i++)
    {
      using var image = Image.Load(path);

      image.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(SIZES[i], SIZES[i]) }));
      await image.SaveAsync($"{ASSET_LOCATION}/{name}_{SIZES[i]}{ext}");
    }

    return;
  }

  private void DeleteResizedFiles(string filename) {
    var path = $"{ASSET_LOCATION}/{filename}";
    var name = Path.GetFileNameWithoutExtension(path);
    var ext = new FileInfo(path).Extension;

    for (int i = 0; i < SIZES.Length; i++)
    {
      filesys.Delete($"{ASSET_LOCATION}/{name}_{SIZES[i]}{ext}");
    }

    return;
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
