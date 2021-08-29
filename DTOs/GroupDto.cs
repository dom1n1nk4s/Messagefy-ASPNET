using System;
using System.Collections.Generic;

namespace API.DTOs
{
    public class GroupDto
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public List<string> Recipients { get; set; }
        public string Image { get; set; }
        public int MessageCount { get; set; }
        public string LastMessageContent { get; set; }
        public string LastMessageDate { get; set; }
        public bool LastMessageIsReferenceToFile { get; set; }
    }
}