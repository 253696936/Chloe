﻿using Chloe.Infrastructure;
using Chloe.RDBMS;
using System.Data;

namespace Chloe.KingbaseES
{
    public class KingbaseESContext : DbContext
    {
        public KingbaseESContext(Func<IDbConnection> dbConnectionFactory) : this(new DbConnectionFactory(dbConnectionFactory))
        {

        }

        public KingbaseESContext(IDbConnectionFactory dbConnectionFactory) : this(new KingbaseESOptions() { DbConnectionFactory = dbConnectionFactory })
        {

        }

        public KingbaseESContext(KingbaseESOptions options) : base(options, new DbContextProviderFactory(options))
        {
            this.Options = options;
        }

        public new KingbaseESOptions Options { get; private set; }

        protected override DbContext CloneImpl()
        {
            KingbaseESOptions options = new KingbaseESOptions()
            {
                DbConnectionFactory = this.Options.DbConnectionFactory,
                InsertStrategy = this.Options.InsertStrategy,
                MaxNumberOfParameters = this.Options.MaxNumberOfParameters,
                MaxInItems = this.Options.MaxInItems,
                DefaultInsertCountPerBatchForInsertRange = this.Options.DefaultInsertCountPerBatchForInsertRange,
                ConvertToLowercase = this.Options.ConvertToLowercase
            };

            KingbaseESContext dbContext = new KingbaseESContext(options);
            return dbContext;
        }

        /// <summary>
        /// 设置属性解析器。
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="handler"></param>
        public static void SetPropertyHandler(string propertyName, IPropertyHandler handler)
        {
            KingbaseESContextProvider.SetPropertyHandler(propertyName, handler);
        }

        /// <summary>
        /// 设置方法解析器。
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="handler"></param>
        public static void SetMethodHandler(string methodName, IMethodHandler handler)
        {
            KingbaseESContextProvider.SetMethodHandler(methodName, handler);
        }
    }

    class DbContextProviderFactory : IDbContextProviderFactory
    {
        KingbaseESOptions _options;

        public DbContextProviderFactory(KingbaseESOptions options)
        {
            PublicHelper.CheckNull(options);
            this._options = options;
        }

        public IDbContextProvider CreateDbContextProvider()
        {
            return new KingbaseESContextProvider(this._options);
        }
    }
}
