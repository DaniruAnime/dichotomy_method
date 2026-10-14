using System;
using System.Collections.Generic;
using System.Globalization;

namespace Dihotomia {
  internal static class NumericalMethods {
    private const int MaximumRefinementSteps = 4096;
    private const int RootScanSegments = 2048;
    private const int RootConfirmationRefinement = 1024;
    private const int MaximumRoots = 256;

    public static bool TryFindSingleRootByBisection(
        Func<double, double> function,
        double a,
        double b,
        double epsilon,
        out double root,
        out string message) {
      ValidateArguments(function, a, b, epsilon);
      root = 0.0;
      message = string.Empty;

      double fa;
      double fb;
      try {
        fa = EvaluateFinite(function, a);
        fb = EvaluateFinite(function, b);
      }
      catch (ArithmeticException) {
        message = "Функция должна быть определена и иметь конечные значения на концах [a, b].";
        return false;
      }

      if (fa == 0.0) {
        root = a;
        message = "Корень найден на левой границе интервала.";
        return true;
      }

      if (fb == 0.0) {
        root = b;
        message = "Корень найден на правой границе интервала.";
        return true;
      }

      if (Math.Sign(fa) == Math.Sign(fb)) {
        message = string.Format(
            CultureInfo.CurrentCulture,
            "На концах интервала функция имеет одинаковый знак: f(a) = {0:G8}, f(b) = {1:G8}. " +
            "Для классического метода половинного деления нужны разные знаки f(a) и f(b). " +
            "Выберите режим поиска всех корней, чтобы проверить внутренние участки интервала.",
            fa,
            fb);
        return false;
      }

      if (!TryBisectBracket(function, a, b, fa, fb, epsilon, out root, out message)) {
        message = "Не удалось подтвердить корень классической бисекцией: " + message;
        return false;
      }

      message = "Корень уточнён методом половинного деления.";
      return true;
    }

    public static void FindRootsByBisection(
        Func<double, double> function,
        double a,
        double b,
        double epsilon,
        out List<double> roots,
        out string message,
        out bool allSampledValuesZero) {
      ValidateArguments(function, a, b, epsilon);
      roots = new List<double>();
      allSampledValuesZero = false;
      bool hasUndefinedSamples = false;
      bool hasUnconfirmedChanges = false;
      bool reachedLimit = false;
      bool hasTangentCandidates = false;
      double intervalLength = b - a;
      double scanStep = intervalLength / RootScanSegments;
      double[] arguments = new double[RootScanSegments + 1];
      double[] values = new double[RootScanSegments + 1];
      bool[] valid = new bool[RootScanSegments + 1];

      for (int index = 0; index <= RootScanSegments; ++index) {
        double x = index == RootScanSegments
            ? b
            : a + (intervalLength * ((double)index / RootScanSegments));
        arguments[index] = x;
        try {
          values[index] = EvaluateFinite(function, x);
          valid[index] = true;
        }
        catch (ArithmeticException) {
          hasUndefinedSamples = true;
        }
      }

      allSampledValuesZero = true;
      for (int index = 0; index <= RootScanSegments; ++index) {
        if (!valid[index] || values[index] != 0.0) {
          allSampledValuesZero = false;
          break;
        }
      }

      if (allSampledValuesZero) {
        message = "Функция равна нулю во всех проверенных точках. " +
            "Возможно, на интервале бесконечно много корней; конечный список не строится.";
        return;
      }

      for (int index = 0; index <= RootScanSegments; ++index) {
        if (!valid[index]) {
          continue;
        }

        if (values[index] == 0.0) {
          AddDistinctRoot(roots, arguments[index], epsilon, scanStep);
          if (roots.Count >= MaximumRoots) {
            reachedLimit = true;
            break;
          }
        }

        if (index == 0 || !valid[index - 1] ||
            values[index - 1] == 0.0 || values[index] == 0.0 ||
            Math.Sign(values[index - 1]) == Math.Sign(values[index])) {
          continue;
        }

        double candidate;
        string candidateMessage;
        if (TryBisectBracket(
            function,
            arguments[index - 1],
            arguments[index],
            values[index - 1],
            values[index],
            epsilon,
            out candidate,
            out candidateMessage)) {
          AddDistinctRoot(roots, candidate, epsilon, scanStep);
        } else {
          hasUnconfirmedChanges = true;
        }

        if (roots.Count >= MaximumRoots) {
          reachedLimit = true;
          break;
        }
      }

      if (!reachedLimit) {
        for (int index = 1; index < RootScanSegments; ++index) {
          if (!valid[index - 1] || !valid[index] || !valid[index + 1] ||
              values[index] == 0.0 ||
              Math.Sign(values[index - 1]) != Math.Sign(values[index]) ||
              Math.Sign(values[index]) != Math.Sign(values[index + 1])) {
            continue;
          }

          double leftAbsolute = Math.Abs(values[index - 1]);
          double middleAbsolute = Math.Abs(values[index]);
          double rightAbsolute = Math.Abs(values[index + 1]);
          if (middleAbsolute >= leftAbsolute || middleAbsolute >= rightAbsolute) {
            continue;
          }

          double candidate;
          if (TryRefineTouchingRoot(
              function,
              arguments[index - 1],
              arguments[index + 1],
              Math.Min(leftAbsolute, rightAbsolute),
              out candidate)) {
            AddDistinctRoot(roots, candidate, epsilon, scanStep);
            hasTangentCandidates = true;
          }

          if (roots.Count >= MaximumRoots) {
            reachedLimit = true;
            break;
          }
        }
      }

      roots.Sort();
      message = roots.Count == 0
          ? "Корни не обнаружены при выбранной точности и частоте поиска."
          : "Найдено корней: " + roots.Count.ToString(CultureInfo.CurrentCulture) + ".";

      if (hasUndefinedSamples || hasUnconfirmedChanges) {
        message += " На интервале возможны разрывы: сомнительные участки не считались корнями.";
      }

      if (hasTangentCandidates) {
        message += " Возможные касательные корни оценены эвристически; это не строгое доказательство.";
      }

      if (reachedLimit) {
        message += " Достигнут лимит " + MaximumRoots.ToString(CultureInfo.CurrentCulture) +
            " точек; список может быть неполным.";
      }

      message += " Поиск по конечной сетке не гарантирует нахождение всех корней.";
    }

