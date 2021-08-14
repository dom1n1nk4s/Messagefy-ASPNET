using System;
using System.Collections.Generic;

namespace API.Models
{
    public class Conversation
    {
        public Conversation()
        {
            Messages = new List<Message>();
            Recipients = new List<Recipient>();
            Files = new List<File>();
        }
        public Conversation(string Id1, string Id2)
        {
            Messages = new List<Message>();
            Recipients = new List<Recipient>(){
                new Recipient{
                    UserId = Id1
                },
                new Recipient{
                    UserId = Id2
                }
            };
            Files = new List<File>();
        }

        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Image { get; set; }
        public bool IsGroup { get; set; }
        public virtual ICollection<Message> Messages { get; private set; }
        public virtual ICollection<Recipient> Recipients { get; private set; }
        public virtual ICollection<File> Files { get; private set; }
        public Guid FriendId { get; set; }
        public Friend Friend { get; set; }
    }
}