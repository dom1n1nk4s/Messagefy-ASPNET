using System;
using System.IO;
using System.Threading.Tasks;
using API.Models;
using Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImageController : ControllerBase
    {
        private readonly UserManager<AppUser> userManager;
        private readonly Context context;

        public ImageController(UserManager<AppUser> userManager, Context context)
        {
            this.userManager = userManager;
            this.context = context;
        }

        [HttpPost("send")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            var userId = userManager.GetUserId(User);
            if (file == null) return BadRequest("File is null");

            MemoryStream ms = new MemoryStream();
            file.CopyTo(ms);
            Image img = new Image
            {
                ImageTitle = file.FileName,
                ImageData = ms.ToArray(),
                Id = System.Guid.Parse(userId),
            };
            ms.Close();
            ms.Dispose();
            var alreadyExistingImage = await context.Images.FindAsync(userId);
            if (alreadyExistingImage != null)
                context.Images.Add(img);
            else
                alreadyExistingImage = img;
            var result = await context.SaveChangesAsync() > 0;
            if (!result) return BadRequest("Failed to save image");

            return Ok("Received!");
        }



    }
}