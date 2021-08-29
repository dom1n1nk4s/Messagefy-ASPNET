using System;

namespace API.DTOs
{
    public class GroupEditDto
    {
        public Guid Id { get; set; }
        public string Parameter { get; set; }

        public void Deconstruct(out Guid id, out string parameter)
        {
            id = Id;
            parameter = Parameter;
        }
    }
}