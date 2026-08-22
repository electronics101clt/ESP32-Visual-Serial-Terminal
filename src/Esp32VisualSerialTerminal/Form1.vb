Imports System.Text.Json
Imports System.Threading
Imports System.Windows.Forms

''' <summary>
''' Main window. Owns the serial link, the loopback viewer server and the
''' browser control, and routes messages between them.
''' </summary>
Public Class Form1

    ' Re-asked at this cadence until the device answers with a page. A request
    ' that is dropped -- because the device was still booting, or the line
    ' glitched -- is otherwise never retried, and the viewer sits blank with no
    ' indication anything is wrong.
    Private Const PageRequestIntervalMs As Integer = 3000

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

    Private ReadOnly _serial As New SerialTransport()
    Private ReadOnly _server As New LocalServer()
    Private ReadOnly _log As New SerialLog()

    ' Fully qualified: System.Threading is imported for Interlocked, and both
    ' namespaces define a Timer.
    Private WithEvents PageRequestTimer As New System.Windows.Forms.Timer() With {.Interval = PageRequestIntervalMs}
    Private WithEvents PortScanTimer As New System.Windows.Forms.Timer() With {.Interval = 1500}

    Private _selectedPort As String
    Private _baudRate As Integer = SerialTransport.DefaultBaudRate
    Private _viewWidth As Integer = 1024
    Private _viewHeight As Integer = 600
    Private _pageReceived As Boolean
    Private _awaitingAck As Boolean
    Private _knownPorts As String() = Array.Empty(Of String)()

    ' Reported through Status -> Connection Status rather than a permanent bar.
    Private _linkState As String = "disconnected"
    Private _bytesIn As Long
    Private _bytesOut As Long
    Private _framesOk As Long
    Private _framesRejected As Long
    Private _statusDialog As StatusDialog

    Private _isFullscreen As Boolean
    Private _preFullscreenBounds As Drawing.Rectangle
    Private _preFullscreenState As FormWindowState

    Public Sub New()
        InitializeComponent()
        BuildBaudMenu()
        BuildViewportMenu()

        AddHandler _serial.LineReceived, AddressOf OnSerialLine
        AddHandler _serial.Disconnected, AddressOf OnSerialDisconnected
        AddHandler _serial.BytesTransferred, AddressOf OnBytesTransferred

        AddHandler _server.ViewerEvent, AddressOf OnViewerEvent
        AddHandler _server.PageRequested, AddressOf OnViewerRequestedPage
    End Sub

    Private Async Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        _server.SetViewport(_viewWidth, _viewHeight)

        Try
            _server.Start()
        Catch ex As Exception
            MessageBox.Show(Me,
                "Could not start the local viewer server." & vbCrLf & vbCrLf & ex.Message,
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
            Close()
            Return
        End Try

        Try
            Await Browser.EnsureCoreWebView2Async()
            ConfigureBrowser()
            Browser.CoreWebView2.Navigate(_server.BaseUrl)
        Catch ex As Exception
            MessageBox.Show(Me,
                "The WebView2 runtime could not be initialised." & vbCrLf & vbCrLf &
                ex.Message & vbCrLf & vbCrLf &
                "Install the Microsoft Edge WebView2 Runtime and restart.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try

        RefreshPorts()
        UpdateViewportLabel()
        PortScanTimer.Start()

        If AutoConnectMenuItem.Checked Then TryAutoConnect()
    End Sub

    Private Sub ConfigureBrowser()
        Dim s = Browser.CoreWebView2.Settings
        s.AreDefaultContextMenusEnabled = True
        s.AreDevToolsEnabled = True
        s.IsStatusBarEnabled = False
        s.IsZoomControlEnabled = False
        s.AreBrowserAcceleratorKeysEnabled = False

        ' Zoom is deliberately pinned. Changing it would resize the CSS
        ' viewport and the emulated layout would stop matching the device.
        ' Scaling is done inside the shell by transforming a fixed-size stage,
        ' which leaves layout untouched.
        Browser.ZoomFactor = 1.0
        AddHandler Browser.ZoomFactorChanged,
            Sub()
                If Browser.ZoomFactor <> 1.0 Then Browser.ZoomFactor = 1.0
            End Sub
    End Sub

#Region "Menu construction"

    Private Sub BuildBaudMenu()
        BaudMenu.DropDownItems.Clear()

        ' Named 'baud' rather than 'rate': VB resolves a bare 'rate' to the
        ' Rate() financial function before the loop variable.
        For Each baud In BaudRates
            Dim captured = baud
            Dim item As New ToolStripMenuItem(baud.ToString()) With {
                .Checked = (baud = _baudRate),
                .Tag = captured
            }
            AddHandler item.Click,
                Sub()
                    _baudRate = captured
                    For Each other As ToolStripMenuItem In BaudMenu.DropDownItems
                        other.Checked = (CInt(other.Tag) = _baudRate)
                    Next
                    If _serial.IsOpen Then Reconnect()
                End Sub
            BaudMenu.DropDownItems.Add(item)
        Next
    End Sub

    Private Sub BuildViewportMenu()
        ViewportMenu.DropDownItems.Clear()

        For Each preset In Presets
            Dim w = preset.W
            Dim h = preset.H
            Dim item As New ToolStripMenuItem(preset.Name) With {
                .Checked = (w = _viewWidth AndAlso h = _viewHeight)
            }
            AddHandler item.Click, Sub() ApplyViewport(w, h)
            ViewportMenu.DropDownItems.Add(item)
        Next

        ViewportMenu.DropDownItems.Add(New ToolStripSeparator())

        Dim custom As New ToolStripMenuItem("Custom...")
        AddHandler custom.Click, AddressOf PromptCustomViewport
        ViewportMenu.DropDownItems.Add(custom)
    End Sub

    Private Sub SyncViewportChecks()
        For Each item In ViewportMenu.DropDownItems.OfType(Of ToolStripMenuItem)()
            Dim parts = item.Text.Split("x"c)
            If parts.Length <> 2 Then
                item.Checked = False
                Continue For
            End If

            Dim w, h As Integer
            item.Checked = Integer.TryParse(parts(0).Trim(), w) AndAlso
                           Integer.TryParse(parts(1).Trim(), h) AndAlso
                           w = _viewWidth AndAlso h = _viewHeight
        Next
    End Sub

#End Region

#Region "Serial ports"

    Private Sub RefreshPorts()
        _knownPorts = SerialTransport.AvailablePorts()
        PortsMenu.DropDownItems.Clear()

        If _knownPorts.Length = 0 Then
            PortsMenu.DropDownItems.Add(New ToolStripMenuItem("(no serial ports found)") With {.Enabled = False})
            Return
        End If

        ' Not 'name': that binds to Form.Name and cannot be a loop variable.
        For Each portName In _knownPorts
            Dim captured = portName
            Dim item As New ToolStripMenuItem(portName) With {
                .Checked = String.Equals(portName, _selectedPort, StringComparison.OrdinalIgnoreCase)
            }
            AddHandler item.Click,
                Sub()
                    _selectedPort = captured
                    For Each other In PortsMenu.DropDownItems.OfType(Of ToolStripMenuItem)()
                        other.Checked = String.Equals(other.Text, _selectedPort, StringComparison.OrdinalIgnoreCase)
                    Next
                    Connect()
                End Sub
            PortsMenu.DropDownItems.Add(item)
        Next
    End Sub

    ''' <summary>
    ''' Polls for port arrival. A USB-serial adapter appearing is not surfaced
    ''' to a plain WinForms app as an event without registering for device
    ''' notifications, and a short poll is far less machinery for the same
    ''' result.
    ''' </summary>
    Private Sub PortScanTimer_Tick(sender As Object, e As EventArgs) Handles PortScanTimer.Tick
        Dim current = SerialTransport.AvailablePorts()
        If current.SequenceEqual(_knownPorts) Then Return

        Dim appeared = current.Except(_knownPorts, StringComparer.OrdinalIgnoreCase).ToArray()
        RefreshPorts()

        If Not _serial.IsOpen AndAlso AutoConnectMenuItem.Checked AndAlso appeared.Length > 0 Then
            _selectedPort = appeared(0)
            Connect()
        End If
    End Sub

    Private Sub TryAutoConnect()
        If _serial.IsOpen Then Return
        If _knownPorts.Length = 0 Then Return

        _selectedPort = If(_selectedPort, _knownPorts(0))
        Connect()
    End Sub

    Private Sub Connect()
        If String.IsNullOrEmpty(_selectedPort) Then
            SetStatus("No port selected")
            Return
        End If

        Try
            _serial.Close()
            _serial.Open(_selectedPort, _baudRate, DtrMenuItem.Checked, RtsMenuItem.Checked)

            _pageReceived = False
            _log.Add($"--- opened {_selectedPort} @ {_baudRate} 8N1 ---")

            SetStatus("connected")
            ConnectMenuItem.Enabled = False
            DisconnectMenuItem.Enabled = True

            ' Ask straight away, then keep asking until a page lands.
            RequestPage()
            PageRequestTimer.Start()

        Catch ex As Exception
            _log.Add($"--- open failed: {ex.Message} ---")
            SetStatus("Connect failed")
            MessageBox.Show(Me,
                $"Could not open {_selectedPort}." & vbCrLf & vbCrLf & ex.Message,
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub Reconnect()
        Dim port = _selectedPort
        Disconnect()
        _selectedPort = port
        Connect()
    End Sub

    Private Sub Disconnect()
        PageRequestTimer.Stop()
        _serial.Close()
        _pageReceived = False

        _server.ClearPage()
        _log.Add("--- closed ---")

        SetStatus("disconnected")
        ConnectMenuItem.Enabled = True
        DisconnectMenuItem.Enabled = False
    End Sub

#End Region

#Region "Protocol"

    Private Sub RequestPage()
        _serial.SendLine(LineCodec.EncodeGetPage())
        _log.Add("> get_page")
    End Sub

    Private Sub PageRequestTimer_Tick(sender As Object, e As EventArgs) Handles PageRequestTimer.Tick
        If _pageReceived OrElse Not _serial.IsOpen Then
            PageRequestTimer.Stop()
            Return
        End If
        RequestPage()
    End Sub

    ''' <summary>
    ''' Runs on the serial reader thread. Marshals to the UI thread before
    ''' touching any control or timer.
    ''' </summary>
    Private Sub OnSerialLine(sender As Object, line As String)
        If IsDisposed OrElse Not IsHandleCreated Then Return

        Try
            BeginInvoke(Sub() HandleLine(line))
        Catch ex As Exception
        End Try
    End Sub

    Private Sub HandleLine(line As String)
        Dim json = LineCodec.Unframe(line)

        If json Is Nothing Then
            ' Either boot log output, which shares the wire and carries no
            ' checksum, or a frame that failed verification. Both are ignored
            ' without a reply: acknowledging a frame we could not verify would
            ' tell the device a corrupted message had been acted on. Staying
            ' silent makes it retry.
            Interlocked.Increment(_framesRejected)
            _log.Add("  " & line)
            Return
        End If

        Interlocked.Increment(_framesOk)

        Dim msg = LineCodec.Decode(json)

        If msg Is Nothing Then
            _log.Add("  ! unparseable payload: " & json)
            Return
        End If

        ' An acknowledgement retires our own in-flight message. It is not a
        ' message to act on, and acknowledging it back would loop forever.
        If msg.Type = MessageType.Ack Then
            _log.Add("< ack")
            _awaitingAck = False
            Return
        End If

        _log.Add("< " & Summarise(msg, json))

        Select Case msg.Type
            Case MessageType.Html
                _pageReceived = True
                PageRequestTimer.Stop()
                _server.PushPage(If(msg.Body, String.Empty))
                SetStatus("Page loaded")

            Case MessageType.Update
                _server.PushUpdate(BuildUpdateJson(msg))

            Case MessageType.Notify
                _server.PushNotify(BuildDialogJson(msg))

            Case MessageType.Dialog
                _server.PushDialog(BuildDialogJson(msg))
        End Select

        ' Acknowledge only after the message has been acted on, so the device
        ' learns the content actually arrived somewhere rather than merely
        ' having been read off the wire.
        SendAck()
    End Sub

    Private Sub SendAck()
        _serial.SendLine(LineCodec.EncodeAck())
        _log.Add("> ack")
    End Sub

    Private Shared Function Summarise(msg As DeviceMessage, raw As String) As String
        Select Case msg.Type
            Case MessageType.Html
                Return $"html ({If(msg.Body, String.Empty).Length} bytes)"
            Case MessageType.Update
                Return $"update {msg.Id} = {msg.Text}"
            Case Else
                Return If(raw.Length > 200, raw.Substring(0, 200) & "...", raw)
        End Select
    End Function

    Private Shared Function BuildUpdateJson(msg As DeviceMessage) As String
        Return JsonSerializer.Serialize(New With {
            Key .id = If(msg.Id, String.Empty),
            Key .text = msg.Text,
            Key .value = msg.Value
        })
    End Function

    Private Shared Function BuildDialogJson(msg As DeviceMessage) As String
        Return JsonSerializer.Serialize(New With {
            Key .id = If(msg.Id, String.Empty),
            Key .title = If(msg.Title, String.Empty),
            Key .message = If(msg.Message, String.Empty)
        })
    End Function

    ''' <summary>Viewer interaction, arriving on an HTTP worker thread.</summary>
    Private Sub OnViewerEvent(sender As Object, e As ViewerEventArgs)
        Dim payload = LineCodec.EncodeEvent(e.Id, e.Action, e.Value)
        _serial.SendLine(payload)
        SafeUi(Sub() _log.Add($"> event {e.Id} {e.Action} {e.Value}"))
    End Sub

    Private Sub OnViewerRequestedPage(sender As Object, file As String)
        _serial.SendLine(LineCodec.EncodeGetPage(file))
        SafeUi(Sub() _log.Add($"> get_page {file}"))
    End Sub

    Private Sub OnSerialDisconnected(sender As Object, reason As String)
        SafeUi(Sub()
                   _log.Add($"--- link lost: {reason} ---")
                   Disconnect()
                   SetStatus("Link lost")
               End Sub)
    End Sub

    ''' <summary>
    ''' Runs on the serial reader thread. Only stores the counters -- the status
    ''' dialog reads them on its own schedule, so there is no reason to marshal
    ''' to the UI thread for every chunk that arrives.
    ''' </summary>
    Private Sub OnBytesTransferred(sender As Object, received As Long, sent As Long)
        Interlocked.Exchange(_bytesIn, received)
        Interlocked.Exchange(_bytesOut, sent)
    End Sub

    Private Sub SafeUi(action As Action)
        If IsDisposed OrElse Not IsHandleCreated Then Return
        Try
            If InvokeRequired Then
                BeginInvoke(action)
            Else
                action()
            End If
        Catch ex As Exception
        End Try
    End Sub

#End Region

#Region "View"

    Private Sub ApplyViewport(w As Integer, h As Integer)
        _viewWidth = w
        _viewHeight = h
        _server.SetViewport(w, h)
        SyncViewportChecks()
        UpdateViewportLabel()

        ' The stage size is baked into the shell at serve time, so the viewer
        ' has to be reloaded for a resolution change to take. The current page
        ' is cached server-side and replays immediately.
        Browser.CoreWebView2?.Reload()

        If Not FitWindowMenuItem.Checked Then ResizeToViewport()
    End Sub

    Private Sub UpdateViewportLabel()
        ' Kept as the single place the resolution change is announced; the
        ' status dialog reads the values directly.
        SetStatus(_linkState)
    End Sub

    ''' <summary>
    ''' Sizes the window so the rendered area is exactly the device's pixel
    ''' count, then pulls it back to fit if the desktop is too small to hold it
    ''' -- in which case the shell scales down on its own.
    ''' </summary>
    Private Sub ResizeToViewport()
        If _isFullscreen Then Return

        Dim chromeW = Width - Browser.ClientSize.Width
        Dim chromeH = Height - Browser.ClientSize.Height

        Dim target As New Drawing.Size(_viewWidth + chromeW, _viewHeight + chromeH)
        Dim work = Screen.FromControl(Me).WorkingArea

        WindowState = FormWindowState.Normal
        Size = New Drawing.Size(
            Math.Min(target.Width, work.Width),
            Math.Min(target.Height, work.Height))

        Location = New Drawing.Point(
            work.Left + Math.Max(0, (work.Width - Size.Width) \ 2),
            work.Top + Math.Max(0, (work.Height - Size.Height) \ 2))
    End Sub

    Private Sub ToggleFullscreen()
        If _isFullscreen Then
            _isFullscreen = False
            FormBorderStyle = FormBorderStyle.Sizable
            MenuStrip.Visible = True
            WindowState = _preFullscreenState
            If _preFullscreenState = FormWindowState.Normal Then Bounds = _preFullscreenBounds
        Else
            _isFullscreen = True
            _preFullscreenBounds = Bounds
            _preFullscreenState = WindowState

            MenuStrip.Visible = False
            WindowState = FormWindowState.Normal
            FormBorderStyle = FormBorderStyle.None
            Bounds = Screen.FromControl(Me).Bounds
        End If

        FullscreenMenuItem.Checked = _isFullscreen
    End Sub

    ''' <summary>
    ''' Escape leaves fullscreen. Without this the menu is hidden and there is
    ''' no visible way back.
    ''' </summary>
    Private Sub Form1_KeyDown(sender As Object, e As KeyEventArgs) Handles MyBase.KeyDown
        If e.KeyCode = Keys.Escape AndAlso _isFullscreen Then
            ToggleFullscreen()
            e.Handled = True
        ElseIf e.KeyCode = Keys.F11 Then
            ToggleFullscreen()
            e.Handled = True
        End If
    End Sub

#End Region

#Region "Menu handlers"

    Private Sub ExitMenuItem_Click(sender As Object, e As EventArgs) Handles ExitMenuItem.Click
        Close()
    End Sub

    Private Sub RefreshPortsMenuItem_Click(sender As Object, e As EventArgs) Handles RefreshPortsMenuItem.Click
        RefreshPorts()
    End Sub

    Private Sub ConnectMenuItem_Click(sender As Object, e As EventArgs) Handles ConnectMenuItem.Click
        If String.IsNullOrEmpty(_selectedPort) Then
            RefreshPorts()
            TryAutoConnect()
        Else
            Connect()
        End If
    End Sub

    Private Sub DisconnectMenuItem_Click(sender As Object, e As EventArgs) Handles DisconnectMenuItem.Click
        Disconnect()
    End Sub

    Private Sub FitWindowMenuItem_Click(sender As Object, e As EventArgs) Handles FitWindowMenuItem.Click
        If Not FitWindowMenuItem.Checked Then ResizeToViewport()
    End Sub

    Private Sub ActualSizeMenuItem_Click(sender As Object, e As EventArgs) Handles ActualSizeMenuItem.Click
        ResizeToViewport()
    End Sub

    Private Sub FullscreenMenuItem_Click(sender As Object, e As EventArgs) Handles FullscreenMenuItem.Click
        ToggleFullscreen()
    End Sub

    Private Sub AlwaysOnTopMenuItem_Click(sender As Object, e As EventArgs) Handles AlwaysOnTopMenuItem.Click
        TopMost = AlwaysOnTopMenuItem.Checked
    End Sub

    Private Sub RequestPageMenuItem_Click(sender As Object, e As EventArgs) Handles RequestPageMenuItem.Click
        If _serial.IsOpen Then
            RequestPage()
        Else
            SetStatus("Not connected")
        End If
    End Sub

    Private Sub ClearViewMenuItem_Click(sender As Object, e As EventArgs) Handles ClearViewMenuItem.Click
        _pageReceived = False
        _server.ClearPage()
    End Sub

    Private Sub SerialLogMenuItem_Click(sender As Object, e As EventArgs) Handles SerialLogMenuItem.Click
        _log.Show(Me)
    End Sub

    Private Sub DevToolsMenuItem_Click(sender As Object, e As EventArgs) Handles DevToolsMenuItem.Click
        Browser.CoreWebView2?.OpenDevToolsWindow()
    End Sub

    Private Sub AboutMenuItem_Click(sender As Object, e As EventArgs) Handles AboutMenuItem.Click
        Dim v = Reflection.Assembly.GetExecutingAssembly().GetName().Version
        MessageBox.Show(Me,
            $"ESP32 Visual Serial Terminal {v}" & vbCrLf & vbCrLf &
            "Renders HTML pushed by a microcontroller over a serial link," & vbCrLf &
            "at the exact pixel dimensions of a target display." & vbCrLf & vbCrLf &
            $"Viewer server: {_server.BaseUrl}" & vbCrLf &
            "Protocol: see PROTOCOL.md",
            "About", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub PromptCustomViewport(sender As Object, e As EventArgs)
        Using dlg As New ViewportDialog(_viewWidth, _viewHeight)
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                ApplyViewport(dlg.ViewWidth, dlg.ViewHeight)
            End If
        End Using
    End Sub

#End Region

    ''' <summary>
    ''' Records the link state. The window title carries it too, so the state is
    ''' visible without opening anything.
    ''' </summary>
    Private Sub SetStatus(text As String)
        _linkState = text

        Dim port = If(_serial.IsOpen, $" — {_selectedPort} @ {_baudRate}", String.Empty)
        Text = $"ESP32 Visual Serial Terminal{port} — {text}"
    End Sub

    Private Function ReadStatus() As StatusSnapshot
        Return New StatusSnapshot With {
            .LinkState = _linkState,
            .PortName = If(_serial.IsOpen, _selectedPort, Nothing),
            .BaudRate = _baudRate,
            .IsOpen = _serial.IsOpen,
            .ViewWidth = _viewWidth,
            .ViewHeight = _viewHeight,
            .BytesReceived = Interlocked.Read(_bytesIn),
            .BytesSent = Interlocked.Read(_bytesOut),
            .FramesReceived = Interlocked.Read(_framesOk),
            .FramesRejected = Interlocked.Read(_framesRejected),
            .ServerUrl = _server.BaseUrl
        }
    End Function

    Private Sub ConnectionStatusMenuItem_Click(sender As Object, e As EventArgs) Handles ConnectionStatusMenuItem.Click
        If _statusDialog Is Nothing OrElse _statusDialog.IsDisposed Then
            _statusDialog = New StatusDialog(AddressOf ReadStatus)
            AddHandler _statusDialog.FormClosed, Sub() _statusDialog = Nothing
            _statusDialog.Show(Me)
        Else
            _statusDialog.BringToFront()
            _statusDialog.Focus()
        End If
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        PortScanTimer.Stop()
        PageRequestTimer.Stop()
        _serial.Dispose()
        _server.Dispose()
        _log.Dispose()
        MyBase.OnFormClosed(e)
    End Sub

End Class
