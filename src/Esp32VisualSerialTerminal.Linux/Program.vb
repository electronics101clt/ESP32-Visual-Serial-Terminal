Imports System.Threading

''' <summary>
''' Linux host. Runs the shared protocol session and shows the page in a browser
''' window, with the terminal itself acting as the control surface in place of a
''' menu bar.
''' </summary>
Public Module Program

    Private ReadOnly Session As New LinkSession()
    Private ReadOnly LogGate As New Object()
    Private ReadOnly Recent As New Queue(Of String)()

    Private Const MaxRecent As Integer = 400

    Private EchoLog As Boolean
    Private Quitting As Boolean

    Public Function Main(argv As String()) As Integer
        Dim opts As Options
        Try
            opts = Options.Parse(argv)
        Catch ex As Exception
            Console.Error.WriteLine("esp32-visual-serial-terminal: " & ex.Message)
            Console.Error.WriteLine("Try --help.")
            Return 2
        End Try

        If opts.ShowHelp Then
            Console.WriteLine(Options.HelpText())
            Return 0
        End If

        If opts.ListPorts Then
            PrintPorts()
            Return 0
        End If

        AddHandler Session.LogLine, AddressOf OnLog
        AddHandler Session.StateChanged, AddressOf OnState

        Session.SetViewport(opts.Width, opts.Height)

        Try
            Session.StartServer(opts.ListenPort)
        Catch ex As Exception
            Console.Error.WriteLine("Could not start the viewer server: " & ex.Message)
            Return 1
        End Try

        Console.WriteLine($"Viewer      {Session.Server.BaseUrl}")
        Console.WriteLine($"Screen      {opts.Width} x {opts.Height}")

        Dim device = ResolvePort(opts)
        If device IsNot Nothing Then
            OpenPort(device, opts)
        Else
            Console.WriteLine("Port        none found — plug the device in, then press 'c'")
        End If

        If Not opts.NoBrowser Then LaunchViewer(opts)

        Console.WriteLine()
        Console.WriteLine("Keys: [c]onnect  [d]isconnect  [r]equest page  [x]clear  [l]og  [s]tatus  [q]uit")
        Console.WriteLine()

        AddHandler Console.CancelKeyPress,
            Sub(s, e)
                e.Cancel = True
                Quitting = True
            End Sub

        RunConsoleLoop(opts)

        Session.Dispose()
        Console.WriteLine("Stopped.")
        Return 0
    End Function

#Region "Console loop"

    Private Sub RunConsoleLoop(opts As Options)
        ' Without a terminal attached -- run from a launcher, a service, or with
        ' input redirected -- there are no keys to read. Idle instead of spinning
        ' on a console that will never answer.
        If Console.IsInputRedirected Then
            Idle()
            Return
        End If

        While Not Quitting
            Dim pending As Boolean
            Try
                pending = Console.KeyAvailable
            Catch ex As Exception
                ' No console is attached at all, which throws rather than
                ' reporting no key. Fall back to idling so the viewer keeps
                ' running instead of the process dying on a keystroke check.
                Idle()
                Return
            End Try

            If Not pending Then
                Thread.Sleep(60)
                Continue While
            End If

            Dim key = Console.ReadKey(intercept:=True).KeyChar

            Select Case Char.ToLowerInvariant(key)
                Case "q"c
                    Quitting = True

                Case "c"c
                    If Session.IsOpen Then
                        Console.WriteLine("Already connected.")
                    Else
                        Dim device = ResolvePort(opts)
                        If device Is Nothing Then
                            Console.WriteLine("No serial device found.")
                        Else
                            OpenPort(device, opts)
                        End If
                    End If

                Case "d"c
                    Session.Close()

                Case "r"c
                    If Session.IsOpen Then Session.RequestPage() Else Console.WriteLine("Not connected.")

                Case "x"c
                    Session.ClearPage()
                    Console.WriteLine("View cleared.")

                Case "l"c
                    EchoLog = Not EchoLog
                    Console.WriteLine(If(EchoLog, "Log echo on.", "Log echo off."))
                    If EchoLog Then DumpRecent()

                Case "s"c
                    PrintStatus()

                Case "?"c, "h"c
                    Console.WriteLine("Keys: [c]onnect  [d]isconnect  [r]equest page  [x]clear  [l]og  [s]tatus  [q]uit")
            End Select
        End While
    End Sub

    Private Sub Idle()
        While Not Quitting
            Thread.Sleep(250)
        End While
    End Sub

    Private Sub PrintStatus()
        Dim s = Session.Snapshot()
        Console.WriteLine()
        Console.WriteLine($"  Link      {s.LinkState}")
        Console.WriteLine($"  Port      {If(String.IsNullOrEmpty(s.PortName), "none", s.PortName)}")
        Console.WriteLine($"  Baud      {If(s.IsOpen, s.BaudRate.ToString(), "-")}")
        Console.WriteLine($"  Screen    {s.ViewWidth} x {s.ViewHeight}")
        Console.WriteLine($"  Received  {Humanise(s.BytesReceived)}")
        Console.WriteLine($"  Sent      {Humanise(s.BytesSent)}")
        Console.WriteLine($"  Frames    {s.FramesReceived} accepted, {s.FramesRejected} rejected")
        Console.WriteLine($"  Viewer    {s.ServerUrl}")
        Console.WriteLine()
    End Sub

    Private Function Humanise(bytes As Long) As String
        If bytes < 1024 Then Return $"{bytes} B"
        If bytes < 1024L * 1024L Then Return $"{bytes / 1024.0:0.0} KB"
        Return $"{bytes / (1024.0 * 1024.0):0.0} MB"
    End Function

#End Region

#Region "Ports"

    Private Sub PrintPorts()
        Dim ports = LinuxSerialPorts.Enumerate(includeOnboard:=True)
        If ports.Count = 0 Then
            Console.WriteLine("No serial devices found.")
            Return
        End If

        For Each p In ports
            Dim access = If(LinuxSerialPorts.CanAccess(p.Path), "", "   [no permission — see below]")
            Console.WriteLine("  " & p.ToString() & access)
        Next

        If ports.Any(Function(p) Not LinuxSerialPorts.CanAccess(p.Path)) Then
            Console.WriteLine()
            Console.WriteLine("Serial devices belong to the 'dialout' group. Add yourself and log back in:")
            Console.WriteLine("    sudo usermod -aG dialout $USER")
        End If
    End Sub

    Private Function ResolvePort(opts As Options) As String
        If Not String.IsNullOrEmpty(opts.Port) Then Return opts.Port

        Dim ports = LinuxSerialPorts.Enumerate()
        If ports.Count = 0 Then Return Nothing
        Return ports(0).Path
    End Function

    Private Sub OpenPort(device As String, opts As Options)
        If Not LinuxSerialPorts.CanAccess(device) Then
            Console.Error.WriteLine()
            Console.Error.WriteLine($"Cannot open {device}: permission denied.")
            Console.Error.WriteLine("Serial devices belong to the 'dialout' group. Add yourself and log back in:")
            Console.Error.WriteLine("    sudo usermod -aG dialout $USER")
            Console.Error.WriteLine()
            Return
        End If

        Try
            Session.Open(device, opts.Baud, opts.Dtr, opts.Rts)
            Console.WriteLine($"Port        {device} @ {opts.Baud} 8N1")
        Catch ex As Exception
            Console.Error.WriteLine($"Could not open {device}: {ex.Message}")
        End Try
    End Sub

#End Region

#Region "Viewer"

    Private Sub LaunchViewer(opts As Options)
        Dim launch = BrowserLauncher.Open(Session.Server.BaseUrl, opts.Width, opts.Height, opts.Browser)

        If launch.HasValue Then
            Console.WriteLine($"Browser     {launch.Value.Command} — {launch.Value.Mode}")
        Else
            Console.WriteLine("Browser     none found — open this address yourself:")
            Console.WriteLine($"            {Session.Server.BaseUrl}")
        End If
    End Sub

#End Region

#Region "Session events"

    Private Sub OnLog(sender As Object, text As String)
        Dim stamped = $"{DateTime.Now:HH:mm:ss.fff}  {text}"

        SyncLock LogGate
            Recent.Enqueue(stamped)
            While Recent.Count > MaxRecent
                Recent.Dequeue()
            End While

            If EchoLog Then Console.WriteLine(stamped)
        End SyncLock
    End Sub

    Private Sub DumpRecent()
        SyncLock LogGate
            For Each line In Recent
                Console.WriteLine(line)
            Next
        End SyncLock
    End Sub

    Private Sub OnState(sender As Object, state As String)
        Console.WriteLine($"Link        {state}")
    End Sub

#End Region

#Region "Options"

    Private Structure Options
        Public Port As String
        Public Baud As Integer
        Public Width As Integer
        Public Height As Integer
        Public ListenPort As Integer
        Public Dtr As Boolean
        Public Rts As Boolean
        Public Browser As String
        Public NoBrowser As Boolean
        Public ShowHelp As Boolean
        Public ListPorts As Boolean

        Public Shared Function Parse(argv As String()) As Options
            Dim o As New Options With {
                .Baud = SerialTransport.DefaultBaudRate,
                .Width = 1024,
                .Height = 600,
                .ListenPort = LocalServer.DefaultPort
            }

            Dim i = 0
            While i < argv.Length
                Dim a = argv(i)

                Select Case a
                    Case "-h", "--help"
                        o.ShowHelp = True

                    Case "-L", "--list-ports"
                        o.ListPorts = True

                    Case "-p", "--port"
                        o.Port = NextValue(argv, i, a)

                    Case "-b", "--baud"
                        o.Baud = ParseInt(NextValue(argv, i, a), a)

                    Case "-s", "--size"
                        Dim v = NextValue(argv, i, a)
                        Dim parts = v.Split({"x"c, "X"c, ","c})
                        If parts.Length <> 2 Then Throw New ArgumentException($"{a} expects WxH, got '{v}'")
                        o.Width = ParseInt(parts(0), a)
                        o.Height = ParseInt(parts(1), a)

                    Case "-l", "--listen"
                        o.ListenPort = ParseInt(NextValue(argv, i, a), a)

                    Case "--dtr"
                        o.Dtr = True

                    Case "--rts"
                        o.Rts = True

                    Case "--browser"
                        o.Browser = NextValue(argv, i, a)

                    Case "--no-browser"
                        o.NoBrowser = True

                    Case Else
                        Throw New ArgumentException($"unknown option '{a}'")
                End Select

                i += 1
            End While

            If o.Width < 64 OrElse o.Height < 64 Then Throw New ArgumentException("--size is too small")
            Return o
        End Function

        Private Shared Function NextValue(argv As String(), ByRef i As Integer, flag As String) As String
            If i + 1 >= argv.Length Then Throw New ArgumentException($"{flag} expects a value")
            i += 1
            Return argv(i)
        End Function

        Private Shared Function ParseInt(text As String, flag As String) As Integer
            Dim n As Integer
            If Not Integer.TryParse(text.Trim(), n) Then
                Throw New ArgumentException($"{flag} expects a number, got '{text}'")
            End If
            Return n
        End Function

        ''' <summary>
        ''' Built from a line array: Visual Basic string literals cannot span
        ''' source lines.
        ''' </summary>
        Public Shared Function HelpText() As String
            Dim lines As String() = {
                "ESP32 Visual Serial Terminal",
                "",
                "A terminal where the device sends HTML instead of escape codes. The page is",
                "served on loopback and shown in a browser window sized to the target display.",
                "",
                "Usage:",
                "  esp32-visual-serial-terminal [options]",
                "",
                "Options:",
                "  -p, --port <device>    Serial device (default: first USB serial device found)",
                "  -b, --baud <rate>      Baud rate (default: 115200)",
                "  -s, --size <WxH>       Emulated display size (default: 1024x600)",
                "  -l, --listen <port>    Loopback port for the viewer (default: 8080)",
                "      --dtr              Assert DTR on open",
                "      --rts              Assert RTS on open",
                "      --browser <cmd>    Use a specific browser command",
                "      --no-browser       Do not open a browser; print the address instead",
                "  -L, --list-ports       List serial devices and exit",
                "  -h, --help             Show this text",
                "",
                "While running:",
                "  c  connect        d  disconnect     r  request page",
                "  x  clear view     l  toggle log     s  status          q  quit",
                "",
                "Serial devices belong to the 'dialout' group. If opening the port is refused:",
                "    sudo usermod -aG dialout $USER",
                "then log out and back in.",
                "",
                "Protocol: see PROTOCOL.md"
            }
            Return String.Join(Environment.NewLine, lines)
        End Function

    End Structure

#End Region

End Module
