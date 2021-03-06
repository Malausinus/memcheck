﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MemCheck.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace MemCheck.WebUI.Areas.Identity.Pages.Account.Manage
{
    public partial class IndexModel : PageModel
    {
        #region Fields
        private readonly UserManager<MemCheckUser> _userManager;
        private readonly SignInManager<MemCheckUser> _signInManager;
        private readonly IStringLocalizer<IndexModel> localizer;
        #endregion

        public IndexModel(
            UserManager<MemCheckUser> userManager,
            SignInManager<MemCheckUser> signInManager,
            IStringLocalizer<IndexModel> localizer)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            this.localizer = localizer;
        }



        public string Username { get; set; } = null!;
        public string UserEmail { get; set; } = null!;

        [TempData] public string StatusMessage { get; set; } = null!;

        [BindProperty] public string UILanguage { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            UserEmail = user.Email;
            Username = await _userManager.GetUserNameAsync(user);
            UILanguage = user.UILanguage ?? "<Not stored>";

            return Page();
        }
        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            await _userManager.UpdateAsync(user);

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = localizer["ProfileUpdated"].Value;
            return RedirectToPage();
        }
    }
}
