namespace API.DTOs
{
    public class FriendDto
    {
        public int MessageCount { get; set; }
        public string DisplayName { get; set; }
        public string UserName { get; set; }
        public string Image { get; set; }
        public string LastMessageContent { get; set; }
        public string LastMessageDate { get; set; }
    }
}