using System;

namespace API.DTOs
{
    public class MessageDto
    {
        public string MessageId { get; set; }
        public string Content { get; set; }
        public string Date { get; set; }
        public string SenderName { get; set; }
        public string DateEdited { get; set; }
    }
}