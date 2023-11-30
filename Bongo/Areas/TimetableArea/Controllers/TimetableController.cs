using Bongo.Areas.TimetableArea.Infrastructure;
using Bongo.Areas.TimetableArea.Models;
using Bongo.Data;
using Bongo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace Bongo.Areas.TimetableArea.Controllers;

[ApiController]
[Route("timetablearea/[controller]/[action]")]
[Area("TimetableArea")]
[Authorize]
public class TimetableController : Controller
{
    private IRepositoryWrapper _repository;

    public TimetableController(IRepositoryWrapper repo, UserManager<BongoUser> userManager)
    {
        _repository = repo;
    }

    #region GetMethods

    ///<summary>
    ///Gets the current user's timetable.
    ///</summary>
    ///<returns>
    ///<list type="string">
    ///<item>StatusCode 200 with a Timetable object if the user's timetable exists.</item>
    ///<item>StatusCode 404 with a Timetable object if the user's timetable does exists.</item>
    ///</list>
    /// </returns>
    [HttpGet]
    public IActionResult GetUserTimetable()
    {
        var table = _repository.Timetable.GetUserTimetable(User.Identity.Name);
        if (table is not null)
            return Ok(table);

        return NotFound();
    }

    ///<summary>
    ///Clears the text of the current user's timetable or deletes the user's timetable.
    ///</summary>
    ///<param name="id">The identifier of the clearing process. Set to 0 if the timetable has to be deleted and 1 if only the text is cleared.</param> 
    ///<returns>
    ///<list type="string">
    ///<item>StatusCode 204 if the clearing process was successful.</item>
    ///<item>StatusCode 400 if the clearing process was unsuccessful.</item>
    ///<item>StatusCode 404 if the user does not have a timetable.</item>
    ///</list>
    ///</returns>
    [HttpGet]
    public IActionResult ClearUserTable(int id)
    {
        Timetable table = _repository.Timetable.GetUserTimetable(User.Identity.Name);
        var moduleColor = _repository.ModuleColor.GetByCondition(m => m.Username == User.Identity.Name);
        if (table != null)
        {
            try
            {
                if (id == 0)
                    _repository.Timetable.Delete(table);
                else
                {
                    table.TimetableText = "";
                    _repository.Timetable.Update(table);
                }

                if (moduleColor != null)
                {
                    foreach (var item in moduleColor)
                    {
                        _repository.ModuleColor.Delete(item);
                    }
                }
                _repository.SaveChanges();

                return NoContent();
            }
            catch
            {
                return BadRequest("Something went wrong");
            }
        }

        return NotFound("Timetable does not exist.");
    }
    #endregion GetMethods

    #region PostMethods

    ///<summary>
    ///Updates or creates the current user's timetable. 
    ///</summary>
    ///<param name="text">The text where the timetable's text will be extracted.</param> 
    ///<returns>
    ///<list type="string">
    ///<item>StatusCode 200 if the timetable was successfully created or updated.</item>
    ///<item>StatusCode 400 if the given text is invalid.</item>
    ///</list>
    /// </returns>
    [HttpPost]
    public IActionResult UpdateOrCreate(string text)
    {
        //Remove unwanted text
        Regex patternTop = new Regex(@"(\d{4}) CLASS TIMETABLE\n(\d{10})");
        Match match = patternTop.Match(text);
        if (match.Success)
        {
            Regex pattern = new Regex(@"205 Nelson Mandela Drive  \|  Park West, Bloemfontein 9301 \| South Africa\nP\.O\. Box 339  \|  Bloemfontein 9300  \|  South Africa \| www\.ufs\.ac\.za|\nVenue Start End Day From To|Venue Start End Day From To\n");//|\(Group [A-Z]{1,2}\)|
            text = pattern.Replace(text, String.Empty);

            Timetable newTimetable = _repository.Timetable.GetUserTimetable(User.Identity.Name) ?? new Timetable { TimetableText = text, Username = User.Identity.Name };
            _repository.Timetable.Update(newTimetable);
            SessionControlHelpers.AddNewUserModuleColor(ref _repository, User.Identity.Name, newTimetable.TimetableText);
            _repository.SaveChanges();

            return Ok("Timetable created/updated successfully");
        }
        else
            return BadRequest("Something went wrong while uploading timetable. " +
                "\n Please make sure your have uploaded your personal timetable");
    }
    #endregion PostMethods
}