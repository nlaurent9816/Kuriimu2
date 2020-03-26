﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Be.Windows.Forms;
using Kontract;
using Kontract.Extensions;
using Kontract.Interfaces.FileSystem;
using Kontract.Interfaces.Managers;
using Kontract.Interfaces.Plugins.State;
using Kontract.Interfaces.Plugins.State.Archive;
using Kontract.Interfaces.Progress;
using Kontract.Models.Archive;
using Kontract.Models.IO;
using Kore.Factories;
using Kore.Managers.Plugins;
using Kuriimu2.WinForms.MainForms.Interfaces;
using Kuriimu2.WinForms.Properties;

namespace Kuriimu2.WinForms.MainForms.FormatForms
{
    public partial class HexForm : UserControl, IKuriimuForm
    {
        private readonly IStateInfo _stateInfo;
        private readonly IHexState _hexState;

        public Func<SaveTabEventArgs, Task<bool>> SaveFilesDelegate { get; set; }
        public Action<IStateInfo> UpdateTabDelegate { get; set; }

        public HexForm(IStateInfo stateInfo)
        {
            InitializeComponent();

            if (!(stateInfo.State is IHexState hexState))
                throw new InvalidOperationException($"This state is not an {nameof(IHexState)}.");

            _stateInfo = stateInfo;
            _hexState = hexState;

            fileData.ByteProvider = new DynamicFileByteProvider(hexState.FileStream);
        }

        public void UpdateForm()
        {
            UpdateTabDelegate(_stateInfo);
        }
    }
}