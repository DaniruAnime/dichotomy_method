// <copyright file="PlotPanel.cs" company="Educational project">
// Copyright (c) Educational project. All rights reserved.
// </copyright>

namespace Dihotomia
{
    using System;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Globalization;
    using System.Windows.Forms;

    /// <summary>
    /// Draws the function and calculated points without third-party libraries.
    /// </summary>
    internal sealed class PlotPanel : Panel
    {
        private const int Samples = 700;
        private const int LeftMargin = 72;
        private const int TopMargin = 38;
        private const int RightMargin = 24;
        private const int BottomMargin = 56;

        private Func<double, double> function;
        private string functionDescription;
        private double a;
        private double b;
        private bool hasMinimum;
        private double minimumX;
        private double minimumY;
        private bool hasRoot;
        private double rootX;
        private double[] xValues;
        private double[] yValues;
        private bool[] validValues;
        private double sampledYMin;
        private double sampledYMax;
        private bool hasDrawableSamples;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlotPanel"/> class.
        /// </summary>
        public PlotPanel()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.White;
            this.BorderStyle = BorderStyle.FixedSingle;
            this.ResizeRedraw = true;
            this.TabStop = false;
        }

        /// <summary>
        /// Sets data to be drawn.
        /// </summary>
        /// <param name="functionToPlot">Function to draw.</param>
        /// <param name="description">Text representation of the function.</param>
        /// <param name="leftBound">Left x bound.</param>
        /// <param name="rightBound">Right x bound.</param>
        /// <param name="foundMinimumX">Minimum x coordinate.</param>
        /// <param name="foundMinimumY">Minimum y coordinate.</param>
        /// <param name="minimumExists">Whether the minimum should be drawn.</param>
        /// <param name="foundRootX">Root x coordinate.</param>
        /// <param name="rootExists">Whether the root should be drawn.</param>
        public void SetPlot(
            Func<double, double> functionToPlot,
            string description,
            double leftBound,
            double rightBound,
            double foundMinimumX,
            double foundMinimumY,
            bool minimumExists,
            double foundRootX,
            bool rootExists)
        {
            this.function = functionToPlot;
            this.functionDescription = description ?? string.Empty;
            this.a = leftBound;
            this.b = rightBound;
            this.minimumX = foundMinimumX;
            this.minimumY = foundMinimumY;
            this.hasMinimum = minimumExists;
            this.rootX = foundRootX;
            this.hasRoot = rootExists;
            this.BuildSampleCache();
            this.Invalidate();
        }

        /// <summary>
        /// Clears all plotted data.
        /// </summary>
        public void ClearPlot()
        {
            this.function = null;
            this.functionDescription = string.Empty;
            this.hasMinimum = false;
            this.hasRoot = false;
            this.xValues = null;
            this.yValues = null;
            this.validValues = null;
            this.hasDrawableSamples = false;
            this.sampledYMin = 0.0;
            this.sampledYMax = 0.0;
            this.Invalidate();
        }

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle area = new Rectangle(
                LeftMargin,
                TopMargin,
                Math.Max(10, this.ClientSize.Width - LeftMargin - RightMargin),
                Math.Max(10, this.ClientSize.Height - TopMargin - BottomMargin));

            using (Pen borderPen = new Pen(Color.Gainsboro))
            {
                graphics.DrawRectangle(borderPen, area);
            }

            if (this.function == null || this.b <= this.a || area.Width < 40 || area.Height < 40)
            {
                this.DrawStatusText(
                    graphics,
                    "После расчёта здесь появится график функции и найденная точка минимума.",
                    Color.DimGray);
                return;
            }

            this.DrawFunctionCaption(graphics, area);

            double intervalLength = this.b - this.a;
            if (!IsFinite(intervalLength) || intervalLength <= 0.0)
            {
                this.DrawStatusText(graphics, "Некорректный диапазон графика.", Color.DarkRed);
                return;
            }

            if (!this.hasDrawableSamples)
            {
                this.DrawStatusText(
                    graphics,
                    "На интервале не удалось получить конечные значения функции.",
                    Color.DarkRed);
                return;
            }

            double yMin = this.sampledYMin;
            double yMax = this.sampledYMax;

            if (this.hasMinimum && IsDrawableValue(this.minimumY))
            {
                yMin = Math.Min(yMin, this.minimumY);
                yMax = Math.Max(yMax, this.minimumY);
            }

            if (this.hasRoot)
            {
                yMin = Math.Min(yMin, 0.0);
                yMax = Math.Max(yMax, 0.0);
            }

            double yScale;
            double normalizedMin;
            double normalizedMax;
            if (!TryPrepareNormalizedRange(yMin, yMax, out yScale, out normalizedMin, out normalizedMax))
            {
                this.DrawStatusText(graphics, "Не удалось определить масштаб графика.", Color.DarkRed);
                return;
            }

