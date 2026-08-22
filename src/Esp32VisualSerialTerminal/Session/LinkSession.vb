Imports System.Text.Json
Imports System.Threading

''' <summary>
''' A snapshot of everything worth reporting about the link.
''' </summary>
Public Structure StatusSnapshot
    Public LinkState As String
    Public PortName As String
    Public BaudRate As Integer
    Public IsOpen As Boolean
    Public ViewWidth As Integer
    Public ViewHeight As Integer
    Public BytesReceived As Long
    Public BytesSent As Long
    Public FramesReceived As Long
    Public FramesRejected As Long
    Public ServerUrl As String
End Structure

''' <summary>
''' Everything the protocol requires of a host, in one place: the serial link,
''' the viewer server, frame verification, acknowledgement, and the request that
''' repeats until a page arrives.
''' </summary>
''' <remarks>
''' Deliberately free of any user-interface dependency, so every host this
''' repository ships runs the same protocol code rather than its own copy. A
''' reference implementation that contained two divergent implementations of its
''' own specification would be worse than useless -- the specification would stop
''' being the thing either one answered to.
'''
''' Hosts observe progress through the events below and supply their own
''' presentation.
''' </remarks>
Public NotInheritable Class LinkSession
    Implements IDisposable

    ''' <summary>
    ''' Re-asked at this cadence until the device answers with a page. A request
    ''' that is dropped -- because the device was still booting, or the line
    ''' glitched -- is otherwise never retried, and the viewer sits blank with no
    ''' indication anything is wrong.
    ''' </summary>
    Public Const PageRequestIntervalMs As Integer = 3000

    Private ReadOnly _serial As New SerialTransport()
    Private ReadOnly _server As New LocalServer()
    Private ReadOnly _gate As New Object()

    Private _retry As Timer
    Private _pageReceived As Boolean
    Private _disposed As Boolean

    Private _state As String = "disconnected"
    Private _selectedPort As String
    Private _baudRate As Integer = SerialTransport.DefaultBaudRate
    Private _viewWidth As Integer = 1024
    Private _viewHeight As Integer = 600

    Private _bytesIn As Long
    Private _bytesOut As Long
    Private _framesOk As Long
    Private _framesRejected As Long

    ''' <summary>Diagnostic text: sent frames, received frames, and link events.</summary>
    Public Event LogLine(sender As Object, text As String)

    ''' <summary>The link state changed. Hosts use this for titles and status.</summary>
    Public Event StateChanged(sender As Object, state As String)

    ''' <summary>A page arrived and is now being served to the viewer.</summary>
    Public Event PageLoaded(sender As Object, e As EventArgs)

    Public ReadOnly Property Server As LocalServer
        Get
            Return _server
        End Get
    End Property

    Public ReadOnly Property IsOpen As Boolean
        Get
            Return _serial.IsOpen
        End Get
    End Property

    Public ReadOnly Property State As String
        Get
            Return _state
        End Get
    End Property

    Public ReadOnly Property BaudRate As Integer
        Get
            Return _baudRate
        End Get
    End Property

    Public ReadOnly Property ViewWidth As Integer
        Get
            Return _viewWidth
        End Get
    End Property

    Public ReadOnly Property ViewHeight As Integer
        Get
            Return _viewHeight
        End Get
    End Property

    Public Sub New()
        AddHandler _serial.LineReceived, AddressOf OnSerialLine
        AddHandler _serial.Disconnected, AddressOf OnSerialDisconnected
        AddHandler _serial.BytesTransferred, AddressOf OnBytesTransferred

        AddHandler _server.ViewerEvent, AddressOf OnViewerEvent
        AddHandler _server.PageRequested, AddressOf OnViewerRequestedPage
    End Sub

    Public Sub StartServer(Optional preferredPort As Integer = LocalServer.DefaultPort)
        _server.SetViewport(_viewWidth, _viewHeight)
        _server.Start(preferredPort)
    End Sub

    Public Sub SetViewport(width As Integer, height As Integer)
        _viewWidth = width
        _viewHeight = height
        _server.SetViewport(width, height)
    End Sub

    ''' <summary>
    ''' Opens the link and begins asking for a page.
    ''' </summary>
    Public Sub Open(portName As String, baudRate As Integer,
                    Optional assertDtr As Boolean = False,
                    Optional assertRts As Boolean = False)

        _serial.Close()
        _serial.Open(portName, baudRate, assertDtr, assertRts)

        _selectedPort = portName
        _baudRate = baudRate
        _pageReceived = False

        Log($"--- opened {portName} @ {baudRate} 8N1 ---")
        SetState("connected")

        ' Ask straight away, then keep asking until a page lands.
        RequestPage()

        SyncLock _gate
            _retry?.Dispose()
            _retry = New Timer(AddressOf OnRetryTick, Nothing,
                               PageRequestIntervalMs, PageRequestIntervalMs)
        End SyncLock
    End Sub

    Public Sub Close()
        StopRetry()
        _serial.Close()
        _pageReceived = False

        _server.ClearPage()
        Log("--- closed ---")
        SetState("disconnected")
    End Sub

    Public Sub RequestPage(Optional file As String = Nothing)
        _serial.SendLine(LineCodec.EncodeGetPage(file))
        Log("> get_page" & If(String.IsNullOrEmpty(file), String.Empty, " " & file))
    End Sub

    Public Sub ClearPage()
        _pageReceived = False
        _server.ClearPage()
    End Sub

    Public Function Snapshot() As StatusSnapshot
        Return New StatusSnapshot With {
            .LinkState = _state,
            .PortName = If(_serial.IsOpen, _selectedPort, Nothing),
            .BaudRate = _baudRate,
            .IsOpen = _serial.IsOpen,
            .ViewWidth = _viewWidth,
            .ViewHeight = _viewHeight,
            .BytesReceived = Interlocked.Read(_bytesIn),
            .BytesSent = Interlocked.Read(_bytesOut),
            .FramesReceived = Interlocked.Read(_framesOk),
            .FramesRejected = Interlocked.Read(_framesRejected),
            .ServerUrl = _server.BaseUrl
        }
    End Function

    Private Sub OnRetryTick(state As Object)
        If _pageReceived OrElse Not _serial.IsOpen Then
            StopRetry()
            Return
        End If
        RequestPage()
    End Sub

    Private Sub StopRetry()
        SyncLock _gate
            _retry?.Dispose()
            _retry = Nothing
        End SyncLock
    End Sub

    ''' <summary>
    ''' Handles one received line. Runs on the serial reader thread; hosts that
    ''' need a particular thread marshal inside their own event handlers.
    ''' </summary>
    Private Sub OnSerialLine(sender As Object, line As String)
        Dim json = LineCodec.Unframe(line)

        If json Is Nothing Then
            ' Either boot log output, which shares the wire and carries no
            ' checksum, or a frame that failed verification. Both are ignored
            ' without a reply: acknowledging a frame we could not verify would
            ' tell the device a corrupted message had been acted on. Staying
            ' silent makes it retry.
            Interlocked.Increment(_framesRejected)
            Log("  " & line)
            Return
        End If

        Interlocked.Increment(_framesOk)

        Dim msg = LineCodec.Decode(json)
        If msg Is Nothing Then
            Log("  ! unparseable payload: " & json)
            Return
        End If

        ' An acknowledgement retires our own in-flight message. It is not a
        ' message to act on, and acknowledging it back would loop forever.
        If msg.Type = MessageType.Ack Then
            Log("< ack")
            Return
        End If

        Log("< " & Summarise(msg, json))

        Select Case msg.Type
            Case MessageType.Html
                _pageReceived = True
                StopRetry()
                _server.PushPage(If(msg.Body, String.Empty))
                SetState("page loaded")
                RaiseEvent PageLoaded(Me, EventArgs.Empty)

            Case MessageType.Update
                _server.PushUpdate(BuildUpdateJson(msg))

            Case MessageType.Notify
                _server.PushNotify(BuildDialogJson(msg))

            Case MessageType.Dialog
                _server.PushDialog(BuildDialogJson(msg))
        End Select

        ' Acknowledge only after the message has been acted on, so the device
        ' learns the content actually arrived somewhere rather than merely
        ' having been read off the wire.
        _serial.SendLine(LineCodec.EncodeAck())
        Log("> ack")
    End Sub

    Private Sub OnViewerEvent(sender As Object, e As ViewerEventArgs)
        _serial.SendLine(LineCodec.EncodeEvent(e.Id, e.Action, e.Value))
        Log($"> event {e.Id} {e.Action} {e.Value}")
    End Sub

    Private Sub OnViewerRequestedPage(sender As Object, file As String)
        RequestPage(file)
    End Sub

    Private Sub OnSerialDisconnected(sender As Object, reason As String)
        Log($"--- link lost: {reason} ---")
        StopRetry()
        _pageReceived = False
        _server.ClearPage()
        SetState("link lost")
    End Sub

    Private Sub OnBytesTransferred(sender As Object, received As Long, sent As Long)
        Interlocked.Exchange(_bytesIn, received)
        Interlocked.Exchange(_bytesOut, sent)
    End Sub

    Private Shared Function Summarise(msg As DeviceMessage, raw As String) As String
        Select Case msg.Type
            Case MessageType.Html
                Return $"html ({If(msg.Body, String.Empty).Length} bytes)"
            Case MessageType.Update
                Return $"update {msg.Id} = {msg.Text}"
            Case Else
                Return If(raw.Length > 200, raw.Substring(0, 200) & "...", raw)
        End Select
    End Function

    Private Shared Function BuildUpdateJson(msg As DeviceMessage) As String
        Return JsonSerializer.Serialize(New With {
            Key .id = If(msg.Id, String.Empty),
            Key .text = msg.Text,
            Key .value = msg.Value
        })
    End Function

    Private Shared Function BuildDialogJson(msg As DeviceMessage) As String
        Return JsonSerializer.Serialize(New With {
            Key .id = If(msg.Id, String.Empty),
            Key .title = If(msg.Title, String.Empty),
            Key .message = If(msg.Message, String.Empty)
        })
    End Function

    Private Sub SetState(value As String)
        _state = value
        RaiseEvent StateChanged(Me, value)
    End Sub

    Private Sub Log(text As String)
        RaiseEvent LogLine(Me, text)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True

        StopRetry()
        _serial.Dispose()
        _server.Dispose()
    End Sub

End Class
