Imports System.Text
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Text

<Generator(LanguageNames.VisualBasic)>
Public NotInheritable Class TaggedUnionGen
    Implements IIncrementalGenerator

    Private Class UnionModel
        Public Property [Imports] As List(Of String)
        Public Property Unions As List(Of UnionDefinition)
        Public Property Records As List(Of RecordDefinition)

        Public ReadOnly Property IsEmpty As Boolean
            Get
                Return (Unions Is Nothing OrElse Unions.Count = 0) AndAlso
                    (Records Is Nothing OrElse Records.Count = 0)
            End Get
        End Property
    End Class

    Private Class UnionDefinition
        Public Property Accessibility As String
        Public Property UnionName As String
        Public Property TypeParameters As List(Of String)
        Public Property Cases As List(Of UnionCase)
        Public Property FileName As String
        Public Property DefContent As String
    End Class

    Private Class UnionCase
        Public Property CaseName As String
        Public Property TypeArguments As List(Of String)
    End Class

    Private Class RecordDefinition
        Public Property Accessibility As String
        Public Property RecordName As String
        Public Property TypeParameters As List(Of String)
        Public Property Properties As List(Of (Name As String, Type As String))
        Public Property FileName As String
        Public Property DefContent As String
    End Class

    Public Sub Initialize(context As IncrementalGeneratorInitializationContext) Implements IIncrementalGenerator.Initialize
        Dim unionFiles = context.AdditionalTextsProvider _
            .Where(Function(f) f.Path.EndsWith(".union", StringComparison.OrdinalIgnoreCase))

        Dim parsedData = unionFiles.Select(
            Function(file, cancelToken)
                Dim text = file.GetText(cancelToken)
                Dim filename = System.IO.Path.GetFileNameWithoutExtension(file.Path)
                Dim lines = If(text IsNot Nothing, text.Lines.Select(Function(l) l.ToString()), Enumerable.Empty(Of String))
                Return ParseUnionFile(lines, filename)
            End Function)

        context.RegisterSourceOutput(parsedData, Sub(outputContext, model)
                                                     If model.IsEmpty Then Return
                                                     GenerateCode(outputContext, model)
                                                 End Sub)
    End Sub

    Private Function ParseUnionFile(lines As IEnumerable(Of String), filename As String) As UnionModel
        Dim [imports] As New List(Of String)
        Dim unions As New List(Of UnionDefinition)
        Dim records As New List(Of RecordDefinition)

        Dim currentDefinition As StringBuilder = Nothing
        Dim inDefinition As Boolean = False

        For Each rawLine In lines
            Dim line = rawLine.Trim()

            If String.IsNullOrWhiteSpace(line) Then
                If inDefinition Then
                    currentDefinition.AppendLine()
                End If
                Continue For
            End If

            With rawLine.TrimStart()
                If .StartsWith("'") OrElse .StartsWith("REM ", StringComparison.OrdinalIgnoreCase) Then
                    Continue For
                End If
            End With

            If line.StartsWith("Imports ", StringComparison.OrdinalIgnoreCase) Then
                Dim ns = line.Substring(8).Trim()
                [imports].Add(ns)
                Continue For
            End If

            If line.StartsWith("Public ", StringComparison.OrdinalIgnoreCase) OrElse
               line.StartsWith("Friend ", StringComparison.OrdinalIgnoreCase) OrElse
               line.StartsWith("Record ", StringComparison.OrdinalIgnoreCase) OrElse
               line.StartsWith("Union ", StringComparison.OrdinalIgnoreCase) Then
                If inDefinition AndAlso currentDefinition IsNot Nothing Then
                    ProcessDefinition(currentDefinition.ToString(), filename, unions, records)
                End If
                currentDefinition = New StringBuilder()
                currentDefinition.AppendLine(rawLine)
                inDefinition = True
            ElseIf inDefinition Then
                currentDefinition.AppendLine(rawLine)
            End If
        Next

        If inDefinition AndAlso currentDefinition IsNot Nothing Then
            ProcessDefinition(currentDefinition.ToString(), filename, unions, records)
        End If

        Return New UnionModel With {
            .Imports = [imports].Distinct().ToList(),
            .Unions = unions,
            .Records = records
        }
    End Function

    Private Sub ProcessDefinition(defContent As String, filename As String, unions As List(Of UnionDefinition), records As List(Of RecordDefinition))
        Dim cleanContent = RemoveComments(defContent)
        Dim firstLine = cleanContent.Trim().Split({vbCr, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim()

        If String.IsNullOrWhiteSpace(firstLine) Then Return

        If firstLine.IndexOf("Union ", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Dim unionDef = ParseUnionDeclaration(cleanContent, filename)
            If unionDef IsNot Nothing Then
                unionDef.DefContent = defContent.Trim()
                unions.Add(unionDef)
            End If
        ElseIf firstLine.IndexOf("Record ", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Dim recordDef = ParseRecordDeclaration(cleanContent, filename)
            If recordDef IsNot Nothing Then
                recordDef.DefContent = defContent.Trim()
                records.Add(recordDef)
            End If
        End If
    End Sub

    Private Function RemoveComments(content As String) As String
        Dim lines = content.Split({vbCr, vbLf}, StringSplitOptions.None)
        Dim result As New StringBuilder()

        For Each line In lines
            Dim trimmed = line.TrimStart()
            If trimmed.StartsWith("'") OrElse trimmed.StartsWith("REM ", StringComparison.OrdinalIgnoreCase) _
                Then Continue For

            Dim commentIndex = line.IndexOf("'"c)
            If commentIndex >= 0 Then
                result.AppendLine(line.Substring(0, commentIndex))
            Else
                result.AppendLine(line)
            End If
        Next

        Return result.ToString()
    End Function

    Private Function SanitizeDeclaration(defContent As String) As String
        Dim sanitized = RemoveComments(defContent)
        sanitized = sanitized.Replace(vbCr, "").Replace(vbLf, " ").Replace(vbTab, " ")
        While sanitized.Contains("  ")
            sanitized = sanitized.Replace("  ", " ")
        End While
        sanitized = sanitized.Replace("( ", "(").Replace(" )", ")")
        Return sanitized.Trim()
    End Function

    Private Function ParseUnionDeclaration(content As String, filename As String) As UnionDefinition
        Dim cleanContent = content.Replace(vbCr, "").Replace(vbLf, " ").Replace(vbTab, " ").Trim()
        While cleanContent.Contains("  ")
            cleanContent = cleanContent.Replace("  ", " ")
        End While

        Dim trimmedLine = cleanContent.Trim()
        Dim pos = 0

        Dim accessibility As String = Nothing
        If trimmedLine.StartsWith("Public ", StringComparison.OrdinalIgnoreCase) Then
            accessibility = "Public"
            pos = 7
        ElseIf trimmedLine.StartsWith("Friend ", StringComparison.OrdinalIgnoreCase) Then
            accessibility = "Friend"
            pos = 7
        End If

        If Not trimmedLine.Substring(pos).StartsWith("Union ", StringComparison.OrdinalIgnoreCase) Then
            Return Nothing
        End If
        pos += 6

        SkipWhitespace(trimmedLine, pos)
        If pos >= trimmedLine.Length Then Return Nothing

        Dim nameStart = pos
        While pos < trimmedLine.Length AndAlso Not Char.IsWhiteSpace(trimmedLine(pos)) AndAlso trimmedLine(pos) <> "("c
            pos += 1
        End While
        Dim unionName = trimmedLine.Substring(nameStart, pos - nameStart)
        If String.IsNullOrWhiteSpace(unionName) Then Return Nothing

        Dim typeParams As New List(Of String)()
        Dim casesStr As String = Nothing
        SkipWhitespace(trimmedLine, pos)

        If pos >= trimmedLine.Length OrElse trimmedLine(pos) <> "("c Then
            Return Nothing
        End If

        Dim closeParen = FindMatchingParen(trimmedLine, pos)
        If closeParen = -1 Then Return Nothing

        Dim contentInside = trimmedLine.Substring(pos + 1, closeParen - pos - 1)
        pos = closeParen + 1

        If contentInside.StartsWith("Of ", StringComparison.OrdinalIgnoreCase) Then
            typeParams = contentInside.Substring(3).Split(","c).Select(Function(t) t.Trim()).Where(Function(t) Not String.IsNullOrWhiteSpace(t)).ToList()

            SkipWhitespace(trimmedLine, pos)
            If pos < trimmedLine.Length AndAlso trimmedLine(pos) = "("c Then
                closeParen = FindMatchingParen(trimmedLine, pos)
                If closeParen = -1 Then Return Nothing
                casesStr = trimmedLine.Substring(pos + 1, closeParen - pos - 1)
            Else
                Return Nothing
            End If
        Else
            casesStr = contentInside
        End If

        Dim cases = ParseUnionCases(casesStr)
        If cases.Count = 0 Then
            Return Nothing
        End If

        Return New UnionDefinition With {
            .Accessibility = accessibility,
            .UnionName = unionName,
            .TypeParameters = typeParams,
            .Cases = cases,
            .FileName = filename
        }
    End Function

    Private Function ParseRecordDeclaration(content As String, filename As String) As RecordDefinition
        Dim cleanContent = content.Replace(vbCr, "").Replace(vbLf, " ").Replace(vbTab, " ").Trim()
        While cleanContent.Contains("  ")
            cleanContent = cleanContent.Replace("  ", " ")
        End While

        Dim trimmedLine = cleanContent.Trim()
        Dim pos = 0

        Dim accessibility As String = Nothing
        If trimmedLine.StartsWith("Public ", StringComparison.OrdinalIgnoreCase) Then
            accessibility = "Public"
            pos = 7
        ElseIf trimmedLine.StartsWith("Friend ", StringComparison.OrdinalIgnoreCase) Then
            accessibility = "Friend"
            pos = 7
        End If

        If Not trimmedLine.Substring(pos).StartsWith("Record ", StringComparison.OrdinalIgnoreCase) Then
            Return Nothing
        End If
        pos += 7

        SkipWhitespace(trimmedLine, pos)
        If pos >= trimmedLine.Length Then Return Nothing

        Dim nameStart = pos
        While pos < trimmedLine.Length AndAlso Not Char.IsWhiteSpace(trimmedLine(pos)) AndAlso trimmedLine(pos) <> "("c
            pos += 1
        End While
        Dim recordName = trimmedLine.Substring(nameStart, pos - nameStart)
        If String.IsNullOrWhiteSpace(recordName) Then Return Nothing

        Dim typeParams As New List(Of String)()
        SkipWhitespace(trimmedLine, pos)

        If pos >= trimmedLine.Length OrElse trimmedLine(pos) <> "("c Then
            Return Nothing
        End If

        Dim closeParen = FindMatchingParen(trimmedLine, pos)
        If closeParen = -1 Then Return Nothing

        Dim contentInside = trimmedLine.Substring(pos + 1, closeParen - pos - 1)
        pos = closeParen + 1

        If contentInside.StartsWith("Of ", StringComparison.OrdinalIgnoreCase) Then
            typeParams = contentInside.Substring(3).Split(","c).Select(Function(t) t.Trim()).Where(Function(t) Not String.IsNullOrWhiteSpace(t)).ToList()

            SkipWhitespace(trimmedLine, pos)
            If pos >= trimmedLine.Length OrElse trimmedLine(pos) <> "("c Then
                Return Nothing
            End If

            closeParen = FindMatchingParen(trimmedLine, pos)
            If closeParen = -1 Then Return Nothing
            contentInside = trimmedLine.Substring(pos + 1, closeParen - pos - 1)
        End If

        Dim properties = ParseRecordProperties(contentInside)
        If properties Is Nothing Then Return Nothing

        Return New RecordDefinition With {
            .Accessibility = accessibility,
            .RecordName = recordName,
            .TypeParameters = typeParams,
            .Properties = properties,
            .FileName = filename
        }
    End Function

    Private Function ParseRecordProperties(propertiesStr As String) As List(Of (Name As String, Type As String))
        Dim properties As New List(Of (Name As String, Type As String))
        Dim parts = propertiesStr.Split(","c)

        For Each part In parts
            Dim trimmed = part.Trim()
            If String.IsNullOrWhiteSpace(trimmed) Then Continue For

            Dim asIndex = trimmed.IndexOf(" As ", StringComparison.OrdinalIgnoreCase)
            If asIndex = -1 Then
                properties.Add((trimmed, "Object"))
            Else
                Dim name = trimmed.Substring(0, asIndex).Trim()
                Dim typeName = trimmed.Substring(asIndex + 4).Trim()
                If String.IsNullOrWhiteSpace(name) Then Return Nothing
                properties.Add((name, If(String.IsNullOrWhiteSpace(typeName), "Object", typeName)))
            End If
        Next

        Return properties
    End Function

    Private Sub SkipWhitespace(line As String, ByRef pos As Integer)
        While pos < line.Length AndAlso Char.IsWhiteSpace(line(pos))
            pos += 1
        End While
    End Sub

    Private Function FindMatchingParen(line As String, startPos As Integer) As Integer
        If startPos >= line.Length OrElse line(startPos) <> "("c Then Return -1

        Dim depth = 1
        Dim pos = startPos + 1
        While pos < line.Length AndAlso depth > 0
            Dim ch = line(pos)
            If ch = "("c Then
                depth += 1
            ElseIf ch = ")"c Then
                depth -= 1
                If depth = 0 Then Return pos
            End If
            pos += 1
        End While
        Return -1
    End Function

    Private Function ParseUnionCases(casesStr As String) As List(Of UnionCase)
        Dim cases As New List(Of UnionCase)
        Dim pos = 0

        While pos < casesStr.Length
            SkipWhitespace(casesStr, pos)
            If pos >= casesStr.Length Then Exit While

            If casesStr(pos) = ","c Then
                pos += 1
                Continue While
            End If

            Dim caseStart = pos
            If casesStr(pos) = "["c Then
                Dim endBracket = casesStr.IndexOf("]"c, pos)
                If endBracket = -1 Then Return Nothing
                pos = endBracket + 1
                Dim depth = 0
                While pos < casesStr.Length
                    Dim ch = casesStr(pos)
                    If ch = "("c Then
                        depth += 1
                    ElseIf ch = ")"c Then
                        depth -= 1
                        If depth = 0 Then
                            pos += 1
                            Exit While
                        End If
                    ElseIf ch = ","c AndAlso depth = 0 Then
                        Exit While
                    End If
                    pos += 1
                End While
            Else
                Dim depth = 0
                While pos < casesStr.Length
                    Dim ch = casesStr(pos)
                    If ch = "("c Then
                        depth += 1
                    ElseIf ch = ")"c Then
                        depth -= 1
                    ElseIf ch = ","c AndAlso depth = 0 Then
                        Exit While
                    End If
                    pos += 1
                End While
            End If

            Dim caseStr = casesStr.Substring(caseStart, pos - caseStart).Trim()
            If Not String.IsNullOrWhiteSpace(caseStr) Then
                Dim [case] = ParseSingleUnionCase(caseStr)
                If [case] IsNot Nothing Then
                    cases.Add([case])
                End If
            End If
        End While

        Return cases
    End Function

    Private Function ParseSingleUnionCase(caseStr As String) As UnionCase
        Dim caseName As String, typeArgs As New List(Of String)

        If caseStr.StartsWith("["c) Then
            Dim endBracket = caseStr.IndexOf("]"c)
            If endBracket = -1 Then Return Nothing
            caseName = caseStr.Substring(0, endBracket + 1)
            Dim remainder = caseStr.Substring(endBracket + 1).Trim()
            If remainder.StartsWith("("c) Then
                Dim closeParen = FindMatchingParen(remainder, 0)
                If closeParen = -1 Then Return Nothing
                Dim typeParamsContent = remainder.Substring(1, closeParen - 1)
                If typeParamsContent.StartsWith("Of ", StringComparison.OrdinalIgnoreCase) Then
                    typeArgs = typeParamsContent.Substring(3).Split(","c).Select(Function(t) t.Trim()).Where(Function(t) Not String.IsNullOrWhiteSpace(t)).ToList()
                End If
            End If
        Else
            Dim parenStart = caseStr.IndexOf("("c)
            If parenStart = -1 Then
                caseName = caseStr.Trim()
            Else
                caseName = caseStr.Substring(0, parenStart).Trim()
                Dim closeParen = FindMatchingParen(caseStr, parenStart)
                If closeParen = -1 Then Return Nothing
                Dim typeParamsContent = caseStr.Substring(parenStart + 1, closeParen - parenStart - 1)
                If typeParamsContent.StartsWith("Of ", StringComparison.OrdinalIgnoreCase) Then
                    typeArgs = typeParamsContent.Substring(3).Split(","c).Select(Function(t) t.Trim()).Where(Function(t) Not String.IsNullOrWhiteSpace(t)).ToList()
                End If
            End If
        End If

        If String.IsNullOrWhiteSpace(caseName) Then Return Nothing
        Return New UnionCase With {
            .CaseName = caseName,
            .TypeArguments = typeArgs
        }
    End Function

    Private Sub GenerateCode(outputContext As SourceProductionContext, model As UnionModel)
        Dim allDefinitions = New List(Of Object)()
        If model.Unions IsNot Nothing Then allDefinitions.AddRange(model.Unions)
        If model.Records IsNot Nothing Then allDefinitions.AddRange(model.Records)

        Dim definitionsByFileName = From def In allDefinitions
                                    Group def By filename = If(TypeOf def Is UnionDefinition, DirectCast(def, UnionDefinition).FileName, DirectCast(def, RecordDefinition).FileName)
                                    Into defs = Group

        For Each group In definitionsByFileName
            Dim code As New StringBuilder
            code.AppendLine("' <auto-generated>")
            code.AppendLine("'     This code was generated by `TaggedUnionVB.Generator`")
            code.AppendLine("'     Changes to this file may cause incorrect behavior and will be lost if")
            code.AppendLine("'     the code is regenerated.")
            code.AppendLine("' </auto-generated>")
            code.AppendLine()
            code.AppendLine("Option Explicit On")
            code.AppendLine("Option Strict On")
            code.AppendLine("Imports [Out] = System.Runtime.InteropServices.OutAttribute")

            For Each ns In model.Imports
                code.AppendLine($"Imports {ns}")
            Next

            If model.Imports.Any() Then code.AppendLine()

            For Each def In group.defs
                If TypeOf def Is UnionDefinition Then
                    GenerateUnionClass(code, DirectCast(def, UnionDefinition))
                ElseIf TypeOf def Is RecordDefinition Then
                    GenerateRecordClass(code, DirectCast(def, RecordDefinition))
                End If
                code.AppendLine()
            Next

            outputContext.AddSource($"{group.filename}_TaggedUnion.g.vb", SourceText.From(code.ToString(), Encoding.UTF8))
        Next
    End Sub

    Private Sub GenerateUnionClass(code As StringBuilder, unionDef As UnionDefinition)
        Dim typeParamList = If(unionDef.TypeParameters.Any(), $"(Of {String.Join(", ", unionDef.TypeParameters)})", "")
        Dim accessibility = If(String.IsNullOrEmpty(unionDef.Accessibility), "", unionDef.Accessibility & " ")

        code.AppendLine("''' <summary>")
        code.AppendLine($"''' <para>Tagged Union: {unionDef.UnionName}{typeParamList}</para>")
        code.AppendLine($"''' <para>From declaration: <c>{SanitizeDeclaration(unionDef.DefContent)}</c></para>")
        code.AppendLine("''' </summary>")
        code.AppendLine($"{accessibility}MustInherit Class {unionDef.UnionName}{typeParamList}")
        code.AppendLine($"    Private Sub New()")
        code.AppendLine($"    End Sub")
        code.AppendLine()

        GeneratePatternMatchingMethods(code, unionDef)
        For Each [case] In unionDef.Cases
            GenerateCaseWrapper(code, unionDef, [case])
        Next
        GenerateConversionOperators(code, unionDef)

        code.AppendLine($"End Class")
    End Sub

    Private Sub GeneratePatternMatchingMethods(code As StringBuilder, unionDef As UnionDefinition)
        For Each [case] In unionDef.Cases
            Dim caseTypeArgs = If([case].TypeArguments.Any(), $"(Of {String.Join(", ", [case].TypeArguments)})", "")
            Dim caseType = $"{[case].CaseName}{caseTypeArgs}"
            Dim cleanCaseName = If([case].CaseName.StartsWith("["), [case].CaseName.Substring(1, [case].CaseName.Length - 2), [case].CaseName)

            code.AppendLine($"    ''' <summary>")
            code.AppendLine($"    ''' Returns True if this {unionDef.UnionName} is a {cleanCaseName}{caseTypeArgs}, otherwise False.")
            code.AppendLine($"    ''' </summary>")
            code.AppendLine($"    Public Function Is{cleanCaseName}() As Boolean")
            code.AppendLine($"        Return TypeOf Me Is {cleanCaseName}_Case")
            code.AppendLine($"    End Function")
            code.AppendLine()
            code.AppendLine($"    ''' <summary>")
            code.AppendLine($"    ''' Returns the {caseType} value if this is a {cleanCaseName}, otherwise throws.")
            code.AppendLine($"    ''' </summary>")
            code.AppendLine($"    Public Function As{cleanCaseName}() As {caseType}")
            code.AppendLine($"        Dim wrapper As {cleanCaseName}_Case = TryCast(Me, {cleanCaseName}_Case)")
            code.AppendLine($"        If wrapper Is Nothing Then")
            code.AppendLine($"            Throw New InvalidOperationException(""{unionDef.UnionName} is not a {cleanCaseName}{caseTypeArgs}."")")
            code.AppendLine($"        End If")
            code.AppendLine($"        Return wrapper.Value")
            code.AppendLine($"    End Function")
            code.AppendLine()
        Next
    End Sub

    Private Sub GenerateCaseWrapper(code As StringBuilder, unionDef As UnionDefinition, [case] As UnionCase)
        Dim typeParamList = If(unionDef.TypeParameters.Any(), $"(Of {String.Join(", ", unionDef.TypeParameters)})", "")
        Dim caseTypeArgs = If([case].TypeArguments.Any(), $"(Of {String.Join(", ", [case].TypeArguments)})", "")
        Dim caseType = $"{[case].CaseName}{caseTypeArgs}"
        Dim cleanCaseName = If([case].CaseName.StartsWith("["), [case].CaseName.Substring(1, [case].CaseName.Length - 2), [case].CaseName)
        Dim accessibility = If(String.IsNullOrEmpty(unionDef.Accessibility), "", unionDef.Accessibility & " ")

        code.AppendLine($"    ''' <summary>")
        code.AppendLine($"    ''' Wrapper for {caseType} in {unionDef.UnionName}{typeParamList}.")
        code.AppendLine($"    ''' </summary>")
        code.AppendLine($"    {accessibility}NotInheritable Class {cleanCaseName}_Case")
        code.AppendLine($"        Inherits {unionDef.UnionName}{typeParamList}")
        code.AppendLine()
        code.AppendLine($"        ''' <summary>")
        code.AppendLine($"        ''' The underlying {caseType} value.")
        code.AppendLine($"        ''' </summary>")
        code.AppendLine($"        Public ReadOnly Property Value As {caseType}")
        code.AppendLine()
        code.AppendLine($"        ''' <summary>")
        code.AppendLine($"        ''' Creates a new {cleanCaseName}_Case with the specified {caseType} value.")
        code.AppendLine($"        ''' </summary>")
        code.AppendLine($"        ''' <param name=""value"">The {caseType} value</param>")
        code.AppendLine($"        Public Sub New(value As {caseType})")
        code.AppendLine($"            Me.Value = value")
        code.AppendLine($"        End Sub")
        code.AppendLine($"    End Class")
        code.AppendLine()
    End Sub

    Private Sub GenerateConversionOperators(code As StringBuilder, unionDef As UnionDefinition)
        Dim typeParamList = If(unionDef.TypeParameters.Any(), $"(Of {String.Join(", ", unionDef.TypeParameters)})", "")

        For Each [case] In unionDef.Cases
            Dim caseTypeArgs = If([case].TypeArguments.Any(), $"(Of {String.Join(", ", [case].TypeArguments)})", "")
            Dim caseType = $"{[case].CaseName}{caseTypeArgs}"
            Dim cleanCaseName = If([case].CaseName.StartsWith("["), [case].CaseName.Substring(1, [case].CaseName.Length - 2), [case].CaseName)

            code.AppendLine($"    ''' <summary>")
            code.AppendLine($"    ''' Implicit conversion from {caseType} to {unionDef.UnionName}{typeParamList}.")
            code.AppendLine($"    ''' </summary>")
            code.AppendLine($"    Public Shared Widening Operator CType(value As {caseType}) As {unionDef.UnionName}{typeParamList}")
            code.AppendLine($"        Return New {cleanCaseName}_Case(value)")
            code.AppendLine($"    End Operator")
            code.AppendLine()
        Next
    End Sub

    Private Sub GenerateRecordClass(code As StringBuilder, recordDef As RecordDefinition)
        Dim typeParamList = If(recordDef.TypeParameters.Any(), $"(Of {String.Join(", ", recordDef.TypeParameters)})", "")
        Dim accessibility = If(String.IsNullOrEmpty(recordDef.Accessibility), "", recordDef.Accessibility & " ")

        Dim fullRecordName = recordDef.RecordName & typeParamList
        code.AppendLine("''' <summary>")
        code.AppendLine($"''' <para>Record: {fullRecordName}</para>")
        code.AppendLine($"''' <para>From declaration: <c>{SanitizeDeclaration(recordDef.DefContent)}</c></para>")
        code.AppendLine("''' </summary>")
        code.AppendLine($"{accessibility}NotInheritable Class {fullRecordName}")
        code.AppendLine()

        For Each prop In recordDef.Properties
            code.AppendLine($"    ''' <summary>")
            code.AppendLine($"    ''' The {prop.Name} value.")
            code.AppendLine($"    ''' </summary>")
            code.AppendLine($"    Public ReadOnly Property {prop.Name} As {prop.Type}")
        Next
        code.AppendLine()

        code.AppendLine($"    ''' <summary>")
        code.AppendLine($"    ''' Creates a new {fullRecordName} with the specified properties.")
        code.AppendLine($"    ''' </summary>")
        code.AppendLine($"    Public Sub New({String.Join(", ", From p In recordDef.Properties Select $"{p.Name} As {p.Type}")})")
        For Each prop In recordDef.Properties
            code.AppendLine($"        Me.{prop.Name} = {prop.Name}")
        Next
        code.AppendLine($"    End Sub")
        code.AppendLine()

        ' Generate With() method
        code.AppendLine($"    ''' <summary>")
        code.AppendLine($"    ''' Creates a new {fullRecordName} with the specified properties changed.")
        code.AppendLine($"    ''' </summary>")
        Dim optionalParams = From p In recordDef.Properties Select $"Optional {p.Name} As {p.Type} = Nothing"
        code.AppendLine($"    Public Function [With]({String.Join(", ", optionalParams)}) As {fullRecordName}")
        code.AppendLine($"        Return New {fullRecordName}(")
        For i = 0 To recordDef.Properties.Count - 1
            Dim prop = recordDef.Properties(i)
            Dim comma = If(i < recordDef.Properties.Count - 1, ",", "")
            code.AppendLine($"            {prop.Name} := If({prop.Name}, Me.{prop.Name})" & comma)
        Next
        code.AppendLine($"        )")
        code.AppendLine($"    End Function")
        code.AppendLine()

        ' Generate Deconstruct() method
        code.AppendLine($"    ''' <summary>")
        code.AppendLine($"    ''' Deconstructs the {fullRecordName} into its properties.")
        code.AppendLine($"    ''' </summary>")
        Dim propNames = From p In recordDef.Properties Select $"<Out> ByRef {p.Name} As {p.Type}"
        code.AppendLine($"    Public Sub Deconstruct({String.Join(", ", propNames)})")
        For Each prop In recordDef.Properties
            code.AppendLine($"        {prop.Name} = Me.{prop.Name}")
        Next
        code.AppendLine($"    End Sub")
        code.AppendLine()

        ' Generate ToString() method
        code.AppendLine($"    ''' <summary>")
        code.AppendLine($"    ''' Returns a string representation of the {fullRecordName}.")
        code.AppendLine($"    ''' </summary>")
        code.AppendLine($"    Public Overrides Function ToString() As String")
        If recordDef.Properties.Any() Then
            code.AppendLine($"        Dim propParts As New List(Of String) From {{")
            Dim propParts = From p In recordDef.Properties Select $"NameOf({p.Name}) & "" = "" & If(Me.{p.Name} Is Nothing, ""Nothing"", Me.{p.Name}.ToString())"
            For i = 0 To propParts.Count - 1
                Dim comma = If(i < propParts.Count - 1, ",", "")
                code.AppendLine($"            {propParts(i)}" & comma)
            Next
            code.AppendLine($"        }}")
            code.AppendLine($"        Return $""{fullRecordName} {{{{ {{propParts}} }}}}""")
        Else
            code.AppendLine($"        Return $""{fullRecordName} {{{{ }}}}""")
        End If
        code.AppendLine($"    End Function")
        code.AppendLine()

        ' Generate Equals() method
        code.AppendLine($"    ''' <summary>")
        code.AppendLine($"    ''' Determines whether the specified object is equal to the current {fullRecordName}.")
        code.AppendLine($"    ''' </summary>")
        code.AppendLine($"    Public Overrides Function Equals(obj As Object) As Boolean")
        code.AppendLine($"        If obj Is Nothing Then Return False")
        code.AppendLine($"        If TypeOf obj IsNot {fullRecordName} Then Return False")
        code.AppendLine($"        Dim other As {fullRecordName} = DirectCast(obj, {fullRecordName})")
        If recordDef.Properties.Any() Then
            code.AppendLine($"        Return {String.Join(" AndAlso ", From p In recordDef.Properties Select $"Object.Equals(Me.{p.Name}, other.{p.Name})")}")
        Else
            code.AppendLine($"        Return True")
        End If
        code.AppendLine($"    End Function")
        code.AppendLine()

        ' Generate GetHashCode() method
        code.AppendLine($"    ''' <summary>")
        code.AppendLine($"    ''' Returns the hash code for this {fullRecordName}.")
        code.AppendLine($"    ''' </summary>")
        code.AppendLine($"    Public Overrides Function GetHashCode() As Integer")
        If recordDef.Properties.Any() Then
            code.AppendLine($"        Return HashCode.Combine({String.Join(", ", From p In recordDef.Properties Select $"Me.{p.Name}")})")
        Else
            code.AppendLine($"        Return GetType({fullRecordName}).GetHashCode()")
        End If
        code.AppendLine($"    End Function")
        code.AppendLine()

        code.AppendLine($"End Class")
    End Sub
End Class