using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Models;
using Microsoft.AspNetCore.Identity;
using Domain;
using API.DTOs;
using Microsoft.AspNetCore.SignalR;
using API.Models.Hubs;
using System.Security.Claims;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessageController : ControllerBase
    {
        private readonly Context _context;
        private readonly UserManager<AppUser> userManager;
        private readonly IHubContext<MessageHub> hub;

        public MessageController(Context context, UserManager<AppUser> UserManager, IHubContext<MessageHub> hub)
        {
            _context = context;
            userManager = UserManager;
            this.hub = hub;
        }

        // GET: api/Message/rob/-1
        [HttpGet("{username}/{num}")]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetMessages(string username, int num = 0) //num is position
        {
            var userId = userManager.GetUserId(User);
            var otherUser = await userManager.FindByNameAsync(username);
            if (otherUser == null) return NotFound("No such user found");

            var friend = await _context.Friends.Include(t => t.Conversation).ThenInclude(t => t.Messages).FirstAsync(f => f.Person1Id == userId && f.Person2Id == otherUser.Id || f.Person1Id == otherUser.Id && f.Person2Id == userId);
            if (friend == null) return BadRequest("You're not friends");
            var conversation = friend.Conversation;
            if (conversation == null) return Ok(null); //shouldnt be possible

            return Ok(conversation.Messages.OrderBy(x => x.Date).Skip(20).TakeLast(20).Select(async m => await CreateMessageObject(m)).ToList());

        }

        // POST: api/Message/rob
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost("send/{username}")]
        public async Task<ActionResult<MessageDto>> PostMessage(string username, Message message)
        {
            var userId = userManager.GetUserId(User);
            var otherUser = await userManager.FindByNameAsync(username);
            if (otherUser == null) return NotFound("No such user found");
            if (!message.Content.Any()) return BadRequest("Empty message");
            if (userId == otherUser.Id) return BadRequest("You can't send a message to yourself");
            var friend = await _context.Friends.Include(t => t.Conversation).ThenInclude(t => t.Messages).FirstAsync(f => f.Person1Id == userId && f.Person2Id == otherUser.Id || f.Person1Id == otherUser.Id && f.Person2Id == userId);
            if (friend == null) return BadRequest("You're not friends");
            if (friend.Conversation == null) friend.Conversation = new Conversation();
            message.SenderId = userId;
            message.Date = DateTime.Now;
            friend.Conversation.Messages.Add(message);

            var result = await _context.SaveChangesAsync() > 0;
            if (!result) return BadRequest("Failed to create message");
            MessageDto messageDto = await CreateMessageObject(message);

            await hub.Clients.User(otherUser.Id).SendAsync("Received Msg", messageDto);
            return Ok(messageDto);

        }

        // PUT: api/Message/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("edit/{id}")]
        public async Task<ActionResult<MessageDto>> PutMessage(Guid id, MessageDto messageDto)
        {
            var userId = userManager.GetUserId(User);
            var message = await _context.Messages.FindAsync(id);
            if (message == null) return NotFound("No such message found");
            if (message.SenderId != userId) return Unauthorized("You're not the sender!");
            if (!message.Content.Any()) return BadRequest("Empty message");
            message.Content = messageDto.Content;
            message.DateEdited = DateTime.Now;

            var result = await _context.SaveChangesAsync() > 0;

            if (!result) return BadRequest("Failed to update message");


            return Ok(await CreateMessageObject(message));
        }

        // DELETE: api/Message/5
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteMessage(Guid id)
        {
            var userId = userManager.GetUserId(User);
            var message = await _context.Messages.FindAsync(id);
            if (message == null) return NotFound("No such message found");
            if (message.SenderId != userId) return Unauthorized("You're not the sender!");

            _context.Messages.Remove(message);
            var result = await _context.SaveChangesAsync() > 0;

            if (!result) return BadRequest("Failed to remove message");
            return Ok("Removed!");
        }
        private async Task<MessageDto> CreateMessageObject(Message message)
        {
            var sender = await userManager.FindByIdAsync(message.SenderId);
            return new MessageDto
            {
                MessageId = message.Id.ToString(),
                Content = message.Content,
                Date = ((DateTimeOffset)message.Date).ToUnixTimeMilliseconds().ToString(),
                SenderName = sender.DisplayName,
                DateEdited = (message.DateEdited != DateTime.MinValue ? ((DateTimeOffset)message.DateEdited).ToUnixTimeMilliseconds().ToString() : null),
            };
        }
    }
}
