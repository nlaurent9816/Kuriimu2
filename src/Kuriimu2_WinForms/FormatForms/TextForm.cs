﻿using Kore;
using Kuriimu2_WinForms.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kuriimu2_WinForms.FormatForms
{
    public partial class TextForm : UserControl, IKuriimuForm
    {
        public TextForm(KoreFileInfo kfi)
        {
            InitializeComponent();

            Kfi = kfi;
        }

        public KoreFileInfo Kfi { get; set; }

        public bool HasChanges => throw new NotImplementedException();

        public event EventHandler<EventArgs> HasChangesHandler;

        public void Close()
        {
            throw new NotImplementedException();
        }

        public void Save(string filename = "")
        {
            throw new NotImplementedException();
        }
    }
}