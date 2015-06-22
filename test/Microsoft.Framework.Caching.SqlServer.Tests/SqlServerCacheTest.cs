// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Framework.Caching.Distributed;
using Microsoft.Framework.Internal;
using Microsoft.Framework.Logging;
using Microsoft.Framework.OptionsModel;
using Xunit;

namespace Microsoft.Framework.Caching.SqlServer
{
    // This requires SQL Server database to be setup
    // public
    public class SqlServerCacheTest
    {
        private readonly string ConnectionString
            = "Server=.;Database=CacheTestDb;Trusted_Connection=True;";

        private const string SchemaName = "dbo";

        private const string TableName = "CacheTest";

        [Fact]
        public async Task ReturnsNullValue_ForNonExistingCacheItem()
        {
            // Arrange
            var options = new SqlServerCacheOptions()
            {
                ConnectionString = ConnectionString,
                SchemaName = SchemaName,
                TableName = TableName,
                ExpiredItemsDeletionInterval = TimeSpan.FromHours(2)
            };
            var sqlServerCache = new SqlServerCache(
                new TestSqlServerCacheOptions(options),
                new LoggerFactory().AddConsole());
            await sqlServerCache.ConnectAsync();

            // Act
            var value = await sqlServerCache.GetAsync("NonExisting");

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public async Task SetWithAbsoluteExpirationSetInThePast_Throws()
        {
            // Arrange
            var testClock = new TestClock();
            var slidingExpiration = TimeSpan.FromSeconds(10);
            var options = new SqlServerCacheOptions()
            {
                ConnectionString = ConnectionString,
                SchemaName = SchemaName,
                TableName = TableName,
                SystemClock = testClock,
                ExpiredItemsDeletionInterval = TimeSpan.FromHours(2)
            };
            var key = Guid.NewGuid().ToString();
            var sqlServerCache = new SqlServerCache(
                new TestSqlServerCacheOptions(options),
                new LoggerFactory().AddConsole());
            await sqlServerCache.ConnectAsync();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");

            // Act & Assert

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                return sqlServerCache.SetAsync(
                    key,
                    expectedValue,
                    new DistributedCacheEntryOptions().SetAbsoluteExpiration(testClock.UtcNow.AddHours(-1)));
            });
            Assert.Equal("The absolute expiration value must be in the future.", exception.Message);
        }

        [Fact]
        public async Task SetWithSlidingExpiration_ReturnsNullValue_ForExpiredCacheItem()
        {
            // Arrange
            var testClock = new TestClock();
            var options = new SqlServerCacheOptions()
            {
                SchemaName = SchemaName,
                TableName = TableName,
                ConnectionString = ConnectionString,
                SystemClock = testClock,
                ExpiredItemsDeletionInterval = TimeSpan.FromHours(2)
            };
            var key = Guid.NewGuid().ToString();
            var sqlServerCache = new SqlServerCache(
                new TestSqlServerCacheOptions(options),
                new LoggerFactory().AddConsole());
            await sqlServerCache.ConnectAsync();
            await sqlServerCache.SetAsync(
                key,
                Encoding.UTF8.GetBytes("Hello, World!"),
                new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(10)));

            // set the clock's UtcNow far in future
            testClock.Add(TimeSpan.FromHours(10));

