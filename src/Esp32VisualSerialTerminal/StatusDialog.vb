Imports System.Windows.Forms

''' <summary>
''' A snapshot of everything worth reporting about the link, gathered in one
''' place so the dialog has a single source to read from.
''' </summary>
Public Structure StatusSnapshot
    Public LinkState As String
    Public PortName As String
    Public BaudRate As Integer
    Public IsOpen As Boolean
    Public ViewWidth As Integer
    Public ViewHeight As Integer
    Public BytesReceived As Long
    Public BytesSent As Long
    Public FramesReceived As Long
    Public FramesRejected As Long
    Public ServerUrl As String
End Structure

''' <summary>
''' Live view of the connection.
''' </summary>
''' <remarks>
''' Shown non-modally and refreshed on a timer rather than captured once on
''' open. The numbers that matter most here are the ones that move -- watching
''' received bytes climb, or rejected frames climb alongside them, is what
''' separates a silent device from a mis-framed one, and a still snapshot
''' cannot show that.
'''
''' Every dimension comes from AutoSize. Fixed pixel sizes are only ever correct
''' at one display scale: the font grows with the scale factor and a hardcoded
''' bound does not, so the text is clipped on any scaled display.
''' </remarks>
Public NotInheritable Class StatusDialog
    Inherits Form

    Private ReadOnly _read As Func(Of StatusSnapshot)
    Private ReadOnly _timer As Timer
    Private ReadOnly _values As New Dictionary(Of String, Label)()

    Private Shared ReadOnly Rows As String() = {
        "Link", "Port", "Baud", "Screen", "Received", "Sent", "Frames", "Rejected", "Viewer"
    }

    Public Sub New(read As Func(Of StatusSnapshot))
        _read = read

        Text = "Connection Status"
        AppIcon.Apply(Me)
        FormBorderStyle = FormBorderStyle.FixedToolWindow
        StartPosition = FormStartPosition.CenterParent
        ShowInTaskbar = False
        AutoScaleMode = AutoScaleMode.Font

        Dim grid As New TableLayoutPanel With {
            .ColumnCount = 2,
            .RowCount = Rows.Length,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Dock = DockStyle.Fill,
            .Padding = New Padding(16, 14, 22, 14)
        }
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))

        ' Not 'name': that binds to Form.Name and cannot be a loop variable.
        For Each rowName In Rows
            grid.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim caption As New Label With {
                .Text = rowName,
                .AutoSize = True,
                .Margin = New Padding(0, 5, 24, 5),
                .ForeColor = Drawing.SystemColors.GrayText
            }

            Dim value As New Label With {
                .Text = "-",
                .AutoSize = True,
                .Margin = New Padding(0, 5, 0, 5)
            }

            _values(rowName) = value
            grid.Controls.Add(caption)
            grid.Controls.Add(value)
        Next

        Controls.Add(grid)

        ' The form takes its size from the grid, which takes its size from the
        ' text it holds, at whatever scale the display is running.
        AutoSize = True
        AutoSizeMode = AutoSizeMode.GrowAndShrink

        Refresh_()

        _timer = New Timer With {.Interval = 500}
        AddHandler _timer.Tick, Sub() Refresh_()
        _timer.Start()
    End Sub

    Private Sub Refresh_()
        If IsDisposed Then Return

        Dim s As StatusSnapshot
        Try
            s = _read()
        Catch ex As Exception
            Return
        End Try

        _values("Link").Text = If(String.IsNullOrEmpty(s.LinkState), "-", s.LinkState)
        _values("Port").Text = If(String.IsNullOrEmpty(s.PortName), "none", s.PortName)
        _values("Baud").Text = If(s.IsOpen, s.BaudRate.ToString(), "-")
        _values("Screen").Text = $"{s.ViewWidth} x {s.ViewHeight}"
        _values("Received").Text = Humanise(s.BytesReceived)
        _values("Sent").Text = Humanise(s.BytesSent)
        _values("Frames").Text = s.FramesReceived.ToString()
        _values("Rejected").Text = s.FramesRejected.ToString()
        _values("Viewer").Text = If(s.ServerUrl, "-")

        ' Rejected frames are normal in small numbers while a device boots, and
        ' a problem when they keep pace with accepted ones. Colour makes that
        ' visible without needing a second reading.
        _values("Rejected").ForeColor =
            If(s.FramesRejected > 0 AndAlso s.FramesRejected >= s.FramesReceived,
               Drawing.Color.Firebrick,
               Drawing.SystemColors.ControlText)
    End Sub

    Private Shared Function Humanise(bytes As Long) As String
        If bytes < 1024 Then Return $"{bytes} B"
        If bytes < 1024L * 1024L Then Return $"{bytes / 1024.0:0.0} KB"
        Return $"{bytes / (1024.0 * 1024.0):0.0} MB"
    End Function

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        _timer?.Stop()
        _timer?.Dispose()
        MyBase.OnFormClosed(e)
    End Sub

End Class
