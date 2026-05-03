# TaggedUnionVB.Generator

A source generator that creates **tagged union types** for VB.NET, providing type-safe pattern matching capabilities similar to discriminated unions in functional languages.

> **v1.1.0 Major Release**: Introduces record definitions directly in `.union` files and multi-line syntax support. Users can combine records with unions to create more complex types. The package is now production-ready and backward compatible with version 1.0.1.

## Description

This source generator simplifies the creation of tagged union types (also known as "discriminated unions") in VB.NET, by parsing `.union` files and generating the corresponding VB.NET classes with pattern matching capabilities.

The design aligns with Microsoft's planned union keyword syntax for C# 15, where users define their custom case classes or records, and the discriminated union references them directly.

_**With the latest version of this source generator, you can effortlessly write `.union` files for records and discriminated unions in VB.NET, using syntax that closely mirrors the upcoming C# feature:**_
```csharp
public record Just<T>(T Value);
public record Nothing();
public union Maybe<T>(Just<T>, Nothing);
```
### 👇
```vb
Public Record Just(Of T)(Value As T)
Public Record [Nothing]()
Public Union Maybe(Of T)(Just(Of T), [Nothing])
```

## Version Notes: 1.0.1 → 1.1.0

The current version (v1.1.0) adds several new features and is **fully backward compatible** with v1.0.1. Here are some important considerations:

