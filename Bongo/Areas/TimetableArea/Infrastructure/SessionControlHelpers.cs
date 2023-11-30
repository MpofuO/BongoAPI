﻿using Bongo.Areas.TimetableArea.Models;
using Bongo.Data;
using System.Text.RegularExpressions;

namespace Bongo.Areas.TimetableArea.Infrastructure
{
    public static class SessionControlHelpers
    {
        public static bool ContainsClashes(string[] sessions)
        {
            // ExtractSessions a List to store the session time ranges
            List<(DateTime StartTime, DateTime EndTime, string Day)> sessionTimes = new List<(DateTime, DateTime, string)>();

            Regex timePattern = new Regex(@"(\d{2}:\d{2}) (\d{2}:\d{2})");
            Regex dayPattern = new Regex(@"Monday|Tuesday|Wednesday|Thursday|Friday");

            foreach (string session in sessions)
            {
                Match timeMatch = timePattern.Match(session);
                Match dayMatch = dayPattern.Match(session);

                if (timeMatch.Success && dayMatch.Success)
                {
                    DateTime startTime = DateTime.Parse(timeMatch.Groups[1].Value);
                    DateTime endTime = DateTime.Parse(timeMatch.Groups[2].Value);
                    string Day = dayMatch.Value;
                    // Check for overlapping sessions
                    foreach (var sessionTime in sessionTimes)
                    {
                        if (Day == sessionTime.Day && (startTime >= sessionTime.StartTime && startTime < sessionTime.EndTime ||
                            endTime > sessionTime.StartTime && endTime <= sessionTime.EndTime ||
                            startTime <= sessionTime.StartTime && endTime >= sessionTime.EndTime))
                        {
                            return true; // Clash detected
                        }
                    }

                    sessionTimes.Add((startTime, endTime, Day));
                }
            }
            return false;
        }
        public static int GetInterval(string sessionPdfValue)
        {
            Regex timePattern = new Regex(@"(\d{2}:\d{2}) (\d{2}:\d{2})");
            Match timeMatch = timePattern.Match(sessionPdfValue);
            int startTime = int.Parse(timeMatch.Groups[1].Value.Substring(0, 2));
            int endTime = int.Parse(timeMatch.Groups[2].Value.Substring(0, 2));
            return endTime - startTime;
        }

        public static void AddNewUserModuleColor(ref IRepositoryWrapper _repo, string Username, string text)
        {
            List<string> moduleCodes;
            List<Lecture> lects;//for Conrtol
            new TimetableExtractor().ExtractSessions(text, out lects, out moduleCodes);
            foreach (string moduleCode in moduleCodes)
            {
                _repo.ModuleColor.Update(new ModuleColor
                {
                    ColorId = _repo.Color.GetByName("no-color").ColorId,
                    Username = Username,
                    ModuleCode = moduleCode
                });
            }
        }
        public static bool HasGroups(List<Session> sessions)
        {
            Regex groupPattern = new Regex(@"Group [A-Z]{1,2}[\d]?");
            List<string> groups = new List<string>();
            foreach (var session in sessions)
            {
                Match groupMatch = groupPattern.Match(session.sessionInPDFValue);
                if (groupMatch.Success)
                {
                    groups.Add(groupMatch.Value);
                }
            }
            return groups.Distinct().Count() < groups.Count;
        }
        public static string[] GetClashing(string[] sessions)
        {
            List<string> checkedSessions = new List<string>();
            List<string> strings = new List<string>();

            for (int i = 0; i < sessions.Length; i++)
            {
                if (checkedSessions.Contains(sessions[i]))
                    continue;

                List<string> list = new List<string>() { sessions[i] };
                checkedSessions.Add(sessions[i]);

                for (int j = i + 1; j < sessions.Length; j++)
                {
                    if (checkedSessions.Contains(sessions[j]))
                        continue;

                    if (ContainsClashes(new string[] { sessions[i], sessions[j] }))
                    {
                        list.Add(sessions[j]);
                        checkedSessions.Add(sessions[j]);
                    }
                }
                if (list.Count > 1)
                    strings.AddRange(list);
            }

            return strings.ToArray();
        }
        public static List<Session> ConvertToList(Session[,] data)
        {
            List<Session> sessions = new();

            foreach (Session s in data)
                if (s is not null)
                    sessions.Add(s);

            return sessions;
        }
    }

}