            // Act
            var value = await sqlServerCache.GetAsync(key);

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public async Task SetWithAbsoluteExpirationRelativeToNow_ReturnsNullValue_ForExpiredCacheItem()
        {
            // Arrange
            var testClock = new TestClock();
            var options = new SqlServerCacheOptions()
            {
                TableName = TableName,
                ConnectionString = ConnectionString,
                SchemaName = SchemaName,
                SystemClock = testClock,
                ExpiredItemsDeletionInterval = TimeSpan.FromHours(2)
            };
            var key = Guid.NewGuid().ToString();
            var sqlServerCache = new SqlServerCache(
                            new TestSqlServerCacheOptions(options),
                            new LoggerFactory().AddConsole());
            await sqlServerCache.ConnectAsync();
            await sqlServerCache.SetAsync(
                key,
                Encoding.UTF8.GetBytes("Hello, World!"),
                new DistributedCacheEntryOptions().SetAbsoluteExpiration(relative: TimeSpan.FromSeconds(10)));

            // set the clock's UtcNow far in future
            testClock.Add(TimeSpan.FromHours(10));

            // Act
            var value = await sqlServerCache.GetAsync(key);

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public async Task SetWithAbsoluteExpiration_ReturnsNullValue_ForExpiredCacheItem()
        {
            // Arrange
            var testClock = new TestClock();
            var options = new SqlServerCacheOptions()
            {
                ConnectionString = ConnectionString,
                SchemaName = SchemaName,
                TableName = TableName,
                SystemClock = testClock,
                ExpiredItemsDeletionInterval = TimeSpan.FromHours(2)
            };
            var key = Guid.NewGuid().ToString();
            var sqlServerCache = new SqlServerCache(
                            new TestSqlServerCacheOptions(options),
                            new LoggerFactory().AddConsole());
            await sqlServerCache.ConnectAsync();
            await sqlServerCache.SetAsync(
                key,
                Encoding.UTF8.GetBytes("Hello, World!"),
                new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(absolute: testClock.UtcNow.Add(TimeSpan.FromSeconds(30))));

            // set the clock's UtcNow far in future
            testClock.Add(TimeSpan.FromHours(10));

            // Act
            var value = await sqlServerCache.GetAsync(key);

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public async Task ThrowsException_OnNoSlidingOrAbsoluteExpirationOptions()
        {
            // Arrange
            var testClock = new TestClock();
            var slidingExpiration = TimeSpan.FromSeconds(10);
            var options = new SqlServerCacheOptions()
            {
                ConnectionString = ConnectionString,
                SchemaName = SchemaName,
                TableName = TableName,
                SystemClock = testClock,
                ExpiredItemsDeletionInterval = TimeSpan.FromHours(2)
            };
            var key = Guid.NewGuid().ToString();
            var sqlServerCache = new SqlServerCache(
                            new TestSqlServerCacheOptions(options),
                            new LoggerFactory().AddConsole());
            await sqlServerCache.ConnectAsync();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                return sqlServerCache.SetAsync(
                    key,
                    expectedValue,
                    new DistributedCacheEntryOptions());
            });
            Assert.Equal("Either absolute or sliding expiration needs to be provided.", exception.Message);
        }

        [Fact]
        public async Task DoesNotThrowException_WhenOnlyAbsoluteExpirationSupplied_AbsoluteExpirationRelativeToNow()
        {
            // Arrange
            var testClock = new TestClock();
            var absoluteExpirationRelativeToUtcNow = TimeSpan.FromSeconds(10);
            var options = new SqlServerCacheOptions()
            {
                ConnectionString = ConnectionString,
                SchemaName = SchemaName,
                TableName = TableName,
                SystemClock = testClock,
                ExpiredItemsDeletionInterval = TimeSpan.FromHours(2)
            };
            var key = Guid.NewGuid().ToString();
            var sqlServerCache = new SqlServerCache(
                new TestSqlServerCacheOptions(options),
                new LoggerFactory().AddConsole());
            await sqlServerCache.ConnectAsync();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");

            // Act
            await sqlServerCache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(relative: absoluteExpirationRelativeToUtcNow));

