using Bongo.Infrastructure;
using Bongo.Models;
using Bongo.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Bongo.Controllers
{
    [ApiController]
    [Route("authorization")]
    public class AccountController : Controller
    {
        private readonly UserManager<BongoUser> _userManager;
        private readonly SignInManager<BongoUser> _signInManager;
        private readonly IConfiguration _config;

        public AccountController(UserManager<BongoUser> userManager, SignInManager<BongoUser> signInManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _config = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody]LoginViewModel model)
        {
            BongoUser user = await _userManager.FindByNameAsync(Encryption.Encrypt(model.Username));
            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, false);
                if (result.Succeeded)
                {
                    string token = GenerateToken(user, model.RememberMe);
                    return Ok(token);
                }
            }
            return BadRequest("Invalid username or password");
        }
        [Authorize]
        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return NoContent();
        }
        private string GenerateToken(BongoUser user, bool isPersistent)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var issuer = jwtSettings["Issuer"];
            var key = jwtSettings["Key"];
            var audience = jwtSettings["Audience"];

            // Create claims for the token
            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
            new Claim(JwtRegisteredClaimNames.Name, user.UserName),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            // Add any additional claims as needed
        };

            // Create credentials
            var securityKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            // Create the token
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(isPersistent ? 14 : 1),
                signingCredentials: credentials
            );

            // Serialize the token to a string
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenString = tokenHandler.WriteToken(token);

            return tokenString;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody]RegisterModel registerModel)
        {
            if (ModelState.IsValid)
            {
                if (await _userManager.FindByNameAsync(Encryption.Encrypt(registerModel.UserName.Trim())) != null)
                {
                    return StatusCode(409, "Username already exists. Please use a different username.");
                }
                var user = new BongoUser
                {
                    UserName = Encryption.Encrypt(registerModel.UserName.Trim()),
                    Email = Encryption.Encrypt(registerModel.Email)
                };

                var result = await _userManager.CreateAsync(user, registerModel.Password);

                if (result.Succeeded)
                {
                    try
                    {
                        //var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                        /* Dictionary<string, string> emailOptions = new Dictionary<string, string>
                         { { "username", user.UserName},
                           { "link",_config.GetValue<string>("Application:AppDomain") + $"Account/ConfirmEmail?userId={user.Id}&token{token}" }
                         };
 */
                        //await _mailSender.SendMailAsync(registerModel.Email, "Welcome to Bongo", "WelcomeEmail", emailOptions);
                        return StatusCode(201);
                    }
                    catch (Exception)
                    {
                        return StatusCode(500,"Something went wrong while registering your account. It's not you, it's us💀");
                    }
                }

                else
                {
                    foreach (var error in result.Errors)
                        ModelState.AddModelError("", error.Description);
                    return StatusCode(406, ModelState);
                }

            }
            return BadRequest();
        }
    }
}
