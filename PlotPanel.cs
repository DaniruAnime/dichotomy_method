using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace Dihotomia {
  internal sealed class PlotPanel : Panel {
    private const int Samples = 2048;
    private const int LeftMargin = 72;
    private const int TopMargin = 38;
    private const int RightMargin = 24;
    private const int BottomMargin = 56;

    private Func<double, double> function;
    private string functionDescription;
    private double a;
    private double b;
    private IList<double> roots;
    private double[] xValues;
    private double[] yValues;
    private bool[] validValues;
    private double sampledYMin;
    private double sampledYMax;
    private bool hasDrawableSamples;
    private bool hasSuspectedDiscontinuity;
    private double visibleYMin;
    private double visibleYMax;

    public PlotPanel() {
      this.DoubleBuffered = true;
      this.BackColor = Color.White;
      this.BorderStyle = BorderStyle.FixedSingle;
      this.ResizeRedraw = true;
      this.TabStop = false;
    }

    public bool HasSuspectedDiscontinuity {
      get { return this.hasSuspectedDiscontinuity; }
    }

    public void SetPlot(
        Func<double, double> functionToPlot,
        string description,
        double leftBound,
        double rightBound,
        IList<double> foundRoots) {
      this.function = functionToPlot;
      this.functionDescription = description ?? string.Empty;
      this.a = leftBound;
      this.b = rightBound;
      this.roots = foundRoots == null
          ? new List<double>()
          : new List<double>(foundRoots);
      this.BuildSampleCache();
      this.Invalidate();
    }

    public void ClearPlot() {
      this.function = null;
      this.functionDescription = string.Empty;
      this.roots = null;
      this.xValues = null;
      this.yValues = null;
      this.validValues = null;
      this.hasDrawableSamples = false;
      this.hasSuspectedDiscontinuity = false;
      this.visibleYMin = 0.0;
      this.visibleYMax = 0.0;
      this.sampledYMin = 0.0;
      this.sampledYMax = 0.0;
      this.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e) {
      base.OnPaint(e);

      Graphics graphics = e.Graphics;
      graphics.SmoothingMode = SmoothingMode.AntiAlias;

      Rectangle area = new Rectangle(
          LeftMargin,
          TopMargin,
          Math.Max(10, this.ClientSize.Width - LeftMargin - RightMargin),
          Math.Max(10, this.ClientSize.Height - TopMargin - BottomMargin));

      using (Pen borderPen = new Pen(Color.Gainsboro)) {
        graphics.DrawRectangle(borderPen, area);
      }

      if (this.function == null || this.b <= this.a || area.Width < 40 || area.Height < 40) {
        this.DrawStatusText(
            graphics,
            "После расчёта здесь появится график функции и обнаруженные корни.",
            Color.DimGray);
        return;
      }

      this.DrawFunctionCaption(graphics, area);

      double intervalLength = this.b - this.a;
      if (!IsFinite(intervalLength) || intervalLength <= 0.0) {
        this.DrawStatusText(graphics, "Некорректный диапазон графика.", Color.DarkRed);
        return;
      }

      if (!this.hasDrawableSamples) {
        this.DrawStatusText(
            graphics,
            "На интервале не удалось получить конечные значения функции.",
            Color.DarkRed);
        return;
      }

      double yMin = this.visibleYMin;
      double yMax = this.visibleYMax;

      if (this.roots != null && this.roots.Count > 0) {
        yMin = Math.Min(yMin, 0.0);
        yMax = Math.Max(yMax, 0.0);
      }

      double yScale;
      double normalizedMin;
      double normalizedMax;
      if (!TryPrepareNormalizedRange(yMin, yMax, out yScale, out normalizedMin, out normalizedMax)) {
        this.DrawStatusText(graphics, "Не удалось определить масштаб графика.", Color.DarkRed);
        return;
      }

      Func<double, float> mapX = delegate (double x) {
        double ratio = (x - this.a) / intervalLength;
        return ToFiniteFloat(area.Left + (ratio * area.Width));
      };

      Func<double, float> mapY = delegate (double y) {
        if (!IsFinite(y)) {
          return float.NaN;
        }

        double normalizedY = y / yScale;
        double ratio = (normalizedY - normalizedMin) / (normalizedMax - normalizedMin);
        return ToFiniteFloat(area.Bottom - (ratio * area.Height));
      };

      double displayYMin = DenormalizeForLabel(normalizedMin, yScale, yMin);
      double displayYMax = DenormalizeForLabel(normalizedMax, yScale, yMax);

      this.DrawGrid(graphics, area);
      this.DrawAxes(graphics, area, normalizedMin, normalizedMax, mapX, mapY);
      this.DrawCurve(graphics, area, mapX, mapY);
      this.DrawRoots(graphics, area, normalizedMin, normalizedMax, mapX, mapY);
      this.DrawLabels(graphics, area, displayYMin, displayYMax);
    }

    private static bool IsDrawableValue(double value) {
      return IsFinite(value);
    }

    private static bool IsFinite(double value) {
      return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool IsFinite(float value) {
      return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static float ToFiniteFloat(double value) {
      if (!IsFinite(value) || value > float.MaxValue || value < -float.MaxValue) {
        return float.NaN;
      }

      return (float)value;
    }

    private static bool TryPrepareNormalizedRange(
        double yMin,
        double yMax,
        out double yScale,
        out double normalizedMin,
        out double normalizedMax) {
      yScale = Math.Max(Math.Abs(yMin), Math.Abs(yMax));
      if (!IsFinite(yScale)) {
        normalizedMin = 0.0;
        normalizedMax = 0.0;
        return false;
      }

      if (yScale < 1.0) {
        yScale = 1.0;
      }

      normalizedMin = yMin / yScale;
      normalizedMax = yMax / yScale;

      if (!IsFinite(normalizedMin) || !IsFinite(normalizedMax)) {
        return false;
      }

      double range = normalizedMax - normalizedMin;
      if (!(range > 0.0) || range < 1E-12) {
        double center = normalizedMin + ((normalizedMax - normalizedMin) / 2.0);
        double padding = Math.Max(0.1, Math.Abs(center) * 0.1);
        normalizedMin = center - padding;
        normalizedMax = center + padding;
      } else {
        double padding = range * 0.08;
        normalizedMin -= padding;
        normalizedMax += padding;
      }

      return IsFinite(normalizedMin) &&
             IsFinite(normalizedMax) &&
             normalizedMax > normalizedMin;
    }

    private static double DenormalizeForLabel(double normalized, double scale, double fallback) {
      double value = normalized * scale;
      return IsFinite(value) ? value : fallback;
    }

    private void BuildSampleCache() {
      this.xValues = new double[Samples + 1];
      this.yValues = new double[Samples + 1];
      this.validValues = new bool[Samples + 1];
      this.hasDrawableSamples = false;
      this.sampledYMin = double.PositiveInfinity;
      this.sampledYMax = double.NegativeInfinity;
      this.visibleYMin = 0.0;
      this.visibleYMax = 0.0;
      this.hasSuspectedDiscontinuity = false;

      if (this.function == null || this.b <= this.a) {
        return;
      }

      double intervalLength = this.b - this.a;
      if (!IsFinite(intervalLength) || intervalLength <= 0.0) {
        return;
      }

      for (int index = 0; index <= Samples; ++index) {
        double x = index == Samples
            ? this.b
            : this.a + (intervalLength * ((double)index / Samples));
        this.xValues[index] = x;

        try {
          double y = this.function(x);
          if (!IsDrawableValue(y)) {
            continue;
          }

          this.yValues[index] = y;
          this.validValues[index] = true;
          this.sampledYMin = Math.Min(this.sampledYMin, y);
          this.sampledYMax = Math.Max(this.sampledYMax, y);
          this.hasDrawableSamples = true;
        }
        catch (ArithmeticException) {
          this.validValues[index] = false;
        }
        catch (FormatException) {
          this.validValues[index] = false;
        }
      }

      this.PrepareVisibleRange();
    }

    private void PrepareVisibleRange() {
      if (!this.hasDrawableSamples) {
        return;
      }

      this.visibleYMin = this.sampledYMin;
      this.visibleYMax = this.sampledYMax;

      int validCount = 0;
      for (int index = 0; index < this.validValues.Length; ++index) {
        if (this.validValues[index]) {
          ++validCount;
        }
      }

      if (validCount < 16) {
        return;
      }

      double[] sorted = new double[validCount];
      int next = 0;
      for (int index = 0; index < this.validValues.Length; ++index) {
        if (this.validValues[index]) {
          sorted[++next] = this.yValues[index];
        }
      }

      Array.Sort(sorted);
      double lower = sorted[(int)((validCount - 1) * 0.025)];
      double upper = sorted[(int)((validCount - 1) * 0.975)];
      double typicalMagnitude = Math.Max(Math.Abs(lower), Math.Abs(upper));
      double extremeMagnitude = Math.Max(
          Math.Abs(this.sampledYMin),
          Math.Abs(this.sampledYMax));

      if (!(typicalMagnitude > 0.0) ||
          !(extremeMagnitude / typicalMagnitude > 8.0)) {
        return;
      }

      for (int index = 1; index < this.validValues.Length; ++index) {
        if (!this.validValues[index - 1] || !this.validValues[index]) {
          continue;
        }

        double previous = this.yValues[index - 1];
        double current = this.yValues[index];
        if (((previous > 0.0 && current < 0.0) ||
             (previous < 0.0 && current > 0.0)) &&
            Math.Abs(previous / typicalMagnitude - current / typicalMagnitude) > 2.0) {
          this.hasSuspectedDiscontinuity = true;
          this.visibleYMin = lower;
          this.visibleYMax = upper;
          return;
        }
      }
    }

    private void DrawGrid(Graphics graphics, Rectangle area) {
      using (Pen gridPen = new Pen(Color.FromArgb(235, 235, 235), 1.0F)) {
        for (int index = 1; index < 5; ++index) {
          float x = area.Left + ((area.Width * index) / 5.0F);
          float y = area.Top + ((area.Height * index) / 5.0F);
          graphics.DrawLine(gridPen, x, area.Top, x, area.Bottom);
          graphics.DrawLine(gridPen, area.Left, y, area.Right, y);
        }
      }
    }

    private void DrawAxes(
        Graphics graphics,
        Rectangle area,
        double normalizedMin,
        double normalizedMax,
        Func<double, float> mapX,
        Func<double, float> mapY) {
      using (Pen axisPen = new Pen(Color.Gray, 1.0F)) {
        if (this.a <= 0.0 && this.b >= 0.0) {
          float xZero = mapX(0.0);
          if (IsFinite(xZero)) {
            graphics.DrawLine(axisPen, xZero, area.Top, xZero, area.Bottom);
          }
        }

        if (normalizedMin <= 0.0 && normalizedMax >= 0.0) {
          float yZero = mapY(0.0);
          if (IsFinite(yZero)) {
            graphics.DrawLine(axisPen, area.Left, yZero, area.Right, yZero);
          }
        }
      }
    }

    private void DrawCurve(
        Graphics graphics,
        Rectangle area,
        Func<double, float> mapX,
        Func<double, float> mapY) {
      if (this.xValues == null || this.yValues == null || this.validValues == null) {
        return;
      }

      // Не допускайте, чтобы слишком большие значения вблизи вертикальных
      // асимптот выходили за пределы области построения графика
      GraphicsState state = graphics.Save();
      graphics.SetClip(area);
      try {
        using (Pen curvePen = new Pen(Color.RoyalBlue, 2.0F)) {
          PointF? previous = null;
          int previousIndex = -1;

          for (int index = 0; index <= Samples; ++index) {
            if (!this.validValues[index]) {
              previous = null;
              previousIndex = -1;
              continue;
            }

            float screenX = mapX(this.xValues[index]);
            float screenY = mapY(this.yValues[index]);
            if (!IsFinite(screenX) || !IsFinite(screenY)) {
              previous = null;
              previousIndex = -1;
              continue;
            }

            PointF current = new PointF(screenX, screenY);
            if (previous.HasValue && previousIndex == index - 1 &&
                this.IsContinuousSegment(previousIndex, index, previous.Value.Y, screenY, area, mapY)) {
              PointF clippedStart;
              PointF clippedEnd;
              if (TryClipSegment(area, previous.Value, current, out clippedStart, out clippedEnd)) {
                graphics.DrawLine(curvePen, clippedStart, clippedEnd);
              }
            }

            previous = current;
            previousIndex = index;
          }
        }
      }
      finally {
        graphics.Restore(state);
      }
    }

    private bool IsContinuousSegment(
        int previousIndex,
        int currentIndex,
        float previousScreenY,
        float currentScreenY,
        Rectangle area,
        Func<double, float> mapY) {
      // Обычные гладкие сегменты не требуют дополнительных вычислений
      if (Math.Abs((double)currentScreenY - previousScreenY) < area.Height * 0.2) {
        return true;
      }

      // Большой скачок может быть как непрерывным крутым склоном, так и асимптотой.
      // Проверяет внутреннюю точку отрезка перед соединением вершин. Это также работает
      // когда полюс (особая точка) лежит между координатами отсчетов, где Math.Tan 
      // возвращает конечные значения на обоих концах интервала
      double left = this.xValues[previousIndex];
      double right = this.xValues[currentIndex];
      double middle = left + ((right - left) / 2.0);
      if (!(middle > left && middle < right)) {
        return false;
      }

      try {
        double middleY = this.function(middle);
        float middleScreenY = mapY(middleY);
        if (!IsFinite(middleScreenY)) {
          return false;
        }

        double interpolatedY = ((double)previousScreenY + currentScreenY) / 2.0;
        return Math.Abs(middleScreenY - interpolatedY) <=
            Math.Max(18.0, area.Height * 0.12);
      }
      catch (ArithmeticException) {
        return false;
      }
      catch (FormatException) {
        return false;
      }
    }

    private static bool TryClipSegment(
        Rectangle area,
        PointF start,
        PointF end,
        out PointF clippedStart,
        out PointF clippedEnd) {
      clippedStart = PointF.Empty;
      clippedEnd = PointF.Empty;
      double dx = (double)end.X - start.X;
      double dy = (double)end.Y - start.Y;
      double first = 0.0;
      double last = 1.0;

      if (!ClipTest(-dx, start.X - area.Left, ref first, ref last) ||
          !ClipTest(dx, area.Right - start.X, ref first, ref last) ||
          !ClipTest(-dy, start.Y - area.Top, ref first, ref last) ||
          !ClipTest(dy, area.Bottom - start.Y, ref first, ref last)) {
        return false;
      }

      clippedStart = new PointF(
          (float)Math.Max(area.Left, Math.Min(area.Right, start.X + (first * dx))),
          (float)Math.Max(area.Top, Math.Min(area.Bottom, start.Y + (first * dy))));
      clippedEnd = new PointF(
          (float)Math.Max(area.Left, Math.Min(area.Right, start.X + (last * dx))),
          (float)Math.Max(area.Top, Math.Min(area.Bottom, start.Y + (last * dy))));
      return true;
    }

    private static bool ClipTest(double direction, double distance, ref double first, ref double last) {
      if (direction == 0.0) {
        return distance >= 0.0;
      }

      double ratio = distance / direction;
      if (direction < 0.0) {
        if (ratio > last) {
          return false;
        }

        first = Math.Max(first, ratio);
      } else {
        if (ratio < first) {
          return false;
        }

        last = Math.Min(last, ratio);
      }

      return true;
    }

    private void DrawRoots(
        Graphics graphics,
        Rectangle area,
        double normalizedMin,
        double normalizedMax,
        Func<double, float> mapX,
        Func<double, float> mapY) {
      if (this.roots == null || this.roots.Count == 0 ||
          normalizedMin > 0.0 || normalizedMax < 0.0) {
        return;
      }

      float rootScreenY = mapY(0.0);
      if (!IsFinite(rootScreenY)) {
        return;
      }

      using (Brush brush = new SolidBrush(Color.DarkGreen)) {
        for (int index = 0; index < this.roots.Count; ++index) {
          float rootScreenX = mapX(this.roots[index]);
          if (!IsPointInside(area, rootScreenX, rootScreenY)) {
            continue;
          }

          graphics.FillEllipse(brush, rootScreenX - 5, rootScreenY - 5, 10, 10);
          if (this.roots.Count == 1) {
            this.DrawMarkerLabel(graphics, area, rootScreenX, rootScreenY, "корень", brush);
          }
        }
      }
    }

    private void DrawMarkerLabel(
        Graphics graphics,
        Rectangle area,
        float markerX,
        float markerY,
        string text,
        Brush textBrush) {
      SizeF textSize = graphics.MeasureString(text, this.Font);
      float x = markerX + 8.0F;
      float y = markerY - textSize.Height - 7.0F;

      if (x + textSize.Width + 4.0F > area.Right) {
        x = markerX - textSize.Width - 10.0F;
      }

      if (y < area.Top) {
        y = markerY + 7.0F;
      }

      x = Math.Max(area.Left + 2.0F, Math.Min(x, area.Right - textSize.Width - 2.0F));
      y = Math.Max(area.Top + 2.0F, Math.Min(y, area.Bottom - textSize.Height - 2.0F));

      RectangleF labelArea = new RectangleF(
          x - 2.0F,
          y - 1.0F,
          textSize.Width + 4.0F,
          textSize.Height + 2.0F);

      using (Brush background = new SolidBrush(Color.FromArgb(225, Color.White))) {
        graphics.FillRectangle(background, labelArea);
      }

      graphics.DrawString(text, this.Font, textBrush, x, y);
    }

    private void DrawLabels(Graphics graphics, Rectangle area, double yMin, double yMax) {
      using (Brush brush = new SolidBrush(Color.Black)) {
        string leftText = this.a.ToString("G5", CultureInfo.CurrentCulture);
        graphics.DrawString(leftText, this.Font, brush, area.Left - 4, area.Bottom + 8);

        string rightText = this.b.ToString("G5", CultureInfo.CurrentCulture);
        SizeF rightSize = graphics.MeasureString(rightText, this.Font);
        graphics.DrawString(
            rightText,
            this.Font,
            brush,
            area.Right - rightSize.Width + 4,
            area.Bottom + 8);

        string maximumText = yMax.ToString("G5", CultureInfo.CurrentCulture);
        SizeF maximumSize = graphics.MeasureString(maximumText, this.Font);
        graphics.DrawString(
            maximumText,
            this.Font,
            brush,
            Math.Max(2.0F, area.Left - maximumSize.Width - 7.0F),
            area.Top - 7.0F);

        string minimumText = yMin.ToString("G5", CultureInfo.CurrentCulture);
        SizeF minimumSize = graphics.MeasureString(minimumText, this.Font);
        graphics.DrawString(
            minimumText,
            this.Font,
            brush,
            Math.Max(2.0F, area.Left - minimumSize.Width - 7.0F),
            area.Bottom - minimumSize.Height + 5.0F);

        graphics.DrawString("f(x)", this.Font, brush, 8.0F, area.Top + 18.0F);

        SizeF xSize = graphics.MeasureString("x", this.Font);
        graphics.DrawString(
            "x",
            this.Font,
            brush,
            area.Right - xSize.Width,
            area.Bottom + 27.0F);
      }
    }

    private void DrawFunctionCaption(Graphics graphics, Rectangle area) {
      string text = "f(x) = " + this.functionDescription +
          (this.roots != null && this.roots.Count > 1 ? "   •   зелёные точки — корни" : string.Empty);
      RectangleF captionArea = new RectangleF(
          area.Left,
          8.0F,
          Math.Max(10.0F, area.Width),
          Math.Max(18.0F, TopMargin - 10.0F));

      using (Brush brush = new SolidBrush(Color.DimGray))
      using (StringFormat format = new StringFormat()) {
        format.Trimming = StringTrimming.EllipsisCharacter;
        format.FormatFlags = StringFormatFlags.NoWrap;
        graphics.DrawString(text, this.Font, brush, captionArea, format);
      }
    }

    private void DrawStatusText(Graphics graphics, string text, Color color) {
      RectangleF textArea = new RectangleF(
          18.0F,
          18.0F,
          Math.Max(20.0F, this.ClientSize.Width - 36.0F),
          Math.Max(20.0F, this.ClientSize.Height - 36.0F));

      using (Brush brush = new SolidBrush(color))
      using (StringFormat format = new StringFormat()) {
        format.Alignment = StringAlignment.Center;
        format.LineAlignment = StringAlignment.Center;
        format.Trimming = StringTrimming.EllipsisWord;
        graphics.DrawString(text, this.Font, brush, textArea, format);
      }
    }

    private static bool IsPointInside(Rectangle area, float x, float y) {
      return IsFinite(x) &&
             IsFinite(y) &&
             x >= area.Left &&
             x <= area.Right &&
             y >= area.Top &&
             y <= area.Bottom;
    }
  }
}