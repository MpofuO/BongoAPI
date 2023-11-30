using Bongo.Areas.TimetableArea.Infrastructure;
using Bongo.Areas.TimetableArea.Models;
using Bongo.Areas.TimetableArea.Models.ViewModels;
using Bongo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace Bongo.Areas.TimetableArea.Controllers;

[ApiController]
[Route("timetablearea/[controller]/[action]")]
[Area("TimetableArea")]
[Authorize]
public class SessionController : Controller
{
    #region Properties
    private static IRepositoryWrapper _repository;
    private static Timetable table;
    private List<List<Session>> ClashesList;
    private List<Lecture> GroupedList;
    private static TimetableProcessor processor;
    private static Session[,] data;
    private static bool _isForFirstSemester;
    #endregion Properties

    public SessionController(IRepositoryWrapper repository)
    {
        _repository = repository;
    }

    #region PrivateMethods
    private bool Initialise()
    {
        table = _repository.Timetable.GetUserTimetable(User.Identity.Name);
        if (table is not null)
        {
            processor = new TimetableProcessor(table.TimetableText, _isForFirstSemester);
            return true;
        }
        return false;
    }
    private void SetCookie(string key, string value)
    {
        CookieOptions cookieOptions = new CookieOptions { Expires = DateTime.Now.AddDays(90) };
        Response.Cookies.Append(key, value == null ? "" : value, cookieOptions);
    }
    private void RemoveSelectedWhereNecessary(string type, List<Lecture> groups = default)
    {

        if (type.ToLower() == "class")
        {
            List<List<Session>> clashes = processor.GetClashingSessions(true);
            foreach (List<Session> list in clashes)
                foreach (Session s in list)
                    table.TimetableText = table.TimetableText.Replace(s.sessionInPDFValue, s.sessionInPDFValue.Replace("selectedClass", ""));
        }
        else
        {

            foreach (Lecture lect in groups)
                foreach (Session s in lect.sessions)
                    if (type.ToLower() == "group")
                        table.TimetableText = table.TimetableText.Replace(s.sessionInPDFValue, s.sessionInPDFValue.Replace("selectedGroup", ""));
                    else
                        table.TimetableText = table.TimetableText.Replace(s.sessionInPDFValue, s.sessionInPDFValue.Replace("ignored", ""));
        }
    }
    private bool AddNewSession(AddSessionViewModel model, bool groupConfirmed = false)
    {
        string moduleCode = model.ModuleCode.ToUpper();
        int moduleIndex = table.TimetableText.IndexOf(moduleCode);
        if (moduleIndex != -1)
        {
            int sessionTypeIndex = moduleIndex + table.TimetableText.Substring(moduleIndex).IndexOf($"{model.SessionType} {model.SessionNumber}");
            if (sessionTypeIndex != moduleIndex - 1)
            {
                string text = table.TimetableText.Substring(moduleIndex + 8, sessionTypeIndex - moduleIndex);
                Regex modulepattern = new Regex(@"[A-Z]{4}[\d]{4}|CLASH!![\d]");
                Match match = modulepattern.Match(text);
                if (!match.Success)
                {
                    if (groupConfirmed)
                    {
                        int index = sessionTypeIndex + $"{model.SessionType} {model.SessionNumber}".Length;
                        string newSessionText = $"{model.SessionType} {model.SessionNumber}\n{model.Venue} {model.startTime} {model.endTime} {model.Day}selectedGroup";
                        Regex breakPoint = new Regex(@"[A-Z]{4}[\d]{4}|Lecture [0-9]?|Tutorial [0-9]?|Practical [0-9]?");
                        Match breakMatch = breakPoint.Match(table.TimetableText.Substring(index));

                        if (breakMatch.Success)
                        {
                            string oldSessionText = table.TimetableText.Substring(sessionTypeIndex + $"{model.SessionType} {model.SessionNumber}".Length, breakMatch.Index);
                            string newText = newSessionText.Replace($"{model.SessionType} {model.SessionNumber}", "") + oldSessionText.Replace("selectedGroup", "");
                            table.TimetableText = table.TimetableText.Replace(oldSessionText, newText);
                        }
                        else
                            table.TimetableText = table.TimetableText.Replace(table.TimetableText.Substring(sessionTypeIndex),
                                $"{newSessionText}\n{table.TimetableText.Substring(sessionTypeIndex + $"{model.SessionType + model.SessionNumber}".Length + 1)}\n");
                    }
                    else
                        return false;
                }
                else
                {
                    table.TimetableText = table.TimetableText.Replace(table.TimetableText.Substring(moduleIndex), $"{moduleCode}\n" +
                    $"{model.SessionType} {model.SessionNumber}\n{model.Venue} {model.startTime} {model.endTime} {model.Day}\n" +
                    table.TimetableText.Substring(moduleIndex + 8) + "\n");
                }
            }
            else
            {
                table.TimetableText = table.TimetableText.Replace(table.TimetableText.Substring(moduleIndex), $"{moduleCode}\n" +
                    $"{model.SessionType} {model.SessionNumber}\n{model.Venue} {model.startTime} {model.endTime} {model.Day}\n" +
                    table.TimetableText.Substring(moduleIndex + 8) + "\n");
            }
        }
        else
        {
            table.TimetableText = $"{table.TimetableText}{moduleCode}" +
                $"\n{model.SessionType} {model.SessionNumber}\n{model.Venue} {model.startTime} {model.endTime} {model.Day}\n";
            _repository.ModuleColor.Create(new ModuleColor
            {
                ColorId = _repository.Color.GetByName("no-color").ColorId,
                Username = User.Identity.Name,
                ModuleCode = moduleCode

            });
        }
        return true;
    }
    private void UpdateAndSave()
    {
        _repository.Timetable.Update(table);
        _repository.SaveChanges();
    }
    #endregion PrivateMethods

