Imports System.IO

''' <summary>
''' Linux-specific serial port enumeration. Scans /dev for ttyUSB*, ttyACM*,
''' and ttyS* devices that are likely to be real serial ports.
''' </summary>
Public NotInheritable Class LinuxSerialPorts

    Private Sub New()
    End Sub

    ''' <summary>
    ''' Returns available serial ports on Linux, sorted naturally.
    ''' </summary>
    Public Shared Function GetAvailablePorts() As String()
        Dim ports As New List(Of String)()

        Try
            ' USB serial adapters (most common for ESP32)
            ports.AddRange(Directory.GetFiles("/dev", "ttyUSB*"))

            ' USB CDC ACM devices (Arduino, ESP32-S2/S3 native USB)
            ports.AddRange(Directory.GetFiles("/dev", "ttyACM*"))

            ' Hardware serial ports (less common, filter to only existing ones)
            For Each ttyS In Directory.GetFiles("/dev", "ttyS*")
                ' Only include ttyS0-ttyS3 which are typically real ports
                Dim name = Path.GetFileName(ttyS)
                If name.Length = 5 AndAlso Char.IsDigit(name(4)) AndAlso CInt(name(4).ToString()) < 4 Then
                    ' Check if it's a real port by trying to read its attributes
                    Dim sysPath = $"/sys/class/tty/{name}/device"
                    If Directory.Exists(sysPath) Then
                        ports.Add(ttyS)
                    End If
                End If
            Next
        Catch ex As Exception
            ' Permission denied or other I/O error
        End Try

        ports.Sort(AddressOf ComparePortNames)
        Return ports.ToArray()
    End Function

    ''' <summary>
    ''' Natural sort: ttyUSB9 comes before ttyUSB10.
    ''' </summary>
    Private Shared Function ComparePortNames(a As String, b As String) As Integer
        Dim na = ExtractNumber(a)
        Dim nb = ExtractNumber(b)

        ' Group by prefix first
        Dim prefixA = ExtractPrefix(a)
        Dim prefixB = ExtractPrefix(b)
        Dim prefixCmp = String.Compare(prefixA, prefixB, StringComparison.Ordinal)
        If prefixCmp <> 0 Then Return prefixCmp

        ' Then by number
        If na >= 0 AndAlso nb >= 0 AndAlso na <> nb Then Return na.CompareTo(nb)
        Return String.Compare(a, b, StringComparison.Ordinal)
    End Function

    Private Shared Function ExtractPrefix(s As String) As String
        Dim name = Path.GetFileName(s)
        Dim i = 0
        While i < name.Length AndAlso Not Char.IsDigit(name(i))
            i += 1
        End While
        Return name.Substring(0, i)
    End Function

    Private Shared Function ExtractNumber(s As String) As Integer
        Dim digits = New String(Path.GetFileName(s).Where(AddressOf Char.IsDigit).ToArray())
        Dim n As Integer
        Return If(Integer.TryParse(digits, n), n, -1)
    End Function

End Class
