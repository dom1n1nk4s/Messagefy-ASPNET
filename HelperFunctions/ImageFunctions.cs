using System;
using System.Threading.Tasks;
using API.Models;
using Domain;
using Microsoft.AspNetCore.Identity;

namespace API.HelperFunctions
{
    public static class ImageFunctions
    {
        public static async Task<string> GetUserImage(string username, UserManager<AppUser> userManager, Context context)
        {
            var otherUser = await userManager.FindByNameAsync(username);
            var img = await context.Images.FindAsync(System.Guid.Parse(otherUser.Id));
            if (img == null) return null;
            string imageData = Convert.ToBase64String(img.ImageData);
            return imageData;
        }
    }
}