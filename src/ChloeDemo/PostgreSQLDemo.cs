﻿using Chloe;
using Chloe.Descriptors;
using Chloe.Infrastructure;
using Chloe.PostgreSQL;
using Chloe.Reflection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChloeDemo
{
    class PostgreSQLDemo : DemoBase
    {
        PostgreSQLContext _dbContext;
        public PostgreSQLDemo()
        {
            this._dbContext = new PostgreSQLContext(new PostgreSQLConnectionFactory("User ID=postgres;Password=sasa;Host=localhost;Port=5432;Database=Chloe;Pooling=true;"));

            DbConfiguration.UseTypeBuilders(typeof(TestEntityMap));
        }

        public override IDbContext DbContext
        {
            get
            {
                return this._dbContext;
            }
        }

        public override void InitTable<TEntity>()
        {
            Type entityType = typeof(TEntity);

            string createTableScript = this.CreateTableScript(entityType);

            this.DbContext.Session.ExecuteNonQuery(createTableScript);
        }
        string CreateTableScript(Type entityType)
        {
            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(entityType);
            string tableName = typeDescriptor.Table.Name;

            StringBuilder sb = new StringBuilder();
            sb.Append($"CREATE TABLE IF NOT EXISTS {this.QuoteName(tableName)}(");

            string c = "";
            foreach (var propertyDescriptor in typeDescriptor.PrimitivePropertyDescriptors)
            {
                sb.AppendLine(c);
                sb.Append($"  {this.BuildColumnPart(propertyDescriptor)}");
                c = ",";
            }

            if (typeDescriptor.PrimaryKeys.Count > 0)
            {
                string key = typeDescriptor.PrimaryKeys.First().Column.Name;
                sb.AppendLine(c);
                sb.Append($"  PRIMARY KEY ({this.QuoteName(key)})");
            }

            sb.AppendLine();
            sb.Append(");");

            return sb.ToString();
        }
        string QuoteName(string name)
        {
            return string.Concat("\"", name.ToLower(), "\"");
        }

        string BuildColumnPart(PrimitivePropertyDescriptor propertyDescriptor)
        {
            string part = $"{this.QuoteName(propertyDescriptor.Column.Name)} {this.GetMappedDbTypeName(propertyDescriptor)}";

            if (!propertyDescriptor.IsNullable)
            {
                part += " NOT NULL";
            }
            else
            {
                part += " NULL";
            }

            return part;
        }

        string GetMappedDbTypeName(PrimitivePropertyDescriptor propertyDescriptor)
        {
            Type type = propertyDescriptor.PropertyType.GetUnderlyingType();
            if (type.IsEnum)
            {
                type = type.GetEnumUnderlyingType();
            }

            if (type == typeof(string))
            {
                int stringLength = propertyDescriptor.Column.Size ?? 4000;
                return $"varchar({stringLength})";
            }

            if (type == typeof(int))
            {
                if (propertyDescriptor.IsAutoIncrement)
                    return "serial4";

                return "int4";
            }

            if (type == typeof(byte))
            {
                return "int2";
            }

            if (type == typeof(Int16))
            {
                return "int2";
            }

            if (type == typeof(long))
            {
                if (propertyDescriptor.IsAutoIncrement)
                    return "serial8";

                return "int8";
            }

            if (type == typeof(float))
            {
                return "float4";
            }

            if (type == typeof(double))
            {
                return "float8";
            }

            if (type == typeof(decimal))
            {
                return "decimal(18,4)";
            }

            if (type == typeof(bool))
            {
                return "boolean";
            }

            if (type == typeof(DateTime))
            {
                return "timestamp";
            }

            if (type == typeof(Guid))
            {
                return "uuid";
            }

            throw new NotSupportedException(type.FullName);
        }

        public override void Method()
        {
            IQuery<Person> q = this.DbContext.Query<Person>();

            var space = new char[] { ' ' };

            DateTime startTime = DateTime.Now;
            DateTime endTime = DateTime.Now.AddDays(1);
            var result = q.Select(a => new
            {
                Id = a.Id,

                //CustomFunction = DbFunctions.MyFunction(a.Id), //自定义函数

                String_Length = (int?)a.Name.Length,//
                Substring = a.Name.Substring(0),//
                Substring1 = a.Name.Substring(1),//
                Substring1_2 = a.Name.Substring(1, 2),//
                ToLower = a.Name.ToLower(),//
                ToUpper = a.Name.ToUpper(),//
                IsNullOrEmpty = string.IsNullOrEmpty(a.Name),//
                Contains = (bool?)a.Name.Contains("s"),//
                Trim = a.Name.Trim(),//
                TrimStart = a.Name.TrimStart(space),//
                TrimEnd = a.Name.TrimEnd(space),//
                StartsWith = (bool?)a.Name.StartsWith("s"),//
                EndsWith = (bool?)a.Name.EndsWith("s"),//
                Replace = a.Name.Replace("l", "L"),

                DateTimeSubtract = endTime.Subtract(startTime),

                /* pgsql does not support Sql.DiffXX methods. */
                //DiffYears = Sql.DiffYears(startTime, endTime),//DATEDIFF(YEAR,@P_0,@P_1)
                //DiffMonths = Sql.DiffMonths(startTime, endTime),//DATEDIFF(MONTH,@P_0,@P_1)
                //DiffDays = Sql.DiffDays(startTime, endTime),//DATEDIFF(DAY,@P_0,@P_1)
                //DiffHours = Sql.DiffHours(startTime, endTime),//DATEDIFF(HOUR,@P_0,@P_1)
                //DiffMinutes = Sql.DiffMinutes(startTime, endTime),//DATEDIFF(MINUTE,@P_0,@P_1)
                //DiffSeconds = Sql.DiffSeconds(startTime, endTime),//DATEDIFF(SECOND,@P_0,@P_1)
                //DiffMilliseconds = Sql.DiffMilliseconds(startTime, endTime),//DATEDIFF(MILLISECOND,@P_0,@P_1)
                //DiffMicroseconds = Sql.DiffMicroseconds(startTime, endTime),//DATEDIFF(MICROSECOND,@P_0,@P_1)  Exception

                AddYears = startTime.AddYears(1),//
                AddMonths = startTime.AddMonths(1),//
                AddDays = startTime.AddDays(1),//
                AddHours = startTime.AddHours(1),//
                AddMinutes = startTime.AddMinutes(2),//
                AddSeconds = startTime.AddSeconds(120),//
                AddMilliseconds = startTime.AddMilliseconds(20000),//

                Now = DateTime.Now,//NOW()
                //UtcNow = DateTime.UtcNow,//GETUTCDATE()
                Today = DateTime.Today,//
                Date = DateTime.Now.Date,//
                Year = DateTime.Now.Year,//
                Month = DateTime.Now.Month,//
                Day = DateTime.Now.Day,//
                Hour = DateTime.Now.Hour,//
                Minute = DateTime.Now.Minute,//
                Second = DateTime.Now.Second,//
                Millisecond = DateTime.Now.Millisecond,//
                DayOfWeek = DateTime.Now.DayOfWeek,//

                Int_Parse = int.Parse("32"),//
                Int16_Parse = Int16.Parse("16"),//
                Long_Parse = long.Parse("64"),//
                Double_Parse = double.Parse("3.123"),//
                Float_Parse = float.Parse("4.123"),//
                Decimal_Parse = decimal.Parse("5.123"),//
                //Guid_Parse = Guid.Parse("D544BC4C-739E-4CD3-A3D3-7BF803FCE179"),//

                Bool_Parse = bool.Parse("1"),//
                DateTime_Parse = DateTime.Parse("1992-1-16"),//

                B = a.Age == null ? false : a.Age > 1, //三元表达式
                CaseWhen = Case.When(a.Id > 100).Then(1).Else(0) //case when
            }).ToList();

            ConsoleHelper.WriteLineAndReadKey();
        }

    }

}
