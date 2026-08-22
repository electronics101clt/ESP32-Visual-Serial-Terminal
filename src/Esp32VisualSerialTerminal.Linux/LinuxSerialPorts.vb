Imports System.IO

''' <summary>
''' Enumerates serial devices on Linux.
''' </summary>
''' <remarks>
''' SerialPort.GetPortNames is not dependable here: on Linux it reports what the
''' tty drivers advertise, which routinely includes a long list of onboard
''' /dev/ttyS* nodes that no device is attached to, while the USB adapter you
''' actually care about is buried among them. Reading the device tree directly
''' gives the real picture.
'''
''' /dev/serial/by-id is preferred where udev provides it, because those names
''' identify the adapter rather than the order it happened to enumerate in --
''' the same board keeps the same name across reboots and across USB ports.
''' </remarks>
Public NotInheritable Class LinuxSerialPorts

    Private Sub New()
    End Sub

    Public Structure PortInfo
        ''' <summary>Path to open, always a real device node.</summary>
        Public Path As String
        ''' <summary>Stable identifier where udev supplied one.</summary>
        Public Description As String

        Public Overrides Function ToString() As String
            If String.IsNullOrEmpty(Description) OrElse Description = Path Then Return Path
            Return $"{Path}  ({Description})"
        End Function
    End Structure

    ''' <summary>
    ''' Returns candidate serial devices, most likely first. USB adapters come
    ''' before onboard UARTs, which are usually not what is wanted and are often
    ''' not connected to anything.
    ''' </summary>
    Public Shared Function Enumerate(Optional includeOnboard As Boolean = False) As List(Of PortInfo)
        Dim found As New List(Of PortInfo)()
        Dim seen As New HashSet(Of String)(StringComparer.Ordinal)

        ' Stable udev names first.
        Try
            Const byId = "/dev/serial/by-id"
            If Directory.Exists(byId) Then
                For Each link In Directory.GetFiles(byId).Concat(Directory.GetFileSystemEntries(byId))
                    Dim target = ResolveLink(link)
                    If target Is Nothing OrElse Not seen.Add(target) Then Continue For

                    found.Add(New PortInfo With {
                        .Path = target,
                        .Description = Path.GetFileName(link)
                    })
                Next
            End If
        Catch ex As Exception
        End Try

        ' Then the usual USB-serial device nodes.
        For Each pattern In {"ttyUSB*", "ttyACM*"}
            For Each dev In SafeGlob("/dev", pattern)
                If seen.Add(dev) Then found.Add(New PortInfo With {.Path = dev, .Description = Nothing})
            Next
        Next

        If includeOnboard Then
            For Each dev In SafeGlob("/dev", "ttyS*")
                If seen.Add(dev) Then found.Add(New PortInfo With {.Path = dev, .Description = "onboard"})
            Next
        End If

        Return found
    End Function

    ' Parameter is 'folder', not 'directory': the latter shadows System.IO.Directory.
    Private Shared Function SafeGlob(folder As String, pattern As String) As String()
        Try
            If Not Directory.Exists(folder) Then Return Array.Empty(Of String)()
            Dim files = Directory.GetFiles(folder, pattern)
            Array.Sort(files, StringComparer.Ordinal)
            Return files
        Catch ex As Exception
            Return Array.Empty(Of String)()
        End Try
    End Function

    ''' <summary>
    ''' Follows a symlink to the device node it points at.
    ''' </summary>
    Private Shared Function ResolveLink(path As String) As String
        Try
            Dim info As New FileInfo(path)
            Dim target = info.ResolveLinkTarget(returnFinalTarget:=True)
            If target IsNot Nothing Then Return target.FullName

            ' Not a link: only useful if it is already a device node.
            If path.StartsWith("/dev/tty", StringComparison.Ordinal) Then Return path
            Return Nothing
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Whether the current user can actually open the device. On Ubuntu the
    ''' serial nodes are owned by the dialout group, and a user outside it gets a
    ''' permission error that reads like a missing device.
    ''' </summary>
    Public Shared Function CanAccess(devicePath As String) As Boolean
        Try
            Using fs = New FileStream(devicePath, FileMode.Open, FileAccess.ReadWrite)
                Return True
            End Using
        Catch ex As UnauthorizedAccessException
            Return False
        Catch ex As Exception
            ' Busy, or some other condition that is not a permissions problem.
            Return True
        End Try
    End Function

End Class