    private static void AddDistinctRoot(List<double> roots, double candidate, double epsilon, double scanStep) {
      double duplicateDistance = Math.Min(epsilon, scanStep) * 0.5;
      for (int index = 0; index < roots.Count; ++index) {
        if (Math.Abs(roots[index] - candidate) <= duplicateDistance) {
          return;
        }
      }

      roots.Add(candidate);
    }

    private static bool TryRefineTouchingRoot(
        Func<double, double> function,
        double left,
        double right,
        double referenceAbsoluteValue,
        out double root) {
      root = 0.0;
      if (!(referenceAbsoluteValue > 0.0)) {
        return false;
      }

      // Поиск методом золотого сечения используется только для обнаружения корней, в которых график касается оси
      // Корни, в которых происходит смена знака, всегда уточняются методом бисекции
      const double part = 0.3819660112501051;
      double x1 = left + ((right - left) * part);
      double x2 = right - ((right - left) * part);
      double f1;
      double f2;
      try {
        f1 = Math.Abs(EvaluateFinite(function, x1));
        f2 = Math.Abs(EvaluateFinite(function, x2));
      }
      catch (ArithmeticException) {
        return false;
      }

      for (int index = 0; index < 90; ++index) {
        if (!(left < x1 && x1 < x2 && x2 < right)) {
          break;
        }

        if (f1 <= f2) {
          right = x2;
          x2 = x1;
          f2 = f1;
          x1 = left + ((right - left) * part);
          try {
            f1 = Math.Abs(EvaluateFinite(function, x1));
          }
          catch (ArithmeticException) {
            return false;
          }
        } else {
          left = x1;
          x1 = x2;
          f1 = f2;
          x2 = right - ((right - left) * part);
          try {
            f2 = Math.Abs(EvaluateFinite(function, x2));
          }
          catch (ArithmeticException) {
            return false;
          }
        }

      }

      double bestResidual = Math.Min(f1, f2);
      if (bestResidual / referenceAbsoluteValue > 1E-10) {
        return false;
      }

      root = f1 <= f2 ? x1 : x2;
      return true;
    }

