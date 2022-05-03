﻿using Chloe;
using Chloe.MySql;
using Chloe.Sharding;
using Chloe.SqlServer;
using System;
using System.Collections.Generic;
using System.Text;

namespace ChloeDemo.Sharding
{
    public class OrderDbContextProviderFactory : IDbContextProviderFactory
    {
        ShardingTest _shardingTest;
        int _year;
        public OrderDbContextProviderFactory(ShardingTest shardingTest, int year)
        {
            this._shardingTest = shardingTest;
            this._year = year;
        }

        public IDbContextProvider CreateDbContextProvider()
        {
            return this._shardingTest.CreateDbContextProvider(this._year);
        }
    }
}
