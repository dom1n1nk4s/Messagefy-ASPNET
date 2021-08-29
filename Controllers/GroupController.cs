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
    public class GroupController : ControllerBase
    {
        private readonly UserManager<AppUser> userManager;
        private readonly Context context;
        private readonly MessageFunctions messageFunctions;
        private readonly IHubContext<MessageHub, IClient> hub;
        private readonly ImageFunctions imageFunctions;
        public GroupController(UserManager<AppUser> userManager, Context context, IHubContext<MessageHub, IClient> hub)
        {
            this.userManager = userManager;
            this.context = context;
            this.hub = hub;
            messageFunctions = new MessageFunctions(userManager);
            imageFunctions = new ImageFunctions(userManager, context);
        }

        [HttpGet]
        public async Task<ActionResult<List<GroupDto>>> GetGroups()
        {
            var userId = userManager.GetUserId(User);
            var recipients = await context.Recipients.Include(r => r.Conversation).ThenInclude(c => c.Recipients).Where(r => r.UserId == userId && r.Conversation.IsGroup).ToListAsync();
            var groupDtos = new List<GroupDto>();
            foreach (var recipient in recipients)
            {
                groupDtos.Add(await CreateGroupObject(recipient.Conversation));
            }
            return groupDtos;
        }

        [HttpGet("userdetails/{id}")]
        public async Task<ActionResult<List<UserDto>>> GetUserDetails(Guid id)
        {
            var userId = userManager.GetUserId(User);
            var conversation = context.Conversations.AsNoTracking().Include(c => c.Recipients).FirstOrDefault(c => c.Id == id);
            if (conversation == null) return BadRequest("No such group found");
            if (!conversation.IsGroup) return BadRequest("Not a group");
            if (conversation.Recipients.FirstOrDefault(r => r.UserId == userId) == null) return BadRequest("You're not a part of this group");
            var users = new List<UserDto>();
            foreach (var recipient in conversation.Recipients)
            {
                var recipientUser = await userManager.FindByIdAsync(recipient.UserId);
                users.Add(new UserDto
                {
                    DisplayName = recipientUser.DisplayName,
                    Image = await imageFunctions.GetUserImageAsync(recipientUser.UserName),
                    Username = recipientUser.UserName,
                    LastSeenMessageId = recipient.LastSeenMessageId != System.Guid.Empty ? recipient.LastSeenMessageId.ToString() : null,
                });
            }

            return Ok(users);
        }

        [HttpPost("create")]
        public async Task<ActionResult<GroupDto>> CreateGroup(GroupDto groupDto)
        {
            var userName = userManager.GetUserName(User);
            var user = await userManager.FindByNameAsync(userName);
            if (!groupDto.Recipients.Contains(userName)) groupDto.Recipients.Add(userName);
            if (groupDto.Recipients.Count() < 2) return BadRequest("Not enough recipients");
            if (groupDto.Recipients.Distinct().Count() != groupDto.Recipients.Count()) return BadRequest("Duplicates in Recipients found");
            var conversation = new Conversation();

            foreach (var recipient in groupDto.Recipients)
            {
                var otherUser = await userManager.FindByNameAsync(recipient);
                if (otherUser == null) return NotFound($"User {recipient} not found");
                conversation.Recipients.Add(new Recipient { UserId = otherUser.Id });
            }
            if (groupDto.Title == null) groupDto.Title = "New group conversation";
            conversation.Title = groupDto.Title;
            var creationMessage = new Message
            {
                Date = System.DateTime.Now,
                Content = $"{user.DisplayName} created this group conversation with {groupDto.Recipients.Count()} recipients."
            };
            conversation.Messages.Add(creationMessage);
            context.Conversations.Add(conversation);
            var result = await context.SaveChangesAsync() > 0;
            if (!result) return BadRequest("Failed to create group");

            groupDto = await CreateGroupObject(conversation);
            var messageDto = await messageFunctions.CreateMessageObjectAsync(creationMessage);
            await hub.Clients.Users(conversation.Recipients.Select(r => r.UserId)).ReceiveMessage(messageDto);

            return Ok(groupDto);
        }
        [HttpPost("renamegroup")]
        public async Task<IActionResult> RenameGroup(GroupEditDto groupEditDto)
        {
            (Guid id, string title) = groupEditDto;
            if (title == null) return BadRequest("No title specified");
            var userId = userManager.GetUserId(User);
            var user = await userManager.FindByIdAsync(userId);
            var conversation = await context.Conversations.Include(c => c.Recipients).Include(c => c.Messages).FirstOrDefaultAsync(c => c.Id == id);
            if (conversation == null) return NotFound("No such group found");
            if (!conversation.IsGroup) return BadRequest("Not a group");
            if (conversation.Recipients.FirstOrDefault(r => r.UserId == userId) == null) return BadRequest("You're not a part of this group!");

            conversation.Title = title;

            var renameMessage = new Message
            {
                Date = DateTime.Now,
                Content = $"{user.DisplayName} has renamed the group to {title}.",
            };

            var result = await context.SaveChangesAsync() > 0;
            if (!result) return BadRequest("Failed to rename group");

            var messageDto = await messageFunctions.CreateMessageObjectAsync(renameMessage);
            await hub.Clients.Users(conversation.Recipients.Select(r => r.UserId)).ReceiveMessage(messageDto);

            return Ok("Renamed group!");
        }

        [HttpPost("addrecipient")]
        public async Task<IActionResult> AddRecipient(GroupEditDto groupEditDto)
        {
            (Guid id, string username) = groupEditDto;
            var userId = userManager.GetUserId(User);
            var user = await userManager.FindByIdAsync(userId);
            var otherUser = await userManager.FindByNameAsync(username);
            if (otherUser == null) return NotFound("No such user found");
            var conversation = await context.Conversations.Include(c => c.Recipients).Include(c => c.Messages).FirstOrDefaultAsync(c => c.Id == id);
            if (conversation == null) return NotFound("No such group found");
            if (!conversation.IsGroup) return BadRequest("Not a group");
            if (conversation.Recipients.FirstOrDefault(r => r.UserId == userId) == null) return BadRequest("You're not a part of this group!");
            if (conversation.Recipients.FirstOrDefault(r => r.UserId == otherUser.UserName) != null) return BadRequest("User is already in group");
            conversation.Recipients.Add(new Recipient
            {
                UserId = otherUser.Id,
            });
            var additionMessage = new Message
            {
                Date = DateTime.Now,
                Content = $"{user.DisplayName} has added {otherUser.DisplayName} to group.",
            };

            conversation.Messages.Add(additionMessage);
            var result = await context.SaveChangesAsync() > 0;
            if (!result) return BadRequest("Failed to add user to group");

            var messageDto = await messageFunctions.CreateMessageObjectAsync(additionMessage);
            await hub.Clients.Users(conversation.Recipients.Select(r => r.UserId)).ReceiveMessage(messageDto);

            return Ok("Added user to group!");
        }
        [HttpDelete("removerecipient")]
        public async Task<IActionResult> RemoveRecipient(GroupEditDto groupEditDto)
        {
            (Guid id, string username) = groupEditDto;
            var userId = userManager.GetUserId(User);
            var user = await userManager.FindByIdAsync(userId);
            var otherUser = await userManager.FindByNameAsync(username);
            if (otherUser == null) return NotFound("No such user found");
            var conversation = await context.Conversations.Include(c => c.Recipients).Include(c => c.Messages).FirstOrDefaultAsync(c => c.Id == id);
            if (conversation == null) return NotFound("No such group found");
            if (!conversation.IsGroup) return BadRequest("Not a group");
            if (conversation.Recipients.FirstOrDefault(r => r.UserId == userId) == null) return BadRequest("You're not a part of this group!");
            var recipient = conversation.Recipients.FirstOrDefault(r => r.UserId == otherUser.Id);
            if (recipient == null) return BadRequest("No such user in group");
            conversation.Recipients.Remove(recipient);
            var deletionMessage = new Message
            {
                Date = DateTime.Now,
                Content = $"{user.DisplayName} has removed {otherUser.DisplayName} from group.",
            };
            conversation.Messages.Add(deletionMessage);
            if (conversation.Recipients.Count() == 0)
            {
                context.Conversations.Remove(conversation);

                var result = await context.SaveChangesAsync() > 0;
                if (!result) return BadRequest("Failed to remove group");
                return Ok("Removed group!");
            }
            else
            {
                var result = await context.SaveChangesAsync() > 0;
                if (!result) return BadRequest("Failed to remove user from group");
                var messageDto = await messageFunctions.CreateMessageObjectAsync(deletionMessage);
                await hub.Clients.Users(conversation.Recipients.Select(r => r.UserId)).ReceiveMessage(messageDto);

                return Ok("Removed user!");
            }
        }

        private async Task<GroupDto> CreateGroupObject(Conversation conversation)
        {
            List<string> recipientUserNames = new List<string>();
            var image = await context.Images.FindAsync(conversation.Id);
            foreach (var recipient in conversation.Recipients.Select(r => r.UserId))
            {
                var x = await userManager.FindByIdAsync(recipient);
                recipientUserNames.Add(x.UserName);
            }
            GroupDto groupDto = new GroupDto
            {
                Id = conversation.Id.ToString(),
                Title = conversation.Title,
                Recipients = recipientUserNames,
            };
            if (image != null)
                groupDto.Image = Convert.ToBase64String(image.Data);
            if (conversation.Messages.Any())
            {
                var lastMessage = conversation.Messages.Aggregate((m1, m2) => m1.Date > m2.Date ? m1 : m2);
                groupDto.LastMessageIsReferenceToFile = lastMessage.IsReferenceToFile;
                groupDto.LastMessageContent = lastMessage.Content;
                groupDto.LastMessageDate = ((System.DateTimeOffset)lastMessage.Date).ToUnixTimeMilliseconds().ToString();

            }
            return groupDto;
        }
    }
}