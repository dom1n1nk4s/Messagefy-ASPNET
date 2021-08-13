using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;

namespace API.Models.Hubs
{
    public interface IClient
    {
        Task ReceiveMessages(List<MessageDto> messageList); // gets 20 messages through paramaters
        Task ReceiveMessage(MessageDto message); // gets a received message from paramater
        Task UpdateMessage(MessageDto message); // updates specified message 
        Task DeleteMessage(MessageDto message); // deletes specified message 
        Task SeenMessage(MessageDto message); // sets the indicator as seen for the other user
    }
}
