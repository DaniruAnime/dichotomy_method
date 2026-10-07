using System;
using System.Collections.Generic;
using System.Globalization;

namespace Dihotomia {
  internal sealed class ExpressionParser {
    public const int MaximumExpressionLength = 2048;

    private const int MaximumRecursionDepth = 128;

    private static readonly Dictionary<string, Func<double, double>> Functions =
        new Dictionary<string, Func<double, double>>(StringComparer.OrdinalIgnoreCase)
        {
                { "sin", Math.Sin },
                { "cos", Math.Cos },
                { "tan", Math.Tan },
                { "tg", Math.Tan },
                { "asin", Math.Asin },
                { "acos", Math.Acos },
                { "atan", Math.Atan },
                { "sqrt", Math.Sqrt },
                { "abs", Math.Abs },
                { "exp", Math.Exp },
                { "ln", Math.Log },
                { "log", Math.Log10 },
                { "log10", Math.Log10 },
        };

    private string text = string.Empty;
    private int position;
    private int recursionDepth;
    private double x;
    private bool syntaxValidationOnly;

    public double Evaluate(string expression, double argument) {
      this.Initialize(expression, argument, false);

      double value = this.ParseExpression();
      this.EnsureEndOfExpression();

      return this.EnsureFinite(value, "Значение функции не является конечным числом.");
    }

    public void ValidateSyntax(string expression) {
      this.Initialize(expression, 0.0, true);
      _ = this.ParseExpression();
      this.EnsureEndOfExpression();
    }

    private void Initialize(string expression, double argument, bool syntaxOnly) {
      if (string.IsNullOrWhiteSpace(expression)) {
        throw new FormatException("Формула функции не задана.");
      }

      if (expression.Length > MaximumExpressionLength) {
        throw new FormatException(
            "Формула слишком длинная. Максимальная длина — " +
            MaximumExpressionLength.ToString(CultureInfo.InvariantCulture) + " символов.");
      }

      this.text = expression;
      this.position = 0;
      this.recursionDepth = 0;
      this.x = argument;
      this.syntaxValidationOnly = syntaxOnly;
    }

    private void EnsureEndOfExpression() {
      this.SkipSpaces();

      if (this.position != this.text.Length) {
        throw new FormatException(
            "Неожиданный символ '" + this.text[this.position] + "' в позиции " +
            (this.position + 1).ToString(CultureInfo.CurrentCulture) + ".");
      }
    }

    private double EnsureFinite(double value, string message) {
      if (double.IsNaN(value) || double.IsInfinity(value)) {
        if (this.syntaxValidationOnly) {
          return 0.0;
        }

        throw new ArithmeticException(message);
      }

      return value;
    }

    private double ParseExpression() {
      double value = this.ParseTerm();

      while (true) {
        this.SkipSpaces();

        if (this.Match('+')) {
          value = this.EnsureFinite(value + this.ParseTerm(), "Переполнение при сложении.");
        } else if (this.Match('-')) {
          value = this.EnsureFinite(value - this.ParseTerm(), "Переполнение при вычитании.");
        } else {
          return value;
        }
      }
    }

    private double ParseTerm() {
      double value = this.ParseUnary();

      while (true) {
        this.SkipSpaces();

        if (this.Match('*')) {
          value = this.EnsureFinite(value * this.ParseUnary(), "Переполнение при умножении.");
        } else if (this.Match('/')) {
          double divisor = this.ParseUnary();
          if (divisor == 0.0) {
            if (this.syntaxValidationOnly) {
              value = 0.0;
              continue;
            }

            throw new DivideByZeroException("Деление на ноль при вычислении функции.");
          }

          value = this.EnsureFinite(value / divisor, "Переполнение при делении.");
        } else {
          return value;
        }
      }
    }

    private double ParseUnary() {
      this.EnterRecursion();

      try {
        this.SkipSpaces();

        if (this.Match('+')) {
          return this.ParseUnary();
        }

        if (this.Match('-')) {
          return this.EnsureFinite(-this.ParseUnary(), "Недопустимое значение после унарного минуса.");
        }

        return this.ParsePower();
      }
      finally {
        this.ExitRecursion();
      }
    }

