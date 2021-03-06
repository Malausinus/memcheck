﻿using System;

namespace MemCheck.Application.Heaping
{
    public abstract class HeapingAlgorithm
    {
        #region Protected method GetHasExpired
        protected abstract DateTime GetExpiryUtcDate(int currentHeap, DateTime lastLearnUtcTime);    //currentHeap is guaranteed to be > 0    //Please find a better name for this method
        protected virtual DateTime GetNow()
        {
            return DateTime.UtcNow;
        }
        #endregion
        public abstract int Id { get; }
        public bool HasExpired(int currentHeap, DateTime lastLearnUtcTime)
        {
            DateServices.CheckUTC(lastLearnUtcTime);
            return ExpiryUtcDate(currentHeap, lastLearnUtcTime) <= GetNow();
        }
        public DateTime ExpiryUtcDate(int currentHeap, DateTime lastLearnUtcTime)
        {
            DateServices.CheckUTC(lastLearnUtcTime);
            if (currentHeap < 1)
                throw new ArgumentException("card is unknown");
            var result = GetExpiryUtcDate(currentHeap, lastLearnUtcTime);
            DateServices.CheckUTC(result);
            return result;
        }
    }
}
