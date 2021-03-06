using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.HelperFunctions;
using API.Models;
using Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FriendController : ControllerBase
    {
        private readonly Context context;
        private readonly ImageFunctions imageFunctions;

        public UserManager<AppUser> userManager { get; }

        public FriendController(UserManager<AppUser> userManager, Context context)
        {
            this.userManager = userManager;
            this.context = context;
            imageFunctions = new ImageFunctions(userManager, context);
        }
        [HttpGet("send/{username}")]
        public async Task<ActionResult<FriendRequestDto>> SendRequest(string username)
        {
            var userId = userManager.GetUserId(User);
            var userUserName = userManager.GetUserName(User);
            if (userUserName == username) return BadRequest("You can't be friends with yourself!");
            var recipient = await userManager.FindByNameAsync(username);
            if (recipient == null) return NotFound("No such recipient");
            var alreadyFriends = await context.Friends.Where(f => f.Person1Id == userId && f.Person2Id == recipient.Id || f.Person1Id == recipient.Id && f.Person2Id == userId).AnyAsync();
            if (alreadyFriends) return BadRequest("Already friends");

            var alreadyExistingRequest = await context.FriendRequests.Where(fr => fr.SenderId == userId && fr.RecipientId == recipient.Id).AnyAsync();
            if (alreadyExistingRequest) return BadRequest("Already sent same request");
            FriendRequest friendRequest = new FriendRequest
            {
                SenderId = userId,
                RecipientId = recipient.Id,
            };
            await context.FriendRequests.AddAsync(friendRequest);
            var result = await context.SaveChangesAsync() > 0;

            if (!result) return BadRequest("Failed to create friend request");
            return Ok(await CreateFriendRequestObject(friendRequest));
        }
        [HttpGet("requests")]
        public async Task<ActionResult<IEnumerable<FriendRequestDto>>> GetRequests()
        {
            var userId = userManager.GetUserId(User);

            var friendRequestList = await context.FriendRequests.Where(fr => fr.SenderId == userId || fr.RecipientId == userId).ToListAsync<FriendRequest>();
            return Ok(friendRequestList.Select(async el => await CreateFriendRequestObject(el)));
        }
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FriendDto>>> GetFriends()
        {
            var userId = userManager.GetUserId(User);

            var friendList = await context.Friends.AsNoTracking().Where(f => f.Person1Id == userId || f.Person2Id == userId).ToListAsync<Friend>();

            return Ok(friendList.Select(async el => await CreateFriendObject(el)));
        }
        [HttpGet("accept/{id}")]
        public async Task<ActionResult<FriendDto>> AcceptRequest(Guid id)
        {
            var userId = userManager.GetUserId(User);

            var request = await context.FriendRequests.FindAsync(id);
            if (request == null) return NotFound("No such friend request found");

            if (request.RecipientId != userId) return BadRequest("You're not the receiver!");

            var otherUserId = (userId == request.SenderId ? request.RecipientId : request.SenderId);

            context.FriendRequests.Remove(request);
            Friend friend = new Friend
            {
                Id = System.Guid.NewGuid(),
                Person1Id = userId,
                Person2Id = otherUserId,

            };
            var conversation = new Conversation(userId, otherUserId, friend.Id);
            await context.Friends.AddAsync(friend);
            await context.Conversations.AddAsync(conversation);
            var result = await context.SaveChangesAsync() > 0;

            if (!result) return BadRequest("Failed to accept friend request");
            return Ok(await CreateFriendObject(friend));
        }

        [HttpGet("decline/{id}")]
        public async Task<IActionResult> DeclineRequest(Guid id)
        {
            var userId = userManager.GetUserId(User);

            var request = await context.FriendRequests.FindAsync(id);
            if (request == null) return NotFound("No such friend request found");
            if (request.RecipientId != userId && request.SenderId != userId) return BadRequest("You're not a part of this request!");
            context.FriendRequests.Remove(request);
            var result = await context.SaveChangesAsync() > 0;

            if (!result) return BadRequest("Failed to decline friend request");
            return Ok("Declined!");
        }
        [HttpGet("removefriend/{username}")]
        public async Task<IActionResult> RemoveFriend(string username)
        {
            var userId = userManager.GetUserId(User);
            var otherUser = await userManager.FindByNameAsync(username);
            if (otherUser == null) return NotFound("No such user found");

            var friend = await context.Friends
            .FirstOrDefaultAsync(f => f.Person1Id == userId && f.Person2Id == otherUser.Id || f.Person1Id == otherUser.Id && f.Person2Id == userId);
            var conversation = context.Conversations.Include(c => c.Messages).Include(c => c.Recipients).Include(c => c.Files).FirstOrDefaultAsync(c => c.Id == friend.Id);

            if (friend == null) return BadRequest("You're not friends");
            context.Friends.Remove(friend);

            var result = await context.SaveChangesAsync() > 0;
            if (!result) return BadRequest("Failed to remove friend");
            return Ok("Removed!");
        }


        private async Task<FriendDto> CreateFriendObject(Friend friend)
        {
            var userId = userManager.GetUserId(User);
            var otherUserId = (userId == friend.Person1Id ? friend.Person2Id : friend.Person1Id);
            var otherUser = await userManager.FindByIdAsync(otherUserId);
            var conversation = await context.Conversations.Include(c => c.Recipients).Include(c => c.Messages).FirstOrDefaultAsync(c => c.Id == friend.Id);
            var recipient = conversation.Recipients.FirstOrDefault(r => r.UserId == otherUserId);
            var friendDto = new FriendDto
            {
                UserName = otherUser.UserName,
                DisplayName = otherUser.DisplayName,
                MessageCount = conversation.Messages.Count(),
                Image = await imageFunctions.GetUserImageAsync(otherUser.UserName),
                ConversationId = conversation.Id.ToString(),
                LastSeenMessageId = recipient.LastSeenMessageId != System.Guid.Empty ? recipient.LastSeenMessageId.ToString() : null,
            };
            if (conversation.Messages.Any())
            {
                var lastMessage = conversation.Messages.Aggregate((m1, m2) => m1.Date > m2.Date ? m1 : m2);
                friendDto.LastMessageIsReferenceToFile = lastMessage.IsReferenceToFile;
                friendDto.LastMessageContent = lastMessage.Content;
                friendDto.LastMessageDate = ((DateTimeOffset)lastMessage.Date).ToUnixTimeMilliseconds().ToString();

            }
            return friendDto;
        }
        private async Task<FriendRequestDto> CreateFriendRequestObject(FriendRequest friendRequest)
        {
            if (friendRequest == null) return null;
            var userId = userManager.GetUserId(User);
            var otherUserId = (userId == friendRequest.SenderId ? friendRequest.RecipientId : friendRequest.SenderId);
            var otherUser = await userManager.FindByIdAsync(otherUserId);
            return new FriendRequestDto
            {
                Image = await imageFunctions.GetUserImageAsync(otherUser.UserName),
                IsOutbound = (userId == friendRequest.SenderId),
                DisplayName = otherUser.DisplayName,
                RequestId = friendRequest.Id.ToString(),

            };


        }

    }
}