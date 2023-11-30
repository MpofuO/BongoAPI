﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bongo.Areas.TimetableArea.Models
{
    public class Session
    {
        [Key]
        public string Username { get; set; }
        public string StudentNumber { get; set; }
        [NotMapped]
        public int[] Period { get; set; } = new int[2];
        public string sessionType { get; set; }
        public string ModuleCode { get; set; }
        public string Venue { get; set; }
        public string sessionInPDFValue { get; set; }
        public string Description
        {
            get { return sessionType + "\n" + ModuleCode + "\n" + Venue; }
        }

        //Used only for when we are merging timetables
        public int userCount { get; set; } = 1;
    }
}