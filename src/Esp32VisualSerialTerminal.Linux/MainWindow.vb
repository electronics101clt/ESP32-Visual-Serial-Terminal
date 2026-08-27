Imports Gtk
Imports WebKit

''' <summary>
''' Main window. Faithful port of the Windows Form1 to GTK3 with WebKitGTK.
''' </summary>
Public Class MainWindow
    Inherits Window

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

    Private _webView As WebView
    Private _menuBar As MenuBar
    Private _statusLabel As Label

    Private _selectedPort As String
    Private _baudRate As Integer = SerialTransport.DefaultBaudRate
    Private _viewWidth As Integer = 1024
    Private _viewHeight As Integer = 600
    Private _knownPorts As String() = Array.Empty(Of String)()

    Private _portsMenu As Menu
    Private _baudMenu As Menu
    Private _viewportMenu As Menu
    Private _connectMenuItem As MenuItem
    Private _disconnectMenuItem As MenuItem
    Private _autoConnectItem As CheckMenuItem
    Private _dtrItem As CheckMenuItem
    Private _rtsItem As CheckMenuItem
    Private _fitWindowItem As CheckMenuItem
    Private _fullscreenItem As CheckMenuItem
    Private _alwaysOnTopItem As CheckMenuItem

    Private _statusDialog As StatusDialog
    Private _isFullscreen As Boolean

    Public Sub New()
        MyBase.New("ESP32 Visual Serial Terminal")

        SetDefaultSize(1040, 700)
        SetPosition(WindowPosition.Center)

        AddHandler DeleteEvent, AddressOf OnWindowDelete
        AddHandler KeyPressEvent, AddressOf OnKeyPress

        AddHandler _session.LogLine, AddressOf OnSessionLog
        AddHandler _session.StateChanged, AddressOf OnSessionState

        BuildUi()
        StartPortScanner()
    End Sub

    Private Sub BuildUi()
        Dim vbox As New Box(Orientation.Vertical, 0)

        ' Menu bar
        _menuBar = BuildMenuBar()
        vbox.PackStart(_menuBar, False, False, 0)

        ' WebView
        _webView = New WebView()
        _webView.Settings.EnableDeveloperExtras = True

        Dim scrolled As New ScrolledWindow()
        scrolled.Add(_webView)
        vbox.PackStart(scrolled, True, True, 0)

        ' Status bar
        Dim statusBar As New Box(Orientation.Horizontal, 4)
        statusBar.MarginStart = 8
        statusBar.MarginEnd = 8
        statusBar.MarginTop = 4
        statusBar.MarginBottom = 4

        _statusLabel = New Label("disconnected")
        _statusLabel.Halign = Align.Start
        statusBar.PackStart(_statusLabel, True, True, 0)

        vbox.PackStart(statusBar, False, False, 0)

        Add(vbox)

        ' Start the server and load the viewer
        InitializeSession()
    End Sub

    Private Sub InitializeSession()
        _session.SetViewport(_viewWidth, _viewHeight)

        Try
            _session.StartServer()
            _webView.LoadUri(_session.Server.BaseUrl)
        Catch ex As Exception
            ShowError("Could not start the local viewer server." & Environment.NewLine & ex.Message)
            Return
        End Try

        RefreshPorts()
        UpdateTitle()

        If _autoConnectItem.Active Then TryAutoConnect()
    End Sub

    Private Function BuildMenuBar() As MenuBar
        Dim menuBar As New MenuBar()

        ' File menu
        Dim fileMenu As New Menu()
        Dim fileItem As New MenuItem("_File")
        fileItem.Submenu = fileMenu

        Dim exitItem As New MenuItem("E_xit")
        AddHandler exitItem.Activated, Sub() Application.Quit()
        fileMenu.Append(exitItem)

        menuBar.Append(fileItem)

        ' Connection menu
        Dim connMenu As New Menu()
        Dim connItem As New MenuItem("_Connection")
        connItem.Submenu = connMenu

        Dim portsItem As New MenuItem("_Serial Port")
        _portsMenu = New Menu()
        portsItem.Submenu = _portsMenu
        connMenu.Append(portsItem)

        Dim refreshItem As New MenuItem("_Refresh Port List")
        AddHandler refreshItem.Activated, Sub() RefreshPorts()
        connMenu.Append(refreshItem)

        connMenu.Append(New SeparatorMenuItem())

        Dim baudItem As New MenuItem("_Baud Rate")
        _baudMenu = New Menu()
        baudItem.Submenu = _baudMenu
        BuildBaudMenu()
        connMenu.Append(baudItem)

        _dtrItem = New CheckMenuItem("Assert _DTR on open")
        connMenu.Append(_dtrItem)

        _rtsItem = New CheckMenuItem("Assert _RTS on open")
        connMenu.Append(_rtsItem)

        connMenu.Append(New SeparatorMenuItem())

        _connectMenuItem = New MenuItem("_Connect")
        AddHandler _connectMenuItem.Activated, AddressOf OnConnectClicked
        connMenu.Append(_connectMenuItem)

        _disconnectMenuItem = New MenuItem("_Disconnect")
        _disconnectMenuItem.Sensitive = False
        AddHandler _disconnectMenuItem.Activated, Sub() Disconnect()
        connMenu.Append(_disconnectMenuItem)

        connMenu.Append(New SeparatorMenuItem())

        _autoConnectItem = New CheckMenuItem("_Auto-connect when a port appears")
        _autoConnectItem.Active = True
        connMenu.Append(_autoConnectItem)

        menuBar.Append(connItem)

        ' View menu
        Dim viewMenu As New Menu()
        Dim viewItem As New MenuItem("_View")
        viewItem.Submenu = viewMenu

        Dim vpItem As New MenuItem("Device _Resolution")
        _viewportMenu = New Menu()
        vpItem.Submenu = _viewportMenu
        BuildViewportMenu()
        viewMenu.Append(vpItem)

        viewMenu.Append(New SeparatorMenuItem())

        _fitWindowItem = New CheckMenuItem("_Fit to Window")
        _fitWindowItem.Active = True
        viewMenu.Append(_fitWindowItem)

        Dim actualItem As New MenuItem("_Actual Size (resize window to device)")
        AddHandler actualItem.Activated, Sub() ResizeToViewport()
        viewMenu.Append(actualItem)

        viewMenu.Append(New SeparatorMenuItem())

        _fullscreenItem = New CheckMenuItem("F_ullscreen")
        AddHandler _fullscreenItem.Toggled, AddressOf OnFullscreenToggled
        viewMenu.Append(_fullscreenItem)

        _alwaysOnTopItem = New CheckMenuItem("Always on _Top")
        AddHandler _alwaysOnTopItem.Toggled, Sub() KeepAbove = _alwaysOnTopItem.Active
        viewMenu.Append(_alwaysOnTopItem)

        menuBar.Append(viewItem)

        ' Tools menu
        Dim toolsMenu As New Menu()
        Dim toolsItem As New MenuItem("_Tools")
        toolsItem.Submenu = toolsMenu

        Dim reqPageItem As New MenuItem("_Request Page")
        AddHandler reqPageItem.Activated, Sub()
            If _session.IsOpen Then
                _session.RequestPage()
            Else
                UpdateTitle("not connected")
            End If
        End Sub
        toolsMenu.Append(reqPageItem)

        Dim clearItem As New MenuItem("_Clear View")
        AddHandler clearItem.Activated, Sub() _session.ClearPage()
        toolsMenu.Append(clearItem)

        toolsMenu.Append(New SeparatorMenuItem())

        Dim logItem As New MenuItem("_Serial Log")
        AddHandler logItem.Activated, Sub() _log.Show(Me)
        toolsMenu.Append(logItem)

        Dim devToolsItem As New MenuItem("Browser _DevTools")
        AddHandler devToolsItem.Activated, Sub()
            ' WebKitGTK inspector - right-click context menu also provides this
            Dim settings = _webView.Settings
            settings.EnableDeveloperExtras = True
        End Sub
        toolsMenu.Append(devToolsItem)

        menuBar.Append(toolsItem)

        ' Status menu
        Dim statusMenu As New Menu()
        Dim statusItem As New MenuItem("_Status")
        statusItem.Submenu = statusMenu

        Dim connStatusItem As New MenuItem("_Connection Status...")
        AddHandler connStatusItem.Activated, AddressOf OnConnectionStatusClicked
        statusMenu.Append(connStatusItem)

        menuBar.Append(statusItem)

        ' Help menu
        Dim helpMenu As New Menu()
        Dim helpItem As New MenuItem("_Help")
        helpItem.Submenu = helpMenu

        Dim aboutItem As New MenuItem("_About")
        AddHandler aboutItem.Activated, AddressOf OnAboutClicked
        helpMenu.Append(aboutItem)

        menuBar.Append(helpItem)

        Return menuBar
    End Function

    Private Sub BuildBaudMenu()
        Dim children = _baudMenu.Children
        For i = children.Length - 1 To 0 Step -1
            _baudMenu.Remove(children(i))
        Next

        Dim group As RadioMenuItem = Nothing

        For i = 0 To BaudRates.Length - 1
            Dim baud = BaudRates(i)
            Dim captured = baud
            Dim item As RadioMenuItem

            If group Is Nothing Then
                item = New RadioMenuItem(baud.ToString())
                group = item
            Else
                item = New RadioMenuItem(group, baud.ToString())
            End If

            item.Active = (baud = _baudRate)

            AddHandler item.Toggled, Sub()
                If item.Active Then
                    _baudRate = captured
                    If _session.IsOpen Then Reconnect()
                End If
            End Sub

            _baudMenu.Append(item)
        Next

        _baudMenu.ShowAll()
    End Sub

    Private Sub BuildViewportMenu()
        Dim children = _viewportMenu.Children
        For i = children.Length - 1 To 0 Step -1
            _viewportMenu.Remove(children(i))
        Next

        Dim group As RadioMenuItem = Nothing

        For i = 0 To Presets.Length - 1
            Dim preset = Presets(i)
            Dim w = preset.W
            Dim h = preset.H
            Dim item As RadioMenuItem

            If group Is Nothing Then
                item = New RadioMenuItem(preset.Name)
                group = item
            Else
                item = New RadioMenuItem(group, preset.Name)
            End If

            item.Active = (w = _viewWidth AndAlso h = _viewHeight)

            AddHandler item.Toggled, Sub()
                If item.Active Then ApplyViewport(w, h)
            End Sub

            _viewportMenu.Append(item)
        Next

        _viewportMenu.Append(New SeparatorMenuItem())

        Dim customItem As New MenuItem("Custom...")
        AddHandler customItem.Activated, AddressOf PromptCustomViewport
        _viewportMenu.Append(customItem)

        _viewportMenu.ShowAll()
    End Sub

#Region "Serial Ports"

    Private Sub RefreshPorts()
        _knownPorts = LinuxSerialPorts.GetAvailablePorts()

        Dim children = _portsMenu.Children
        For i = children.Length - 1 To 0 Step -1
            _portsMenu.Remove(children(i))
        Next

        If _knownPorts.Length = 0 Then
            Dim noPortsItem As New MenuItem("(no serial ports found)")
            noPortsItem.Sensitive = False
            _portsMenu.Append(noPortsItem)
        Else
            Dim group As RadioMenuItem = Nothing

            For j = 0 To _knownPorts.Length - 1
                Dim portName = _knownPorts(j)
                Dim captured = portName
                Dim item As RadioMenuItem

                If group Is Nothing Then
                    item = New RadioMenuItem(portName)
                    group = item
                Else
                    item = New RadioMenuItem(group, portName)
                End If

                item.Active = String.Equals(portName, _selectedPort, StringComparison.OrdinalIgnoreCase)

                AddHandler item.Toggled, Sub()
                    If item.Active Then
                        _selectedPort = captured
                        Connect()
                    End If
                End Sub

                _portsMenu.Append(item)
            Next
        End If

        _portsMenu.ShowAll()
    End Sub

    Private Sub StartPortScanner()
        GLib.Timeout.Add(1500, Function()
            Dim current = LinuxSerialPorts.GetAvailablePorts()
            If Not current.SequenceEqual(_knownPorts) Then
                Dim appeared = current.Except(_knownPorts, StringComparer.OrdinalIgnoreCase).ToArray()
                RefreshPorts()

                If Not _session.IsOpen AndAlso _autoConnectItem.Active AndAlso appeared.Length > 0 Then
                    _selectedPort = appeared(0)
                    Connect()
                End If
            End If
            Return True
        End Function)
    End Sub

    Private Sub TryAutoConnect()
        If _session.IsOpen Then Return
        If _knownPorts.Length = 0 Then Return

        _selectedPort = If(_selectedPort, _knownPorts(0))
        Connect()
    End Sub

    Private Sub Connect(Optional showErrorDialog As Boolean = False)
        If String.IsNullOrEmpty(_selectedPort) Then
            UpdateTitle("no port selected")
            Return
        End If

        Try
            _session.Open(_selectedPort, _baudRate, _dtrItem.Active, _rtsItem.Active)
            _connectMenuItem.Sensitive = False
            _disconnectMenuItem.Sensitive = True
        Catch ex As Exception
            _log.Add($"--- open failed: {ex.Message} ---")
            UpdateTitle("connect failed")
            If showErrorDialog Then
                ShowError($"Could not open {_selectedPort}." & Environment.NewLine & ex.Message)
            End If
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
        _connectMenuItem.Sensitive = True
        _disconnectMenuItem.Sensitive = False
    End Sub

    Private Sub OnConnectClicked(sender As Object, e As EventArgs)
        If String.IsNullOrEmpty(_selectedPort) Then
            RefreshPorts()
            TryAutoConnect()
        Else
            Connect(showErrorDialog:=True)
        End If
    End Sub

#End Region

#Region "Session Events"

    Private Sub OnSessionLog(sender As Object, text As String)
        Application.Invoke(Sub() _log.Add(text))
    End Sub

    Private Sub OnSessionState(sender As Object, state As String)
        Application.Invoke(Sub()
            UpdateTitle(state)
            If state = "link lost" Then
                _connectMenuItem.Sensitive = True
                _disconnectMenuItem.Sensitive = False
            End If
        End Sub)
    End Sub

#End Region

#Region "View"

    Private Sub ApplyViewport(w As Integer, h As Integer)
        _viewWidth = w
        _viewHeight = h
        _session.SetViewport(w, h)
        UpdateTitle()

        ' Reload the viewer to pick up the new resolution
        _webView.Reload()

        If Not _fitWindowItem.Active Then ResizeToViewport()
    End Sub

    Private Sub ResizeToViewport()
        If _isFullscreen Then Return

        Dim chromeW = 0
        Dim chromeH = 80 ' Approximate menu + status bar height

        Resize(_viewWidth + chromeW, _viewHeight + chromeH)
    End Sub

    Private Sub OnFullscreenToggled(sender As Object, e As EventArgs)
        If _fullscreenItem.Active Then
            _isFullscreen = True
            _menuBar.Visible = False
            Fullscreen()
        Else
            _isFullscreen = False
            _menuBar.Visible = True
            Unfullscreen()
        End If
    End Sub

    Private Sub PromptCustomViewport(sender As Object, e As EventArgs)
        Using dlg As New ViewportDialog(Me, _viewWidth, _viewHeight)
            If dlg.Run() = CInt(ResponseType.Ok) Then
                ApplyViewport(dlg.ViewWidth, dlg.ViewHeight)
            End If
            dlg.Destroy()
        End Using
    End Sub

#End Region

#Region "Dialogs"

    Private Sub OnConnectionStatusClicked(sender As Object, e As EventArgs)
        If _statusDialog Is Nothing OrElse Not _statusDialog.Visible Then
            _statusDialog = New StatusDialog(Me, AddressOf _session.Snapshot)
            _statusDialog.ShowAll()
        Else
            _statusDialog.Present()
        End If
    End Sub

    Private Sub OnAboutClicked(sender As Object, e As EventArgs)
        Dim dlg As New MessageDialog(Me, DialogFlags.Modal, Gtk.MessageType.Info, ButtonsType.Ok,
            "ESP32 Visual Serial Terminal 0.1.0" & Environment.NewLine & Environment.NewLine &
            "Renders HTML pushed by a device over a serial link," & Environment.NewLine &
            "at the exact pixel dimensions of a target display." & Environment.NewLine & Environment.NewLine &
            $"Viewer server: {_session.Server.BaseUrl}" & Environment.NewLine &
            "Protocol: see PROTOCOL.md")
        dlg.Title = "About"
        dlg.Run()
        dlg.Destroy()
    End Sub

    Private Sub ShowError(message As String)
        Dim dlg As New MessageDialog(Me, DialogFlags.Modal, Gtk.MessageType.Error, ButtonsType.Ok, message)
        dlg.Title = "Error"
        dlg.Run()
        dlg.Destroy()
    End Sub

#End Region

    Private Sub UpdateTitle(Optional state As String = Nothing)
        Dim shown = If(state, _session.State)
        Dim port = If(_session.IsOpen, $" — {_selectedPort} @ {_baudRate}", String.Empty)
        Title = $"ESP32 Visual Serial Terminal{port} — {shown}"
        _statusLabel.Text = shown
    End Sub

    Private Sub OnKeyPress(sender As Object, e As KeyPressEventArgs)
        If e.Event.Key = Gdk.Key.Escape AndAlso _isFullscreen Then
            _fullscreenItem.Active = False
            e.RetVal = True
        ElseIf e.Event.Key = Gdk.Key.F11 Then
            _fullscreenItem.Active = Not _fullscreenItem.Active
            e.RetVal = True
        End If
    End Sub

    Private Sub OnWindowDelete(sender As Object, e As DeleteEventArgs)
        _session.Dispose()
        _log.Dispose()
        Application.Quit()
        e.RetVal = True
    End Sub

End Class
