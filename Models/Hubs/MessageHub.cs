using System;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace API.Models.Hubs
{
    [Authorize]
    public class MessageHub : Hub<IClient>
    {
        private readonly Context _context;
        private readonly UserManager<AppUser> userManager;

        public MessageHub(Context context, UserManager<AppUser> userManager)
        {
            this._context = context;
            this.userManager = userManager;
        }

        public async Task DownloadMessages(string username, int num = 0)
        {

            var userId = userManager.GetUserId(Context.User);
            var otherUser = await userManager.FindByNameAsync(username);
            if (otherUser == null) throw new HubException("No such user found");
            var friend = await _context.Friends.Include(t => t.Conversation).ThenInclude(t => t.Messages).AsNoTracking().FirstOrDefaultAsync(f => f.Person1Id == userId && f.Person2Id == otherUser.Id || f.Person1Id == otherUser.Id && f.Person2Id == userId);
            if (friend == null) throw new HubException("You're not friends");
            var conversation = friend.Conversation;
            var messageList = conversation.Messages.OrderBy(x => x.Date).Skip(num).TakeLast(20).Select(async m => await CreateMessageObject(m)).Select(m => m.Result).Reverse().ToList();

            await Clients.Caller.ReceiveMessages(messageList);

        }
        public async Task<MessageDto> PostMessage(string username, Message message)
        {
            var userId = userManager.GetUserId(Context.User);
            var otherUser = await userManager.FindByNameAsync(username);
            if (otherUser == null) throw new HubException("No such user found");
            if (!message.Content.Any()) throw new HubException("Empty message");
            if (userId == otherUser.Id) throw new HubException("You can't send a message to yourself");
            var friend = await _context.Friends.Include(t => t.Conversation).ThenInclude(t => t.Messages).FirstOrDefaultAsync(f => f.Person1Id == userId && f.Person2Id == otherUser.Id || f.Person1Id == otherUser.Id && f.Person2Id == userId);
            if (friend == null) throw new HubException("You're not friends");
            if (friend.Conversation == null) friend.Conversation = new Conversation();
            message.SenderId = userId;
            message.Date = DateTime.Now;
            friend.Conversation.Messages.Add(message);
            // friend.Conversation.Recipients.FirstOrDefault(r => r.UserId == userId).LastSeenMessageId = message.Id;
            var result = await _context.SaveChangesAsync() > 0;
            if (!result) throw new HubException("Failed to create message");
            var messageDto = await CreateMessageObject(message);

            await Clients.User(otherUser.Id).ReceiveMessage(messageDto);

            return messageDto;
        }

        public async Task EditMessage(Guid id, MessageDto messageDto)
        {
            var userId = userManager.GetUserId(Context.User);
            var message = await _context.Messages.Include(m => m.Conversation).ThenInclude(c => c.Friend).FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) throw new HubException("No such message found");
            if (message.SenderId != userId) throw new HubException("You're not the sender!");
            if (!message.Content.Any()) throw new HubException("Empty message");
            message.Content = messageDto.Content;
            message.DateEdited = DateTime.Now;

            var otherUserId = (message.Conversation.Friend.Person1Id == userId ? message.Conversation.Friend.Person2Id : message.Conversation.Friend.Person1Id);

            var result = await _context.SaveChangesAsync() > 0;

            if (!result) throw new HubException("Failed to update message");
            messageDto = await CreateMessageObject(message);

            await Clients.User(otherUserId).UpdateMessage(messageDto);
        }

        public async Task DeleteMessage(Guid id)
        {
            var userId = userManager.GetUserId(Context.User);

            var message = await _context.Messages.Include(m => m.Conversation).ThenInclude(c => c.Friend).FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) throw new HubException("No such message found");
            if (message.SenderId != userId) throw new HubException("You're not the sender!");

            var otherUserId = (message.Conversation.Friend.Person1Id == userId ? message.Conversation.Friend.Person2Id : message.Conversation.Friend.Person1Id);
            _context.Messages.Remove(message);
            var result = await _context.SaveChangesAsync() > 0;

            if (!result) throw new HubException("Failed to remove message");

            var messageDto = await CreateMessageObject(message);

            await Clients.User(otherUserId).DeleteMessage(messageDto);
        }

        public async Task SeenMessage(string username, Guid messageId)
        {
            var userId = userManager.GetUserId(Context.User);
            var otherUser = await userManager.FindByNameAsync(username);
            //
            var friend = await _context.Friends
            .Include(f => f.Conversation)
                .ThenInclude(c => c.Recipients)
            .AsSplitQuery()
            .Include(f => f.Conversation)
                .ThenInclude(c => c.Messages)
            .FirstOrDefaultAsync(f => f.Person1Id == userId && f.Person2Id == otherUser.Id || f.Person1Id == otherUser.Id && f.Person2Id == userId);
            var message = friend.Conversation.Messages.FirstOrDefault(m => m.Id == messageId);
            if (message == null) throw new HubException("No such message found");
            if (friend == null) throw new HubException("You're not friends");
            if (friend.Conversation == null) friend.Conversation = new Conversation(); // should not be needed
            var userRecipient = friend.Conversation.Recipients.FirstOrDefault(r => r.UserId == userId);
            if (userRecipient.LastSeenMessageId == messageId) return;//throw new HubException("YOU'RE TRYING TO UPDATE THE DATABASE WITH THE SAME DATA!!!");

            userRecipient.LastSeenMessageId = messageId;
            var result = await _context.SaveChangesAsync() > 0;
            if (!result) throw new HubException("Failed to update message");

            var messageDto = await CreateMessageObject(message);
            await Clients.User(otherUser.Id).SeenMessage(messageDto);

        }
        public async Task<MessageDto> GetSeenMessageId(string username)
        {
            var userId = userManager.GetUserId(Context.User);
            var otherUser = await userManager.FindByNameAsync(username);
            if (otherUser == null) throw new HubException("No such user found");
            var friend = await _context.Friends.Include(f => f.Conversation).ThenInclude(c => c.Recipients).AsNoTracking().FirstOrDefaultAsync(f => f.Person1Id == userId && f.Person2Id == otherUser.Id || f.Person1Id == otherUser.Id && f.Person2Id == userId);
            if (friend == null) throw new HubException("You're not friends");
            var recipient = friend.Conversation.Recipients.FirstOrDefault(r => r.UserId == otherUser.Id);
            var message = await _context.Messages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == recipient.LastSeenMessageId);
            var messageDto = await CreateMessageObject(message);

            return messageDto;
        }


        private async Task<MessageDto> CreateMessageObject(Message message)
        {
            var sender = await userManager.FindByIdAsync(message.SenderId);
            return new MessageDto
            {
                MessageId = message.Id.ToString(),
                Content = message.Content,
                Date = ((DateTimeOffset)message.Date).ToUnixTimeMilliseconds().ToString(),
                SenderName = sender.UserName,
                DateEdited = (message.DateEdited != DateTime.MinValue ? ((DateTimeOffset)message.DateEdited).ToUnixTimeMilliseconds().ToString() : null),
            };
        }



    }
}
