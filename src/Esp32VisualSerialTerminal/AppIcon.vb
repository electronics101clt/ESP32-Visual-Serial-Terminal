Imports System.Drawing
Imports System.Reflection

''' <summary>
''' The application icon, loaded once from the embedded resource and shared by
''' every window.
''' </summary>
''' <remarks>
''' Setting the project's ApplicationIcon only stamps the executable, which is
''' what Explorer and the taskbar read. A Form still falls back to the default
''' Windows Forms icon unless its own Icon is assigned, so each window asks for
''' this one explicitly.
'''
''' The instance is cached and deliberately never disposed: it lives as long as
''' the process and is referenced by every open window, so disposing it on
''' behalf of one closing window would blank the others.
''' </remarks>
Public NotInheritable Class AppIcon

    Private Shared _cached As Icon
    Private Shared ReadOnly Gate As New Object()

    Private Sub New()
    End Sub

    Public Shared ReadOnly Property Current As Icon
        Get
            SyncLock Gate
                If _cached Is Nothing Then _cached = Load()
                Return _cached
            End SyncLock
        End Get
    End Property

    Private Shared Function Load() As Icon
        Try
            Dim asm = Assembly.GetExecutingAssembly()

            Dim name = asm.GetManifestResourceNames() _
                          .FirstOrDefault(Function(n) n.EndsWith("app.ico", StringComparison.OrdinalIgnoreCase))

            If name Is Nothing Then Return Nothing

            Using stream = asm.GetManifestResourceStream(name)
                Return New Icon(stream)
            End Using

        Catch ex As Exception
            ' A missing or malformed icon must not stop the application from
            ' starting; the window simply keeps the default one.
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Assigns the icon to a window, leaving it untouched if the resource could
    ''' not be loaded.
    ''' </summary>
    Public Shared Sub Apply(target As System.Windows.Forms.Form)
        If target Is Nothing Then Return

        Dim icon = Current
        If icon IsNot Nothing Then target.Icon = icon
    End Sub

End Class
