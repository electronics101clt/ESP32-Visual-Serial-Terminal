Imports Gtk

''' <summary>
''' Live view of the connection. GTK4 implementation mirroring the Windows version.
''' </summary>
''' <remarks>
''' Shown non-modally and refreshed on a timer rather than captured once on open.
''' </remarks>
Public NotInheritable Class StatusDialog
    Inherits Gtk.Window

    Private ReadOnly _read As Func(Of StatusSnapshot)
    Private ReadOnly _values As New Dictionary(Of String, Gtk.Label)()
    Private _timerId As UInteger

    Private Shared ReadOnly Rows As String() = {
        "Link", "Port", "Baud", "Screen", "Received", "Sent", "Frames", "Rejected", "Viewer"
    }

    Public Sub New(parent As Gtk.Window, read As Func(Of StatusSnapshot))
        MyBase.New()
        _read = read

        Title = "Connection Status"
        SetTransientFor(parent)
        Modal = False
        Resizable = False
        SetDefaultSize(300, -1)

        Dim grid As New Gtk.Grid()
        grid.RowSpacing = 8
        grid.ColumnSpacing = 24
        grid.MarginTop = 16
        grid.MarginBottom = 16
        grid.MarginStart = 22
        grid.MarginEnd = 22

        Dim row = 0
        For Each rowName In Rows
            Dim caption As Gtk.Label = Gtk.Label.New(rowName)
            caption.Halign = Gtk.Align.Start
            caption.AddCssClass("dim-label")

            Dim value As Gtk.Label = Gtk.Label.New("-")
            value.Halign = Gtk.Align.Start
            value.Selectable = True

            _values(rowName) = value
            grid.Attach(caption, 0, row, 1, 1)
            grid.Attach(value, 1, row, 1, 1)
            row += 1
        Next

        SetChild(grid)
        RefreshValues()

        _timerId = GLib.Functions.TimeoutAdd(0, 500, AddressOf OnRefreshTick)
    End Sub

    Private Function OnRefreshTick() As Boolean
        RefreshValues()
        Return True ' Continue timer
    End Function

    Private Sub RefreshValues()
        Dim s As StatusSnapshot
        Try
            s = _read()
        Catch ex As Exception
            Return
        End Try

        _values("Link").SetText(If(String.IsNullOrEmpty(s.LinkState), "-", s.LinkState))
        _values("Port").SetText(If(String.IsNullOrEmpty(s.PortName), "none", s.PortName))
        _values("Baud").SetText(If(s.IsOpen, s.BaudRate.ToString(), "-"))
        _values("Screen").SetText($"{s.ViewWidth} x {s.ViewHeight}")
        _values("Received").SetText(Humanise(s.BytesReceived))
        _values("Sent").SetText(Humanise(s.BytesSent))
        _values("Frames").SetText(s.FramesReceived.ToString())
        _values("Rejected").SetText(s.FramesRejected.ToString())
        _values("Viewer").SetText(If(s.ServerUrl, "-"))

        ' Highlight rejected frames when they're problematic
        If s.FramesRejected > 0 AndAlso s.FramesRejected >= s.FramesReceived Then
            _values("Rejected").AddCssClass("error")
        Else
            _values("Rejected").RemoveCssClass("error")
        End If
    End Sub

    Private Shared Function Humanise(bytes As Long) As String
        If bytes < 1024 Then Return $"{bytes} B"
        If bytes < 1024L * 1024L Then Return $"{bytes / 1024.0:0.0} KB"
        Return $"{bytes / (1024.0 * 1024.0):0.0} MB"
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        If _timerId > 0 Then
            GLib.Functions.SourceRemove(_timerId)
            _timerId = 0
        End If
        MyBase.Dispose(disposing)
    End Sub

End Class
