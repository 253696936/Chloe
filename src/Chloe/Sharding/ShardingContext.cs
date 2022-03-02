﻿using Chloe.Descriptors;
using System.Reflection;

namespace Chloe.Sharding
{
    internal interface IShardingContext
    {
        TypeDescriptor TypeDescriptor { get; }
        ShardingDbContext DbContext { get; }
        IShardingRoute Route { get; }
        int MaxInItems { get; }
        bool IsPrimaryKey(MemberInfo member);
        bool IsShardingMember(MemberInfo member);
        List<IDbContext> CreateDbContexts(IPhysicDataSource dataSource, int count);
    }

    class ShardingContext : IShardingContext
    {
        public ShardingContext(ShardingDbContext dbContext, IShardingConfig shardingConfig, TypeDescriptor typeDescriptor)
        {
            this.DbContext = dbContext;
            this.TypeDescriptor = typeDescriptor;
            this.ShardingConfig = shardingConfig;
            this.Route = shardingConfig.RouteFactory.CreateRoute();
        }

        public TypeDescriptor TypeDescriptor { get; set; }
        public ShardingDbContext DbContext { get; set; }
        public IShardingRoute Route { get; private set; }

        public int MaxConnectionsPerDataSource { get { return this.DbContext.Options.MaxConnectionsPerDataSource; } }
        public int MaxInItems { get { return this.DbContext.Options.MaxInItems; } }

        public IShardingConfig ShardingConfig { get; private set; }

        public bool IsShardingMember(MemberInfo member)
        {
            return this.ShardingConfig.ShardingKey == member;
        }

        public bool IsPrimaryKey(MemberInfo member)
        {
            return this.TypeDescriptor.IsPrimaryKey(member);
        }

        public List<IDbContext> CreateDbContexts(IPhysicDataSource dataSource, int count)
        {
            var routeDbContextFactory = (dataSource as PhysicDataSource).DataSource.DbContextFactory;
            int connectionCount = Math.Min(count, this.MaxConnectionsPerDataSource);

            List<IDbContext> dbContexts = new List<IDbContext>(connectionCount);

            for (int i = 0; i < connectionCount; i++)
            {
                var dbContext = routeDbContextFactory.CreateDbContext();

                foreach (var kv in (this.DbContext as IDbContextInternal).QueryFilters)
                {
                    foreach (var filter in kv.Value)
                    {
                        dbContext.HasQueryFilter(kv.Key, filter);
                    }
                }

                dbContexts.Add(dbContext);
            }

            return dbContexts;

            throw new NotImplementedException();
        }
    }

    static class ShardingContextExtension
    {
        public static List<RouteTable> GetTables(this IShardingContext shardingContext)
        {
            return shardingContext.Route.GetTables(shardingContext.DbContext);
        }
        public static List<RouteTable> GetTables(this IShardingContext shardingContext, object shardingValue, ShardingOperator shardingOperator)
        {
            return shardingContext.Route.GetTables(shardingContext.DbContext, shardingValue, shardingOperator);
        }
        public static RouteTable GetTable(this IShardingContext shardingContext, object shardingValue)
        {
            return shardingContext.Route.GetTable(shardingContext.DbContext, shardingValue);
        }
        public static List<RouteTable> GetTablesByKey(this IShardingContext shardingContext, object keyValue)
        {
            return shardingContext.Route.GetTablesByKey(shardingContext.DbContext, keyValue);
        }
        public static SortResult SortTables(this IShardingContext shardingContext, List<RouteTable> tables, List<Ordering> orderings)
        {
            return shardingContext.Route.SortTables(shardingContext.DbContext, tables, orderings);
        }
    }

    public enum ShardingOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
    }

    public class SortResult
    {
        public bool IsOrdered { get; set; }
        public List<RouteTable> Tables { get; set; }
    }
}