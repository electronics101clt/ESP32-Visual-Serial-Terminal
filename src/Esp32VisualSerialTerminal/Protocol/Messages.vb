Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization


''' <summary>
''' Message type identifiers used on the wire. Values are the literal
''' strings carried in the "type" field and must not be renamed without a
''' matching protocol version bump.
''' </summary>
Public NotInheritable Class MessageType
    Private Sub New()
    End Sub

    ' Device -> host
    Public Const Html As String = "html"
    Public Const Update As String = "update"
    Public Const Notify As String = "notify"
    Public Const Dialog As String = "dialog"

    ' Host -> device
    Public Const GetPage As String = "get_page"
    Public Const [Event] As String = "event"

    ' Both directions: acknowledges the message currently in flight.
    Public Const Ack As String = "ack"
End Class

''' <summary>
''' One decoded message from the device. Only the fields relevant to the
''' message's own type are populated; the rest stay Nothing.
''' </summary>
Public NotInheritable Class DeviceMessage

    <JsonPropertyName("type")>
    Public Property Type As String

    <JsonPropertyName("body")>
    Public Property Body As String

    <JsonPropertyName("id")>
    Public Property Id As String

    <JsonPropertyName("text")>
    Public Property Text As String

    <JsonPropertyName("value")>
    Public Property Value As String

    <JsonPropertyName("title")>
    Public Property Title As String

    <JsonPropertyName("message")>
    Public Property Message As String

End Class

''' <summary>
''' Encodes and decodes the wire format: one JSON object per line, UTF-8,
''' newline terminated. Carriage returns are tolerated on input so a device
''' emitting CRLF still parses.
''' </summary>
Public NotInheritable Class LineCodec

    ''' <summary>Separator between the JSON payload and its checksum.</summary>
    Public Const CrcDelimiter As String = "|CRC:"

    Private Shared ReadOnly ReadOptions As New JsonSerializerOptions With {
        .PropertyNameCaseInsensitive = True,
        .AllowTrailingCommas = True,
        .ReadCommentHandling = JsonCommentHandling.Skip
    }

    Private Sub New()
    End Sub

    ''' <summary>
    ''' Wraps a JSON payload in the on-wire frame: the payload, the checksum
    ''' delimiter, its CRC-32, and a terminating line feed.
    ''' </summary>
    Public Shared Function Frame(json As String) As String
        Return json & CrcDelimiter & Crc32.Hex(json) & vbLf
    End Function

    ''' <summary>
    ''' Extracts and verifies the JSON payload of a received frame. Returns
    ''' Nothing when the frame carries no checksum or fails verification, in
    ''' which case the caller must stay silent -- a corrupted frame is not
    ''' acknowledged, and the sender retries it.
    ''' </summary>
    ''' <remarks>
    ''' Splits on the FIRST delimiter only. A payload may legitimately contain
    ''' the delimiter's own text, and splitting on all occurrences would reject
    ''' exactly those frames while the sender considered them valid -- leaving
    ''' both ends retrying a message neither could agree was well-formed.
    ''' </remarks>
    Public Shared Function Unframe(line As String) As String
        If String.IsNullOrEmpty(line) Then Return Nothing

        Dim trimmed = line.Trim()
        Dim pos = trimmed.IndexOf(CrcDelimiter, StringComparison.Ordinal)
        If pos < 0 Then Return Nothing

        Dim json = trimmed.Substring(0, pos)
        Dim received = trimmed.Substring(pos + CrcDelimiter.Length).Trim()

        If Not String.Equals(received, Crc32.Hex(json), StringComparison.OrdinalIgnoreCase) Then
            Return Nothing
        End If

        Return json
    End Function

    ''' <summary>
    ''' Parses a single line. Returns Nothing for blank lines, malformed
    ''' JSON, or an object with no usable "type" -- a device mid-reboot
    ''' emits boot log noise on the same wire, and that must not be fatal.
    ''' </summary>
    Public Shared Function Decode(line As String) As DeviceMessage
        If String.IsNullOrWhiteSpace(line) Then Return Nothing

        Dim trimmed = line.Trim()
        If Not trimmed.StartsWith("{"c) Then Return Nothing

        Try
            Dim msg = JsonSerializer.Deserialize(Of DeviceMessage)(trimmed, ReadOptions)
            If msg Is Nothing OrElse String.IsNullOrEmpty(msg.Type) Then Return Nothing
            Return msg
        Catch ex As JsonException
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Host -> device request for the current page. The optional file name
    ''' selects a named page where the device implements more than one.
    ''' </summary>
    Public Shared Function EncodeGetPage(Optional file As String = Nothing) As String
        If String.IsNullOrEmpty(file) Then
            Return Frame("{""type"":""get_page""}")
        End If

        Dim sb As New StringBuilder()
        sb.Append("{""type"":""get_page"",""file"":""")
        AppendEscaped(sb, file)
        sb.Append("""}")
        Return Frame(sb.ToString())
    End Function

    ''' <summary>
    ''' Acknowledges the device's in-flight message. The device retries an
    ''' unacknowledged message indefinitely, so failing to send this leaves it
    ''' repeating the same frame forever.
    ''' </summary>
    Public Shared Function EncodeAck() As String
        Return Frame("{""type"":""ack""}")
    End Function

    ''' <summary>
    ''' Host -> device user interaction. Value is optional and is emitted as
    ''' an empty string when absent, matching the device-side expectation of
    ''' a always-present field.
    ''' </summary>
    Public Shared Function EncodeEvent(id As String, action As String, value As String) As String
        Dim sb As New StringBuilder()
        sb.Append("{""type"":""event"",""id"":""")
        AppendEscaped(sb, If(id, String.Empty))
        sb.Append(""",""action"":""")
        AppendEscaped(sb, If(action, String.Empty))
        sb.Append(""",""value"":""")
        AppendEscaped(sb, If(value, String.Empty))
        sb.Append("""}")
        Return Frame(sb.ToString())
    End Function

    ''' <summary>
    ''' Minimal JSON string escaping. Kept hand-rolled rather than routed
    ''' through a serializer so the emitted bytes stay predictable and the
    ''' encoder has no allocation surprises on a hot path.
    ''' </summary>
    Private Shared Sub AppendEscaped(sb As StringBuilder, s As String)
        For Each c As Char In s
            Select Case c
                Case """"c : sb.Append("\""")
                Case "\"c : sb.Append("\\")
                Case ChrW(8) : sb.Append("\b")
                Case ChrW(12) : sb.Append("\f")
                Case ChrW(10) : sb.Append("\n")
                Case ChrW(13) : sb.Append("\r")
                Case ChrW(9) : sb.Append("\t")
                Case Else
                    If AscW(c) < 32 Then
                        sb.Append("\u").Append(AscW(c).ToString("x4"))
                    Else
                        sb.Append(c)
                    End If
            End Select
        Next
    End Sub

End Class
