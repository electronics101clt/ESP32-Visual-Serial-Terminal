Imports Gtk

Module Program

    Sub Main(args As String())
        Application.Init()

        Dim window As New MainWindow()
        window.ShowAll()

        Application.Run()
    End Sub

End Module
