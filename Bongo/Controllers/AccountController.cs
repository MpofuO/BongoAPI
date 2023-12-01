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

        /// <summary>
        /// Logs the user in
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        /// <list type="string">
        /// <item>StatusCode 200 with the user object if the user is successfully authenticated.</item>
        /// <item> StatusCode 400 otherwise</item>
        /// </list>
        /// </returns>
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
            return BadRequest("Invalid email or password");
        }

        /// <summary>
        /// Logs the user out
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        /// <list type="string">
        /// <item>StatusCode 204.</item>
        /// </list>
        /// </returns>
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

        /// <summary>
        /// Registers a user
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        /// <list type="string">
        /// <item>StatusCode 200 if the user is successfully registered.</item>
        /// <item>StatusCode 409 if there was a email conflict.</item>
        /// <item>StatusCode 406 with an IEnumerable object of IdentityError if registration failed at user creation.</item>
        /// <item>StatusCode 400 if the model was not valid.</item>
        /// <item>StatusCode 500 if there was a server error.</item>
        /// </list>
        /// </returns>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterModel registerModel)
        {
            if (ModelState.IsValid)
            {
                if (await _userManager.FindByNameAsync(Encryption.Encrypt(registerModel.UserName.Trim())) != null)
                {
                    return StatusCode(409, "Username already exists. Please use a different email.");
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
                        //var token = await userManager.GeneratePasswordResetTokenAsync(user);
                        /* Dictionary<string, string> emailOptions = new Dictionary<string, string>
                         { { "email", user.UserName},
                           { "link",_config.GetValue<string>("Application:AppDomain") + $"Account/ConfirmEmail?userId={user.Id}&token{token}" }
                         };
 */
                        //await _mailSender.SendMailAsync(registerModel.Email, "Welcome to Bongo", "WelcomeEmail", emailOptions);
                        return Ok();
                    }
                    catch (Exception)
                    {
                        return StatusCode(500, "Something went wrong while registering your account. It's not you, it's us💀");
                    }
                }
                else
                {
                    return StatusCode(406, result.Errors);
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
        //        var user = await userManager.FindByIdAsync(model.UserId);
        //        if (user != null)
        //        {
        //            var result = await userManager.ConfirmEmailAsync(user, model.Token);
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

        /// <summary>
        /// Verifies if a user with the given email exists
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        /// <list type="string">
        /// <item>StatusCode 200 if the user exists, 404 otherwise.</item>
        /// </list>
        /// </returns>
        [HttpGet("VerifyEmail/{email}")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail(string email)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(Encryption.Encrypt(email));
                if (user != null)
                    return Ok();
            }
            return NotFound();
        }

        /// <summary>
        /// Initiates a password reset for a user once the user's answer to security question is validated.
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        /// <list type="string">
        /// <item>StatusCode 200 with the an array containing the reset password token and the userId respectively if the token was successfully created.</item>
        /// <item>StatusCode 400 if the model was not valid.</item>
        /// <item>StatusCode 404 if user with the given email does not exist.</item>
        /// <item>StatusCode 406 if the given answer to the security question is incorrect.</item>
        /// </list>
        /// </returns>
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
                        return await ChangePassword(user.Id, true, model.SecurityAnswer);
                    }
                    return StatusCode(406, $"Incorrect answer. Please try again.");
                }
                return NotFound($"Invalid. User with email {model.Email} does not exist");
            }
            return BadRequest($"Something went wrong with email {model.Email}. Please try again, if the problem persists contact us.");
        }

        /// <summary>
        /// Initiates a change password event by creating a password reset token
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        /// <list type="string">
        /// <item>StatusCode 200 with the an array containing the reset password token and the userId respectively if the token was successfully created.</item>
        /// <item>StatusCode 400 if the request is potentially an attempt wrongfully reset a user's password.</item>
        /// <item>StatusCode 404 if the given userId matches no user.</item>
        /// </list>
        /// </returns>
        [HttpGet("ChangePassword/{userId}")]
        [AllowAnonymous]
        public async Task<IActionResult> ChangePassword(string userId, bool fromForgot = false, string secAnswer = "")
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {

                if (!(fromForgot && user.SecurityAnswer == Encryption.Encrypt(secAnswer)))
                    return BadRequest();

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                return Ok(new string[] { token, user.Id });
            }
            return NotFound();
        }

        /// <summary>
        /// Resets a user's password.
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        /// <list type="string">
        /// <item>StatusCode 200 if the password was successfully reset.</item>
        /// <item>StatusCode 406 with an IEnumerable object of IdentityError if resetting the password failed.</item>
        /// <item>StatusCode 400 if model was not valid.</item>
        /// <item>StatusCode 404 if the given userId matches no user.</item>
        /// </list>
        /// </returns>
        [HttpPost("ResetPassword")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPassword model)
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
                    return StatusCode(406, result.Errors);
                }
                else
                    return NotFound();
            }
            return BadRequest();
        }

        /// <summary>
        /// Updates a user's security question and it's answer.
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        /// <list type="string">
        /// <item>StatusCode 200 if the security question and answer were successfully set.</item>
        /// <item>StatusCode 400 if model was not valid.</item>
        /// <item>StatusCode 404 if the given userId matches no user.</item>
        /// </list>
        /// </returns>
        [HttpPost("UpdateSecurityQuestion")]
        public async Task<IActionResult> UpdateSecurityQuestion([FromBody] SecurityQuestionViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(User.Identity.Name);
                if (user is null)
                    return NotFound();

                user.SecurityQuestion = Encryption.Encrypt(model.SecurityQuestion);
                user.SecurityAnswer = Encryption.Encrypt(model.SecurityAnswer);
                await _userManager.UpdateAsync(user);

                return Ok();
            }
            return BadRequest();
        }
    }
}
