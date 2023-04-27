﻿using Chloe.DbExpressions;
using Chloe.InternalExtensions;
using Chloe.RDBMS;

namespace Chloe.RDBMS.MethodHandlers
{
    public class NextValueForSequence_HandlerBase : MethodHandlerBase
    {
        public override bool CanProcess(DbMethodCallExpression exp)
        {
            if (exp.Method.DeclaringType != PublicConstants.TypeOfSql)
                return false;

            return true;
        }
    }
}
