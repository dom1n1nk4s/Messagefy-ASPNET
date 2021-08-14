using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.HelperFunctions;
using API.Hubs;
using API.Models;
using Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileController : ControllerBase
    {
        private readonly UserManager<AppUser> userManager;
        private readonly Context context;
        private readonly IHubContext<MessageHub> hub;
        private readonly List<string> ImageExtensions = new List<string> { ".JPG", ".JPE", ".BMP", ".GIF", ".PNG" };
        private readonly MessageFunctions messageFunctions;

        public FileController(UserManager<AppUser> userManager, Context context, IHubContext<MessageHub> hub)
        {
            this.userManager = userManager;
            this.context = context;
            this.hub = hub;
            messageFunctions = new MessageFunctions(userManager);
        }
        [HttpPost("sendmessage/{username}")]
        public async Task<IActionResult> SendBinaryMessage(string username, IFormFile file)
        {
            var userId = userManager.GetUserId(User);
            var otherUser = await userManager.FindByNameAsync(username);
            if (otherUser == null) return NotFound("No such user found");
            if (file.Length > 25 * 1024 * 1024) return BadRequest("File too large");
            if (userId == otherUser.Id) return BadRequest("You can't send a message to yourself");

            var friend = await context.Friends
            .Include(t => t.Conversation)
                .ThenInclude(t => t.Messages)
            .Include(f => f.Conversation)
                .ThenInclude(c => c.Files)
            .FirstAsync(f => f.Person1Id == userId && f.Person2Id == otherUser.Id || f.Person1Id == otherUser.Id && f.Person2Id == userId);

            if (friend == null) return BadRequest("You're not friends");

            MemoryStream ms = new MemoryStream();
            file.CopyTo(ms);
            Models.File dbFile = new Models.File
            {
                Id = System.Guid.NewGuid(),
                FileName = file.FileName,
                Data = ms.ToArray(),

            };
            ms.Close();
            ms.Dispose();

            friend.Conversation.Files.Add(dbFile);

            Message message = new Message
            {
                Content = dbFile.Id.ToString(),
                IsReferenceToFile = true,
                Date = DateTime.Now,
                SenderId = userId,
            };
            friend.Conversation.Messages.Add(message);


            var result = await context.SaveChangesAsync() > 0;
            if (!result) return BadRequest("Failed to create message");
            MessageDto messageDto = await messageFunctions.CreateMessageObject(message);

            await hub.Clients.User(otherUser.Id).SendAsync("ReceiveMessage", messageDto);
            return Ok(messageDto);
        }
        [HttpGet("receivemessage/{id}")]
        public async Task<IActionResult> ReceiveBinaryMessage(string id)
        {
            var userId = userManager.GetUserId(User);

            Models.File dbFile = await context.Files.Include(f => f.Conversation).ThenInclude(c => c.Recipients).AsNoTracking().FirstOrDefaultAsync(f => f.Id == System.Guid.Parse(id));
            if (dbFile == null) return BadRequest("No such file found");

            var recipient = dbFile.Conversation.Recipients.FirstOrDefault(r => r.UserId == userId);
            if (recipient == null) return BadRequest("This file is not accessable to you");

            var data = Convert.ToBase64String(dbFile.Data);
            return Ok(data);
        }



        [HttpPost("sendprofile")]
        public async Task<IActionResult> SetUserProfile(IFormFile file)
        {
            if (file == null) return BadRequest("File is null");
            if (!ImageExtensions.Contains(file.FileName)) return BadRequest("Not an image");
            if (file.Length > 4 * 1024 * 1024) return BadRequest("File too large");
            var userId = userManager.GetUserId(User);

            MemoryStream ms = new MemoryStream();
            file.CopyTo(ms);
            Image img = new Image
            {
                FileName = file.FileName,
                Data = ms.ToArray(),
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