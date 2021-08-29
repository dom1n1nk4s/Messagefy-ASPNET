using System;
using System.Threading.Tasks;
using API.DTOs;
using API.Models;
using Domain;
using Microsoft.AspNetCore.Identity;

namespace API.HelperFunctions
{
    public class MessageFunctions
    {
        private readonly UserManager<AppUser> userManager;
        public MessageFunctions(UserManager<AppUser> userManager)
        {
            this.userManager = userManager;
        }

        public async Task<MessageDto> CreateMessageObjectAsync(Message message)
        {
            var sender = await userManager.FindByIdAsync(message.SenderId);
            var messageDto = new MessageDto
            {
                IsReferenceToFile = message.IsReferenceToFile,
                MessageId = message.Id.ToString(),
                Content = message.Content,
                Date = ((DateTimeOffset)message.Date).ToUnixTimeMilliseconds().ToString(),

                DateEdited = (message.DateEdited != DateTime.MinValue ? ((DateTimeOffset)message.DateEdited).ToUnixTimeMilliseconds().ToString() : null),
            };
            if (sender != null)
                messageDto.SenderName = sender.UserName;
            return messageDto;
        }
    }
}