    private double ParsePower() {
      double left = this.ParsePrimary();
      this.SkipSpaces();

      if (this.Match('^')) {
        double right = this.ParseUnary();
        left = this.EnsureFinite(Math.Pow(left, right), "Недопустимый результат возведения в степень.");
      }

      return left;
    }

    private double ParsePrimary() {
      this.SkipSpaces();

      if (this.Match('(')) {
        double value = this.ParseExpression();
        this.SkipSpaces();

        if (!this.Match(')')) {
          throw new FormatException("Ожидалась закрывающая скобка ')'.");
        }

        return value;
      }

      if (this.position >= this.text.Length) {
        throw new FormatException("Ожидалось число, переменная или выражение в скобках.");
      }

      char current = this.text[this.position];
      if (char.IsDigit(current) || current == '.' || current == ',') {
        return this.ParseNumber();
      }

      if (char.IsLetter(current)) {
        string name = this.ParseIdentifier();

        if (string.Equals(name, "x", StringComparison.OrdinalIgnoreCase)) {
          return this.x;
        }

        if (string.Equals(name, "pi", StringComparison.OrdinalIgnoreCase)) {
          return Math.PI;
        }

        if (string.Equals(name, "e", StringComparison.OrdinalIgnoreCase)) {
          return Math.E;
        }

        Func<double, double> function;
        if (!Functions.TryGetValue(name, out function)) {
          throw new FormatException("Неизвестная функция или идентификатор: " + name + ".");
        }

        this.SkipSpaces();
        if (!this.Match('(')) {
          throw new FormatException("После функции " + name + " ожидается '('.");
        }

        double functionArgument = this.ParseExpression();
        this.SkipSpaces();

        if (!this.Match(')')) {
          throw new FormatException("После аргумента функции " + name + " ожидается ')'.");
        }

        return this.EnsureFinite(
            function(functionArgument),
            "Функция " + name + " получила недопустимый аргумент.");
      }

      throw new FormatException(
          "Неожиданный символ '" + current + "' в позиции " +
          (this.position + 1).ToString(CultureInfo.CurrentCulture) + ".");
    }

    private double ParseNumber() {
      int start = this.position;
      bool hasSeparator = false;
      bool hasExponent = false;

      while (this.position < this.text.Length) {
        char current = this.text[this.position];

        if (char.IsDigit(current)) {
          this.position++;
          continue;
        }

        if ((current == '.' || current == ',') && !hasSeparator && !hasExponent) {
          hasSeparator = true;
          this.position++;
          continue;
        }

        if ((current == 'e' || current == 'E') && !hasExponent) {
          hasExponent = true;
          this.position++;

          if (this.position < this.text.Length &&
              (this.text[this.position] == '+' || this.text[this.position] == '-')) {
            this.position++;
          }

          continue;
        }

        break;
      }

      string token = this.text.Substring(start, this.position - start).Replace(',', '.');
      double value;

      if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
          double.IsNaN(value) ||
          double.IsInfinity(value)) {
        throw new FormatException("Некорректное число: " + token + ".");
      }

      return value;
    }

    private string ParseIdentifier() {
      int start = this.position;

      while (this.position < this.text.Length &&
             (char.IsLetterOrDigit(this.text[this.position]) || this.text[this.position] == '_')) {
        this.position++;
      }

      return this.text.Substring(start, this.position - start);
    }

    private void SkipSpaces() {
      while (this.position < this.text.Length && char.IsWhiteSpace(this.text[this.position])) {
        this.position++;
      }
    }

    private bool Match(char expected) {
      if (this.position < this.text.Length && this.text[this.position] == expected) {
        this.position++;
        return true;
      }

      return false;
    }

    private void EnterRecursion() {
      this.recursionDepth++;

      if (this.recursionDepth > MaximumRecursionDepth) {
        throw new FormatException("Формула содержит слишком глубокую вложенность.");
      }
    }

    private void ExitRecursion() {
      this.recursionDepth--;
    }
  }
}