    #region GetMethods

    ///<summary>
    ///Gets the current user's sessions for the specified semester.
    ///</summary>
    ///<param name="isForFirstSemester">Specifies if the required sessions are for the first semester.</param> 
    ///<returns>
    ///<list type="string">
    ///<item>StatusCode 200 with a list of Session objects the represents the user's timetable sessions</item>
    ///<item>StatusCode 400 if the user has clashes or groups that need to be handled.</item>
    ///<item>StatusCode 404 if the user does not have a timetable.</item>
    ///</list>
    ///</returns>
    [HttpGet("isForFirstSemester")]
    public IActionResult GetTimetableSessions(bool isForFirstSemester)
    {
        _isForFirstSemester = isForFirstSemester;
        if (Initialise())
        {
            data = processor.GetSessionsArray(out ClashesList, out GroupedList);

            if (ClashesList.Count > 0 || GroupedList.Count > 0)
                return BadRequest("Please manage your groups/clashes");

            return Ok(SessionControlHelpers.ConvertToList(data));
        }
        return NotFound("User does not have a timetable");
    }

    ///<summary>
    ///Gets the clasing sessions for the current user.
    ///</summary>
    ///<returns>
    ///<list type="string">
    ///<item>StatusCode 200 with a list of list of Sessions objects representing the clashes</item>
    ///<item>StatusCode 400 if the user does not have a timetable</item>
    ///</list>
    ///</returns>
    [HttpGet]
    public IActionResult GetClashes()
    {
        if (Initialise())
        {
            ClashesList = processor.GetClashingSessions(true);
            return Ok(ClashesList);
        }
        return BadRequest("User does not have a timetable");
    }

    ///<summary>
    ///Gets the lectures that have groups for the current user.
    ///</summary>
    ///<returns>
    ///<list type="string">
    ///<item>StatusCode 200 with a list of Lecture objects representing the clashes</item>
    ///<item>StatusCode 400 if the user does not have a timetable</item>
    ///</list>
    ///</returns>
    [HttpGet]
    public IActionResult GetGroups()
    {
        if (Initialise())
        {
            GroupedList = processor.GetGroupedLectures(true, true);
            return Ok(GroupedList);
        }
        return BadRequest("User does not have a timetable");
    }

    ///<summary>
    ///Gets the details of a given session.
    ///</summary>
    ///<param name="sessionInPDFValue">The value of the session in the timetable's text</param> 
    ///<returns>
    ///<list type="string">
    ///<item>StatusCode 200 with a SessionModuleColorViewModel object containing the session details.</item>
    ///<item>StatusCode 400 if the value of the session is null.</item>
    ///<item>StatusCode 404 if the value of the session is does not match any session in the timetable.</item>
    ///</list>
    ///</returns>
    [HttpGet("{sessionInPDFValue}")]
    public IActionResult GetSessionDetails(string sessionInPDFValue)
    {
        if (sessionInPDFValue != null)
        {
            Session _session;

            Initialise();
            var arr = data;
            for (int i = 0; i < arr.GetLength(0); i++)
            {
                for (int j = 0; j < arr.GetLength(1); j++)
                {
                    if (arr[i, j] != null && arr[i, j].sessionInPDFValue.Contains(sessionInPDFValue))
                    {
                        _session = arr[i, j];

                        ModuleColor moduleColor = _repository.ModuleColor
                            .GetModuleColorWithColorDetails(User.Identity.Name, _session.ModuleCode);

                        return Ok(new SessionModuleColorViewModel
                        {
                            ModuleColor = moduleColor,
                            Session = _session,
                            Colors = _repository.Color.FindAll()
                        });
                    }
                }
            }
            return NotFound("Session does not exist");
        }
        return BadRequest();
    }