- **Record Limitations**: Record definitions in `.union` files do not support generic constraints. Adding this support would require significant additional complexity. *Records in `.union` files treat **every member** as an equality member, just like C# records.*
- **Advanced Record Features**: For advanced record capabilities (instance methods, `Key` keyword for equality members), consider using the [VB.NET Record Generator](https://github.com/VBAndCs/VB-Record-Source-Generator) (created by [@VBAndCs](https://github.com/VBAndCs)) with `.rec` files as an alternative.
- _**Comments in `.union` Files**: Version 1.0.1 already supports comments in `.union` files, but only full-line comments are valid. Comment flexibility has been greatly improved in the current version._
```vb
' ✅ This is a valid comment in 1.0.1
Public Union NumberUnion(Integer, Single, Double) ' ❌ Invalid in 1.0.1
```

### What's New in 1.1.0?

1. **Record Definitions** - Define records directly in `.union` files:

   ```vb
   Public Record Just(Of T)(Value As T)
   Public Record [Nothing]()
   ```
   Generates constructor, read-only properties, `With()` method for immutable updates, `Deconstruct()` for tuple unpacking, and the equality methods: `ToString()`, `Equals()`, `GetHashCode()`.

2. **Multi-Line Syntax** - Support for multi-line declarations with inline comments:

   ```vb
   Imports System.Numerics

   Public Union VectorUnion(  ' Union of vectors
       Vector2,  ' 2D vector
       Vector3,  ' 3D vector
       Vector4   ' 4D vector
   )
   ```

3. **Combined Records and Unions** - Mix records and unions in a single `.union` file:

   ```vb
   Public Record Just(Of T)(Value As T)
   Public Record [Nothing]()
   Public Union Maybe(Of T)(Just(Of T), [Nothing])
   ```

4. **Enhanced Comment Handling** - Comments are preserved in generated XML documentation while being ignored during parsing.

### Improvements

- Improved line break and whitespace handling
- More robust parsing for complex definitions with type parameters
- Better error tolerance and graceful handling of invalid syntax
- Support for declarations without accessibility modifiers (defaults to package-private)

## Installation

1. Add the source generator as a NuGet package into your VB.NET project:
```bash
dotnet add package TaggedUnionVB.Generator
```
2. Create `.union` files in your project, and set "Build Action" to "Additional Files":
```xml
<ItemGroup>
  <AdditionalFiles Include="**/*.union" />
</ItemGroup>
```
3. _The source generator will automatically create the corresponding VB.NET classes._
4. If you want to build this generator from source, see the [Building from Source](#building-from-source) section.

## Usage

### Creating Union Definitions

Create files with the `.union` extension in your project. These files contain simple union and record definitions, together with namespace imports:

```vb
Imports System.Numerics

' Valid union with existing, imported types
Public Union VectorUnion(Vector2, Vector3, Vector4)

' Define records directly in the .union file
Public Record Just(Of T)(Value As T)
Public Record [Nothing]()

' Union using records from the same file
Public Union Maybe(Of T)(Just(Of T), [Nothing])

' Users can still define custom types in VB.NET and use them
Friend Union Result(Of T, E)(Ok(Of T), [Error](Of E))
```

### Syntax Rules

#### Keywords
- `Imports` - Import required namespaces (optional)
- `Union` - Declare a union type
- `Record` - Declare a record type (NEW in 1.1.0)
- `Public`/`Friend` - Accessibility modifiers (optional, defaults to Public)
- `Of` - Generic type parameters
- `'` or `REM` - Line comments (lines starting with these are ignored)

#### Union Declaration Format
```
[Public|Friend] Union Name[(Of TypeParam1, TypeParam2, ...)](Case1, Case2(Of T), Case3)
```

#### Record Declaration Format
```
[Public|Friend] Record Name[(Of TypeParam1, TypeParam2, ...)](Property1 As Type1, Property2 As Type2, ...)
```

This syntax is for **`.union` files**, and supports:
- primitive types like `Integer`, `String`, `Double`
- existing types from imported namespaces (e.g., `Vector2` from `System.Numerics`)
- **user-defined classes or structs**
- **records defined in the same `.union` file**

Use square brackets around reserved keywords, like `[Nothing]`.

### Generated Code Features

#### For Unions
- **Base abstract class** with pattern matching methods
- **Wrapper classes** for each union variant that hold existing type instances
- **Pattern matching methods** (`IsXxx()`, `AsXxx()`)
- **Implicit conversions** from existing types to union
- **XML documentation comments**

#### For Records (NEW in 1.1.0)
- **Read-only properties** for each declared property
- **Constructor** with all properties as parameters
- **`With()` method** for immutable updates
- **`Deconstruct()` method** for tuple unpacking
- **`ToString()` method** for string representation
- **`Equals()` method** for value-based equality comparison
- **`GetHashCode()` method** for hash-based collections
- **XML documentation comments**

## Example

### Example 1: Union with Predefined Types

#### Input: `.union` File
```vb
Imports System.Numerics

' Multi-line union with comments
Public Union VectorUnion(  ' Union of numeric vectors
    Vector2,  ' 2D vector from System.Numerics
    Vector3,  ' 3D vector
    Vector4   ' 4D vector
)
```

#### Output: Generated VB.NET Classes
```vb
' <auto-generated>
'     This code was generated by `TaggedUnionVB.Generator`
'     Changes to this file may cause incorrect behavior and will be lost if
'     the code is regenerated.
' </auto-generated>

Option Explicit On
Option Strict On

Imports System.Numerics

''' <summary>
''' <para>Tagged Union: VectorUnion</para>
''' <para>From declaration: <c>Public Union VectorUnion(Vector2, Vector3, Vector4)</c></para>
''' </summary>
Public MustInherit Class VectorUnion
    Private Sub New()
    End Sub

    ''' <summary>
    ''' Returns True if this VectorUnion is a Vector2
    ''' </summary>
    Public Function IsVector2() As Boolean
        Return TypeOf Me Is Vector2_Case
    End Function

    ''' <summary>
    ''' Returns the Vector2 value if this is a Vector2, otherwise throws.
    ''' </summary>
    Public Function AsVector2() As Vector2
        Dim wrapper As Vector2_Case = TryCast(Me, Vector2_Case)
        If wrapper Is Nothing Then
            Throw New InvalidOperationException("VectorUnion is not a Vector2.")
        End If
        Return wrapper.Value
    End Function

    ' ... Similar methods for Vector3 and Vector4 ...

    ''' <summary>
    ''' Wrapper for Vector2 in VectorUnion
    ''' </summary>
    Public NotInheritable Class Vector2_Case
        Inherits VectorUnion
        Public ReadOnly Property Value As Vector2
        Public Sub New(value As Vector2)
            Me.Value = value
        End Sub
        Public Shared Widening Operator CType(value As Vector2) As VectorUnion
            Return New Vector2_Case(value)
        End Operator
    End Class

    ' ... Similar wrapper classes for Vector3 and Vector4 ...
End Class
```

### Example 2: Records and Unions Combined (NEW in 1.1.0)

#### Input: `.union` File
```vb
' Define records directly in the .union file
Public Record Just(Of T)(Value As T)
Public Record [Nothing]()

' Union using the records
Public Union Maybe(Of T)(Just(Of T), [Nothing])
```

#### Output: Generated VB.NET Classes

**Record Generation:**
```vb
''' <summary>
''' <para>Record: Just(Of T)</para>
''' <para>From declaration: <c>Public Record Just(Of T)(Value As T)</c></para>
''' </summary>
Public NotInheritable Class Just(Of T)
    ''' <summary>
    ''' The Value value
    ''' </summary>
    Public ReadOnly Property Value As T

    ''' <summary>
    ''' Creates a new Just(Of T)
    ''' </summary>
    Public Sub New(Value As T)
        Me.Value = Value
    End Sub

    ''' <summary>
    ''' Creates a new Just(Of T) with the specified properties changed
    ''' </summary>
    Public Function [With](Optional Value As T = Nothing) As Just(Of T)
        Return New Just(Of T)(
            Value := If(Value IsNot Nothing, Value, Me.Value)
        )
    End Function

    ''' <summary>
    ''' Deconstructs the Just(Of T) into its properties
    ''' </summary>
    Public Sub Deconstruct(<Out> ByRef Value As T)
        Value = Me.Value
    End Sub

    ''' <summary>
    ''' Returns a string representation of the Just(Of T)
    ''' </summary>
    Public Overrides Function ToString() As String
        Return $"Just(Of T) {{ Value = {Value} }}"
    End Function

    ''' <summary>
    ''' Determines whether the specified object is equal to the current Just(Of T)
    ''' </summary>
    Public Overrides Function Equals(obj As Object) As Boolean
        If obj Is Nothing Then Return False
        If TypeOf obj IsNot Just(Of T) Then Return False
        Dim other = DirectCast(obj, Just(Of T))
        Return Object.Equals(Me.Value, other.Value)
    End Function

    ''' <summary>
    ''' Returns the hash code for this Just(Of T)
    ''' </summary>
    Public Overrides Function GetHashCode() As Integer
        Return HashCode.Combine(Me.Value)
    End Function
End Class

''' <summary>
''' <para>Record: [Nothing]</para>
''' <para>From declaration: <c>Public Record [Nothing]()</c></para>
''' </summary>
Public NotInheritable Class [Nothing]
    ''' <summary>
    ''' Creates a new [Nothing]
    ''' </summary>
    Public Sub New()
    End Sub

    ''' <summary>
    ''' Creates a new [Nothing] with the specified properties changed
    ''' </summary>
    Public Function [With]() As [Nothing]
        Return New [Nothing](
        )
    End Function

    ''' <summary>
    ''' Deconstructs the [Nothing] into its properties
    ''' </summary>
    Public Sub Deconstruct()
    End Sub

    ''' <summary>
    ''' Returns a string representation of the [Nothing]
    ''' </summary>
    Public Overrides Function ToString() As String
        Return $"[Nothing] {{ }}"
    End Function

    ''' <summary>
    ''' Determines whether the specified object is equal to the current [Nothing]
    ''' </summary>
    Public Overrides Function Equals(obj As Object) As Boolean
        If obj Is Nothing Then Return False
        If TypeOf obj IsNot [Nothing] Then Return False
        Return True
    End Function

    ''' <summary>
    ''' Returns the hash code for this [Nothing]
    ''' </summary>
    Public Overrides Function GetHashCode() As Integer
        Return GetType([Nothing]).GetHashCode()
    End Function
End Class
```

**Union Generation:**
```vb
''' <summary>
''' <para>Tagged Union: Maybe(Of T)</para>
''' <para>From declaration: <c>Public Union Maybe(Of T)(Just(Of T), [Nothing])</c></para>
''' </summary>
Public MustInherit Class Maybe(Of T)
    Private Sub New()
    End Sub

    ''' <summary>
    ''' Returns True if this Maybe is a Just
    ''' </summary>
    Public Function IsJust() As Boolean
        Return TypeOf Me Is Just_Case
    End Function

    ''' <summary>
    ''' Returns the Just(Of T) value if this is a Just, otherwise throws.
    ''' </summary>
    Public Function AsJust() As Just(Of T)
        Dim wrapper As Just_Case = TryCast(Me, Just_Case)
        If wrapper Is Nothing Then
            Throw New InvalidOperationException("Maybe is not a Just.")
        End If
        Return wrapper.Value
    End Function

    ''' <summary>
    ''' Returns True if this Maybe is a Nothing
    ''' </summary>
    Public Function IsNothing() As Boolean
        Return TypeOf Me Is Nothing_Case
    End Function

    ''' <summary>
    ''' Returns the [Nothing] value if this is a Nothing, otherwise throws.
    ''' </summary>
    Public Function AsNothing() As [Nothing]
        Dim wrapper As Nothing_Case = TryCast(Me, Nothing_Case)
        If wrapper Is Nothing Then
            Throw New InvalidOperationException("Maybe is not a Nothing.")
        End If
        Return wrapper.Value
    End Function

    ' ... Wrapper classes and conversion operators ...
End Class
```

### Usage in Code

```vb
' Create union values using implicit conversion
Dim someValue As Maybe(Of Integer) = New Just(Of Integer)(42)
Dim noValue As Maybe(Of Integer) = New [Nothing]()

' Pattern matching
If someValue.IsJust() Then
    Dim just = someValue.AsJust()
    Console.WriteLine($"Just value: {just.Value}")
End If

If noValue.IsNothing() Then
    Console.WriteLine("Nothing value")
End If

' Use record With() method for immutable updates
Dim updated = New Just(Of Integer)(42).With(Value := 100)

' Use record Deconstruct() for tuple unpacking
Dim (extractedValue) = someValue.AsJust()
```

## Advanced Features

### Generic Unions

Unions can be generic with multiple type parameters:

```vb
Public Union Maybe(Of T)(Just(Of T), [Nothing])
Public Union Result(Of T, E)(Ok(Of T), [Error](Of E))
```

### Generic Records (NEW in 1.1.0)

Records can also be generic:

```vb
Public Record Ok(Of T)(Value As T)
Public Record [Error](Of E)(Message As String, Code As E)
Public Union Result(Of T, E)(Ok(Of T), [Error](Of E))
```

### Existing Type Wrappers

The generator creates wrappers for existing types with implicit conversions:

```vb
Imports System.Numerics

Public Union Shape(Circle(Of Single), Rectangle(Of Single, Single), [Nothing])
```

### Pattern Matching

The generated classes provide type-safe pattern matching through `IsXxx()` and `AsXxx()` methods.

## File Structure

- `.union` files should be included in your project as "Additional Files"
- Generated files are created with `_TaggedUnion.g.vb` suffix
- All unions and records from a single `.union` file are generated into one output file

## Error Handling

The generator includes error handling that will silently skip invalid union/record definitions. Ensure your syntax follows the specification.

## Limitations

- No support for nested unions or recursive definitions
- Reserved keywords used as case/record names must be wrapped in square brackets (e.g., `[Nothing]`)
- Record properties without explicit types default to `Object`

## Building from Source

To build the source generator from its source code, follow these steps:

1. Clone or download the repository:
```bash
git clone https://github.com/Pac-Dessert1436/TaggedUnionVB.Generator.git
```
2. Build the project using Visual Studio or .NET CLI:
```bash
dotnet build
```
3. Reference the built assembly in your target VB.NET project:
```bash
dotnet add path/to/YourProject.vbproj reference TaggedUnionVB.Generator.vbproj
```

## License

This project is licensed under the BSD 3-Clause License. See the [LICENSE](LICENSE) file for details.
