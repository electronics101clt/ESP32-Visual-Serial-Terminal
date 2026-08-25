Imports Gtk
Imports GLib

''' <summary>
''' Main window. GTK4 native implementation mirroring the Windows Forms version.
''' Presentation only: the link, the viewer server and the protocol itself live
''' in <see cref="LinkSession"/>, which every host in this repository shares.
''' </summary>
Public Class MainWindow
    Inherits Gtk.ApplicationWindow

    Private Shared ReadOnly BaudRates As Integer() =
        {9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600}

    Private Shared ReadOnly Presets As (Name As String, W As Integer, H As Integer)() = {
        ("1024 x 600", 1024, 600),
        ("800 x 480", 800, 480),
        ("1280 x 720", 1280, 720),
        ("1280 x 800", 1280, 800),
        ("1920 x 1080", 1920, 1080),
        ("480 x 320", 480, 320)
    }

    Private ReadOnly _session As New LinkSession()
    Private ReadOnly _log As New SerialLog()

    Private _webView As WebKit.WebView
    Private _headerBar As Gtk.HeaderBar
    Private _mainBox As Gtk.Box

    ' Menu actions
    Private _portsMenu As Gio.Menu
    Private _baudMenu As Gio.Menu
    Private _viewportMenu As Gio.Menu

    ' State
    Private _selectedPort As String
    Private _baudRate As Integer = SerialTransport.DefaultBaudRate
    Private _viewWidth As Integer = 1024
    Private _viewHeight As Integer = 600
    Private _knownPorts As String() = Array.Empty(Of String)()
    Private _statusDialog As StatusDialog
    Private _isFullscreen As Boolean
    Private _autoConnect As Boolean = True
    Private _assertDtr As Boolean
    Private _assertRts As Boolean
    Private _fitToWindow As Boolean = True
    Private _alwaysOnTop As Boolean

    ' Timer for port scanning
    Private _portScanSourceId As UInteger

    Public Sub New(app As Gtk.Application)
        MyBase.New()
        Application = app
        Title = "ESP32 Visual Serial Terminal"
        DefaultWidth = 1040
        DefaultHeight = 676

        AddHandler _session.LogLine, AddressOf OnSessionLog
        AddHandler _session.StateChanged, AddressOf OnSessionState

        SetupActions()
        BuildUI()

        _session.SetViewport(_viewWidth, _viewHeight)

        Try
            _session.StartServer()
        Catch ex As Exception
            ShowError("Could not start the local viewer server." & vbLf & vbLf & ex.Message)
            Close()
            Return
        End Try

        ConfigureWebView()
        _webView.LoadUri(_session.Server.BaseUrl)

        RefreshPorts()
        UpdateTitle()
        StartPortScanning()

        If _autoConnect Then TryAutoConnect()
    End Sub

#Region "UI Setup"

    Private Sub BuildUI()
        ' Create header bar with menus
        _headerBar = Gtk.HeaderBar.New()
        _headerBar.ShowTitleButtons = True
        Titlebar = _headerBar

        ' Build menus
        BuildMenuBar()

        ' Main content area
        _mainBox = Gtk.Box.New(Gtk.Orientation.Vertical, 0)

        ' WebKit WebView
        _webView = WebKit.WebView.New()
        _webView.Hexpand = True
        _webView.Vexpand = True
        _mainBox.Append(_webView)

        Child = _mainBox

        ' Keyboard shortcuts
        Dim controller = Gtk.EventControllerKey.New()
        AddHandler controller.OnKeyPressed, AddressOf OnKeyPressed
        AddController(controller)
    End Sub

    Private Sub BuildMenuBar()
        Dim menuBar = Gio.Menu.New()

        ' File menu
        Dim fileMenu = Gio.Menu.New()
        fileMenu.Append("E_xit", "win.quit")
        menuBar.AppendSubmenu("_File", fileMenu)

        ' Connection menu
        Dim connectionMenu = Gio.Menu.New()

        _portsMenu = Gio.Menu.New()
        connectionMenu.AppendSubmenu("_Serial Port", _portsMenu)
        connectionMenu.Append("_Refresh Port List", "win.refresh-ports")

        Dim connSep1 = Gio.Menu.New()
        _baudMenu = Gio.Menu.New()
        For Each rate In BaudRates
            _baudMenu.Append(rate.ToString(), $"win.set-baud({rate})")
        Next
        connSep1.AppendSubmenu("_Baud Rate", _baudMenu)
        connSep1.Append("Assert _DTR on open", "win.toggle-dtr")
        connSep1.Append("Assert _RTS on open", "win.toggle-rts")
        connectionMenu.AppendSection(Nothing, connSep1)

        Dim connSep2 = Gio.Menu.New()
        connSep2.Append("_Connect", "win.connect")
        connSep2.Append("_Disconnect", "win.disconnect")
        connectionMenu.AppendSection(Nothing, connSep2)

        Dim connSep3 = Gio.Menu.New()
        connSep3.Append("_Auto-connect when a port appears", "win.toggle-autoconnect")
        connectionMenu.AppendSection(Nothing, connSep3)

        menuBar.AppendSubmenu("_Connection", connectionMenu)

        ' View menu
        Dim viewMenu = Gio.Menu.New()

        _viewportMenu = Gio.Menu.New()
        For Each preset In Presets
            _viewportMenu.Append(preset.Name, $"win.set-viewport({preset.W},{preset.H})")
        Next
        _viewportMenu.Append("Custom...", "win.custom-viewport")
        viewMenu.AppendSubmenu("Device _Resolution", _viewportMenu)

        Dim viewSep1 = Gio.Menu.New()
        viewSep1.Append("_Fit to Window", "win.toggle-fit")
        viewSep1.Append("_Actual Size (resize window to device)", "win.actual-size")
        viewMenu.AppendSection(Nothing, viewSep1)

        Dim viewSep2 = Gio.Menu.New()
        viewSep2.Append("F_ullscreen", "win.fullscreen")
        viewSep2.Append("Always on _Top", "win.toggle-ontop")
        viewMenu.AppendSection(Nothing, viewSep2)

        menuBar.AppendSubmenu("_View", viewMenu)

        ' Tools menu
        Dim toolsMenu = Gio.Menu.New()
        toolsMenu.Append("_Request Page", "win.request-page")
        toolsMenu.Append("_Clear View", "win.clear-view")

        Dim toolsSep1 = Gio.Menu.New()
        toolsSep1.Append("_Serial Log", "win.serial-log")
        toolsSep1.Append("Browser _DevTools", "win.devtools")
        toolsMenu.AppendSection(Nothing, toolsSep1)

        menuBar.AppendSubmenu("_Tools", toolsMenu)

        ' Status menu
        Dim statusMenu = Gio.Menu.New()
        statusMenu.Append("_Connection Status...", "win.status")
        menuBar.AppendSubmenu("_Status", statusMenu)

        ' Help menu
        Dim helpMenu = Gio.Menu.New()
        helpMenu.Append("_About", "win.about")
        menuBar.AppendSubmenu("_Help", helpMenu)

        ' Create menu button for header bar
        Dim menuButton = Gtk.MenuButton.New()
        menuButton.IconName = "open-menu-symbolic"
        menuButton.MenuModel = menuBar
        _headerBar.PackEnd(menuButton)

        ' Also set app menu for traditional menu bar (if desktop shows it)
        Application.SetMenubar(menuBar)
    End Sub

    Private Sub SetupActions()
        ' File actions
        AddSimpleAction("quit", AddressOf DoQuit)

        ' Connection actions
        AddSimpleAction("refresh-ports", AddressOf DoRefreshPorts)
        AddSimpleAction("connect", AddressOf DoConnect)
        AddSimpleAction("disconnect", AddressOf DoDisconnect)
        AddStatefulAction("toggle-autoconnect", _autoConnect, AddressOf DoToggleAutoConnect)
        AddStatefulAction("toggle-dtr", _assertDtr, AddressOf DoToggleDtr)
        AddStatefulAction("toggle-rts", _assertRts, AddressOf DoToggleRts)
        AddParameterizedAction("set-baud", AddressOf DoSetBaud)
        AddParameterizedAction("set-port", AddressOf DoSetPort)

        ' View actions
        AddParameterizedAction("set-viewport", AddressOf DoSetViewport)
        AddSimpleAction("custom-viewport", AddressOf DoCustomViewport)
        AddStatefulAction("toggle-fit", _fitToWindow, AddressOf DoToggleFit)
        AddSimpleAction("actual-size", AddressOf DoActualSize)
        AddSimpleAction("fullscreen", AddressOf DoToggleFullscreen)
        AddStatefulAction("toggle-ontop", _alwaysOnTop, AddressOf DoToggleOnTop)

        ' Tools actions
        AddSimpleAction("request-page", AddressOf DoRequestPage)
        AddSimpleAction("clear-view", AddressOf DoClearView)
        AddSimpleAction("serial-log", AddressOf DoShowSerialLog)
        AddSimpleAction("devtools", AddressOf DoShowDevTools)

        ' Status actions
        AddSimpleAction("status", AddressOf DoShowStatus)

        ' Help actions
        AddSimpleAction("about", AddressOf DoShowAbout)
    End Sub

    Private Sub AddSimpleAction(name As String, handler As Action)
        Dim action = Gio.SimpleAction.New(name, Nothing)
        AddHandler action.OnActivate, Sub(s, e) handler()
        Dim actionMap = DirectCast(Me, Gio.ActionMap)
        actionMap.AddAction(action)
    End Sub

    Private Sub AddStatefulAction(name As String, initialState As Boolean, handler As Action)
        Dim action = Gio.SimpleAction.NewStateful(name, Nothing, GLib.Variant.NewBoolean(initialState))
        AddHandler action.OnActivate, Sub(s, e)
            Dim current = action.State.GetBoolean()
            action.State = GLib.Variant.NewBoolean(Not current)
            handler()
        End Sub
        Dim actionMap = DirectCast(Me, Gio.ActionMap)
        actionMap.AddAction(action)
    End Sub

    Private Sub AddParameterizedAction(name As String, handler As Action(Of String))
        Dim action = Gio.SimpleAction.New(name, GLib.VariantType.String_)
        AddHandler action.OnActivate, Sub(s, e)
            Dim outLen As UIntPtr = Nothing
            Dim param = e.Parameter.GetString(outLen)
            handler(param)
        End Sub
        Dim actionMap = DirectCast(Me, Gio.ActionMap)
        actionMap.AddAction(action)
    End Sub

    Private Sub ConfigureWebView()
        Dim settings = _webView.Settings
        settings.EnableDeveloperExtras = True
        settings.EnableJavascript = True
        settings.ZoomLevel = 1.0
    End Sub

#End Region

#Region "Actions"

    Private Sub DoQuit()
        Close()
    End Sub

    Private Sub DoRefreshPorts()
        RefreshPorts()
    End Sub

    Private Sub DoConnect()
        If String.IsNullOrEmpty(_selectedPort) Then
            RefreshPorts()
            TryAutoConnect()
        Else
            Connect()
        End If
    End Sub

    Private Sub DoDisconnect()
        Disconnect()
    End Sub

    Private Sub DoToggleAutoConnect()
        _autoConnect = Not _autoConnect
    End Sub

    Private Sub DoToggleDtr()
        _assertDtr = Not _assertDtr
        If _session.IsOpen Then Reconnect()
    End Sub

    Private Sub DoToggleRts()
        _assertRts = Not _assertRts
        If _session.IsOpen Then Reconnect()
    End Sub

    Private Sub DoSetBaud(param As String)
        If Integer.TryParse(param, _baudRate) Then
            If _session.IsOpen Then Reconnect()
        End If
    End Sub

    Private Sub DoSetPort(param As String)
        _selectedPort = param
        Connect()
    End Sub

    Private Sub DoSetViewport(param As String)
        Dim parts = param.Split(","c)
        If parts.Length = 2 Then
            Dim w, h As Integer
            If Integer.TryParse(parts(0), w) AndAlso Integer.TryParse(parts(1), h) Then
                ApplyViewport(w, h)
            End If
        End If
    End Sub

    Private Sub DoCustomViewport()
        Dim dialog As New ViewportDialog(Me, _viewWidth, _viewHeight)
        AddHandler dialog.OnResponse, Sub(s, e)
            If e.ResponseId = Gtk.ResponseType.Ok Then
                ApplyViewport(dialog.ViewWidth, dialog.ViewHeight)
            End If
            dialog.Destroy()
        End Sub
        dialog.Show()
    End Sub

    Private Sub DoToggleFit()
        _fitToWindow = Not _fitToWindow
    End Sub

    Private Sub DoActualSize()
        ResizeToViewport()
    End Sub

    Private Sub DoToggleFullscreen()
        ToggleFullscreen()
    End Sub

    Private Sub DoToggleOnTop()
        _alwaysOnTop = Not _alwaysOnTop
        ' GTK4 doesn't have direct always-on-top; would need platform-specific code
    End Sub

    Private Sub DoRequestPage()
        If _session.IsOpen Then
            _session.RequestPage()
        Else
            UpdateTitle("not connected")
        End If
    End Sub

    Private Sub DoClearView()
        _session.ClearPage()
    End Sub

    Private Sub DoShowSerialLog()
        _log.Show(Me)
    End Sub

    Private Sub DoShowDevTools()
        Dim inspector = _webView.GetInspector()
        inspector.Show()
    End Sub

    Private Sub DoShowStatus()
        If _statusDialog Is Nothing Then
            _statusDialog = New StatusDialog(Me, AddressOf _session.Snapshot)
            AddHandler _statusDialog.OnCloseRequest, Function(s, e)
                _statusDialog = Nothing
                Return False
            End Function
            _statusDialog.Show()
        Else
            _statusDialog.Present()
        End If
    End Sub

    Private Sub DoShowAbout()
        Dim v = Reflection.Assembly.GetExecutingAssembly().GetName().Version
        Dim dialog = Gtk.MessageDialog.New(
            Me,
            Gtk.DialogFlags.Modal Or Gtk.DialogFlags.DestroyWithParent,
            Gtk.MessageType.Info,
            Gtk.ButtonsType.Ok,
            $"ESP32 Visual Serial Terminal {v}" & vbLf & vbLf &
            "Renders HTML pushed by a device over a serial link," & vbLf &
            "at the exact pixel dimensions of a target display." & vbLf & vbLf &
            $"Viewer server: {_session.Server.BaseUrl}" & vbLf &
            "Protocol: see PROTOCOL.md")
        dialog.Title = "About"
        AddHandler dialog.OnResponse, Sub(s, e) dialog.Destroy()
        dialog.Show()
    End Sub

#End Region

#Region "Serial ports"

    Private Sub RefreshPorts()
        _knownPorts = LinuxSerialPorts.Enumerate().Select(Function(p) p.Path).ToArray()
        RebuildPortsMenu()
    End Sub

    Private Sub RebuildPortsMenu()
        _portsMenu.RemoveAll()

        If _knownPorts.Length = 0 Then
            _portsMenu.Append("(no serial ports found)", Nothing)
            Return
        End If

        For Each portName In _knownPorts
            _portsMenu.Append(portName, $"win.set-port('{portName}')")
        Next
    End Sub

    Private Sub StartPortScanning()
        _portScanSourceId = GLib.Functions.TimeoutAdd(0, 1500, AddressOf OnPortScanTick)
    End Sub

    Private Function OnPortScanTick() As Boolean
        Dim current = LinuxSerialPorts.Enumerate().Select(Function(p) p.Path).ToArray()
        If Not current.SequenceEqual(_knownPorts) Then
            Dim appeared = current.Except(_knownPorts, StringComparer.OrdinalIgnoreCase).ToArray()
            _knownPorts = current
            RebuildPortsMenu()

            If Not _session.IsOpen AndAlso _autoConnect AndAlso appeared.Length > 0 Then
                _selectedPort = appeared(0)
                Connect()
            End If
        End If
        Return True ' Continue timer
    End Function

    Private Sub TryAutoConnect()
        If _session.IsOpen Then Return
        If _knownPorts.Length = 0 Then Return

        _selectedPort = If(_selectedPort, _knownPorts(0))
        Connect()
    End Sub

    Private Sub Connect()
        If String.IsNullOrEmpty(_selectedPort) Then
            UpdateTitle("no port selected")
            Return
        End If

        If Not LinuxSerialPorts.CanAccess(_selectedPort) Then
            ShowError($"Cannot open {_selectedPort}: permission denied." & vbLf & vbLf &
                     "Serial devices belong to the 'dialout' group. Add yourself and log back in:" & vbLf &
                     "    sudo usermod -aG dialout $USER")
            Return
        End If

        Try
            _session.Open(_selectedPort, _baudRate, _assertDtr, _assertRts)
        Catch ex As Exception
            _log.Add($"--- open failed: {ex.Message} ---")
            UpdateTitle("connect failed")
            ShowError($"Could not open {_selectedPort}." & vbLf & vbLf & ex.Message)
        End Try
    End Sub

    Private Sub Reconnect()
        Dim port = _selectedPort
        Disconnect()
        _selectedPort = port
        Connect()
    End Sub

    Private Sub Disconnect()
        _session.Close()
    End Sub

#End Region

#Region "Session events"

    Private Sub OnSessionLog(sender As Object, text As String)
        GLib.Functions.IdleAdd(0, Function()
            _log.Add(text)
            Return False
        End Function)
    End Sub

    Private Sub OnSessionState(sender As Object, state As String)
        GLib.Functions.IdleAdd(0, Function()
            UpdateTitle(state)
            Return False
        End Function)
    End Sub

#End Region

#Region "View"

    Private Sub ApplyViewport(w As Integer, h As Integer)
        _viewWidth = w
        _viewHeight = h
        _session.SetViewport(w, h)
        UpdateTitle()

        ' Reload to apply new viewport size
        _webView.Reload()

        If Not _fitToWindow Then ResizeToViewport()
    End Sub

    Private Sub ResizeToViewport()
        If _isFullscreen Then Return

        ' Add some padding for window chrome
        Dim chromeW = 40
        Dim chromeH = 80

        SetDefaultSize(_viewWidth + chromeW, _viewHeight + chromeH)
    End Sub

    Private Sub ToggleFullscreen()
        If _isFullscreen Then
            Unfullscreen()
            _isFullscreen = False
        Else
            Fullscreen()
            _isFullscreen = True
        End If
    End Sub

    Private Function OnKeyPressed(controller As Gtk.EventControllerKey, keyval As UInteger, keycode As UInteger, state As Gdk.ModifierType) As Boolean
        Select Case keyval
            Case Gdk.Constants.KEY_Escape
                If _isFullscreen Then
                    ToggleFullscreen()
                    Return True
                End If

            Case Gdk.Constants.KEY_F11
                ToggleFullscreen()
                Return True

            Case Gdk.Constants.KEY_F5
                RefreshPorts()
                Return True

            Case Gdk.Constants.KEY_F6
                If _session.IsOpen Then _session.RequestPage()
                Return True

            Case Gdk.Constants.KEY_F12
                DoShowDevTools()
                Return True
        End Select

        ' Ctrl+key shortcuts
        If (state And Gdk.ModifierType.ControlMask) <> 0 Then
            Select Case keyval
                Case Gdk.Constants.KEY_o, Gdk.Constants.KEY_O
                    DoConnect()
                    Return True

                Case Gdk.Constants.KEY_l, Gdk.Constants.KEY_L
                    DoShowSerialLog()
                    Return True

                Case Gdk.Constants.KEY_i, Gdk.Constants.KEY_I
                    DoShowStatus()
                    Return True

                Case Gdk.Constants.KEY_0
                    ResizeToViewport()
                    Return True
            End Select
        End If

        Return False
    End Function

#End Region

#Region "Helpers"

    Private Sub UpdateTitle(Optional state As String = Nothing)
        Dim shown = If(state, _session.State)
        Dim port = If(_session.IsOpen, $" - {_selectedPort} @ {_baudRate}", String.Empty)
        Title = $"ESP32 Visual Serial Terminal{port} - {shown}"
    End Sub

    Private Sub ShowError(message As String)
        Dim dialog = Gtk.MessageDialog.New(
            Me,
            Gtk.DialogFlags.Modal Or Gtk.DialogFlags.DestroyWithParent,
            Gtk.MessageType.Error,
            Gtk.ButtonsType.Ok,
            message)
        dialog.Title = "Error"
        AddHandler dialog.OnResponse, Sub(s, e) dialog.Destroy()
        dialog.Show()
    End Sub

#End Region

    Protected Overrides Sub Dispose(disposing As Boolean)
        If _portScanSourceId > 0 Then
            GLib.Functions.SourceRemove(_portScanSourceId)
        End If
        _session.Dispose()
        _log.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
