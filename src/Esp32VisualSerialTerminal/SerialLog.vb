Imports System.Text
Imports System.Windows.Forms

''' <summary>
''' Rolling view of the raw link. Owns its own window so it can be closed and
''' reopened without losing history, and holds a bounded buffer so a chatty
''' device cannot grow it without limit.
''' </summary>
Public NotInheritable Class SerialLog
    Implements IDisposable

    Private Const MaxLines As Integer = 2000

    Private ReadOnly _lines As New Queue(Of String)(MaxLines)
    Private _window As LogWindow
    Private _disposed As Boolean

    Public Sub Add(line As String)
        If _disposed Then Return

        Dim stamped = $"{DateTime.Now:HH:mm:ss.fff}  {line}"

        _lines.Enqueue(stamped)
        While _lines.Count > MaxLines
            _lines.Dequeue()
        End While

        _window?.Append(stamped)
    End Sub

    Public Sub Show(owner As IWin32Window)
        If _disposed Then Return

        If _window Is Nothing OrElse _window.IsDisposed Then
            _window = New LogWindow()
            AddHandler _window.FormClosed, Sub() _window = Nothing
            _window.LoadHistory(_lines)
            _window.Show(owner)
        Else
            _window.BringToFront()
            _window.Focus()
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True

        If _window IsNot Nothing AndAlso Not _window.IsDisposed Then _window.Close()
        _window = Nothing
        _lines.Clear()
    End Sub

    Private NotInheritable Class LogWindow
        Inherits Form

        Private ReadOnly _text As TextBox
        Private ReadOnly _autoScroll As CheckBox

        Public Sub New()
            Text = "Serial Log"
            ClientSize = New Drawing.Size(760, 460)
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            MinimizeBox = False

            _text = New TextBox With {
                .Multiline = True,
                .ReadOnly = True,
                .ScrollBars = ScrollBars.Both,
                .WordWrap = False,
                .Dock = DockStyle.Fill,
                .BackColor = Drawing.Color.FromArgb(24, 26, 30),
                .ForeColor = Drawing.Color.FromArgb(220, 223, 228),
                .BorderStyle = BorderStyle.None,
                .Font = New Drawing.Font("Consolas", 9.0F)
            }

            Dim bar As New Panel With {.Dock = DockStyle.Bottom, .Height = 34}

            _autoScroll = New CheckBox With {
                .Text = "Follow output",
                .Checked = True,
                .Location = New Drawing.Point(8, 7),
                .AutoSize = True
            }

            Dim clear As New Button With {
                .Text = "Clear",
                .Location = New Drawing.Point(130, 4),
                .Width = 76
            }
            AddHandler clear.Click, Sub() _text.Clear()

            Dim copy As New Button With {
                .Text = "Copy All",
                .Location = New Drawing.Point(214, 4),
                .Width = 82
            }
            AddHandler copy.Click,
                Sub()
                    If _text.TextLength > 0 Then Clipboard.SetText(_text.Text)
                End Sub

            bar.Controls.AddRange({_autoScroll, clear, copy})
            Controls.Add(_text)
            Controls.Add(bar)
        End Sub

        Public Sub LoadHistory(lines As IEnumerable(Of String))
            Dim sb As New StringBuilder()
            For Each l In lines
                sb.AppendLine(l)
            Next
            _text.Text = sb.ToString()
            ScrollToEnd()
        End Sub

        Public Sub Append(line As String)
            If IsDisposed OrElse Not IsHandleCreated Then Return

            Try
                If InvokeRequired Then
                    BeginInvoke(Sub() Append(line))
                    Return
                End If

                _text.AppendText(line & Environment.NewLine)
                If _autoScroll.Checked Then ScrollToEnd()
            Catch ex As Exception
            End Try
        End Sub

        Private Sub ScrollToEnd()
            _text.SelectionStart = _text.TextLength
            _text.ScrollToCaret()
        End Sub

    End Class

End Class
