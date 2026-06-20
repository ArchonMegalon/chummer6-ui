/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */

using System;
using System.Drawing;
using System.Windows.Forms;

namespace Chummer
{
    public class ElasticComboBox : ComboBox
    {
        public ElasticComboBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Popup;
            IntegralHeight = false;
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            if (DropDownStyle != ComboBoxStyle.DropDownList && IsHandleCreated)
                ClearSelection();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (DropDownStyle != ComboBoxStyle.DropDownList && IsHandleCreated)
                ClearSelection();
        }

        protected override void OnDataSourceChanged(EventArgs e)
        {
            // Save the current selected index before the data source changes
            // The base class will try to restore it, but it might be out of range for the new data source
            int intPreviousSelectedIndex = SelectedIndex;
            // Temporarily clear selection to prevent base class from trying to restore an invalid index
            // We'll restore it after if it's still valid
            if (intPreviousSelectedIndex >= 0)
            {
                SelectedIndex = -1;
            }
            try
            {
                base.OnDataSourceChanged(e);
                // After the data source has changed, try to restore the previous selection if it's still valid
                if (intPreviousSelectedIndex >= 0 && intPreviousSelectedIndex < Items.Count)
                {
                    SelectedIndex = intPreviousSelectedIndex;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // If setting the selected index fails, just leave it at -1 (no selection)
                // This can happen if the Items collection changes between setting the data source and here
                SelectedIndex = -1;
            }
            ResizeDropDown();
        }

        protected override void OnDisplayMemberChanged(EventArgs e)
        {
            base.OnDisplayMemberChanged(e);
            ResizeDropDown();
        }

        protected override void OnValueMemberChanged(EventArgs e)
        {
            base.OnValueMemberChanged(e);
            ResizeDropDown();
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < -1)
            {
                base.OnDrawItem(e);
                return;
            }

            // Editable combo boxes render their active text through the native edit control.
            // Drawing the -1 "selected item" surface here produces duplicated, overlapping text.
            if (e.Index == -1 && DropDownStyle != ComboBoxStyle.DropDownList)
                return;

            bool blnEnabled = Enabled;
            bool blnSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color objBackColor;
            Color objForeColor;
            if (!blnEnabled)
            {
                objBackColor = ColorManager.Control;
                objForeColor = ColorManager.GrayText;
            }
            else if (blnSelected)
            {
                objBackColor = ColorManager.Highlight;
                objForeColor = ColorManager.HighlightText;
            }
            else
            {
                objBackColor = BackColor;
                objForeColor = ForeColor;
            }

            objForeColor = ColorManager.EnsureReadableForeground(objForeColor, objBackColor);

            using (SolidBrush objBackBrush = new SolidBrush(objBackColor))
            using (SolidBrush objForeBrush = new SolidBrush(objForeColor))
            {
                e.Graphics.FillRectangle(objBackBrush, e.Bounds);
                string strItemText = e.Index >= 0 && e.Index < Items.Count
                    ? GetItemText(Items[e.Index])
                    : Text;
                Rectangle objTextBounds = Rectangle.Inflate(e.Bounds, -4, 0);
                TextRenderer.DrawText(
                    e.Graphics,
                    strItemText,
                    Font,
                    objTextBounds,
                    objForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus)
                e.DrawFocusRectangle();
        }

        private void ResizeDropDown()
        {
            int intMaxItemWidth = Width;
            foreach (object objItem in Items)
            {
                string strItemText = string.Empty;
                if (objItem is ListItem objListItem)
                    strItemText = objListItem.Name;
                if (string.IsNullOrEmpty(strItemText))
                    strItemText = GetItemText(objItem);
                int intLoopItemWidth = TextRenderer.MeasureText(strItemText, Font).Width;
                if (intLoopItemWidth > intMaxItemWidth)
                    intMaxItemWidth = intLoopItemWidth;
            }
            DropDownWidth = intMaxItemWidth;
        }

        private void ClearSelection()
        {
            Control objActiveControl = FindForm()?.ActiveControl;
            if (objActiveControl == null || objActiveControl == this)
                return;
            SelectionLength = 0;
        }
    }
}
