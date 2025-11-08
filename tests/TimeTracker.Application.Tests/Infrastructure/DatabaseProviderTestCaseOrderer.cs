using System;
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace TimeTracker.Application.Tests.Infrastructure;

public sealed class DatabaseProviderTestCaseOrderer : ITestCaseOrderer
{
    public IEnumerable<IXunitTestCase> OrderTestCases(IEnumerable<IXunitTestCase> testCases)
    {
        return testCases
            .OrderBy(GetOrder)
            .ThenBy(testCase => testCase.TestMethod.Method.Name, StringComparer.Ordinal)
            .ThenBy(testCase => testCase.DisplayName, StringComparer.Ordinal);
    }

    private static int GetOrder(ITestCase testCase)
    {
        var arguments = testCase?.TestMethodArguments;
        if (arguments is not null)
        {
            foreach (var argument in arguments)
            {
                if (argument is DatabaseProvider provider)
                {
                    if (provider == DatabaseProvider.Sqlite)
                    {
                        return 0;
                    }

                    if (provider == DatabaseProvider.PgSql)
                    {
                        return 1;
                    }
                }
            }
        }

        return 2;
    }

    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases) where TTestCase : ITestCase
    {
        return testCases.OrderBy(testCase => GetOrder(testCase));
    }
}
