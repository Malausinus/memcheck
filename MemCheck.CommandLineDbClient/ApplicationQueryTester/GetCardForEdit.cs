﻿using MemCheck.Application;
using MemCheck.Application.Heaping;
using MemCheck.Database;
using MemCheck.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MemCheck.CommandLineDbClient.ApplicationQueryTester
{
    internal sealed class GetCardForEdit : IMemCheckTest
    {
        #region Fields
        private readonly ILogger<GetCards> logger;
        private readonly MemCheckDbContext dbContext;
        #endregion
        public GetCardForEdit(IServiceProvider serviceProvider)
        {
            dbContext = serviceProvider.GetRequiredService<MemCheckDbContext>();
            logger = serviceProvider.GetRequiredService<ILogger<GetCards>>();
        }
        async public Task RunAsync(MemCheckDbContext dbContext)
        {
            var userId = dbContext.Users.Where(user => user.UserName == "Voltan").Single().Id;
            var cardId = dbContext.Cards.Where(card => !card.UsersWithView.Any() && card.Images.Any()).OrderBy(card => card.VersionUtcDate).First().Id;

            const int runCount = 20;

            var chronos = new List<double>();
            for (int i = 0; i < runCount; i++)
            {
                var request = new Application.GetCardForEdit.Request(userId, cardId);
                var runner = new Application.GetCardForEdit(dbContext);
                var oneRunChrono = Stopwatch.StartNew();
                var card = await runner.RunAsync(request);
                logger.LogInformation($"Got a card with {card.Images.Count()} images, {card.CountOfUserRatings} ratings, {card.Tags.Count()} tags, {card.UsersOwningDeckIncluding.Count()} users, {card.UsersWithVisibility} users with access in {oneRunChrono.Elapsed}");
                chronos.Add(oneRunChrono.Elapsed.TotalSeconds);
            }


            logger.LogInformation($"Average time: {chronos.Average()} seconds");
        }
        public void DescribeForOpportunityToCancel()
        {
            logger.LogInformation($"Will request a card for edit mode");
        }
    }
}
