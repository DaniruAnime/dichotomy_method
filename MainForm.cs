using System;
using System.Collections.Generic;
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
    private readonly ComboBox searchModeComboBox;
    private readonly ListBox rootsListBox;
    private readonly Label rootResultLabel;
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

      this.Text = "Поиск корней — метод половинного деления";
      this.StartPosition = FormStartPosition.CenterScreen;
      this.MinimumSize = new Size(860, 720);
      this.ClientSize = new Size(1040, 760);
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

      this.searchModeComboBox = new ComboBox {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        TabIndex = 4,
        Margin = new Padding(3, 2, 3, 3),
      };
      this.searchModeComboBox.Items.Add("Один корень — классическая бисекция");
      this.searchModeComboBox.Items.Add("Все обнаруженные корни — поиск на интервале");
      this.searchModeComboBox.SelectedIndex = 1;
      this.toolTip.SetToolTip(
          this.searchModeComboBox,
          "Классический режим требует разных знаков f(a) и f(b). " +
          "Расширенный режим проверяет участки внутри [a, b].");

      this.toolTip.SetToolTip(this.aTextBox, "Левая граница интервала поиска корней.");
      this.toolTip.SetToolTip(this.bTextBox, "Правая граница интервала поиска корней.");
      this.toolTip.SetToolTip(this.epsilonTextBox, "Требуемая точность по аргументу x.");
      this.toolTip.SetToolTip(
          this.functionTextBox,
          "Введите формулу от x, например: (x-3)^2 или sin(x) + x/2.");

      this.rootResultLabel = this.CreateResultLabel("Корни: —", Color.MidnightBlue);
      this.rootsListBox = new ListBox {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        HorizontalScrollbar = true,
        TabIndex = 5,
        Margin = new Padding(3, 1, 3, 3),
      };
      this.plotPanel = new PlotPanel {
        Dock = DockStyle.Fill,
        MinimumSize = new Size(300, 200),
      };
      this.searchModeComboBox.SelectedIndexChanged += this.SearchModeChanged;

      MenuStrip menu = this.CreateMenu();
      this.MainMenuStrip = menu;

      TableLayoutPanel root = new TableLayoutPanel {
        Dock = DockStyle.Fill,
        Padding = new Padding(12, 10, 12, 12),
        ColumnCount = 1,
        RowCount = 3,
        Margin = Padding.Empty,
      };
      root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0F));
      root.RowStyles.Add(new RowStyle(SizeType.Absolute, 214.0F));
      root.RowStyles.Add(new RowStyle(SizeType.Absolute, 220.0F));
      root.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0F));

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
      fileMenu.DropDownItems.Add(exitItem);

      ToolStripMenuItem calculationMenu = new ToolStripMenuItem("&Расчёт");
      ToolStripMenuItem calculateItem = new ToolStripMenuItem("&Рассчитать") {
        ShortcutKeys = Keys.F5,
        ShowShortcutKeys = true,
        ToolTipText = "Найти корни на интервале и построить график (F5).",
      };
      ToolStripMenuItem clearItem = new ToolStripMenuItem("&Очистить") {
        ShortcutKeys = Keys.Control | Keys.L,
        ShowShortcutKeys = true,
        ToolTipText = "Очистить исходные данные, результаты и график (Ctrl+L).",
      };

      calculateItem.Click += this.CalculateItemClick;
      clearItem.Click += this.ClearItemClick;

      calculationMenu.DropDownItems.Add(calculateItem);
      calculationMenu.DropDownItems.Add(new ToolStripSeparator());
      calculationMenu.DropDownItems.Add(clearItem);

      menu.Items.Add(fileMenu);
      menu.Items.Add(calculationMenu);
      return menu;
    }

    private GroupBox CreateInputGroup() {
      GroupBox group = new GroupBox {
        Text = "Исходные данные",
        Dock = DockStyle.Fill,
        Padding = new Padding(10, 8, 10, 10),
        Margin = new Padding(0, 0, 0, 8),
        TabStop = false,
      };

      TableLayoutPanel layout = new TableLayoutPanel {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 6,
        Margin = Padding.Empty,
      };
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
      layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0F));

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

      Label searchModeLabel = this.CreateFieldLabel("Режим поиска корней");
      layout.Controls.Add(searchModeLabel, 0, 4);
      layout.Controls.Add(this.searchModeComboBox, 1, 4);
      layout.SetColumnSpan(this.searchModeComboBox, 2);

      Label help = new Label {
        Dock = DockStyle.Fill,
        ForeColor = Color.DimGray,
        AutoEllipsis = true,
        TextAlign = ContentAlignment.TopLeft,
        Margin = new Padding(3, 3, 3, 0),
        Text =
              "Классическая бисекция: f(a) и f(b) разных знаков. Поиск всех корней: просмотр [a, b].\r\n" +
              "Формулы: x, pi, e, +, -, *, /, ^, sin, cos, tan, sqrt, ln и др.; углы в радианах.",
      };
      layout.Controls.Add(help, 0, 5);
      layout.SetColumnSpan(help, 3);
      this.toolTip.SetToolTip(help, help.Text);

      group.Controls.Add(layout);
      return group;
    }

    private GroupBox CreateResultGroup() {
      GroupBox group = new GroupBox {
        Text = "Результат",
        Dock = DockStyle.Fill,
        Padding = new Padding(10, 8, 10, 8),
        Margin = new Padding(0, 0, 0, 8),
        TabStop = false,
      };

      TableLayoutPanel layout = new TableLayoutPanel {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        RowCount = 2,
        Margin = Padding.Empty,
      };
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0F));
      layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26.0F));
      layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0F));

      layout.Controls.Add(this.rootResultLabel, 0, 0);
      layout.Controls.Add(this.rootsListBox, 0, 1);

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

      List<double> roots;
      string rootMessage;
      bool allSampledValuesZero;
      try {
        if (this.searchModeComboBox.SelectedIndex == 0) {
          roots = new List<double>();
          allSampledValuesZero = false;
          double singleRoot;
          if (!NumericalMethods.TryFindSingleRootByBisection(
              function, a, b, epsilon, out singleRoot, out rootMessage)) {
            this.rootResultLabel.Text = "Корень не найден";
            this.ShowCalculationError(rootMessage);
            return;
          }

          roots.Add(singleRoot);
        } else {
          NumericalMethods.FindRootsByBisection(
              function, a, b, epsilon, out roots, out rootMessage,
              out allSampledValuesZero);
        }
      }
      catch (ArgumentException exception) {
        this.ShowCalculationError(exception.Message);
        return;
      }
      catch (FormatException exception) {
        this.ShowFormulaError(exception.Message);
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
        this.ShowCalculationError("Неожиданная ошибка. Проверьте формулу и параметры расчёта.");
        return;
      }

      List<double> verifiedRoots = new List<double>();
      for (int index = 0; index < roots.Count; ++index) {
        double value;
        try {
          value = function(roots[index]);
          if (!IsFinite(value)) {
            continue;
          }
        }
        catch (ArithmeticException) {
          continue;
        }

        verifiedRoots.Add(roots[index]);
        this.rootsListBox.Items.Add(string.Format(
            CultureInfo.CurrentCulture,
            "{0}. x = {1:G12}; f(x) ≈ {2:G6}",
            verifiedRoots.Count,
            roots[index],
            value));
      }

      if (verifiedRoots.Count != roots.Count) {
        rootMessage += " Некоторые точки исключены после повторной проверки.";
      }

      this.rootResultLabel.Text = allSampledValuesZero
          ? "Особый случай: функция равна нулю во всех проверенных точках"
          : this.searchModeComboBox.SelectedIndex == 0
              ? "Корень найден"
              : string.Format(
                  CultureInfo.CurrentCulture,
                  "Найдено корней: {0}",
                  verifiedRoots.Count);

      // Keep diagnostic details available on hover without a separate notes block.
      this.toolTip.SetToolTip(this.rootResultLabel, rootMessage);
      this.plotPanel.SetPlot(function, expression, a, b, verifiedRoots);
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
      MessageBox.Show(
          this,
          message,
          "Проверьте исходные данные",
          MessageBoxButtons.OK,
          MessageBoxIcon.Warning);

      control.Focus();
      TextBox textBox = control as TextBox;
      if (textBox != null) {
        textBox.SelectAll();
      }
    }

    private void ShowFormulaError(string details) {
      MessageBox.Show(
          this,
          "Некорректная формула функции f(x). Проверьте скобки, операции и имена функций.\n\n" +
          details,
          "Ошибка формулы",
          MessageBoxButtons.OK,
          MessageBoxIcon.Warning);
      this.functionTextBox.Focus();
      this.functionTextBox.SelectAll();
    }

    private void ShowCalculationError(string details) {
      MessageBox.Show(
          this,
          "Не удалось найти корни с заданными параметрами.\n\n" + details,
          "Ошибка расчёта",
          MessageBoxButtons.OK,
          MessageBoxIcon.Error);
    }

    private void SearchModeChanged(object sender, EventArgs e) {
      this.ResetOutput();
    }

    private void ClearItemClick(object sender, EventArgs e) {
      this.aTextBox.Clear();
      this.bTextBox.Clear();
      this.epsilonTextBox.Clear();
      this.functionTextBox.Clear();
      this.ResetOutput();
      this.aTextBox.Focus();
    }

    private void ExitItemClick(object sender, EventArgs e) {
      this.Close();
    }

    private void ResetOutput() {
      this.rootResultLabel.Text = "Корни: —";
      this.rootsListBox.Items.Clear();
      this.toolTip.SetToolTip(this.rootResultLabel, null);
      this.plotPanel.ClearPlot();
    }
  }
}