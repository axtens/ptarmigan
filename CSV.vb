Imports CsvHelper
Imports System.Text.RegularExpressions
Imports System.Linq
Imports System.IO
Imports System.Data
Imports System.Globalization
Imports System.Formats.Asn1

Namespace BODB

    Public Module CSV

        Friend Function DBCSV(ByVal PString As String) As (status As String, cargo As String)

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
            handle.Close()

            Dim reader = New StreamReader(fileName)
            Dim csv = New CsvReader(reader, CultureInfo.InvariantCulture)
            'dim records = csv.GetRecords()

            Dim dr = New CsvDataReader(csv)
            Dim dt = New DataTable()
            dt.Load(dr)

            Dim listOfFields As New List(Of String)
            For Each dc As DataColumn In dt.Columns
                listOfFields.Add(dc.ColumnName)
            Next

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
                Return ("", GetAllValuesOfFieldsInTable(dt, fieldNames))
            End If

            Return ("", GetAllRecordsWhereFieldEqualsValue(dt, fieldNames, fieldName, value))

        End Function

        Private Function GetAllValuesOfFieldsInTable(dt As DataTable, fieldNames As List(Of String)) As String
            Dim result As New List(Of String)
            For Each row As DataRow In dt.Rows
                Dim line As New List(Of String)
                For Each name As String In fieldNames
                    line.Add($"[{name}]{row.Item(name)}")
                Next
                result.Add(String.Join("^", line.ToArray))
            Next
            Return String.Join("~", result.ToArray)
        End Function

        Private Function GetAllRecordsWhereFieldEqualsValue(dt As DataTable, fieldNames As List(Of String), fieldName As String, value As String)
            Dim result As New List(Of String)
            For Each row As DataRow In dt.Rows
                Dim line As New List(Of String)
                If row.Item(fieldName) = value Then
                    For Each name As String In fieldNames
                        line.Add($"[{name}]{row.Item(name)}")
                    Next
                    result.Add(String.Join("^", line.ToArray))
                    Exit For
                End If
            Next
            Return String.Join("~", result.ToArray)
        End Function
    End Module

End Namespace
