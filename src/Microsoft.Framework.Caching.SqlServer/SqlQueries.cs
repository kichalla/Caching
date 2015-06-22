// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.Caching.SqlServer
{
    internal class SqlQueries
    {
        private const string CreateTableFormat = "CREATE TABLE {0}(" +
            "Id nvarchar(100)  NOT NULL PRIMARY KEY, " +
            "Value varbinary(MAX) NOT NULL, " +
            "ExpiresAtTime datetime2 NOT NULL, " +
            "SlidingExpirationInTicks bigint NULL," +
            "AbsoluteExpiration datetime2 NULL)";

        private const string CreateNonClusteredIndexOnExpirationTimeFormat
            = "CREATE NONCLUSTERED INDEX Index_ExpiresAtTime ON {0}(ExpiresAtTime)";

        // Used for getting table schema when trying to 'Connect' to the database
        private const string GetTableSchemaFormat = "SELECT * FROM {0}";

        private const string GetCacheItemFormat =
            "SELECT Id, Value, ExpiresAtTime, SlidingExpirationInTicks, AbsoluteExpiration " +
            "FROM {0} WHERE Id = @Id AND @UtcNow <= ExpiresAtTime";

        private const string AddCacheItemFormat = "INSERT INTO {0} " +
            "(Id, Value, ExpiresAtTime, SlidingExpirationInTicks, AbsoluteExpiration) " +
            "VALUES (@Id, @Value, @ExpiresAtTime, @SlidingExpirationInTicks, @AbsoluteExpiration)";

        private const string UpdateCacheItemFormat = "UPDATE {0} SET Value = @Value, " +
            "ExpiresAtTime = @ExpiresAtTime  WHERE Id = @Id";

        private const string DeleteCacheItemFormat = "DELETE FROM {0} WHERE Id = @Id";

        private const string UpdateCacheItemExpirationFormat = "UPDATE {0} SET ExpiresAtTime = @ExpiresAtTime " +
            "WHERE Id = @Id";

        public const string DeleteExpiredCacheItemsFormat = "DELETE FROM {0} WHERE @UtcNow > ExpiresAtTime";

        public SqlQueries(string schemaName, string tableName)
        {
            //TODO: sanitize schema and table name

            var tableNameWithSchema = string.Format("[{0}].[{1}]", schemaName, tableName);
            CreateTable = string.Format(CreateTableFormat, tableNameWithSchema);
            CreateNonClusteredIndexOnExpirationTime = string.Format(
                CreateNonClusteredIndexOnExpirationTimeFormat,
                tableNameWithSchema);
            GetTableSchema = string.Format(GetTableSchemaFormat, tableNameWithSchema);
            GetCacheItem = string.Format(GetCacheItemFormat, tableNameWithSchema);
            AddCacheItem = string.Format(AddCacheItemFormat, tableNameWithSchema);
            UpdateCacheItem = string.Format(UpdateCacheItemFormat, tableNameWithSchema);
            DeleteCacheItem = string.Format(DeleteCacheItemFormat, tableNameWithSchema);
            UpdateCacheItemExpiration = string.Format(UpdateCacheItemExpirationFormat, tableNameWithSchema);
            DeleteExpiredCacheItems = string.Format(DeleteExpiredCacheItemsFormat, tableNameWithSchema);
        }

        public string CreateTable { get; }

        public string CreateNonClusteredIndexOnExpirationTime { get; }

        public string GetTableSchema { get; }

        public string GetCacheItem { get; }

        public string AddCacheItem { get; }

        public string UpdateCacheItem { get; }

        public string DeleteCacheItem { get; }

        public string UpdateCacheItemExpiration { get; }

        public string DeleteExpiredCacheItems { get; }
    }
}
