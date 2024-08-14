Imports Microsoft.Data.Sqlite
Imports System.Text.RegularExpressions
Imports System.Linq
Imports System.Data

Namespace BODB

    Public Module SQL

        Friend Function DBSqlite(ByVal PString As String) As (status As String, cargo As String)

            'PString: fileName '|' table '|' field '|' value 

            'Check for vertical bar char
            Dim verticalBarPos As Integer = InStr(PString, "|", CompareMethod.Text)
            If verticalBarPos = 0 Then
                Return ("BOBD-E-NO_VERTICAL_BAR_CHARS", PString)
            End If

            'split and fill to 4 slots
            Dim parts = Split(PString, "|", -1, CompareMethod.Text).ToList()
            Do While parts.Count < 4
                parts.Add(vbNullString)
            Loop

            Dim fileName As String = parts(0)
            'check filename
            If fileName = vbNullString Then
                Return ("BOBD-E-EMPTY_FILENAME", "")
            End If
            If Not IO.File.Exists(fileName) Then
                Return ("BOBD-E-FILE_NOT_FOUND", fileName)
            End If

            'check if file can be read
            Dim handle As IO.FileStream
            Try
                handle = IO.File.Open(fileName, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.ReadWrite)
            Catch ex As Exception
                Return ("BOBD-E-FILE_CANNOT_BE_READ", ex.Message)
            End Try

            'If Not handle.CanRead() Then
            'Return ("BOBD-E-FILE_CANNOT_BE_READ", fileName)
            'End If

            'check if fileName is a Sqlite database
            Dim reader = New System.IO.BinaryReader(handle)
            Dim First16 = reader.ReadBytes(16)
            If System.Text.Encoding.ASCII.GetString(First16) <> ("SQLite format 3" & vbNullChar) Then
                Return ("BOBD-E-FILE_NOT_SQLITE", fileName)
            End If
            reader.Close()
            handle.Close()

            'fileName might be a connection string

            Dim connectionString As String = ReworkConnectionString(fileName)
            'connect to sqlite file
            Dim connection As New SqliteConnection(connectionString)
            connection.Open()

            Dim tableName As String = parts(1)
            Dim listOfTables = GetListOfTables(connection)
            If tableName = vbNullString Then
                Return ("", String.Join("^", listOfTables))
            End If

            If listOfTables.IndexOf(tableName) = -1 Then
                Return ("BODB-E-TABLENAME_NOT_FOUND", PString)
            End If

            Dim listOfFields = GetAllFieldNamesInTable(connection, tableName)
            Dim fieldName = parts(2)
            If fieldName = vbNullString Then
                Return ("", Join(listOfFields.ToArray(), "^"))
            End If

            Dim fieldNames As List(Of String) = Split(fieldName, "^", -1, CompareMethod.Text).ToList
            For Each field In fieldNames
                If listOfFields.IndexOf(field) = -1 Then
                    Return ("BODB-E-FIELD_NOT_FOUND", PString)
                End If
            Next

            Dim value = parts(3)
            If value = vbNullString Then
                Return ("", GetAllValuesOfFieldsInTable(connection, tableName, fieldNames))
            End If

            Return ("", GetAllRecordsWhereFieldEqualsValue(connection, tableName, fieldName, value))
        End Function

        Private Function GetAllRecordsWhereFieldEqualsValue(connection As SqliteConnection, tableName As String, fieldName As String, value As String) As String
            Dim result As New List(Of String)
            Dim command = New SqliteCommand($"SELECT * FROM {tableName} WHERE {fieldName} = '{value}';", connection)
            Dim reader As SqliteDataReader = command.ExecuteReader()

            Dim row As New List(Of String)
            Do While reader.Read()
                For i = 0 To reader.FieldCount - 1
                    Dim name As String = reader.GetName(i)
                    Dim valu As String = reader.GetString(i)
                    'If name = fieldName AndAlso valu = value Then
                    row.Add($"[{name}]{valu}")
                    'End If
                Next
                result.Add(String.Join("^", row.ToArray()))
                row.Clear()
            Loop
            reader.Close()
            Return Join(result.ToArray(), "~")
        End Function

        Private Function GetAllValuesOfFieldsInTable(connection As SqliteConnection, tableName As String, fieldNames As List(Of String)) As String
            Dim result As New List(Of String)
            Dim command As New SqliteCommand($"SELECT {Join(fieldNames.ToArray, ",")} FROM {tableName};", connection)

            Dim reader As SqliteDataReader = command.ExecuteReader()

            Dim row As New List(Of String)
            Do While reader.Read()
                For i = 0 To reader.FieldCount - 1
                    Dim name As String = reader.GetName(i)
                    Dim valu As String = reader.GetString(i)
                    'If name = fieldName AndAlso valu = value Then
                    row.Add($"[{name}]{valu}")
                    'End If
                Next
                result.Add(String.Join("^", row.ToArray()))
                row.Clear()
            Loop
            reader.Close()
            Return Join(result.ToArray(), "~")
        End Function

        Private Function GetAllFieldNamesInTable(connection As SqliteConnection, tableName As String) As List(Of String)
            Dim names As New List(Of String)
            Dim command As New SqliteCommand($"PRAGMA table_info({tableName});", connection)
            Dim reader As SqliteDataReader = command.ExecuteReader()
            Do While reader.Read()
                Dim name = reader.GetString(1)
                names.Add(name)
            Loop
            reader.Close()
            Return names
        End Function

        Private Function GetAllRecordsInTable(connection As SqliteConnection, tableName As String) As String
            Dim result As New List(Of String)
            Dim command As New SqliteCommand($"SELECT * FROM {tableName};", connection)
            Dim reader As SqliteDataReader = command.ExecuteReader()
            Dim row As New List(Of String)
            Do While reader.Read()
                For i = 0 To reader.FieldCount - 1
                    Dim name As String = reader.GetName(i)
                    Dim valu As String = reader.GetString(i)
                    row.Add($"[{name}]{valu}")
                Next
                result.Add(String.Join("^", row.ToArray()))
            Loop
            reader.Close()
            Return Join(result.ToArray(), "~")
        End Function

        Private Function GetListOfTables(connection As SqliteConnection) As List(Of String)
            Dim tables As New List(Of String)
            Dim command As New SqliteCommand("PRAGMA table_list;", connection)
            Dim reader As SqliteDataReader = command.ExecuteReader()

            Do While reader.Read()
                Dim name As String = reader.GetString(1)
                If Not name.StartsWith("sqlite_") Then
                    tables.Add(name)
                End If
            Loop
            reader.Close()
            Return tables
        End Function

        Private Function ReworkConnectionString(fileName As String) As String
            Dim source As String = fileName
            If Right(source, 1) <> ";" Then
                source &= ";"
            End If

            Dim answer = UnPattern(source, "Data Source=(.*?);")
            Dim dataSource As String = answer.result
            source = answer.newSource

            answer = UnPattern(source, "Version=(\d);")
            Dim version As String = answer.result
            source = answer.newSource

            answer = UnPattern(source, "New=(True|False);")
            Dim newDB As String = answer.result
            source = answer.newSource

            answer = UnPattern(source, "UseUTF16Encoding=True;")
            Dim useUTF16Encoding As String = answer.result
            source = answer.newSource

            answer = UnPattern(source, "Password=(.*?);")
            source = answer.newSource
            Dim password As String = answer.result

            answer = UnPattern(source, "Legacy Format=(True|False);")
            source = answer.newSource
            Dim legacyFormat As String = answer.result

            answer = UnPattern(source, "Pooling=(True|False);")
            source = answer.newSource
            Dim pooling As String = answer.result

            answer = UnPattern(source, "Max Pool Size=(\d+?);")
            source = answer.newSource
            Dim maxPoolSize As String = answer.result

            answer = UnPattern(source, "BinaryGUID=(False|True);")
            source = answer.newSource
            Dim binaryGUID As String = answer.result

            answer = UnPattern(source, "Cache Size=(\d+?);")
            source = answer.newSource
            Dim cacheSize As String = answer.result

            answer = UnPattern(source, "Page Size=(\d+?);")
            source = answer.newSource
            Dim pageSize As String = answer.result

            answer = UnPattern(source, "Enlist=(N|Y);")
            source = answer.newSource
            Dim enlist As String = answer.result

            answer = UnPattern(source, "FailIfMissing=(True|False);")
            source = answer.newSource
            Dim failIfMissing As String = answer.result

            answer = UnPattern(source, "Max Page Count=(\d+?);")
            source = answer.newSource
            Dim maxPageCount As String = answer.result

            answer = UnPattern(source, "Journal Mode=(Off|On);")
            source = answer.newSource
            Dim journalMode As String = answer.result

            answer = UnPattern(source, "Synchronous=(Full|Normal);")
            source = answer.newSource
            Dim synchronous As String = answer.result


            If dataSource = vbNullString Then
                Return $"Data Source={fileName};"
            Else
                Return Join({
                            IIf(dataSource <> vbNullString, $"Data Source={dataSource};", vbNullString),
                            IIf(version <> vbNullString, $"Version={version};", vbNullString),
                            IIf(binaryGUID <> vbNullString, $"BinaryGUID={binaryGUID};", vbNullString),
                            IIf(cacheSize <> vbNullString, $"Cache Size={cacheSize};", vbNullString),
                            IIf(enlist <> vbNullString, $"Enlist={enlist};", vbNullString),
                            IIf(failIfMissing <> vbNullString, $"FailIfMissing={failIfMissing};", vbNullString),
                            IIf(journalMode <> vbNullString, $"Journal Mode={journalMode};", vbNullString),
                            IIf(legacyFormat <> vbNullString, $"Legacy Format={legacyFormat};", vbNullString),
                            IIf(maxPageCount <> vbNullString, $"Max Page Count={maxPageCount};", vbNullString),
                            IIf(maxPoolSize <> vbNullString, $"Max Pool Size={maxPoolSize};", vbNullString),
                            IIf(newDB <> vbNullString, $"New={newDB};", vbNullString),
                            IIf(pageSize <> vbNullString, $"Page Size={pageSize};", vbNullString),
                            IIf(password <> vbNullString, $"Password={password};", vbNullString),
                            IIf(pooling <> vbNullString, $"Pooling={pooling};", vbNullString),
                            IIf(synchronous <> vbNullString, $"Synchronous={synchronous};", vbNullString),
                            IIf(useUTF16Encoding <> vbNullString, $"UseUTF16Encoding={useUTF16Encoding};", vbNullString)
                        }, vbNullString)
            End If
        End Function

        Private Function UnPattern(source As String, pattern As String) As (newSource As String, result As String)
            Dim result As String = vbNullString
            Dim newSource As String = vbNullString
            If source <> vbNullString Then
                Dim regex As New Regex(pattern, RegexOptions.IgnoreCase)
                If regex.IsMatch(source) Then
                    Dim matches = regex.Matches(source)
                    result = matches.Item(0).Groups(1).Value.Trim()
                    newSource = source.Replace(matches.Item(0).Groups(0).Value, vbNullString)
                End If
            End If

            Return (newSource, result)
        End Function

    End Module

End Namespace
