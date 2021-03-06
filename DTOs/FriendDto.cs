namespace API.DTOs
{
    public class FriendDto
    {
        public string DisplayName { get; set; }
        public string UserName { get; set; }
        public string Image { get; set; }
        public string ConversationId { get; set; }
        public int MessageCount { get; set; }
        public string LastSeenMessageId { get; set; }
        public string LastMessageContent { get; set; }
        public string LastMessageDate { get; set; }
        public bool LastMessageIsReferenceToFile { get; set; }
    }
}