    ///<summary>
    ///Randomly sets the colors for the current user's modules.
    ///</summary>
    ///<returns>
    ///StatusCode 204.
    ///</returns>
    [HttpGet]
    public IActionResult SetColorsRandomly()
    {
        var lstModuleColor = _repository.ModuleColor.GetByCondition(m => m.Username == User.Identity.Name).ToList();
        int colorId = 1;
        foreach (var moduleColor in lstModuleColor)
        {
            moduleColor.ColorId = colorId++;
            _repository.ModuleColor.Update(moduleColor);
            colorId = colorId > 14 ? 1 : colorId + 0;
        }
        _repository.SaveChanges();
        return NoContent();
    }

    /// <summary>
    /// Gets the modules with colors for management.
    /// </summary>
    /// <returns>StatusCode 200 with a ModulesColorsViewModel object containing lists of ModuleColor and Color objects</returns>
    [HttpGet]
    public IActionResult GetModulesWithColors()
    {
        var colors = _repository.Color.FindAll();
        var moduleColors = _repository.ModuleColor.GetByCondition(m => m.Username == User.Identity.Name);
        var x = moduleColors.Where(m =>
             _isForFirstSemester ? (int.Parse(m.ModuleCode.Substring(6, 1)) == 0 || int.Parse(m.ModuleCode.Substring(6, 1)) % 2 == 1)
                            : int.Parse(m.ModuleCode.Substring(6, 1)) % 2 == 0);
        return Ok(new ModulesColorsViewModel()
        {
            ModuleColors = x,
            Colors = colors
        });
    }

    #endregion GetMethods

    #region PostMethods

    ///<summary>
    ///Add a new session to the timetable.
    ///</summary>
    ///<param name="model"></param> 
    ///<returns>
    ///<item>StatusCode 200 if the session has successfully been added.</item>
    ///<item>StatusCode 409 if the added session is conflicting with an already existing session on the timetable.</item>
    ///<item>StatusCode 400 if the model is invalid.</item>
    ///</returns>
    [HttpPost]
    public IActionResult AddSession([FromBody] AddSessionViewModel model)
    {
        if (ModelState.IsValid)
        {
            if (AddNewSession(model))
                return Ok("Session added successfully");
            UpdateAndSave();
            return StatusCode(409, "Please confirm adding this session in place of existing one.");
        }

        return BadRequest("Unable to add the session.");
    }

    ///<summary>
    ///Confirms that an added session can be added in place of already existing one if they are now of the same session.
    ///</summary>
    ///<param name="model">The session being added.</param> 
    ///<returns>
    ///<list type="string">
    ///<item>StatusCode 200 if the session has successfully been added.</item>
    ///<item>StatusCode 400 if the model is invalid.</item>
    ///</list>
    ///</returns>
    [HttpPost]
    public IActionResult ConfirmGroup([FromBody] AddSessionViewModel model)
    {
        if (ModelState.IsValid)
        {
            AddNewSession(model, true);
            UpdateAndSave();
            return Ok();
        }

        return BadRequest("Model was not valid.");
    }

