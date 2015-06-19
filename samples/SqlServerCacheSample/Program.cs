﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Framework.Caching.Distributed;
using Microsoft.Framework.Caching.SqlServer;
using Microsoft.Framework.Logging;
using Microsoft.Framework.OptionsModel;

namespace SqlServerCacheSample
{
    /// <summary>
    /// This sample requires setting up a Microsoft SQL Server based cache database.
    /// 1. Create a new database or use as existing gone.
    /// 2. Run the command "dnx . create-sqlservercache <connectionstring-here> <name-of-tableschema-to-be-created> <name-of-table-to-be-created>"
    ///    to setup the table.
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var key = Guid.NewGuid().ToString();
            var message = "Hello, World!";
            var value = Encoding.UTF8.GetBytes(message);

            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole();

            Console.WriteLine("Connecting to cache");
            var cache = new SqlServerCache(
                new OptionsWrapper<SqlServerCacheOptions>(
                new SqlServerCacheOptions()
                {
                    ConnectionString = "Server=localhost;Database=CacheSampleDb;Trusted_Connection=True;",
                    SchemaName = "dbo",
                    TableName = "CacheSample"
                }),
                loggerFactory);
            await cache.ConnectAsync();

            Console.WriteLine("Connected");

            Console.WriteLine("Cache item key: {0}", key);
            Console.WriteLine($"Setting value '{message}' in cache");
            await cache.SetAsync(
                key,
                value,
                new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(10)));
            Console.WriteLine("Set");

            Console.WriteLine("Getting value from cache");
            value = await cache.GetAsync(key);
            if (value != null)
            {
                Console.WriteLine("Retrieved: " + Encoding.UTF8.GetString(value));
            }
            else
            {
                Console.WriteLine("Not Found");
            }

            Console.WriteLine("Refreshing value in cache");
            await cache.RefreshAsync(key);
            Console.WriteLine("Refreshed");

            Console.WriteLine("Removing value from cache");
            await cache.RemoveAsync(key);
            Console.WriteLine("Removed");

            Console.WriteLine("Getting value from cache again");
            value = await cache.GetAsync(key);
            if (value != null)
            {
                Console.WriteLine("Retrieved: " + Encoding.UTF8.GetString(value));
            }
            else
            {
                Console.WriteLine("Not Found");
            }

            Console.ReadLine();
        }

        private class OptionsWrapper<T> : IOptions<T> where T : class, new()
        {
            private readonly T _innerOptions;

            public OptionsWrapper(T innerOptions)
            {
                _innerOptions = innerOptions;
            }

            public T Options
            {
                get
                {
                    return _innerOptions;
                }
            }

            public T GetNamedOptions(string name)
            {
                return _innerOptions;
            }
        }
    }
}
