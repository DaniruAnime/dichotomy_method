using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Dihotomia {
  public sealed class MainForm : Form {
    private const int MaximumNumericInputLength = 64;

    private readonly TextBox aTextBox;
    private readonly TextBox bTextBox;
    private readonly TextBox epsilonTextBox;
    private readonly TextBox functionTextBox;
    private readonly Label minimumResultLabel;
    private readonly Label rootResultLabel;
    private readonly Label iterationLabel;
    private readonly PlotPanel plotPanel;
    private readonly ExpressionParser parser;
    private readonly ToolTip toolTip;

    public MainForm() {
      this.parser = new ExpressionParser();
      this.toolTip = new ToolTip {
        AutoPopDelay = 10000,
        InitialDelay = 500,
        ReshowDelay = 100,
        ShowAlways = true,
      };

      this.Text = "Метод дихотомии";
      this.StartPosition = FormStartPosition.CenterScreen;
      this.MinimumSize = new Size(860, 600);
      this.ClientSize = new Size(1040, 720);
      this.AutoScaleMode = AutoScaleMode.Font;
      this.Font = SystemFonts.MessageBoxFont;

      this.aTextBox = this.CreateNumericTextBox("-2", 0);
      this.bTextBox = this.CreateNumericTextBox("3", 1);
      this.epsilonTextBox = this.CreateNumericTextBox("0.001", 2);
      this.functionTextBox = new TextBox {
        Text = "(x-1)^2-1",
        Dock = DockStyle.Fill,
        MaxLength = ExpressionParser.MaximumExpressionLength + 1,
        TabIndex = 3,
        Margin = new Padding(3, 3, 3, 7),
      };

      this.toolTip.SetToolTip(this.aTextBox, "Левая граница интервала поиска минимума.");
      this.toolTip.SetToolTip(this.bTextBox, "Правая граница интервала поиска минимума.");
      this.toolTip.SetToolTip(this.epsilonTextBox, "Требуемая точность по аргументу x.");
      this.toolTip.SetToolTip(
          this.functionTextBox,
          "Введите формулу от x, например: (x-3)^2 или sin(x) + x/2.");

      this.minimumResultLabel = this.CreateResultLabel("Минимум: —", Color.MidnightBlue);
      this.rootResultLabel = this.CreateResultLabel("Корень: —", SystemColors.ControlText);
      this.iterationLabel = this.CreateResultLabel("Итерации: —", Color.DimGray);

      this.plotPanel = new PlotPanel {
        Dock = DockStyle.Fill,
        MinimumSize = new Size(300, 200),
      };

      MenuStrip menu = this.CreateMenu();
      this.MainMenuStrip = menu;

      TableLayoutPanel root = new TableLayoutPanel {
        Dock = DockStyle.Fill,
        Padding = new Padding(12, 10, 12, 12),
        ColumnCount = 1,
        RowCount = 3,
        Margin = Padding.Empty,
      };
      _ = root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0F));
      _ = root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      _ = root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      _ = root.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0F));

      GroupBox inputGroup = this.CreateInputGroup();
      GroupBox resultGroup = this.CreateResultGroup();
      GroupBox graphGroup = this.CreateGraphGroup();

      root.Controls.Add(inputGroup, 0, 0);
      root.Controls.Add(resultGroup, 0, 1);
      root.Controls.Add(graphGroup, 0, 2);

      this.Controls.Add(root);
      this.Controls.Add(menu);
      menu.BringToFront();

      this.ActiveControl = this.aTextBox;
    }

    protected override void Dispose(bool disposing) {
      if (disposing) {
        this.toolTip.Dispose();
      }

      base.Dispose(disposing);
    }

    private MenuStrip CreateMenu() {
      MenuStrip menu = new MenuStrip {
        Dock = DockStyle.Top,
        ShowItemToolTips = true,
      };

      ToolStripMenuItem fileMenu = new ToolStripMenuItem("&Файл");
      ToolStripMenuItem exitItem = new ToolStripMenuItem("&Выход");
      exitItem.Click += this.ExitItemClick;
      _ = fileMenu.DropDownItems.Add(exitItem);

      ToolStripMenuItem calculationMenu = new ToolStripMenuItem("&Расчёт");
      ToolStripMenuItem calculateItem = new ToolStripMenuItem("&Рассчитать") {
        ShortcutKeys = Keys.F5,
        ShowShortcutKeys = true,
        ToolTipText = "Выполнить расчёт минимума и построить график (F5).",
      };
      ToolStripMenuItem clearItem = new ToolStripMenuItem("&Очистить") {
        ShortcutKeys = Keys.Control | Keys.L,
        ShowShortcutKeys = true,
        ToolTipText = "Очистить исходные данные, результаты и график (Ctrl+L).",
      };

      calculateItem.Click += this.CalculateItemClick;
      clearItem.Click += this.ClearItemClick;

      _ = calculationMenu.DropDownItems.Add(calculateItem);
      _ = calculationMenu.DropDownItems.Add(new ToolStripSeparator());
      _ = calculationMenu.DropDownItems.Add(clearItem);

      _ = menu.Items.Add(fileMenu);
      _ = menu.Items.Add(calculationMenu);
      return menu;
    }

    private GroupBox CreateInputGroup() {
      GroupBox group = new GroupBox {
        Text = "Исходные данные",
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(10, 8, 10, 10),
        Margin = new Padding(0, 0, 0, 8),
        TabStop = false,
      };

      TableLayoutPanel layout = new TableLayoutPanel {
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnCount = 3,
        RowCount = 5,
        Margin = Padding.Empty,
      };
      _ = layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
      _ = layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
      _ = layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
      _ = layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      _ = layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      _ = layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      _ = layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      _ = layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

      layout.Controls.Add(this.CreateFieldLabel("a — левая граница интервала"), 0, 0);
      layout.Controls.Add(this.CreateFieldLabel("b — правая граница интервала"), 1, 0);
      layout.Controls.Add(this.CreateFieldLabel("e — требуемая точность"), 2, 0);

      this.aTextBox.Margin = new Padding(3, 3, 10, 8);
      this.bTextBox.Margin = new Padding(3, 3, 10, 8);
      this.epsilonTextBox.Margin = new Padding(3, 3, 3, 8);
      layout.Controls.Add(this.aTextBox, 0, 1);
      layout.Controls.Add(this.bTextBox, 1, 1);
      layout.Controls.Add(this.epsilonTextBox, 2, 1);

      Label functionLabel = this.CreateFieldLabel("f(x) — функция");
      functionLabel.Margin = new Padding(3, 2, 3, 0);
      layout.Controls.Add(functionLabel, 0, 2);
      layout.SetColumnSpan(functionLabel, 3);

      layout.Controls.Add(this.functionTextBox, 0, 3);
      layout.SetColumnSpan(this.functionTextBox, 3);

      Label help = new Label {
        AutoSize = true,
        Dock = DockStyle.Fill,
        ForeColor = Color.DimGray,
        Margin = new Padding(3, 1, 3, 1),
        Text =
              "Поддерживаются: +, -, *, /, ^, скобки, x, pi, e; функции sin, cos, tan/tg, " +
              "asin, acos, atan, sqrt, abs, exp, ln, log.\r\n" +
              "Условие метода: f(x) должна быть определена, конечна и унимодальна на всём " +
              "интервале [a, b].",
      };
      layout.Controls.Add(help, 0, 4);
      layout.SetColumnSpan(help, 3);

      group.Controls.Add(layout);
      return group;
    }

    private GroupBox CreateResultGroup() {
      GroupBox group = new GroupBox {
        Text = "Результат",
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(10, 8, 10, 8),
        Margin = new Padding(0, 0, 0, 8),
        TabStop = false,
      };

      TableLayoutPanel layout = new TableLayoutPanel {
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnCount = 1,
        RowCount = 3,
        Margin = Padding.Empty,
      };
      _ = layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0F));
      _ = layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24.0F));
      _ = layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24.0F));
      _ = layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24.0F));

      layout.Controls.Add(this.minimumResultLabel, 0, 0);
      layout.Controls.Add(this.rootResultLabel, 0, 1);
      layout.Controls.Add(this.iterationLabel, 0, 2);

      group.Controls.Add(layout);
      return group;
    }

    private GroupBox CreateGraphGroup() {
      GroupBox group = new GroupBox {
        Text = "График функции",
        Dock = DockStyle.Fill,
        Padding = new Padding(8, 8, 8, 8),
        Margin = Padding.Empty,
        TabStop = false,
      };
      group.Controls.Add(this.plotPanel);
      return group;
    }

    private TextBox CreateNumericTextBox(string text, int tabIndex) {
      return new TextBox {
        Text = text,
        Dock = DockStyle.Fill,
        MaxLength = MaximumNumericInputLength,
        TabIndex = tabIndex,
        TextAlign = HorizontalAlignment.Left,
      };
    }

    private Label CreateFieldLabel(string text) {
      return new Label {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(3, 1, 3, 0),
      };
    }

    private Label CreateResultLabel(string text, Color foreColor) {
      return new Label {
        Text = text,
        Dock = DockStyle.Fill,
        AutoEllipsis = true,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = foreColor,
        Margin = new Padding(3, 0, 3, 0),
      };
    }

    private void CalculateItemClick(object sender, EventArgs e) {
      this.ResetOutput();

      double a;
      double b;
      double epsilon;

      if (!this.TryReadInputs(out a, out b, out epsilon)) {
        return;
      }

      string expression = this.functionTextBox.Text.Trim();
      if (expression.Length == 0) {
        this.ShowInputError("Введите формулу функции f(x).", this.functionTextBox);
        return;
      }

      try {
        this.parser.ValidateSyntax(expression);
      }
      catch (FormatException exception) {
        this.ShowFormulaError(exception.Message);
        return;
      }

      Func<double, double> function = delegate (double x) {
        return this.parser.Evaluate(expression, x);
      };

      try {
        NumericalMethods.ValidateFunctionOnInterval(function, a, b);
      }
      catch (FormatException exception) {
        this.ShowFormulaError(exception.Message);
        return;
      }
      catch (ArithmeticException exception) {
        _ = MessageBox.Show(
            this,
            "Функция должна иметь конечные значения на всём интервале [a, b].\n\n" +
            exception.Message,
            "Недопустимая область определения",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        _ = this.functionTextBox.Focus();
        return;
      }
      catch (Exception) {
        _ = MessageBox.Show(
            this,
            "Не удалось проверить функцию. Проверьте формулу и границы интервала.",
            "Ошибка формулы",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        _ = this.functionTextBox.Focus();
        return;
      }

      double minimumX;
      double minimumY;
      int minimumIterations;

      try {
        NumericalMethods.FindMinimumByDichotomy(
            function,
            a,
            b,
            epsilon,
            out minimumX,
            out minimumY,
            out minimumIterations);
      }
      catch (ArgumentException exception) {
        this.ShowCalculationError(exception.Message);
        return;
      }
      catch (ArithmeticException exception) {
        this.ShowCalculationError(exception.Message);
        return;
      }
      catch (InvalidOperationException exception) {
        this.ShowCalculationError(exception.Message);
        return;
      }
      catch (Exception) {
        this.ShowCalculationError(
            "Произошла непредвиденная ошибка расчёта. Проверьте исходные данные и повторите попытку.");
        return;
      }

      bool hasRoot;
      double rootX;
      int rootIterations;
      string rootMessage;

      try {
        hasRoot = NumericalMethods.TryFindRootByBisection(
            function,
            a,
            b,
            epsilon,
            out rootX,
            out rootIterations,
            out rootMessage);
      }
      catch (Exception) {
        hasRoot = false;
        rootX = 0.0;
        rootIterations = 0;
        rootMessage = "поиск корня не выполнен из-за непредвиденной ошибки.";
      }

      this.minimumResultLabel.Text = string.Format(
          CultureInfo.CurrentCulture,
          "Минимум: x_min = {0:G12}; f(x_min) = {1:G12}",
          minimumX,
          minimumY);
      this.toolTip.SetToolTip(this.minimumResultLabel, this.minimumResultLabel.Text);

      if (hasRoot) {
        try {
          double rootValue = function(rootX);
          if (!IsFinite(rootValue)) {
            throw new ArithmeticException("Значение функции в найденной точке не является конечным.");
          }

          this.rootResultLabel.Text = string.Format(
              CultureInfo.CurrentCulture,
              "Корень: x = {0:G12}; f(x) ≈ {1:G6}",
              rootX,
              rootValue);
        }
        catch (ArithmeticException) {
          hasRoot = false;
          this.rootResultLabel.Text =
              "Корень: найденную точку не удалось подтвердить из-за недопустимого значения функции.";
        }
        catch (FormatException) {
          hasRoot = false;
          this.rootResultLabel.Text =
              "Корень: найденную точку не удалось подтвердить из-за ошибки формулы.";
        }
        catch (Exception) {
          hasRoot = false;
          this.rootResultLabel.Text = "Корень: найденную точку не удалось подтвердить.";
        }
      } else {
        this.rootResultLabel.Text = "Корень: " + rootMessage;
      }

      this.toolTip.SetToolTip(this.rootResultLabel, this.rootResultLabel.Text);

      this.iterationLabel.Text = string.Format(
          CultureInfo.CurrentCulture,
          "Итерации: минимум — {0}; корень — {1}",
          minimumIterations,
          hasRoot ? rootIterations.ToString(CultureInfo.CurrentCulture) : "—");
      this.toolTip.SetToolTip(this.iterationLabel, this.iterationLabel.Text);

      this.plotPanel.SetPlot(function, expression, a, b, minimumX,
          minimumY, true, rootX, hasRoot);
    }

    private bool TryReadInputs(out double a, out double b, out double epsilon) {
      a = 0.0;
      b = 0.0;
      epsilon = 0.0;

      if (!TryParseFlexible(this.aTextBox.Text, out a) || !IsFinite(a)) {
        this.ShowInputError(
            "Параметр a (левая граница) должен быть конечным вещественным числом.",
            this.aTextBox);
        return false;
      }

      if (!TryParseFlexible(this.bTextBox.Text, out b) || !IsFinite(b)) {
        this.ShowInputError(
            "Параметр b (правая граница) должен быть конечным вещественным числом.",
            this.bTextBox);
        return false;
      }

      if (!TryParseFlexible(this.epsilonTextBox.Text, out epsilon) || !IsFinite(epsilon)) {
        this.ShowInputError(
            "Параметр e (точность) должен быть конечным вещественным числом.",
            this.epsilonTextBox);
        return false;
      }

      if (a >= b) {
        this.ShowInputError(
            "Левая граница a должна быть меньше правой границы b.",
            this.aTextBox);
        return false;
      }

      double intervalLength = b - a;
      if (!IsFinite(intervalLength)) {
        this.ShowInputError(
            "Интервал [a, b] слишком широк для вычислений типа double.",
            this.bTextBox);
        return false;
      }

      if (epsilon <= 0.0) {
        this.ShowInputError(
            "Параметр e (точность) должен быть положительным числом.",
            this.epsilonTextBox);
        return false;
      }

      return true;
    }

    private static bool TryParseFlexible(string text, out double value) {
      value = 0.0;

      if (string.IsNullOrWhiteSpace(text)) {
        return false;
      }

      string trimmed = text.Trim();
      if (trimmed.Length > MaximumNumericInputLength) {
        return false;
      }

      string normalized = trimmed.Replace(',', '.');
      return double.TryParse(
          normalized,
          NumberStyles.Float,
          CultureInfo.InvariantCulture,
          out value);
    }

    private static bool IsFinite(double value) {
      return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private void ShowInputError(string message, Control control) {
      _ = MessageBox.Show(
          this,
          message,
          "Проверьте исходные данные",
          MessageBoxButtons.OK,
          MessageBoxIcon.Warning);

      _ = control.Focus();
      TextBox textBox = control as TextBox;
      if (textBox != null) {
        textBox.SelectAll();
      }
    }

    private void ShowFormulaError(string details) {
      _ = MessageBox.Show(
          this,
          "Некорректная формула функции f(x). Проверьте скобки, операции и имена функций.\n\n" +
          details,
          "Ошибка формулы",
          MessageBoxButtons.OK,
          MessageBoxIcon.Warning);
      _ = this.functionTextBox.Focus();
      this.functionTextBox.SelectAll();
    }

    private void ShowCalculationError(string details) {
      _ = MessageBox.Show(
          this,
          "Не удалось найти минимум с заданными параметрами.\n\n" + details,
          "Ошибка расчёта",
          MessageBoxButtons.OK,
          MessageBoxIcon.Error);
    }

    private void ClearItemClick(object sender, EventArgs e) {
      this.aTextBox.Clear();
      this.bTextBox.Clear();
      this.epsilonTextBox.Clear();
      this.functionTextBox.Clear();
      this.ResetOutput();
      _ = this.aTextBox.Focus();
    }

    private void ExitItemClick(object sender, EventArgs e) {
      this.Close();
    }

    private void ResetOutput() {
      this.minimumResultLabel.Text = "Минимум: —";
      this.rootResultLabel.Text = "Корень: —";
      this.iterationLabel.Text = "Итерации: —";
      this.toolTip.SetToolTip(this.minimumResultLabel, null);
      this.toolTip.SetToolTip(this.rootResultLabel, null);
      this.toolTip.SetToolTip(this.iterationLabel, null);
      this.plotPanel.ClearPlot();
    }
  }
}
