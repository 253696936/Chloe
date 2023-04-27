﻿using Chloe.DbExpressions;
using Chloe.InternalExtensions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;
using System.Collections;
using System.Reflection;

namespace Chloe.SqlServer.Odbc.MethodHandlers
{
    class Contains_Handler : Contains_HandlerBase
    {
        protected override void Method_String_Contains(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            exp.Object.Accept(generator);
            generator.SqlBuilder.Append(" LIKE '%' + ");
            exp.Arguments.First().Accept(generator);
            generator.SqlBuilder.Append(" + '%'");
        }
    }
}
