// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace Boku
{
    // (TODO (****) BROKEN
    public partial class ErrorForm : Form
    {
        public ErrorForm()
        {
            InitializeComponent();
        }

        private void ErrorForm_Load(object sender, EventArgs e)
        {
            Cursor.Show();

            buttonSendAndClose.Enabled = Program2.SiteOptions.NetworkEnabled;
            textBoxAddInfo.Enabled = Program2.SiteOptions.NetworkEnabled;
            textBoxLiveId.Enabled = Program2.SiteOptions.NetworkEnabled;
        }
    }
}
