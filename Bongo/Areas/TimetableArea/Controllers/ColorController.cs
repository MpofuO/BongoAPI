using Bongo.Areas.TimetableArea.Infrastructure;
using Bongo.Areas.TimetableArea.Models;
using Bongo.Data;
using Bongo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;

namespace Bongo.Areas.TimetableArea.Controllers
{
    [ApiController]
    [Route("timetablearea/[controller]/[action]")]
    [Area("TimetableArea")]
    [Authorize]
    public class ColorController : ControllerBase
    {
        private static IRepositoryWrapper repository;
        public ColorController(IRepositoryWrapper _repository)
        {
            repository = _repository;
        }

        [HttpGet]
        public IActionResult GetAllColors()
        {
            return Ok(repository.Color.FindAll());
        }

        [HttpGet("{moduleCode}")]
        public IActionResult GetModuleColorWithColorDetails(string moduleCode)
        {
            var moduleColor = repository.ModuleColor.GetModuleColorWithColorDetails(User.Identity.Name, moduleCode);
            return Ok(moduleColor);
        }

        [HttpGet]
        public IActionResult GetModulesWithColors()
        {
            return Ok(repository.ModuleColor.GetByCondition(mc => mc.Username == User.Identity.Name));
        }
    }
}
