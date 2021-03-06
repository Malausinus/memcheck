﻿using System.Threading.Tasks;
using MemCheck.Database;
using MemCheck.Domain;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace MemCheck.Application
{
    public sealed class CreateTag
    {
        #region Fields
        private const int minLength = 3;
        private const int maxLength = 50;
        private readonly MemCheckDbContext dbContext;
        private static readonly char[] forbiddenChars = new[] { '<', '>' };
        #endregion
        public CreateTag(MemCheckDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
        public async Task<bool> RunAsync(string name)
        {
            name = name.Trim();
            CheckNameValidity(dbContext, name);

            dbContext.Tags.Add(new Tag() { Name = name });
            await dbContext.SaveChangesAsync();

            return true;
        }
        public static void CheckNameValidity(MemCheckDbContext dbContext, string name)
        {
            if (name.Length < minLength || name.Length > maxLength)
                throw new InvalidOperationException($"Invalid tag name '{name}' (length must be between {minLength} and {maxLength})");

            foreach (var forbiddenChar in forbiddenChars)
                if (name.Contains(forbiddenChar))
                    throw new InvalidOperationException($"Invalid tag name '{name}' (length must be between {minLength} and {maxLength})");

            var exists = dbContext.Tags.Where(tag => EF.Functions.Like(tag.Name, $"{name}")).Any();
            if (exists)
                throw new InvalidOperationException($"A tag with name '{name}' already exists (tags are case insensitive)");
        }
    }
}
