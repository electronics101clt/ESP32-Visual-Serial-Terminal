Imports System.Windows.Forms

''' <summary>
''' Prompts for an arbitrary device resolution, for panels that none of the
''' presets match.
''' </summary>
Public NotInheritable Class ViewportDialog
    Inherits Form

    Private ReadOnly _width As NumericUpDown
    Private ReadOnly _height As NumericUpDown

    Public ReadOnly Property ViewWidth As Integer
        Get
            Return CInt(_width.Value)
        End Get
    End Property

    Public ReadOnly Property ViewHeight As Integer
        Get
            Return CInt(_height.Value)
        End Get
    End Property

    Public Sub New(currentWidth As Integer, currentHeight As Integer)
        Text = "Device Resolution"
        FormBorderStyle = FormBorderStyle.FixedDialog
        StartPosition = FormStartPosition.CenterParent
        ClientSize = New Drawing.Size(300, 132)
        MinimizeBox = False
        MaximizeBox = False
        ShowInTaskbar = False

        Dim wLabel As New Label With {
            .Text = "Width", .Location = New Drawing.Point(16, 18), .AutoSize = True}
        Dim hLabel As New Label With {
            .Text = "Height", .Location = New Drawing.Point(16, 52), .AutoSize = True}

        _width = New NumericUpDown With {
            .Minimum = 64, .Maximum = 7680, .Value = Clamp(currentWidth, 64, 7680),
            .Location = New Drawing.Point(84, 16), .Width = 96, .Increment = 8
        }

        _height = New NumericUpDown With {
            .Minimum = 64, .Maximum = 4320, .Value = Clamp(currentHeight, 64, 4320),
            .Location = New Drawing.Point(84, 50), .Width = 96, .Increment = 8
        }

        Dim px As New Label With {
            .Text = "pixels", .Location = New Drawing.Point(190, 18), .AutoSize = True,
            .ForeColor = Drawing.SystemColors.GrayText}
        Dim px2 As New Label With {
            .Text = "pixels", .Location = New Drawing.Point(190, 52), .AutoSize = True,
            .ForeColor = Drawing.SystemColors.GrayText}

        Dim ok As New Button With {
            .Text = "OK", .DialogResult = DialogResult.OK,
            .Location = New Drawing.Point(122, 92), .Width = 80}

        Dim cancel As New Button With {
            .Text = "Cancel", .DialogResult = DialogResult.Cancel,
            .Location = New Drawing.Point(208, 92), .Width = 80}

        Controls.AddRange({wLabel, hLabel, _width, _height, px, px2, ok, cancel})
        AcceptButton = ok
        CancelButton = cancel
    End Sub

    Private Shared Function Clamp(value As Integer, min As Integer, max As Integer) As Decimal
        Return CDec(Math.Min(Math.Max(value, min), max))
    End Function

End Class
