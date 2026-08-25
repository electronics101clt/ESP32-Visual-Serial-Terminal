Imports Gtk
Imports System.Text

''' <summary>
''' Rolling view of the raw link. GTK4 implementation mirroring the Windows version.
''' Owns its own window so it can be closed and reopened without losing history.
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

    Public Sub Show(parent As Gtk.Window)
        If _disposed Then Return

        If _window Is Nothing Then
            _window = New LogWindow(parent)
            AddHandler _window.OnCloseRequest, Function(s, e)
                _window = Nothing
                Return False
            End Function
            _window.LoadHistory(_lines)
            _window.Show()
        Else
            _window.Present()
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True

        _window?.Close()
        _window = Nothing
        _lines.Clear()
    End Sub

    Private NotInheritable Class LogWindow
        Inherits Gtk.Window

        Private ReadOnly _textView As Gtk.TextView
        Private ReadOnly _buffer As Gtk.TextBuffer
        Private ReadOnly _autoScroll As Gtk.CheckButton
        Private ReadOnly _scrolled As Gtk.ScrolledWindow

        Public Sub New(parent As Gtk.Window)
            MyBase.New()

            Title = "Serial Log"
            SetTransientFor(parent)
            SetDefaultSize(760, 460)

            Dim mainBox = Gtk.Box.New(Gtk.Orientation.Vertical, 0)

            ' Scrolled text area
            _scrolled = Gtk.ScrolledWindow.New()
            _scrolled.Hexpand = True
            _scrolled.Vexpand = True

            _buffer = Gtk.TextBuffer.New(Nothing)
            _textView = Gtk.TextView.NewWithBuffer(_buffer)
            _textView.Editable = False
            _textView.Monospace = True
            _textView.WrapMode = Gtk.WrapMode.None
            _textView.AddCssClass("log-view")

            _scrolled.SetChild(_textView)
            mainBox.Append(_scrolled)

            ' Bottom bar
            Dim bar = Gtk.Box.New(Gtk.Orientation.Horizontal, 8)
            bar.MarginTop = 8
            bar.MarginBottom = 8
            bar.MarginStart = 8
            bar.MarginEnd = 8

            _autoScroll = Gtk.CheckButton.NewWithLabel("Follow output")
            _autoScroll.Active = True

            Dim clearBtn = Gtk.Button.NewWithLabel("Clear")
            AddHandler clearBtn.OnClicked, Sub(s, e) _buffer.SetText("", 0)

            Dim copyBtn = Gtk.Button.NewWithLabel("Copy All")
            AddHandler copyBtn.OnClicked, Sub(s, e)
                Dim startIter As Gtk.TextIter = Nothing
                Dim endIter As Gtk.TextIter = Nothing
                _buffer.GetBounds(startIter, endIter)
                Dim text = _buffer.GetText(startIter, endIter, False)
                If Not String.IsNullOrEmpty(text) Then
                    Dim clipboard = Display.GetClipboard()
                    clipboard.SetText(text)
                End If
            End Sub

            bar.Append(_autoScroll)
            bar.Append(clearBtn)
            bar.Append(copyBtn)
            mainBox.Append(bar)

            SetChild(mainBox)

            ' Apply dark styling
            ApplyStyles()
        End Sub

        Private Sub ApplyStyles()
            Dim css = "
                .log-view {
                    background-color: rgb(24, 26, 30);
                    color: rgb(220, 223, 228);
                    font-family: monospace;
                    font-size: 9pt;
                }
            "
            Dim provider = Gtk.CssProvider.New()
            provider.LoadFromData(css, -1)
            Gtk.StyleContext.AddProviderForDisplay(
                Display,
                provider,
                Gtk.Constants.STYLE_PROVIDER_PRIORITY_APPLICATION)
        End Sub

        Public Sub LoadHistory(lines As IEnumerable(Of String))
            Dim sb As New StringBuilder()
            For Each l In lines
                sb.AppendLine(l)
            Next
            _buffer.SetText(sb.ToString(), -1)
            ScrollToEnd()
        End Sub

        Public Sub Append(line As String)
            Dim endIter As Gtk.TextIter = Nothing
            _buffer.GetEndIter(endIter)
            _buffer.Insert(endIter, line & Environment.NewLine, -1)

            If _autoScroll.Active Then ScrollToEnd()
        End Sub

        Private Sub ScrollToEnd()
            Dim endIter As Gtk.TextIter = Nothing
            _buffer.GetEndIter(endIter)
            Dim mark = _buffer.CreateMark(Nothing, endIter, False)
            _textView.ScrollToMark(mark, 0, False, 0, 0)
        End Sub

    End Class

End Class
