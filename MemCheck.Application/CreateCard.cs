﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MemCheck.Database;
using MemCheck.Domain;
using Microsoft.Extensions.Localization;

namespace MemCheck.Application
{
    public sealed class CreateCard
    {
        #region Fields
        private readonly MemCheckDbContext dbContext;
        #endregion
        #region Private methods
        private void AddImage(Guid cardId, Guid imageId, int cardSide, List<ImageInCard> cardImageList)
        {
            var imageFromDb = dbContext.Images.Where(img => img.Id == imageId).Single();    //To be reviewed: it sounds stupid that we have to load the whole image info, with the blob, while we only need an id???
            var img = new ImageInCard() { ImageId = imageId, Image = imageFromDb, CardId = cardId, CardSide = cardSide };
            dbContext.ImagesInCards.Add(img);
            cardImageList.Add(img);
        }
        #endregion
        public CreateCard(MemCheckDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
        public async Task<Guid> RunAsync(Request request, IStringLocalizer localizer)
        {
            CardInputValidator.Run(request, localizer);

            var language = dbContext.CardLanguages.Where(language => language.Id == request.LanguageId).Single();

            var card = new Card()
            {
                FrontSide = request.FrontSide,
                BackSide = request.BackSide,
                AdditionalInfo = request.AdditionalInfo,
                CardLanguage = language,
                VersionCreator = request.VersionCreator,
                InitialCreationUtcDate = DateTime.Now.ToUniversalTime(),
                VersionUtcDate = DateTime.Now.ToUniversalTime(),
                VersionDescription = request.VersionDescription
            };
            dbContext.Cards.Add(card);

            var usersWithView = new List<UserWithViewOnCard>();
            foreach (var userFromRequestId in request.UsersWithVisibility)
            {
                var userFromDb = dbContext.Users.Where(u => u.Id == userFromRequestId).Single();
                var userWithView = new UserWithViewOnCard() { UserId = userFromDb.Id, User = userFromDb, CardId = card.Id, Card = card };
                dbContext.UsersWithViewOnCards.Add(userWithView);
                usersWithView.Add(userWithView);
            }
            card.UsersWithView = usersWithView;

            var tagsInCards = new List<TagInCard>();
            foreach (var tagToAdd in request.Tags)
            {
                var tagFromDb = dbContext.Tags.Where(t => t.Id == tagToAdd).Single();
                var tagInCard = new TagInCard() { TagId = tagFromDb.Id, Tag = tagFromDb, CardId = card.Id };
                dbContext.TagsInCards.Add(tagInCard);
                tagsInCards.Add(tagInCard);
            }
            card.TagsInCards = tagsInCards;

            var cardImageList = new List<ImageInCard>();
            foreach (var image in request.FrontSideImageList)
                AddImage(card.Id, image, 1, cardImageList);
            foreach (var image in request.BackSideImageList)
                AddImage(card.Id, image, 2, cardImageList);
            foreach (var image in request.AdditionalInfoImageList)
                AddImage(card.Id, image, 3, cardImageList);
            card.Images = cardImageList;

            await dbContext.SaveChangesAsync();

            return card.Id;
        }
        public sealed class Request : ICardInput
        {
            public Request(MemCheckUser versionCreator, string frontSide, IEnumerable<Guid> frontSideImageList, string backSide, IEnumerable<Guid> backSideImageList, string additionalInfo, IEnumerable<Guid> additionalInfoImageList, Guid languageId, IEnumerable<Guid> tags, IEnumerable<Guid> usersWithVisibility, string versionDescription)
            {
                VersionCreator = versionCreator;
                FrontSide = frontSide.Trim();
                BackSide = backSide.Trim();
                AdditionalInfo = additionalInfo.Trim();
                FrontSideImageList = frontSideImageList;
                BackSideImageList = backSideImageList;
                AdditionalInfoImageList = additionalInfoImageList;
                LanguageId = languageId;
                Tags = tags;
                UsersWithVisibility = usersWithVisibility;
                VersionDescription = versionDescription;
            }
            public MemCheckUser VersionCreator { get; }
            public string FrontSide { get; }
            public IEnumerable<Guid> FrontSideImageList { get; }
            public string BackSide { get; }
            public IEnumerable<Guid> BackSideImageList { get; }
            public string AdditionalInfo { get; }
            public IEnumerable<Guid> AdditionalInfoImageList { get; }
            public Guid LanguageId { get; }
            public IEnumerable<Guid> Tags { get; }
            public IEnumerable<Guid> UsersWithVisibility { get; }
            public string VersionDescription { get; }
        }
    }
}