    ///<summary>
    ///Handles the user's clashing sessions according to their selection.
    ///</summary>
    ///<param name="Sessions"></param> 
    ///<returns>
    ///<list type="string">
    ///<item>Status code 200 if clashes have successfully been handled</item>
    ///<item>Status code 400 if user has selected sessions that may be clashing with each other or with already existing sessions on the timetable.</item>
    ///</list>
    ///</returns>
    [HttpPost]
    public IActionResult HandleClashes([FromBody] string[] Sessions)
    {
        RemoveSelectedWhereNecessary("class");

        foreach (string session in Sessions)
        {
            if (SessionControlHelpers.ContainsClashes(Sessions))
            {
                return BadRequest("You have selected clashing sessions");
            }

            if (session is not null)
            {
                if (!session.Contains("Tap to handle") || (session.Contains("Tap to handle") && table.TimetableText.Contains(session)))
                    table.TimetableText = table.TimetableText.
                        Replace(session, session + "selectedClass");
                else
                {
                    Regex timepattern = new Regex(@"[\d]{2}:[\d]{2} [\d]{2}:[\d]{2}");
                    Regex daypattern = new Regex(@"Monday|Tuesday|Wednesday|Thursday|Friday");
                    Match timeMatch = timepattern.Match(session);
                    string startTime = timeMatch.Value.Substring(0, 5);
                    string endTime = timeMatch.Value.Replace(startTime + " ", "");
                    int semesterNo = Request.Cookies["isForFirstSemester"] == "true" ? 1 : 2;
                    AddNewSession(new AddSessionViewModel
                    {
                        ModuleCode = $"CCCC12{semesterNo}3",
                        SessionType = "Lecture",
                        SessionNumber = 1,
                        startTime = startTime,
                        endTime = endTime,
                        Day = $"{daypattern.Match(session).Value}selectedClass",
                        Venue = "Tap to handle"

                    });
                }

            }
        }
        UpdateAndSave();

        return Ok();
    }

    ///<summary>
    ///Handles the user's grouped sessions according to their selection.
    ///</summary>
    ///<param name="model"></param> 
    ///<returns>
    ///<list type="string">
    ///<item>Status code 200 if groups have successfully been handled</item>
    ///<item>Status code 400 if user has selected sessions that may be clashing with each other or with already existing sessions on the timetable.</item>
    ///</list>
    ///</returns>
    [HttpPost]
    public IActionResult HandleGroups([FromBody] GroupsViewModel model)
    {
        List<Lecture> grouped = processor.GetGroupedLectures();
        List<string> sessions = new List<string>(model.Sessions);

        RemoveSelectedWhereNecessary("ignored", grouped);
        if (model.Ignore != null)
            foreach (string s in model.Ignore)
            {
                table.TimetableText = table.TimetableText.Replace(s, s + "ignored");
                sessions.Remove(s);
            }

        string[] strings = SessionControlHelpers.GetClashing(sessions.ToArray());
        if (strings.Count() > 0)
        {
            foreach (string item in strings)
            {
                sessions.Remove(item);
            }
        }

        List<Lecture> clashing = new();
        List<Lecture> unclashing = new();

        if (strings.Length > 0)
        {
            foreach (var session in strings)
            {
                foreach (var lect in grouped)
                {
                    if (lect.sessions.Select(l => l.sessionInPDFValue).Contains(session))
                    {
                        clashing.Add(lect);
                    }
                    else
                        unclashing.Add(lect);

                }
            }
        }

        RemoveSelectedWhereNecessary("group", unclashing);

        foreach (string session in sessions)
        {
            if (session is not null)
            {
                Lecture sessionLecture = grouped.FirstOrDefault(lect => lect.sessions.Select(s => s.sessionInPDFValue).Contains(session));
                if (model.SameGroups != null && model.SameGroups.Contains($"{sessionLecture.ModuleCode} {sessionLecture.LectureDesc}"))
                {
                    Regex groupPattern = new Regex(@"Group [A-Z]{1,2}[\d]?");
                    foreach (Session s in sessionLecture.sessions)
                        if (groupPattern.Match(s.sessionInPDFValue).Success)
                            table.TimetableText = table.TimetableText.Replace(s.sessionInPDFValue, s.sessionInPDFValue + "selectedGroup");
                }
                else
                    table.TimetableText = table.TimetableText.Replace(session, session + "selectedGroup");
            }
        }

        UpdateAndSave();
        if (strings.Length > 0)
        {
            return BadRequest("You have selected clashing sessions. Please ensure you don't select sessions that are clashing with each other or with sessions that are already there.");
        }

        return Ok();

    }

