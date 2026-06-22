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
using Chummer.Desktop.Runtime;

namespace Chummer.Forms
{
    internal sealed class DesktopInstallLinkingGateForm : Form
    {
        private readonly DesktopInstallLinkingStartupContext _startupContext;

        public DesktopInstallLinkingGateForm(DesktopInstallLinkingStartupContext startupContext)
        {
            _startupContext = startupContext;
            Text = "Claim your copy";
            Font = new Font(FontFamily.GenericSansSerif, 9.0f);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Size = new Size(520, 220);
            MinimumSize = new Size(420, 190);

            Label objHeadline = new()
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Font = new Font(Font, FontStyle.Bold),
                Padding = new Padding(16, 14, 16, 2),
                Height = 52,
                Text = "This copy is not linked yet."
            };

            Label objInfo = new()
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Padding = new Padding(16, 2, 16, 12),
                Height = 90,
                Text = BuildMessage(startupContext)
            };

            Button objLinkButton = new()
            {
                Text = "Claim your copy",
                AutoSize = true,
                Anchor = AnchorStyles.None
            };

            Button objCloseButton = new()
            {
                Text = "Continue unlinked",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                Anchor = AnchorStyles.None
            };

            FlowLayoutPanel objButtons = new()
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(12),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            objButtons.Controls.Add(objCloseButton);
            objButtons.Controls.Add(objLinkButton);

            objLinkButton.Click += OpenWebsite;

            AcceptButton = objLinkButton;
            CancelButton = objCloseButton;

            Controls.Add(objButtons);
            Controls.Add(objInfo);
            Controls.Add(objHeadline);
            this.UpdateLightDarkMode();
            this.UpdateParentForToolTipControls();
            ActiveControl = objLinkButton;
        }

        private static string BuildMessage(DesktopInstallLinkingStartupContext startupContext)
        {
            string strMessage = "Claim this copy online when you want recovery, updates, and support to stay attached to this install. " +
                                "Close this window to keep using Chummer unlinked.";
            if (!string.IsNullOrWhiteSpace(startupContext.PromptReason))
                strMessage += Environment.NewLine + Environment.NewLine + "Status: " + startupContext.PromptReason;
            if (!string.IsNullOrWhiteSpace(startupContext.ClaimResult?.Message))
                strMessage += Environment.NewLine + Environment.NewLine + startupContext.ClaimResult.Message;

            return strMessage;
        }

        private void OpenWebsite(object sender, EventArgs e)
        {
            if (DesktopInstallLinkingRuntime.TryOpenAccountPortalForInstall(_startupContext.State))
                DialogResult = DialogResult.OK;
            else
                MessageBox.Show(
                    this,
                    "We could not open the claim page. Please check your network connection and browser settings.",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
        }
    }
}
