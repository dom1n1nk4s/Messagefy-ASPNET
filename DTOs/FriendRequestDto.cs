using System;

namespace API.DTOs
{
    public class FriendRequestDto
    {
        public bool IsOutbound { get; set; }
        public string Image { get; set; }
        public string DisplayName { get; set; }
        public string RequestId {get;set;}
    }
}