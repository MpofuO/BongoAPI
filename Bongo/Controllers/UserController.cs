using Bongo.Infrastructure;
using Bongo.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bongo.Controllers
{
    [ApiController]
    [Route("User/[action]")]
    public class UserController : Controller
    {
        private readonly UserManager<BongoUser> userManager;

        public UserController(UserManager<BongoUser> _userManager)
        {
            userManager = _userManager;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BongoUser user)
        {
            var result = await userManager.CreateAsync(user.EncryptUser());
            if (result.Succeeded)
                return Ok();
            return BadRequest();
        }
        [HttpPost]
        public async Task<IActionResult> Update([FromBody] BongoUser user)
        {
            var result = await userManager.UpdateAsync(user.EncryptUser());
            if (result.Succeeded)
                return Ok();
            return BadRequest();
        }
        [HttpPost]
        public async Task<IActionResult> Delete([FromBody] BongoUser user)
        {
            var result = await userManager.DeleteAsync(user.EncryptUser());
            if (result.Succeeded)
                return Ok();
            return BadRequest();
        }
    }
}
