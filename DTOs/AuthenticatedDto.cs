using System;
using System.Collections.Generic;
using API.Models;

namespace API.DTOs
{
    public class AuthenticatedDto : UserDto
    {
        public string Token { get; set; }

    }
}