Imports System.Text

''' <summary>
''' Standard IEEE 802.3 CRC-32, the same polynomial and bit order used by zlib
''' and by the checksum routines commonly available on microcontroller
''' toolchains, so both ends of the link agree without either side needing a
''' bespoke implementation.
''' </summary>
Public NotInheritable Class Crc32

    Private Const Polynomial As UInteger = &HEDB88320UI

    Private Shared ReadOnly Table As UInteger() = BuildTable()

    Private Sub New()
    End Sub

    Private Shared Function BuildTable() As UInteger()
        Dim t(255) As UInteger

        For i As UInteger = 0UI To 255UI
            Dim c = i
            For k = 0 To 7
                If (c And 1UI) <> 0UI Then
                    c = Polynomial Xor (c >> 1)
                Else
                    c >>= 1
                End If
            Next
            t(CInt(i)) = c
        Next

        Return t
    End Function

    Public Shared Function Compute(bytes As Byte()) As UInteger
        Dim crc As UInteger = &HFFFFFFFFUI

        For Each b In bytes
            crc = Table(CInt((crc Xor b) And &HFFUI)) Xor (crc >> 8)
        Next

        Return crc Xor &HFFFFFFFFUI
    End Function

    ''' <summary>
    ''' Checksum of a string's UTF-8 bytes as 8 uppercase hex digits, which is
    ''' the form that travels on the wire.
    ''' </summary>
    Public Shared Function Hex(s As String) As String
        Return Compute(Encoding.UTF8.GetBytes(If(s, String.Empty))).ToString("X8")
    End Function

End Class
