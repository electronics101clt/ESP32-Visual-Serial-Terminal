Imports Gtk

''' <summary>
''' GTK4 host entry point. Initializes GTK and runs the main window.
''' </summary>
Public Module Program

    Public Function Main(argv As String()) As Integer
        Dim app = Gtk.Application.New("org.esp32.visualserialterminal", Gio.ApplicationFlags.FlagsNone)
        AddHandler app.OnActivate, AddressOf OnActivate
        Return app.RunWithSynchronizationContext(Nothing)
    End Function

    Private Sub OnActivate(sender As Gio.Application, args As EventArgs)
        Dim app = DirectCast(sender, Gtk.Application)
        Dim window As New MainWindow(app)
        window.Show()
    End Sub

End Module
