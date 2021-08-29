using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.HelperFunctions;
using API.Models;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace API.Hubs
{
    [Authorize]
    public class MessageHub : Hub<IClient>
    {
        private readonly Context _context;
        private readonly UserManager<AppUser> userManager;
        private readonly MessageFunctions messageFunctions;


        public MessageHub(Context context, UserManager<AppUser> userManager)
        {
            this._context = context;
            this.userManager = userManager;
            messageFunctions = new MessageFunctions(userManager);
        }

        public async Task DownloadMessages(Guid id, int num = 0)
        {
            var userId = userManager.GetUserId(Context.User);
            var conversation = await _context.Conversations.AsNoTracking().Include(c => c.Messages).Include(c => c.Recipients).FirstOrDefaultAsync(c => c.Id == id);
            if (conversation == null) throw new HubException("No such conversation found");
            if (conversation.Recipients.FirstOrDefault(r => r.UserId == userId) == null) throw new HubException("You're not a part of this conversation");
            var messageList = conversation.Messages.OrderBy(x => x.Date).SkipLast(num).TakeLast(20).Select(async m => await messageFunctions.CreateMessageObjectAsync(m)).Select(m => m.Result).Reverse().ToList();

            await Clients.Caller.ReceiveMessages(messageList);
        }

        public async Task<MessageDto> PostMessage(Guid id, Message message)
        {
            var userId = userManager.GetUserId(Context.User);
            var conversation = await _context.Conversations.Include(c => c.Messages).Include(c => c.Recipients).FirstOrDefaultAsync(c => c.Id == id);
            if (conversation == null) throw new HubException("No such conversation found");
            if (conversation.Recipients.FirstOrDefault(r => r.UserId == userId) == null) throw new HubException("You're not a part of this conversation");
            message.SenderId = userId;
            message.Date = DateTime.Now;
            conversation.Messages.Add(message);
            var result = await _context.SaveChangesAsync() > 0;
            if (!result) throw new HubException("Failed to create message");
            var messageDto = await messageFunctions.CreateMessageObjectAsync(message);

            await Clients.Users(message.Conversation.Recipients.Select(r => r.UserId).Except(new List<string> { userId })).ReceiveMessage(messageDto);

            return messageDto;
        }
        public async Task EditMessage(Guid id, MessageDto messageDto)
        {
            var userId = userManager.GetUserId(Context.User);
            var message = await _context.Messages.Include(m => m.Conversation).ThenInclude(c => c.Recipients).FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) throw new HubException("No such message found");
            if (message.SenderId != userId) throw new HubException("You're not the sender!");
            if (!message.Content.Any()) throw new HubException("Empty message");
            message.Content = messageDto.Content;
            message.DateEdited = DateTime.Now;

            var result = await _context.SaveChangesAsync() > 0;

            if (!result) throw new HubException("Failed to update message");
            messageDto = await messageFunctions.CreateMessageObjectAsync(message);

            await Clients.Users(message.Conversation.Recipients.Select(r => r.UserId).Except(new List<string> { userId })).UpdateMessage(messageDto);
        }

        public async Task DeleteMessage(Guid id)
        {
            var userId = userManager.GetUserId(Context.User);

            var message = await _context.Messages.Include(m => m.Conversation).ThenInclude(c => c.Recipients).FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) throw new HubException("No such message found");
            if (message.SenderId != userId) throw new HubException("You're not the sender!");

            _context.Messages.Remove(message);

            var result = await _context.SaveChangesAsync() > 0;
            if (!result) throw new HubException("Failed to remove message");

            var messageDto = await messageFunctions.CreateMessageObjectAsync(message);

            await Clients.Users(message.Conversation.Recipients.Select(r => r.UserId).Except(new List<string> { userId })).DeleteMessage(messageDto);
        }

        public async Task SeenMessage(Guid messageId)
        {
            var userId = userManager.GetUserId(Context.User);

            var message = _context.Messages.Include(m => m.Conversation).ThenInclude(c => c.Recipients).FirstOrDefault(m => m.Id == messageId);
            if (message == null) throw new HubException("No such message found");
            var userRecipient = message.Conversation.Recipients.FirstOrDefault(r => r.UserId == userId);
            if (userRecipient == null) throw new HubException("You're not a part of this conversation");
            if (userRecipient.LastSeenMessageId == messageId) return; // shouldnt be allowed to happen

            userRecipient.LastSeenMessageId = messageId;
            var result = await _context.SaveChangesAsync() > 0;
            if (!result) throw new HubException("Failed to update message");

            var messageDto = await messageFunctions.CreateMessageObjectAsync(message);
            await Clients.Users(message.Conversation.Recipients.Select(r => r.UserId).Except(new List<string> { userId })).SeenMessage(messageDto);

        }
    }
}
