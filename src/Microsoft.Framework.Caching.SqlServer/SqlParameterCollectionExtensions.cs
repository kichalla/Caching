// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;

namespace Microsoft.Framework.Caching.SqlServer
{
    internal static class SqlParameterCollectionExtensions
    {
        // Table column names
        private const string CacheItemId = "Id";
        private const string CacheItemValue = "Value";
        private const string ExpiresAtTime = "ExpiresAtTime";
        private const string SlidingExpirationInTicks = "SlidingExpirationInTicks";
        private const string AbsoluteExpiration = "AbsoluteExpiration";

        public static SqlParameterCollection AddCacheItemId(this SqlParameterCollection parameters, string value)
        {
            return parameters.AddWithValue(CacheItemId, SqlDbType.NVarChar, 100, value);
        }

        public static SqlParameterCollection AddCacheItemValue(this SqlParameterCollection parameters, byte[] value)
        {
            // For all values where the length is less than the below value, try setting the size of the
            // parameter for improved performance.
            var defaultVarBinaryColumnLength = 8000;

            if (value != null && value.Length < defaultVarBinaryColumnLength)
            {
                return parameters.AddWithValue(
                    CacheItemValue,
                    SqlDbType.VarBinary,
                    defaultVarBinaryColumnLength,
                    value);
            }
            else
            {
                // do not mention the size
                return parameters.AddWithValue(CacheItemValue, SqlDbType.VarBinary, value);
            }
        }

        public static SqlParameterCollection AddExpiresAtTime(
            this SqlParameterCollection parameters,
            DateTimeOffset value)
        {
            return parameters.AddWithValue(ExpiresAtTime, SqlDbType.DateTime2, value.DateTime);
        }

        public static SqlParameterCollection AddSlidingExpirationInTicks(
            this SqlParameterCollection parameters,
            TimeSpan? value)
        {
            if (value.HasValue)
            {
                return parameters.AddWithValue(SlidingExpirationInTicks, SqlDbType.BigInt, value.Value.Ticks);
            }
            else
            {
                return parameters.AddWithValue(SlidingExpirationInTicks, SqlDbType.BigInt, DBNull.Value);
            }
        }

        public static SqlParameterCollection AddAbsoluteExpiration(
            this SqlParameterCollection parameters,
            DateTimeOffset? value)
        {
            if (value.HasValue)
            {
                return parameters.AddWithValue(AbsoluteExpiration, SqlDbType.DateTime2, value.Value.DateTime);
            }
            else
            {
                return parameters.AddWithValue(AbsoluteExpiration, SqlDbType.DateTime2, DBNull.Value);
            }
        }

        public static SqlParameterCollection AddWithValue(
            this SqlParameterCollection parameters,
            string parameterName,
            SqlDbType dbType,
            object value)
        {
            var parameter = new SqlParameter(parameterName, dbType);
            parameter.Value = value;
            parameters.Add(parameter);
            return parameters;
        }

        public static SqlParameterCollection AddWithValue(
            this SqlParameterCollection parameters,
            string parameterName,
            SqlDbType dbType,
            int size,
            object value)
        {
            var parameter = new SqlParameter(parameterName, dbType, size);
            parameter.Value = value;
            parameters.Add(parameter);
            return parameters;
        }
    }
}
