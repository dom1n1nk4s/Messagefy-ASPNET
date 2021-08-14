using System;

namespace API.Models
{
    public class File
    {
        public Guid Id { get; set; }
        public string FileName { get; set; }
        public byte[] Data { get; set; }
        public Conversation Conversation { get; set; }
    }
}