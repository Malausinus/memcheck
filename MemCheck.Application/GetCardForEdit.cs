﻿using MemCheck.Database;
using MemCheck.Domain;
using System;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Collections.Immutable;

namespace MemCheck.Application
{
    public sealed class GetCardForEdit
    {
        #region Fields
        private readonly MemCheckDbContext dbContext;
        #endregion
        public GetCardForEdit(MemCheckDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
        public async Task<ResultModel> RunAsync(Request request)
        {
            await request.CheckValidityAsync(dbContext);

            var card = await dbContext.Cards
                .Include(card => card.Images)
                .ThenInclude(img => img.Image)
                .ThenInclude(img => img.Owner)
                .Include(card => card.CardLanguage)
                .Include(card => card.TagsInCards)
                .ThenInclude(tagInCard => tagInCard.Tag)
                .Include(card => card.UsersWithView)
                .Where(card => card.Id == request.CardId)
                .AsSingleQuery()
                .SingleOrDefaultAsync();

            if (card == null)
                throw new RequestInputException("Card not found in database");

            var ratings = CardRatings.Load(dbContext, request.CurrentUserId, ImmutableHashSet.Create(request.CardId));

            var ownersOfDecksWithThisCard = dbContext.CardsInDecks
                .Where(cardInDeck => cardInDeck.CardId == request.CardId)
                .Select(cardInDeck => cardInDeck.Deck.Owner.UserName)
                .Distinct();

            return new ResultModel(
                card.FrontSide,
                card.BackSide,
                card.AdditionalInfo,
                card.CardLanguage.Id,
                card.TagsInCards.Select(tagInCard => new ResultTagModel(tagInCard.TagId, tagInCard.Tag.Name)),
                card.UsersWithView.Select(userWithView => new ResultUserModel(userWithView.UserId, userWithView.User.UserName)),
                card.InitialCreationUtcDate,
                card.VersionUtcDate,
                ownersOfDecksWithThisCard,
                card.Images.Select(img => new ResultImageModel(img)),
                ratings.User(request.CardId),
                ratings.Average(request.CardId),
                ratings.Count(request.CardId)
                );
        }
        #region Result classes
        public sealed class Request
        {
            public Request(Guid currentUserId, Guid cardId)
            {
                CurrentUserId = currentUserId;
                CardId = cardId;
            }
            public Guid CurrentUserId { get; }
            public Guid CardId { get; }
            public async Task CheckValidityAsync(MemCheckDbContext dbContext)
            {
                if (QueryValidationHelper.IsReservedGuid(CurrentUserId))
                    throw new RequestInputException($"Invalid user id '{CurrentUserId}'");
                if (QueryValidationHelper.IsReservedGuid(CardId))
                    throw new RequestInputException($"Invalid card id '{CardId}'");
                await QueryValidationHelper.CheckUserIsAllowedToViewCardAsync(dbContext, CurrentUserId, CardId);
            }
        }
        public sealed class ResultModel
        {
            public ResultModel(string frontSide, string backSide, string additionalInfo, Guid languageId, IEnumerable<ResultTagModel> tags, IEnumerable<ResultUserModel> usersWithVisibility, DateTime creationUtcDate,
                DateTime lastChangeUtcDate, IEnumerable<string> usersOwningDeckIncluding, IEnumerable<ResultImageModel> images, int userRating, double averageRating, int countOfUserRatings)
            {
                FrontSide = frontSide;
                BackSide = backSide;
                AdditionalInfo = additionalInfo;
                LanguageId = languageId;
                Tags = tags;
                UsersWithVisibility = usersWithVisibility;
                CreationUtcDate = creationUtcDate;
                LastChangeUtcDate = lastChangeUtcDate;
                UsersOwningDeckIncluding = usersOwningDeckIncluding;
                Images = images;
                UserRating = userRating;
                AverageRating = averageRating;
                CountOfUserRatings = countOfUserRatings;
            }
            public string FrontSide { get; }
            public string BackSide { get; }
            public string AdditionalInfo { get; }
            public Guid LanguageId { get; }
            public IEnumerable<ResultTagModel> Tags { get; }
            public IEnumerable<ResultUserModel> UsersWithVisibility { get; }
            public DateTime CreationUtcDate { get; }
            public DateTime LastChangeUtcDate { get; }
            public IEnumerable<string> UsersOwningDeckIncluding { get; }
            public IEnumerable<ResultImageModel> Images { get; }
            public int UserRating { get; }
            public double AverageRating { get; }
            public int CountOfUserRatings { get; }
        }
        public sealed class ResultTagModel
        {
            public ResultTagModel(Guid tagId, string tagName)
            {
                TagId = tagId;
                TagName = tagName;
            }
            public Guid TagId { get; }
            public string TagName { get; }
        }
        public sealed class ResultUserModel
        {
            public ResultUserModel(Guid userId, string userName)
            {
                UserId = userId;
                UserName = userName;
            }
            public Guid UserId { get; }
            public string UserName { get; }
        }
        public sealed class ResultImageModel
        {
            public ResultImageModel(ImageInCard img)
            {
                ImageId = img.ImageId;
                Owner = img.Image.Owner;
                Name = img.Image.Name;
                Description = img.Image.Description;
                Source = img.Image.Source;
                CardSide = img.CardSide;
                CardCount = img.Image.Cards.Count();
            }
            public Guid ImageId { get; }
            public MemCheckUser Owner { get; }
            public string Name { get; }
            public string Description { get; }
            public string Source { get; }
            public int CardSide { get; }   //1 = front side ; 2 = back side ; 3 = AdditionalInfo
            public int CardCount { get; }
        }
        #endregion
    }
}
