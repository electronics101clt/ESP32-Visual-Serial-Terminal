Imports Gtk

''' <summary>
''' Rolling view of the raw link. Faithful port of the Windows SerialLog to GTK.
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

    Public Sub Show(parent As Window)
        If _disposed Then Return

        If _window Is Nothing OrElse Not _window.Visible Then
            _window = New LogWindow(parent)
            AddHandler _window.DeleteEvent, Sub() _window = Nothing
            _window.LoadHistory(_lines)
            _window.ShowAll()
        Else
            _window.Present()
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True

        If _window IsNot Nothing AndAlso _window.Visible Then _window.Destroy()
        _window = Nothing
        _lines.Clear()
    End Sub

    Private NotInheritable Class LogWindow
        Inherits Window

        Private ReadOnly _textView As TextView
        Private ReadOnly _buffer As TextBuffer
        Private ReadOnly _autoScroll As CheckButton

        Public Sub New(parent As Window)
            MyBase.New("Serial Log")

            TransientFor = parent
            SetDefaultSize(760, 460)
            SetPosition(WindowPosition.CenterOnParent)

            Dim vbox As New Box(Orientation.Vertical, 0)

            ' Text view with scrolling
            _buffer = New TextBuffer(Nothing)
            _textView = New TextView(_buffer)
            _textView.Editable = False
            _textView.Monospace = True
            _textView.WrapMode = WrapMode.None

            ' Dark theme for the log
            Dim css As New CssProvider()
            css.LoadFromData("textview { background-color: #181a1e; color: #dcdfe4; }")
            _textView.StyleContext.AddProvider(css, 800)
            _textView.StyleContext.AddClass("log-view")

            Dim scrolled As New ScrolledWindow()
            scrolled.Add(_textView)
            vbox.PackStart(scrolled, True, True, 0)

            ' Bottom bar
            Dim bar As New Box(Orientation.Horizontal, 8)
            bar.MarginStart = 8
            bar.MarginEnd = 8
            bar.MarginTop = 4
            bar.MarginBottom = 4

            _autoScroll = New CheckButton("Follow output")
            _autoScroll.Active = True
            bar.PackStart(_autoScroll, False, False, 0)

            Dim clearBtn As New Button("Clear")
            AddHandler clearBtn.Clicked, Sub() _buffer.Text = ""
            bar.PackStart(clearBtn, False, False, 0)

            Dim copyBtn As New Button("Copy All")
            AddHandler copyBtn.Clicked, Sub()
                Dim clip = Clipboard.Get(Gdk.Atom.Intern("CLIPBOARD", False))
                clip.Text = _buffer.Text
            End Sub
            bar.PackStart(copyBtn, False, False, 0)

            vbox.PackStart(bar, False, False, 0)

            Add(vbox)
        End Sub

        Public Sub LoadHistory(lines As IEnumerable(Of String))
            Dim sb As New System.Text.StringBuilder()
            For Each l In lines
                sb.AppendLine(l)
            Next
            _buffer.Text = sb.ToString()
            ScrollToEnd()
        End Sub

        Public Sub Append(line As String)
            If Not Visible Then Return

            Application.Invoke(Sub()
                Dim endIter = _buffer.EndIter
                _buffer.Insert(endIter, line & Environment.NewLine)
                If _autoScroll.Active Then ScrollToEnd()
            End Sub)
        End Sub

        Private Sub ScrollToEnd()
            Dim endMark = _buffer.CreateMark(Nothing, _buffer.EndIter, False)
            _textView.ScrollToMark(endMark, 0, False, 0, 0)
        End Sub

    End Class

End Class
