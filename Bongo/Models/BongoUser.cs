﻿using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bongo.Models
{
    public class BongoUser : IdentityUser
    {
        public string SecurityQuestion { get; set; }
        public string SecurityAnswer { get; set; }
        public bool Notified { get; set; }
        public string MergeKey { get; set; }

        [NotMapped]
        public string Token { get; set; }
    }
}
