// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.Caching.Distributed;
using Microsoft.Framework.Internal;
using Microsoft.Framework.Logging;
using Microsoft.Framework.OptionsModel;

namespace Microsoft.Framework.Caching.SqlServer
{
    /// <summary>
    /// Distributed cache implementation using Microsoft SQL Server database.
    /// </summary>
    public class SqlServerCache : IDistributedCache
    {
        /// <summary>
        /// To prevent frequent updates to a cache item's expiration time in database, especially when doing a
        /// Get operation, a cache item's expiration time is extended by doubling the sliding expiration.
        /// The 'SlidingExpiration' here should be interpreted as the minimum time to live for a cache item before
        /// it expires.
        /// Example:
        /// For a sliding expiration of 30 mins, a new cache item would have its 'ExpiresAtTime' 60 mins(30 * 2)
        /// from now. All 'Get' operations before the first 30 mins do not cause any database updates to the cache
        /// item's expiration time and any 'Get' operations between the 30th and 60th minute would cause a database
        /// update where the expiration time is again extended by 60 mins.
        /// </summary>
        internal const int ExpirationTimeMultiplier = 2;

        // The value of the following index positions is dependent on how the SQL queries
        // are selecting the columns.
        private const int CacheItemIdIndex = 0;
        private const int CacheItemValueIndex = 1;
        private const int ExpiresAtTimeIndex = 2;
        private const int SlidingExpirationInTicksIndex = 3;
        private const int AbsoluteExpirationIndex = 4;

        private readonly SqlServerCacheOptions _options;
        private readonly ILogger _logger;
        private readonly SqlQueries _sqlQueries;
        private DateTime _lastExpirationScan;

        public SqlServerCache(IOptions<SqlServerCacheOptions> options, ILoggerFactory loggerFactory)
        {
            _options = options.Options;
            _sqlQueries = new SqlQueries(_options.SchemaName, _options.TableName);
            _logger = loggerFactory.CreateLogger<SqlServerCache>();
        }

        public void Connect()
        {
            // Try connecting to the database and check if its available.
            using (var connection = new SqlConnection(_options.ConnectionString))
            {
                var command = new SqlCommand(_sqlQueries.GetTableSchema, connection);
                connection.Open();

                var reader = command.ExecuteReader(CommandBehavior.SchemaOnly);
                if(reader.Read())
                {
                    //TODO: log info
                }
            }
        }

        public async Task ConnectAsync()
        {
            // Try connecting to the database and check if its available.
            using (var connection = new SqlConnection(_options.ConnectionString))
            {
                var command = new SqlCommand(_sqlQueries.GetTableSchema, connection);
                await connection.OpenAsync();

                var reader = await command.ExecuteReaderAsync(CommandBehavior.SchemaOnly);

                if(await reader.ReadAsync())
                {
                    //TODO: log
                }
            }
        }

        public byte[] Get([NotNull] string key)
        {
            byte[] cacheItemvalue = null;
            var utcNow = _options.SystemClock.UtcNow.UtcDateTime;

            using (var connection = new SqlConnection(_options.ConnectionString))
            {
                var command = new SqlCommand(_sqlQueries.GetCacheItem, connection);
                command.Parameters
                    .AddCacheItemId(key)
                    .AddWithValue("UtcNow", SqlDbType.DateTime, utcNow);

                connection.Open();

                var reader = command.ExecuteReader(CommandBehavior.SingleRow);

                DateTime oldExpirationTime;
                TimeSpan? slidingExpiration = null;
                DateTime? absoluteExpiration = null;
                if (reader.Read())
                {
                    oldExpirationTime = reader.GetFieldValue<DateTime>(ExpiresAtTimeIndex);
                    cacheItemvalue = reader.GetFieldValue<byte[]>(CacheItemValueIndex);

                    if (!reader.IsDBNull(SlidingExpirationInTicksIndex))
                    {
                        slidingExpiration = TimeSpan.FromTicks(
                            reader.GetFieldValue<long>(SlidingExpirationInTicksIndex));
                    }

                    if (!reader.IsDBNull(AbsoluteExpirationIndex))
                    {
                        absoluteExpiration = reader.GetFieldValue<DateTime>(AbsoluteExpirationIndex);
                    }
                }
                else
                {
                    return null;
                }

                // Check if the current item has a sliding expiration and is going to expire within the
                // next timeout period(ex:20 minutes), then extend the expiration time.
                if (slidingExpiration.HasValue &&
                    utcNow >= (oldExpirationTime - slidingExpiration.Value))
                {
                    var newExpirationTime = GetExpirationTime(slidingExpiration, absoluteExpiration, utcNow);

                    if (oldExpirationTime != newExpirationTime)
                    {
                        reader.Dispose();

                        command = new SqlCommand(_sqlQueries.UpdateCacheItemExpiration, connection);
                        command.Parameters
                            .AddCacheItemId(key)
                            .AddExpiresAtTime(newExpirationTime);

                        command.ExecuteNonQuery();
                    }
                }
            }

            ScanForExpiredItemsIfRequired();

            return cacheItemvalue;
        }

