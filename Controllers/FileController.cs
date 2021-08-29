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
        private readonly IHubContext<MessageHub, IClient> hub;
        private readonly List<string> ImageExtensions = new List<string> { "JPG", "JPEG", "JPE", "BMP", "GIF", "PNG" };
        private readonly MessageFunctions messageFunctions;

        public FileController(UserManager<AppUser> userManager, Context context, IHubContext<MessageHub, IClient> hub)
        {
            this.userManager = userManager;
            this.context = context;
            this.hub = hub;
            messageFunctions = new MessageFunctions(userManager);
        }
        [HttpPost("sendmessagegroup/{id}")]
        public async Task<ActionResult<MessageDto>> SendBinaryMessage(Guid id, IFormFile file)
        {
            if (file == null) return BadRequest("File is null");
            var userId = userManager.GetUserId(User);
            var conversation = await context.Conversations.Include(c => c.Messages).Include(c => c.Files).FirstOrDefaultAsync(c => c.Id == id);
            if (conversation == null) return NotFound("No such conversation found");
            if (file.Length > 25 * 1024 * 1024) return BadRequest("File too large");

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

            conversation.Files.Add(dbFile);

            Message message = new Message
            {
                Content = String.Format("{0},{1}", dbFile.FileName, dbFile.Id.ToString()),
                IsReferenceToFile = true,
                Date = DateTime.Now,
                SenderId = userId,
            };
            conversation.Messages.Add(message);


            var result = await context.SaveChangesAsync() > 0;
            if (!result) return BadRequest("Failed to create message");
            MessageDto messageDto = await messageFunctions.CreateMessageObjectAsync(message);

            await hub.Clients.Users(message.Conversation.Recipients.Select(r => r.UserId).Except(new List<string> { userId })).ReceiveMessage(messageDto);
            return Ok(messageDto);
        }

        [HttpGet("receivemessage/{id}")]
        public async Task<IActionResult> ReceiveBinaryMessage(Guid id)
        {
            var userId = userManager.GetUserId(User);

            Models.File dbFile = await context.Files.AsNoTracking().Include(f => f.Conversation).ThenInclude(c => c.Recipients).AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
            if (dbFile == null) return BadRequest("No such file found");

            var recipient = dbFile.Conversation.Recipients.FirstOrDefault(r => r.UserId == userId);
            if (recipient == null) return BadRequest("This file is not accessable to you");
            var splitName = dbFile.FileName.Split('.');
            var fileExtension = splitName.Count() == 1 ? "" : splitName.Last();
            var fileDto = new FileDto
            {
                FileName = dbFile.FileName.Substring(0, dbFile.FileName.Count() - fileExtension.Count() - (fileExtension.Count() == 0 ? 0 : 1)),
                FileExtension = fileExtension,
                Data = Convert.ToBase64String(dbFile.Data),
            };

            return Ok(fileDto);
        }

        [HttpPost("sendgroup")]
        public async Task<IActionResult> SetGroupProfile([FromForm] Guid id, [FromForm] IFormFile file)
        {
            var userId = userManager.GetUserId(User);
            var conversation = await context.Conversations.Include(c => c.Recipients).Include(c => c.Messages).FirstOrDefaultAsync(c => c.Id == id);
            if (conversation == null) return NotFound("No such group found");
            if (!conversation.IsGroup) return BadRequest("Not a group");
            else if (conversation.Recipients.FirstOrDefault(r => r.UserId == userId) == null) return BadRequest("You're not a part of this group!");

            string result;
            try
            {
                result = await SetConversationImage(id, file);
            }
            catch (Exception err)
            {
                return BadRequest(err.Message);
            }

            return Ok(result);
        }

        [HttpPost("sendprofile")]
        public async Task<IActionResult> SetUserProfile(IFormFile file)
        {
            var userId = userManager.GetUserId(User);
            string result;
            try
            {
                result = await SetConversationImage(System.Guid.Parse(userId), file);
            }
            catch (Exception err)
            {
                return BadRequest(err.Message);
            }

            return Ok(result);
        }
        private async Task<string> SetConversationImage(Guid id, IFormFile file)
        {
            if (file == null) throw new Exception("File is null");
            var fileExtension = file.FileName.ToUpper().Split('.').Last();
            if (!ImageExtensions.Contains(fileExtension)) throw new Exception("Not an image");
            if (file.Length > 4 * 1024 * 1024) throw new Exception("File too large");

            MemoryStream ms = new MemoryStream();
            file.CopyTo(ms);
            Image img = new Image
            {
                FileName = file.FileName,
                Data = ms.ToArray(),
                Id = id,
            };
            ms.Close();
            ms.Dispose();
            var alreadyExistingImage = await context.Images.FindAsync(id);
            if (alreadyExistingImage == null)
                context.Images.Add(img);
            else
            {
                if (alreadyExistingImage.Data == img.Data && alreadyExistingImage.FileName == img.FileName) throw new Exception("Same image is already set");
                alreadyExistingImage.Data = img.Data;
                alreadyExistingImage.FileName = img.FileName;
            }
            var result = await context.SaveChangesAsync() > 0;
            if (!result) throw new Exception("Failed to save image");

            return "Received!";

        }



    }
}