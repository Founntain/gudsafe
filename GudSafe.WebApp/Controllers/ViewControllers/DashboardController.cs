using System.Security.Claims;
using System.Text;
using AutoMapper;
using GudSafe.Data;
using GudSafe.Data.Entities;
using GudSafe.Data.Models.EntityModels;
using GudSafe.Data.Models.RequestModels;
using GudSafe.Data.ViewModels;
using GudSafe.WebApp.Classes;
using GudSafe.WebApp.Controllers.EnitityControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace GudSafe.WebApp.Controllers.ViewControllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly GudFileController _fileController;
    private readonly UserController _userController;
    private readonly GudSafeContext _context;
    private readonly IMapper _mapper;

    public DashboardController(GudFileController fileController, UserController userController, GudSafeContext context, IMapper mapper)
    {
        _fileController = fileController;
        _context = context;
        _mapper = mapper;
        _userController = userController;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> Gallery()
    {
        var user = await FindUser();

        var files = user?.FilesUploaded.OrderByDescending(x => x.CreationTime).ToList() ?? new List<GudFile>();

        return View(new GalleryViewModel
        {
            Username = user?.Name,
            Files = files.ToList()
        });
    }

    public async Task<IActionResult> UserSettings()
    {
        var user = await FindUser();

        var viewmodel = new UserSettingsViewModel
        {
            User = _mapper.Map<UserModel>(user),
            ApiKey = user?.ApiKey ?? string.Empty
        };

        return View(viewmodel);
    }

    public async Task<IActionResult> ShareXProfile()
    {
        var user = await FindUser();

        if (user == default) return NotFound("User not found");

        var shareXProfile = new ShareXProfile
        {
            Name = $"GudSafe {Request.Host}",
            DestinationType = "ImageUploader, FileUploader",
            RequestMethod = "POST",
            RequestURL = $"{Request.Scheme}://{Request.Host}/api/files/upload",
            Body = "MultipartFormData",
            Headers =
                new Dictionary<string, object>
                {
                    {"apikey", user.ApiKey}
                },
            FileFormName = "file",
            URL = "$json:url$",
            ThumbnailURL = "$json:thumbnailurl$"
        };

        var json = JsonConvert.SerializeObject(shareXProfile);

        var data = Encoding.UTF8.GetBytes(json);

        return File(data, "application/octet-stream", $"GudSafe-{Request.Host}.sxcu");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _fileController.Delete(id);

        var user = await FindUser();

        var files = user?.FilesUploaded ?? new HashSet<GudFile>();

        HttpContext.Response.StatusCode = 302;
        HttpContext.Response.Headers["Location"] = "/Dashboard/Gallery";

        return View("Gallery", new GalleryViewModel
        {
            Username = user?.Name ?? "Unkown",
            Files = files.ToList()
        });
    }

    private Task<User?> FindUser()
    {
        return _context.Users
            .FirstOrDefaultAsync(x => x.Name == User.FindFirstValue(ClaimTypes.Name));
    }

    public async Task<IActionResult> AdminSettings()
    {
        var user = await FindUser();

        return View(new AdminSettingsViewModel
        {
            ApiKey = user.ApiKey,
            NewUserPassword = string.Join("", Guid.NewGuid().ToString().Split('-'))
        });
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateUserFromUi([FromForm] AdminSettingsViewModel model)
    {
        var user = await FindUser();
        
        Request.Headers["apikey"] = model.ApiKey;

        if (string.IsNullOrWhiteSpace(model.ApiKey) || string.IsNullOrWhiteSpace(model.NewUserPassword) ||
            string.IsNullOrWhiteSpace(model.NewUserUsername))
            return BadRequest();

        var result = (ObjectResult) await _userController.Create(new CreateUserModel
        {
            Name = model.NewUserUsername,
            Password = model.NewUserPassword
        });

        if (result.StatusCode != 200)
        {
            ModelState.AddModelError("UsernameExists", "The username already exists");
        }

        return View("AdminSettings", new AdminSettingsViewModel
        {
            ApiKey = user?.ApiKey,
            NewUserPassword = string.Join("", Guid.NewGuid().ToString().Split('-'))
        });
    }

    public IActionResult ChangePassword(StringValues returnurl)
    {
        throw new NotImplementedException();
    }
}