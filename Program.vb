Imports System
Imports System.Reflection

Module Program
    Sub Main(args As String())
        If args.Count < 2 Then
            Console.WriteLine($"{AssemblyName.GetAssemblyName(Assembly.GetExecutingAssembly().Location).Name} -csv|-sql <string>")
            End
        End If
        Dim result As (String, String)
        Select Case args(0)
            Case "-csv"
                result = BODB.DBCSV(args(1))
            Case "-sql"
                result = BODB.DBSqlite(args(1))
            Case "-csd"
                Debugger.Launch()
                result = BODB.DBCSV(args(1))
            Case "-sqd"
                Debugger.Launch()
                result = BODB.DBSqlite(args(1))
            Case Else
                Console.WriteLine(args(0) + " method unknown.")
                End
        End Select

        If result.Item1 <> vbNullString Then
            Console.WriteLine(result.Item1)
        Else
            Console.WriteLine(result.Item2)
        End If
    End Sub
End Module
