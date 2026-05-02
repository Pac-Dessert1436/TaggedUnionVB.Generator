Imports System.Text
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Text

<Generator(LanguageNames.VisualBasic)>
Public NotInheritable Class TaggedUnionGen
    Implements IIncrementalGenerator

    Private Class UnionModel
        Public Property [Imports] As List(Of String)
        Public Property Unions As List(Of UnionDefinition)

        Public ReadOnly Property IsEmpty As Boolean
            Get
                Return Unions Is Nothing OrElse Unions.Count = 0
            End Get
        End Property
    End Class

    Private Class UnionDefinition
        Public Property Accessibility As String
        Public Property UnionName As String
        Public Property TypeParameters As List(Of String)
        Public Property Cases As List(Of UnionCase)
        Public Property FileName As String
        Public Property LineContent As String
    End Class

    Private Class UnionCase
        Public Property CaseName As String
        Public Property TypeArguments As List(Of String)
    End Class

    Public Sub Initialize(context As IncrementalGeneratorInitializationContext) Implements IIncrementalGenerator.Initialize
        Dim unionFiles = context.AdditionalTextsProvider _
            .Where(Function(f) f.Path.EndsWith(".union", StringComparison.OrdinalIgnoreCase))

        Dim parsedData = unionFiles.Select(
            Function(file, cancelToken)
                Dim text = file.GetText(cancelToken)
                Dim filename = System.IO.Path.GetFileNameWithoutExtension(file.Path)
                Dim lines = If(text IsNot Nothing, text.Lines.Select(Function(l) l.ToString().Trim()), Enumerable.Empty(Of String))
                Return ParseUnionFile(lines, filename)
            End Function)

        context.RegisterSourceOutput(parsedData, Sub(outputContext, model)
                                                     If model.IsEmpty Then Return
                                                     GenerateUnionCode(outputContext, model)
                                                 End Sub)
    End Sub

    Private Function ParseUnionFile(lines As IEnumerable(Of String), filename As String) As UnionModel
        Dim [imports] As New List(Of String)
        Dim unions As New List(Of UnionDefinition)

        For Each line In lines
            If String.IsNullOrWhiteSpace(line) Then Continue For
            Dim trimmed = line.TrimStart()
            If trimmed.StartsWith("'") OrElse trimmed.StartsWith("REM ", StringComparison.OrdinalIgnoreCase) Then Continue For

            If line.StartsWith("Imports ", StringComparison.OrdinalIgnoreCase) Then
                Dim ns = line.Substring(8).Trim()
                [imports].Add(ns)
                Continue For
            End If

            Dim unionDef = ParseUnionDeclaration(line, filename)
            If unionDef IsNot Nothing Then
                unions.Add(unionDef)
            End If
        Next

        Return New UnionModel With {
            .Imports = [imports].Distinct().ToList(),
            .Unions = unions
        }
    End Function

    Private Function ParseUnionDeclaration(line As String, filename As String) As UnionDefinition
        Dim trimmedLine = line.Trim()
        Dim pos = 0

        Dim accessibility = ""
        If trimmedLine.StartsWith("Public ", StringComparison.OrdinalIgnoreCase) Then
            accessibility = "Public"
            pos = 7
        ElseIf trimmedLine.StartsWith("Friend ", StringComparison.OrdinalIgnoreCase) Then
            accessibility = "Friend"
            pos = 6
        End If

        If pos + 6 > trimmedLine.Length OrElse Not trimmedLine.Substring(pos, 6).Equals("Union ", StringComparison.OrdinalIgnoreCase) Then
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

        Dim content = trimmedLine.Substring(pos + 1, closeParen - pos - 1)
        pos = closeParen + 1

        If content.StartsWith("Of ", StringComparison.OrdinalIgnoreCase) Then
            typeParams = content.Substring(3).Split(","c).Select(Function(t) t.Trim()).Where(Function(t) Not String.IsNullOrWhiteSpace(t)).ToList()

            SkipWhitespace(trimmedLine, pos)
            If pos >= trimmedLine.Length OrElse trimmedLine(pos) <> "("c Then
                Return Nothing
            End If

            Dim casesCloseParen = FindMatchingParen(trimmedLine, pos)
            If casesCloseParen = -1 Then Return Nothing
            casesStr = trimmedLine.Substring(pos + 1, casesCloseParen - pos - 1)
        Else
            casesStr = content
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
            .FileName = filename,
            .LineContent = line
        }
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
                If depth = 0 Then
                    Return pos
                End If
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
                If endBracket = -1 Then
                    Return Nothing
                End If
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

    Private Sub GenerateUnionCode(outputContext As SourceProductionContext, model As UnionModel)
        Dim unionsByFileName = From union In model.Unions
                               Group union By filename = union.FileName
                               Into unionDefs = Group
        For Each unionGroup In unionsByFileName
            Dim code As New StringBuilder
            code.AppendLine("' <auto-generated>")
            code.AppendLine("'     This code was generated by `TaggedUnionVB.Generator`")
            code.AppendLine("'     Changes to this file may cause incorrect behavior and will be lost if")
            code.AppendLine("'     the code is regenerated.")
            code.AppendLine("' </auto-generated>")
            code.AppendLine()
            code.AppendLine("Option Explicit On")
            code.AppendLine("Option Strict On")

            For Each ns In model.Imports
                code.AppendLine($"Imports {ns}")
            Next

            If model.Imports.Any() Then code.AppendLine()
            For Each unionDef In unionGroup.unionDefs
                GenerateUnionClass(code, unionDef)
                code.AppendLine()
            Next

            outputContext.AddSource($"{unionGroup.filename}_TaggedUnion.g.vb", SourceText.From(code.ToString(), Encoding.UTF8))
        Next
    End Sub

    Private Sub GenerateUnionClass(code As StringBuilder, unionDef As UnionDefinition)
        Dim typeParamList = If(unionDef.TypeParameters.Any(), $"(Of {String.Join(", ", unionDef.TypeParameters)})", "")
        Dim accessibility = If(String.IsNullOrEmpty(unionDef.Accessibility), "", unionDef.Accessibility & " ")

        code.AppendLine("''' <summary>")
        code.AppendLine($"''' <para>Tagged Union: {unionDef.UnionName}{typeParamList}</para>")
        code.AppendLine($"''' <para>From declaration: <c>{unionDef.LineContent}</c></para>")
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
            code.AppendLine($"    ''' Returns True if this {unionDef.UnionName} is a {cleanCaseName}{caseTypeArgs}")
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
        code.AppendLine($"    ''' Wrapper for {caseType} in {unionDef.UnionName}{typeParamList}")
        code.AppendLine($"    ''' </summary>")
        code.AppendLine($"    {accessibility}NotInheritable Class {cleanCaseName}_Case")
        code.AppendLine($"        Inherits {unionDef.UnionName}{typeParamList}")
        code.AppendLine()
        code.AppendLine($"        ''' <summary>")
        code.AppendLine($"        ''' The underlying {caseType} value")
        code.AppendLine($"        ''' </summary>")
        code.AppendLine($"        Public ReadOnly Property Value As {caseType}")
        code.AppendLine()
        code.AppendLine($"        ''' <summary>")
        code.AppendLine($"        ''' Creates a new {cleanCaseName}_Case")
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
            code.AppendLine($"    ''' Implicit conversion from {caseType} to {unionDef.UnionName}{typeParamList}")
            code.AppendLine($"    ''' </summary>")
            code.AppendLine($"    Public Shared Widening Operator CType(value As {caseType}) As {unionDef.UnionName}{typeParamList}")
            code.AppendLine($"        Return New {cleanCaseName}_Case(value)")
            code.AppendLine($"    End Operator")
            code.AppendLine()
        Next
    End Sub
End Class