using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace API.Models
{
    public class Message
    {
        public Guid Id { get; set; }
        [JsonIgnore]
        public Conversation Conversation { get; set; }
        public string Content { get; set; }
        public DateTime Date { get; set; }
        public string SenderId { get; set; }
        public DateTime DateEdited { get; set; }
        public bool IsSeen { get; set; }
    }
}