            Func<double, float> mapX = delegate(double x)
            {
                double ratio = (x - this.a) / intervalLength;
                return ToFiniteFloat(area.Left + (ratio * area.Width));
            };

            Func<double, float> mapY = delegate(double y)
            {
                if (!IsFinite(y))
                {
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
            this.DrawRoot(graphics, area, normalizedMin, normalizedMax, mapX, mapY);
            this.DrawMinimum(graphics, area, mapX, mapY);
            this.DrawLabels(graphics, area, displayYMin, displayYMax);
        }

        private static bool IsDrawableValue(double value)
        {
            return IsFinite(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float ToFiniteFloat(double value)
        {
            if (!IsFinite(value) || value > float.MaxValue || value < -float.MaxValue)
            {
                return float.NaN;
            }

            return (float)value;
        }

        private static bool TryPrepareNormalizedRange(
            double yMin,
            double yMax,
            out double yScale,
            out double normalizedMin,
            out double normalizedMax)
        {
            yScale = Math.Max(Math.Abs(yMin), Math.Abs(yMax));
            if (!IsFinite(yScale))
            {
                normalizedMin = 0.0;
                normalizedMax = 0.0;
                return false;
            }

            if (yScale < 1.0)
            {
                yScale = 1.0;
            }

            normalizedMin = yMin / yScale;
            normalizedMax = yMax / yScale;

            if (!IsFinite(normalizedMin) || !IsFinite(normalizedMax))
            {
                return false;
            }

            double range = normalizedMax - normalizedMin;
            if (!(range > 0.0) || range < 1E-12)
            {
                double center = normalizedMin + ((normalizedMax - normalizedMin) / 2.0);
                double padding = Math.Max(0.1, Math.Abs(center) * 0.1);
                normalizedMin = center - padding;
                normalizedMax = center + padding;
            }
            else
            {
                double padding = range * 0.08;
                normalizedMin -= padding;
                normalizedMax += padding;
            }

            return IsFinite(normalizedMin) &&
                   IsFinite(normalizedMax) &&
                   normalizedMax > normalizedMin;
        }

        private static double DenormalizeForLabel(double normalized, double scale, double fallback)
        {
            double value = normalized * scale;
            return IsFinite(value) ? value : fallback;
        }

        private void BuildSampleCache()
        {
            this.xValues = new double[Samples + 1];
            this.yValues = new double[Samples + 1];
            this.validValues = new bool[Samples + 1];
            this.hasDrawableSamples = false;
            this.sampledYMin = double.PositiveInfinity;
            this.sampledYMax = double.NegativeInfinity;

            if (this.function == null || this.b <= this.a)
            {
                return;
            }

            double intervalLength = this.b - this.a;
            if (!IsFinite(intervalLength) || intervalLength <= 0.0)
            {
                return;
            }

            for (int index = 0; index <= Samples; index++)
            {
                double x = index == Samples
                    ? this.b
                    : this.a + (intervalLength * ((double)index / Samples));
                this.xValues[index] = x;

                try
                {
                    double y = this.function(x);
                    if (!IsDrawableValue(y))
                    {
                        continue;
                    }

                    this.yValues[index] = y;
                    this.validValues[index] = true;
                    this.sampledYMin = Math.Min(this.sampledYMin, y);
                    this.sampledYMax = Math.Max(this.sampledYMax, y);
                    this.hasDrawableSamples = true;
                }
                catch (ArithmeticException)
                {
                    this.validValues[index] = false;
                }
                catch (FormatException)
                {
                    this.validValues[index] = false;
                }
            }
        }

        private void DrawGrid(Graphics graphics, Rectangle area)
        {
            using (Pen gridPen = new Pen(Color.FromArgb(235, 235, 235), 1.0F))
            {
                for (int index = 1; index < 5; index++)
                {
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
            Func<double, float> mapY)
        {
            using (Pen axisPen = new Pen(Color.Gray, 1.0F))
            {
                if (this.a <= 0.0 && this.b >= 0.0)
                {
                    float xZero = mapX(0.0);
                    if (IsFinite(xZero))
                    {
                        graphics.DrawLine(axisPen, xZero, area.Top, xZero, area.Bottom);
                    }
                }

                if (normalizedMin <= 0.0 && normalizedMax >= 0.0)
                {
                    float yZero = mapY(0.0);
                    if (IsFinite(yZero))
                    {
                        graphics.DrawLine(axisPen, area.Left, yZero, area.Right, yZero);
                    }
                }
            }
        }

        private void DrawCurve(
            Graphics graphics,
            Rectangle area,
            Func<double, float> mapX,
            Func<double, float> mapY)
        {
            if (this.xValues == null || this.yValues == null || this.validValues == null)
            {
                return;
            }

            using (Pen curvePen = new Pen(Color.RoyalBlue, 2.0F))
            {
                PointF? previous = null;

                for (int index = 0; index <= Samples; index++)
                {
                    if (!this.validValues[index])
                    {
                        previous = null;
                        continue;
                    }

                    float screenX = mapX(this.xValues[index]);
                    float screenY = mapY(this.yValues[index]);
                    if (!IsFinite(screenX) || !IsFinite(screenY))
                    {
                        previous = null;
                        continue;
                    }

                    PointF current = new PointF(screenX, screenY);

                    if (previous.HasValue &&
                        Math.Abs(current.Y - previous.Value.Y) < (area.Height * 0.8F))
                    {
                        graphics.DrawLine(curvePen, previous.Value, current);
                    }

                    previous = current;
                }
            }
        }

        private void DrawRoot(
            Graphics graphics,
            Rectangle area,
            double normalizedMin,
            double normalizedMax,
            Func<double, float> mapX,
            Func<double, float> mapY)
        {
            if (!this.hasRoot || normalizedMin > 0.0 || normalizedMax < 0.0)
            {
                return;
            }

            float rootScreenX = mapX(this.rootX);
            float rootScreenY = mapY(0.0);
            if (!IsPointInside(area, rootScreenX, rootScreenY))
            {
                return;
            }

            using (Brush brush = new SolidBrush(Color.DarkGreen))
            {
                graphics.FillEllipse(brush, rootScreenX - 5, rootScreenY - 5, 10, 10);
                this.DrawMarkerLabel(graphics, area, rootScreenX, rootScreenY, "корень", brush);
            }
        }

        private void DrawMinimum(
            Graphics graphics,
            Rectangle area,
            Func<double, float> mapX,
            Func<double, float> mapY)
        {
            if (!this.hasMinimum || !IsDrawableValue(this.minimumY))
            {
                return;
            }

            float minimumScreenX = mapX(this.minimumX);
            float minimumScreenY = mapY(this.minimumY);

            if (!IsPointInside(area, minimumScreenX, minimumScreenY))
            {
                return;
            }

            using (Brush brush = new SolidBrush(Color.Crimson))
            {
                graphics.FillEllipse(brush, minimumScreenX - 6, minimumScreenY - 6, 12, 12);
                this.DrawMarkerLabel(graphics, area, minimumScreenX, minimumScreenY, "минимум", brush);
            }
        }

        private void DrawMarkerLabel(
            Graphics graphics,
            Rectangle area,
            float markerX,
            float markerY,
            string text,
            Brush textBrush)
        {
            SizeF textSize = graphics.MeasureString(text, this.Font);
            float x = markerX + 8.0F;
            float y = markerY - textSize.Height - 7.0F;

            if (x + textSize.Width + 4.0F > area.Right)
            {
                x = markerX - textSize.Width - 10.0F;
            }

            if (y < area.Top)
            {
                y = markerY + 7.0F;
            }

            x = Math.Max(area.Left + 2.0F, Math.Min(x, area.Right - textSize.Width - 2.0F));
            y = Math.Max(area.Top + 2.0F, Math.Min(y, area.Bottom - textSize.Height - 2.0F));

            RectangleF labelArea = new RectangleF(
                x - 2.0F,
                y - 1.0F,
                textSize.Width + 4.0F,
                textSize.Height + 2.0F);

            using (Brush background = new SolidBrush(Color.FromArgb(225, Color.White)))
            {
                graphics.FillRectangle(background, labelArea);
            }

            graphics.DrawString(text, this.Font, textBrush, x, y);
        }

        private void DrawLabels(Graphics graphics, Rectangle area, double yMin, double yMax)
        {
            using (Brush brush = new SolidBrush(Color.Black))
            {
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

        private void DrawFunctionCaption(Graphics graphics, Rectangle area)
        {
            string text = "f(x) = " + this.functionDescription;
            RectangleF captionArea = new RectangleF(
                area.Left,
                8.0F,
                Math.Max(10.0F, area.Width),
                Math.Max(18.0F, TopMargin - 10.0F));

            using (Brush brush = new SolidBrush(Color.DimGray))
            using (StringFormat format = new StringFormat())
            {
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.NoWrap;
                graphics.DrawString(text, this.Font, brush, captionArea, format);
            }
        }

        private void DrawStatusText(Graphics graphics, string text, Color color)
        {
            RectangleF textArea = new RectangleF(
                18.0F,
                18.0F,
                Math.Max(20.0F, this.ClientSize.Width - 36.0F),
                Math.Max(20.0F, this.ClientSize.Height - 36.0F));

            using (Brush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisWord;
                graphics.DrawString(text, this.Font, brush, textArea, format);
            }
        }

        private static bool IsPointInside(Rectangle area, float x, float y)
        {
            return IsFinite(x) &&
                   IsFinite(y) &&
                   x >= area.Left &&
                   x <= area.Right &&
                   y >= area.Top &&
                   y <= area.Bottom;
        }
    }
}
