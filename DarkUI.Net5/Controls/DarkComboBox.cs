using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using DarkUI.Config;

namespace DarkUI.Controls
{
    public class DarkComboBox : ComboBox
    {
        private Bitmap _buffer;

        public DarkComboBox() : base()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);

            this.DrawMode = DrawMode.OwnerDrawVariable;

            base.FlatStyle = FlatStyle.Flat;
            base.DropDownStyle = ComboBoxStyle.DropDownList;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Color BackColor { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new ComboBoxStyle DropDownStyle { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new FlatStyle FlatStyle { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Color ForeColor { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this._buffer = null;
            }

            base.Dispose(disposing);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle rect = e.Bounds;

            Color textColor = Colors.LightText;
            Color fillColor = Colors.LightBackground;

            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected ||
                (e.State & DrawItemState.Focus) == DrawItemState.Focus ||
                (e.State & DrawItemState.NoFocusRect) != DrawItemState.NoFocusRect)
            {
                fillColor = Colors.BlueSelection;
            }

            using (var b = new SolidBrush(fillColor))
            {
                g.FillRectangle(b, rect);
            }

            if (e.Index >= 0 && e.Index < this.Items.Count)
            {
                var text = this.Items[e.Index].ToString();

                using (var b = new SolidBrush(textColor))
                {
                    var padding = 2;

                    var modRect = new Rectangle(rect.Left + padding,
                        rect.Top + padding,
                        rect.Width - (padding * 2),
                        rect.Height - (padding * 2));

                    var stringFormat = new StringFormat
                    {
                        LineAlignment = StringAlignment.Center,
                        Alignment = StringAlignment.Near,
                        FormatFlags = StringFormatFlags.NoWrap,
                        Trimming = StringTrimming.EllipsisCharacter
                    };

                    g.DrawString(text, this.Font, b, modRect, stringFormat);
                }
            }
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            this.Invalidate();
        }

        protected override void OnInvalidated(InvalidateEventArgs e)
        {
            base.OnInvalidated(e);
            this.PaintCombobox();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (this._buffer == null)
            {
                this.PaintCombobox();
            }

            Graphics g = e.Graphics;
            g.DrawImageUnscaled(this._buffer, Point.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this._buffer = null;
            this.Invalidate();
        }

        protected override void OnSelectedValueChanged(EventArgs e)
        {
            base.OnSelectedValueChanged(e);
            this.Invalidate();
        }

        protected override void OnTabIndexChanged(EventArgs e)
        {
            base.OnTabIndexChanged(e);
            this.Invalidate();
        }

        protected override void OnTabStopChanged(EventArgs e)
        {
            base.OnTabStopChanged(e);
            this.Invalidate();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            this.Invalidate();
        }

        protected override void OnTextUpdate(EventArgs e)
        {
            base.OnTextUpdate(e);
            this.Invalidate();
        }

        private void PaintCombobox()
        {
            if (this._buffer == null)
            {
                this._buffer = new Bitmap(this.ClientRectangle.Width, this.ClientRectangle.Height);
            }

            using (var g = Graphics.FromImage(this._buffer))
            {
                var rect = new Rectangle(0, 0, this.ClientSize.Width, this.ClientSize.Height);

                Color textColor = Colors.LightText;
                Color borderColor = Colors.GreySelection;
                Color fillColor = Colors.LightBackground;

                if (this.Focused && this.TabStop)
                {
                    borderColor = Colors.BlueHighlight;
                }

                using (var b = new SolidBrush(fillColor))
                {
                    g.FillRectangle(b, rect);
                }

                using (var p = new Pen(borderColor, 1))
                {
                    var modRect = new Rectangle(rect.Left, rect.Top, rect.Width - 1, rect.Height - 1);
                    g.DrawRectangle(p, modRect);
                }

                Bitmap icon = ScrollIcons.scrollbar_arrow_hot;
                g.DrawImageUnscaled(icon,
                                    rect.Right - icon.Width - (Consts.Padding / 2),
                                    (rect.Height / 2) - (icon.Height / 2));

                var text = this.SelectedItem != null ? this.SelectedItem.ToString() : this.Text;

                using (var b = new SolidBrush(textColor))
                {
                    var padding = 2;

                    var modRect = new Rectangle(rect.Left + padding,
                                                rect.Top + padding,
                                                rect.Width - icon.Width - (Consts.Padding / 2) - (padding * 2),
                                                rect.Height - (padding * 2));

                    var stringFormat = new StringFormat
                    {
                        LineAlignment = StringAlignment.Center,
                        Alignment = StringAlignment.Near,
                        FormatFlags = StringFormatFlags.NoWrap,
                        Trimming = StringTrimming.EllipsisCharacter
                    };

                    g.DrawString(text, this.Font, b, modRect, stringFormat);
                }
            }
        }
    }
}