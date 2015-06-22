// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Framework.Internal
{
    public class TestClock : ISystemClock
    {
        public TestClock()
        {
            // Examples:
            // DateTime.Now:            6/11/2015 3:34:23 PM
            // DateTime.UtcNow:         6/11/2015 10:34:23 PM
            // DateTimeOffset.Now:      6/11/2015 3:34:23 PM - 07:00
            // DateTimeOffset.UtcNow:   6/11/2015 10:34:23 PM + 00:00 -> no offset for UTC
            UtcNow = new DateTimeOffset(2013, 1, 1, 1, 0, 0, offset: TimeSpan.Zero);
        }

        public DateTimeOffset UtcNow { get; private set; }

        public ISystemClock Add(TimeSpan timeSpan)
        {
            UtcNow = UtcNow.Add(timeSpan);

            return this;
        }

        public DateTime DateTimeInUTC
        {
            get
            {
                return UtcNow.UtcDateTime;
            }
        }
    }
}