        public async Task<byte[]> GetAsync([NotNull] string key)
        {
            byte[] cacheItemvalue = null;
            var utcNow = _options.SystemClock.UtcNow.UtcDateTime;

            using (var connection = new SqlConnection(_options.ConnectionString))
            {
                var command = new SqlCommand(_sqlQueries.GetCacheItem, connection);
                command.Parameters
                    .AddCacheItemId(key)
                    .AddWithValue("UtcNow", SqlDbType.DateTime, utcNow);

                await connection.OpenAsync();

                var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);

                DateTime oldExpirationTime;
                TimeSpan? slidingExpiration = null;
                DateTime? absoluteExpiration = null;
                if (await reader.ReadAsync())
                {
                    oldExpirationTime = await reader.GetFieldValueAsync<DateTime>(ExpiresAtTimeIndex);
                    cacheItemvalue = await reader.GetFieldValueAsync<byte[]>(CacheItemValueIndex);

                    if (!await reader.IsDBNullAsync(SlidingExpirationInTicksIndex))
                    {
                        slidingExpiration = TimeSpan.FromTicks(
                            await reader.GetFieldValueAsync<long>(SlidingExpirationInTicksIndex));
                    }

                    if (!reader.IsDBNull(AbsoluteExpirationIndex))
                    {
                        absoluteExpiration = await reader.GetFieldValueAsync<DateTime>(AbsoluteExpirationIndex);
                    }
                }
                else
                {
                    return null;
                }

                // Check if the current item has a sliding expiration and is going to expire within the
                // next timeout period(ex:20 minutes), then extend the expiration time.
                if (slidingExpiration.HasValue &&
                    utcNow >= (oldExpirationTime - slidingExpiration.Value))
                {
                    var newExpirationTime = GetExpirationTime(slidingExpiration, absoluteExpiration, utcNow);

                    if (oldExpirationTime != newExpirationTime)
                    {
                        reader.Dispose();

                        command = new SqlCommand(_sqlQueries.UpdateCacheItemExpiration, connection);
                        command.Parameters
                            .AddCacheItemId(key)
                            .AddExpiresAtTime(newExpirationTime);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }

            ScanForExpiredItemsIfRequired();

            return cacheItemvalue;
        }

        public void Refresh([NotNull] string key)
        {
            Get(key);
        }

        public async Task RefreshAsync([NotNull] string key)
        {
            await GetAsync(key);
        }

        public void Remove([NotNull] string key)
        {
            using (var connection = new SqlConnection(_options.ConnectionString))
            {
                var command = new SqlCommand(_sqlQueries.DeleteCacheItem, connection);
                command.Parameters.AddCacheItemId(key);

                connection.Open();

                command.ExecuteNonQuery();
            }

            ScanForExpiredItemsIfRequired();
        }

        public async Task RemoveAsync([NotNull] string key)
        {
            using (var connection = new SqlConnection(_options.ConnectionString))
            {
                var command = new SqlCommand(_sqlQueries.DeleteCacheItem, connection);
                command.Parameters.AddCacheItemId(key);

                await connection.OpenAsync();

                await command.ExecuteNonQueryAsync();
            }

            ScanForExpiredItemsIfRequired();
        }

        public void Set([NotNull] string key, [NotNull] byte[] value, [NotNull] DistributedCacheEntryOptions options)
        {
            var utcNow = _options.SystemClock.UtcNow.UtcDateTime;
            var expirationInfo = GetExpirationInfo(utcNow, options);

            using (var connection = new SqlConnection(_options.ConnectionString))
            {
                // Try updating the cache item and if it does not succeed then its probably a new item
                var command = new SqlCommand(_sqlQueries.UpdateCacheItem, connection);
                command.Parameters
                    .AddCacheItemId(key)
                    .AddCacheItemValue(value)
                    .AddExpiresAtTime(expirationInfo.ExpiresAtTime);

                connection.Open();

                var recordsAffected = command.ExecuteNonQuery();

                // only add if update didn't affect any records
                if (recordsAffected <= 0)
                {
                    command = new SqlCommand(_sqlQueries.AddCacheItem, connection);
                    command.Parameters
                        .AddCacheItemId(key)
                        .AddCacheItemValue(value)
                        .AddExpiresAtTime(expirationInfo.ExpiresAtTime)
                        .AddSlidingExpirationInTicks(expirationInfo.SlidingExpiration)
                        .AddAbsoluteExpiration(expirationInfo.AbsoluteExpiration);

                    recordsAffected = command.ExecuteNonQuery();
                }
            }

            ScanForExpiredItemsIfRequired();
        }

        public async Task SetAsync(
            [NotNull] string key,
            [NotNull] byte[] value,
            [NotNull] DistributedCacheEntryOptions options)
        {
            var utcNow = _options.SystemClock.UtcNow.UtcDateTime;
            var expirationInfo = GetExpirationInfo(utcNow, options);

            using (var connection = new SqlConnection(_options.ConnectionString))
            {
                // Try updating the cache item and if it does not succeed then its probably a new item
                var command = new SqlCommand(_sqlQueries.UpdateCacheItem, connection);
                command.Parameters
                    .AddCacheItemId(key)
                    .AddCacheItemValue(value)
                    .AddExpiresAtTime(expirationInfo.ExpiresAtTime);

                await connection.OpenAsync();

                var recordsAffected = await command.ExecuteNonQueryAsync();

                // only add if update didn't affect any records
                if (recordsAffected <= 0)
                {
                    command = new SqlCommand(_sqlQueries.AddCacheItem, connection);
                    command.Parameters
                        .AddCacheItemId(key)
                        .AddCacheItemValue(value)
                        .AddExpiresAtTime(expirationInfo.ExpiresAtTime)
                        .AddSlidingExpirationInTicks(expirationInfo.SlidingExpiration)
                        .AddAbsoluteExpiration(expirationInfo.AbsoluteExpiration);

                    recordsAffected = await command.ExecuteNonQueryAsync();
                }
            }

            ScanForExpiredItemsIfRequired();
        }

        // Called by multiple actions to see how long it's been since we last checked for expired items.
        // If sufficient time has elapsed then a scan is initiated on a background task.
        private void ScanForExpiredItemsIfRequired()
        {
            var utcNow = _options.SystemClock.UtcNow.UtcDateTime;
            if ((utcNow - _lastExpirationScan) > _options.ExpiredItemsDeletionInterval)
            {
                _lastExpirationScan = utcNow;
                ThreadPool.QueueUserWorkItem(DeleteExpiredCacheItems, state: utcNow);
            }
        }

        // to enable unit testing
        internal void DeleteExpiredCacheItems(object state)
        {
            var utcNow = (DateTime)state;
            var connection = new SqlConnection(_options.ConnectionString);
            var command = new SqlCommand(_sqlQueries.DeleteExpiredCacheItems, connection);
            command.Parameters.AddWithValue("UtcNow", SqlDbType.DateTime, utcNow);

            try
            {
                connection.Open();

                var effectedRowCount = command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError("An error occurred while deleting expired cache items.", ex);
            }
            finally
            {
                connection.Close();
            }
        }

        private ExpirationInfo GetExpirationInfo(DateTime utcNow, DistributedCacheEntryOptions options)
        {
            var result = new ExpirationInfo();
            result.SlidingExpiration = options.SlidingExpiration;

            // calculate absolute expiration
            DateTime? absoluteExpiration = null;
            if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                absoluteExpiration = utcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
            }
            else if (options.AbsoluteExpiration.HasValue)
            {
                // Since DateTimeOffset has the timezone offset information,
                // try getting the UTC time out of it.
                var absoluteExpirationInUTC = options.AbsoluteExpiration.Value.UtcDateTime;
                if (absoluteExpirationInUTC <= utcNow)
                {
                    throw new InvalidOperationException("The absolute expiration value must be in the future.");
                }

                absoluteExpiration = absoluteExpirationInUTC;
            }
            result.AbsoluteExpiration = absoluteExpiration;

            result.ExpiresAtTime = GetExpirationTime(result.SlidingExpiration, result.AbsoluteExpiration, utcNow);

            return result;
        }

        private DateTime GetExpirationTime(
            TimeSpan? slidingExpiration,
            DateTime? absoluteExpiration,
            DateTime utcNow)
        {
            if (!slidingExpiration.HasValue && !absoluteExpiration.HasValue)
            {
                throw new InvalidOperationException("Either absolute or sliding expiration needs " +
                    "to be provided.");
            }

            if (slidingExpiration.HasValue)
            {
                // if there is also an absolute expiration, then the sliding expiration extension should
                // not exceed the absolute expiration.
                var newSlidingExpirationTime = utcNow + TimeSpan.FromTicks(
                    ExpirationTimeMultiplier * slidingExpiration.Value.Ticks);
                if (absoluteExpiration.HasValue && newSlidingExpirationTime > absoluteExpiration.Value)
                {
                    return absoluteExpiration.Value;
                }

                return newSlidingExpirationTime;
            }

            return absoluteExpiration.Value;
        }

        private class ExpirationInfo
        {
            // This should never be null. This value is calculated based on Sliding or Absolute expiration
            public DateTime ExpiresAtTime { get; set; }

            public TimeSpan? SlidingExpiration { get; set; }

            public DateTime? AbsoluteExpiration { get; set; }
        }
    }
}