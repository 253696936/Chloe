﻿using Chloe.RDBMS;
using System.Reflection;

namespace Chloe.Oracle
{
    partial class SqlGenerator : SqlGeneratorBase
    {
        static Dictionary<string, IMethodHandler[]> InitMethodHandlers()
        {
            return PublicHelper.FindMethodHandlers(Assembly.GetExecutingAssembly());
        }
    }
}
