Imports System.Collections.Concurrent
Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Reflection
Imports System.Text
Imports System.Threading


''' <summary>
''' Interaction reported by the viewer, on its way back to the device.
''' </summary>
Public NotInheritable Class ViewerEventArgs
    Inherits EventArgs

    Public Property Id As String
    Public Property Action As String
    Public Property Value As String

End Class

''' <summary>
''' Loopback HTTP server backing the viewer.
'''
''' The renderer is an ordinary browser engine pointed at an ordinary URL.
''' Everything it needs -- the shell document, the push stream, and the
''' endpoint interactions post back to -- is served from here, so the page
''' runs under a real origin with a real EventSource and working relative
''' URLs. Nothing host-specific is injected into the device's markup.
''' </summary>
Public NotInheritable Class LocalServer
    Implements IDisposable

    Public Const DefaultPort As Integer = 8080

    ' Sent when nothing else has gone out for this long, so a silent link
    ' still fails fast at the socket layer instead of hanging forever.
    Private Const KeepAliveMs As Integer = 15000

    Private ReadOnly _clients As New ConcurrentDictionary(Of Guid, SseClient)()
    Private ReadOnly _stateLock As New Object()

    Private _listener As HttpListener
    Private _accept As Thread
    Private _keepAlive As Timer
    Private _running As Boolean
    Private _disposed As Boolean

    Private _shellTemplate As String
    Private _currentPage As String
    Private _viewWidth As Integer = 1024
    Private _viewHeight As Integer = 600

    Public ReadOnly Property Port As Integer
    Public ReadOnly Property BaseUrl As String
        Get
            Return $"http://127.0.0.1:{Port}/"
        End Get
    End Property

    Public ReadOnly Property ClientCount As Integer
        Get
            Return _clients.Count
        End Get
    End Property

    ''' <summary>Raised when the viewer reports an interaction.</summary>
    Public Event ViewerEvent(sender As Object, e As ViewerEventArgs)

    ''' <summary>Raised when the viewer asks for the current page.</summary>
    Public Event PageRequested(sender As Object, file As String)

    Public Sub New()
        _shellTemplate = LoadShellTemplate()
    End Sub

    ''' <summary>
    ''' Sets the emulated viewport. Takes effect for pages served from this
    ''' point on; existing viewers reload to pick it up.
    ''' </summary>
    Public Sub SetViewport(width As Integer, height As Integer)
        SyncLock _stateLock
            _viewWidth = Math.Max(1, width)
            _viewHeight = Math.Max(1, height)
        End SyncLock
    End Sub

    Public Sub Start(Optional preferredPort As Integer = DefaultPort)
        If _running Then Return

        Dim listener As HttpListener = Nothing
        Dim chosen = -1

        ' 8080 is a popular port. Rather than fail, walk forward until one
        ' binds -- the URL is internal and never typed by hand.
        For candidate = preferredPort To preferredPort + 24
            Try
                Dim l As New HttpListener()
                l.Prefixes.Add($"http://127.0.0.1:{candidate}/")
                l.Start()
                listener = l
                chosen = candidate
                Exit For
            Catch ex As HttpListenerException
            Catch ex As SocketException
            End Try
        Next

        If listener Is Nothing Then
            Throw New InvalidOperationException(
                $"No free loopback port in {preferredPort}-{preferredPort + 24}.")
        End If

        _listener = listener
        _Port = chosen
        _running = True

        _accept = New Thread(AddressOf AcceptLoop) With {
            .IsBackground = True,
            .Name = "http-accept"
        }
        _accept.Start()

        _keepAlive = New Timer(AddressOf SendKeepAlive, Nothing, KeepAliveMs, KeepAliveMs)
    End Sub

    Public Sub [Stop]()
        If Not _running Then Return
        _running = False

        _keepAlive?.Dispose()
        _keepAlive = Nothing

        For Each c In _clients.Values
            c.Dispose()
        Next
        _clients.Clear()

        Try
            _listener?.Stop()
            _listener?.Close()
        Catch ex As Exception
        End Try
        _listener = Nothing

        Dim t = _accept
        _accept = Nothing
        If t IsNot Nothing AndAlso t.IsAlive AndAlso t IsNot Thread.CurrentThread Then
            t.Join(1000)
        End If
    End Sub

    ''' <summary>
    ''' Publishes a new page. Cached so a viewer connecting later, or
    ''' reconnecting after a reload, is brought up to date immediately
    ''' rather than sitting blank until the device happens to push again.
    ''' </summary>
    Public Sub PushPage(html As String)
        SyncLock _stateLock
            _currentPage = html
        End SyncLock
        Broadcast("page", html)
    End Sub

    Public Sub PushUpdate(json As String)
        Broadcast("update", json)
    End Sub

    Public Sub PushNotify(json As String)
        Broadcast("notify", json)
    End Sub

    Public Sub PushDialog(json As String)
        Broadcast("dialog", json)
    End Sub

    ''' <summary>
    ''' Drops the displayed page back to the waiting placeholder. Used when
    ''' the link goes away, so a stale page is never left on screen looking
    ''' live.
    ''' </summary>
    Public Sub ClearPage()
        SyncLock _stateLock
            _currentPage = Nothing
        End SyncLock
        Broadcast("clear", "1")
    End Sub

    Private Sub AcceptLoop()
        While _running
            Dim ctx As HttpListenerContext
            Try
                ctx = _listener.GetContext()
            Catch ex As HttpListenerException
                Exit While
            Catch ex As ObjectDisposedException
                Exit While
            Catch ex As InvalidOperationException
                Exit While
            End Try

            ThreadPool.QueueUserWorkItem(Sub() Handle(ctx))
        End While
    End Sub

    Private Sub Handle(ctx As HttpListenerContext)
        Try
            Dim path = ctx.Request.Url.AbsolutePath

            Select Case True
                Case path.Equals("/events", StringComparison.OrdinalIgnoreCase)
                    HandleSse(ctx)

                Case path.Equals("/event", StringComparison.OrdinalIgnoreCase)
                    Dim q = ctx.Request.QueryString
                    RaiseEvent ViewerEvent(Me, New ViewerEventArgs With {
                        .Id = If(q("id"), String.Empty),
                        .Action = If(q("action"), String.Empty),
                        .Value = q("value")
                    })
                    WriteText(ctx, "OK")

                Case path.Equals("/get_page", StringComparison.OrdinalIgnoreCase)
                    RaiseEvent PageRequested(Me, ctx.Request.QueryString("file"))
                    WriteText(ctx, "OK")

                Case Else
                    WriteHtml(ctx, RenderShell())
            End Select

        Catch ex As Exception
            ' Client vanished mid-response, or the listener is shutting
            ' down. Neither is worth surfacing.
            Try
                ctx.Response.Abort()
            Catch ex2 As Exception
            End Try
        End Try
    End Sub

    Private Sub HandleSse(ctx As HttpListenerContext)
        Dim res = ctx.Response
        res.StatusCode = 200
        res.ContentType = "text/event-stream"
        res.Headers("Cache-Control") = "no-cache"
        res.Headers("X-Accel-Buffering") = "no"
        res.KeepAlive = True
        res.SendChunked = True

        Dim client As New SseClient(res)
        _clients(client.Id) = client

        ' Bring this viewer level with whatever is already on screen.
        Dim page As String
        SyncLock _stateLock
            page = _currentPage
        End SyncLock

        If page IsNot Nothing Then
            client.Send("page", page)
        End If

        ' The response stays open; the connection is torn down from
        ' Broadcast or Stop when it stops being writable.
        client.Send("ping", "1")
    End Sub

    Private Sub Broadcast(eventName As String, data As String)
        For Each pair In _clients
            Dim c = pair.Value
            If Not c.Send(eventName, data) Then
                Dim dead As SseClient = Nothing
                _clients.TryRemove(pair.Key, dead)
                c.Dispose()
            End If
        Next
    End Sub

    Private Sub SendKeepAlive(state As Object)
        If Not _running Then Return
        Broadcast("ping", "1")
    End Sub

    Private Function RenderShell() As String
        Dim w As Integer, h As Integer
        SyncLock _stateLock
            w = _viewWidth
            h = _viewHeight
        End SyncLock

        Return _shellTemplate _
            .Replace("__VIEW_W__", w.ToString(Globalization.CultureInfo.InvariantCulture)) _
            .Replace("__VIEW_H__", h.ToString(Globalization.CultureInfo.InvariantCulture))
    End Function

    Private Shared Function LoadShellTemplate() As String
        Dim asm = Assembly.GetExecutingAssembly()

        Dim name = asm.GetManifestResourceNames() _
                      .FirstOrDefault(Function(n) n.EndsWith("shell.html", StringComparison.OrdinalIgnoreCase))

        If name Is Nothing Then
            Throw New InvalidOperationException("Embedded shell.html is missing from the build.")
        End If

        Using stream = asm.GetManifestResourceStream(name)
            Using reader As New StreamReader(stream, Encoding.UTF8)
                Return reader.ReadToEnd()
            End Using
        End Using
    End Function

    Private Shared Sub WriteText(ctx As HttpListenerContext, body As String)
        Write(ctx, body, "text/plain; charset=utf-8")
    End Sub

    Private Shared Sub WriteHtml(ctx As HttpListenerContext, body As String)
        Write(ctx, body, "text/html; charset=utf-8")
    End Sub

    Private Shared Sub Write(ctx As HttpListenerContext, body As String, contentType As String)
        Dim bytes = Encoding.UTF8.GetBytes(body)
        ctx.Response.StatusCode = 200
        ctx.Response.ContentType = contentType
        ctx.Response.ContentLength64 = bytes.LongLength
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length)
        ctx.Response.OutputStream.Close()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        [Stop]()
    End Sub

    ''' <summary>
    ''' One attached viewer. Writes are serialised because pushes originate
    ''' on the serial reader thread and the keep-alive timer alike.
    ''' </summary>
    Private NotInheritable Class SseClient
        Implements IDisposable

        Private ReadOnly _res As HttpListenerResponse
        Private ReadOnly _writeLock As New Object()
        Private _dead As Boolean

        Public ReadOnly Property Id As Guid = Guid.NewGuid()

        Public Sub New(res As HttpListenerResponse)
            _res = res
        End Sub

        ''' <summary>
        ''' Emits one SSE record. Returns False once the socket is gone, so
        ''' the caller can retire this client.
        ''' </summary>
        Public Function Send(eventName As String, data As String) As Boolean
            If _dead Then Return False

            SyncLock _writeLock
                If _dead Then Return False
                Try
                    Dim sb As New StringBuilder()
                    sb.Append("event: ").Append(eventName).Append(vbLf)

                    ' A payload containing newlines is illegal as a single
                    ' data: line -- every physical line needs its own
                    ' prefix, and the receiver rejoins them with \n.
                    For Each line In SplitLines(If(data, String.Empty))
                        sb.Append("data: ").Append(line).Append(vbLf)
                    Next

                    sb.Append(vbLf)

                    Dim bytes = Encoding.UTF8.GetBytes(sb.ToString())
                    _res.OutputStream.Write(bytes, 0, bytes.Length)
                    _res.OutputStream.Flush()
                    Return True
                Catch ex As Exception
                    _dead = True
                    Return False
                End Try
            End SyncLock
        End Function

        Private Shared Function SplitLines(s As String) As String()
            Return s.Replace(vbCrLf, vbLf).Replace(ChrW(13), ChrW(10)).Split(ChrW(10))
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            _dead = True
            Try
                _res.OutputStream.Close()
                _res.Close()
            Catch ex As Exception
            End Try
        End Sub

    End Class

End Class
