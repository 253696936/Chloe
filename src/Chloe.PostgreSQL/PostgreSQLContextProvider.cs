﻿using Chloe.Core;
using Chloe.Visitors;
using Chloe.DbExpressions;
using Chloe.Descriptors;
using Chloe.Exceptions;
using Chloe.Infrastructure;
using Chloe.RDBMS;
using Chloe.Utility;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Chloe.PostgreSQL
{
    public partial class PostgreSQLContextProvider : DbContextProvider
    {
        DatabaseProvider _databaseProvider;

        public PostgreSQLContextProvider(Func<IDbConnection> dbConnectionFactory) : this(new DbConnectionFactory(dbConnectionFactory))
        {

        }

        public PostgreSQLContextProvider(IDbConnectionFactory dbConnectionFactory) : this(new PostgreSQLOptions() { DbConnectionFactory = dbConnectionFactory })
        {

        }

        public PostgreSQLContextProvider(PostgreSQLOptions options) : base(options)
        {
            this._databaseProvider = new DatabaseProvider(this);
        }

        public new PostgreSQLOptions Options { get { return base.Options as PostgreSQLOptions; } }
        public override IDatabaseProvider DatabaseProvider
        {
            get { return this._databaseProvider; }
        }


        /// <summary>
        /// 设置属性解析器。
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="handler"></param>
        public static void SetPropertyHandler(string propertyName, IPropertyHandler handler)
        {
            PublicHelper.CheckNull(propertyName, nameof(propertyName));
            PublicHelper.CheckNull(handler, nameof(handler));
            lock (SqlGenerator.PropertyHandlerDic)
            {
                List<IPropertyHandler> propertyHandlers = new List<IPropertyHandler>();
                propertyHandlers.Add(handler);
                if (SqlGenerator.PropertyHandlerDic.TryGetValue(propertyName, out var propertyHandlerArray))
                {
                    propertyHandlers.AddRange(propertyHandlerArray);
                }

                SqlGenerator.PropertyHandlerDic[propertyName] = propertyHandlers.ToArray();
            }
        }

        /// <summary>
        /// 设置方法解析器。
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="handler"></param>
        public static void SetMethodHandler(string methodName, IMethodHandler handler)
        {
            PublicHelper.CheckNull(methodName, nameof(methodName));
            PublicHelper.CheckNull(handler, nameof(handler));
            lock (SqlGenerator.MethodHandlerDic)
            {
                List<IMethodHandler> methodHandlers = new List<IMethodHandler>();
                methodHandlers.Add(handler);
                if (SqlGenerator.MethodHandlerDic.TryGetValue(methodName, out var methodHandlerArray))
                {
                    methodHandlers.AddRange(methodHandlerArray);
                }

                SqlGenerator.MethodHandlerDic[methodName] = methodHandlers.ToArray();
            }
        }


        protected override async Task<TEntity> Insert<TEntity>(TEntity entity, string table, bool @async)
        {
            PublicHelper.CheckNull(entity);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            bool ignoreNullValueInsert = (this.Options.InsertStrategy & InsertStrategy.IgnoreNull) == InsertStrategy.IgnoreNull;
            bool ignoreEmptyStringValueInsert = (this.Options.InsertStrategy & InsertStrategy.IgnoreEmptyString) == InsertStrategy.IgnoreEmptyString;
            PrimitivePropertyDescriptor firstIgnoreProperty = null;
            object firstIgnorePropertyValue = null;

            Func<object, bool> canIgnoreInsert = value =>
            {
                if (ignoreNullValueInsert && value == null)
                {
                    return true;
                }
                if (ignoreEmptyStringValueInsert && string.Empty.Equals(value))
                {
                    return true;
                }

                return false;
            };

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            DbInsertExpression insertExpression = new DbInsertExpression(dbTable);

            foreach (PrimitivePropertyDescriptor propertyDescriptor in typeDescriptor.PrimitivePropertyDescriptors)
            {
                if (propertyDescriptor.IsAutoIncrement)
                    continue;

                if (propertyDescriptor.HasSequence())
                {
                    DbMethodCallExpression getNextValueForSequenceExp = PublicHelper.MakeNextValueForSequenceDbExpression(propertyDescriptor, dbTable.Schema);
                    insertExpression.AppendInsertColumn(propertyDescriptor.Column, getNextValueForSequenceExp);
                    continue;
                }

                object val = propertyDescriptor.GetValue(entity);

                PublicHelper.NotNullCheck(propertyDescriptor, val);
                PublicHelper.EnsurePrimaryKeyNotNull(propertyDescriptor, val);

                if (canIgnoreInsert(val))
                {
                    if (firstIgnoreProperty == null)
                    {
                        firstIgnoreProperty = propertyDescriptor;
                        firstIgnorePropertyValue = val;
                    }

                    continue;
                }

                DbParameterExpression valExp = DbExpression.Parameter(val, propertyDescriptor.PropertyType, propertyDescriptor.Column.DbType);
                insertExpression.AppendInsertColumn(propertyDescriptor.Column, valExp);
            }

            if (insertExpression.InsertColumns.Count == 0 && firstIgnoreProperty != null)
            {
                DbExpression valExp = DbExpression.Parameter(firstIgnorePropertyValue, firstIgnoreProperty.PropertyType, firstIgnoreProperty.Column.DbType);
                insertExpression.AppendInsertColumn(firstIgnoreProperty.Column, valExp);
            }

            List<Action<TEntity, IDataReader>> mappers = new List<Action<TEntity, IDataReader>>();
            foreach (var item in typeDescriptor.PrimitivePropertyDescriptors.Where(a => a.IsAutoIncrement || a.HasSequence()))
            {
                mappers.Add(PublicHelper.GetMapper<TEntity>(item, insertExpression.Returns.Count));
                insertExpression.Returns.Add(item.Column);
            }

            if (mappers.Count == 0)
            {
                await this.ExecuteNonQuery(insertExpression, @async);
                return entity;
            }

            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            DbCommandInfo dbCommandInfo = translator.Translate(insertExpression);

            IDataReader dataReader = this.Session.ExecuteReader(dbCommandInfo.CommandText, dbCommandInfo.GetParameters());
            using (dataReader)
            {
                dataReader.Read();
                foreach (var mapper in mappers)
                {
                    mapper(entity, dataReader);
                }
            }

            return entity;
        }
        protected override async Task<object> Insert<TEntity>(Expression<Func<TEntity>> content, string table, bool @async)
        {
            PublicHelper.CheckNull(content);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            if (typeDescriptor.PrimaryKeys.Count > 1)
            {
                /* 对于多主键的实体，暂时不支持调用这个方法进行插入 */
                throw new NotSupportedException(string.Format("Can not call this method because entity '{0}' has multiple keys.", typeDescriptor.Definition.Type.FullName));
            }

            PrimitivePropertyDescriptor keyPropertyDescriptor = typeDescriptor.PrimaryKeys.FirstOrDefault();

            List<KeyValuePair<MemberInfo, Expression>> insertColumns = InitMemberExtractor.Extract(content);

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);

            DefaultExpressionParser expressionParser = typeDescriptor.GetExpressionParser(dbTable);
            DbInsertExpression insertExpression = new DbInsertExpression(dbTable);

            object keyVal = null;

            foreach (var kv in insertColumns)
            {
                MemberInfo key = kv.Key;
                PrimitivePropertyDescriptor propertyDescriptor = typeDescriptor.GetPrimitivePropertyDescriptor(key);

                if (propertyDescriptor.IsAutoIncrement)
                    throw new ChloeException(string.Format("Could not insert value into the identity column '{0}'.", propertyDescriptor.Column.Name));

                if (propertyDescriptor.HasSequence())
                    throw new ChloeException(string.Format("Can not insert value into the column '{0}', because it's mapping member has define a sequence.", propertyDescriptor.Column.Name));

                if (propertyDescriptor.IsPrimaryKey)
                {
                    object val = ExpressionEvaluator.Evaluate(kv.Value);
                    if (val == null)
                        throw new ChloeException(string.Format("The primary key '{0}' could not be null.", propertyDescriptor.Property.Name));
                    else
                    {
                        keyVal = val;
                        insertExpression.AppendInsertColumn(propertyDescriptor.Column, DbExpression.Parameter(keyVal));
                        continue;
                    }
                }

                insertExpression.AppendInsertColumn(propertyDescriptor.Column, expressionParser.Parse(kv.Value));
            }

            foreach (var item in typeDescriptor.PrimitivePropertyDescriptors.Where(a => a.HasSequence()))
            {
                DbMethodCallExpression getNextValueForSequenceExp = PublicHelper.MakeNextValueForSequenceDbExpression(item, dbTable.Schema);
                insertExpression.AppendInsertColumn(item.Column, getNextValueForSequenceExp);
            }

            if (keyPropertyDescriptor != null)
            {
                //主键为空并且主键又不是自增列
                if (keyVal == null && !keyPropertyDescriptor.IsAutoIncrement && !keyPropertyDescriptor.HasSequence())
                {
                    throw new ChloeException(string.Format("The primary key '{0}' could not be null.", keyPropertyDescriptor.Property.Name));
                }
            }

            if (keyPropertyDescriptor == null)
            {
                await this.ExecuteNonQuery(insertExpression, @async);
                return keyVal; /* It will return null if an entity does not define primary key. */
            }
            if (!keyPropertyDescriptor.IsAutoIncrement && !keyPropertyDescriptor.HasSequence())
            {
                await this.ExecuteNonQuery(insertExpression, @async);
                return keyVal;
            }

            insertExpression.Returns.Add(keyPropertyDescriptor.Column);

            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            DbCommandInfo dbCommandInfo = translator.Translate(insertExpression);

            object ret = this.Session.ExecuteScalar(dbCommandInfo.CommandText, dbCommandInfo.GetParameters());
            if (ret == null || ret == DBNull.Value)
            {
                throw new ChloeException("Unable to get the identity/sequence value.");
            }

            ret = PublicHelper.ConvertObjectType(ret, typeDescriptor.AutoIncrement.PropertyType);
            return ret;
        }

        protected override async Task InsertRange<TEntity>(List<TEntity> entities, string table, bool @async)
        {
            /*
             * 将 entities 分批插入数据库
             * 每批生成 insert into TableName(...) values(...),(...)... 
             * 该方法相对循环一条一条插入，速度提升 2/3 这样
             */

            PublicHelper.CheckNull(entities);
            if (entities.Count == 0)
                return;

            int maxParameters = 2100;
            int batchSize = 50; /* 每批实体大小，此值通过测试得出相对插入速度比较快的一个值 */

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            List<PrimitivePropertyDescriptor> mappingPropertyDescriptors = typeDescriptor.PrimitivePropertyDescriptors.Where(a => a.IsAutoIncrement == false).ToList();
            int maxDbParamsCount = maxParameters - mappingPropertyDescriptors.Count; /* 控制一个 sql 的参数个数 */

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            string sqlTemplate = this.AppendInsertRangeSqlTemplate(dbTable, mappingPropertyDescriptors);

            Func<Task> insertAction = async () =>
            {
                int batchCount = 0;
                List<DbParam> dbParams = new List<DbParam>();
                StringBuilder sqlBuilder = new StringBuilder();
                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];

                    if (batchCount > 0)
                        sqlBuilder.Append(",");

                    sqlBuilder.Append("(");
                    for (int j = 0; j < mappingPropertyDescriptors.Count; j++)
                    {
                        if (j > 0)
                            sqlBuilder.Append(",");

                        PrimitivePropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[j];

                        if (mappingPropertyDescriptor.HasSequence())
                        {
                            string sequenceSchema = mappingPropertyDescriptor.Definition.SequenceSchema;
                            sequenceSchema = string.IsNullOrEmpty(sequenceSchema) ? dbTable.Schema : sequenceSchema;

                            sqlBuilder.Append("nextval('");

                            if (!string.IsNullOrEmpty(sequenceSchema))
                            {
                                sqlBuilder.Append(sequenceSchema).Append(".");
                            }

                            sqlBuilder.Append(mappingPropertyDescriptor.Definition.SequenceName);
                            sqlBuilder.Append("')");
                            continue;
                        }

                        object val = mappingPropertyDescriptor.GetValue(entity);

                        PublicHelper.NotNullCheck(mappingPropertyDescriptor, val);

                        if (val == null)
                        {
                            sqlBuilder.Append("NULL");
                            continue;
                        }

                        Type valType = val.GetType();
                        if (valType.IsEnum)
                        {
                            val = Convert.ChangeType(val, Enum.GetUnderlyingType(valType));
                            valType = val.GetType();
                        }

                        if (PublicHelper.IsToStringableNumericType(valType))
                        {
                            sqlBuilder.Append(val.ToString());
                            continue;
                        }

                        if (val is bool)
                        {
                            if ((bool)val == true)
                                sqlBuilder.AppendFormat("true");
                            else
                                sqlBuilder.AppendFormat("false");
                            continue;
                        }

                        string paramName = UtilConstants.ParameterNamePrefix + dbParams.Count.ToString();
                        DbParam dbParam = new DbParam(paramName, val) { DbType = mappingPropertyDescriptor.Column.DbType };
                        dbParams.Add(dbParam);
                        sqlBuilder.Append(paramName);
                    }
                    sqlBuilder.Append(")");

                    batchCount++;

                    if ((batchCount >= 20 && dbParams.Count >= 120/*参数个数太多也会影响速度*/) || dbParams.Count >= maxDbParamsCount || batchCount >= batchSize || (i + 1) == entities.Count)
                    {
                        sqlBuilder.Insert(0, sqlTemplate);
                        string sql = sqlBuilder.ToString();
                        await this.Session.ExecuteNonQuery(sql, dbParams.ToArray(), @async);

                        sqlBuilder.Clear();
                        dbParams.Clear();
                        batchCount = 0;
                    }
                }
            };

            Func<Task> fAction = insertAction;

            if (this.Session.IsInTransaction)
            {
                await fAction();
                return;
            }

            /* 因为分批插入，所以需要开启事务保证数据一致性 */
            this.Session.BeginTransaction();
            try
            {
                await fAction();
                this.Session.CommitTransaction();
            }
            catch
            {
                this.Session.RollbackTransaction();
                throw;
            }
        }


        protected override string GetSelectLastInsertIdClause()
        {
            throw new NotSupportedException();
        }
    }
}
