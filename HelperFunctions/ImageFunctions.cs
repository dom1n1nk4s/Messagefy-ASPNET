using System;
using System.Threading.Tasks;
using API.Models;
using Domain;
using Microsoft.AspNetCore.Identity;

namespace API.HelperFunctions
{
    public class ImageFunctions
    {
        private readonly UserManager<AppUser> userManager;
        private readonly Context context;

        public ImageFunctions(UserManager<AppUser> userManager, Context context)
        {
            this.userManager = userManager;
            this.context = context;
        }

        public async Task<string> GetUserImageAsync(string username)
        {
            var otherUser = await userManager.FindByNameAsync(username);
            var img = await context.Images.FindAsync(System.Guid.Parse(otherUser.Id));
            if (img == null) return null;
            string imageData = Convert.ToBase64String(img.Data);
            return imageData;
        }
    }
}