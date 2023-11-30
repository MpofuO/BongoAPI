﻿namespace Bongo.Areas.TimetableArea.Models.ViewModels
{
    public class MergerIndexViewModel
    {
        public Dictionary<string,string> Users { get; set; }
        public List<string> MergedUsers { get; set; }
        public List<Session> Sessions { get; set; }
        public int latestPeriod
        {
            get
            {
                int latest = 0;
                foreach (Session session in Sessions)
                {
                    if (session != null)
                    {
                        latest = session.Period[1] > latest ? session.Period[1] : latest;
                    }
                }
                return latest;
            }
        }
    }
}