    ///<summary>
    ///Updates module colors for current user.
    ///</summary>
    ///<param name="model"></param> 
    ///<returns>
    ///<list type="string">
    ///<item>StatusCode 204 once the module colors have been successfully updated.</item>
    ///<item>StatusCode 500 if something went wrong.</item>
    /// </list>
    ///</returns>
    [HttpPost]
    public IActionResult UpdateModuleColor([FromBody]SessionModuleColorsUpdate model)
    {
        try
        {
            if (model.ColorId.Count() > 0)
            {
                for (int i = 0; i < model.ColorId.Count(); i++)
                {
                    Color color = _repository.Color.GetById(model.ColorId[i]);
                    ModuleColor moduleColor = _repository.ModuleColor.GetById(model.ModuleColorId[i]);
                    moduleColor.Color = color;
                    _repository.ModuleColor.Update(moduleColor);
                }
                if (model.ColorId.Count() == 1 && model.View == "Details")
                {
                    Regex timePattern = new Regex(@"(\d{2}:\d{2}) (\d{2}:\d{2})");
                    string newSessionInPDFValue = model.oldSessionInPDFValue.Replace(model.oldSessionInPDFValue.
                        Substring(0, timePattern.Match(model.oldSessionInPDFValue).Index), $"{model.Venue} ");
                    table.TimetableText = table.TimetableText.Replace(model.oldSessionInPDFValue, newSessionInPDFValue);
                }
                UpdateAndSave();
            }
            return StatusCode(204, "Changes saved successfully.");
        }
        catch
        {
            return StatusCode(500, "Something went wrong. Please try again.");
        }
    }
    #endregion PostMethods

    #region DeleteMethods

    ///<summary>
    ///Deletes a module from the timetable.
    ///</summary>
    ///<param name="moduleCode">The module code of the module to be deleted.</param> 
    ///<returns>
    ///StatusCode 200 after the module has been deleted.
    ///</returns>
    [HttpDelete("{moduleCode}")]
    public IActionResult DeleteModule(string moduleCode)
    {
        int moduleIndex = table.TimetableText.IndexOf(moduleCode);
        if (moduleIndex != -1)
        {
            Regex modPattern = new Regex(@"[A-Z]{4}[\d]{4}");
            Match nextModule = modPattern.Match(table.TimetableText.Substring(moduleIndex + moduleCode.Length));
            if (nextModule.Success)
            {
                int nextModuleIndex = moduleIndex + nextModule.Index;
                string whole = table.TimetableText.Substring(moduleIndex, nextModuleIndex - moduleIndex);
                table.TimetableText = table.TimetableText.Replace(whole, "");
            }
            else
                table.TimetableText = table.TimetableText.Replace(table.TimetableText.Substring(moduleIndex), "");

            var moduleColor = _repository.ModuleColor.FindAll().FirstOrDefault(mc => mc.Username == User.Identity.Name && mc.ModuleCode == moduleCode);
            _repository.ModuleColor.Delete(moduleColor);

            UpdateAndSave();
        }
        return Ok("Module removed successfuly.");
    }

    ///<summary>
    ///Deletes the given session from the timetable.
    ///</summary>
    ///<param name="session">The value of the session as it is on the timetable's text.</param> 
    ///<returns>
    ///StatusCode 200 after the session is deleted.
    ///</returns>
    [HttpDelete("{session}")]
    public IActionResult DeleteSession(string session)
    {
        if (session != null)
        {
            //table.TimetableText.Trim();
            string[] timeLines = table.TimetableText.Split("\n");

            List<string> rem = new List<string>();


            int x = 0;
            for (int i = 0; i < timeLines.Length; i++)
            {
                if (timeLines[i] == session)
                {
                    x = i; break;
                }
            }

            Regex lecturepattern = new Regex(@"Lecture [0-9]?|Tutorial [0-9]?|Practical [0-9]?");
            Regex modulepattern = new Regex(@"[A-Z]{4}[\d]{4}");

            for (int i = x; i > 0; i--)
            {
                Match match = lecturepattern.Match(timeLines[i]);
                if (match.Success)
                {
                    //timeLines[i] = "";
                    for (int j = i + 1; j < timeLines.Length; j++)
                    {
                        Match matchAfter = lecturepattern.Match(timeLines[j]);
                        Match matchMod = modulepattern.Match(timeLines[j]);
                        matchMod.Equals(timeLines[j]);
                        if ((matchAfter.Success || matchMod.Success) && match.Value != timeLines[j])
                        {
                            break;
                        }
                        else
                        {
                            if (timeLines[j] != "")
                            {
                                table.TimetableText = table.TimetableText.Replace(timeLines[j], "");
                                rem.Add(timeLines[j]);
                            }
                        }
                    }
                    break;
                }
            }
            UpdateAndSave();
        }
        return Ok("Session has successfully been removed.");
    }
    #endregion DeleteMethods

}
