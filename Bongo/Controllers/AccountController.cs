using Bongo.Infrastructure;
using Bongo.Models;
using Bongo.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

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
        public async Task<IActionResult> Login([FromBody] LoginViewModel model)
        {
            BongoUser user = await _userManager.FindByEmailAsync(Encryption.Encrypt(model.Email));
            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, false);
                if (result.Succeeded)
                {
                    user.Token = GenerateToken(user, model.RememberMe);
                    return Ok(user.DecryptUser());
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
        public async Task<IActionResult> Register([FromBody] RegisterModel registerModel)
        {
            if (ModelState.IsValid)
            {
                if (await _userManager.FindByNameAsync(Encryption.Encrypt(registerModel.UserName.Trim())) != null)
                {
                    return StatusCode(409, "Username already exists. Please use a different username.");
                }
                var user = new BongoUser
                {
                    UserName = registerModel.UserName.Trim(),
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
                        return StatusCode(500, "Something went wrong while registering your account. It's not you, it's us💀");
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

        //[HttpGet]
        //[AllowAnonymous]
        //public IActionResult ConfirmEmail(string userId, string token)
        //{
        //    return View(new ConfirmEmail { UserId = userId, Token = token });
        //}
        //[HttpPost]
        //[AllowAnonymous]
        //public async Task<IActionResult> ConfirmEmail(ConfirmEmail model)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        var user = await _userManager.FindByIdAsync(model.UserId);
        //        if (user != null)
        //        {
        //            var result = await _userManager.ConfirmEmailAsync(user, model.Token);
        //            if (result.Succeeded)
        //            {

        //            }
        //            else
        //            {

        //            }
        //            TempData["Message"] = "Email verified successfully";
        //        }
        //        TempData["Message"] = "Something went wrong😐.";

        //        return RedirectToAction("SignIn");
        //    }
        //    return View(model);

        //}

        [HttpGet("VerifyUsername/{username}")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyUsername(string username)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(Encryption.Encrypt(username));
                if (user != null)
                    return Ok();
            }
            return NotFound();
        }

        [HttpPost("ForgotPassword")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] AnswerSecurityQuestionViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(Encryption.Encrypt(model.Email));
                if (user != null)
                {
                    if (user.SecurityAnswer.ToLower().Trim() == Encryption.Encrypt(model.SecurityAnswer.ToLower().Trim()))
                    {
                        return await ChangePassword(user.Id);
                    }
                    return StatusCode(406, $"Incorrect answer. Please try again.");
                }
                return NotFound($"Invalid. User with email {model.Email} does not exist");
            }
            return BadRequest($"Something went wrong with username {model.Email}. Please try again, if the problem persists contact us.");
        }

        [HttpGet("ChangePassword")]
        [AllowAnonymous]
        public async Task<IActionResult> ChangePassword(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                return Ok(token);
            }
            return NotFound();
        }
        [HttpPost("ResetPassword")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody]ResetPassword model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user != null)
                {
                    var result = await _userManager.ResetPasswordAsync(user, model.Token, model.ConfirmPassword);
                    if (result.Succeeded)
                    {
                        return Ok();
                    }
                    foreach (var error in result.Errors)
                        ModelState.AddModelError("", error.Description);
                }
            }
            return BadRequest(ModelState);
        }

        [HttpPost("UpdateSecurityQuestion")]
        [AllowAnonymous]
        public async Task<IActionResult> UpdateSecurityQuestion([FromBody]SecurityQuestionViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(Encryption.Encrypt(model.UserName));
                user.SecurityQuestion = Encryption.Encrypt(model.SecurityQuestion);
                user.SecurityAnswer = Encryption.Encrypt(model.SecurityAnswer);
                await _userManager.UpdateAsync(user);

                return Ok();
            }
            return BadRequest();
        }
    }
}
