Imports Gtk

''' <summary>
''' Prompts for an arbitrary device resolution. GTK4 implementation.
''' </summary>
Public NotInheritable Class ViewportDialog
    Inherits Gtk.Dialog

    Private ReadOnly _widthSpin As Gtk.SpinButton
    Private ReadOnly _heightSpin As Gtk.SpinButton

    Public ReadOnly Property ViewWidth As Integer
        Get
            Return CInt(_widthSpin.Value)
        End Get
    End Property

    Public ReadOnly Property ViewHeight As Integer
        Get
            Return CInt(_heightSpin.Value)
        End Get
    End Property

    Public Sub New(parent As Gtk.Window, currentWidth As Integer, currentHeight As Integer)
        MyBase.New()

        Title = "Device Resolution"
        SetTransientFor(parent)
        Modal = True
        Resizable = False

        AddButton("Cancel", Gtk.ResponseType.Cancel)
        AddButton("OK", Gtk.ResponseType.Ok)
        SetDefaultResponse(Gtk.ResponseType.Ok)

        Dim grid As Gtk.Grid = Gtk.Grid.New()
        grid.RowSpacing = 12
        grid.ColumnSpacing = 12
        grid.MarginTop = 16
        grid.MarginBottom = 16
        grid.MarginStart = 16
        grid.MarginEnd = 16

        ' Width row
        Dim widthLabel As Gtk.Label = Gtk.Label.New("Width")
        widthLabel.Halign = Gtk.Align.Start

        _widthSpin = Gtk.SpinButton.NewWithRange(64, 7680, 8)
        _widthSpin.Value = Clamp(currentWidth, 64, 7680)
        _widthSpin.WidthChars = 6

        Dim widthUnit As Gtk.Label = Gtk.Label.New("pixels")
        widthUnit.AddCssClass("dim-label")

        grid.Attach(widthLabel, 0, 0, 1, 1)
        grid.Attach(_widthSpin, 1, 0, 1, 1)
        grid.Attach(widthUnit, 2, 0, 1, 1)

        ' Height row
        Dim heightLabel As Gtk.Label = Gtk.Label.New("Height")
        heightLabel.Halign = Gtk.Align.Start

        _heightSpin = Gtk.SpinButton.NewWithRange(64, 4320, 8)
        _heightSpin.Value = Clamp(currentHeight, 64, 4320)
        _heightSpin.WidthChars = 6

        Dim heightUnit As Gtk.Label = Gtk.Label.New("pixels")
        heightUnit.AddCssClass("dim-label")

        grid.Attach(heightLabel, 0, 1, 1, 1)
        grid.Attach(_heightSpin, 1, 1, 1, 1)
        grid.Attach(heightUnit, 2, 1, 1, 1)

        Dim contentArea = GetContentArea()
        contentArea.Append(grid)
    End Sub

    Private Shared Function Clamp(value As Integer, min As Integer, max As Integer) As Double
        Return CDbl(Math.Min(Math.Max(value, min), max))
    End Function

End Class