    private static bool TryBisectBracket(
        Func<double, double> function,
        double a,
        double b,
        double fa,
        double fb,
        double epsilon,
        out double root,
        out string message) {
      root = 0.0;
      int refinementSteps = 0;
      message = string.Empty;

      double initialWidth = b - a;
      double confirmationWidth = initialWidth / RootConfirmationRefinement;
      if (confirmationWidth <= 0.0 || double.IsNaN(confirmationWidth)) {
        confirmationWidth = epsilon;
      }

      double targetWidth = Math.Min(epsilon, confirmationWidth);
      double initialBestAbsoluteValue = Math.Min(Math.Abs(fa), Math.Abs(fb));
      double bestX = Math.Abs(fa) <= Math.Abs(fb) ? a : b;
      double bestAbsoluteValue = initialBestAbsoluteValue;
      bool precisionLimitReached = false;

      while ((b - a) > targetWidth) {
        double middle = Midpoint(a, b);

        if (middle == a || middle == b) {
          precisionLimitReached = true;
          break;
        }

        double fm;
        try {
          fm = EvaluateFinite(function, middle);
        }
        catch (ArithmeticException) {
          message = "смена знака связана с точкой, где функция не определена.";
          return false;
        }

        double absoluteMiddleValue = Math.Abs(fm);
        if (absoluteMiddleValue < bestAbsoluteValue) {
          bestAbsoluteValue = absoluteMiddleValue;
          bestX = middle;
        }

        if (fm == 0.0) {
          root = middle;
          return true;
        }

        if (Math.Sign(fa) != Math.Sign(fm)) {
          b = middle;
        } else {
          a = middle;
          fa = fm;
        }

        ++refinementSteps;

        if (refinementSteps > MaximumRefinementSteps) {
          throw new InvalidOperationException(
              "Не удалось завершить уточнение корня. Измените интервал или точность.");
        }
      }

      double midpoint = Midpoint(a, b);
      if (midpoint != a && midpoint != b) {
        double midpointValue;
        try {
          midpointValue = EvaluateFinite(function, midpoint);
        }
        catch (ArithmeticException) {
          message = "смена знака связана с точкой, где функция не определена.";
          return false;
        }

        double absoluteMidpointValue = Math.Abs(midpointValue);
        if (absoluteMidpointValue < bestAbsoluteValue) {
          bestAbsoluteValue = absoluteMidpointValue;
          bestX = midpoint;
        }

        if (midpointValue == 0.0) {
          root = midpoint;
          return true;
        }
      }

      if (precisionLimitReached && (b - a) > epsilon) {
        message = "заданная точность меньше разрешающей способности double на этом интервале.";
        return false;
      }

      if (initialBestAbsoluteValue > 0.0 &&
          bestAbsoluteValue / initialBestAbsoluteValue <= 0.25) {
        root = bestX;
        return true;
      }

      message = precisionLimitReached
          ? "не удалось подтвердить корень из-за ограниченной точности типа double."
          : "значения функции не стремятся к нулю при сужении интервала; вероятен разрыв.";
      return false;
    }

    private static void ValidateArguments(
        Func<double, double> function,
        double a,
        double b,
        double epsilon) {
      if (function == null) {
        throw new ArgumentNullException("function");
      }

      if (!IsFinite(a) || !IsFinite(b) || !IsFinite(epsilon)) {
        throw new ArgumentException("a, b и e должны быть конечными числами.");
      }

      if (a >= b) {
        throw new ArgumentException("Должно выполняться условие a < b.");
      }

      double intervalLength = b - a;
      if (!IsFinite(intervalLength) || intervalLength <= 0.0) {
        throw new ArgumentException("Интервал [a, b] слишком широк для вычислений типа double.");
      }

      if (epsilon <= 0.0) {
        throw new ArgumentOutOfRangeException(
            "epsilon",
            "Точность e должна быть больше нуля.");
      }
    }

    private static double EvaluateFinite(Func<double, double> function, double argument) {
      double value = function(argument);

      if (!IsFinite(value)) {
        throw new ArithmeticException("Получено недопустимое значение функции.");
      }

      return value;
    }

    private static bool IsFinite(double value) {
      return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static double Midpoint(double a, double b) {
      return a + ((b - a) / 2.0);
    }
  }
}