Imports Microsoft.Web.WebView2.WinForms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Form1
    Inherits System.Windows.Forms.Form

    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    Private components As System.ComponentModel.IContainer

    Friend WithEvents MenuStrip As System.Windows.Forms.MenuStrip
    Friend WithEvents FileMenu As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ExitMenuItem As System.Windows.Forms.ToolStripMenuItem

    Friend WithEvents ConnectionMenu As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents PortsMenu As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents RefreshPortsMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents BaudMenu As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents DtrMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents RtsMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ConnectMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents DisconnectMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents AutoConnectMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ConnSep1 As System.Windows.Forms.ToolStripSeparator
    Friend WithEvents ConnSep2 As System.Windows.Forms.ToolStripSeparator
    Friend WithEvents ConnSep3 As System.Windows.Forms.ToolStripSeparator

    Friend WithEvents ViewMenu As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ViewportMenu As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents FitWindowMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ActualSizeMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents FullscreenMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents AlwaysOnTopMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ViewSep1 As System.Windows.Forms.ToolStripSeparator
    Friend WithEvents ViewSep2 As System.Windows.Forms.ToolStripSeparator

    Friend WithEvents ToolsMenu As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents RequestPageMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ClearViewMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents SerialLogMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents DevToolsMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ToolsSep1 As System.Windows.Forms.ToolStripSeparator

    Friend WithEvents StatusMenu As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ConnectionStatusMenuItem As System.Windows.Forms.ToolStripMenuItem

    Friend WithEvents HelpMenu As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents AboutMenuItem As System.Windows.Forms.ToolStripMenuItem

    Friend WithEvents Browser As WebView2

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container()

        Me.MenuStrip = New System.Windows.Forms.MenuStrip()
        Me.FileMenu = New System.Windows.Forms.ToolStripMenuItem()
        Me.ExitMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ConnectionMenu = New System.Windows.Forms.ToolStripMenuItem()
        Me.PortsMenu = New System.Windows.Forms.ToolStripMenuItem()
        Me.RefreshPortsMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.BaudMenu = New System.Windows.Forms.ToolStripMenuItem()
        Me.DtrMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.RtsMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ConnectMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.DisconnectMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.AutoConnectMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ConnSep1 = New System.Windows.Forms.ToolStripSeparator()
        Me.ConnSep2 = New System.Windows.Forms.ToolStripSeparator()
        Me.ConnSep3 = New System.Windows.Forms.ToolStripSeparator()
        Me.ViewMenu = New System.Windows.Forms.ToolStripMenuItem()
        Me.ViewportMenu = New System.Windows.Forms.ToolStripMenuItem()
        Me.FitWindowMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ActualSizeMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.FullscreenMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.AlwaysOnTopMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ViewSep1 = New System.Windows.Forms.ToolStripSeparator()
        Me.ViewSep2 = New System.Windows.Forms.ToolStripSeparator()
        Me.ToolsMenu = New System.Windows.Forms.ToolStripMenuItem()
        Me.RequestPageMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ClearViewMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.SerialLogMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.DevToolsMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ToolsSep1 = New System.Windows.Forms.ToolStripSeparator()
        Me.StatusMenu = New System.Windows.Forms.ToolStripMenuItem()
        Me.ConnectionStatusMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.HelpMenu = New System.Windows.Forms.ToolStripMenuItem()
        Me.AboutMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.Browser = New WebView2()

        Me.MenuStrip.SuspendLayout()
        CType(Me.Browser, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()

        '
        ' MenuStrip
        '
        Me.MenuStrip.Items.AddRange(New System.Windows.Forms.ToolStripItem() {
            Me.FileMenu, Me.ConnectionMenu, Me.ViewMenu, Me.ToolsMenu, Me.StatusMenu, Me.HelpMenu})
        Me.MenuStrip.Location = New System.Drawing.Point(0, 0)
        Me.MenuStrip.Name = "MenuStrip"
        Me.MenuStrip.Size = New System.Drawing.Size(1040, 24)
        Me.MenuStrip.TabIndex = 0

        '
        ' File
        '
        Me.FileMenu.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {Me.ExitMenuItem})
        Me.FileMenu.Name = "FileMenu"
        Me.FileMenu.Text = "&File"

        Me.ExitMenuItem.Name = "ExitMenuItem"
        Me.ExitMenuItem.Text = "E&xit"

        '
        ' Connection
        '
        Me.ConnectionMenu.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {
            Me.PortsMenu, Me.RefreshPortsMenuItem, Me.ConnSep1,
            Me.BaudMenu, Me.DtrMenuItem, Me.RtsMenuItem, Me.ConnSep2,
            Me.ConnectMenuItem, Me.DisconnectMenuItem, Me.ConnSep3,
            Me.AutoConnectMenuItem})
        Me.ConnectionMenu.Name = "ConnectionMenu"
        Me.ConnectionMenu.Text = "&Connection"

        Me.PortsMenu.Name = "PortsMenu"
        Me.PortsMenu.Text = "&Serial Port"

        Me.RefreshPortsMenuItem.Name = "RefreshPortsMenuItem"
        Me.RefreshPortsMenuItem.Text = "&Refresh Port List"
        Me.RefreshPortsMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F5

        Me.BaudMenu.Name = "BaudMenu"
        Me.BaudMenu.Text = "&Baud Rate"

        Me.DtrMenuItem.Name = "DtrMenuItem"
        Me.DtrMenuItem.Text = "Assert &DTR on open"
        Me.DtrMenuItem.CheckOnClick = True

        Me.RtsMenuItem.Name = "RtsMenuItem"
        Me.RtsMenuItem.Text = "Assert &RTS on open"
        Me.RtsMenuItem.CheckOnClick = True

        Me.ConnectMenuItem.Name = "ConnectMenuItem"
        Me.ConnectMenuItem.Text = "&Connect"
        Me.ConnectMenuItem.ShortcutKeys = CType(System.Windows.Forms.Keys.Control Or System.Windows.Forms.Keys.O, System.Windows.Forms.Keys)

        Me.DisconnectMenuItem.Name = "DisconnectMenuItem"
        Me.DisconnectMenuItem.Text = "&Disconnect"
        Me.DisconnectMenuItem.Enabled = False

        Me.AutoConnectMenuItem.Name = "AutoConnectMenuItem"
        Me.AutoConnectMenuItem.Text = "&Auto-connect when a port appears"
        Me.AutoConnectMenuItem.CheckOnClick = True
        Me.AutoConnectMenuItem.Checked = True

        '
        ' View
        '
        Me.ViewMenu.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {
            Me.ViewportMenu, Me.ViewSep1,
            Me.FitWindowMenuItem, Me.ActualSizeMenuItem, Me.ViewSep2,
            Me.FullscreenMenuItem, Me.AlwaysOnTopMenuItem})
        Me.ViewMenu.Name = "ViewMenu"
        Me.ViewMenu.Text = "&View"

        Me.ViewportMenu.Name = "ViewportMenu"
        Me.ViewportMenu.Text = "Device &Resolution"

        Me.FitWindowMenuItem.Name = "FitWindowMenuItem"
        Me.FitWindowMenuItem.Text = "&Fit to Window"
        Me.FitWindowMenuItem.Checked = True
        Me.FitWindowMenuItem.CheckOnClick = True

        Me.ActualSizeMenuItem.Name = "ActualSizeMenuItem"
        Me.ActualSizeMenuItem.Text = "&Actual Size (resize window to device)"
        Me.ActualSizeMenuItem.ShortcutKeys = CType(System.Windows.Forms.Keys.Control Or System.Windows.Forms.Keys.D0, System.Windows.Forms.Keys)

        Me.FullscreenMenuItem.Name = "FullscreenMenuItem"
        Me.FullscreenMenuItem.Text = "F&ullscreen"
        Me.FullscreenMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F11

        Me.AlwaysOnTopMenuItem.Name = "AlwaysOnTopMenuItem"
        Me.AlwaysOnTopMenuItem.Text = "Always on &Top"
        Me.AlwaysOnTopMenuItem.CheckOnClick = True

        '
        ' Tools
        '
        Me.ToolsMenu.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {
            Me.RequestPageMenuItem, Me.ClearViewMenuItem, Me.ToolsSep1,
            Me.SerialLogMenuItem, Me.DevToolsMenuItem})
        Me.ToolsMenu.Name = "ToolsMenu"
        Me.ToolsMenu.Text = "&Tools"

        Me.RequestPageMenuItem.Name = "RequestPageMenuItem"
        Me.RequestPageMenuItem.Text = "&Request Page"
        Me.RequestPageMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F6

        Me.ClearViewMenuItem.Name = "ClearViewMenuItem"
        Me.ClearViewMenuItem.Text = "&Clear View"

        Me.SerialLogMenuItem.Name = "SerialLogMenuItem"
        Me.SerialLogMenuItem.Text = "&Serial Log"
        Me.SerialLogMenuItem.ShortcutKeys = CType(System.Windows.Forms.Keys.Control Or System.Windows.Forms.Keys.L, System.Windows.Forms.Keys)

        Me.DevToolsMenuItem.Name = "DevToolsMenuItem"
        Me.DevToolsMenuItem.Text = "Browser &DevTools"
        Me.DevToolsMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F12

        '
        ' Status
        '
        Me.StatusMenu.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {Me.ConnectionStatusMenuItem})
        Me.StatusMenu.Name = "StatusMenu"
        Me.StatusMenu.Text = "&Status"

        Me.ConnectionStatusMenuItem.Name = "ConnectionStatusMenuItem"
        Me.ConnectionStatusMenuItem.Text = "&Connection Status..."
        Me.ConnectionStatusMenuItem.ShortcutKeys = CType(System.Windows.Forms.Keys.Control Or System.Windows.Forms.Keys.I, System.Windows.Forms.Keys)

        '
        ' Help
        '
        Me.HelpMenu.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {Me.AboutMenuItem})
        Me.HelpMenu.Name = "HelpMenu"
        Me.HelpMenu.Text = "&Help"

        Me.AboutMenuItem.Name = "AboutMenuItem"
        Me.AboutMenuItem.Text = "&About"

        '
        ' StatusStrip
        '
        '
        ' Browser
        '
        Me.Browser.AllowExternalDrop = False
        Me.Browser.CreationProperties = Nothing
        Me.Browser.DefaultBackgroundColor = System.Drawing.Color.FromArgb(20, 22, 26)
        Me.Browser.Dock = System.Windows.Forms.DockStyle.Fill
        Me.Browser.Location = New System.Drawing.Point(0, 24)
        Me.Browser.Name = "Browser"
        Me.Browser.Size = New System.Drawing.Size(1040, 630)
        Me.Browser.TabIndex = 1
        Me.Browser.ZoomFactor = 1.0R

        '
        ' Form1
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(96.0!, 96.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi
        Me.BackColor = System.Drawing.Color.FromArgb(20, 22, 26)
        Me.ClientSize = New System.Drawing.Size(1040, 676)
        Me.Controls.Add(Me.Browser)
        Me.Controls.Add(Me.MenuStrip)
        Me.MainMenuStrip = Me.MenuStrip
        Me.KeyPreview = True
        Me.MinimumSize = New System.Drawing.Size(420, 260)
        Me.Name = "Form1"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "ESP32 Visual Serial Terminal"

        Me.MenuStrip.ResumeLayout(False)
        Me.MenuStrip.PerformLayout()
        CType(Me.Browser, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()
    End Sub

End Class
