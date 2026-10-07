using System;
using System.Globalization;

namespace Dihotomia {
  internal static class NumericalMethods {
    private const int MaximumIterations = 4096;
    private const int RootScanSegments = 1024;
    private const int RootConfirmationRefinement = 1024;
    private const int ValidationSampleSegments = 2048;

    public static void FindMinimumByDichotomy(
        Func<double, double> function,
        double a,
        double b,
        double epsilon,
        out double minimumX,
        out double minimumY,
        out int iterations) {
      ValidateArguments(function, a, b, epsilon);

      double delta = epsilon / 4.0;
      if (delta <= 0.0 || double.IsInfinity(delta)) {
        throw new ArgumentOutOfRangeException(
            "epsilon",
            "Точность e слишком мала или некорректна для вычислений типа double.");
      }

      iterations = 0;

      while ((b - a) > epsilon) {
        double oldA = a;
        double oldB = b;
        double middle = Midpoint(a, b);
        double x1 = middle - delta;
        double x2 = middle + delta;

        if (!(x1 > a && x1 < x2 && x2 < b)) {
          throw new InvalidOperationException(
              "Заданная точность слишком мала для выбранного интервала и типа double.");
        }

        double y1 = EvaluateFinite(function, x1);
        double y2 = EvaluateFinite(function, x2);

        if (y1 <= y2) {
          b = x2;
        } else {
          a = x1;
        }

        iterations++;

        if (iterations > MaximumIterations) {
          throw new InvalidOperationException("Превышено допустимое число итераций.");
        }

        if (a == oldA && b == oldB) {
          throw new InvalidOperationException(
              "Расчёт перестал сходиться из-за ограниченной точности типа double.");
        }
      }

      minimumX = Midpoint(a, b);
      minimumY = EvaluateFinite(function, minimumX);
    }

    public static bool TryFindRootByBisection(
        Func<double, double> function,
        double a,
        double b,
        double epsilon,
        out double root,
        out int iterations,
        out string message) {
      ValidateArguments(function, a, b, epsilon);

      root = 0.0;
      iterations = 0;
      message = string.Empty;

      double intervalLength = b - a;
      double left = 0.0;
      double leftValue = 0.0;
      bool hasLeft = false;
      bool detectedUndefinedPoint = false;
      bool detectedSuspiciousSignChange = false;
      string lastCandidateMessage = string.Empty;

      for (int index = 0; index <= RootScanSegments; index++) {
        double current = index == RootScanSegments
            ? b
            : a + (intervalLength * ((double)index / RootScanSegments));
        double currentValue;

        try {
          currentValue = EvaluateFinite(function, current);
        }
        catch (ArithmeticException) {
          detectedUndefinedPoint = true;
          hasLeft = false;
          continue;
        }

        if (currentValue == 0.0) {
          root = current;
          return true;
        }

        if (hasLeft && Math.Sign(leftValue) != Math.Sign(currentValue)) {
          double candidateRoot;
          int candidateIterations;
          string candidateMessage;

          if (TryBisectBracket(
              function,
              left,
              current,
              leftValue,
              currentValue,
              epsilon,
              out candidateRoot,
              out candidateIterations,
              out candidateMessage)) {
            root = candidateRoot;
            iterations = candidateIterations;
            return true;
          }

          detectedSuspiciousSignChange = true;
          lastCandidateMessage = candidateMessage;
        }

        left = current;
        leftValue = currentValue;
        hasLeft = true;
      }

      if (detectedSuspiciousSignChange) {
        message =
            "обнаружена смена знака, но она не подтверждена как корень. " +
            "Возможен разрыв функции на интервале. " + lastCandidateMessage;
      } else if (detectedUndefinedPoint) {
        message =
            "на интервале обнаружены точки, где функция не определена. " +
            "Метод половинного деления не может подтвердить корень.";
      } else {
        message =
            "на интервале не обнаружена смена знака. " +
            "Метод половинного деления не может подтвердить наличие корня.";
      }

      return false;
    }

    public static void ValidateFunctionOnInterval(Func<double, double> function, double a, double b) {
      if (function == null) {
        throw new ArgumentNullException("function");
      }

      if (!IsFinite(a) || !IsFinite(b) || a >= b) {
        throw new ArgumentException("Для проверки функции должен быть задан конечный интервал a < b.");
      }

      double intervalLength = b - a;
      if (!IsFinite(intervalLength) || intervalLength <= 0.0) {
        throw new ArgumentException("Интервал [a, b] слишком широк для вычислений типа double.");
      }

      for (int index = 0; index <= ValidationSampleSegments; index++) {
        double x = index == ValidationSampleSegments
            ? b
            : a + (intervalLength * ((double)index / ValidationSampleSegments));

        try {
          _ = EvaluateFinite(function, x);
        }
        catch (FormatException) {
          throw;
        }
        catch (ArithmeticException exception) {
          throw new ArithmeticException(
              "Функция не определена или не имеет конечного значения около x = " +
              x.ToString("G12", CultureInfo.CurrentCulture) + ". " + exception.Message,
              exception);
        }
      }
    }

    private static bool TryBisectBracket(
        Func<double, double> function,
        double a,
        double b,
        double fa,
        double fb,
        double epsilon,
        out double root,
        out int iterations,
        out string message) {
      root = 0.0;
      iterations = 0;
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

        iterations++;

        if (iterations > MaximumIterations) {
          throw new InvalidOperationException("Превышено допустимое число итераций.");
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