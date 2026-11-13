using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SchoolNetAutoAuth.Installer;

/// <summary>零圆角实心 / 描边按钮。</summary>
internal sealed class FlatButton : Button
{
    internal FlatButton(string text, bool primary, float fontSize = 12f)
    {
        Text = text;
        AutoSize = false;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        FlatAppearance.BorderSize = 1;
        FlatAppearance.BorderColor = primary ? InstallerTheme.Brand : InstallerTheme.BorderStrong;
        BackColor = primary ? InstallerTheme.Brand : InstallerTheme.Surface;
        ForeColor = primary ? Color.White : InstallerTheme.Fg;
        FlatAppearance.MouseOverBackColor = primary ? InstallerTheme.BrandHover : InstallerTheme.Panel;
        FlatAppearance.MouseDownBackColor = primary ? InstallerTheme.Brand : InstallerTheme.Border;
        Font = InstallerTheme.Ui(InstallerMetrics.Px(fontSize));
        Cursor = Cursors.Hand;
        TabStop = true;
    }
}

/// <summary>带 1px 边框的单行输入框，内部 TextBox 无边框。</summary>
internal sealed class FlatTextBox : Panel
{
    internal TextBox Input { get; }

    internal FlatTextBox(string text)
    {
        DoubleBuffered = true;
        BackColor = InstallerTheme.Bg;
        Padding = new Padding(InstallerMetrics.Px(8), InstallerMetrics.Px(8), InstallerMetrics.Px(8), InstallerMetrics.Px(6));
        Input = new TextBox
        {
            Text = text,
            BorderStyle = BorderStyle.None,
            BackColor = InstallerTheme.Bg,
            ForeColor = InstallerTheme.Fg,
            Font = InstallerTheme.Mono(InstallerMetrics.Px(11)),
            Dock = DockStyle.Fill
        };
        Controls.Add(Input);
    }

    internal new event EventHandler? TextChanged
    {
        add => Input.TextChanged += value;
        remove => Input.TextChanged -= value;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(InstallerTheme.Input);
        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;
        e.Graphics.DrawRectangle(pen, bounds);
    }
}

/// <summary>0–100 的 4px 细进度条，零圆角。</summary>
internal sealed class ThinProgressBar : Control
{
    private int _value;

    internal ThinProgressBar()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, 100);
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var track = new SolidBrush(InstallerTheme.Border);
        e.Graphics.FillRectangle(track, 0, 0, Width, Height);
        var filled = (int)Math.Round(Width * (_value / 100.0));
        if (filled <= 0) return;
        using var fill = new SolidBrush(InstallerTheme.Brand);
        e.Graphics.FillRectangle(fill, 0, 0, filled, Height);
    }
}

/// <summary>左图区：cover 裁切居中。SizeMode.Zoom 会留白，所以自己画。</summary>
internal sealed class CoverImageBox : Control
{
    private readonly Image? _image;

    internal CoverImageBox(Image? image)
    {
        _image = image;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _image?.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_image is null)
        {
            using var brush = new SolidBrush(InstallerTheme.Card);
            e.Graphics.FillRectangle(brush, ClientRectangle);
            return;
        }

        var scale = Math.Max(Width / (float)_image.Width, Height / (float)_image.Height);
        var width = _image.Width * scale;
        var height = _image.Height * scale;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(_image, new RectangleF(
            (Width - width) / 2f, (Height - height) / 2f, width, height));
    }
}

internal enum StageState
{
    Pending,
    Active,
    Done
}

/// <summary>五阶段清单：符号 / 文案 / 右对齐文件计数。整块自绘，避免 15 个 Label 打架。</summary>
internal sealed class StageList : Control
{
    private static readonly string[] Titles =
    [
        "准备安装目录",
        "解压程序文件",
        "安全替换旧版本",
        "注册开始菜单与卸载信息",
        "创建快捷方式"
    ];

    private readonly StageState[] _states = new StageState[Titles.Length];
    private readonly string?[] _details = new string?[Titles.Length];

    internal StageList()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    internal static int Count => Titles.Length;

    internal int RowHeight => InstallerMetrics.Px(24);

    internal void Reset()
    {
        Array.Fill(_states, StageState.Pending);
        Array.Fill(_details, null);
        Invalidate();
    }

    internal void SetState(int index, StageState state, string? detail = null)
    {
        if (index < 0 || index >= Titles.Length) return;
        _states[index] = state;
        _details[index] = detail;
        Invalidate();
    }

    internal void CompleteAll()
    {
        for (var index = 0; index < Titles.Length; index++)
        {
            _states[index] = StageState.Done;
            _details[index] = null;
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var rowHeight = RowHeight;
        using var pending = new SolidBrush(InstallerTheme.MutedFg);
        using var active = new SolidBrush(InstallerTheme.Brand);
        using var done = new SolidBrush(InstallerTheme.Success);
        using var text = new SolidBrush(InstallerTheme.Fg);
        using var detail = new SolidBrush(InstallerTheme.MutedFg);
        using var glyphFont = InstallerTheme.Ui(InstallerMetrics.Px(11));
        using var textFont = InstallerTheme.Ui(InstallerMetrics.Px(12));
        using var detailFont = InstallerTheme.Mono(InstallerMetrics.Px(11));
        using var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };

        for (var index = 0; index < Titles.Length; index++)
        {
            var y = index * rowHeight;
            var (glyph, brush) = _states[index] switch
            {
                StageState.Done => ("✓", done),
                StageState.Active => ("●", active),
                _ => ("○", pending)
            };
            e.Graphics.DrawString(glyph, glyphFont, brush, 0, y);
            var label = Titles[index];
            var detailText = _details[index];
            var width = Width - InstallerMetrics.Px(18);
            if (detailText is not null) width -= TextRenderer.MeasureText(detailText, detailFont).Width + InstallerMetrics.Px(8);
            e.Graphics.DrawString(label, textFont, text, new RectangleF(InstallerMetrics.Px(18), y, width, rowHeight), format);
            if (detailText is not null)
            {
                var detailWidth = TextRenderer.MeasureText(detailText, detailFont).Width;
                e.Graphics.DrawString(detailText, detailFont, detail,
                    new RectangleF(Width - detailWidth, y, detailWidth, rowHeight), format);
            }
        }
    }

}

/// <summary>长路径单行显示：放不下时左截断成「…\末段」。</summary>
internal sealed class PathEllipsisLabel : Control
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string FullText { get; set; } = string.Empty;

    internal PathEllipsisLabel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        Font = InstallerTheme.Mono(InstallerMetrics.Px(11));
        ForeColor = InstallerTheme.Fg;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (string.IsNullOrEmpty(FullText)) return;
        var text = FullText;
        var width = ClientSize.Width;
        if (TextRenderer.MeasureText(text, Font).Width > width)
        {
            var parts = text.Split(Path.DirectorySeparatorChar);
            for (var keep = parts.Length - 1; keep > 0; keep--)
            {
                var candidate = "…" + Path.DirectorySeparatorChar
                    + string.Join(Path.DirectorySeparatorChar, parts.Skip(Math.Max(0, parts.Length - keep)));
                if (TextRenderer.MeasureText(candidate, Font).Width <= width)
                {
                    text = candidate;
                    break;
                }
            }
        }

        TextRenderer.DrawText(e.Graphics, text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}
