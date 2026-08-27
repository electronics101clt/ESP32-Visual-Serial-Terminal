Imports Gtk

''' <summary>
''' Prompts for an arbitrary device resolution. Faithful port of the Windows
''' ViewportDialog to GTK.
''' </summary>
Public NotInheritable Class ViewportDialog
    Inherits Dialog

    Private ReadOnly _widthSpin As SpinButton
    Private ReadOnly _heightSpin As SpinButton

    Public ReadOnly Property ViewWidth As Integer
        Get
            Return CInt(_widthSpin.ValueAsInt)
        End Get
    End Property

    Public ReadOnly Property ViewHeight As Integer
        Get
            Return CInt(_heightSpin.ValueAsInt)
        End Get
    End Property

    Public Sub New(parent As Window, currentWidth As Integer, currentHeight As Integer)
        MyBase.New("Device Resolution", parent, DialogFlags.Modal Or DialogFlags.DestroyWithParent,
                   "Cancel", ResponseType.Cancel,
                   "OK", ResponseType.Ok)

        SetDefaultSize(300, -1)
        Resizable = False

        Dim grid As New Grid()
        grid.ColumnSpacing = 12
        grid.RowSpacing = 8
        grid.MarginStart = 16
        grid.MarginEnd = 16
        grid.MarginTop = 14
        grid.MarginBottom = 14

        ' Width row
        Dim wLabel As New Label("Width")
        wLabel.Halign = Align.Start
        grid.Attach(wLabel, 0, 0, 1, 1)

        _widthSpin = New SpinButton(64, 7680, 8)
        _widthSpin.Value = Clamp(currentWidth, 64, 7680)
        _widthSpin.WidthChars = 6
        grid.Attach(_widthSpin, 1, 0, 1, 1)

        Dim px1 As New Label("pixels")
        px1.Halign = Align.Start
        px1.StyleContext.AddClass("dim-label")
        grid.Attach(px1, 2, 0, 1, 1)

        ' Height row
        Dim hLabel As New Label("Height")
        hLabel.Halign = Align.Start
        grid.Attach(hLabel, 0, 1, 1, 1)

        _heightSpin = New SpinButton(64, 4320, 8)
        _heightSpin.Value = Clamp(currentHeight, 64, 4320)
        _heightSpin.WidthChars = 6
        grid.Attach(_heightSpin, 1, 1, 1, 1)

        Dim px2 As New Label("pixels")
        px2.Halign = Align.Start
        px2.StyleContext.AddClass("dim-label")
        grid.Attach(px2, 2, 1, 1, 1)

        ContentArea.Add(grid)
        ContentArea.ShowAll()

        DefaultResponse = ResponseType.Ok
    End Sub

    Private Shared Function Clamp(value As Integer, min As Integer, max As Integer) As Double
        Return CDbl(Math.Min(Math.Max(value, min), max))
    End Function

End Class
