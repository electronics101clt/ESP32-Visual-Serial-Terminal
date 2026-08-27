Imports Gtk

''' <summary>
''' Live view of the connection. Faithful port of the Windows StatusDialog to GTK.
''' </summary>
Public NotInheritable Class StatusDialog
    Inherits Window

    Private ReadOnly _read As Func(Of StatusSnapshot)
    Private ReadOnly _values As New Dictionary(Of String, Label)()

    Private Shared ReadOnly Rows As String() = {
        "Link", "Port", "Baud", "Screen", "Received", "Sent", "Frames", "Rejected", "Viewer"
    }

    Public Sub New(parent As Window, read As Func(Of StatusSnapshot))
        MyBase.New("Connection Status")

        _read = read

        TransientFor = parent
        SetPosition(WindowPosition.CenterOnParent)
        Resizable = False
        TypeHint = Gdk.WindowTypeHint.Dialog

        Dim grid As New Grid()
        grid.ColumnSpacing = 24
        grid.RowSpacing = 8
        grid.MarginStart = 16
        grid.MarginEnd = 22
        grid.MarginTop = 14
        grid.MarginBottom = 14

        Dim row = 0
        For Each rowName In Rows
            Dim caption As New Label(rowName)
            caption.Halign = Align.Start
            caption.StyleContext.AddClass("dim-label")

            Dim value As New Label("-")
            value.Halign = Align.Start
            value.Selectable = True

            _values(rowName) = value

            grid.Attach(caption, 0, row, 1, 1)
            grid.Attach(value, 1, row, 1, 1)
            row += 1
        Next

        Add(grid)

        Refresh_()

        ' Timer for live updates
        GLib.Timeout.Add(500, Function()
            If Not Visible Then Return False
            Refresh_()
            Return True
        End Function)
    End Sub

    Private Sub Refresh_()
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

        ' Colour rejected frames red if they're keeping pace with accepted ones
        If s.FramesRejected > 0 AndAlso s.FramesRejected >= s.FramesReceived Then
            Dim css As New CssProvider()
            css.LoadFromData("label { color: #b22222; }")
            _values("Rejected").StyleContext.AddProvider(css, 800)
        Else
            ' Reset to default
            For Each provider In _values("Rejected").StyleContext.ListProviders()
                _values("Rejected").StyleContext.RemoveProvider(provider)
            Next
        End If
    End Sub

    Private Shared Function Humanise(bytes As Long) As String
        If bytes < 1024 Then Return $"{bytes} B"
        If bytes < 1024L * 1024L Then Return $"{bytes / 1024.0:0.0} KB"
        Return $"{bytes / (1024.0 * 1024.0):0.0} MB"
    End Function

End Class
