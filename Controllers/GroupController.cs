using API.DTOs;
using API.HelperFunctions;
using API.Hubs;
using API.Models;
using Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

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
        private const int MINIMUM_GROUP_MEMBER_COUNT = 2;

        public GroupController(UserManager<AppUser> userManager, Context context, IHubContext<MessageHub, IClient> hub)
        {
            this.userManager = userManager;
            this.context = context;
            this.hub = hub;
            messageFunctions = new MessageFunctions(userManager);
            imageFunctions = new ImageFunctions(userManager, context);
        }

        //[HttpGet]
        //public async Task<ActionResult<List<GroupDto>>> GetGroups()
        //{
        //    var userId = userManager.GetUserId(User);
        //    var recipients = await context.Recipients.Include(r => r.Conversation).ThenInclude(c => c.Recipients).Where(r => r.UserId == userId && r.Conversation.IsGroup).ToListAsync();
        //    var groupDtos = new List<GroupDto>();
        //    foreach (var recipient in recipients)
        //    {
        //        groupDtos.Add(await CreateGroupObject(recipient.Conversation));
        //    }
        //    return groupDtos;
        //}

        //[HttpGet(nameof(GetUserDetails) + "/{id}")]
        //public async Task<ActionResult<List<UserDto>>> GetUserDetails(Guid id)
        //{
        //    var userId = userManager.GetUserId(User);
        //    var conversation = await GetConversationAsync(id, userId);
        //    var users = new List<UserDto>();
        //    foreach (var recipient in conversation.Recipients)
        //    {
        //        var recipientUser = await userManager.FindByIdAsync(recipient.UserId);
        //        users.Add(new UserDto
        //        {
        //            DisplayName = recipientUser.DisplayName,
        //            Image = await imageFunctions.GetUserImageAsync(recipientUser.UserName),
        //            Username = recipientUser.UserName,
        //            LastSeenMessageId = recipient.LastSeenMessageId != System.Guid.Empty ? recipient.LastSeenMessageId.ToString() : null,
        //        });
        //    }

        //    return Ok(users);
        //}

        [HttpPost(nameof(CreateGroup))]
        public async Task<ActionResult<GroupDto>> CreateGroup(GroupDto groupDto)
        {
            var userName = userManager.GetUserName(User);
            var user = await userManager.FindByNameAsync(userName);
            if (!groupDto.Recipients.Contains(userName)) groupDto.Recipients.Add(userName);

            ValidateGroupDto(groupDto);
            
            var conversation = await CreateConversationAsync(groupDto, user);

            await NotifyRecipientsOfCreatedGroup(conversation);
            
            var groupCreatedDto = await CreateGroupObject(conversation);

            return Ok(groupCreatedDto);
        }

        private async Task NotifyRecipientsOfCreatedGroup(Conversation conversation)
        {
            var creationMessage = conversation.Messages.First();
            var messageDto = await messageFunctions.CreateMessageObjectAsync(creationMessage);
            var clients = hub.Clients;
            var usersInterface = hub.Clients.Users(conversation.Recipients.Select(r => r.UserId));
            if(usersInterface != null)
                await usersInterface.ReceiveMessage(messageDto);
        }

        private void ValidateGroupDto(GroupDto groupDto)
        {
            if (groupDto.Recipients.Count() < MINIMUM_GROUP_MEMBER_COUNT) throw new HttpRequestException($"Not enough recipients", null, HttpStatusCode.BadRequest);
            if (groupDto.Recipients.Distinct().Count() != groupDto.Recipients.Count()) throw new HttpRequestException($"Duplicates in Recipients found", null, HttpStatusCode.BadRequest);
        }

        private async Task<Conversation> CreateConversationAsync(GroupDto groupDto, AppUser user)
        {
            var conversation = new Conversation();

            foreach (var recipient in groupDto.Recipients)
            {
                var otherUser = await userManager.FindByNameAsync(recipient);
                if (otherUser == null) throw new HttpRequestException($"User {recipient} not found", null, HttpStatusCode.NotFound);

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
            if (!result) throw new HttpRequestException("Failed to create group", null, HttpStatusCode.BadRequest);

            return conversation;
        }

        //[HttpPost(nameof(RenameGroup))]
        //public async Task<IActionResult> RenameGroup(GroupEditDto groupEditDto)
        //{
        //    (Guid id, string title) = groupEditDto;
        //    if (title == null) return BadRequest("No title specified");
        //    var userId = userManager.GetUserId(User);
        //    var user = await userManager.FindByIdAsync(userId);
        //    var conversation = await GetConversationAsync(id, userId);

        //    conversation.Title = title;

        //    var renameMessage = new Message
        //    {
        //        Date = DateTime.Now,
        //        Content = $"{user.DisplayName} has renamed the group to {title}.",
        //    };

        //    var result = await context.SaveChangesAsync() > 0;
        //    if (!result) return BadRequest("Failed to rename group");

        //    var messageDto = await messageFunctions.CreateMessageObjectAsync(renameMessage);
        //    await hub.Clients.Users(conversation.Recipients.Select(r => r.UserId)).ReceiveMessage(messageDto);

        //    return Ok("Renamed group!");
        //}

        //[HttpPost(nameof(AddRecipient))]
        //public async Task<IActionResult> AddRecipient(GroupEditDto groupEditDto)
        //{
        //    (Guid id, string username) = groupEditDto;
        //    var userId = userManager.GetUserId(User);
        //    var user = await userManager.FindByIdAsync(userId);
        //    var otherUser = await userManager.FindByNameAsync(username);
        //    if (otherUser == null) return NotFound("No such user found");
        //    var conversation = await GetConversationAsync(id, userId);
        //    if (conversation.Recipients.FirstOrDefault(r => r.UserId == otherUser.UserName) != null) return BadRequest("User is already in group");
        //    conversation.Recipients.Add(new Recipient
        //    {
        //        UserId = otherUser.Id,
        //    });
        //    var additionMessage = new Message
        //    {
        //        Date = DateTime.Now,
        //        Content = $"{user.DisplayName} has added {otherUser.DisplayName} to group.",
        //    };

        //    conversation.Messages.Add(additionMessage);
        //    var result = await context.SaveChangesAsync() > 0;
        //    if (!result) return BadRequest("Failed to add user to group");

        //    var messageDto = await messageFunctions.CreateMessageObjectAsync(additionMessage);
        //    await hub.Clients.Users(conversation.Recipients.Select(r => r.UserId)).ReceiveMessage(messageDto);

        //    return Ok("Added user to group!");
        //}

        [HttpDelete(nameof(RemoveRecipient))]
        public async Task<ActionResult<string>> RemoveRecipient(GroupEditDto groupEditDto)
        {
            (Guid id, string username) = groupEditDto;
            var userId = userManager.GetUserId(User);
            var user = await userManager.FindByIdAsync(userId);

            var otherUser = await userManager.FindByNameAsync(username);
            if (otherUser == null) return NotFound("No such user found");

            var conversation = await GetConversationAsync(id, userId);

            var recipient = conversation.Recipients.FirstOrDefault(r => r.UserId == otherUser.Id);
            if (recipient == null) return BadRequest("No such user in group");

            await RemoveRecipientFromConversationAsync(conversation, recipient);

            if (!conversation.Recipients.Any())
            {
                await RemoveGroupAsync(conversation);

                return Ok("Removed group!");
            }
            else
            {
                await NotifyGroupUsersOfRecipientRemovalAsync(user, otherUser, conversation);

                return Ok("Removed user!");
            }
        }

        private async Task NotifyGroupUsersOfRecipientRemovalAsync(AppUser user, AppUser otherUser, Conversation conversation)
        {
            var deletionMessage = new Message
            {
                Date = DateTime.Now,
                Content = $"{user.DisplayName} has removed {otherUser.DisplayName} from group.",
            };
            conversation.Messages.Add(deletionMessage);

            var messageDto = await messageFunctions.CreateMessageObjectAsync(deletionMessage);
            var clientInterface = hub.Clients.Users(conversation.Recipients.Select(r => r.UserId));
            if(clientInterface != null)
               await clientInterface.ReceiveMessage(messageDto);
        }

        private async Task RemoveRecipientFromConversationAsync(Conversation conversation, Recipient recipient)
        {
            conversation.Recipients.Remove(recipient);

            var result = await context.SaveChangesAsync() > 0;
            if (!result) throw new HttpRequestException("Failed to remove user from group", null, HttpStatusCode.BadRequest);
        }

        private async Task RemoveGroupAsync(Conversation conversation)
        {
            context.Conversations.Remove(conversation);

            var result = await context.SaveChangesAsync() > 0;
            if (!result) throw new HttpRequestException("Failed to remove group", null, HttpStatusCode.BadRequest);
        }

        private async Task<Conversation> GetConversationAsync(Guid conversationId, string userId)
        {
            var conversation = context.Conversations.Include(c => c.Recipients).Include(c => c.Messages).FirstOrDefault(c => c.Id == conversationId);
            if (conversation == null) throw new HttpRequestException("No such group found", null, HttpStatusCode.NotFound);
            if (!conversation.IsGroup) throw new HttpRequestException("Not a group", null, HttpStatusCode.BadRequest);
            if (conversation.Recipients.FirstOrDefault(r => r.UserId == userId) == null) throw new HttpRequestException("You're not a part of this group!", null, HttpStatusCode.BadRequest);

            return conversation;
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
                var lastMessage = conversation.Messages.MaxBy(m => m.Date);
                groupDto.LastMessageIsReferenceToFile = lastMessage.IsReferenceToFile;
                groupDto.LastMessageContent = lastMessage.Content;
                groupDto.LastMessageDate = ((System.DateTimeOffset)lastMessage.Date).ToUnixTimeMilliseconds().ToString();

            }
            return groupDto;
        }
    }
}