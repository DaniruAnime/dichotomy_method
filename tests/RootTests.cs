// <copyright file="RootTests.cs" company="Educational project">
// Copyright (c) Educational project. All rights reserved.
// </copyright>
namespace Dihotomia
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Console regression tests for root finding, without WinForms dependencies.
    /// </summary>
    internal static class RootTests
    {
        private static int failures;

        private static int Main()
        {
            Check("sin(x)", -10.0, 8.0, 0.001, new[]
            {
                -3.0 * Math.PI, -2.0 * Math.PI, -Math.PI, 0.0, Math.PI, 2.0 * Math.PI,
            });
            Check("cos(x)", -10.0, 8.0, 0.001, new[]
            {
                -2.5 * Math.PI, -1.5 * Math.PI, -0.5 * Math.PI,
                0.5 * Math.PI, 1.5 * Math.PI, 2.5 * Math.PI,
            });
            Check("tan(x)", -2.0, 3.0, 0.001, new[] { 0.0 });
            Check("sin(x)^2", -10.0, 8.0, 0.001, new[]
            {
                -3.0 * Math.PI, -2.0 * Math.PI, -Math.PI, 0.0, Math.PI, 2.0 * Math.PI,
            });
            Check("x^2-4", -3.0, 3.0, 0.001, new[] { -2.0, 2.0 });
            Check("(x-0.123)^2", -1.0, 1.0, 0.001, new[] { 0.123 });
            Check("x^2+1", -5.0, 5.0, 0.001, new double[0]);
            Check("x^2+0.00000001", -1.0, 1.0, 0.001, new double[0]);
            Check("1/(x-0.1)", 0.0, 1.0, 0.001, new double[0]);
            Check("(x+2)^2+5", -10.0, 5.0, 0.001, new double[0]);
            Check("2*x^2+4*x+1", -5.0, 5.0, 0.001, new[]
            {
                -1.0 - (Math.Sqrt(2.0) / 2.0),
                -1.0 + (Math.Sqrt(2.0) / 2.0),
            });
            CheckClassicalMode();
            CheckAllSampledZero();
            CheckInvalidArguments();
            Console.WriteLine(failures == 0 ? "Все тесты пройдены." : "Ошибок: " + failures);
            return failures == 0 ? 0 : 1;
        }

        private static void Check(string formula, double a, double b, double epsilon, double[] expected)
        {
            ExpressionParser parser = new ExpressionParser();
            parser.ValidateSyntax(formula);
            Func<double, double> function = x => parser.Evaluate(formula, x);
            List<double> actual;
            string message;
            bool allZero;
            NumericalMethods.FindRootsByBisection(
                function, a, b, epsilon, out actual, out message, out allZero);

            bool success = actual.Count == expected.Length && !allZero;
            if (success)
            {
                for (int index = 0; index < expected.Length; ++index)
                {
                    if (Math.Abs(actual[index] - expected[index]) > epsilon)
                    {
                        success = false;
                        break;
                    }
                }
            }

            Console.WriteLine((success ? "OK   " : "FAIL ") + formula +
                ": " + actual.Count + " корней");
            if (!success)
            {
                ++failures;
                Console.WriteLine("  " + message);
                foreach (double x in actual)
                {
                    Console.WriteLine("  x=" + x.ToString("G17"));
                }
            }
        }

        private static void CheckClassicalMode()
        {
            CheckClassical("sin(x)", -10.0, 8.0, 0.001, false, 0.0);
            CheckClassical("sin(x)", 3.0, 4.0, 0.001, true, Math.PI);
            CheckClassical("x-1", 0.0, 2.0, 0.001, true, 1.0);
            CheckClassical("x^2-4", -3.0, 3.0, 0.001, false, 0.0);
            CheckClassical("x^2-4", 2.0, 3.0, 0.001, true, 2.0);
            CheckClassical("x-1", 0.0, 1.0, 0.001, true, 1.0);
            CheckClassical("x-1", 1.0, 2.0, 0.001, true, 1.0);
            CheckClassical("x^2+1", -1.0, 1.0, 0.001, false, 0.0);
            CheckClassical("1/(x-0.1)", 0.0, 1.0, 0.001, false, 0.0);
            CheckClassical("tan(x)", -2.0, -1.0, 0.001, false, 0.0);
        }

        private static void CheckClassical(
            string formula,
            double a,
            double b,
            double epsilon,
            bool expectedFound,
            double expectedRoot)
        {
            ExpressionParser parser = new ExpressionParser();
            parser.ValidateSyntax(formula);
            Func<double, double> function = x => parser.Evaluate(formula, x);
            double root;
            string message;
            bool found = NumericalMethods.TryFindSingleRootByBisection(
                function, a, b, epsilon, out root, out message);
            bool success = found == expectedFound &&
                (!expectedFound || Math.Abs(root - expectedRoot) <= epsilon);
            if (formula == "sin(x)" && a == -10.0 && b == 8.0)
            {
                success = success && message.IndexOf("одинаковый", StringComparison.Ordinal) >= 0;
            }

            Console.WriteLine((success ? "OK   " : "FAIL ") +
                "Классическая бисекция: " + formula + " на [" + a + "; " + b + "]");
            if (!success)
            {
                ++failures;
                Console.WriteLine("  " + message);
            }
        }

        private static void CheckAllSampledZero()
        {
            List<double> roots;
            string message;
            bool allZero;
            NumericalMethods.FindRootsByBisection(
                x => 0.0, -1.0, 1.0, 0.001,
                out roots, out message, out allZero);
            bool success = allZero && roots.Count == 0;
            Console.WriteLine((success ? "OK   " : "FAIL ") + "f(x)=0: особый случай");
            if (!success)
            {
                ++failures;
            }
        }

        private static void CheckInvalidArguments()
        {
            bool rejected = false;
            try
            {
                List<double> roots;
                string message;
                bool allZero;
                NumericalMethods.FindRootsByBisection(
                    x => x, 3.0, -1.0, 0.001,
                    out roots, out message, out allZero);
            }
            catch (ArgumentException)
            {
                rejected = true;
            }

            Console.WriteLine((rejected ? "OK   " : "FAIL ") + "a >= b: отказ");
            if (!rejected)
            {
                ++failures;
            }

            rejected = false;
            try
            {
                List<double> roots;
                string message;
                bool allZero;
                NumericalMethods.FindRootsByBisection(
                    x => x, -1.0, 1.0, 0.0,
                    out roots, out message, out allZero);
            }
            catch (ArgumentException)
            {
                rejected = true;
            }

            Console.WriteLine((rejected ? "OK   " : "FAIL ") + "e <= 0: отказ");
            if (!rejected)
            {
                ++failures;
            }
        }
    }
}