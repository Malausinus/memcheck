﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;
using MemCheck.Database;
using System.Linq;
using System;
using MemCheck.Application.Notifying;
using MemCheck.Domain;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MemCheck.Application.Tests
{
    [TestClass()]
    public class UserCardVersionsNotifierTests
    {
        #region Private methods
        private DbContextOptions<MemCheckDbContext> OptionsForNewDB()
        {
            return new DbContextOptionsBuilder<MemCheckDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        }
        private async Task<MemCheckUser> CreateUserAsync(DbContextOptions<MemCheckDbContext> db)
        {
            using var dbContext = new MemCheckDbContext(db);
            var result = new MemCheckUser();
            dbContext.Users.Add(result);
            await dbContext.SaveChangesAsync();
            return result;
        }
        private async Task<Card> CreateCardAsync(DbContextOptions<MemCheckDbContext> db, Guid versionCreatorId, DateTime versionDate, IEnumerable<Guid>? userWithViewIds = null)
        {
            //userWithViewIds null means public card

            using var dbContext = new MemCheckDbContext(db);
            var creator = await dbContext.Users.Where(u => u.Id == versionCreatorId).SingleAsync();

            var result = new Card();
            result.VersionCreator = creator;
            result.FrontSide = Guid.NewGuid().ToString();
            result.VersionDescription = Guid.NewGuid().ToString();
            result.VersionType = CardVersionType.Creation;
            result.InitialCreationUtcDate = versionDate;
            result.VersionUtcDate = versionDate;
            dbContext.Cards.Add(result);

            var usersWithView = new List<UserWithViewOnCard>();
            if (userWithViewIds != null)
            {
                Assert.IsTrue(userWithViewIds.Any(id => id == versionCreatorId), "Version creator must be allowed to view");
                foreach (var userWithViewId in userWithViewIds)
                {
                    var userWithView = new UserWithViewOnCard();
                    userWithView.CardId = result.Id;
                    userWithView.UserId = userWithViewId;
                    dbContext.UsersWithViewOnCards.Add(userWithView);
                    usersWithView.Add(userWithView);
                }
            }
            result.UsersWithView = usersWithView;

            await dbContext.SaveChangesAsync();
            return result;
        }
        private async Task<CardPreviousVersion> CreateCardPreviousVersionAsync(DbContextOptions<MemCheckDbContext> db, Guid versionCreatorId, Guid cardId, DateTime versionDate)
        {
            using var dbContext = new MemCheckDbContext(db);
            var creator = await dbContext.Users.Where(u => u.Id == versionCreatorId).SingleAsync();

            var result = new CardPreviousVersion();
            result.Card = cardId;
            result.VersionCreator = creator;
            result.VersionUtcDate = versionDate;
            result.VersionType = CardPreviousVersionType.Creation;
            dbContext.CardPreviousVersions.Add(result);

            var card = await dbContext.Cards.Where(c => c.Id == cardId).SingleAsync();
            card.PreviousVersion = result;
            card.VersionType = CardVersionType.Changes;

            await dbContext.SaveChangesAsync();
            return result;
        }
        private async Task<CardPreviousVersion> CreatePreviousVersionPreviousVersionAsync(DbContextOptions<MemCheckDbContext> db, Guid versionCreatorId, CardPreviousVersion previousVersion, DateTime versionDate)
        {
            using var dbContext = new MemCheckDbContext(db);
            var creator = await dbContext.Users.Where(u => u.Id == versionCreatorId).SingleAsync();

            var result = new CardPreviousVersion();
            result.Card = previousVersion.Card;
            result.VersionCreator = creator;
            result.VersionUtcDate = versionDate;
            result.VersionType = CardPreviousVersionType.Creation;
            dbContext.CardPreviousVersions.Add(result);

            previousVersion.PreviousVersion = result;
            previousVersion.VersionType = CardPreviousVersionType.Changes;

            await dbContext.SaveChangesAsync();
            return result;
        }
        private async Task CreateCardNotificationAsync(DbContextOptions<MemCheckDbContext> db, Guid subscriberId, Guid cardId, DateTime lastNotificationDate)
        {
            using var dbContext = new MemCheckDbContext(db);
            var notif = new CardNotificationSubscription();
            notif.CardId = cardId;
            notif.UserId = subscriberId;
            notif.LastNotificationUtcDate = lastNotificationDate;
            dbContext.CardNotifications.Add(notif);
            await dbContext.SaveChangesAsync();
        }
        #endregion
        [TestMethod()]
        public async Task EmptyDB()
        {
            var options = OptionsForNewDB();
            var user1 = await CreateUserAsync(options);

            using (var dbContext = new MemCheckDbContext(options))
            {
                var notifier = new UserCardVersionsNotifier(dbContext);
                var versions = notifier.Run(user1);
                Assert.AreEqual(0, versions.Length);
            }
        }
        [TestMethod()]
        public async Task CardWithoutPreviousVersion_NotToBeNotified()
        {
            var options = OptionsForNewDB();

            var user = await CreateUserAsync(options);
            var card = await CreateCardAsync(options, user.Id, new DateTime(2020, 11, 2));
            await CreateCardNotificationAsync(options, user.Id, card.Id, new DateTime(2020, 11, 3));

            using (var dbContext = new MemCheckDbContext(options))
            {
                var notifier = new UserCardVersionsNotifier(dbContext);
                var versions = notifier.Run(user);
                Assert.AreEqual(0, versions.Length);
            }
        }
        [TestMethod()]
        public async Task CardWithoutPreviousVersion_ToBeNotified()
        {
            var options = OptionsForNewDB();

            var user = await CreateUserAsync(options);
            var card = await CreateCardAsync(options, user.Id, new DateTime(2020, 11, 2));
            await CreateCardNotificationAsync(options, user.Id, card.Id, new DateTime(2020, 11, 1));

            using (var dbContext = new MemCheckDbContext(options))
            {
                var notifier = new UserCardVersionsNotifier(dbContext);
                var versions = notifier.Run(user);
                Assert.AreEqual(1, versions.Length);
                Assert.AreEqual(card.Id, versions[0].CardId);
                Assert.IsTrue(versions[0].CardIsViewable);
                Assert.AreEqual(card.FrontSide, versions[0].FrontSide);
                Assert.AreEqual(card.VersionDescription, versions[0].VersionDescription);
            }
        }
        [TestMethod()]
        public async Task CardWitOnePreviousVersion_NotToBeNotifiedBecauseOfDate()
        {
            var options = OptionsForNewDB();

            var user1 = await CreateUserAsync(options);
            var card = await CreateCardAsync(options, user1.Id, new DateTime(2020, 11, 2));
            var user2 = await CreateUserAsync(options);
            await CreateCardPreviousVersionAsync(options, user2.Id, card.Id, new DateTime(2020, 11, 1));
            await CreateCardNotificationAsync(options, user1.Id, card.Id, new DateTime(2020, 11, 3));

            using (var dbContext = new MemCheckDbContext(options))
            {
                var notifier = new UserCardVersionsNotifier(dbContext);
                var versions = notifier.Run(user1);
                Assert.AreEqual(0, versions.Length);
            }
        }
        [TestMethod()]
        public async Task CardWitOnePreviousVersion_ToBeNotifiedWithoutAccessibility()
        {
            var options = OptionsForNewDB();

            var user1 = await CreateUserAsync(options);

            var card = await CreateCardAsync(options, user1.Id, new DateTime(2020, 11, 2), new[] { user1.Id });
            await CreateCardPreviousVersionAsync(options, user1.Id, card.Id, new DateTime(2020, 11, 1));

            var user2 = await CreateUserAsync(options);
            await CreateCardNotificationAsync(options, user2.Id, card.Id, new DateTime(2020, 11, 1));

            using (var dbContext = new MemCheckDbContext(options))
            {
                var notifier = new UserCardVersionsNotifier(dbContext);

                var user1versions = notifier.Run(user1);
                Assert.AreEqual(0, user1versions.Length);

                var user2versions = notifier.Run(user2);
                Assert.AreEqual(1, user2versions.Length);
                Assert.AreEqual(card.Id, user2versions[0].CardId);
                Assert.IsFalse(user2versions[0].CardIsViewable);
                Assert.IsNull(user2versions[0].VersionDescription);
                Assert.IsNull(user2versions[0].FrontSide);
            }
        }
        [TestMethod()]
        public async Task CardWitOnePreviousVersion_ToBeNotifiedWithAccessibility()
        {
            var options = OptionsForNewDB();

            var user1 = await CreateUserAsync(options);
            var user2 = await CreateUserAsync(options);

            var card = await CreateCardAsync(options, user1.Id, new DateTime(2020, 11, 2), new Guid[] { user1.Id, user2.Id });
            await CreateCardPreviousVersionAsync(options, user1.Id, card.Id, new DateTime(2020, 11, 1));

            await CreateCardNotificationAsync(options, user2.Id, card.Id, new DateTime(2020, 11, 1));

            using (var dbContext = new MemCheckDbContext(options))
            {
                var notifier = new UserCardVersionsNotifier(dbContext);
                var versions = notifier.Run(user2);
                Assert.AreEqual(1, versions.Length);
                Assert.AreEqual(card.Id, versions[0].CardId);
                Assert.IsTrue(versions[0].CardIsViewable);
            }
        }
        [TestMethod()]
        public async Task CardWitOnePreviousVersion_ToBeNotified_LastNotifAfterInitialCreation()
        {
            var options = OptionsForNewDB();

            var user1 = await CreateUserAsync(options);
            var card = await CreateCardAsync(options, user1.Id, new DateTime(2020, 11, 3));
            var user2 = await CreateUserAsync(options);
            await CreateCardPreviousVersionAsync(options, user2.Id, card.Id, new DateTime(2020, 11, 1));
            await CreateCardNotificationAsync(options, user1.Id, card.Id, new DateTime(2020, 11, 2));

            using (var dbContext = new MemCheckDbContext(options))
            {
                var notifier = new UserCardVersionsNotifier(dbContext);
                var versions = notifier.Run(user1);
                Assert.AreEqual(1, versions.Length);
                Assert.AreEqual(card.Id, versions[0].CardId);
                Assert.IsTrue(versions[0].CardIsViewable);
            }
        }
        [TestMethod()]
        public async Task CardWitOnePreviousVersion_ToBeNotified_LastNotifBeforeInitialCreation()
        {
            var options = OptionsForNewDB();

            var user1 = await CreateUserAsync(options);
            var card = await CreateCardAsync(options, user1.Id, new DateTime(2020, 11, 3));
            var user2 = await CreateUserAsync(options);
            await CreateCardPreviousVersionAsync(options, user2.Id, card.Id, new DateTime(2020, 11, 2));
            await CreateCardNotificationAsync(options, user1.Id, card.Id, new DateTime(2020, 11, 1));

            using (var dbContext = new MemCheckDbContext(options))
            {
                var notifier = new UserCardVersionsNotifier(dbContext);
                var versions = notifier.Run(user1);
                Assert.AreEqual(1, versions.Length);
                Assert.AreEqual(card.Id, versions[0].CardId);
                Assert.IsTrue(versions[0].CardIsViewable);
            }
        }
        [TestMethod()]
        public async Task CardWitPreviousVersions_NotToBeNotified()
        {
            var options = OptionsForNewDB();

            var user1 = await CreateUserAsync(options);
            var card = await CreateCardAsync(options, user1.Id, new DateTime(2020, 11, 3));
            var user2 = await CreateUserAsync(options);
            var previousVersion1 = await CreateCardPreviousVersionAsync(options, user2.Id, card.Id, new DateTime(2020, 11, 2));
            await CreatePreviousVersionPreviousVersionAsync(options, user2.Id, previousVersion1, new DateTime(2020, 11, 1));
            await CreateCardNotificationAsync(options, user1.Id, card.Id, new DateTime(2020, 11, 3));

            using (var dbContext = new MemCheckDbContext(options))
            {
                var notifier = new UserCardVersionsNotifier(dbContext);
                var versions = notifier.Run(user1);
                Assert.AreEqual(0, versions.Length);
            }
        }
        [TestMethod()]
        public async Task CardWitPreviousVersions_ToBeNotified_LastNotifAfterPreviousVersionCreation()
        {
            var options = OptionsForNewDB();

            var user1 = await CreateUserAsync(options);
            var card = await CreateCardAsync(options, user1.Id, new DateTime(2020, 11, 5));
            var user2 = await CreateUserAsync(options);
            var previousVersion1 = await CreateCardPreviousVersionAsync(options, user2.Id, card.Id, new DateTime(2020, 11, 3));
            await CreatePreviousVersionPreviousVersionAsync(options, user2.Id, previousVersion1, new DateTime(2020, 11, 1));
            await CreateCardNotificationAsync(options, user1.Id, card.Id, new DateTime(2020, 11, 4));

            using (var dbContext = new MemCheckDbContext(options))
            {
                var notifier = new UserCardVersionsNotifier(dbContext);
                var versions = notifier.Run(user1);
                Assert.AreEqual(1, versions.Length);
                Assert.AreEqual(card.Id, versions[0].CardId);
                Assert.IsTrue(versions[0].CardIsViewable);
            }
        }
        [TestMethod()]
        public async Task CardWitPreviousVersions_ToBeNotified_LastNotifBeforePreviousVersionCreation()
        {
            var options = OptionsForNewDB();

            var user1 = await CreateUserAsync(options);
            var card = await CreateCardAsync(options, user1.Id, new DateTime(2020, 11, 5));
            var user2 = await CreateUserAsync(options);
            var previousVersion1 = await CreateCardPreviousVersionAsync(options, user2.Id, card.Id, new DateTime(2020, 11, 3));
            await CreatePreviousVersionPreviousVersionAsync(options, user2.Id, previousVersion1, new DateTime(2020, 11, 1));
            await CreateCardNotificationAsync(options, user1.Id, card.Id, new DateTime(2020, 11, 2));

            using (var dbContext = new MemCheckDbContext(options))
            {
                var notifier = new UserCardVersionsNotifier(dbContext);
                var versions = notifier.Run(user1);
                Assert.AreEqual(1, versions.Length);
                Assert.AreEqual(card.Id, versions[0].CardId);
                Assert.IsTrue(versions[0].CardIsViewable);
            }
        }
        [TestMethod()]
        public async Task MultipleVersions()
        {
            var options = OptionsForNewDB();

            var user1 = await CreateUserAsync(options);
            var user2 = await CreateUserAsync(options);

            var card1 = await CreateCardAsync(options, user1.Id, new DateTime(2020, 11, 10));
            var card1PV1 = await CreateCardPreviousVersionAsync(options, user2.Id, card1.Id, new DateTime(2020, 11, 5));
            await CreatePreviousVersionPreviousVersionAsync(options, user2.Id, card1PV1, new DateTime(2020, 11, 1));
            await CreateCardNotificationAsync(options, user1.Id, card1.Id, new DateTime(2020, 11, 2));

            var card2 = await CreateCardAsync(options, user1.Id, new DateTime(2020, 11, 10));
            var card2PV1 = await CreateCardPreviousVersionAsync(options, user2.Id, card2.Id, new DateTime(2020, 11, 5));
            await CreatePreviousVersionPreviousVersionAsync(options, user2.Id, card2PV1, new DateTime(2020, 11, 2));
            await CreateCardNotificationAsync(options, user1.Id, card2.Id, new DateTime(2020, 11, 1));

            var card3 = await CreateCardAsync(options, user1.Id, new DateTime(2020, 11, 10));
            var card3PV1 = await CreateCardPreviousVersionAsync(options, user2.Id, card3.Id, new DateTime(2020, 11, 5));
            await CreatePreviousVersionPreviousVersionAsync(options, user2.Id, card3PV1, new DateTime(2020, 11, 2));
            await CreateCardNotificationAsync(options, user1.Id, card3.Id, new DateTime(2020, 11, 11));

            var card4 = await CreateCardAsync(options, user2.Id, new DateTime(2020, 11, 10), new Guid[] { user2.Id });  //Not to be notified because no access for user1
            await CreateCardPreviousVersionAsync(options, user2.Id, card4.Id, new DateTime(2020, 11, 5));
            await CreateCardNotificationAsync(options, user1.Id, card4.Id, new DateTime(2020, 11, 2));

            var card5 = await CreateCardAsync(options, user2.Id, new DateTime(2020, 11, 10), new Guid[] { user1.Id, user2.Id });
            await CreateCardPreviousVersionAsync(options, user2.Id, card5.Id, new DateTime(2020, 11, 5));
            await CreateCardNotificationAsync(options, user1.Id, card5.Id, new DateTime(2020, 11, 2));

            using (var dbContext = new MemCheckDbContext(options))
            {
                var notifier = new UserCardVersionsNotifier(dbContext);
                var user1Versions = notifier.Run(user1);
                Assert.AreEqual(4, user1Versions.Length);

                var notifForCard1 = user1Versions.Where(v => v.CardId == card1.Id).Single();
                Assert.IsTrue(notifForCard1.CardIsViewable);

                var notifForCard2 = user1Versions.Where(v => v.CardId == card2.Id).Single();
                Assert.IsTrue(notifForCard2.CardIsViewable);

                Assert.IsFalse(user1Versions.Any(v => v.CardId == card3.Id));

                var notifForCard4 = user1Versions.Where(v => v.CardId == card4.Id).Single();
                Assert.IsFalse(notifForCard4.CardIsViewable);

                var notifForCard5 = user1Versions.Where(v => v.CardId == card5.Id).Single();
                Assert.IsTrue(notifForCard5.CardIsViewable);

            }
        }
    }
}