using System.Security.Claims;
using System.Threading.Tasks;
using API.DTOs;
using API.HelperFunctions;
using API.Models;
using API.Services;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        public UserManager<AppUser> UserManager { get; }
        public SignInManager<AppUser> SignInManager { get; }
        private readonly TokenService tokenService;
        private readonly Context context;
        private readonly ImageFunctions imageFunctions;

        public AccountController(UserManager<AppUser> UserManager, SignInManager<AppUser> signInManager, TokenService tokenService, Context context)
        {
            this.tokenService = tokenService;
            this.context = context;
            this.SignInManager = signInManager;
            this.UserManager = UserManager;
            imageFunctions = new ImageFunctions(UserManager, context);

        }
        [HttpPost("login")]
        public async Task<ActionResult<AuthenticatedDto>> Login(LoginDto loginDto)
        {
            var user = await UserManager.FindByEmailAsync(loginDto.Email);

            if (user == null) return Unauthorized("No such user found");

            var result = await SignInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (result.Succeeded)
            {
                return Ok(await CreateUserObject(user));
            }
            return Unauthorized(result.ToString());
        }
        [HttpPost("register")]
        public async Task<ActionResult<AuthenticatedDto>> Register(RegisterDto registerDto)
        {
            if (await UserManager.Users.AnyAsync(x => x.Email == registerDto.Email))
            {
                return BadRequest("Email taken");
            }
            if (await UserManager.Users.AnyAsync(x => x.UserName == registerDto.UserName))
            {
                return BadRequest("Username taken");
            }

            var user = new AppUser
            {
                DisplayName = registerDto.DisplayName,
                Email = registerDto.Email,
                UserName = registerDto.UserName,
            };
            var result = await UserManager.CreateAsync(user, registerDto.Password);
            if (result.Succeeded)
            {
                return Ok(await CreateUserObject(user));
            }
            return BadRequest(result.ToString());
        }
        [Authorize]
        [HttpGet]
        public async Task<ActionResult<AuthenticatedDto>> GetCurrentUser()
        {
            var user = await UserManager.FindByEmailAsync(User.FindFirstValue(ClaimTypes.Email));

            return await CreateUserObject(user);
        }
        private async Task<AuthenticatedDto> CreateUserObject(AppUser user)
        {
            return new AuthenticatedDto
            {
                DisplayName = user.DisplayName,
                Image = await imageFunctions.GetUserImageAsync(user.UserName),
                Token = tokenService.CreateToken(user),
                Username = user.UserName,

            };
        }
    }
}