            // Assert
            await AssertGetCacheItemFromDatabaseAsync(
                sqlServerCache,
                key,
                expectedValue,
                testClock.Add(absoluteExpirationRelativeToUtcNow).UtcNow);
        }

        [Fact]
        public async Task DoesNotThrowException_WhenOnlyAbsoluteExpirationSupplied_AbsoluteExpiration()
        {
            // Arrange
            var testClock = new TestClock();
            var absoluteExpiration = new DateTimeOffset(2025, 1, 1, 1, 0, 0, TimeSpan.Zero);
            var options = new SqlServerCacheOptions()
            {
                ConnectionString = ConnectionString,
                SchemaName = SchemaName,
                TableName = TableName,
                SystemClock = testClock,
                ExpiredItemsDeletionInterval = TimeSpan.FromHours(2)
            };
            var key = Guid.NewGuid().ToString();
            var sqlServerCache = new SqlServerCache(
                new TestSqlServerCacheOptions(options),
                new LoggerFactory().AddConsole());
            await sqlServerCache.ConnectAsync();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");

            // Act
            await sqlServerCache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(absolute: absoluteExpiration));

            // Assert
            await AssertGetCacheItemFromDatabaseAsync(
                sqlServerCache,
                key,
                expectedValue,
                absoluteExpiration);
        }

        [Fact]
        public async Task SetCacheItem_UpdatesAbsoluteExpirationTime()
        {
            // Arrange
            var testClock = new TestClock();
            var options = new SqlServerCacheOptions()
            {
                ConnectionString = ConnectionString,
                SchemaName = SchemaName,
                TableName = TableName,
                SystemClock = testClock,
                ExpiredItemsDeletionInterval = TimeSpan.FromHours(2)
            };
            var key = Guid.NewGuid().ToString();
            var sqlServerCache = new SqlServerCache(
                new TestSqlServerCacheOptions(options),
                new LoggerFactory().AddConsole());
            await sqlServerCache.ConnectAsync();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            var absoluteExpiration = testClock.UtcNow.Add(TimeSpan.FromSeconds(10));

            // Act & Assert
            // Creates a new item
            await sqlServerCache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpiration));
            await AssertGetCacheItemFromDatabaseAsync(
                sqlServerCache,
                key,
                expectedValue,
                absoluteExpiration);

            // Updates an existing item with new absolute expiration time
            absoluteExpiration = testClock.UtcNow.Add(TimeSpan.FromMinutes(30));
            await sqlServerCache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpiration));
            await AssertGetCacheItemFromDatabaseAsync(sqlServerCache, key, expectedValue, absoluteExpiration);
        }

        [Fact]
        public async Task ExtendsExpirationTime_ForSlidingExpiration()
        {
            // Arrange
            var testClock = new TestClock();
            var slidingExpiration = TimeSpan.FromSeconds(10);
            var options = new SqlServerCacheOptions()
            {
                ConnectionString = ConnectionString,
                SchemaName = SchemaName,
                TableName = TableName,
                SystemClock = testClock,
                ExpiredItemsDeletionInterval = TimeSpan.FromHours(2)
            };
            var key = Guid.NewGuid().ToString();
            var sqlServerCache = new SqlServerCache(
                new TestSqlServerCacheOptions(options),
                new LoggerFactory().AddConsole());
            await sqlServerCache.ConnectAsync();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            await sqlServerCache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpiration));
            // modify the 'UtcNow' to fall in the window which
            // causes the expiration time to be extended
            var utcNow = testClock.Add(TimeSpan.FromSeconds(15)).UtcNow;

            // Act
            var value = await sqlServerCache.GetAsync(key);

            // Assert
            Assert.NotNull(value);
            Assert.Equal(expectedValue, value);

            // verify if the expiration time in database is set as expected
            await AssertGetCacheItemFromDatabaseAsync(
                sqlServerCache,
                key,
                expectedValue,
                utcNow.Add(TimeSpan.FromTicks(SqlServerCache.ExpirationTimeMultiplier * slidingExpiration.Ticks)));
        }

        [Fact]
        public async Task GetItem_SlidingExpirationDoesNot_ExceedAbsoluteExpirationIfSet()
        {
            // Arrange
            var testClock = new TestClock();
            var slidingExpiration = TimeSpan.FromMinutes(5);
            var absoluteExpiration = testClock.UtcNow.Add(TimeSpan.FromMinutes(30));
            var options = new SqlServerCacheOptions()
            {
                ConnectionString = ConnectionString,
                SchemaName = SchemaName,
                TableName = TableName,
                SystemClock = testClock,
                ExpiredItemsDeletionInterval = TimeSpan.FromHours(2)
            };
            var key = Guid.NewGuid().ToString();
            var sqlServerCache = new SqlServerCache(
                new TestSqlServerCacheOptions(options),
                new LoggerFactory().AddConsole());
            await sqlServerCache.ConnectAsync();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            await sqlServerCache.SetAsync(
                key,
                expectedValue,
                // Set both sliding and absolute expiration
                new DistributedCacheEntryOptions()
                .SetSlidingExpiration(slidingExpiration)
                .SetAbsoluteExpiration(absoluteExpiration));

            // Act && Assert
            var utcNow = testClock.UtcNow;
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
            Assert.NotNull(cacheItemInfo);
            Assert.Equal(utcNow.AddMinutes(10).UtcDateTime, cacheItemInfo.ExpiresAtTime);

            // trigger extension of expiration - succeeds
            utcNow = testClock.Add(TimeSpan.FromMinutes(8)).UtcNow;
            await AssertGetCacheItemFromDatabaseAsync(sqlServerCache, key, expectedValue, utcNow.AddMinutes(10));

            // trigger extension of expiration - succeeds
            utcNow = testClock.Add(TimeSpan.FromMinutes(8)).UtcNow;
            await AssertGetCacheItemFromDatabaseAsync(sqlServerCache, key, expectedValue, utcNow.AddMinutes(10));

            // trigger extension of expiration - fails
            utcNow = testClock.Add(TimeSpan.FromMinutes(8)).UtcNow;
            // The expiration extension must not exceed the absolute expiration
            await AssertGetCacheItemFromDatabaseAsync(sqlServerCache, key, expectedValue, absoluteExpiration);
        }

        [Fact]
        public async Task DoestNotExtendExpirationTime_ForAbsoluteExpiration()
        {
            // Arrange
            var testClock = new TestClock();
            var absoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            var expectedExpiresAtTimeDateTimeOffset = testClock.UtcNow.Add(absoluteExpirationRelativeToNow);
            var options = new SqlServerCacheOptions()
            {
                ConnectionString = ConnectionString,
                SchemaName = SchemaName,
                TableName = TableName,
                SystemClock = testClock,
                ExpiredItemsDeletionInterval = TimeSpan.FromHours(2)
            };
            var key = Guid.NewGuid().ToString();
            var sqlServerCache = new SqlServerCache(
                new TestSqlServerCacheOptions(options),
                new LoggerFactory().AddConsole());
            await sqlServerCache.ConnectAsync();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            await sqlServerCache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpirationRelativeToNow));
            testClock.Add(TimeSpan.FromSeconds(25));

            // Act
            var value = await sqlServerCache.GetAsync(key);

            // Assert
            Assert.NotNull(value);
            Assert.Equal(expectedValue, value);

            // verify if the expiration time in database is set as expected
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
            Assert.NotNull(cacheItemInfo);
            Assert.Equal(expectedExpiresAtTimeDateTimeOffset.UtcDateTime, cacheItemInfo.ExpiresAtTime);
        }

        [Fact]
        public async Task DeletesCacheItem_OnExplicitlyCalled()
        {
            // Arrange
            var testClock = new TestClock();
            var options = new SqlServerCacheOptions()
            {
                ConnectionString = ConnectionString,
                SchemaName = SchemaName,
                TableName = TableName,
                SystemClock = testClock,
                ExpiredItemsDeletionInterval = TimeSpan.FromHours(2)
            };
            var key = Guid.NewGuid().ToString();
            var sqlServerCache = new SqlServerCache(
                new TestSqlServerCacheOptions(options),
                new LoggerFactory().AddConsole());
            await sqlServerCache.ConnectAsync();
            await sqlServerCache.SetAsync(
                key,
                Encoding.UTF8.GetBytes("Hello, World!"),
                new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(10)));

            // Act
            await sqlServerCache.RemoveAsync(key);

            // Assert
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
            Assert.Null(cacheItemInfo);
        }

        private async Task AssertGetCacheItemFromDatabaseAsync(
            SqlServerCache cache,
            string key,
            byte[] expectedValue,
            DateTimeOffset expectedExpirationTime)
        {
            var value = await cache.GetAsync(key);
            Assert.NotNull(value);
            Assert.Equal(expectedValue, value);
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
            Assert.NotNull(cacheItemInfo);
            Assert.Equal(expectedExpirationTime.UtcDateTime, cacheItemInfo.ExpiresAtTime);
        }

        private async Task<CacheItemInfo> GetCacheItemFromDatabaseAsync(string key)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var command = new SqlCommand(
                    $"SELECT Id, Value, ExpiresAtTime, SlidingExpirationInTicks, AbsoluteExpiration " +
                    $"FROM {TableName} WHERE Id = @Id",
                    connection);
                command.Parameters.AddWithValue("Id", key);

                await connection.OpenAsync();

                var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var cacheItemInfo = new CacheItemInfo();
                    cacheItemInfo.Id = key;
                    cacheItemInfo.Value = await reader.GetFieldValueAsync<byte[]>(1);
                    cacheItemInfo.ExpiresAtTime = await reader.GetFieldValueAsync<DateTime>(2);

                    if (!await reader.IsDBNullAsync(3))
                    {
                        cacheItemInfo.SlidingExpirationInTicks = await reader.GetFieldValueAsync<long>(3);
                    }

                    if (!await reader.IsDBNullAsync(4))
                    {
                        cacheItemInfo.AbsoluteExpiration = await reader.GetFieldValueAsync<DateTime>(4);
                    }

                    return cacheItemInfo;
                }
                else
                {
                    return null;
                }
            }
        }

        private class TestSqlServerCacheOptions : IOptions<SqlServerCacheOptions>
        {
            private readonly SqlServerCacheOptions _innerOptions;

            public TestSqlServerCacheOptions(SqlServerCacheOptions innerOptions)
            {
                _innerOptions = innerOptions;
            }

            public SqlServerCacheOptions Options
            {
                get
                {
                    return _innerOptions;
                }
            }

            public SqlServerCacheOptions GetNamedOptions(string name)
            {
                return _innerOptions;
            }
        }
    }
}
