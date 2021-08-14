using System;

namespace API.Models
{
    public class FriendRequest
    {
        public Guid Id { get; set; }
        public string SenderId { get; set; }
        public string RecipientId { get; set; }
    }
}