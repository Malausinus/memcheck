﻿using System;
using Microsoft.AspNetCore.Identity;

namespace MemCheck.Domain
{
    //An account deletion will replace all the fields of the user with some information (eg UserName becomes <deleted user>, same for email, etc.)
    //But the user id always remains valid
    public sealed class MemCheckUser : IdentityUser<Guid>
    {
        public string? UILanguage { get; set; } = null;
        public CardLanguage? PreferredCardCreationLanguage { get; set; } = null;
    }
}
