Imports System.IO.Ports
Imports System.Text
Imports System.Threading


''' <summary>
''' Line-oriented serial link to the device. Owns the port, reassembles
''' newline-delimited records from arbitrarily fragmented reads, and raises
''' one event per complete line on a background thread.
''' </summary>
Public NotInheritable Class SerialTransport
    Implements IDisposable

    Public Const DefaultBaudRate As Integer = 115200

    ' A full page push is a single line and can run to tens of kilobytes.
    ' Anything past this is treated as a device stuck mid-garbage and the
    ' partial record is dropped rather than grown without bound.
    Private Const MaxLineBytes As Integer = 1024 * 1024

    Private ReadOnly _sync As New Object()
    Private ReadOnly _buffer As New List(Of Byte)(8192)

    Private _port As SerialPort
    Private _reader As Thread
    Private _running As Boolean
    Private _disposed As Boolean

    Public Event LineReceived(sender As Object, line As String)
    Public Event Disconnected(sender As Object, reason As String)
    Public Event BytesTransferred(sender As Object, received As Long, sent As Long)

    Private _bytesIn As Long
    Private _bytesOut As Long

    Public ReadOnly Property IsOpen As Boolean
        Get
            SyncLock _sync
                Return _port IsNot Nothing AndAlso _port.IsOpen
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property PortName As String
        Get
            SyncLock _sync
                Return If(_port?.PortName, String.Empty)
            End SyncLock
        End Get
    End Property

    Public Shared Function AvailablePorts() As String()
        Try
            Dim names = SerialPort.GetPortNames()
            Array.Sort(names, AddressOf ComparePortNames)
            Return names
        Catch ex As Exception
            Return Array.Empty(Of String)()
        End Try
    End Function

    ''' <summary>
    ''' Orders COM10 after COM9 rather than lexically before it.
    ''' </summary>
    Private Shared Function ComparePortNames(a As String, b As String) As Integer
        Dim na = ExtractNumber(a)
        Dim nb = ExtractNumber(b)
        If na >= 0 AndAlso nb >= 0 AndAlso na <> nb Then Return na.CompareTo(nb)
        Return String.Compare(a, b, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function ExtractNumber(s As String) As Integer
        Dim digits = New String(s.Where(AddressOf Char.IsDigit).ToArray())
        Dim n As Integer
        Return If(Integer.TryParse(digits, n), n, -1)
    End Function

    ''' <summary>
    ''' Opens the port at 8-N-1.
    ''' </summary>
    ''' <remarks>
    ''' DTR and RTS are left deasserted. On the common USB-serial bridge
    ''' wiring those two lines drive the module's reset and bootstrap pins
    ''' through a transistor pair, so asserting them on open resets the
    ''' device or drops it into its bootloader instead of running firmware.
    ''' Boards that instead expect DTR held active can opt in per connection.
    ''' </remarks>
    Public Sub Open(portName As String,
                    Optional baudRate As Integer = DefaultBaudRate,
                    Optional assertDtr As Boolean = False,
                    Optional assertRts As Boolean = False)

        Close()

        Dim p As New SerialPort(portName, baudRate, Parity.None, 8, StopBits.One) With {
            .Handshake = Handshake.None,
            .DtrEnable = assertDtr,
            .RtsEnable = assertRts,
            .ReadTimeout = 250,
            .WriteTimeout = 2000,
            .ReadBufferSize = 1 << 16,
            .WriteBufferSize = 1 << 16,
            .Encoding = Encoding.UTF8
        }

        p.Open()

        ' Discard whatever the driver buffered before we attached, so a
        ' half-line left over from a previous session cannot corrupt the
        ' first record we assemble.
        Try
            p.DiscardInBuffer()
            p.DiscardOutBuffer()
        Catch ex As Exception
        End Try

        SyncLock _sync
            _port = p
            _buffer.Clear()
            _bytesIn = 0
            _bytesOut = 0
            _running = True
        End SyncLock

        _reader = New Thread(AddressOf ReadLoop) With {
            .IsBackground = True,
            .Name = "serial-reader"
        }
        _reader.Start()
    End Sub

    Public Sub Close()
        Dim p As SerialPort

        SyncLock _sync
            _running = False
            p = _port
            _port = Nothing
        End SyncLock

        If p IsNot Nothing Then
            Try
                If p.IsOpen Then p.Close()
            Catch ex As Exception
            End Try
            p.Dispose()
        End If

        Dim t = _reader
        _reader = Nothing
        If t IsNot Nothing AndAlso t.IsAlive AndAlso t IsNot Thread.CurrentThread Then
            t.Join(1000)
        End If
    End Sub

    ''' <summary>
    ''' Writes one already-terminated record. Silently no-ops when the port
    ''' is closed; a send racing a disconnect is expected, not exceptional.
    ''' </summary>
    Public Sub SendLine(payload As String)
        If String.IsNullOrEmpty(payload) Then Return

        Dim p As SerialPort
        SyncLock _sync
            p = _port
        End SyncLock

        If p Is Nothing OrElse Not p.IsOpen Then Return

        Try
            Dim bytes = Encoding.UTF8.GetBytes(payload)
            p.Write(bytes, 0, bytes.Length)
            Interlocked.Add(_bytesOut, bytes.Length)
            RaiseEvent BytesTransferred(Me, Interlocked.Read(_bytesIn), Interlocked.Read(_bytesOut))
        Catch ex As Exception
            RaiseEvent Disconnected(Me, ex.Message)
        End Try
    End Sub

    Private Sub ReadLoop()
        Dim chunk(4095) As Byte

        While True
            Dim p As SerialPort
            SyncLock _sync
                If Not _running Then Exit While
                p = _port
            End SyncLock

            If p Is Nothing OrElse Not p.IsOpen Then Exit While

            Dim read As Integer
            Try
                read = p.Read(chunk, 0, chunk.Length)
            Catch ex As TimeoutException
                Continue While
            Catch ex As Exception
                SyncLock _sync
                    If Not _running Then Exit While
                End SyncLock
                RaiseEvent Disconnected(Me, ex.Message)
                Exit While
            End Try

            If read <= 0 Then Continue While

            Interlocked.Add(_bytesIn, read)
            Dispatch(chunk, read)
            RaiseEvent BytesTransferred(Me, Interlocked.Read(_bytesIn), Interlocked.Read(_bytesOut))
        End While
    End Sub

    ''' <summary>
    ''' Accumulates bytes and emits every complete newline-terminated record
    ''' found. A record is decoded as UTF-8 only once whole, so a multi-byte
    ''' character split across two reads still decodes correctly.
    ''' </summary>
    Private Sub Dispatch(chunk() As Byte, count As Integer)
        Dim complete As New List(Of String)()

        SyncLock _sync
            For i = 0 To count - 1
                Dim b = chunk(i)

                If b = 10 Then
                    If _buffer.Count > 0 Then
                        complete.Add(Encoding.UTF8.GetString(_buffer.ToArray()))
                        _buffer.Clear()
                    End If
                ElseIf b <> 13 Then
                    If _buffer.Count >= MaxLineBytes Then
                        _buffer.Clear()
                    End If
                    _buffer.Add(b)
                End If
            Next
        End SyncLock

        For Each line In complete
            RaiseEvent LineReceived(Me, line)
        Next
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        Close()
    End Sub

End Class
