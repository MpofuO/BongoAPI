using Bongo.Areas.TimetableArea.Infrastructure;
using Bongo.Areas.TimetableArea.Models;
using Bongo.Areas.TimetableArea.Models.ViewModels;
using Bongo.Data;
using Bongo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bongo.Areas.TimetableArea.Controllers;

[ApiController]
[Route("timetablearea/[controller]/[action]")]
[Area("TimetableArea")]
[Authorize]
public class MergerController : Controller
{
    #region Properties
    private static IRepositoryWrapper repository;
    private static UserManager<BongoUser> userManager;
    private static TimetableProcessor processor;
    private static bool _isForFirstSemester;
    private static List<List<Session>> clashes;
    private static List<Lecture> groups;
    private static List<string> mergedUsers;
    private static Session[,] mergedSessions;
    #endregion Properties
    public MergerController(IRepositoryWrapper _repository, UserManager<BongoUser> _userManager)
    {
        repository = _repository;
        userManager = _userManager;
    }

    #region GetMethods

    ///<summary>
    ///Initialises the merger for the current user.
    ///</summary>
    ///<param name="isForFirstSemester"></param> 
    ///<returns>
    ///<list type="string">
    ///<item>StatusCode 202 with a MergerIndexViewModel object if the user's timetable has no issues and the initialization was successful.</item>
    ///<item>StatusCode 204 if the user's timetable does not have any sessions.</item>
    ///<item>StatusCode 400 if the user's timetable has clashes or groups that need to be managed.</item>
    ///<item>Status code 404 if the user does not have a timetable to merge.</item>
    ///</list>
    /// </returns>
    [HttpGet("{isForFirstSemester}")]
    public IActionResult InitialiseMerger(bool isForFirstSemester)
    {
        _isForFirstSemester = isForFirstSemester;
        mergedUsers = new();
        var timetabe = repository.Timetable.GetUserTimetable(User.Identity.Name);
        if (timetabe != null)
            return AddUserTimetable(User.Identity.Name);

        return NotFound("Please create your timetable before you can merge with others.");
    }
    private IActionResult GetMerge()
    {
        var usersKeyValuePairs = (IEnumerable<KeyValuePair<string, string>>)userManager.Users
            .Select(user => new KeyValuePair<string, string>(user.UserName, user.MergeKey));

        var users = new Dictionary<string, string>(usersKeyValuePairs);
        users.Remove(User.Identity.Name);

        return StatusCode(202, new MergerIndexViewModel
        {
            Sessions = SessionControlHelpers.ConvertToList(mergedSessions),
            MergedUsers = mergedUsers,
            Users = users
        });
    }

    ///<summary>
    ///Merges a user's timetable with the existing merged users' timetables.
    ///</summary>
    ///<param name="username">The username of the user whose timetable is being merged.</param> 
    ///<returns>
    ///<list type="string">
    ///<item>StatusCode 202 with a MergerIndexViewModel object if if the user's timetable was successfully merged with.</item>
    ///<item>StatusCode 200 with a MergerIndexViewModel object if if the user's timetable was already merged with.</item>
    ///<item>StatusCode 204 if the user's timetable does not have any sessions.</item>
    ///<item>StatusCode 400 if the user's timetable has clashes or groups that need to be managed.</item>
    ///<item>Status code 404 if the user does not have a timetable to merge.</item>
    ///</list>
    ///</returns>
    [HttpGet("{username}")]
    public IActionResult AddUserTimetable(string username)
    {
        if (mergedUsers.Contains(username))
        {
            return StatusCode(200, $"{username}'s timetable has already been merged with.");
        }
        else
        {
            var timetable = repository.Timetable.GetUserTimetable(username);
            if (timetable != null)
            {
                if (timetable.TimetableText == "")
                {
                    return StatusCode(204, $"Please note that {username}'s timetable has no sessions, no changes will be seen.");
                }

                processor = new TimetableProcessor(timetable.TimetableText, _isForFirstSemester);
                Session[,] newUserSessions = processor.GetSessionsArray(out clashes, out groups);

                if (clashes.Count > 0 || groups.Count > 0)
                {
                    if (username == User.Identity.Name)
                    {
                        return BadRequest("Please ensure that you have managed your clashes and/groups before merging with others.");
                    }
                    else
                    {
                        return BadRequest($"Could not merge with {username}'s timetable.\n" +
                            $"Please ensure that {username} has managed their clashes and/or groups before merging with them.");
                    }
                }
                if (mergedUsers.Count == 0)
                    mergedSessions = newUserSessions.SplitSessions();
                else
                    MergerControlHelpers.Merge(mergedSessions, newUserSessions);

                mergedUsers.Add(username);
                return GetMerge();
            }

            return NotFound($"Could not merge with {username}'s timetable.\n" +
                            $"Please ensure that {username} has created their timetable before merging with them.");
        }
    }

    ///<summary>
    ///Removes a user's timetable from the merged timetable.
    ///</summary>
    ///<param name="username">The username of the user whose timetable must be removed from the merged timetable</param> 
    ///<returns>
    ///<list type="string">
    ///<item>StatusCode 202 if the specified user's timetable was successfully removed from the merged timetable.</item>
    ///<item>StatusCode 404 if the specified user's timetable was never merged with.</item>
    ///</list>
    ///</returns>
    [HttpGet("{username}")]
    public IActionResult RemoveUserTimetable(string username)
    {
        if (mergedUsers.Contains(username))
        {
            var timetable = repository.Timetable.GetUserTimetable(username);
            if (timetable.TimetableText != "")
            {
                processor = new TimetableProcessor(timetable.TimetableText, _isForFirstSemester);
                MergerControlHelpers.UnMerge(mergedSessions, processor.GetSessionsArray(out clashes, out groups));
            }
            mergedUsers.Remove(username);
            return GetMerge();
        }
        return NotFound($"{username}'s timetable was never merged with.");
    }
    #endregion GetMethods
}
