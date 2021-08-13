using System;
using System.Text.Json.Serialization;

namespace API.Models
{
    public class Recipient
    {
        public Guid Id { get; set; }
        [JsonIgnore]
        public Conversation Conversation { get; set; }
        public string UserId { get; set; }
        public Guid LastSeenMessageId { get; set; }
    }
}