namespace DungeonCoopFix;

internal sealed class MainForm : Form
{
    private readonly ControllerService _service;
    private readonly Label _fixState = new() { AutoSize = true };
    private readonly Label _dummyState = new() { AutoSize = true };
    private readonly Label _physicalState = new() { AutoSize = true };
    private readonly Label _message = new() { AutoSize = true, MaximumSize = new Size(388, 0) };
    private readonly Button _toggle = new() { Text = "Enable Co-op Fix", AutoSize = true, MinimumSize = new Size(0, 44), Dock = DockStyle.Fill };
    private readonly Button _activate = new() { Text = "Experimental: Virtual L3 pulse", AutoSize = true, MinimumSize = new Size(0, 40), Dock = DockStyle.Fill, Enabled = false };
    private readonly CheckBox _setupMode = new() { Text = "Experimental: P1 Setup Mode", AutoSize = true, Enabled = false };
    private readonly Label _setupStatus = new() { Text = "P1 Setup Mode: OFF", AutoSize = true };
    private readonly LinkLabel _install = new() { Text = "Install required driver…", AutoSize = true, Visible = false };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1500 };
    private bool _busy, _closeRequested;
    private bool _changingSetup, _keyboardRegistered;

    internal MainForm(ControllerService service)
    {
        _service = service;
        Text = "DungeonCoopFix";
        ClientSize = new Size(440, 570);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(248, 249, 251);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 13,
            Padding = new Padding(24, 20, 24, 20)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Text = "Minecraft Dungeons Local Co-op",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 18)
        }, 0, 0);
        _fixState.Font = new Font(Font.FontFamily, 16, FontStyle.Bold);
        _fixState.Margin = new Padding(0, 0, 0, 12);
        layout.Controls.Add(_fixState, 0, 1);
        _toggle.UseVisualStyleBackColor = false;
        _toggle.FlatStyle = FlatStyle.Flat;
        _toggle.FlatAppearance.BorderSize = 0;
        _toggle.BackColor = Color.FromArgb(0, 99, 177);
        _toggle.ForeColor = Color.White;
        _toggle.Margin = new Padding(0, 0, 0, 8);
        layout.Controls.Add(_toggle, 0, 2);
        _activate.Margin = new Padding(0, 0, 0, 18);
        layout.Controls.Add(_activate, 0, 3);
        _setupMode.Margin = new Padding(0, 0, 0, 4);
        layout.Controls.Add(_setupMode, 0, 4);
        _setupStatus.Margin = new Padding(0, 0, 0, 4);
        layout.Controls.Add(_setupStatus, 0, 5);
        layout.Controls.Add(new Label
        {
            Text = "Experimental controls are not needed for normal co-op.\nEnter → A · Escape → B · Arrows/WASD → D-pad · F8 → L3",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 8.5f),
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 0, 0, 14)
        }, 0, 6);
        _dummyState.Margin = new Padding(0, 0, 0, 6);
        _physicalState.Margin = new Padding(0, 0, 0, 6);
        layout.Controls.Add(_dummyState, 0, 7);
        layout.Controls.Add(_physicalState, 0, 8);
        layout.Controls.Add(new Label
        {
            Text = "Steam/PS4 pads may not appear; other virtual pads count.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Font = new Font(Font.FontFamily, 8.5f),
            Margin = new Padding(0, 0, 0, 14)
        }, 0, 9);
        _message.Margin = new Padding(0, 0, 0, 8);
        layout.Controls.Add(_message, 0, 10);
        _install.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(_install, 0, 11);
        for (int row = 0; row < 12; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        AcceptButton = _toggle;

        _toggle.Click += ToggleClicked;
        _activate.Click += ActivateClicked;
        _setupMode.CheckedChanged += SetupModeChanged;
        _install.LinkClicked += InstallClicked;
        _timer.Tick += (_, _) => RefreshStatus();
        Shown += (_, _) => { RefreshStatus(); _timer.Start(); };
        FormClosing += ClosingWindow;
        FormClosed += (_, _) => { _timer.Stop(); _service.Disable(); };
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (_busy) return;
        ControllerStatus status = _service.GetStatus();
        _fixState.Text = $"Co-op Fix: {(status.Enabled ? "ON" : "OFF")}";
        _fixState.ForeColor = status.Enabled ? Color.FromArgb(22, 115, 69) : Color.FromArgb(45, 52, 62);
        _toggle.Text = status.Enabled ? "Disable Co-op Fix" : "Enable Co-op Fix";
        _dummyState.Text = status.Enabled ? $"Dummy Controller: Active · XInput slot {status.DummySlot + 1}" : "Dummy Controller: Not active";
        _physicalState.Text = $"XInput Physical Controllers: {status.OtherControllerCount} detected";
        _activate.Enabled = status.Enabled && status.DummySlot == 0;
        SyncSetupMode();
        _message.Text = status.Message;
        _message.ForeColor = status.Ready ? Color.FromArgb(22, 115, 69) : Color.FromArgb(65, 72, 82);
        _install.Visible = _service.DriverMissing;
    }

    private void SetupModeChanged(object? sender, EventArgs e)
    {
        if (_changingSetup) return;
        try
        {
            _service.SetP1SetupMode(_setupMode.Checked);
            if (_service.P1SetupMode)
            {
                SetupKeyboardInput.Register(Handle, true);
                _keyboardRegistered = true;
            }
        }
        catch (Exception error)
        {
            try { _service.SetP1SetupMode(false); } catch { _service.Disable(); }
            ShowError(error.Message);
        }
        RefreshStatus();
    }

    private void SyncSetupMode()
    {
        if (!_service.P1SetupMode && _keyboardRegistered)
        {
            SetupKeyboardInput.Register(IntPtr.Zero, false);
            _keyboardRegistered = false;
        }
        _changingSetup = true;
        try { _setupMode.Checked = _service.P1SetupMode; }
        finally { _changingSetup = false; }
        _setupMode.Enabled = !_busy && _service.Enabled;
        _setupStatus.Text = _service.P1SetupMode ? "P1 Setup Mode: ACTIVE · forwards keys in background" : "P1 Setup Mode: OFF";
        _setupStatus.ForeColor = _service.P1SetupMode ? Color.FromArgb(166, 70, 0) : Color.DimGray;
        _activate.Enabled = !_busy && _service.Enabled && !_service.P1SetupMode;
        AcceptButton = _service.P1SetupMode ? null : _toggle;
    }

    protected override void WndProc(ref Message m)
    {
        if (_service.P1SetupMode && _keyboardRegistered && !_busy)
        {
            try
            {
                if (m.Msg == SetupKeyboardInput.InputMessage && SetupKeyboardInput.Read(m.LParam) is { } input)
                    _service.ForwardSetupKey(input.Key, input.Pressed);
                else if (m.Msg == SetupKeyboardInput.DeviceChangeMessage && m.WParam.ToInt64() == 2)
                    _service.ReleaseSetupKeys(); // Avoid a held button after keyboard unplug.
            }
            catch (Exception error)
            {
                try { _service.SetP1SetupMode(false); } catch { _service.Disable(); }
                RefreshStatus();
                ShowError(error.Message);
            }
        }
        // Let WinForms/DefWindowProc clean up WM_INPUT and preserve normal keyboard input.
        base.WndProc(ref m);
    }

    private async void ToggleClicked(object? sender, EventArgs e)
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            if (_service.Enabled) _service.Disable();
            else
            {
                _toggle.Text = "Enabling…";
                _message.Text = "Checking the dummy controller's slot…";
                await _service.EnableAsync();
            }
        }
        catch (Exception error) { ShowError(error.Message); }
        finally { FinishOperation(); }
    }

    private async void InstallClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (MessageBox.Show(this,
                "DungeonCoopFix needs the signed Nefarius ViGEmBus 1.22.0 driver to create a dummy Xbox controller.\n\n" +
                "This is a one-time installation. The project is retired and receives no updates. Its final installer has no updater. " +
                "Windows will ask for administrator permission; normal app use does not need it.\n\n" +
                "Open the bundled official setup wizard?",
                "Install virtual-controller driver", MessageBoxButtons.YesNo, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        SetBusy(true);
        try
        {
            string message = await DriverInstaller.InstallAsync();
            MessageBox.Show(this, message, "Driver setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception error) { ShowError(error.Message); }
        finally { FinishOperation(); }
    }

    private async void ActivateClicked(object? sender, EventArgs e)
    {
        SetBusy(true);
        try
        {
            _message.Text = "Sending L3 to Virtual P1…";
            await _service.ActivateVirtualP1Async();
        }
        catch (Exception error) { ShowError(error.Message); }
        finally { FinishOperation(); }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _toggle.Enabled = _install.Enabled = !busy;
        _activate.Enabled = !busy && _service.Enabled;
        _setupMode.Enabled = !busy && _service.Enabled;
        if (busy) _timer.Stop();
    }

    private void FinishOperation()
    {
        SetBusy(false);
        if (_closeRequested) { Close(); return; }
        RefreshStatus();
        _timer.Start();
    }

    private void ClosingWindow(object? sender, FormClosingEventArgs e)
    {
        if (_busy)
        {
            // Do not dispose a connection while a worker is creating its target.
            _closeRequested = true;
            e.Cancel = true;
            _message.Text = "Finishing the current operation, then closing…";
        }
        else
        {
            _service.Disable();
            SyncSetupMode();
        }
    }

    private void ShowError(string message) => MessageBox.Show(this, message, "DungeonCoopFix", MessageBoxButtons.OK, MessageBoxIcon.Error);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            if (_service.P1SetupMode)
            {
                try { _service.SetP1SetupMode(false); } catch { _service.Disable(); }
            }
            if (_keyboardRegistered)
            {
                // Window destruction also ends delivery if unregister fails on shutdown.
                try { SetupKeyboardInput.Register(IntPtr.Zero, false); } catch { }
                _keyboardRegistered = false;
            }
        }
        base.Dispose(disposing);
    }
}
